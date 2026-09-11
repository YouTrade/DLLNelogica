using DLLNelogica.Configuration;

namespace DLLNelogica.Tests;

public sealed class ConfigurationTests
{
    [Theory]
    [InlineData("")]
    [InlineData(",\"TimesAndTrades\":{}")]
    public void AbsentOrEmptySectionUsesCompatibleDefaults(string section)
    {
        var options = Load(section);
        Assert.False(options.TimesAndTrades.Enabled);
        Assert.Equal(16_384, options.TimesAndTrades.ChannelCapacity);
        Assert.True(options.TimesAndTrades.ResolveAgentNames);
        Assert.Single(options.MarketData.Instruments);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1_000_000)]
    public void ExplicitOptionsAndCapacityBoundariesAreAccepted(int capacity)
    {
        var options = Load(
            $",\"TimesAndTrades\":{{\"Enabled\":true,\"ChannelCapacity\":{capacity},\"ResolveAgentNames\":false}}");
        Assert.True(options.TimesAndTrades.Enabled);
        Assert.Equal(capacity, options.TimesAndTrades.ChannelCapacity);
        Assert.False(options.TimesAndTrades.ResolveAgentNames);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("true")]
    [InlineData("{\"Enabled\":\"true\"}")]
    [InlineData("{\"Enabled\":null}")]
    [InlineData("{\"ResolveAgentNames\":1}")]
    [InlineData("{\"ResolveAgentNames\":null}")]
    [InlineData("{\"ChannelCapacity\":0}")]
    [InlineData("{\"ChannelCapacity\":-1}")]
    [InlineData("{\"ChannelCapacity\":1000001}")]
    [InlineData("{\"ChannelCapacity\":2147483648}")]
    [InlineData("{\"ChannelCapacity\":1.5}")]
    [InlineData("{\"ChannelCapacity\":\"4096\"}")]
    [InlineData("{\"ChannelCapacity\":null}")]
    public void InvalidOptionsProduceConfigurationErrors(string section)
    {
        var error = Assert.Throws<ConfigurationException>(() => Load(",\"TimesAndTrades\":" + section));
        Assert.Contains("TimesAndTrades", error.Message, StringComparison.Ordinal);
    }

    private static ApplicationOptions Load(string section)
    {
        var directory = Directory.CreateTempSubdirectory("DLLNelogica-tests-");
        try
        {
            // Dados fictícios: nunca ler o appsettings pessoal para executar testes.
            File.WriteAllText(Path.Combine(directory.FullName, "appsettings.json"),
                "{\"Credenciais\":{\"Key\":\"test\",\"User\":\"test\",\"Password\":\"test\"}," +
                "\"MarketData\":{\"ChannelCapacity\":4096,\"ReportIntervalSeconds\":1," +
                "\"Instruments\":[{\"Ticker\":\"VALE3\",\"Exchange\":\"B\"}]}" + section + "}");
            return new JsonConfigurationLoader(directory.FullName).Load();
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
