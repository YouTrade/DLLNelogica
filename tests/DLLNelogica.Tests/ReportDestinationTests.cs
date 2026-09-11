using DLLNelogica.Configuration;
using DLLNelogica.Logging;

namespace DLLNelogica.Tests;

public sealed class ReportDestinationTests
{
    [Theory]
    [InlineData("A/B", "A\\B", "B", "B")]
    [InlineData("vale3", "VALE3", "b", "B")]
    [InlineData("VALE3", "VALE3", "B", "B_TimesAndTrades")]
    public void CollisionsIncludeSanitizationCaseAndCrossStreamNames(string ticker1, string ticker2, string exchange1, string exchange2)
    {
        var options = new ApplicationOptions
        {
            TimesAndTrades = new TimesAndTradesOptions { Enabled = true },
            MarketData = new MarketDataOptions
            {
                Instruments = [new() { Ticker = ticker1, Exchange = exchange1 }, new() { Ticker = ticker2, Exchange = exchange2 }]
            }
        };
        var issues = new List<string>();
        ReportDestinationValidator.Validate(options, issues);
        Assert.NotEmpty(issues);
        Assert.Contains(issues, issue => issue.Contains("Colisão", StringComparison.Ordinal));
    }

    [Fact]
    public void WindowsNamesAndExistingQuoteNamesRemainCompatible()
    {
        Assert.Equal("VALE3_B.txt", ReportFileNames.Quote("VALE3", "B"));
        Assert.Equal("VALE3_B_TimesAndTrades.txt", ReportFileNames.Trades("VALE3", "B"));
        Assert.Equal("A________B", ReportFileNames.Sanitize("A<>:\"/\\|?B"));
    }
}
