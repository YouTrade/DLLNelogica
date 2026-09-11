using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

public sealed class TradeFlushFailureTests
{
    [Fact]
    public async Task FailedFlushIsNotConfirmedAndDisposeDoesNotRetryTheBatch()
    {
        using var fixture = new TradeFileFixture();
        using var shutdown = new CancellationTokenSource();
        var fileSystem = new FaultingTradeFileSystem { FailFlushAfter = 1 };
        var output = fixture.Output(fileSystem);
        var pump = new TradeEventPump(4, fixture.Destinations.Keys, TradeTestData.Records(), output, shutdown,
            new FixedTradeClock(fixture.Now));
        Assert.True(pump.TryPublish(fixture.Record().Raw));
        pump.Complete();
        await pump.RunAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, pump.Snapshot.Total.Delivered);
        Assert.Equal(0, pump.Snapshot.Total.Confirmed);
        Assert.Equal(1, pump.Snapshot.Total.Unconfirmed);
        Assert.True(pump.Snapshot.IsIncomplete);
        Assert.True(shutdown.IsCancellationRequested);
        Assert.All(fileSystem.Streams, stream => Assert.True(stream.WasDisposed));
        Assert.Equal(2, fileSystem.Streams[0].Writes); // Cabeçalho + lote, sem nova tentativa ao descartar.
    }


}

public sealed class TradePartialWriteTests
{
    [Fact]
    public async Task PartialWriteCannotProduceAConfirmationOrAnAutomaticRetry()
    {
        using var fixture = new TradeFileFixture();
        var fileSystem = new FaultingTradeFileSystem();
        var output = fixture.Output(fileSystem);
        output.Maintain(fixture.Now, false);
        await output.WriteAsync(fixture.Record());
        fileSystem.PartialWrite = true;
        Assert.Throws<IOException>(() => output.Maintain(fixture.Now, true));
        var writes = fileSystem.Streams.Sum(stream => stream.Writes);
        output.Dispose();
        Assert.Equal(writes, fileSystem.Streams.Sum(stream => stream.Writes));
        Assert.All(fileSystem.Streams, stream => Assert.True(stream.WasDisposed));
    }


}

public sealed class TradePreparationFailureTests
{
    [Fact]
    public async Task PreparationFailureIsVisibleBeforeTheSessionCanStart()
    {
        using var fixture = new TradeFileFixture();
        using var shutdown = new CancellationTokenSource();
        var pump = new TradeEventPump(2, fixture.Destinations.Keys, TradeTestData.Records(),
            fixture.Output(new FaultingTradeFileSystem { DenyDirectory = true }), shutdown);
        var worker = pump.RunAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pump.Ready);
        await worker.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(pump.Snapshot.IsIncomplete);
        Assert.Equal(0, pump.Snapshot.Total.Confirmed);
        Assert.True(shutdown.IsCancellationRequested);
    }
}
