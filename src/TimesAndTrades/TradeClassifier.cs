using DLLNelogica.Interop;

namespace DLLNelogica.TimesAndTrades;

internal static class TradeClassifier
{
    internal static string Describe(TradeType type) => type switch
    {
        TradeType.CrossTrade => "CrossTrade",
        TradeType.AggressorBuyer => "CompraAgressao",
        TradeType.AggressorSeller => "VendaAgressao",
        TradeType.Auction => "Leilao",
        TradeType.Surveillance => "Surveillance",
        TradeType.Expit => "Expit",
        TradeType.OptionExercise => "OptionsExercise",
        TradeType.OverTheCounter => "OverTheCounter",
        TradeType.DerivativeTerm => "DerivativeTerm",
        TradeType.Index => "Index",
        TradeType.BTC => "BTC",
        TradeType.OnBehalf => "OnBehalf",
        TradeType.RLP => "RLP",
        TradeType.BBT => "BBT",
        TradeType.RFQ => "RFQ",
        TradeType.MPT => "MPT",
        TradeType.TAC => "TAC",
        TradeType.TAA => "TAA",
        TradeType.Unknown => "Desconhecido",
        TradeType.Update => "Update",
        TradeType.Mid => "Mid",
        TradeType.OffExchange => "OffExchange",
        _ => "NaoMapeado"
    };

    internal static string Aggressor(TradeType type) => type switch
    {
        TradeType.AggressorBuyer => "comprador",
        TradeType.AggressorSeller => "vendedor",
        _ => "nao_classificado"
    };

    internal static DateTime? GetDate(SystemTime date)
    {
        try
        {
            return new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute,
                date.Second, date.Milliseconds, DateTimeKind.Unspecified);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
