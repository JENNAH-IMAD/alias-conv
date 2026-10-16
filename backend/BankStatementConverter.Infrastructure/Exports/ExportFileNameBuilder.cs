using System.Globalization;
using System.Text;
using BankStatementConverter.Domain;
namespace BankStatementConverter.Infrastructure.Exports;

public sealed class ExportFileNameBuilder
{
    public string Build(BankStatement statement, ExportTemplate template)
    {
        string Safe(string? value)
        {
            var text = new StringBuilder();
            foreach (var character in value ?? "")
                if (char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
                    text.Append(character);
            return text.Length == 0 ? "Alias" : text.ToString()[..Math.Min(text.Length, 40)];
        }
        var digits = new string((statement.AccountNumber ?? "").Where(char.IsAsciiDigit).ToArray());
        var suffix = digits.Length > 4 ? digits[^4..] : "compte";
        var bankCode = Safe(statement.BankCode);
        var account = new string((statement.AccountNumber ?? "").Where(char.IsAsciiLetterOrDigit).ToArray());
        // Treat a misplaced account identifier as private, including one supplied as BankCode.
        if (bankCode.Count(char.IsAsciiDigit) > 4 || (account.Length > 0 && bankCode.Contains(account, StringComparison.OrdinalIgnoreCase)))
            bankCode = "Alias";
        var period = statement.PeriodStart?.ToString("yyyy-MM", CultureInfo.InvariantCulture) ?? "sans-periode";
        // No user-supplied label or account identifier can introduce a path or disclose a complete account.
        return $"{bankCode}_{suffix}_{period}_{Safe(template.Type)}.{(template.FileExtension == "csv" ? "csv" : "txt")}";
    }
}
