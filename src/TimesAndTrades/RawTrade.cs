using DLLNelogica.Interop;
using DLLNelogica.MarketData;

namespace DLLNelogica.TimesAndTrades;

// Só valores e strings gerenciadas. A estrutura Trade é copiada por valor e não contém ponteiros.
// Preserva inclusive datas inválidas e códigos desconhecidos para interpretação no consumidor.
internal readonly record struct RawTrade(
    Guid SessionId,
    MarketInstrument Instrument,
    DateTimeOffset ReceivedAt,
    long ArrivalSequence,
    TConnectorTrade Trade,
    TConnectorTradeCallbackFlags Flags);
