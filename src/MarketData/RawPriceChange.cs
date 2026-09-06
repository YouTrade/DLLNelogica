namespace DLLNelogica.MarketData;

internal readonly record struct RawPriceChange(
    MarketInstrument Instrument,
    string NativeDateText,
    uint NativeSequenceNumber,
    long ArrivalSequence,
    double Price);
