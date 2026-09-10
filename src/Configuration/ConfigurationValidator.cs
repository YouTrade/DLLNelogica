namespace DLLNelogica.Configuration;

internal static class ConfigurationValidator
{
    private const int MaximumChannelCapacity = 1_000_000;
    private const int MaximumReportIntervalSeconds = 3600;

    internal static List<string> GetIssues(ApplicationOptions options)
    {
        var issues = new List<string>();

        ValidateValue(options.Credenciais.Key, "Credenciais.Key", issues);
        ValidateValue(options.Credenciais.User, "Credenciais.User", issues);
        ValidateValue(options.Credenciais.Password, "Credenciais.Password", issues);
        ValidateRange(
            options.MarketData.ChannelCapacity,
            1,
            MaximumChannelCapacity,
            "MarketData.ChannelCapacity",
            issues);
        ValidateRange(
            options.MarketData.ReportIntervalSeconds,
            1,
            MaximumReportIntervalSeconds,
            "MarketData.ReportIntervalSeconds",
            issues);
        ValidateInstruments(options.MarketData.Instruments, issues);

        return issues;
    }

    internal static bool HasOuterWhitespace(CredentialsOptions credentials) =>
        HasOuterWhitespace(credentials.Key) ||
        HasOuterWhitespace(credentials.User) ||
        HasOuterWhitespace(credentials.Password);

    internal static bool HasOuterWhitespace(IEnumerable<InstrumentOptions> instruments) =>
        instruments.Any(instrument =>
            HasOuterWhitespace(instrument.Ticker) || HasOuterWhitespace(instrument.Exchange));

    private static void ValidateValue(string value, string propertyName, List<string> issues)
    {
        if (value.Length == 0)
        {
            issues.Add($"{propertyName} está vazio.");
        }
        else if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add($"{propertyName} contém apenas espaços.");
        }
    }

    private static void ValidateRange(
        int value,
        int minimum,
        int maximum,
        string propertyName,
        List<string> issues)
    {
        if (value < minimum || value > maximum)
        {
            issues.Add($"{propertyName} deve estar no intervalo [{minimum}, {maximum}].");
        }
    }

    private static void ValidateInstruments(
        List<InstrumentOptions> instruments,
        List<string> issues)
    {
        if (instruments.Count == 0)
        {
            issues.Add("MarketData.Instruments deve conter pelo menos um instrumento.");
            return;
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var instrument in instruments)
        {
            ValidateValue(instrument.Ticker, $"MarketData.Instruments[{index}].Ticker", issues);
            ValidateValue(instrument.Exchange, $"MarketData.Instruments[{index}].Exchange", issues);

            if (!keys.Add($"{instrument.Ticker}:{instrument.Exchange}"))
            {
                issues.Add(
                    $"MarketData.Instruments contém o instrumento duplicado " +
                    $"{instrument.Ticker}:{instrument.Exchange}.");
            }

            index++;
        }
    }

    private static bool HasOuterWhitespace(string value) =>
        value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]));
}
