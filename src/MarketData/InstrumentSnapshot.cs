namespace DLLNelogica.MarketData;

internal readonly record struct InstrumentSnapshot(string Key, double Price, long IntervalTicks);
