using DLLNelogica.Configuration;

namespace DLLNelogica.MarketData;

internal readonly record struct MarketInstrument(string Ticker, string Exchange, int Feed)
{
    internal string Key => $"{Ticker}:{Exchange}";

    internal static MarketInstrument FromOptions(InstrumentOptions options) =>
        new(options.Ticker, options.Exchange, 0);
}
