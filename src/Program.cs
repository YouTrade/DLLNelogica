using DLLNelogica.Application;
using DLLNelogica.Configuration;
using DLLNelogica.Connection;
using DLLNelogica.Interop;

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
        var credentialsLoader = new JsonCredentialsLoader(AppContext.BaseDirectory);
        var application = new ApplicationRunner(
            credentialsLoader,
            profitSession,
            callbackBridge,
            connectionState,
            stateEvents);

        return await application.RunAsync().ConfigureAwait(false);
    }
}
