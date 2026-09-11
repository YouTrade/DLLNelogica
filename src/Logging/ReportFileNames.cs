namespace DLLNelogica.Logging;

internal static class ReportFileNames
{
    internal static string Quote(string ticker, string exchange) => $"{Sanitize(ticker)}_{Sanitize(exchange)}.txt";
    internal static string Trades(string ticker, string exchange) =>
        $"{Sanitize(ticker)}_{Sanitize(exchange)}_TimesAndTrades.txt";

    // Regras Windows também quando a validação é executada em outro sistema operacional.
    internal static string Sanitize(string value)
    {
        var characters = value.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            if (characters[index] < 32 || "<>:\"/\\|?*".Contains(characters[index], StringComparison.Ordinal))
            {
                characters[index] = '_';
            }
        }

        return new string(characters);
    }
}
