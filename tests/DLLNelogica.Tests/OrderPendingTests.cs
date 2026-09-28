using DLLNelogica.Configuration;
using DLLNelogica.Interop;
using DLLNelogica.Orders;

namespace DLLNelogica.Tests;

public sealed class OrderPendingTests
{
    [Fact]
    public async Task UnknownOrdersAreCanceledByLocalIdAndFailedCancellationIsRetried()
    {
        var scenario = new AccountApiScenario();
        var sends = 0;
        var attempts = new Dictionary<long, int>();
        scenario.Api.OnSendOrder = _ => ++sends;
        scenario.Api.OnCancelOrder = order =>
        {
            var id = order.OrderId.LocalOrderId;
            attempts[id] = attempts.GetValueOrDefault(id) + 1;
            return attempts[id] == 1 ? -1 : 0;
        };
        scenario.Api.OnOrderDetails = (ref TConnectorOrderOut order) =>
        {
            if (attempts.GetValueOrDefault(order.OrderId.LocalOrderId) < 2) { return -1; }
            order.OrderStatus = 4;
            return 0;
        };
        var gateway = new ProfitOrderGateway(scenario.Api, _ => 1000);
        gateway.Start();
        await new OrderSequence(gateway, _ => { }, (_, _) => Task.CompletedTask)
            .RunAsync(Options(), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, sends);
        Assert.Equal(2, attempts[1]);
        Assert.Equal(2, attempts[2]);
    }

    [Fact]
    public async Task NativeTickMismatchBlocksEveryOrder()
    {
        var scenario = new AccountApiScenario();
        scenario.Api.NativeTick = 0.01;
        var sends = 0;
        scenario.Api.OnSendOrder = _ => ++sends;
        var gateway = new ProfitOrderGateway(scenario.Api, _ => 1000);
        gateway.Start();
        var lines = new List<string>();
        await new OrderSequence(gateway, lines.Add).RunAsync(Options(), CancellationToken.None);
        Assert.Equal(0, sends);
        Assert.Contains(lines, line => line.Contains("difere do tick", StringComparison.Ordinal));
    }

    private static OrderSequenceOptions Options() => new()
    {
        Enabled = true, BrokerId = 15, AccountId = "0012703", RoutingPassword = "secret",
        Ticker = "WINV26", Exchange = "F", Quantity = 1, TickSize = 5,
        BuyLimitOffset = 10, SellLimitOffset = 10, BuyMarket = false, SellMarket = false,
        WaitSeconds = [0, 0, 0, 0, 0]
    };
}
