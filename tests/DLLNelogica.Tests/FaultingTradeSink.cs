using DLLNelogica.MarketData;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

internal sealed class FaultingTradeSink(TradeEventPump pump) : ITradeEventSink
{
    public bool TryPublish(RawTrade trade) => throw new InvalidOperationException("Destino sintético falhou.");
    public void RecordCallbackFailure(MarketInstrument instrument) => pump.RecordCallbackFailure(instrument);
    public void RecordTranslationFailure(MarketInstrument instrument) => pump.RecordTranslationFailure(instrument);
}
