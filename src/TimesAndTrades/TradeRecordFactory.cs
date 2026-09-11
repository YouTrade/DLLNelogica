using DLLNelogica.Interop;

namespace DLLNelogica.TimesAndTrades;

internal sealed class TradeRecordFactory(TradeParticipantCache participants)
{
    internal static TradeRecordFactory ForSession(bool resolveNames, ProfitSession session) =>
        new(new TradeParticipantCache(resolveNames, session.LookupAgentName));

    internal long LookupFailures => participants.LookupFailures;

    internal TradeRecord Create(RawTrade raw) => new(
        raw,
        TradeClassifier.GetDate(raw.Trade.TradeDate),
        TradeClassifier.Describe(raw.Trade.TradeType),
        TradeClassifier.Aggressor(raw.Trade.TradeType),
        (raw.Flags & TConnectorTradeCallbackFlags.IsEdit) != 0,
        participants.Resolve(raw.Trade.BuyAgent),
        participants.Resolve(raw.Trade.SellAgent));
}
