using DLLNelogica.Connection;
using DLLNelogica.MarketData;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Interop;

internal sealed class ProfitCallbackBridge
{
    private readonly ConnectionStateEventPump _stateEvents;
    private readonly ProfitTradeTranslator _tradeTranslator;
    private ITradeEventSink? _tradeEvents;
    private MarketPriceEventPump? _marketPriceEvents;
    private CancellationTokenSource? _shutdownRequested;

    internal ProfitCallbackBridge(ConnectionStateEventPump stateEvents, IProfitApi profitApi)
    {
        _stateEvents = stateEvents;
        _tradeTranslator = new ProfitTradeTranslator(profitApi);
    }

    internal Guid TradeSessionId => _tradeTranslator.SessionId;

    internal bool HasTradeSink => Volatile.Read(ref _tradeEvents) is not null;

    internal void AttachTrades(ITradeEventSink tradeEvents)
    {
        var existing = Interlocked.CompareExchange(ref _tradeEvents, tradeEvents, null);
        if (existing is not null && !ReferenceEquals(existing, tradeEvents))
        {
            throw new InvalidOperationException("Um segundo destino de negócios foi bloqueado.");
        }
    }

    internal void DetachTrades(ITradeEventSink tradeEvents) =>
        Interlocked.CompareExchange(ref _tradeEvents, null, tradeEvents);

    internal void HandleTradeV2(
        TConnectorAssetIdentifier asset,
        nint tradePointer,
        TConnectorTradeCallbackFlags flags)
    {
        var events = Volatile.Read(ref _tradeEvents)
            ?? throw new InvalidOperationException("O pipeline de Times and Trades não está conectado.");
        var instrument = new MarketInstrument(asset.Ticker, asset.Exchange, asset.FeedType);
        try
        {
            var translation = _tradeTranslator.Translate(asset, tradePointer, flags);
            if (!translation.IsSuccessful)
            {
                events.RecordTranslationFailure(instrument);
                SignalFailureNoThrow();
            }
            else if (!events.TryPublish(translation.Trade!.Value))
            {
                SignalFailureNoThrow();
            }
        }
#pragma warning disable CA1031 // A raiz mantém a última barreira; contar falhas antes de sinalizar.
        catch (Exception)
        {
            events.RecordCallbackFailure(instrument);
            SignalFailureNoThrow();
        }
#pragma warning restore CA1031
    }

    internal void AttachShutdown(CancellationTokenSource shutdownRequested) =>
        Volatile.Write(ref _shutdownRequested, shutdownRequested);

    internal void DetachShutdown(CancellationTokenSource shutdownRequested) =>
        Interlocked.CompareExchange(ref _shutdownRequested, null, shutdownRequested);

    internal void AttachMarketData(MarketPriceEventPump marketPriceEvents) =>
        Volatile.Write(ref _marketPriceEvents, marketPriceEvents);

    internal void DetachMarketData(MarketPriceEventPump marketPriceEvents) =>
        Interlocked.CompareExchange(ref _marketPriceEvents, null, marketPriceEvents);

    internal void HandleState(int stateType, int result)
    {
        if (!_stateEvents.TryPublish(stateType, result) && !ProfitProcessLifetime.HasFinalizationStarted)
        {
            SignalFailureNoThrow();
        }
    }

#pragma warning disable CA1822 // Pontos de extensão de instância para o pipeline limitado da Aula 03.
    internal void HandleAccount(int brokerId, string? brokerName, string? accountId, string? ownerName)
    {
        // Aula futura: somente publicar em pipeline; callback nativo nunca executa I/O.
    }

    internal void HandleNewDaily(
        TAssetID assetId,
        string? date,
        double open,
        double high,
        double low,
        double close,
        double volume,
        double adjustment,
        double maxLimit,
        double minLimit,
        double volumeBuyer,
        double volumeSeller,
        int quantity,
        int tradesCount,
        int openContracts,
        int quantityBuyer,
        int quantitySeller,
        int tradesBuyer,
        int tradesSeller)
    {
        // Aula futura: publicar em canal limitado; callback nativo nunca executa I/O.
    }

    internal void HandleProgress(TAssetID assetId, int progress)
    {
        // Aula futura: publicar em canal limitado; callback nativo nunca executa I/O.
    }

    internal void HandleTinyBook(TAssetID assetId, double price, int quantity, int side)
    {
        // Aula futura: publicar em canal limitado; callback nativo nunca executa I/O.
    }
#pragma warning restore CA1822

    internal void HandleChangeCotation(
        TAssetID assetId,
        string? date,
        uint tradeNumber,
        double price)
    {
        var events = Volatile.Read(ref _marketPriceEvents)
            ?? throw new InvalidOperationException("O pipeline de Market Data não está conectado.");
        var instrument = new MarketInstrument(assetId.Ticker, assetId.Exchange, assetId.Feed);
        events.PublishPrice(instrument, date ?? string.Empty, tradeNumber, price);
    }

    internal void HandleInvalidTicker(TConnectorAssetIdentifier assetId)
    {
        var events = Volatile.Read(ref _marketPriceEvents)
            ?? throw new InvalidOperationException("O pipeline de Market Data não está conectado.");
        var instrument = new MarketInstrument(assetId.Ticker, assetId.Exchange, assetId.FeedType);
        if (!events.TryPublishInvalidTicker(instrument))
        {
            SignalFailureNoThrow();
        }
    }

    internal void SignalFailureNoThrow()
    {
        ProfitProcessLifetime.SignalCallbackFailure();

        var shutdown = Volatile.Read(ref _shutdownRequested);
        if (shutdown is not null)
        {
            _ = CancelShutdownNoThrowAsync(shutdown);
        }
    }

    private static async Task CancelShutdownNoThrowAsync(CancellationTokenSource shutdown)
    {
        try
        {
            await shutdown.CancelAsync().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Registros de cancelamento não executam na thread do callback.
        catch (Exception)
        {
        }
#pragma warning restore CA1031
    }
}
