using System.Text.Json;

namespace DLLNelogica.Configuration;

internal static class OrderSequenceValidation
{
    internal static void ValidateShape(JsonElement root)
    {
        if (!root.TryGetProperty("OrderSequence", out var section)) { return; }
        try
        {
            if (section.ValueKind != JsonValueKind.Object ||
                section.Deserialize<OrderSequenceOptions>() is not { WaitSeconds: not null, AccountId: not null,
                    RoutingPassword: not null, Ticker: not null, Exchange: not null })
            {
                throw new ConfigurationException("OrderSequence deve ser um objeto com campos válidos, sem null.");
            }
        }
        catch (JsonException)
        {
            throw new ConfigurationException("OrderSequence: tipos inválidos; use booleanos, textos, números e WaitSeconds como lista de inteiros.");
        }
    }

    internal static void Validate(ApplicationOptions application, List<string> issues)
    {
        var value = application.OrderSequence;
        if (!value.Enabled) { return; }
        if (value.BrokerId <= 0)
        {
            issues.Add("OrderSequence.BrokerId deve ser maior que zero. Consulte o id da corretora no relatório de contas obtidas da DLL; execute com OrderSequence.Enabled=false para consultar.");
        }
        if (string.IsNullOrWhiteSpace(value.AccountId) || string.IsNullOrWhiteSpace(value.RoutingPassword))
        {
            issues.Add("OrderSequence: informe AccountId e RoutingPassword.");
        }
        if (value.Quantity <= 0 || value.TickSize <= 0 ||
            (value.BuyLimit && value.BuyLimitOffset <= 0) || (value.SellLimit && value.SellLimitOffset <= 0))
        {
            issues.Add("OrderSequence: Quantity e TickSize devem ser positivos; BuyLimitOffset e SellLimitOffset devem ser positivos quando a respectiva limitada estiver ligada.");
        }
        if (value.WaitSeconds.Length != 5 || value.WaitSeconds.Any(seconds => seconds < 0 || seconds > 3600))
        {
            issues.Add("OrderSequence.WaitSeconds deve conter cinco esperas entre 0 e 3600 segundos.");
        }
        if (!application.MarketData.Instruments.Any(instrument =>
            instrument.Ticker == value.Ticker && instrument.Exchange == value.Exchange))
        {
            issues.Add("OrderSequence: o ativo e a bolsa devem estar em MarketData.Instruments.");
        }
    }
}
