using DLLNelogica.Connection;
using DLLNelogica.Interop;

namespace DLLNelogica.MarketData;

internal sealed class MarketDataRuntimePipeline
{
    private readonly CancellationTokenSource _shutdownRequested;
    private readonly ProfitCallbackBridge _callbackBridge;
    private readonly ConnectionStateEventPump _stateEvents;
    private readonly MarketDataMetrics _metrics;
    private readonly MarketPriceEventPump _marketEvents;
    private readonly Task _stateConsumer;
    private readonly Task _priceConsumer;
    private readonly Task _invalidTickerConsumer;

    internal MarketDataRuntimePipeline(
        int channelCapacity,
        CancellationTokenSource shutdownRequested,
        ProfitCallbackBridge callbackBridge,
        ConnectionStateEventPump stateEvents,
        MarketDataSubscriptionManager subscriptions,
        MarketDataMetrics metrics)
    {
        _shutdownRequested = shutdownRequested;
        _callbackBridge = callbackBridge;
        _stateEvents = stateEvents;
        _metrics = metrics;
        _marketEvents = new MarketPriceEventPump(channelCapacity, metrics);
        callbackBridge.AttachShutdown(shutdownRequested);
        callbackBridge.AttachMarketData(_marketEvents);
        _stateConsumer = stateEvents.RunAsync(shutdownRequested);
        _priceConsumer = _marketEvents.ObservePricesAsync(shutdownRequested);
        _invalidTickerConsumer = _marketEvents.ObserveInvalidTickersAsync(
            subscriptions,
            shutdownRequested);
    }

    internal async Task<bool> CompleteAndDrainAsync()
    {
        _marketEvents.Complete();
        _stateEvents.Complete();
        await Task.WhenAll(_stateConsumer, _priceConsumer, _invalidTickerConsumer).ConfigureAwait(false);

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
