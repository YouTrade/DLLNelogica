using DLLNelogica.Application;
using DLLNelogica.Configuration;
using DLLNelogica.Connection;
using DLLNelogica.Interop;
using DLLNelogica.MarketData;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

internal sealed class CallbackFileScenario : IAsyncDisposable
{
    private readonly TradeFileFixture _fixture = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TradeRuntimePipeline _runtime;
    private readonly MarketDataSessionCoordinator _coordinator;
    private readonly MarketDataSubscriptionManager _subscriptions;

    internal CallbackFileScenario(ProfitCallbackBridge bridge, ProfitSession session)
    {
        var options = new ApplicationOptions
        {
            TimesAndTrades = new TimesAndTradesOptions { Enabled = true },
            MarketData = new MarketDataOptions
            {
                ReportIntervalSeconds = 1,
                Instruments = [new() { Ticker = "VALE3", Exchange = "B" }]
            }
        };
        _runtime = new TradePipelineFactory(bridge, session, new TradeReportProbe(), _fixture.Root).Start(options, _shutdown)!;
        _subscriptions = new MarketDataSubscriptionManager(session);
        _coordinator = new MarketDataSessionCoordinator(session, new ConnectionStateMachine(), _subscriptions);
    }

    internal async Task PrepareAndSubscribeAsync()
    {
        await _runtime.Ready;
        Assert.True(_subscriptions.SubscribeAll([new InstrumentOptions { Ticker = "VALE3", Exchange = "B" }]).IsSuccessful);
    }

    internal bool Shutdown() => _coordinator.Shutdown();

    internal async Task CheckFinalEventAsync()
    {
        Assert.True(await _runtime.CompleteAndDrainAsync());
        Assert.Equal(1, _runtime.Snapshot.Total.Confirmed);
        Assert.Single(_fixture.AllEvents());
    }

    public async ValueTask DisposeAsync()
    {
        await _runtime.DisposeAsync();
        _shutdown.Dispose();
        _fixture.Dispose();
    }
}
