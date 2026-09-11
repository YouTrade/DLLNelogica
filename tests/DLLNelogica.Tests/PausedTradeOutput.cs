using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

internal sealed class PausedTradeOutput : ITradeOutput, IDisposable
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ManualResetEventSlim _cancellationEntered = new();
    private readonly ManualResetEventSlim _releaseCancellation = new();
    private readonly CancellationTokenRegistration _registration;
    internal int Delivered { get; private set; }
    internal bool OutputReleased => _release.Task.IsCompleted;
    internal bool CancellationReleased => _releaseCancellation.IsSet;

    internal PausedTradeOutput(CancellationTokenSource shutdown)
    {
        _registration = shutdown.Token.Register(() =>
        {
            _cancellationEntered.Set();
            _releaseCancellation.Wait(TimeSpan.FromSeconds(10));
        });
    }

    internal Task WaitForEntryAsync() => _entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
    internal bool WaitForCancellation() => _cancellationEntered.Wait(TimeSpan.FromSeconds(10));

    public async ValueTask WriteAsync(TradeRecord trade)
    {
        _entered.TrySetResult();
        await _release.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Delivered++;
    }

    internal void Release()
    {
        _releaseCancellation.Set();
        _release.TrySetResult();
    }

    public void Dispose()
    {
        Release();
        _registration.Dispose();
        _cancellationEntered.Dispose();
        _releaseCancellation.Dispose();
    }
}
