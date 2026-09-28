using System.Diagnostics;
using DLLNelogica.Configuration;
using DLLNelogica.Interop;
using DLLNelogica.MarketData;

namespace DLLNelogica.Tests;

public sealed class OrderConcurrencyTests
{
    [Fact]
    public async Task FinalizationWaitsForNativeOrderAndBlocksSubsequentCalls()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var api = new FakeProfitApi
        {
            OnOrderDetails = (ref TConnectorOrderOut _) =>
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
                return 0;
            }
        };
        var gateway = new ProfitOrderGateway(api, _ => null);
        gateway.Start();
        var query = Task.Run(() => gateway.Read(1, CancellationToken.None));
        Task? stop = null;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            stop = Task.Run(() => { gateway.StopAndWait(); api.FinalizeServices(); });
            Assert.True(SpinWait.SpinUntil(() => gateway.IsStopping, TimeSpan.FromSeconds(5)));
            Assert.Equal(0, api.FinalizationCalls);
        }
        finally
        {
            release.Set();
            await query;
            if (stop is not null) { await stop; }
        }
        Assert.Equal(1, api.FinalizationCalls);
        Assert.Throws<OperationCanceledException>(() => gateway.Read(1, CancellationToken.None));
        Assert.Throws<OperationCanceledException>(() => gateway.Cancel(new OrderSequenceOptions(), 1, CancellationToken.None));
        Assert.Throws<OperationCanceledException>(() => gateway.Send(new OrderSequenceOptions(), 1, true, 100, CancellationToken.None));
    }

    [Fact]
    public void OldQueuedQuoteIsNotFreshenedByConsumer()
    {
        var snapshot = new MarketDataSnapshot();
        var price = new RawPriceChange(new MarketInstrument("WINV26", "F", 0), "data", 1, 1, 1000)
        {
            ReceivedTimestamp = Stopwatch.GetTimestamp() - 31 * Stopwatch.Frequency
        };
        snapshot.Record(price);
        Assert.Null(snapshot.LatestPrice("WINV26:F"));
        snapshot.Record(price with { ReceivedTimestamp = Stopwatch.GetTimestamp() });
        Assert.Equal(1000, snapshot.LatestPrice("WINV26:F"));
    }
}
