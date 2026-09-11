using System.Collections.Immutable;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

public sealed class TradeFinalReportTests
{
    [Fact]
    public void FinalIncludesInstrumentReconciliationPeakAndDrainDuration()
    {
        var reports = new TradeReportProbe();
        var counters = new TradeCounters
        {
            Callbacks = 4, Accepted = 3, Delivered = 3, Additions = 2, Edits = 1,
            Confirmed = 3, ConfirmedAdditions = 2, ConfirmedEdits = 1, RejectedFull = 1
        };
        var snapshot = new TradeMetricsSnapshot(counters,
            ImmutableDictionary<string, TradeCounters>.Empty.Add("VALE3:B", counters),
            0, 3, 0, true, true, 0, 0);

        new TradeReporter(reports).Final(snapshot, false, TimeSpan.FromMilliseconds(12.5));

        var lines = reports.Lines.ToArray();
        Assert.Equal(2, lines.Length);
        Assert.Contains("pico_fila=3 | pendentes=0 | drenagem_ms=12.500", lines[0].Message, StringComparison.Ordinal);
        Assert.Contains("incompleta=True", lines[0].Message, StringComparison.Ordinal);
        Assert.Contains("instrumento=VALE3:B | callbacks=4 | aceitos=3 | adicoes=2 | edicoes=1 | recusados=1", lines[1].Message, StringComparison.Ordinal);
        Assert.Contains("confirmados=3 | adicoes_confirmadas=2 | edicoes_confirmadas=1 | nao_confirmados=0", lines[1].Message, StringComparison.Ordinal);
    }
}
