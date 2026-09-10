using DLLNelogica.Configuration;
using DLLNelogica.MarketData;

namespace DLLNelogica.Application;

internal sealed class MarketDataApplication
{
    private readonly MarketDataSessionCoordinator _session;
    private readonly MarketDataPipelineFactory _pipelineFactory;

    internal MarketDataApplication(
        MarketDataSessionCoordinator session,
        MarketDataPipelineFactory pipelineFactory)
    {
        _session = session;
        _pipelineFactory = pipelineFactory;
    }

    internal async Task<int> RunAsync(ApplicationOptions options)
    {
        using var shutdown = new ConsoleShutdown();
        using var pipeline = _pipelineFactory.Start(options.MarketData, shutdown.Source);
        var exitCode = 1;
        try
        {
            exitCode = await _session.RunAsync(options, shutdown).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // O finally ainda precisa finalizar a DLL após qualquer falha operacional.
        catch (Exception exception)
        {
            TryReportFailure(exception);
            exitCode = 1;
        }
#pragma warning restore CA1031
        finally
        {
            if (!_session.Shutdown())
            {
                exitCode = 1;
            }

            if (!await pipeline.CompleteAndDrainAsync().ConfigureAwait(false))
            {
                exitCode = 1;
            }
        }

        return exitCode;
    }

    private static void TryReportFailure(Exception exception)
    {
        try
        {
            Console.Error.WriteLine(
                $"Falha operacional ({exception.GetType().Name}); encerramento controlado iniciado.");
        }
        catch
        {
            // A falha de saída não pode impedir a finalização nativa.
        }
    }
}
