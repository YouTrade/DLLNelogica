using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

internal sealed class BlockedTradeStorage : ITradeStorage
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool WasDisposed { get; private set; }
    internal Task Entered => _entered.Task;
    internal void Release() => _release.TrySetResult();

    public async ValueTask WriteAsync(TradeRecord trade)
    {
        _entered.TrySetResult();
        await _release.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public IReadOnlyList<TradeRecord> Maintain(DateTimeOffset now, bool complete, bool forceFlush = false) => [];
    public void Dispose() => WasDisposed = true;
}
