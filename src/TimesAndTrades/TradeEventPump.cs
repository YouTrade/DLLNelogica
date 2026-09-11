using System.Threading.Channels;
using DLLNelogica.MarketData;

namespace DLLNelogica.TimesAndTrades;

internal sealed class TradeEventPump : ITradeEventSink
{
    private readonly Channel<RawTrade> _events;
    private readonly TradeMetrics _metrics;
    private readonly TradeConsumer _consumer;
    private readonly CancellationTokenSource _shutdown;
    private int _started;

    internal TradeEventPump(
        int capacity,
        IEnumerable<string> instrumentKeys,
        TradeRecordFactory records,
        ITradeOutput output,
        CancellationTokenSource shutdown,
        TimeProvider? clock = null,
        Action<TradeRecord>? observed = null)
    {
        _metrics = new TradeMetrics(capacity, instrumentKeys);
        _shutdown = shutdown;
        _events = Channel.CreateBounded<RawTrade>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        _consumer = new TradeConsumer(_events.Reader, _metrics, records, output, ConsumerFailed,
            clock ?? TimeProvider.System, observed);
    }

    internal void MarkDrainTimeout() => _metrics.DrainTimedOut();

    internal Task Ready => _consumer.Ready;

    internal TradeMetricsSnapshot Snapshot => _metrics.Snapshot;

    public bool TryPublish(RawTrade trade)
    {
        // Roteamento mínimo antes da fila: nenhuma consulta nativa ou interpretação de negócio.
        // A lista é fixa; tickers externos não fazem crescer o dicionário de métricas.
        if (!Snapshot.Instruments.ContainsKey(trade.Instrument.Key))
        {
            _metrics.Uncorrelated();
            return true;
        }

        if (!_metrics.TryAccept(trade.Instrument.Key))
        {
            RequestStop();
            return false;
        }

        var published = _events.Writer.TryWrite(trade);
        _metrics.FinishPublish(published);
        CompleteWriterIfReady();
        if (!published)
        {
            RequestStop();
        }

        return published;
    }

    public void RecordTranslationFailure(MarketInstrument instrument)
    {
        _metrics.TranslationFailed(instrument.Key);
        RequestStop();
    }

    public void RecordCallbackFailure(MarketInstrument instrument)
    {
        _metrics.CallbackFailed(instrument.Key);
        RequestStop();
    }

    internal Task RunAsync()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("O consumidor de negócios só pode iniciar uma vez.");
        }

        return Task.Run(_consumer.RunAsync);
    }

    // Chamado após a origem parar. Reserva em andamento termina antes de fechar o writer.
    internal void Complete()
    {
        _metrics.Close();
        CompleteWriterIfReady();
    }

    private void CompleteWriterIfReady()
    {
        if (Snapshot.CanCompleteWriter)
        {
            _events.Writer.TryComplete();
        }
    }

    private void ConsumerFailed()
    {
        Complete();
        RequestStop();
    }

    private void RequestStop() => _ = RequestStopAsync();

    private async Task RequestStopAsync()
    {
        try
        {
            // CancelAsync marca o token e agenda os registros fora da thread nativa.
            await _shutdown.CancelAsync().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Sinalização também deve resistir a shutdown já descartado/falho.
        catch (Exception)
        {
        }
#pragma warning restore CA1031
    }
}
