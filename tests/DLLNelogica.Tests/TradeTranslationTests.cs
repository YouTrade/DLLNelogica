using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

public sealed class TradeTranslationTests
{
    [Fact]
    public void TranslationCopiesDataSynchronouslyAndPreservesRawValues()
    {
        var api = new FakeProfitApi
        {
            Trade = new TConnectorTrade
            {
                TradeDate = new SystemTime { Year = 2026, Month = 99, Milliseconds = 999 },
                TradeNumber = uint.MaxValue,
                Price = 60.25,
                Quantity = (long)int.MaxValue + 1,
                Volume = 100.5,
                BuyAgent = -1,
                SellAgent = 123,
                TradeType = (TradeType)255
            }
        };
        var translator = new ProfitTradeTranslator(api);
        var before = DateTimeOffset.Now;
        var result = translator.Translate(Asset(), (nint)123, (TConnectorTradeCallbackFlags)0x80000001);
        var after = DateTimeOffset.Now;
        Assert.True(result.IsSuccessful);
        var trade = result.Trade!.Value;
        Assert.Equal((byte)0, api.RequestedVersion);
        Assert.Equal(Environment.CurrentManagedThreadId, api.TranslationThread);
        Assert.Equal((nint)123, api.LastTradePointer);
        Assert.InRange(trade.ReceivedAt, before, after);
        Assert.Equal(api.Trade, trade.Trade);
        Assert.Equal("VALE3:B", trade.Instrument.Key);
        Assert.Equal(7, trade.Instrument.Feed);
        Assert.Equal((TConnectorTradeCallbackFlags)0x80000001, trade.Flags);
        Assert.Equal(1, trade.ArrivalSequence);
        Assert.NotEqual(Guid.Empty, trade.SessionId);

        api.Trade = default;
        var second = translator.Translate(Asset(), (nint)456, TConnectorTradeCallbackFlags.None).Trade!.Value;
        Assert.Equal((long)int.MaxValue + 1, trade.Trade.Quantity);
        Assert.Equal(2, second.ArrivalSequence);
        Assert.Equal(trade.SessionId, second.SessionId);
    }

    [Fact]
    public void NativeFailureAndExceptionNeverProduceAnEvent()
    {
        var api = new FakeProfitApi { TranslationResult = (int)NResult.NL_VERSION_NOT_SUPPORTED };
        var translator = new ProfitTradeTranslator(api);
        var failed = translator.Translate(Asset(), (nint)123, TConnectorTradeCallbackFlags.None);
        Assert.False(failed.IsSuccessful);
        Assert.Null(failed.Trade);
        Assert.Equal((int)NResult.NL_VERSION_NOT_SUPPORTED, failed.Call.NativeResult);

        api.TranslationException = new EntryPointNotFoundException();
        failed = translator.Translate(Asset(), (nint)123, TConnectorTradeCallbackFlags.None);
        Assert.False(failed.IsSuccessful);
        Assert.Null(failed.Trade);
        Assert.Equal(nameof(EntryPointNotFoundException), failed.Call.ExceptionType);
    }

    [Fact]
    public void NullPointerIsRejectedWithoutCallingNativeCode()
    {
        var api = new FakeProfitApi();
        var result = new ProfitTradeTranslator(api).Translate(Asset(), nint.Zero, TConnectorTradeCallbackFlags.None);
        Assert.False(result.IsSuccessful);
        Assert.Equal((int)NResult.NL_INVALID_ARGS, result.Call.NativeResult);
        Assert.Equal(0, api.TranslationCalls);
    }

    internal static TConnectorAssetIdentifier Asset() => new()
    {
        Ticker = "VALE3",
        Exchange = "B",
        FeedType = 7
    };
}
