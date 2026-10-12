using System.Globalization;
using System.Text;
using BankStatementConverter.Domain;
namespace BankStatementConverter.Infrastructure.Exports;

// This inspection file retains the original descriptions and separate debit/credit columns.
public static class StandardStatementCsv
{
    public static byte[] Generate(BankStatement statement)
    {
        static string Number(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";
        var rows = statement.Transactions.Select(t => new[] { t.Date?.ToString("yyyy-MM-dd") ?? "", t.ValueDate?.ToString("yyyy-MM-dd") ?? "",
            t.Description ?? "", Number(t.Debit), Number(t.Credit), Number(t.Amount), t.Direction ?? "", t.Reference ?? "", t.Currency ?? statement.Currency ?? "" }).ToList();
        return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(new CsvExportWriter().Write(
            ["Date opération", "Date valeur", "Libellé", "Débit", "Crédit", "Montant", "Sens", "Référence", "Devise"], rows, ";", true))).ToArray();
    }
}
