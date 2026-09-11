namespace DLLNelogica.TimesAndTrades;

internal readonly record struct TradeParticipant(int Id, string? Name, string Status);

// Raw conserva todos os campos nativos; textos permanecem sem escape até o gravador da sprint 3.
internal readonly record struct TradeRecord(
    RawTrade Raw,
    DateTime? TradeDate,
    string Type,
    string Aggressor,
    bool IsEdit,
    TradeParticipant Buyer,
    TradeParticipant Seller)
{
    internal const int SchemaVersion = 1;
}

internal interface ITradeOutput
{
    // Executado somente pelo consumidor. Retorno não confirma flush nem durabilidade em disco.
    ValueTask WriteAsync(TradeRecord trade);
}
