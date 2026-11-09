using System.Text.RegularExpressions;

namespace BankStatementConverter.Infrastructure.Exports;

public sealed partial class StatementPdfExtractor
{
    // Captions describe columns, even when OCR puts each caption on a different line.
    // Require both semantic headings and dated rows underneath; never classify by amounts alone.
    private static Header? FindMultilineHeader(IReadOnlyList<Token> words)
    {
        bool Operation(Token token) => Has(token.Text, @"^OP.?RATIONS?$|^BOOKING$|^POSTING$")
            || token.Text.Trim() is "العملية" or "عملية";
        bool ParentDate(Token token) => Has(token.Text, @"^DATE(?:\s|$)|^DATES$")
            || token.Text.Trim() is "التاريخ" or "تاريخ";
        bool DatedRows(Token caption, double below) => words.Count(w => w.Y < below - 8
            && Math.Abs(w.Left - caption.Left) < 28
            && Regex.IsMatch(w.Text, @"^\d{1,2}(?:[/.-]|\s+)\d{1,2}(?:(?:[/.-]|\s+)\d{2,4})?$|^\d{4}[-/.]\d{1,2}[-/.]\d{1,2}$", RegexOptions.None, RegexTimeout)) >= 1;

        foreach (var debit in words.Where(w => StatementFieldNames.Column(w.Text) == "Debit").OrderByDescending(w => w.Y))
        {
            var nearby = words.Where(w => Math.Abs(w.Y - debit.Y) <= 18).OrderBy(w => w.Left).ToArray();
            var credit = nearby.FirstOrDefault(w => w.Left != debit.Left && StatementFieldNames.Column(w.Text) == "Credit");
            var description = nearby.FirstOrDefault(w => w.Left < Math.Min(debit.Left, credit?.Left ?? debit.Left)
                && StatementFieldNames.Column(w.Text) == "Description" && !Operation(w));
            if (credit == null || description == null)
                continue;
            var dates = nearby.Where(w => w.Left < description.Left && StatementFieldNames.Column(w.Text) == "Date").ToArray();
            var value = nearby.FirstOrDefault(w => w.Left < description.Left && StatementFieldNames.Column(w.Text) == "ValueDate");
            var operation = nearby.FirstOrDefault(w => w.Left < (value?.Left ?? description.Left) && Operation(w));
            var date = dates.FirstOrDefault();
            if (operation != null && value != null && words.Any(w => ParentDate(w)
                && w.Y > operation.Y + 3 && w.Y < operation.Y + 45
                && w.Left >= operation.Left - 20 && w.Left <= value.Right))
                date = operation;
            value ??= dates.Skip(1).FirstOrDefault();
            if (date == null || !DatedRows(date, nearby.Min(w => w.Y)))
                continue;
            // A value-date column may legitimately be empty; its explicit caption still defines it.
            if (value != null && (value.Left <= date.Left || value.Left >= description.Left))
                continue;
            var reference = nearby.FirstOrDefault(w => StatementFieldNames.Column(w.Text) == "Reference");
            var captions = new[] { date, value, reference, description, debit, credit }.Where(w => w != null).Select(w => w!);
            return new(captions.Min(w => w.Y), date, value, reference, description, debit, credit);
        }
        return null;
    }
}
