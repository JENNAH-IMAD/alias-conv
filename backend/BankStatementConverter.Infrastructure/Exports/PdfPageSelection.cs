using BankStatementConverter.Application;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace BankStatementConverter.Infrastructure.Exports;

internal static class PdfPageSelection
{
    internal static (byte[] Content, int Total) Create(byte[] content, IReadOnlyList<int> pages)
    {
        using var input = new MemoryStream(content, false);
        using var source = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        if (pages.Count == 0 || pages.Distinct().Count() != pages.Count || pages.Any(number => number < 1 || number > source.PageCount))
            throw new AppException(400, "Sélection de pages invalide.");
        using var selection = new PdfDocument();
        foreach (var number in pages)
            selection.AddPage(source.Pages[number - 1]);
        using var buffer = new MemoryStream();
        selection.Save(buffer, false);
        return (buffer.ToArray(), source.PageCount);
    }
}
