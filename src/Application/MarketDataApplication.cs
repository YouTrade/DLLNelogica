using DLLNelogica.Configuration;
using DLLNelogica.MarketData;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Application;

internal sealed class MarketDataApplication
{
    private readonly MarketDataSessionCoordinator _session;
    private readonly MarketDataPipelineFactory _pipelineFactory;
    private readonly TradePipelineFactory _tradeFactory;

    internal MarketDataApplication(
        MarketDataSessionCoordinator session,
        MarketDataPipelineFactory pipelineFactory,
        TradePipelineFactory tradeFactory)
    {
        _session = session;
        _pipelineFactory = pipelineFactory;
        _tradeFactory = tradeFactory;
    }

    internal async Task<int> RunAsync(ApplicationOptions options)
    {
        using var shutdown = new ConsoleShutdown();
        using var pipeline = _pipelineFactory.Start(options.MarketData, shutdown.Source);
        var exitCode = 1;
        TradeRuntimePipeline? trades = null;
        try
        {
            trades = _tradeFactory.Start(options, shutdown.Source);
            if (trades is not null)
            {
                await trades.Ready.WaitAsync(shutdown.Source.Token).ConfigureAwait(false);
            }

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

            if (trades is not null && !await trades.CompleteAndDrainAsync().ConfigureAwait(false))
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
