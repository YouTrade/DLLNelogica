using DLLNelogica.Configuration;
using DLLNelogica.Interop;
using DLLNelogica.Orders;

namespace DLLNelogica.Tests;

public sealed class OrderTerminalSequenceTests
{
    [Theory]
    [InlineData(204, 0, false)]
    [InlineData(205, 0, false)]
    [InlineData(205, 3, true)]
    [InlineData(204, 3, true)]
    public async Task TerminalStatesSkipCancellationAndRetainReportedExecutions(byte status, long filled, bool warn)
    {
        var scenario = new AccountApiScenario();
        var sends = 0;
        var cancels = 0;
        var queries = 0;
        var delays = 0;
        scenario.Api.OnSendOrder = _ => ++sends;
        scenario.Api.OnCancelOrder = _ => { cancels++; return 0; };
        scenario.Api.OnOrderDetails = (ref TConnectorOrderOut order) =>
        {
            queries++;
            order.OrderStatus = status;
            order.TradedQuantity = order.OrderId.LocalOrderId == 1 ? filled : 0;
            return 0;
        };
        var gateway = new ProfitOrderGateway(scenario.Api, _ => 1000);
        gateway.Start();
        var lines = new List<string>();
        var sequence = new OrderSequence(gateway, lines.Add, (_, _) => { delays++; return Task.CompletedTask; });
        await sequence.RunAsync(Options(), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, sends);
        Assert.Equal(0, cancels);
        Assert.Equal(2, queries);
        Assert.Equal(0, delays);
        Assert.Equal(warn, lines.Any(line => line.StartsWith("AVISO:", StringComparison.Ordinal)));
        Assert.Equal(2, lines.Count(line => line.Contains("| estado=final |", StringComparison.Ordinal)));
        Assert.Contains(lines, line => line.Contains($"código={status}", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains($"saldo executado observado nesta sequência={filled}", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(202)]
    [InlineData(203)]
    [InlineData(206)]
    [InlineData(207)]
    [InlineData(255)]
    public async Task NonFinalStatesRemainObservedAndWarnOnShutdown(byte status)
    {
        using var stopping = new CancellationTokenSource();
        var scenario = new AccountApiScenario();
        var sends = 0;
        var cancels = 0;
        scenario.Api.OnSendOrder = _ => ++sends;
        scenario.Api.OnCancelOrder = _ => { cancels++; return 0; };
        scenario.Api.OnOrderDetails = (ref TConnectorOrderOut order) => { order.OrderStatus = status; return 0; };
        var gateway = new ProfitOrderGateway(scenario.Api, _ => 1000);
        gateway.Start();
        var lines = new List<string>();
        var sequence = new OrderSequence(gateway, lines.Add, (_, token) =>
        {
            stopping.Cancel();
            return Task.FromCanceled(token);
        });
        await sequence.RunAsync(Options(), stopping.Token).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, cancels);
        Assert.Contains(lines, line => line.StartsWith("AVISO:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("| estado=final |", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains($"código={status}", StringComparison.Ordinal));
    }

    private static OrderSequenceOptions Options() => new()
    {
        Enabled = true, BrokerId = 15, AccountId = "0012703", RoutingPassword = "test", Ticker = "WINV26", Exchange = "F",
        Quantity = 3, TickSize = 5, BuyLimitOffset = 10, SellLimitOffset = 10, BuyMarket = false, SellMarket = false,
        WaitSeconds = [0, 0, 0, 0, 0]
    };
}
