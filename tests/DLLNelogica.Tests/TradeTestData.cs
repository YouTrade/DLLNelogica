using DLLNelogica.Interop;
using DLLNelogica.MarketData;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

internal static class TradeTestData
{
    internal static RawTrade Create(string ticker = "VALE3", string exchange = "B") => new(
        Guid.NewGuid(), new MarketInstrument(ticker, exchange, 7), DateTimeOffset.Now, 1,
        new TConnectorTrade
        {
            TradeDate = new SystemTime
            {
                Year = 2026, Month = 9, Day = 11, Hour = 10, Minute = 30, Second = 12, Milliseconds = 123
            },
            TradeNumber = uint.MaxValue,
            Price = 60.25,
            Quantity = (long)int.MaxValue + 10,
            Volume = 100.75,
            BuyAgent = 123,
            SellAgent = 456,
            TradeType = TradeType.AggressorBuyer
        }, TConnectorTradeCallbackFlags.None);

    internal static TradeRecordFactory Records(bool resolve = false) => new(
        new TradeParticipantCache(resolve, (_, _) => new(true, null, (int)NResult.NL_NOT_FOUND, null)));

    internal static TradeEventPump Pump(int capacity, ITradeOutput output, CancellationTokenSource shutdown) =>
        new(capacity, ["VALE3:B", "WINV26:F"], Records(), output, shutdown);

    internal static void AssertBalanced(TradeMetricsSnapshot snapshot)
    {
        var total = snapshot.Total;
        Assert.Equal(total.Callbacks, total.Accepted + total.RejectedFull + total.RejectedClosed +
            total.TranslationFailures + total.CallbackFailures + total.Uncorrelated);
        Assert.Equal(total.Delivered, total.Additions + total.Edits);
        Assert.True(total.Pending >= 0);
        Assert.True(snapshot.QueueOccupancy >= 0);
        Assert.True(snapshot.Publishing >= 0);
        foreach (var counters in snapshot.Instruments.Values)
        {
            Assert.Equal(counters.Callbacks, counters.Accepted + counters.RejectedFull + counters.RejectedClosed +
                counters.TranslationFailures + counters.CallbackFailures);
            Assert.Equal(counters.Delivered, counters.Additions + counters.Edits);
            Assert.True(counters.Pending >= 0);
        }
    }
}
