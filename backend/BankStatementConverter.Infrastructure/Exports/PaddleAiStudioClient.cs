using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using Microsoft.Extensions.Configuration;

namespace BankStatementConverter.Infrastructure.Exports;

public sealed class PaddleAiStudioClient(HttpClient http, IConfiguration config, StatementPdfExtractor native, IProcessingProgress? reporter = null, OcrRuntimeSettings? settings = null) : IPdfOcrClient
{
    private const string JobsUrl = "https://paddleocr.aistudio-app.com/api/v2/ocr/jobs";
    public async Task<PdfTextDocument> ExtractPagesAsync(byte[] content, IReadOnlyList<int> pages, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var selection = PdfPageSelection.Create(content, pages);
        var reading = await ExtractAsync(selection.Content, ct);
        if (reading.Pages.Count != pages.Count)
            throw new AppException(502, "PaddleOCR n’a pas retourné toutes les pages demandées.");
        return reading with
        {
            Pages = reading.Pages.Select((page, index) => page with { Number = pages[index] }).ToArray(),
            SourcePageCount = selection.Total
        };
    }
    public async Task<PdfTextDocument> ExtractAsync(byte[] content, CancellationToken ct)
    {
        var source = native.ReadDocument(content, ct);
        var token = settings is null ? config["Ocr:CloudToken"] : settings.CloudToken;
        var model = settings?.CloudModel ?? config["Ocr:CloudModel"] ?? "PaddleOCR-VL-1.6";
        if (model is not ("PaddleOCR-VL-1.6" or "PP-OCRv6"))
            throw new AppException(503, "Ocr:CloudModel doit être PP-OCRv6 ou PaddleOCR-VL-1.6.");
        if (string.IsNullOrWhiteSpace(token))
            throw new AppException(503, "Configurez Ocr:CloudToken côté serveur pour utiliser PaddleOCR.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ExtractionTimeout.Apply(deadline, config, "Ocr:TimeoutSeconds");
        var cancellation = deadline.Token;
        try
        {
            reporter?.Report("queued", 20, 0, source.Pages.Count);
            using var submitted = await Submit(content, model, token, source.Pages.Count, cancellation);
            var jobId = submitted.RootElement.GetProperty("data").GetProperty("jobId").GetString();
            if (string.IsNullOrWhiteSpace(jobId) || jobId.Length > 200)
                throw PaddleDocumentParser.Invalid();
            while (true)
            {
                using var poll = new HttpRequestMessage(HttpMethod.Get, JobsUrl + "/" + Uri.EscapeDataString(jobId));
                poll.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var status = await SendJson(poll, cancellation);
                var data = status.RootElement.GetProperty("data");
                var state = data.GetProperty("state").GetString();
                if (state is "pending" or "running")
                {
                    int? completed = null;
                    if (data.TryGetProperty("extractProgress", out var extractionProgress)
                        && extractionProgress.TryGetProperty("extractedPages", out var pageCount) && pageCount.TryGetInt32(out var n))
                        completed = Math.Clamp(n, 0, source.Pages.Count);
                    reporter?.Report(state == "pending" ? "queued" : "reading", 20 + (completed ?? 0) * 55 / Math.Max(source.Pages.Count, 1), completed, source.Pages.Count);
                }
                if (state == "done")
                {
                    reporter?.Report("collecting", 75, source.Pages.Count, source.Pages.Count);
                    if (data.TryGetProperty("extractProgress", out var progress) && progress.TryGetProperty("extractedPages", out var extracted)
                        && extracted.GetInt32() != source.Pages.Count)
                        throw new AppException(502, "La lecture PaddleOCR est incomplète. Aucun import partiel n’a été enregistré.");
                    var url = data.GetProperty("resultUrl").GetProperty("jsonUrl").GetString();
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var resultUri) || resultUri.Scheme != "https" || resultUri.IsLoopback || resultUri.UserInfo.Length != 0)
                        throw PaddleDocumentParser.Invalid();
                    // Signed result URLs are downloaded without sending the API bearer token.
                    using var download = new HttpRequestMessage(HttpMethod.Get, resultUri);
                    using var response = await http.SendAsync(download, HttpCompletionOption.ResponseHeadersRead, cancellation);
                    CheckStatus(response.StatusCode);
                    var bytes = await ReadBounded(response.Content, 64 * 1024 * 1024, cancellation);
                    var jsonl = System.Text.Encoding.UTF8.GetString(bytes);
                    return model == "PP-OCRv6" ? PaddleTextDocumentParser.Parse(jsonl, source) : PaddleDocumentParser.Parse(jsonl, source);
                }
                if (state == "failed")
                    throw new AppException(422, "PaddleOCR n’a pas pu analyser ce PDF. Vérifiez sa lisibilité puis réessayez.");
                if (state is not ("pending" or "running"))
                    throw PaddleDocumentParser.Invalid();
                await Task.Delay(TimeSpan.FromSeconds(5), cancellation);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new AppException(504, "PaddleOCR a dépassé le délai de lecture. Réessayez avec un PDF plus court."); }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.SecureConnectionError || ex.InnerException is System.Security.Authentication.AuthenticationException)
        {
            throw new AppException(503, "La connexion HTTPS PaddleOCR est bloquée ou son certificat n’est pas valide. Faites vérifier l’accès à paddleocr.aistudio-app.com dans votre filtre réseau ou pare-feu.");
        }
        catch (HttpRequestException) { throw new AppException(503, "L’API PaddleOCR est indisponible. Vérifiez la connexion puis réessayez."); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or IndexOutOfRangeException)
        {
            throw PaddleDocumentParser.Invalid();
        }
    }
    private async Task<JsonDocument> Submit(byte[] content, string model, string token, int pages, CancellationToken ct)
    {
        var retries = Math.Clamp(config.GetValue("Ocr:QueueRetryCount", 3), 0, 5);
        for (var attempt = 0; ; attempt++)
        {
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(content);
            file.Headers.ContentType = new("application/pdf");
            form.Add(file, "file", "releve.pdf");
            form.Add(new StringContent(model), "model");
            form.Add(new StringContent(model == "PP-OCRv6"
                ? """{"useDocOrientationClassify":false,"useDocUnwarping":false,"useTextlineOrientation":false}"""
                : """{"useDocOrientationClassify":false,"useDocUnwarping":false,"useChartRecognition":false}"""), "optionalPayload");
            using var request = new HttpRequestMessage(HttpMethod.Post, JobsUrl) { Content = form };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            try
            {
                return await SendJson(request, ct);
            }
            // Retry only a confirmed rejection before a job was created. A timeout or
            // transport failure may hide an accepted job and must never duplicate it.
            catch (AppException e) when (e.Data["PaddleCode"] is 10010 && attempt < retries)
            {
                reporter?.Report("queued", 20, 0, pages);
                await Task.Delay(TimeSpan.FromSeconds(2 * Math.Pow(2, attempt)), ct);
            }
        }
    }
    private async Task<JsonDocument> SendJson(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var bytes = await ReadBounded(response.Content, 1024 * 1024, ct);
        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(bytes);
        }
        catch (JsonException) { CheckStatus(response.StatusCode); throw PaddleDocumentParser.Invalid(); }
        try
        {
            if (json.RootElement.TryGetProperty("code", out var code) && code.TryGetInt32(out var providerCode) && providerCode != 0)
            {
                var error = ProviderFailure(providerCode);
                error.Data["PaddleCode"] = providerCode;
                throw error;
            }
            CheckStatus(response.StatusCode);
        }
        catch { json.Dispose(); throw; }
        return json;
    }
    // Official AI Studio codes distinguish file errors from service/configuration errors.
    // Never expose the provider's raw errorMsg, which can contain private diagnostics.
    private static AppException ProviderFailure(int code) => code switch
    {
        10001 => new(422, "Le PDF envoyé est vide. Sélectionnez un fichier contenant des pages."),
        10003 => new(422, "Le PDF dépasse la taille acceptée par PaddleOCR. Réduisez sa taille."),
        10004 or 10005 => new(422, "PaddleOCR ne peut pas lire ce PDF. Vérifiez son format et sa lisibilité."),
        10006 => new(422, "Le PDF contient trop de pages pour PaddleOCR. Divisez-le en plusieurs fichiers."),
        10007 => new(503, "Le modèle OCR configuré est refusé par AI Studio. Vérifiez sa disponibilité côté serveur."),
        10008 or 10002 or 10009 => new(502, "AI Studio refuse les paramètres de lecture. Vérifiez la configuration OCR côté serveur."),
        10010 => new(503, "La file d’attente PaddleOCR est pleine. Réessayez dans quelques instants ; ce refus ne signifie pas que votre PDF est invalide."),
        11001 or 11002 => new(503, "Le traitement PaddleOCR a expiré ou est introuvable. Relancez l’import."),
        11003 => new(422, "PaddleOCR n’a pas pu analyser ce PDF. Vérifiez sa lisibilité puis réessayez."),
        12001 => new(429, "Le quota quotidien de pages PaddleOCR est atteint. Vérifiez votre quota AI Studio ou réessayez demain."),
        12002 => new(429, "Trop de demandes PaddleOCR en peu de temps. Attendez quelques instants avant de réessayer."),
        401 => new(503, "Le jeton PaddleOCR a été refusé. Mettez à jour Ocr:CloudToken côté serveur."),
        _ => new(503, "L’API PaddleOCR a refusé la demande. Vérifiez sa disponibilité et votre accès AI Studio.")
    };
    private static void CheckStatus(HttpStatusCode status)
    {
        if ((int)status is >= 200 and < 300)
            return;
        throw status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new AppException(503, "Le jeton PaddleOCR a été refusé. Mettez à jour Ocr:CloudToken côté serveur."),
            HttpStatusCode.TooManyRequests => new AppException(429, "Le quota PaddleOCR est atteint. Réessayez plus tard ou vérifiez votre quota AI Studio."),
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => new AppException(502, "PaddleOCR a refusé la demande sans préciser la cause. Vérifiez la configuration OCR et réessayez."),
            _ => new AppException(503, "L’API PaddleOCR est indisponible. Réessayez plus tard.")
        };
    }
    private static async Task<byte[]> ReadBounded(HttpContent content, int limit, CancellationToken ct)
    {
        if (content.Headers.ContentLength > limit)
            throw PaddleDocumentParser.Invalid();
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (memory.Length + read > limit)
                throw PaddleDocumentParser.Invalid();
            await memory.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return memory.ToArray();
    }
}
