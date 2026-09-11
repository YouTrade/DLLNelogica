using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

public sealed class TradeConcurrencyTests
{
    [Fact]
    public async Task MultipleProducersDeliverEightThousandEventsWithConsistentSnapshots()
    {
        using var shutdown = new CancellationTokenSource();
        var output = new TradeOutputProbe();
        var pump = TradeTestData.Pump(8000, output, shutdown);
        var consumer = pump.RunAsync();
        var producers = Enumerable.Range(0, 4).Select(index => Task.Run(() =>
        {
            var raw = index % 2 == 0 ? TradeTestData.Create() : TradeTestData.Create("WINV26", "F");
            for (var arrival = 0; arrival < 2000; arrival++)
            {
                Assert.True(pump.TryPublish(raw with { ArrivalSequence = index * 2000 + arrival }));
                TradeTestData.AssertBalanced(pump.Snapshot);
            }
        })).ToArray();
        try
        {
            await Task.WhenAll(producers).WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            pump.Complete();
            await consumer.WaitAsync(TimeSpan.FromSeconds(30));
        }

        Assert.Equal(8000, output.Records.Count);
        Assert.Equal(8000, output.Records.Select(record => record.Raw.ArrivalSequence).Distinct().Count());
        Assert.Equal(4000, pump.Snapshot.Instruments["VALE3:B"].Delivered);
        Assert.Equal(4000, pump.Snapshot.Instruments["WINV26:F"].Delivered);
        Assert.Equal(0, pump.Snapshot.Total.Pending);
        Assert.Equal(0, pump.Snapshot.Publishing);
        Assert.Equal(0, pump.Snapshot.QueueOccupancy);
        Assert.False(pump.Snapshot.IsIncomplete);
    }

    [Fact]
    public async Task ConcurrentCompletionAccountsForEveryPublication()
    {
        using var shutdown = new CancellationTokenSource();
        var output = new TradeOutputProbe();
        var pump = TradeTestData.Pump(2000, output, shutdown);
        var consumer = pump.RunAsync();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var producer = Task.Run(async () =>
        {
            await start.Task;
            var raw = TradeTestData.Create();
            for (var index = 0; index < 2000; index++)
            {
                pump.TryPublish(raw with { ArrivalSequence = index });
            }
        });
        var complete = Task.Run(async () =>
        {
            await start.Task;
            pump.Complete();
        });
        start.SetResult();
        await Task.WhenAll(producer, complete).WaitAsync(TimeSpan.FromSeconds(30));
        await consumer.WaitAsync(TimeSpan.FromSeconds(30));
        var snapshot = pump.Snapshot;
        Assert.Equal(2000, snapshot.Total.Callbacks);
        Assert.Equal(snapshot.Total.Accepted, snapshot.Total.Delivered);
        Assert.Equal(0, snapshot.PublicationFailures);
        Assert.Equal(0, snapshot.Publishing);
        Assert.Equal(0, snapshot.QueueOccupancy);
        TradeTestData.AssertBalanced(snapshot);
    }
}
