using DLLNelogica.Connection;
using DLLNelogica.Interop;

namespace DLLNelogica.MarketData;

internal sealed class MarketDataPipelineFactory
{
    private readonly ProfitCallbackBridge _callbackBridge;
    private readonly ConnectionStateEventPump _stateEvents;
    private readonly MarketDataSubscriptionManager _subscriptions;
    private readonly MarketDataMetrics _metrics;

    internal MarketDataPipelineFactory(
        ProfitCallbackBridge callbackBridge,
        ConnectionStateEventPump stateEvents,
        MarketDataSubscriptionManager subscriptions,
        MarketDataMetrics metrics)
    {
        _callbackBridge = callbackBridge;
        _stateEvents = stateEvents;
        _subscriptions = subscriptions;
        _metrics = metrics;
    }

    internal MarketDataRuntimePipeline Start(
        int channelCapacity,
        CancellationTokenSource shutdownRequested) =>
        new(
            channelCapacity,
            shutdownRequested,
            _callbackBridge,
            _stateEvents,
            _subscriptions,
            _metrics);
}
