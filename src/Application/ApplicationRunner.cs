using System.Text.Json;
using DLLNelogica.Configuration;
using DLLNelogica.Connection;
using DLLNelogica.Interop;

namespace DLLNelogica.Application;

internal sealed class ApplicationRunner
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(90);
    private readonly JsonCredentialsLoader _credentialsLoader;
    private readonly ProfitSession _profitSession;
    private readonly ProfitCallbackBridge _callbackBridge;
    private readonly ConnectionStateMachine _connectionState;
    private readonly ConnectionStateEventPump _stateEvents;

    internal ApplicationRunner(
        JsonCredentialsLoader credentialsLoader,
        ProfitSession profitSession,
        ProfitCallbackBridge callbackBridge,
        ConnectionStateMachine connectionState,
        ConnectionStateEventPump stateEvents)
    {
        _credentialsLoader = credentialsLoader;
        _profitSession = profitSession;
        _callbackBridge = callbackBridge;
        _connectionState = connectionState;
        _stateEvents = stateEvents;
    }

    internal async Task<int> RunAsync()
    {
        using var shutdown = new ConsoleShutdown();
        _callbackBridge.AttachShutdown(shutdown.Source);
        var consumerTask = _stateEvents.RunAsync(shutdown.Source);
        var exitCode = 0;

        try
        {
            var credentials = _credentialsLoader.Load();
            ReportLoadedConfiguration(credentials);

            var initialization = _profitSession.Initialize(credentials);
            if (!initialization.IsAccepted)
            {
                Console.Error.WriteLine(initialization.Message);
                exitCode = 1;
            }
            else
            {
                Console.WriteLine("DLLInitializeLogin retornou NL_OK; aguardando os estados de conexão.");
                var connection = await _connectionState.WaitForConnectionAsync(
                    ConnectionTimeout,
                    shutdown.Source.Token).ConfigureAwait(false);
                exitCode = await HandleConnectionResultAsync(connection, shutdown).ConfigureAwait(false);
            }
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine("O arquivo appsettings.json não foi encontrado.");
            exitCode = 1;
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Acesso negado ao appsettings.json.");
            exitCode = 1;
        }
        catch (IOException)
        {
            Console.Error.WriteLine("Não foi possível ler o appsettings.json.");
            exitCode = 1;
        }
        catch (JsonException exception)
        {
            var line = exception.LineNumber.HasValue ? exception.LineNumber.Value + 1 : 0;
            var position = exception.BytePositionInLine.HasValue ? exception.BytePositionInLine.Value + 1 : 0;
            Console.Error.WriteLine($"JSON inválido na linha {line}, posição {position}.");
            exitCode = 1;
        }
        catch (ConfigurationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            exitCode = 1;
        }
        finally
        {
            try
            {
                FinalizeProfitServices();
                _stateEvents.Complete();
                await consumerTask.ConfigureAwait(false);

                if (_stateEvents.HasFailed || ProfitProcessLifetime.HasCallbackFailure)
                {
                    exitCode = 1;
                }
            }
            finally
            {
                _callbackBridge.DetachShutdown(shutdown.Source);
            }
        }

        return exitCode;
    }

    private static async Task<int> HandleConnectionResultAsync(
        ConnectionWaitResult connection,
        ConsoleShutdown shutdown)
    {
        if (connection.IsConnected && !shutdown.Source.IsCancellationRequested)
        {
            Console.WriteLine("Conexão confirmada pelos quatro estados obrigatórios.");
            Console.WriteLine("Pressione Ctrl+C para encerrar.");
            await shutdown.WaitAsync().ConfigureAwait(false);

            if (shutdown.WasRequestedByUser)
            {
                Console.WriteLine("Encerramento solicitado pelo usuário.");
            }

            return 0;
        }

        if (shutdown.WasRequestedByUser)
        {
            if (connection.IsConnected)
            {
                Console.WriteLine("Conexão confirmada pelos quatro estados obrigatórios.");
            }

            Console.WriteLine("Encerramento solicitado pelo usuário.");
            return connection.IsConnected ? 0 : 1;
        }

        if (!connection.IsConnected)
        {
            Console.Error.WriteLine(connection.Message);
            return 1;
        }

        return 0;
    }

    private static void ReportLoadedConfiguration(CredentialsOptions credentials)
    {
        Console.WriteLine("Configuração carregada e validada com sucesso.");
        Console.WriteLine($"Processo x64: {Environment.Is64BitProcess}");

        if (ConfigurationValidator.HasOuterWhitespace(credentials))
        {
            Console.WriteLine(
                "Aviso: uma ou mais credenciais contêm espaços nas extremidades e serão enviadas sem alteração.");
        }
    }

    private void FinalizeProfitServices()
    {
        if (!ProfitProcessLifetime.IsFinalizationRequired)
        {
            return;
        }

        try
        {
            Console.WriteLine("Finalizando serviços da DLL...");
        }
        catch
        {
            // A ausência de saída não pode impedir a finalização nativa.
        }

        var finalization = _profitSession.FinalizeOnce();
        if (!finalization.WasExecuted)
        {
            return;
        }

        try
        {
            if (finalization.IsSuccessful)
            {
                Console.WriteLine(finalization.Message);
            }
            else
            {
                Console.Error.WriteLine(finalization.Message);
            }
        }
        catch
        {
            // A drenagem do canal deve continuar mesmo quando toda saída está indisponível.
        }
    }

}
