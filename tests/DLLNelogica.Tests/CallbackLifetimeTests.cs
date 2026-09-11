using DLLNelogica.Configuration;
using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

// Um único teste possui o ciclo de vida nativo global. Não criar reset de estado produtivo
// para testes: a restrição de uma inicialização por processo faz parte do contrato.
public sealed class CallbackLifetimeTests
{
    [Fact]
    public async Task RootSurvivesGcFinalizationAndContainsAllCallbackFailures()
    {
        var api = new FakeProfitApi { Trade = new TConnectorTrade { Quantity = 3_000_000_000 } };
        var bridge = CallbackRegistrationTests.CreateBridge(api);
        var sink = new RecordingTradeSink();
        bridge.AttachTrades(sink);
        var session = new ProfitSession(api, bridge);
        Assert.True(session.Initialize(new CredentialsOptions { Key = "test", User = "test", Password = "test" }).IsAccepted);
        Assert.True(session.RegisterMarketDataCallbacks(true).IsSuccessful);
        Assert.Equal(1, api.TradeRegistrationCalls);
        var callback = CallbackBoundaryAssertions.CollectAndRecoverTradeCallback();
        callback(TradeTranslationTests.Asset(), (nint)1, TConnectorTradeCallbackFlags.IsEdit);
        Assert.Single(sink.Trades);
        Assert.Equal(3_000_000_000, sink.Trades[0].Trade.Quantity);

        bridge.DetachTrades(sink);
        await using var files = new CallbackFileScenario(bridge, session);
        await files.PrepareAndSubscribeAsync();
        api.OnFinalize = () =>
        {
            CallbackBoundaryAssertions.NamesAreStopped(session);
            callback(TradeTranslationTests.Asset(), (nint)2, TConnectorTradeCallbackFlags.None);
        };
        Assert.True(files.Shutdown());
        await files.CheckFinalEventAsync();
        CallbackBoundaryAssertions.UnsubscribedOnce(api);
        bridge.AttachTrades(sink);
        Assert.False(session.FinalizeOnce().WasExecuted);
        Assert.Equal(1, api.FinalizationCalls);
        Assert.Single(sink.Trades);

        sink.ThrowOnPublish = true;
        CallbackBoundaryAssertions.SignalsFailure(bridge, callback);
        sink.ThrowOnPublish = false;
        sink.Accept = false;
        CallbackBoundaryAssertions.SignalsFailure(bridge, callback);
        sink.Accept = true;
        api.TranslationResult = (int)NResult.NL_INTERNAL_ERROR;
        CallbackBoundaryAssertions.SignalsFailure(bridge, callback);
        api.TranslationException = new InvalidOperationException("Tradução sintética.");
        CallbackBoundaryAssertions.SignalsFailure(bridge, callback);
        bridge.DetachTrades(sink);
        CallbackBoundaryAssertions.SignalsFailure(bridge, callback);
        Assert.Single(sink.Trades);
        Assert.True(ProfitProcessLifetime.HasCallbackFailure);
        Assert.False(session.Initialize(new CredentialsOptions()).IsAccepted);
        Assert.Equal(1, api.InitializationCalls);
    }

}
