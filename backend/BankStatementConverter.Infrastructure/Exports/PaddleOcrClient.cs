using System.Net.Http.Json;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using Microsoft.Extensions.Configuration;

namespace BankStatementConverter.Infrastructure.Exports;

public sealed class PaddleOcrClient(HttpClient http, IConfiguration config, OcrRuntimeSettings? settings = null) : IPdfOcrClient
{
    public async Task<PdfTextDocument> ExtractAsync(byte[] content, CancellationToken ct)
        => await ReadAsync(content, null, ct);

    public Task<PdfTextDocument> ExtractPagesAsync(byte[] content, IReadOnlyList<int> pages, CancellationToken ct)
        => ReadAsync(content, pages, ct);

    private async Task<PdfTextDocument> ReadAsync(byte[] content, IReadOnlyList<int>? pages, CancellationToken ct)
    {
        var address = settings?.BaseUrl ?? config.GetValue<string>("Ocr:BaseUrl") ?? "http://127.0.0.1:8091";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ExtractionTimeout.Apply(deadline, config, "Ocr:TimeoutSeconds");
        try
        {
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(content);
            file.Headers.ContentType = new("application/pdf");
            form.Add(file, "file", "releve.pdf");
            if (pages != null)
                form.Add(new StringContent(string.Join(',', pages)), "pages");
            using var response = await http.PostAsync(address.TrimEnd('/') + "/extract", form, deadline.Token);
            if (!response.IsSuccessStatusCode)
                throw new AppException(response.StatusCode is System.Net.HttpStatusCode.UnprocessableEntity or System.Net.HttpStatusCode.BadRequest ? 422 : 503,
                "Le moteur OCR n’a pas pu lire ce PDF. Vérifiez qu’il est déverrouillé et lisible.");
            var document = await response.Content.ReadFromJsonAsync<PdfTextDocument>(cancellationToken: deadline.Token);
            if (document == null || document.Pages is not { Count: > 0 } || document.Pages.Any(p => p == null || p.Boxes == null || p.Boxes.Count > 100000
                || !double.IsFinite(p.Width) || !double.IsFinite(p.Height) || p.Width <= 0 || p.Height <= 0
                || p.Boxes.Any(b => b == null || b.Text == null || b.Text.Length > 10000 || !double.IsFinite(b.Left) || !double.IsFinite(b.Right) || !double.IsFinite(b.Top) || !double.IsFinite(b.Bottom)
                    || b.Left > b.Right || b.Top > b.Bottom || (b.Confidence.HasValue && !double.IsFinite(b.Confidence.Value)) || b.Confidence is < 0 or > 1))
                || document.Pages.Sum(p => p.Boxes.Count) > 500000)
                throw new AppException(502, "La réponse du moteur OCR est invalide.");
            if (document.Pages.Select(page => page.Number).Distinct().Count() != document.Pages.Count
                || pages != null && !pages.Order().SequenceEqual(document.Pages.Select(page => page.Number).Order()))
                throw new AppException(502, "Le moteur OCR n’a pas retourné les pages demandées. Mettez à jour le service OCR local.");
            return document with
            {
                Method = "PADDLE_OCR"
            };
        }
        catch (HttpRequestException) { throw new AppException(503, "Le moteur OCR local est indisponible. Démarrez services/ocr/start.ps1 puis réessayez."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new AppException(504, "La lecture OCR a dépassé le délai. Réessayez avec un PDF plus court."); }
        catch (System.Text.Json.JsonException) { throw new AppException(502, "La réponse du moteur OCR est invalide."); }
    }
}
