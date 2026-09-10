using DLLNelogica.Configuration;

namespace DLLNelogica.MarketData;

internal readonly record struct MarketInstrument(string Ticker, string Exchange, int Feed)
{
    internal string Key => $"{Ticker}:{Exchange}";

    // O ticker vem do appsettings.json e vira nome de arquivo, então precisa ser higienizado:
    // separadores de caminho e caracteres reservados viram sublinhado antes de tocar o disco.
    internal string ReportFileName => $"{Sanitize(Ticker)}_{Sanitize(Exchange)}.txt";

    internal static MarketInstrument FromOptions(InstrumentOptions options) =>
        new(options.Ticker, options.Exchange, 0);

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = value.ToCharArray();
        for (var index = 0; index < sanitized.Length; index++)
        {
            if (Array.IndexOf(invalid, sanitized[index]) >= 0)
            {
                sanitized[index] = '_';
            }
        }

        return new string(sanitized);
    }
}
