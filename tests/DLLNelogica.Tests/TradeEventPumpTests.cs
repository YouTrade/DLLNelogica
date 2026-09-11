using DLLNelogica.Interop;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

public sealed class TradeEventPumpTests
{
    [Fact]
    public async Task DuplicatesEditsAndLastPacketAreDeliveredWithoutConsolidation()
    {
        using var shutdown = new CancellationTokenSource();
        var output = new TradeOutputProbe();
        var pump = TradeTestData.Pump(8, output, shutdown);
        var raw = TradeTestData.Create();
        Assert.True(pump.TryPublish(raw with { Flags = TConnectorTradeCallbackFlags.IsEdit }));
        Assert.True(pump.TryPublish(raw));
        Assert.True(pump.TryPublish(raw));
        Assert.True(pump.TryPublish(raw with { Flags = TConnectorTradeCallbackFlags.LastPacket }));
        Assert.True(pump.TryPublish(TradeTestData.Create("WINV26", "F")));
        var before = pump.Snapshot;
        pump.Complete();
        await pump.RunAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(5, output.Records.Count);
        Assert.True(output.Records[0].IsEdit);
        Assert.Equal(output.Records[1], output.Records[2]);
        var snapshot = pump.Snapshot;
        Assert.Equal(4, snapshot.Total.Additions);
        Assert.Equal(1, snapshot.Total.Edits);
        Assert.Equal(0, snapshot.Total.Pending);
        Assert.Equal(0, snapshot.QueueOccupancy);
        Assert.Equal(4, snapshot.Instruments["VALE3:B"].Delivered);
        Assert.Equal(1, snapshot.Instruments["WINV26:F"].Delivered);
        Assert.NotNull(snapshot.Instruments["WINV26:F"].LastTrade);
        Assert.Equal(5, before.Total.Pending); // Snapshot antigo é imutável.
        Assert.False(snapshot.IsIncomplete);
        TradeTestData.AssertBalanced(snapshot);
    }

    [Fact]
    public async Task UnconfiguredInstrumentsAreCountedWithoutAllocatingDestinations()
    {
        using var shutdown = new CancellationTokenSource();
        var output = new TradeOutputProbe();
        var pump = TradeTestData.Pump(2, output, shutdown);
        Assert.True(pump.TryPublish(TradeTestData.Create("OTHER")));
        Assert.True(pump.TryPublish(TradeTestData.Create("vale3", "b")));
        pump.Complete();
        await pump.RunAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(output.Records);
        Assert.Equal(2, pump.Snapshot.Instruments.Count);
        Assert.Equal(1, pump.Snapshot.Total.Uncorrelated);
        Assert.False(shutdown.IsCancellationRequested);
        TradeTestData.AssertBalanced(pump.Snapshot);
    }

    [Fact]
    public async Task CancellationDoesNotInterruptDrainAndLatePublicationIsRejected()
    {
        using var shutdown = new CancellationTokenSource();
        var output = new TradeOutputProbe();
        var pump = TradeTestData.Pump(2, output, shutdown);
        Assert.True(pump.TryPublish(TradeTestData.Create()));
        await shutdown.CancelAsync();
        pump.Complete();
        await pump.RunAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(output.Records);
        Assert.False(pump.Snapshot.IsIncomplete);
        Assert.False(pump.TryPublish(TradeTestData.Create()));
        Assert.Equal(1, pump.Snapshot.Total.RejectedClosed);
        Assert.True(pump.Snapshot.IsIncomplete);
        await Assert.ThrowsAsync<InvalidOperationException>(pump.RunAsync);
        TradeTestData.AssertBalanced(pump.Snapshot);
    }

    [Fact]
    public async Task InvalidDateAndNameErrorsRemainVisibleWithoutLosingTheTrade()
    {
        using var shutdown = new CancellationTokenSource();
        var output = new TradeOutputProbe();
        var pump = new TradeEventPump(2, ["VALE3:B"], TradeTestData.Records(true), output, shutdown);
        var raw = TradeTestData.Create();
        var native = raw.Trade;
        native.TradeDate.Day = 99;
        Assert.True(pump.TryPublish(raw with { Trade = native }));
        pump.Complete();
        await pump.RunAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(output.Records);
        Assert.Equal(1, pump.Snapshot.Total.InvalidDates);
        Assert.Equal(2, pump.Snapshot.Total.NameLookupFailures);
        Assert.False(pump.Snapshot.IsIncomplete);
        Assert.Null(output.Records[0].TradeDate);
        TradeTestData.AssertBalanced(pump.Snapshot);
    }
}

public sealed class TradeConsumerFailureTests
{
    [Fact]
    public async Task ConsumerFailureLeavesUndeliveredEventsExplicitlyPending()
    {
        using var shutdown = new CancellationTokenSource();
        var output = new TradeOutputProbe
        {
            BeforeWrite = _ => throw new IOException("Saída sintética indisponível")
        };
        var pump = TradeTestData.Pump(3, output, shutdown);
        Assert.True(pump.TryPublish(TradeTestData.Create()));
        Assert.True(pump.TryPublish(TradeTestData.Create()));
        await pump.RunAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var snapshot = pump.Snapshot;
        Assert.True(snapshot.IsIncomplete);
        Assert.True(snapshot.IsClosed);
        Assert.True(shutdown.IsCancellationRequested);
        Assert.Equal(1, snapshot.ConsumerFailures);
        Assert.Equal(nameof(IOException), snapshot.ConsumerFailureType);
        Assert.Equal(2, snapshot.Total.Pending);
        Assert.Equal(0, snapshot.Total.Delivered);
        Assert.Equal(1, snapshot.QueueOccupancy);
        TradeTestData.AssertBalanced(snapshot);
    }

}
