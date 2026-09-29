using System.Text.RegularExpressions;

namespace BankStatementConverter.Infrastructure;

public static class ExportFileName
{
    public static string Create(string bank, string account, DateTime date, Guid id, string extension)
    {
        static string Part(string value)
        {
            var safe = Regex.Replace(value.Trim(), @"[^\p{L}\p{N}_-]+", "_").Trim('_');
            return safe.Length == 0 ? "releve" : safe[..Math.Min(safe.Length, 45)];
        }
        if (extension is not ("csv" or "txt" or "xlsx")) throw new ArgumentException("Extension invalide.", nameof(extension));
        return $"{Part(bank)}_{Part(account)}_{date:yyyy-MM-dd}_{id.ToString("N")[..8]}.{extension}";
    }
}
