using DLLNelogica.Configuration;
using DLLNelogica.Connection;
using DLLNelogica.Interop;
using DLLNelogica.Logging;

namespace DLLNelogica.MarketData;

internal sealed class MarketDataRuntimePipeline : IDisposable
{
    private readonly CancellationTokenSource _shutdownRequested;

    // O relator tem a própria parada. O token de encerramento só é cancelado pelo Ctrl+C —
    // numa falha de conexão ele permanece ativo, e esperar por um timer que ninguém cancelou
    // travaria a drenagem para sempre.
    private readonly CancellationTokenSource _reporterStop = new();
    private readonly ProfitCallbackBridge _callbackBridge;
    private readonly ConnectionStateEventPump _stateEvents;
    private readonly MarketDataMetrics _metrics;
    private readonly MarketPriceEventPump _marketEvents;
    private readonly Task _stateConsumer;
    private readonly Task _priceConsumer;
    private readonly Task _invalidTickerConsumer;
    private readonly Task _reporter;

    internal MarketDataRuntimePipeline(
        MarketDataOptions marketData,
        CancellationTokenSource shutdownRequested,
        ProfitCallbackBridge callbackBridge,
        ConnectionStateEventPump stateEvents,
        MarketDataSubscriptionManager subscriptions,
        MarketDataMetrics metrics,
        IReportLog reportLog)
    {
        _shutdownRequested = shutdownRequested;
        _callbackBridge = callbackBridge;
        _stateEvents = stateEvents;
        _metrics = metrics;

        var snapshot = new MarketDataSnapshot();
        _marketEvents = new MarketPriceEventPump(
            marketData.ChannelCapacity,
            metrics,
            snapshot,
            reportLog);

        callbackBridge.AttachShutdown(shutdownRequested);
        callbackBridge.AttachMarketData(_marketEvents);
        _stateConsumer = stateEvents.RunAsync(shutdownRequested);
        _priceConsumer = _marketEvents.ObservePricesAsync(shutdownRequested);
        _invalidTickerConsumer = _marketEvents.ObserveInvalidTickersAsync(
            subscriptions,
            shutdownRequested);
        _reporter = new MarketDataReporter(
            snapshot,
            metrics,
            reportLog,
            marketData.ReportIntervalSeconds).RunAsync(_reporterStop.Token);
    }

    internal async Task<bool> CompleteAndDrainAsync()
    {
        await _reporterStop.CancelAsync().ConfigureAwait(false);
        _marketEvents.Complete();
        _stateEvents.Complete();
        await Task.WhenAll(_stateConsumer, _priceConsumer, _invalidTickerConsumer, _reporter)
            .ConfigureAwait(false);

        var isSuccessful =
            !_stateEvents.HasFailed &&
            !_marketEvents.HasFailed &&
            !_marketEvents.HasFatalInvalidTicker &&
            !ProfitProcessLifetime.HasCallbackFailure;

        TryWriteLine(
            $"Resumo de market data | cotações recebidas={_metrics.ReceivedPriceChanges} | " +
            $"descartadas={_metrics.DroppedPriceChanges} | " +
            $"tickers inválidos={_metrics.InvalidTickerCount}");
        _callbackBridge.DetachMarketData(_marketEvents);
        _callbackBridge.DetachShutdown(_shutdownRequested);
        return isSuccessful;
    }

    public void Dispose() => _reporterStop.Dispose();

    private static void TryWriteLine(string message)
    {
        try
        {
            Console.WriteLine(message);
        }
        catch
        {
            // Falha de saída não pode impedir o desligamento do pipeline.
        }
    }
}
