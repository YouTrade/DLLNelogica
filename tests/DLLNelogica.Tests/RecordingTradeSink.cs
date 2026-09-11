using DLLNelogica.TimesAndTrades;
using DLLNelogica.MarketData;

namespace DLLNelogica.Tests;

internal sealed class RecordingTradeSink : ITradeEventSink
{
    internal List<RawTrade> Trades { get; } = [];
    internal bool Accept { get; set; } = true;
    internal bool ThrowOnPublish { get; set; }

    public void RecordTranslationFailure(MarketInstrument instrument) { }

    public void RecordCallbackFailure(MarketInstrument instrument) { }

    public bool TryPublish(RawTrade trade)
    {
        if (ThrowOnPublish)
        {
            throw new InvalidOperationException("Falha sintética do destino.");
        }

        if (!Accept)
        {
            return false;
        }

        Trades.Add(trade);
        return true;
    }
}
