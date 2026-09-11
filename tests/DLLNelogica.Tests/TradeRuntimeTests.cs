using DLLNelogica.Configuration;
using DLLNelogica.Interop;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

public sealed class TradeDrainTimeoutTests
{
    [Fact]
    public async Task TimeoutReportsFailureWithoutDisposingActiveStorage()
    {
        using var shutdown = new CancellationTokenSource();
        var bridge = CallbackRegistrationTests.CreateBridge(new FakeProfitApi());
        var storage = new BlockedTradeStorage();
        var reports = new TradeReportProbe();
        var pump = TradeTestData.Pump(2, storage, shutdown);
        await using var runtime = new TradeRuntimePipeline(bridge, pump, new TradeReporter(reports), 1, TimeProvider.System);
        try
        {
            await runtime.Ready;
            pump.TryPublish(TradeTestData.Create());
            await storage.Entered.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(await runtime.CompleteAndDrainAsync(TimeSpan.Zero));
            Assert.False(storage.WasDisposed);
            Assert.True(runtime.Snapshot.DrainTimedOut);
            Assert.True(runtime.Snapshot.IsIncomplete);
            Assert.Equal(1, runtime.Snapshot.Total.Unconfirmed);
            Assert.Contains(reports.Lines, line => line.Message.Contains("timeout=True", StringComparison.Ordinal));
        }
        finally
        {
            storage.Release();
            await runtime.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.True(storage.WasDisposed);
    }


}

public sealed class TradeDisabledFactoryTests
{
    [Fact]
    public void DisabledFactoryDoesNotCreateWorkerSinkOrDirectory()
    {
        using var fixture = new TradeFileFixture();
        using var shutdown = new CancellationTokenSource();
        var api = new FakeProfitApi();
        var bridge = CallbackRegistrationTests.CreateBridge(api);
        var factory = new TradePipelineFactory(bridge, new ProfitSession(api, bridge), new TradeReportProbe(), fixture.Root);
        Assert.Null(factory.Start(new ApplicationOptions(), shutdown));
        Assert.False(bridge.HasTradeSink);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Relatorios")));
        Assert.Equal(0, api.TradeRegistrationCalls);
    }


}

public sealed class TradeIdleFlushTests
{
    [Fact]
    public async Task IdleConsumerFlushesAndReportsFirstEventOnlyOnce()
    {
        using var fixture = new TradeFileFixture();
        using var shutdown = new CancellationTokenSource();
        var reports = new TradeReportProbe();
        var reporter = new TradeReporter(reports);
        var pump = TradeStorageTestFactory.Create(fixture, shutdown, reporter.Observe);
        var worker = pump.RunAsync();
        try
        {
            await pump.Ready;
            reporter.Report(pump.Snapshot);
            Assert.Contains(reports.Lines, line => line.Message.Contains("sem_eventos", StringComparison.Ordinal));
            pump.TryPublish(fixture.Record().Raw);
            pump.TryPublish(fixture.Record().Raw);
            // Timer real do consumidor; relógio de parede fixo prova o flush periódico forçado.
            await WaitForConfirmationAsync(pump, 2);
            Assert.Equal(2, fixture.Events().Length);
            Assert.Single(reports.Lines, line => line.Message.StartsWith("Primeiro negócio", StringComparison.Ordinal));
        }
        finally
        {
            pump.Complete();
            await worker.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(pump.Snapshot.Total.Delivered, pump.Snapshot.Total.Confirmed);
    }

    private static async Task WaitForConfirmationAsync(TradeEventPump pump, long count)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (pump.Snapshot.Total.Confirmed != count)
        {
            await Task.Delay(20, deadline.Token);
        }
    }
}
