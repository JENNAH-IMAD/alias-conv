using Microsoft.Extensions.Configuration;

namespace BankStatementConverter.Infrastructure.Exports;

internal static class ExtractionTimeout
{
    // Zero leaves conversion duration to the caller's cancellation token.
    public static void Apply(CancellationTokenSource cancellation, IConfiguration config, string key)
    {
        var seconds = config.GetValue(key, 0);
        if (seconds > 0)
            cancellation.CancelAfter(TimeSpan.FromSeconds(seconds));
    }
}
