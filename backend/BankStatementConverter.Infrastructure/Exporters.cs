using System.Globalization;
using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using CsvHelper;
using CsvHelper.Configuration;
namespace BankStatementConverter.Infrastructure;

public abstract class TransactionExporter : ITransactionExporter
{
    public abstract string Type { get; }
    public byte[] Export(IEnumerable<BankTransaction> transactions, ExportTemplate template)
    {
        Validation.Require(template.Type == Type, "Stratégie d'export incompatible.");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var culture = CultureInfo.GetCultureInfo(template.NumberCulture);
        using var writer = new StringWriter(culture);
        using var csv = new CsvWriter(writer, new CsvConfiguration(culture) { WhiteSpaceChars = [], Delimiter = template.Delimiter, NewLine = "\r\n" });
        var fields = template.Fields.OrderBy(x => x.Position).ToArray();
        if (template.IncludeHeader) { foreach (var f in fields) csv.WriteField(f.FieldName); csv.NextRecord(); }
        foreach (var t in transactions)
        {
            foreach (var f in fields)
            {
                object? value = f.SourceField switch
                {
                    "Date" => t.Date, "ValueDate" => t.ValueDate, "Journal" => t.Journal, "Account" => t.Account, "Description" => t.Description,
                    "Debit" => t.Debit, "Credit" => t.Credit, "Reference" => t.Reference, "Amount" => Type == "SAGE100" ? Math.Abs(t.Amount) : t.Amount,
                    "Balance" => t.Balance, "Direction" => t.Direction, "Analytic" => t.Analytic,
                    _ => throw new AppException(400, "Champ d'export inconnu.")
                };
                var formatted = value switch { DateOnly d => d.ToString(string.IsNullOrEmpty(f.Format) ? "dd/MM/yyyy" : f.Format, culture), decimal n => n.ToString(string.IsNullOrEmpty(f.Format) ? "0.00" : f.Format, culture), _ => value?.ToString() ?? "" };
                Validation.Require(!f.Required || !string.IsNullOrWhiteSpace(formatted), $"Champ obligatoire absent : {f.FieldName}.");
                csv.WriteField(formatted);
            }
            csv.NextRecord();
        }
        csv.Flush();
        try { return Encoding.GetEncoding(template.Encoding, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetBytes(writer.ToString()); }
        catch (EncoderFallbackException) { throw new AppException(400, "Caractère non représentable : choisissez UTF-8."); }
    }
}
public class Sage100Exporter : TransactionExporter { public override string Type => "SAGE100"; }
public class SageX3Exporter : TransactionExporter { public override string Type => "SAGEX3"; }
public class CustomCsvExporter : TransactionExporter { public override string Type => "CUSTOMCSV"; }
