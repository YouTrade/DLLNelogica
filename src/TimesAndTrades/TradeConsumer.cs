using System.Threading.Channels;

namespace DLLNelogica.TimesAndTrades;

internal sealed class TradeConsumer(
    ChannelReader<RawTrade> reader,
    TradeMetrics metrics,
    TradeRecordFactory records,
    ITradeOutput output,
    Action failed,
    TimeProvider clock,
    Action<TradeRecord>? observed)
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task Ready => _ready.Task;

    internal async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        try
        {
            Maintain(false);
            _ready.TrySetResult();
            var available = reader.WaitToReadAsync().AsTask();
            var tick = timer.WaitForNextTickAsync().AsTask();
            while (true)
            {
                await Task.WhenAny(available, tick).ConfigureAwait(false);
                if (tick.IsCompleted)
                {
                    await tick.ConfigureAwait(false);
                    Maintain(false, true);
                    tick = timer.WaitForNextTickAsync().AsTask();
                }

                if (available.IsCompleted)
                {
                    if (!await available.ConfigureAwait(false))
                    {
                        break;
                    }

                    // Lotes limitados também dão oportunidade ao timer sob fluxo contínuo.
                    for (var count = 0; count < TradeFileOutput.BatchSize && reader.TryRead(out var raw); count++)
                    {
                        await ConsumeAsync(raw).ConfigureAwait(false);
                        Maintain(false);
                    }

                    available = reader.WaitToReadAsync().AsTask();
                }
            }

            Maintain(true);
        }
#pragma warning disable CA1031 // Falhas ficam nos contadores; a aplicação encerra e não repete eventos.
        catch (Exception exception)
        {
            _ready.TrySetException(exception);
            metrics.ConsumerFailed(exception);
            failed();
        }
        finally
        {
            try
            {
                (output as ITradeStorage)?.Dispose();
            }
            catch (Exception exception)
            {
                metrics.ConsumerFailed(exception);
                failed();
            }
        }
#pragma warning restore CA1031
    }

    private async ValueTask ConsumeAsync(RawTrade raw)
    {
        metrics.Dequeued();
        var before = records.LookupFailures;
        var trade = records.Create(raw);
        metrics.Interpreted(raw.Instrument.Key, trade, records.LookupFailures - before);
        await output.WriteAsync(trade).ConfigureAwait(false);
        metrics.Delivered(raw.Instrument.Key, trade);
        observed?.Invoke(trade);
    }

    private void Maintain(bool complete, bool forceFlush = false)
    {
        if (output is ITradeStorage storage)
        {
            foreach (var record in storage.Maintain(clock.GetLocalNow(), complete, forceFlush))
            {
                metrics.Confirmed(record);
            }
        }
    }
}
