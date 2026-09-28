using DLLNelogica.Configuration;
using DLLNelogica.Interop;
using DLLNelogica.Orders;

namespace DLLNelogica.Tests;

public sealed class OrderCancellationReportTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(-1, true)]
    public async Task FinalAfterCancellationAttemptDoesNotReportCancellationAsDispensed(int result, bool throws)
    {
        var scenario = new AccountApiScenario();
        var requested = new List<long>();
        var sends = 0;
        scenario.Api.OnSendOrder = _ => ++sends;
        scenario.Api.OnCancelOrder = order =>
        {
            Assert.DoesNotContain(order.OrderId.LocalOrderId, requested);
            requested.Add(order.OrderId.LocalOrderId);
            if (throws) { throw new InvalidOperationException("Falha simulada."); }
            return result;
        };
        scenario.Api.OnOrderDetails = (ref TConnectorOrderOut order) =>
        {
            order.OrderStatus = requested.Contains(order.OrderId.LocalOrderId) ? (byte)4 : (byte)0;
            return 0;
        };
        var gateway = new ProfitOrderGateway(scenario.Api, _ => 1000);
        gateway.Start();
        var lines = new List<string>();
        await new OrderSequence(gateway, lines.Add, (_, _) => Task.CompletedTask)
            .RunAsync(Options(), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, requested.Count);
        Assert.Equal(2, lines.Count(line => line.Contains("| estado=4 (cancelada)", StringComparison.Ordinal)));
        Assert.DoesNotContain(lines, line => line.Contains("cancelamento dispensado", StringComparison.Ordinal));
    }

    private static OrderSequenceOptions Options() => new()
    {
        Enabled = true, BrokerId = 15, AccountId = "0012703", RoutingPassword = "test", Ticker = "WINV26", Exchange = "F",
        Quantity = 3, TickSize = 5, BuyLimitOffset = 10, SellLimitOffset = 10, BuyMarket = false, SellMarket = false,
        WaitSeconds = [0, 0, 0, 0, 0]
    };
}
