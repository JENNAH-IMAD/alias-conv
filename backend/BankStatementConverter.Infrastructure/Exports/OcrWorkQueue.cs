using System.Threading.RateLimiting;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using Microsoft.Extensions.Configuration;

namespace BankStatementConverter.Infrastructure.Exports;

// One shared admission queue per API process; each request retains its own client,
// progress reporter, cancellation token and financial data. No database lock is held.
public sealed class OcrWorkQueue : IDisposable
{
    private readonly ConcurrencyLimiter limiter;
    private readonly TimeSpan waitTimeout;

    public OcrWorkQueue(IConfiguration config)
    {
        limiter = new(new ConcurrencyLimiterOptions
        {
            PermitLimit = Math.Clamp(config.GetValue("Ocr:MaxConcurrentJobs", 2), 1, 16),
            QueueLimit = Math.Clamp(config.GetValue("Ocr:MaxQueuedJobs", 32), 0, 256),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
        waitTimeout = TimeSpan.FromSeconds(Math.Clamp(config.GetValue("Ocr:QueueWaitSeconds", 600), 1, 600));
    }

    public async Task<PdfTextDocument> ExtractAsync(IPdfOcrClient client, byte[] content,
        IProcessingProgress? progress, CancellationToken ct)
    {
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(ct);
        waiting.CancelAfter(waitTimeout);
        RateLimitLease lease;
        try
        {
            var admission = limiter.AcquireAsync(1, waiting.Token);
            if (!admission.IsCompletedSuccessfully)
                progress?.Report("queued", 25);
            lease = await admission;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AppException(504, "Tous les moteurs OCR sont encore occupés. Le délai d’attente de ce PDF est dépassé ; les autres traitements continuent.");
        }
        using (lease)
        {
            if (!lease.IsAcquired)
                throw new AppException(429, "La file locale des conversions est pleine. Réessayez après la fin d’un traitement.");
            ct.ThrowIfCancellationRequested();
            progress?.Report("reading", 25);
            return await client.ExtractAsync(content, ct);
        }
    }

    public void Dispose() => limiter.Dispose();
}

public sealed class QueuedPdfOcrClient(IPdfOcrClient client, OcrWorkQueue queue, IProcessingProgress progress) : IPdfOcrClient
{
    public Task<PdfTextDocument> ExtractAsync(byte[] content, CancellationToken ct)
        => queue.ExtractAsync(client, content, progress, ct);
}
