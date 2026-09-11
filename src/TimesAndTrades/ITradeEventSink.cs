using DLLNelogica.MarketData;

namespace DLLNelogica.TimesAndTrades;

internal interface ITradeEventSink
{
    // Fronteira para o canal da sprint 2: não esperar, fazer I/O ou propagar trabalho síncrono.
    bool TryPublish(RawTrade trade);

    void RecordTranslationFailure(MarketInstrument instrument);

    void RecordCallbackFailure(MarketInstrument instrument);
}
