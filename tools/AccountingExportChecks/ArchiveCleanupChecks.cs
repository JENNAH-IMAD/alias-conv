using BankStatementConverter.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class ArchiveCleanupChecks
{
    public static async Task Run()
    {
        using (var stopping = new CancellationTokenSource())
        {
            stopping.Cancel();
            var scopes = new FailingScopes(() => throw new Exception("Unexpected cleanup after cancellation"));
            var log = new StopOnWarning(stopping);
            using var worker = new TestWorker(scopes, log);
            await worker.Execute(stopping.Token).WaitAsync(TimeSpan.FromSeconds(2));
            Check(scopes.Calls == 0 && log.Warnings == 0, "ArchiveCleanup_AlreadyCanceled_NoWork");
        }
        using (var stopping = new CancellationTokenSource())
        {
            var scopes = new FailingScopes(() => { stopping.Cancel(); throw new OperationCanceledException(stopping.Token); });
            var log = new StopOnWarning(stopping);
            using var worker = new TestWorker(scopes, log);
            await worker.Execute(stopping.Token).WaitAsync(TimeSpan.FromSeconds(2));
            Check(scopes.Calls == 1 && log.Warnings == 0, "ArchiveCleanup_CanceledDuringCleanup_NoWarning");
        }
        foreach (var unrelatedCancellation in new[] { false, true })
        {
            using var stopping = new CancellationTokenSource();
            var failure = unrelatedCancellation ? (Exception)new OperationCanceledException("Unrelated operation") : new IOException("Storage unavailable");
            var scopes = new FailingScopes(() => throw failure);
            // Cancel only after a recoverable failure was logged, immediately before the delay.
            var log = new StopOnWarning(stopping);
            using var worker = new TestWorker(scopes, log);
            await worker.Execute(stopping.Token).WaitAsync(TimeSpan.FromSeconds(2));
            Check(scopes.Calls == 1 && log.Warnings == 1 && ReferenceEquals(log.Failure, failure),
                unrelatedCancellation ? "ArchiveCleanup_UnrelatedCancellation_IsLogged" : "ArchiveCleanup_CanceledDelay_CompletesNormally");
        }
    }
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
    private sealed class TestWorker(IServiceScopeFactory scopes, ILogger<ArchiveCleanupWorker> logger) : ArchiveCleanupWorker(scopes, logger)
    {
        public Task Execute(CancellationToken token) => ExecuteAsync(token);
    }
    private sealed class FailingScopes(Func<IServiceScope> create) : IServiceScopeFactory
    {
        public int Calls { get; private set; }
        public IServiceScope CreateScope() { Calls++; return create(); }
    }
    private sealed class StopOnWarning(CancellationTokenSource stopping) : ILogger<ArchiveCleanupWorker>
    {
        public int Warnings { get; private set; }
        public Exception? Failure { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (level != LogLevel.Warning) throw new Exception("Unexpected log level");
            Warnings++; Failure = exception; stopping.Cancel();
        }
    }
}
