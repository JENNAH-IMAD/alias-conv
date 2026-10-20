using BankStatementConverter.Application.Exports;
using Microsoft.Extensions.Caching.Memory;

namespace BankStatementConverter.Infrastructure.Exports;

// Short-lived, bounded memory only. No API exposes this cache; keys also include the credential hash.
public sealed class OpenRouterPageCache : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 32 * 1024 * 1024 });
    public bool TryGet(string key, out PdfTextPage? page) => cache.TryGetValue(key, out page);
    public void Store(string key, PdfTextPage page)
    {
        var bytes = 1024L + 2L * ((page.Markdown?.Length ?? 0)
            + page.Boxes.Sum(box => box.Text.Length)
            + (page.Tables?.Sum(table => table.Rows.Sum(row => row.Sum(cell => cell.Length))) ?? 0));
        cache.Set(key, page, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10), Size = bytes });
    }
    public void Dispose() => cache.Dispose();
}
