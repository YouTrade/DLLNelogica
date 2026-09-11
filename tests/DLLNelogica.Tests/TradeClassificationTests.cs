using DLLNelogica.Interop;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

public sealed class TradeClassificationTests
{
    [Theory]
    [InlineData(1, "CrossTrade", "nao_classificado")]
    [InlineData(2, "CompraAgressao", "comprador")]
    [InlineData(3, "VendaAgressao", "vendedor")]
    [InlineData(4, "Leilao", "nao_classificado")]
    [InlineData(5, "Surveillance", "nao_classificado")]
    [InlineData(6, "Expit", "nao_classificado")]
    [InlineData(7, "OptionsExercise", "nao_classificado")]
    [InlineData(8, "OverTheCounter", "nao_classificado")]
    [InlineData(9, "DerivativeTerm", "nao_classificado")]
    [InlineData(10, "Index", "nao_classificado")]
    [InlineData(11, "BTC", "nao_classificado")]
    [InlineData(12, "OnBehalf", "nao_classificado")]
    [InlineData(13, "RLP", "nao_classificado")]
    [InlineData(14, "BBT", "nao_classificado")]
    [InlineData(15, "RFQ", "nao_classificado")]
    [InlineData(16, "MPT", "nao_classificado")]
    [InlineData(17, "TAC", "nao_classificado")]
    [InlineData(18, "TAA", "nao_classificado")]
    [InlineData(32, "Desconhecido", "nao_classificado")]
    [InlineData(33, "Update", "nao_classificado")]
    [InlineData(34, "Mid", "nao_classificado")]
    [InlineData(35, "OffExchange", "nao_classificado")]
    [InlineData(255, "NaoMapeado", "nao_classificado")]
    public void EveryDocumentedTypeHasAnExplicitClassification(int code, string description, string aggressor)
    {
        var raw = TradeTestData.Create();
        var native = raw.Trade;
        native.TradeType = (TradeType)code;
        var record = TradeTestData.Records().Create(raw with { Trade = native });
        Assert.Equal(description, record.Type);
        Assert.Equal(aggressor, record.Aggressor);
        Assert.Equal(code, (int)record.Raw.Trade.TradeType);
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(1u, true)]
    [InlineData(2u, false)]
    [InlineData(3u, true)]
    [InlineData(2147483651u, true)]
    public void EditUsesOnlyTheFlagAndPreservesUnknownBits(uint flags, bool edit)
    {
        var raw = TradeTestData.Create() with { Flags = (TConnectorTradeCallbackFlags)flags };
        var record = TradeTestData.Records().Create(raw);
        Assert.Equal(edit, record.IsEdit);
        Assert.Equal(flags, (uint)record.Raw.Flags);
    }

    [Fact]
    public void NativeFieldsAndTimestampsArePreservedWithoutTimezoneGuess()
    {
        var raw = TradeTestData.Create();
        var record = TradeTestData.Records().Create(raw);
        Assert.Equal(raw, record.Raw);
        Assert.Equal((long)int.MaxValue + 10, record.Raw.Trade.Quantity);
        Assert.Equal(new DateTime(2026, 9, 11, 10, 30, 12, 123, DateTimeKind.Unspecified), record.TradeDate);
        Assert.Equal(DateTimeKind.Unspecified, record.TradeDate!.Value.Kind);
        Assert.Equal(1, TradeRecord.SchemaVersion);
    }

    [Fact]
    public void InvalidDateRetainsItsRawComponentsAndDoesNotInventATime()
    {
        var raw = TradeTestData.Create();
        var native = raw.Trade;
        native.TradeDate.Month = 99;
        var record = TradeTestData.Records().Create(raw with { Trade = native });
        Assert.Null(record.TradeDate);
        Assert.Equal((ushort)99, record.Raw.Trade.TradeDate.Month);
        Assert.Equal(raw.Trade.Quantity, record.Raw.Trade.Quantity);
    }
}
