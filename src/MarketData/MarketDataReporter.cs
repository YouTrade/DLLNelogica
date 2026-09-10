using System.Globalization;
using System.Text;
using DLLNelogica.Logging;

namespace DLLNelogica.MarketData;

// O relatório periódico que o ReportIntervalSeconds sempre prometeu. Ele é a única visão
// contínua do mercado: o arquivo por instrumento guarda tick a tick para análise depois, e
// aqui sai uma amostra por intervalo, legível enquanto a aplicação roda.
internal sealed class MarketDataReporter
{
    private readonly MarketDataSnapshot _snapshot;
    private readonly MarketDataMetrics _metrics;
    private readonly IReportLog _reportLog;
    private readonly TimeSpan _interval;

    internal MarketDataReporter(
        MarketDataSnapshot snapshot,
        MarketDataMetrics metrics,
        IReportLog reportLog,
        int intervalSeconds)
    {
        _snapshot = snapshot;
        _metrics = metrics;
        _reportLog = reportLog;
        _interval = TimeSpan.FromSeconds(intervalSeconds);
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                Report();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
#pragma warning disable CA1031 // A observabilidade nunca pode derrubar o encerramento do pipeline.
        catch (Exception exception)
        {
            TryReportFailure(exception);
        }
#pragma warning restore CA1031
    }

    private void Report()
    {
        var instruments = _snapshot.TakeInterval();
        if (instruments.Count == 0)
        {
            return;
        }

        var line = new StringBuilder("Market data");
        foreach (var instrument in instruments)
        {
            line.Append(" | ")
                .Append(instrument.Key)
                .Append(' ')
                .Append(instrument.Price.ToString("0.####", CultureInfo.CurrentCulture))
                .Append(" (")
                .Append(instrument.IntervalTicks.ToString(CultureInfo.CurrentCulture))
                .Append(')');
        }

        line.Append(" | descartadas=").Append(_metrics.DroppedPriceChanges.ToString(CultureInfo.CurrentCulture));
        _reportLog.WriteToFileAndConsole(ReportFiles.Summary, line.ToString());
    }

    private static void TryReportFailure(Exception exception)
    {
        try
        {
            Console.Error.WriteLine(
                $"Falha no relatório periódico de market data ({exception.GetType().Name}); " +
                "a coleta de cotações continua.");
        }
        catch
        {
            // A falha de saída não pode interromper o encerramento do pipeline.
        }
    }
}
