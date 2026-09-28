using DLLNelogica.Configuration;

namespace DLLNelogica.Tests;

public sealed class OrderLimitConfigurationTests
{
    [Fact]
    public void OldConfigurationKeepsBothLimitsEnabled()
    {
        var options = Load("\"BuyLimitOffset\":10,\"SellLimitOffset\":10");
        Assert.True(options.BuyLimit);
        Assert.True(options.SellLimit);
    }

    [Theory]
    [InlineData("\"BuyLimit\":false,\"SellLimitOffset\":10", false, true)]
    [InlineData("\"SellLimit\":false,\"BuyLimitOffset\":10", true, false)]
    [InlineData("\"BuyLimit\":false,\"SellLimit\":false", false, false)]
    [InlineData("\"BuyLimit\":false,\"BuyLimitOffset\":0,\"SellLimitOffset\":10", false, true)]
    [InlineData("\"SellLimit\":false,\"SellLimitOffset\":0,\"BuyLimitOffset\":10", true, false)]
    [InlineData("\"BuyLimit\":false,\"SellLimit\":false,\"BuyLimitOffset\":-10,\"SellLimitOffset\":-10", false, false)]
    public void DisabledOffsetsAreNotUsed(string members, bool buy, bool sell)
    {
        var options = Load(members);
        Assert.Equal(buy, options.BuyLimit);
        Assert.Equal(sell, options.SellLimit);
    }

    [Theory]
    [InlineData("\"BuyLimitOffset\":0,\"SellLimitOffset\":10")]
    [InlineData("\"SellLimitOffset\":0,\"BuyLimitOffset\":10")]
    [InlineData("\"BuyLimitOffset\":-10,\"SellLimitOffset\":10")]
    [InlineData("\"SellLimitOffset\":-10,\"BuyLimitOffset\":10")]
    [InlineData("\"BuyLimit\":false,\"SellLimit\":true,\"SellLimitOffset\":0")]
    [InlineData("\"SellLimit\":false,\"BuyLimit\":true,\"BuyLimitOffset\":0")]
    [InlineData("\"BuyLimit\":\"false\"")]
    [InlineData("\"SellLimit\":null")]
    [InlineData("\"BuyLimit\":false,\"BuyLimitOffset\":\"zero\"")]
    [InlineData("\"SellLimit\":false,\"SellLimitOffset\":null")]
    public void EnabledLimitsRequirePositiveOffsetsAndAllFieldsKeepTheirTypes(string members)
    {
        var exception = Assert.Throws<ConfigurationException>(() => Load(members));
        Assert.Contains("OrderSequence", exception.Message, StringComparison.Ordinal);
    }

    private static OrderSequenceOptions Load(string members)
    {
        var directory = Directory.CreateTempSubdirectory("DLLNelogica-order-switches-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "appsettings.json"),
                "{\"Credenciais\":{\"Key\":\"test\",\"User\":\"test\",\"Password\":\"test\"}," +
                "\"MarketData\":{\"ChannelCapacity\":4096,\"ReportIntervalSeconds\":1," +
                "\"Instruments\":[{\"Ticker\":\"WINV26\",\"Exchange\":\"F\"}]}," +
                "\"OrderSequence\":{\"Enabled\":true,\"BrokerId\":18,\"AccountId\":\"test\",\"RoutingPassword\":\"test\"," +
                "\"Ticker\":\"WINV26\",\"Exchange\":\"F\",\"TickSize\":5," + members + "}}");
            return new JsonConfigurationLoader(directory.FullName).Load().OrderSequence;
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
