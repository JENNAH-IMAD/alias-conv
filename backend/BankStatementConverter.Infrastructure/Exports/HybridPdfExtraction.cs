using System.Diagnostics;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
using Microsoft.Extensions.Configuration;

namespace BankStatementConverter.Infrastructure.Exports;

public sealed class HybridPdfExtraction(StatementPdfExtractor native, IPdfOcrClient paddle, IPdfOcrClient? ai,
    IConfiguration config, IProcessingProgress? progress = null) : IAutomaticPdfExtraction
{
    public async Task<PdfStatementExtraction> ExtractAsync(byte[] content, string bankName, string bankCode, string currency, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var source = native.ReadDocument(content, ct);
        var evidence = source;
        var pages = source.Pages.Select(page => page with { ReadingMethod = "NATIVE_PDF" }).ToArray();
        var warnings = new List<ExportError>();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ExtractionTimeout.Apply(budget, config, "Hybrid:TimeoutSeconds");
        progress?.Report("reading", 20, 0, pages.Length);
        PdfStatementExtraction? initial = null;
        try
        {
            initial = native.Normalize(source, bankName, bankCode, currency, ct);
        }
        catch (AppException error) when (HybridStatementNormalizer.CanReview(error))
        {
            // A cover page may have no movement header; still try the other native pages.
            initial = HybridStatementNormalizer.Normalize(native, source with
            {
                Method = "HYBRID",
                Pages = pages
            }, bankName, bankCode, currency, ct);
            if (initial.Statement.Transactions.Count == 0)
                initial = null;
        }
        if (initial != null && pages.All(page => page.Boxes.Count >= 10 && !page.RequiresImageReading && !page.HasUnreliableText) && PdfExtractionQuality.Defects(initial) == 0
            && ExtractionAudit.Errors(initial.Statement, source).Count == 0)
            return Finish(initial, pages, clock, warnings);

        var context = PdfStatementMetadata.Complete(new(new BankStatement { BankName = bankName, BankCode = bankCode, Currency = currency }, [], source)).Statement;
        var pending = pages.Where(page => NeedsReading(page, context, pages.Length, ct)).Select(page => page.Number).ToArray();
        // A document-level imbalance cannot always be localized. Compare all pages without overwriting a sound reading.
        if (pending.Length == 0 && (initial == null || PdfExtractionQuality.Defects(initial) > 0))
        {
            pending = pages.Select(page => page.Number).ToArray();
            if (initial == null)
                pages = pages.Select(page => page with { HasUnreliableText = true }).ToArray();
        }
        progress?.Report("reading", 25, pages.Length - pending.Length, pages.Length);
        if (pending.Length > 0)
        {
            using var paddleBudget = CancellationTokenSource.CreateLinkedTokenSource(budget.Token);
            ExtractionTimeout.Apply(paddleBudget, config, "Hybrid:PaddleTimeoutSeconds");
            try
            {
                var reading = await paddle.ExtractPagesAsync(content, pending, paddleBudget.Token);
                evidence = source with
                {
                    Pages = source.Pages.Select(page => reading.Pages.FirstOrDefault(candidate => candidate.Number == page.Number) ?? page).ToArray()
                };
                Apply(reading, pending, pages, context, warnings, ct);
            }
            catch (AppException error) when (error.Status is 429 or 502 or 503 or 504 || HybridStatementNormalizer.CanReview(error))
            {
                warnings.Add(new("EXTRACTION_PADDLE_UNAVAILABLE", "La lecture PaddleOCR n’a pas pu terminer. Le mode automatique conserve la lecture locale et essaie les pages restantes avec l’assistance configurée."));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                warnings.Add(new("EXTRACTION_PADDLE_TIMEOUT", "Le budget PaddleOCR est atteint. Les pages non résolues restent à vérifier."));
            }
        }

        var remaining = pages.Where(page => NeedsReading(page, context, pages.Length, ct)).Select(page => page.Number).ToArray();
        // Process every unresolved page unless an administrator explicitly sets a cap.
        var maxAiPages = config.GetValue("Hybrid:MaxAiPages", 0);
        var aiPages = maxAiPages > 0 ? remaining.Take(maxAiPages).ToArray() : remaining;
        if (ai != null && aiPages.Length > 0 && !budget.IsCancellationRequested)
        {
            try
            {
                var reading = await ai.ExtractPagesWithContextAsync(content, aiPages, evidence, budget.Token);
                Apply(reading, aiPages, pages, context, warnings, ct);
            }
            catch (AppException error) when (error.Status is 429 or 502 or 503 or 504 || HybridStatementNormalizer.CanReview(error))
            {
                warnings.Add(new("EXTRACTION_AI_UNAVAILABLE", "L’assistance gratuite est indisponible ou a atteint son délai/quota. La lecture obtenue reste conservée et doit être vérifiée."));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                warnings.Add(new("EXTRACTION_TIME_BUDGET", "Le budget de conversion est atteint. Les données déjà lues sont conservées ; les pages non reconnues restent signalées avant validation."));
            }
        }
        ct.ThrowIfCancellationRequested();
        var document = new PdfTextDocument("HYBRID", pages, SourcePageCount: pages.Length);
        var result = native.Normalize(document, bankName, bankCode, currency, ct);
        if (initial != null && ExtractionAudit.Errors(initial.Statement, source).Count == 0
            && !PdfExtractionQuality.Improves(result, initial) && initial.Statement.Transactions.Count > 0)
        {
            // A cleaner-looking, shorter table must not replace a fuller reading without independent evidence.
            result = initial;
            pages = source.Pages.Select(page => page with { ReadingMethod = "NATIVE_PDF" }).ToArray();
        }
        if (pages.Any(page => NeedsReading(page, context, pages.Length, ct)))
            warnings.Add(new("EXTRACTION_REVIEW_REQUIRED", "Certaines pages nécessitent un contrôle. Comparez les mouvements et le texte source au PDF original avant de valider ou d’exporter."));
        warnings.AddRange(ExtractionAudit.EvidenceWarnings(result.Statement, new("HYBRID", pages), evidence));
        return Finish(result, pages, clock, warnings);
    }

    private bool NeedsReading(PdfTextPage page, BankStatement context, int total, CancellationToken ct)
    {
        if (page.RequiresImageReading || page.HasUnreliableText || page.Boxes.Count == 0 || page.Boxes.Any(box => box.Confidence < 0.85))
            return true;
        return ExtractionAudit.RowDefects(HybridStatementNormalizer.ReadPage(native, page, total, context, ct), page) > 0;
    }

    private void Apply(PdfTextDocument reading, IReadOnlyList<int> requested, PdfTextPage[] pages, BankStatement context,
        List<ExportError> warnings, CancellationToken ct)
    {
        if (requested.Any(number => reading.Pages.Count(page => page.Number == number) != 1))
            throw new AppException(502, "Le moteur n’a pas retourné toutes les pages demandées.");
        foreach (var number in requested)
        {
            var current = pages[number - 1];
            var candidate = reading.Pages.Single(page => page.Number == number) with
            {
                ReadingMethod = reading.Method,
                RequiresImageReading = false,
                HasUnreliableText = false
            };
            var before = current.HasUnreliableText ? null : HybridStatementNormalizer.ReadPage(native, current, pages.Length, context, ct);
            var after = HybridStatementNormalizer.ReadPage(native, candidate, pages.Length, context, ct);
            var oldRows = before?.Statement.Transactions ?? [];
            var newRows = after?.Statement.Transactions ?? [];
            var oldDefects = ExtractionAudit.RowDefects(before, current);
            var newDefects = ExtractionAudit.RowDefects(after, candidate);
            if (oldRows.Count > 0 && newRows.Count == oldRows.Count && oldDefects == 0 && newDefects == 0
                && !oldRows.Select(row => (row.Date, row.Debit, row.Credit)).SequenceEqual(newRows.Select(row => (row.Date, row.Debit, row.Credit))))
            {
                warnings.Add(new("EXTRACTION_ENGINE_DISAGREEMENT", $"Les moteurs ne donnent pas les mêmes dates ou montants pour la page {number}. La première lecture est conservée ; contrôlez cette page avec l’original."));
                continue;
            }
            if (candidate.Boxes.Count > 0 && (current.Boxes.Count == 0 || oldRows.Count == 0 && newRows.Count > 0
                || newRows.Count >= oldRows.Count && newDefects < oldDefects
                || (current.RequiresImageReading || current.HasUnreliableText) && newDefects == 0 && newRows.Count >= oldRows.Count
                    && ContainsFinancialReading(oldRows, newRows)
                || before == null && after == null && candidate.Boxes.Sum(box => box.Text.Length) > current.Boxes.Sum(box => box.Text.Length)))
                pages[number - 1] = candidate;
        }
        progress?.Report("reading", 70, pages.Count(page => page.Boxes.Count > 0), pages.Length);
    }

    private static bool ContainsFinancialReading(IReadOnlyList<StatementTransaction> original, IReadOnlyList<StatementTransaction> candidate)
    {
        var next = 0;
        foreach (var row in candidate)
        {
            if (next < original.Count && (row.Date, row.ValueDate, row.Debit, row.Credit)
                == (original[next].Date, original[next].ValueDate, original[next].Debit, original[next].Credit))
                next++;
        }
        return next == original.Count;
    }

    private PdfStatementExtraction Finish(PdfStatementExtraction result, PdfTextPage[] pages, Stopwatch clock, List<ExportError> warnings)
    {
        var performance = new ExtractionPerformance(clock.ElapsedMilliseconds,
            pages.Count(page => page.ReadingMethod == "NATIVE_PDF"), pages.Count(page => page.ReadingMethod?.StartsWith("PADDLE", StringComparison.Ordinal) == true),
            pages.Count(page => page.ReadingMethod == "OPENROUTER"));
        var document = new PdfTextDocument("HYBRID", pages, SourcePageCount: pages.Length, Performance: performance);
        progress?.Report("adapting", 80, pages.Count(page => page.Boxes.Count > 0), pages.Length);
        var checks = ExtractionAudit.Errors(result.Statement, document);
        return result with
        {
            Statement = result.Statement with
            {
                IsValidated = false
            },
            RawDocument = document,
            Warnings = result.Warnings.Concat(warnings).Concat(checks).DistinctBy(error => (error.Code, error.Message)).ToArray()
        };
    }
}
