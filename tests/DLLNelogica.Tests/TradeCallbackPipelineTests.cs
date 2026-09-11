using DLLNelogica.Interop;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

public sealed class TradeCallbackPipelineTests
{
    [Fact]
    public async Task CallbackReturnsWithConsumerAndCancellationHandlerPaused()
    {
        using var shutdown = new CancellationTokenSource();
        using var output = new PausedTradeOutput(shutdown);
        var api = new FakeProfitApi { Trade = TradeTestData.Create().Trade };
        var bridge = CallbackRegistrationTests.CreateBridge(api);
        var pump = TradeTestData.Pump(1, output, shutdown);
        bridge.AttachTrades(pump);
        bridge.AttachShutdown(shutdown);
        var consumer = pump.RunAsync();
        try
        {
            bridge.HandleTradeV2(TradeTranslationTests.Asset(), (nint)1, TConnectorTradeCallbackFlags.None);
            await output.WaitForEntryAsync();
            bridge.HandleTradeV2(TradeTranslationTests.Asset(), (nint)2, TConnectorTradeCallbackFlags.None);
            var callback = Task.Run(() => bridge.HandleTradeV2(
                TradeTranslationTests.Asset(), (nint)3, TConnectorTradeCallbackFlags.None));
            await callback.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(output.WaitForCancellation());
            Assert.False(output.OutputReleased);
            Assert.False(output.CancellationReleased);
            Assert.Equal(1, pump.Snapshot.Total.RejectedFull);
            Assert.Equal(1, pump.Snapshot.PeakQueueOccupancy);
            Assert.True(pump.Snapshot.IsIncomplete);
        }
        finally
        {
            output.Release();
            pump.Complete();
            await consumer.WaitAsync(TimeSpan.FromSeconds(10));
            bridge.DetachTrades(pump);
            bridge.DetachShutdown(shutdown);
        }

        Assert.Equal(2, output.Delivered);
        Assert.Equal(0, pump.Snapshot.Total.Pending);
        TradeTestData.AssertBalanced(pump.Snapshot);
    }

}
