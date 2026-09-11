using DLLNelogica.Configuration;
using DLLNelogica.Logging;

namespace DLLNelogica.MarketData;

internal readonly record struct MarketInstrument(string Ticker, string Exchange, int Feed)
{
    internal string Key => $"{Ticker}:{Exchange}";

    // O ticker vem do appsettings.json e vira nome de arquivo, então precisa ser higienizado:
    // separadores de caminho e caracteres reservados viram sublinhado antes de tocar o disco.
    internal string ReportFileName => ReportFileNames.Quote(Ticker, Exchange);

    internal static MarketInstrument FromOptions(InstrumentOptions options) =>
        new(options.Ticker, options.Exchange, 0);

}
