using DLLNelogica.Application;
using DLLNelogica.Configuration;
using DLLNelogica.Connection;
using DLLNelogica.Interop;
using DLLNelogica.MarketData;

namespace DLLNelogica.Tests;

public sealed class CallbackRegistrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegistrationFailurePreventsSubsequentNativeSubscription(bool missingExport)
    {
        var api = new FakeProfitApi
        {
            TradeRegistrationResult = (int)NResult.NL_NO_LICENSE,
            RegistrationException = missingExport ? new EntryPointNotFoundException() : null
        };
        var bridge = CreateBridge(api);
        bridge.AttachTrades(new RecordingTradeSink());
        var session = new ProfitSession(api, bridge);
        var result = session.RegisterMarketDataCallbacks(true);
        Assert.False(result.IsSuccessful);
        Assert.NotNull(result.TradeV2);
        Assert.False(session.Subscribe("VALE3", "B").IsSuccessful);
        Assert.Equal(0, api.SubscriptionCalls);
        if (missingExport)
        {
            Assert.Equal(nameof(EntryPointNotFoundException), result.TradeV2.Value.ExceptionType);
        }
        else
        {
            Assert.Equal((int)NResult.NL_NO_LICENSE, result.TradeV2.Value.NativeResult);
        }
    }

    [Fact]
    public void DisabledTradesPreserveRegistrationAndSubscription()
    {
        var api = new FakeProfitApi();
        var session = new ProfitSession(api, CreateBridge(api));
        Assert.False(session.Subscribe("VALE3", "B").IsSuccessful);
        var result = session.RegisterMarketDataCallbacks();
        Assert.True(result.IsSuccessful);
        Assert.Null(result.TradeV2);
        Assert.Equal(0, api.TradeRegistrationCalls);
        Assert.True(session.Subscribe("VALE3", "B").IsSuccessful);
        Assert.Equal(1, api.SubscriptionCalls);
    }

    [Fact]
    public async Task EnabledTradesWithoutConsumerFailBeforeInitialization()
    {
        var api = new FakeProfitApi();
        var session = new ProfitSession(api, CreateBridge(api));
        var coordinator = new MarketDataSessionCoordinator(
            session, new ConnectionStateMachine(), new MarketDataSubscriptionManager(session));
        using var shutdown = new ConsoleShutdown();
        var exitCode = await coordinator.RunAsync(new ApplicationOptions
        {
            TimesAndTrades = new TimesAndTradesOptions { Enabled = true }
        }, shutdown);
        Assert.Equal(1, exitCode);
        Assert.Equal(0, api.InitializationCalls);
        Assert.Equal(0, api.SubscriptionCalls);
        Assert.Equal(0, api.TradeRegistrationCalls);
    }

    [Fact]
    public void MissingConsumerAlsoPreventsDirectTradeRegistration()
    {
        var api = new FakeProfitApi();
        var session = new ProfitSession(api, CreateBridge(api));
        Assert.False(session.RegisterMarketDataCallbacks(true).IsSuccessful);
        Assert.Equal(0, api.TradeRegistrationCalls);
    }

    internal static ProfitCallbackBridge CreateBridge(IProfitApi api) =>
        new(new ConnectionStateEventPump(new ConnectionStateMachine()), api);
}
