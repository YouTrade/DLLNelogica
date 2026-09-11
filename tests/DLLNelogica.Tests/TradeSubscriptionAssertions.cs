using DLLNelogica.Configuration;
using DLLNelogica.Interop;
using DLLNelogica.MarketData;

namespace DLLNelogica.Tests;

internal static class TradeSubscriptionAssertions
{
    internal static void FailSecondAndCheckRollback(FakeProfitApi api, ProfitCallbackBridge bridge, ProfitSession session)
    {
            api.OnSubscribe = (ticker, _) =>
            {
                if (ticker != "VALE3")
                {
                    return (int)NResult.NL_INVALID_TICKER;
                }

                bridge.HandleTradeV2(TradeTranslationTests.Asset(), (nint)1, TConnectorTradeCallbackFlags.None);
                return 0;
            };
            var subscriptions = new MarketDataSubscriptionManager(session);
            var batch = subscriptions.SubscribeAll([
                new InstrumentOptions { Ticker = "VALE3", Exchange = "B" },
                new InstrumentOptions { Ticker = "WINV26", Exchange = "F" }]);
            Assert.False(batch.IsSuccessful);
            Assert.Equal("VALE3:B", Assert.Single(api.Unsubscribed));
            Assert.Empty(subscriptions.UnsubscribeAll());
            Assert.Equal(2, api.SubscriptionCalls);
    }
}
