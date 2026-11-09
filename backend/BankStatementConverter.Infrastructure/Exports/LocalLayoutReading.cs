using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Exports;

public sealed partial class StatementPdfExtractor
{
    // Compare two interpretations of this same fresh OCR reading, without another provider.
    private PdfStatementExtraction? TryLocalLayoutReading(PdfTextDocument document, PdfStatementExtraction? cells,
        string bankName, string bankCode, string currency, CancellationToken ct, BankStatement? metadata)
    {
        if (document.PipelineVersion != "local-layout-v1" || document.Pages.Any(p => p.Boxes.Count == 0))
            return null;
        var positionalDocument = document with
        {
            Pages = document.Pages.Select(p => p with { Tables = null, Boxes = p.Boxes.Select(b => b with { Table = null }).ToArray() }).ToArray()
        };
        PdfStatementExtraction positions;
        try
        {
            positions = Normalize(positionalDocument, bankName, bankCode, currency, ct, metadata: metadata);
        }
        catch (AppException error) when (HybridStatementNormalizer.CanReview(error)) { return cells; }
        var sameFinancialRows = cells != null && cells.Statement.Transactions.Select(t => (t.Date, t.ValueDate, t.Debit, t.Credit))
            .SequenceEqual(positions.Statement.Transactions.Select(t => (t.Date, t.ValueDate, t.Debit, t.Credit)));
        static int Balances(BankStatement s) => (s.OpeningBalance.HasValue ? 1 : 0) + (s.ClosingBalance.HasValue ? 1 : 0);
        if (cells == null || PdfExtractionQuality.Improves(positions, cells)
            || sameFinancialRows && PdfExtractionQuality.Defects(positions) == 0 && Balances(positions.Statement) > Balances(cells.Statement))
            return positions with
            {
                Warnings = positions.Warnings.Concat(new[] { new ExportError("LOCAL_LAYOUT_POSITIONAL_RECOVERY",
                    "La reconstruction des cellules était moins complète. Les mouvements proviennent des positions du texte de cette même lecture du PDF original.") }).ToArray()
            };
        if (PdfExtractionQuality.Defects(cells) == 0 && PdfExtractionQuality.Defects(positions) == 0
            && (!sameFinancialRows
                || cells.Statement.OpeningBalance.HasValue && positions.Statement.OpeningBalance.HasValue
                    && cells.Statement.OpeningBalance != positions.Statement.OpeningBalance
                || cells.Statement.ClosingBalance.HasValue && positions.Statement.ClosingBalance.HasValue
                    && cells.Statement.ClosingBalance != positions.Statement.ClosingBalance))
            return cells with
            {
                Warnings = cells.Warnings.Concat(new[] { new ExportError("EXTRACTION_ENGINE_DISAGREEMENT",
                    "Les cellules et les positions du texte donnent des mouvements différents. Comparez les valeurs au PDF original avant de confirmer la lecture.") }).ToArray()
            };
        return cells;
    }
}
