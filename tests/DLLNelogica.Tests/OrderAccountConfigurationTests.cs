using System.Text.Json;
using DLLNelogica.Configuration;

namespace DLLNelogica.Tests;

public sealed class OrderAccountConfigurationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidBrokerExplainsWhereToFindTheId(int broker)
    {
        var issues = Validate($"\"BrokerId\":{broker}");
        var issue = Assert.Single(issues);
        Assert.Contains("OrderSequence.BrokerId deve ser maior que zero", issue, StringComparison.Ordinal);
        Assert.Contains("relatório de contas", issue, StringComparison.Ordinal);
        Assert.Contains("OrderSequence.Enabled=false", issue, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingBrokerDoesNotSelectAnAccountImplicitly()
    {
        Assert.Equal(0, new OrderSequenceOptions().BrokerId);
        Assert.Single(Validate("\"LegacyField\":false"));
        var issues = new List<string>();
        OrderSequenceValidation.Validate(new ApplicationOptions(), issues);
        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void RemovedConfirmationIsIgnoredLikeOtherUnknownFields(string legacyValue)
    {
        Assert.Empty(Validate("\"BrokerId\":15005,\"ConfirmRealAccount\":" + legacyValue));
    }

    [Theory]
    [InlineData("AccountId")]
    [InlineData("RoutingPassword")]
    public void AccountAndPasswordRemainRequired(string field)
    {
        var issues = Validate("\"BrokerId\":18", field);
        var issue = Assert.Single(issues);
        Assert.Contains(field, issue, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret", issue, StringComparison.Ordinal);
    }

    private static List<string> Validate(string members, string? emptyField = null)
    {
        var json = "{\"OrderSequence\":{\"Enabled\":true,\"AccountId\":\"test-account\"," +
            "\"RoutingPassword\":\"test-secret\",\"Ticker\":\"WINV26\",\"Exchange\":\"F\"," +
            "\"TickSize\":5,\"BuyLimitOffset\":10,\"SellLimitOffset\":10," + members + "}}";
        if (emptyField is not null)
        {
            json = json.Replace(emptyField == "AccountId" ? "test-account" : "test-secret", string.Empty, StringComparison.Ordinal);
        }
        using var document = JsonDocument.Parse(json);
        OrderSequenceValidation.ValidateShape(document.RootElement);
        var options = document.RootElement.GetProperty("OrderSequence").Deserialize<OrderSequenceOptions>()!;
        var application = new ApplicationOptions
        {
            OrderSequence = options,
            MarketData = new MarketDataOptions { Instruments = [new() { Ticker = "WINV26", Exchange = "F" }] }
        };
        var issues = new List<string>();
        OrderSequenceValidation.Validate(application, issues);
        return issues;
    }
}
