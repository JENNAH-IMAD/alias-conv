using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
using Microsoft.Extensions.Configuration;
namespace BankStatementConverter.Infrastructure.Exports;

public sealed class AutomaticPdfExtraction(StatementPdfExtractor native, IPdfOcrClient ocr, IConfiguration? config = null, IProcessingProgress? progress = null) : IAutomaticPdfExtraction
{
    public async Task<PdfStatementExtraction> ExtractAsync(byte[] content, string bankName, string bankCode, string currency, CancellationToken ct)
    {
        progress?.Report("reading", 20);
        var document = native.ReadDocument(content, ct);
        progress?.Report("reading", 20, 0, document.Pages.Count);
        var nativePages = document.Pages;
        PdfStatementExtraction? nativeResult = null;
        if (!ocr.RequiresProviderReading && !string.Equals(config?["Ocr:PreferNativeText"], "false", StringComparison.OrdinalIgnoreCase) && document.Pages.All(p => p.Boxes.Count >= 10 && !p.RequiresImageReading && !p.HasUnreliableText))
        {
            try
            {
                progress?.Report("adapting", 25);
                nativeResult = native.Normalize(document, bankName, bankCode, currency, ct);
                if (PdfExtractionQuality.Defects(nativeResult) == 0)
                {
                    progress?.Report("adapting", 80, document.Pages.Count, document.Pages.Count);
                    return nativeResult;
                }
            }
            catch (AppException e) when (e.Status == 422 && !e.Message.Contains("devise", StringComparison.OrdinalIgnoreCase)) { }
        }
        progress?.Report("reading", 25, 0, document.Pages.Count);
        try
        {
            document = await ocr.ExtractAsync(content, ct);
        }
        catch (AppException) when (nativeResult != null)
        {
            // Keep the original, unvalidated reading when the second engine is unavailable.
            // Never turn a provider outage into a validated or silently completed statement.
            return nativeResult with
            {
                Warnings = nativeResult.Warnings.Concat(new[] {new ExportError("EXTRACTION_RECHECK_UNAVAILABLE",
                "La seconde lecture automatique est indisponible. Les champs non reconnus restent à vérifier avec le PDF original.")}).ToList()
            };
        }
        if (document.Pages.All(p => p.Boxes.Count == 0))
        {
            if (nativeResult != null)
                return nativeResult;
            throw new AppException(422, "Aucun texte lisible n’a été trouvé par le moteur OCR.");
        }
        if (nativePages.Count == document.Pages.Count && nativePages.All(p => p.Boxes.Count >= 10))
            document = document with
            {
                NativePages = nativePages
            };
        PdfStatementExtraction result;
        progress?.Report("adapting", 80, document.Pages.Count, document.Pages.Count);
        try
        {
            result = native.Normalize(document, bankName, bankCode, currency, ct);
        }
        catch (AppException e) when (e.Status == 422 && !e.Message.Contains("devise", StringComparison.OrdinalIgnoreCase))
        {
            result = new(new BankStatement { BankName = bankName, BankCode = bankCode, Currency = currency },
                [new("EXTRACTION_MAPPING_REQUIRED", "Le document a été lu, mais les mouvements n’ont pas été reconnus automatiquement. Vérifiez le PDF original et les données avant l’export.")], document);
            result = PdfStatementMetadata.Complete(result);
        }
        if (nativeResult != null)
        {
            if (!PdfExtractionQuality.Improves(result, nativeResult))
                result = nativeResult with
                {
                    RawDocument = document
                };
            result = result with
            {
                Warnings = result.Warnings.Concat(new[] {new ExportError("EXTRACTION_AUTOMATIC_RECHECK",
                "Les dates, montants et contrôles du relevé ont déclenché une seconde lecture automatique. Vérifiez les champs encore signalés avant validation.")}).ToList()
            };
        }
        var uncertain = document.Pages.Sum(p => p.Boxes.Count(b => b.Confidence < 0.90));
        var review = document.Method == "OPENROUTER"
            ? "Transcription OpenRouter : vérifiez chaque ligne, colonne, date, montant et solde avec le PDF original. Aucune confiance chiffrée ni exhaustivité financière n’est garantie."
            : document.Method == "PADDLE_OCR_VL_1_6"
            ? "Lecture structurée PaddleOCR-VL-1.6 : vérifiez toutes les dates, montants et soldes avec le PDF original. Ce résultat ne fournit pas de score de confiance du texte."
            : $"Lecture PaddleOCR : vérifiez les dates, montants et soldes avec le PDF original. {uncertain} bloc(s) de texte ont une confiance inférieure à 90 %.";
        result = result with
        {
            Warnings = result.Warnings.Concat(new[] { new ExportError("OCR_REVIEW_REQUIRED", review) }).ToList()
        };
        return result;
    }
}
