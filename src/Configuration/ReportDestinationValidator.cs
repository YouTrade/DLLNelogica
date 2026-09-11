using DLLNelogica.Logging;

namespace DLLNelogica.Configuration;

internal static class ReportDestinationValidator
{
    internal static void Validate(ApplicationOptions options, List<string> issues)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ReportFiles.Session, ReportFiles.Summary };
        foreach (var instrument in options.MarketData.Instruments)
        {
            Add(ReportFileNames.Quote(instrument.Ticker, instrument.Exchange), names, issues);
            if (options.TimesAndTrades.Enabled)
            {
                Add(ReportFileNames.Trades(instrument.Ticker, instrument.Exchange), names, issues);
            }
        }
    }

    private static void Add(string name, HashSet<string> names, List<string> issues)
    {
        if (!names.Add(name))
        {
            issues.Add($"Colisão entre destinos de relatório: {name}.");
        }
    }
}
