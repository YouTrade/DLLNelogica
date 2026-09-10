using DLLNelogica.Application;
using DLLNelogica.Configuration;
using DLLNelogica.Connection;
using DLLNelogica.Interop;
using DLLNelogica.MarketData;

namespace DLLNelogica;

internal static class Program
{
    private static async Task<int> Main()
    {
        using var environment = ConsoleApplicationEnvironment.Start();
        var connectionState = new ConnectionStateMachine();
        var stateEvents = new ConnectionStateEventPump(connectionState);
        var callbackBridge = new ProfitCallbackBridge(stateEvents);
        IProfitApi profitApi = new ProfitNativeApi(AppContext.BaseDirectory);
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
        var marketDataApplication = new MarketDataApplication(sessionCoordinator, pipelineFactory);
        var application = new ApplicationRunner(configurationLoader, marketDataApplication);

        return await application.RunAsync().ConfigureAwait(false);
    }
}
