namespace DLLNelogica.Configuration;

internal sealed class ApplicationOptions
{
    public OrderSequenceOptions OrderSequence { get; init; } = new();

    public CredentialsOptions Credenciais { get; init; } = new();

    public MarketDataOptions MarketData { get; init; } = new();

    public TimesAndTradesOptions TimesAndTrades { get; init; } = new();
}
