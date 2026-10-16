using System.Collections.Concurrent;
using System.Threading.Channels;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BankStatementConverter.Tests;

public sealed class OcrWorkQueueTests
{
    internal static IConfiguration Config(int concurrent = 2, int queued = 32, int wait = 600)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ocr:MaxConcurrentJobs"] = concurrent.ToString(),
            ["Ocr:MaxQueuedJobs"] = queued.ToString(),
            ["Ocr:QueueWaitSeconds"] = wait.ToString()
        }).Build();

    internal sealed class HeldOcr : IPdfOcrClient
    {
        public readonly Channel<int> Started = Channel.CreateUnbounded<int>();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> releases = new();
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public async Task<PdfTextDocument> ExtractAsync(byte[] content, CancellationToken ct)
        {
            var id = Interlocked.Increment(ref calls);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            releases[id] = release;
            await Started.Writer.WriteAsync(id, ct);
            await release.Task.WaitAsync(ct);
            return new("PADDLE_OCR", [new(1, 600, 800, [new(id.ToString(), 0, 20, 0, 10)])]);
        }
        public void Release(int id) => releases[id].TrySetResult();
        public void Fail(int id) => releases[id].TrySetException(new AppException(503, "Test OCR failure"));
        public async Task<int> Next() => await Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
    private sealed class Progress : IProcessingProgress
    {
        public readonly ConcurrentQueue<string> Stages = new();
        public void Report(string stage, int percent, int? extractedPages = null, int? totalPages = null) => Stages.Enqueue(stage);
    }

    [Fact]
    public async Task TwoDocumentsRunTogetherAndWaitingDocumentsStartInOrder()
    {
        using var queue = new OcrWorkQueue(Config());
        var client = new HeldOcr();
        var first = queue.ExtractAsync(client, [1], null, default);
        Assert.Equal(1, await client.Next());
        var second = queue.ExtractAsync(client, [2], null, default);
        Assert.Equal(2, await client.Next());
        var progress = new Progress();
        var third = queue.ExtractAsync(client, [3], progress, default);
        var fourth = queue.ExtractAsync(client, [4], null, default);
        Assert.Equal(2, client.Calls);
        Assert.Contains("queued", progress.Stages);
        client.Release(1);
        await first;
        Assert.Equal(3, await client.Next());
        Assert.False(second.IsCompleted);
        Assert.False(fourth.IsCompleted);
        client.Release(3);
        await third;
        Assert.Equal(4, await client.Next());
        client.Release(4);
        client.Release(2);
        await Task.WhenAll(second, fourth);
        Assert.Equal(new[] { "queued", "reading" }, progress.Stages);
    }

    [Fact]
    public async Task CancelledWaiterNeverCallsProviderAndFreesQueueSpace()
    {
        using var queue = new OcrWorkQueue(Config(1, 1));
        var client = new HeldOcr();
        var first = queue.ExtractAsync(client, [], null, default);
        await client.Next();
        using var cancel = new CancellationTokenSource();
        var second = queue.ExtractAsync(client, [], null, cancel.Token);
        Assert.Equal(429, (await Assert.ThrowsAsync<AppException>(() => queue.ExtractAsync(client, [], null, default))).Status);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        var third = queue.ExtractAsync(client, [], null, default);
        client.Release(1);
        await first;
        Assert.Equal(2, await client.Next());
        client.Release(2);
        await third;
        Assert.Equal(2, client.Calls);
    }

    [Fact]
    public async Task FailedOrCancelledProviderReleasesSlotForAnotherUser()
    {
        using var queue = new OcrWorkQueue(Config(1));
        var client = new HeldOcr();
        var first = queue.ExtractAsync(client, [], null, default);
        await client.Next();
        using var cancel = new CancellationTokenSource();
        var second = queue.ExtractAsync(client, [], null, cancel.Token);
        client.Fail(1);
        await Assert.ThrowsAsync<AppException>(() => first);
        await client.Next();
        var third = queue.ExtractAsync(client, [], null, default);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        Assert.Equal(3, await client.Next());
        client.Release(3);
        await third;
    }

    [Fact]
    public async Task QueueTimeoutDoesNotCancelAnActiveDocument()
    {
        using var queue = new OcrWorkQueue(Config(1, 1, 1));
        var client = new HeldOcr();
        var first = queue.ExtractAsync(client, [], null, default);
        await client.Next();
        Assert.Equal(504, (await Assert.ThrowsAsync<AppException>(() => queue.ExtractAsync(client, [], null, default))).Status);
        Assert.False(first.IsCompleted);
        Assert.Equal(1, client.Calls);
        client.Release(1);
        await first;
    }

    [Fact]
    public async Task NativePdfDoesNotWaitForBusyOcrWorkers()
    {
        using var queue = new OcrWorkQueue(Config(1));
        var client = new HeldOcr();
        var active = queue.ExtractAsync(client, [], null, default);
        await client.Next();
        var extraction = new AutomaticPdfExtraction(new(), new QueuedPdfOcrClient(client, queue, new Progress()));
        var bytes = File.ReadAllBytes(StatementPdfExtractorTests.Fixture("releve_credit_agricole_exemple_02.pdf"));
        var native = await extraction.ExtractAsync(bytes, "Test", "CA", "MAD", default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(22, native.Statement.Transactions.Count);
        Assert.Equal(1, client.Calls);
        Assert.False(active.IsCompleted);
        client.Release(1);
        await active;
    }
}
