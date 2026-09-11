using System.Diagnostics;
using DLLNelogica.Interop;

namespace DLLNelogica.TimesAndTrades;

internal sealed class TradeRuntimePipeline : IAsyncDisposable
{
    private readonly ProfitCallbackBridge _bridge;
    private readonly TradeEventPump _pump;
    private readonly TradeReporter _reporter;
    private readonly CancellationTokenSource _reporterStop = new();
    private readonly Task _consumer;
    private readonly Task _reporting;
    private Task<bool>? _completion;

    internal TradeRuntimePipeline(ProfitCallbackBridge bridge, TradeEventPump pump, TradeReporter reporter,
        int reportIntervalSeconds, TimeProvider clock)
    {
        _bridge = bridge;
        _pump = pump;
        _reporter = reporter;
        bridge.AttachTrades(pump);
        _consumer = pump.RunAsync();
        _reporting = ReportAsync(reportIntervalSeconds, clock);
        reporter.Session($"Times and Trades | sessao={bridge.TradeSessionId:D} | preparação de arquivos iniciada.");
    }

    internal Task Completion => _consumer;

    internal Task Ready => _pump.Ready;
    internal TradeMetricsSnapshot Snapshot => _pump.Snapshot;

    internal Task<bool> CompleteAndDrainAsync(TimeSpan? timeout = null) =>
        _completion ??= DrainAsync(timeout ?? TimeSpan.FromSeconds(10));

    public async ValueTask DisposeAsync() => await CompleteAndDrainAsync().ConfigureAwait(false);

    private async Task<bool> DrainAsync(TimeSpan timeout)
    {
        await _reporterStop.CancelAsync().ConfigureAwait(false);
        await _reporting.ConfigureAwait(false);
        _reporterStop.Dispose();
        var drainStarted = Stopwatch.GetTimestamp();
        _pump.Complete();
        var timedOut = false;
        try
        {
            await _consumer.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // O worker segue dono do writer: não há Dispose concorrente nem promessa de aborto do I/O.
            timedOut = true;
            _pump.MarkDrainTimeout();
        }

        _bridge.DetachTrades(_pump);
        _reporter.Final(Snapshot, timedOut, Stopwatch.GetElapsedTime(drainStarted));
        return !timedOut && !Snapshot.IsIncomplete && Snapshot.Total.Unconfirmed == 0;
    }

    private async Task ReportAsync(int intervalSeconds, TimeProvider clock)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(_reporterStop.Token).ConfigureAwait(false))
            {
                _reporter.Report(Snapshot);
            }
        }
        catch (OperationCanceledException) when (_reporterStop.IsCancellationRequested)
        {
        }
    }
}
