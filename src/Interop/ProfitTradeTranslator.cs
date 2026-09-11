using DLLNelogica.MarketData;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Interop;

internal sealed class ProfitTradeTranslator(IProfitApi profitApi)
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private long _arrivalSequence;

    internal Guid SessionId => _sessionId;

    // TranslateTrade é acessória: materializar ainda dentro do callback (manual, p. 78).
    internal TradeTranslationResult Translate(
        TConnectorAssetIdentifier asset,
        nint tradePointer,
        TConnectorTradeCallbackFlags flags)
    {
        var receivedAt = DateTimeOffset.Now;
        var sequence = Interlocked.Increment(ref _arrivalSequence);
        if (tradePointer == nint.Zero)
        {
            return new(NativeCallResult.Completed("TranslateTrade", (int)NResult.NL_INVALID_ARGS), null);
        }

        try
        {
            var trade = new TConnectorTrade { Version = 0 };
            var call = NativeCallResult.Completed("TranslateTrade", profitApi.TranslateTrade(tradePointer, ref trade));
            return call.IsSuccessful
                ? new(call, new RawTrade(
                    _sessionId,
                    new MarketInstrument(asset.Ticker, asset.Exchange, asset.FeedType),
                    receivedAt,
                    sequence,
                    trade,
                    flags))
                : new(call, null);
        }
#pragma warning disable CA1031 // Falhas da tradução viram resultado; nenhuma exceção atravessa a DLL.
        catch (Exception exception)
        {
            return new(NativeCallResult.Failed("TranslateTrade", exception), null);
        }
#pragma warning restore CA1031
    }
}
