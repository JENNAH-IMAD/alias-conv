using System.Globalization;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace BankStatementConverter.Infrastructure.Exports;

// Generated directly from the saved, reviewed statement data.
public static class NormalizedStatementPdf
{
    private sealed class FontResolver : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new("AliasNotoSans");
        public byte[] GetFont(string faceName)
        {
            using var stream = typeof(NormalizedStatementPdf).Assembly.GetManifestResourceStream("BankStatementConverter.Infrastructure.Exports.Fonts.NotoSans-Regular.ttf")!;
            using var data = new MemoryStream();
            stream.CopyTo(data);
            return data.ToArray();
        }
    }
    private static readonly Lazy<bool> Fonts = new(() => { GlobalFontSettings.FontResolver ??= new FontResolver(); return true; });
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("fr-FR");
    public static byte[] Generate(BankStatement statement, bool validated)
    {
        _ = Fonts.Value;
        if (statement.Transactions.Count == 0)
            throw new AppException(422, "Aucun mouvement reconnu : adaptez la lecture avant de générer le PDF structuré.");
        using var document = new PdfDocument();
        document.Info.Title = statement.ConversionType == "EXTRACT" ? "Extrait de relevé bancaire" : "Relevé bancaire structuré";
        var options = new XPdfFontOptions(PdfFontEncoding.Unicode, PdfFontEmbedding.EmbedCompleteFontFile);
        var font = new XFont("AliasNotoSans", 9, XFontStyleEx.Regular, options);
        // Share one font instance: all page headers, cells and footers use the same embedded glyph set.
        var title = font;
        var small = font;
        XGraphics? graphics = null;
        double y = 0;
        double[] starts = [32, 132, 232, 602, 706];
        double[] widths = [100, 100, 370, 104, 104];
        string Money(decimal? value) => value?.ToString("N2", Culture) ?? "Non déterminé";
        string Balance(decimal? value) => value.HasValue ? Money(Math.Abs(value.Value)) + (value.Value < 0 ? " · Crédit" : " · Débit") : "Non déterminé";
        void Text(string value, double x, double top, XFont? f = null) => graphics!.DrawString(value, f ?? font, XBrushes.Black, x, top + (f ?? font).Size);
        void Page()
        {
            graphics?.Dispose();
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = PdfSharp.PageOrientation.Landscape;
            graphics = XGraphics.FromPdfPage(page);
            Text(statement.ConversionType == "EXTRACT" ? "Extrait de relevé bancaire" : "Relevé bancaire structuré", 32, 22, title);
            var account = $"Compte : {statement.AccountNumber ?? "Non déterminé"} | Période : {statement.PeriodStart:dd/MM/yyyy} — {statement.PeriodEnd:dd/MM/yyyy} | Devise : {statement.Currency}";
            if (graphics.MeasureString(account, small).Width > 775)
                throw new AppException(422, "Informations du compte trop longues pour le PDF normalisé.");
            Text(account, 32, 48, small);
            Text(validated ? "Relevé validé" : "À vérifier — données non validées", 32, 64, small);
            y = 86;
            if (document.PageCount == 1)
            {
                var bank = $"Banque : {statement.BankName} | N° relevé : {statement.StatementNumber ?? "Non déterminé"}";
                foreach (var line in Wrap(bank, 775))
                {
                    Text(line, 32, y);
                    y += 13;
                }
                foreach (var line in Wrap("Titulaire : " + (statement.AccountHolder ?? "Non déterminé"), 775))
                {
                    Text(line, 32, y);
                    y += 13;
                }
                if (y > 180)
                    throw new AppException(422, "Les informations du relevé sont trop longues. Vérifiez les champs avant de générer le PDF.");
                Text((statement.ConversionType == "EXTRACT" ? "Base de calcul de l’extrait : " : "Solde de départ du relevé : ") + Balance(statement.OpeningBalance) + " " + statement.Currency, 32, y + 4);
                y += 30;
            }
            graphics.DrawRectangle(XBrushes.LightGray, 32, y, 778, 24);
            string[] headers = ["Date opération", "Date valeur", "Libellé", "Débit", "Crédit"];
            for (var i = 0; i < 5; i++)
                Text(headers[i], starts[i] + 6, y + 5);
            y += 24;
        }
        List<string> Wrap(string text, double width)
        {
            var lines = new List<string>();
            foreach (var paragraph in text.Replace("\r", "").Split('\n'))
            {
                var line = "";
                foreach (var ch in paragraph)
                {
                    if (graphics!.MeasureString(line + ch, font).Width > width && line.Length > 0)
                    {
                        lines.Add(line);
                        line = "";
                    }
                    line += ch;
                }
                lines.Add(line);
            }
            return lines;
        }
        Page();
        foreach (var row in statement.Transactions)
        {
            var lines = Wrap(row.Description ?? "À vérifier", widths[2] - 12);
            var height = Math.Max(27, lines.Count * 13 + 12);
            if (height > 425)
                throw new AppException(422, $"Le libellé du mouvement {row.Sequence} dépasse une page. Corrigez-le avant de générer le PDF ; aucun texte n'a été tronqué.");
            if (y + height > 535)
                Page();
            graphics!.DrawRectangle(row.Sequence % 2 == 0 ? XBrushes.WhiteSmoke : XBrushes.White, 32, y, 778, height);
            Text(row.Date?.ToString("dd/MM/yyyy") ?? "À vérifier", starts[0] + 6, y + 6);
            Text(row.ValueDate?.ToString("dd/MM/yyyy") ?? "", starts[1] + 6, y + 6);
            for (var i = 0; i < lines.Count; i++)
                Text(lines[i], starts[2] + 6, y + 6 + i * 13);
            for (var i = 0; i < 2; i++)
            {
                var amount = i == 0 ? row.Debit : row.Credit;
                if (!amount.HasValue)
                    continue;
                var text = Money(amount);
                var measured = graphics.MeasureString(text, font).Width;
                if (measured > widths[i + 3] - 12)
                    throw new AppException(422, "Montant trop long pour le PDF normalisé.");
                Text(text, starts[i + 3] + widths[i + 3] - measured - 6, y + 6);
            }
            y += height;
        }
        if (y + 65 > 535)
            Page();
        var debit = statement.Transactions.Sum(t => t.Debit ?? 0);
        var credit = statement.Transactions.Sum(t => t.Credit ?? 0);
        Text($"Total débits : {Money(debit)} | Total crédits : {Money(credit)}", 32, y + 12);
        Text((statement.ConversionType == "EXTRACT" ? "Solde de l’extrait : " : "Solde final du relevé : ") + Balance(statement.ClosingBalance) + " " + statement.Currency, 32, y + 34);
        graphics!.Dispose();
        for (var i = 0; i < document.PageCount; i++)
        {
            using var footer = XGraphics.FromPdfPage(document.Pages[i], XGraphicsPdfPageOptions.Append);
            footer.DrawString("Copie structurée du document source", small, XBrushes.Gray, 32, 575);
            footer.DrawString($"Page {i + 1} / {document.PageCount}", small, XBrushes.Gray, 735, 575);
        }
        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }
}
