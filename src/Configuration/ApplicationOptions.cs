namespace DLLNelogica.Configuration;

internal sealed class ApplicationOptions
{
    public CredentialsOptions Credenciais { get; init; } = new();

    public MarketDataOptions MarketData { get; init; } = new();

    public TimesAndTradesOptions TimesAndTrades { get; init; } = new();
}
