using System.Text;
using BankStatementConverter.Application.Exports;

namespace BankStatementConverter.Infrastructure.Exports;

public static class SourceStatementText
{
    public static string Generate(PdfTextDocument document)
    {
        var text = new StringBuilder();
        foreach (var page in document.Pages)
        {
            text.AppendLine($"--- Page {page.Number} ---");
            text.AppendLine(page.Markdown ?? PageLayoutText.Generate(page));
            text.AppendLine();
        }
        return text.ToString();
    }
}
