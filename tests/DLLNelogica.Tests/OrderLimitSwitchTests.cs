using DLLNelogica.Configuration;
using DLLNelogica.Interop;
using DLLNelogica.Orders;

namespace DLLNelogica.Tests;

public sealed class OrderLimitSwitchTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public async Task SwitchesPreserveStepOrderWaitsAndCancelOnlySentLimits(int mask)
    {
        var scenario = new AccountApiScenario();
        var sent = new List<TConnectorSendOrder>();
        var sendTimes = new List<int>();
        var canceled = new HashSet<long>();
        var elapsed = 0;
        scenario.Api.OnSendOrder = order => { sent.Add(order); sendTimes.Add(elapsed); return sent.Count; };
        scenario.Api.OnCancelOrder = order =>
        {
            Assert.Equal(15, elapsed);
            Assert.Equal(2, sent[(int)order.OrderId.LocalOrderId - 1].OrderType);
            Assert.True(canceled.Add(order.OrderId.LocalOrderId));
            return 0;
        };
        scenario.Api.OnOrderDetails = (ref TConnectorOrderOut order) =>
        {
            order.OrderStatus = sent[(int)order.OrderId.LocalOrderId - 1].OrderType == 1 ? (byte)2 :
                canceled.Contains(order.OrderId.LocalOrderId) ? (byte)4 : (byte)0;
            return 0;
        };
        var quotes = 0;
        var gateway = new ProfitOrderGateway(scenario.Api, _ => { quotes++; return 1000; });
        gateway.Start();
        var lines = new List<string>();
        await new OrderSequence(gateway, lines.Add, (_, _) => { elapsed++; return Task.CompletedTask; })
            .RunAsync(Options(mask), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        AssertSteps(mask, sent, sendTimes, lines);
        var limits = sent.Count(order => order.OrderType == 2);
        Assert.Equal(limits, canceled.Count);
        Assert.Equal(limits, quotes);
        Assert.Equal(limits == 0 ? 15 : 16, elapsed);
        Assert.Equal(limits == 0, lines.Contains("Ordens: cancelamento dispensado; nenhuma ordem limitada foi enviada."));
    }

    private static void AssertSteps(int mask, List<TConnectorSendOrder> sent, List<int> times, List<string> lines)
    {
        var sentIndex = 0;
        int[] expectedTimes = [1, 3, 6, 10];
        for (var step = 0; step < 4; step++)
        {
            if ((mask & (1 << step)) == 0)
            {
                Assert.Contains($"Ordens: passo {step + 1} desligado.", lines);
                continue;
            }
            Assert.Equal(step < 2 ? 1 : 2, sent[sentIndex].OrderSide);
            Assert.Equal(step % 2 == 0 ? 1 : 2, sent[sentIndex].OrderType);
            Assert.Equal(expectedTimes[step], times[sentIndex]);
            sentIndex++;
        }
        Assert.Equal(sentIndex, sent.Count);
        Assert.Equal(Enumerable.Range(1, 5).Select(seconds => $"Ordens: espera solicitada={seconds}s."),
            lines.Where(line => line.StartsWith("Ordens: espera solicitada=", StringComparison.Ordinal)).Take(5));
    }

    private static OrderSequenceOptions Options(int mask) => new()
    {
        Enabled = true, BrokerId = 15, AccountId = "0012703", RoutingPassword = "test", Ticker = "WINV26", Exchange = "F",
        TickSize = 5, BuyLimitOffset = (mask & 2) != 0 ? 10 : 0, SellLimitOffset = (mask & 8) != 0 ? 10 : 0,
        BuyMarket = (mask & 1) != 0, BuyLimit = (mask & 2) != 0,
        SellMarket = (mask & 4) != 0, SellLimit = (mask & 8) != 0, WaitSeconds = [1, 2, 3, 4, 5]
    };
}
