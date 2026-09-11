using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

internal static class TradeStorageTestFactory
{
    internal static TradeEventPump Create(TradeFileFixture fixture, CancellationTokenSource shutdown,
        Action<TradeRecord>? observed = null) =>
        new(4, fixture.Destinations.Keys, TradeTestData.Records(), fixture.Output(), shutdown,
            new FixedTradeClock(fixture.Now), observed);
}
