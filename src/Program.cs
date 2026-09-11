using DLLNelogica.Application;
using DLLNelogica.Configuration;
using DLLNelogica.Connection;
using DLLNelogica.Interop;
using DLLNelogica.MarketData;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica;

internal static class Program
{
    private static async Task<int> Main()
    {
        using var environment = ConsoleApplicationEnvironment.Start();
        var connectionState = new ConnectionStateMachine();
        var stateEvents = new ConnectionStateEventPump(connectionState);
        IProfitApi profitApi = new ProfitNativeApi(AppContext.BaseDirectory);
        var callbackBridge = new ProfitCallbackBridge(stateEvents, profitApi);
        var profitSession = new ProfitSession(profitApi, callbackBridge);
        var configurationLoader = new JsonConfigurationLoader(AppContext.BaseDirectory);
        var metrics = new MarketDataMetrics();
        var subscriptions = new MarketDataSubscriptionManager(profitSession);
        var pipelineFactory = new MarketDataPipelineFactory(
            callbackBridge,
            stateEvents,
            subscriptions,
            metrics,
            environment.Reports);
        var sessionCoordinator = new MarketDataSessionCoordinator(
            profitSession,
            connectionState,
            subscriptions);
        var tradeFactory = new TradePipelineFactory(callbackBridge, profitSession, environment.Reports, AppContext.BaseDirectory);
        var marketDataApplication = new MarketDataApplication(sessionCoordinator, pipelineFactory, tradeFactory);
        var application = new ApplicationRunner(configurationLoader, marketDataApplication);

        return await application.RunAsync().ConfigureAwait(false);
    }
}
