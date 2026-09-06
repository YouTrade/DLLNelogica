using System.Threading.Channels;

namespace DLLNelogica.MarketData;

internal sealed class MarketPriceEventPump
{
    private const int ControlChannelCapacity = 32;
    private readonly Channel<RawPriceChange> _priceChanges;
    private readonly Channel<InvalidTickerEvent> _invalidTickers;
    private readonly MarketDataMetrics _metrics;
    private long _arrivalSequence;
    private int _priceConsumerFailure;
    private int _invalidTickerConsumerFailure;
    private int _fatalInvalidTicker;
    private int _firstPriceReported;

    internal MarketPriceEventPump(int channelCapacity, MarketDataMetrics metrics)
    {
        _metrics = metrics;
        _priceChanges = Channel.CreateBounded<RawPriceChange>(new BoundedChannelOptions(channelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        _invalidTickers = Channel.CreateBounded<InvalidTickerEvent>(
            new BoundedChannelOptions(ControlChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });
    }

    internal bool HasFailed =>
        Volatile.Read(ref _priceConsumerFailure) != 0 ||
        Volatile.Read(ref _invalidTickerConsumerFailure) != 0;

    internal bool HasFatalInvalidTicker => Volatile.Read(ref _fatalInvalidTicker) != 0;

    internal void PublishPrice(
        MarketInstrument instrument,
        string nativeDateText,
        uint nativeSequenceNumber,
        double price)
    {
        _metrics.IncrementReceivedPriceChanges();
        var priceChange = new RawPriceChange(
            instrument,
            nativeDateText,
            nativeSequenceNumber,
            Interlocked.Increment(ref _arrivalSequence),
            price);
        if (!_priceChanges.Writer.TryWrite(priceChange))
        {
            _metrics.IncrementDroppedPriceChanges();
        }
    }

    internal bool TryPublishInvalidTicker(MarketInstrument instrument)
    {
        _metrics.IncrementInvalidTickerCount();
        return _invalidTickers.Writer.TryWrite(new InvalidTickerEvent(instrument));
    }

    internal void Complete()
    {
        _priceChanges.Writer.TryComplete();
        _invalidTickers.Writer.TryComplete();
    }

    internal async Task ObservePricesAsync(CancellationTokenSource shutdownRequested)
    {
        try
        {
            await foreach (var priceChange in _priceChanges.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                ReportFirstPriceChange(priceChange);
            }
        }
        catch (Exception exception)
        {
            ReportConsumerFailure("cotações", exception);
            Interlocked.Exchange(ref _priceConsumerFailure, 1);
            CancelNoThrow(shutdownRequested);
        }
    }

    internal async Task ObserveInvalidTickersAsync(
        MarketDataSubscriptionManager subscriptions,
        CancellationTokenSource shutdownRequested)
    {
        try
        {
            await foreach (var invalidTicker in _invalidTickers.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                var instrument = invalidTicker.Instrument;
                if (subscriptions.IsCorrelatedWithAcceptedSubscription(instrument))
                {
                    Interlocked.Exchange(ref _fatalInvalidTicker, 1);
                    TryWriteLine(
                        $"Ticker inválido para assinatura ativa: {instrument.Key}; encerramento solicitado.");
                    CancelNoThrow(shutdownRequested);
                }
                else
                {
                    TryWriteLine(
                        $"Aviso: ticker inválido não correlacionado a assinatura ativa: {instrument.Key}.");
                }
            }
        }
        catch (Exception exception)
        {
            ReportConsumerFailure("tickers inválidos", exception);
            Interlocked.Exchange(ref _invalidTickerConsumerFailure, 1);
            CancelNoThrow(shutdownRequested);
        }
    }

    private void ReportFirstPriceChange(RawPriceChange priceChange)
    {
        if (Interlocked.CompareExchange(ref _firstPriceReported, 1, 0) != 0)
        {
            return;
        }

        TryWriteLine(
            $"Gate Sprint 3: primeira TChangeCotation recebida | " +
            $"instrumento={priceChange.Instrument.Key} | " +
            $"pwcDate={priceChange.NativeDateText} | " +
            $"sequência={priceChange.NativeSequenceNumber} | preço={priceChange.Price}.");
    }

    private static void TryWriteLine(string message)
    {
        try
        {
            Console.WriteLine(message);
        }
        catch
        {
            // A observabilidade nunca interfere no consumo do canal.
        }
    }

    private static void CancelNoThrow(CancellationTokenSource shutdownRequested)
    {
        try
        {
            shutdownRequested.Cancel();
        }
        catch
        {
            // O consumidor nunca propaga uma segunda falha durante o encerramento.
        }
    }

    private static void ReportConsumerFailure(string consumer, Exception exception)
    {
        try
        {
            Console.Error.WriteLine(
                $"Falha no consumidor de {consumer} ({exception.GetType().Name}); encerramento solicitado.");
        }
        catch
        {
            // A sinalização de falha não depende da disponibilidade da saída.
        }
    }
}
