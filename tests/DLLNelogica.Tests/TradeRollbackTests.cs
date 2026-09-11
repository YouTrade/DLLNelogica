using DLLNelogica.Configuration;
using DLLNelogica.Interop;
using DLLNelogica.MarketData;

namespace DLLNelogica.Tests;

public sealed class TradeRollbackTests
{
    [Fact]
    public async Task FailedSecondSubscriptionPreservesAcceptedTradeAndRollsBackOnce()
    {
        using var fixture = new TradeFileFixture();
        using var shutdown = new CancellationTokenSource();
        var api = new FakeProfitApi { Trade = fixture.Record().Raw.Trade };
        var bridge = CallbackRegistrationTests.CreateBridge(api);
        var pump = TradeStorageTestFactory.Create(fixture, shutdown);
        bridge.AttachTrades(pump);
        var session = new ProfitSession(api, bridge);
        var worker = pump.RunAsync();
        try
        {
            await pump.Ready;
            Assert.True(session.RegisterMarketDataCallbacks(true).IsSuccessful);
            TradeSubscriptionAssertions.FailSecondAndCheckRollback(api, bridge, session);
        }
        finally
        {
            pump.Complete();
            await worker.WaitAsync(TimeSpan.FromSeconds(10));
            bridge.DetachTrades(pump);
        }

        Assert.Equal(1, pump.Snapshot.Total.Confirmed);
        Assert.Single(fixture.AllEvents());
    }
}
