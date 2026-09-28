using DLLNelogica.Configuration;
using DLLNelogica.Connection;
using DLLNelogica.Interop;
using DLLNelogica.MarketData;

namespace DLLNelogica.Application;

internal sealed class MarketDataSessionCoordinator
{
    private const int ConnectionTimeoutSeconds = 90;
    private readonly ProfitSession _profitSession;
    private readonly ConnectionStateMachine _connectionState;
    private readonly MarketDataSubscriptionManager _subscriptions;

    internal MarketDataSessionCoordinator(
        ProfitSession profitSession,
        ConnectionStateMachine connectionState,
        MarketDataSubscriptionManager subscriptions)
    {
        _profitSession = profitSession;
        _connectionState = connectionState;
        _subscriptions = subscriptions;
    }

    internal async Task<int> RunAsync(ApplicationOptions options, ConsoleShutdown shutdown)
    {
        if (!MarketDataSessionStartup.TryStart(_profitSession, options))
        {
            return 1;
        }

        TryWriteLine("Callbacks registrados; aguardando os estados de conexão.");
        var connection = await _connectionState.WaitForConnectionAsync(
            ConnectionTimeoutSeconds,
            shutdown.Source.Token).ConfigureAwait(false);
        return await HandleConnectionResultAsync(connection, options, shutdown)
            .ConfigureAwait(false);
    }

    internal bool Shutdown()
    {
        _profitSession.StopNativeQueries();
        var unsubscribeResults = _subscriptions.UnsubscribeAll();
        var isSuccessful = unsubscribeResults.All(result => result.IsSuccessful);
        if (!ProfitProcessLifetime.IsFinalizationRequired)
        {
            return isSuccessful;
        }

        TryWriteLine("Finalizando serviços da DLL...");
        var finalization = _profitSession.FinalizeOnce();
        if (!finalization.WasExecuted)
        {
            return isSuccessful;
        }

        TryWriteLine(finalization.Message, !finalization.IsSuccessful);
        return isSuccessful && finalization.IsSuccessful;
    }

    private async Task<int> HandleConnectionResultAsync(
        ConnectionWaitResult connection,
        ApplicationOptions options,
        ConsoleShutdown shutdown)
    {
        if (!connection.IsConnected || shutdown.Source.IsCancellationRequested)
        {
            return ReportConnectionFailure(connection, shutdown);
        }

        TryWriteLine("Conexão confirmada pelos quatro estados obrigatórios.");
        var subscription = _subscriptions.SubscribeAll(options.MarketData.Instruments);
        if (!subscription.IsSuccessful)
        {
            TryWriteLine(
                $"Assinatura interrompida em {subscription.FailedInstrument?.Key}; rollback concluído.",
                true);
            return 1;
        }

        TryWriteLine("Todos os instrumentos foram assinados. Pressione Ctrl+C para encerrar.");
        // A captura já está assinada; contas têm consumidor e falhas independentes.
        await ConnectedSessionTasks.RunAsync(_profitSession, options, shutdown).ConfigureAwait(false);
        if (shutdown.WasRequestedByUser)
        {
            TryWriteLine("Encerramento solicitado pelo usuário.");
            return 0;
        }

        return 1;
    }

    private static int ReportConnectionFailure(
        ConnectionWaitResult connection,
        ConsoleShutdown shutdown)
    {
        if (shutdown.WasRequestedByUser)
        {
            TryWriteLine("Encerramento solicitado pelo usuário.");
        }
        else
        {
            TryWriteLine(connection.Message, true);
        }

        return 1;
    }

    private static void TryWriteLine(string message, bool isError = false)
    {
        try
        {
            if (isError)
            {
                Console.Error.WriteLine(message);
            }
            else
            {
                Console.WriteLine(message);
            }
        }
        catch
        {
            // A ausência de saída não pode impedir a finalização e a drenagem.
        }
    }
}
