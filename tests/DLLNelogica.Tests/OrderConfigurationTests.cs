using System.Text.Json;
using DLLNelogica.Configuration;

namespace DLLNelogica.Tests;

public sealed class OrderConfigurationTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("{\"RoutingPassword\":123}")]
    [InlineData("{\"RoutingPassword\":null}")]
    [InlineData("{\"WaitSeconds\":null}")]
    [InlineData("{\"Enabled\":\"secret\"}")]
    [InlineData("{\"WaitSeconds\":[1.5]}")]
    public void MalformedSectionProducesSafeConfigurationError(string section)
    {
        using var json = JsonDocument.Parse("{\"OrderSequence\":" + section + "}");
        var exception = Assert.Throws<ConfigurationException>(() => OrderSequenceValidation.ValidateShape(json.RootElement));
        Assert.Contains("OrderSequence", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnabledConfigurationReportsMissingCredentialsInstrumentAndInvalidWaits()
    {
        var application = new ApplicationOptions
        {
            OrderSequence = new OrderSequenceOptions { Enabled = true, WaitSeconds = [-1] }
        };
        var issues = new List<string>();
        OrderSequenceValidation.Validate(application, issues);
        Assert.Equal(5, issues.Count);
        Assert.Contains(issues, issue => issue.Contains("OrderSequence.BrokerId", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("WaitSeconds", StringComparison.Ordinal));
    }
}
