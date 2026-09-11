using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

public sealed class TradeCallbackFailureTests
{
    [Fact]
    public async Task TranslationAndCallbackFailuresAreCountedByInstrument()
    {
        using var shutdown = new CancellationTokenSource();
        var output = new TradeOutputProbe();
        var api = new FakeProfitApi { TranslationResult = (int)NResult.NL_INTERNAL_ERROR };
        var bridge = CallbackRegistrationTests.CreateBridge(api);
        var pump = TradeTestData.Pump(2, output, shutdown);
        bridge.AttachTrades(pump);
        bridge.HandleTradeV2(TradeTranslationTests.Asset(), (nint)1, TConnectorTradeCallbackFlags.None);
        api.TranslationException = new InvalidOperationException("Tradução sintética");
        bridge.HandleTradeV2(TradeTranslationTests.Asset(), (nint)2, TConnectorTradeCallbackFlags.None);
        api.TranslationResult = 0;
        api.TranslationException = null;
        bridge.DetachTrades(pump);
        var faulting = new FaultingTradeSink(pump);
        bridge.AttachTrades(faulting);
        bridge.HandleTradeV2(TradeTranslationTests.Asset(), (nint)3, TConnectorTradeCallbackFlags.None);
        pump.Complete();
        await pump.RunAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Empty(output.Records);
        Assert.Equal(2, pump.Snapshot.Total.TranslationFailures);
        Assert.Equal(1, pump.Snapshot.Total.CallbackFailures);
        Assert.Equal(2, pump.Snapshot.Instruments["VALE3:B"].TranslationFailures);
        Assert.True(shutdown.IsCancellationRequested);
        TradeTestData.AssertBalanced(pump.Snapshot);
        bridge.DetachTrades(faulting);
    }
}
