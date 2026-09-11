using System.Globalization;
using System.Text;
using DLLNelogica.Logging;

namespace DLLNelogica.TimesAndTrades;

internal sealed class TradeReporter(IReportLog reports)
{
    private readonly HashSet<string> _first = new(StringComparer.OrdinalIgnoreCase);
    private TradeMetricsSnapshot? _previous;

    // Invocado somente pelo consumidor; a primeira operação não depende do intervalo do resumo.
    internal void Observe(TradeRecord trade)
    {
        if (_first.Add(trade.Raw.Instrument.Key))
        {
            Write(ReportFiles.Session, $"Primeiro negócio T&T | instrumento={TradeLineFormatter.Escape(trade.Raw.Instrument.Key)} | sessao={trade.Raw.SessionId:D}");
        }
    }

    internal void Report(TradeMetricsSnapshot snapshot)
    {
        var line = new StringBuilder("Times and trades");
        foreach (var (key, counters) in snapshot.Instruments.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var prior = _previous?.Instruments.GetValueOrDefault(key) ?? default;
            line.Append(FormattableString.Invariant(
                $" | {TradeLineFormatter.Escape(key)} adicoes={counters.Additions - prior.Additions} edicoes={counters.Edits - prior.Edits}"));
            line.Append(counters.LastTrade is { } last
                ? $" ultimo={last.Raw.Trade.Price.ToString("R", CultureInfo.InvariantCulture)}"
                : " sem_eventos");
        }

        line.Append(FormattableString.Invariant(
            $" | confirmados={snapshot.Total.Confirmed} | nao_confirmados={snapshot.Total.Unconfirmed} | recusados={snapshot.Total.RejectedFull + snapshot.Total.RejectedClosed} | fila={snapshot.QueueOccupancy} | captura={(snapshot.IsIncomplete ? "incompleta" : "sem_perda_local_conhecida")}"));
        Write(ReportFiles.Summary, line.ToString());
        _previous = snapshot;
    }

    internal void Final(TradeMetricsSnapshot snapshot, bool timeout, TimeSpan drainDuration)
    {
        var total = snapshot.Total;
        Write(ReportFiles.Session, FormattableString.Invariant(
            $"Resumo final T&T | callbacks={total.Callbacks} | aceitos={total.Accepted} | entregues={total.Delivered} | confirmados={total.Confirmed} | nao_confirmados={total.Unconfirmed} | adicoes_confirmadas={total.ConfirmedAdditions} | edicoes_confirmadas={total.ConfirmedEdits} | recusados={total.RejectedFull + total.RejectedClosed} | falhas_traducao={total.TranslationFailures} | falhas_callback={total.CallbackFailures} | fora_da_lista={total.Uncorrelated} | falhas_publicacao={snapshot.PublicationFailures} | falhas_consumidor={snapshot.ConsumerFailures} | erro={snapshot.ConsumerFailureType ?? "NA"} | pico_fila={snapshot.PeakQueueOccupancy} | pendentes={total.Pending} | drenagem_ms={drainDuration.TotalMilliseconds:F3} | timeout={timeout} | incompleta={snapshot.IsIncomplete || timeout}"));
        foreach (var (key, counters) in snapshot.Instruments.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            Write(ReportFiles.Session, FormattableString.Invariant(
                $"Final T&T instrumento={TradeLineFormatter.Escape(key)} | callbacks={counters.Callbacks} | aceitos={counters.Accepted} | adicoes={counters.Additions} | edicoes={counters.Edits} | recusados={counters.RejectedFull + counters.RejectedClosed} | falhas_traducao={counters.TranslationFailures} | falhas_callback={counters.CallbackFailures} | confirmados={counters.Confirmed} | adicoes_confirmadas={counters.ConfirmedAdditions} | edicoes_confirmadas={counters.ConfirmedEdits} | nao_confirmados={counters.Unconfirmed}"));
        }
    }

    internal void Session(string message) => Write(ReportFiles.Session, message);

    private void Write(string file, string message)
    {
        try
        {
            reports.WriteToFileAndConsole(file, message);
        }
#pragma warning disable CA1031 // A observabilidade não altera confirmação de escrita dos negócios.
        catch (Exception)
        {
        }
#pragma warning restore CA1031
    }
}
