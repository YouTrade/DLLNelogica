using DLLNelogica.Configuration;
using DLLNelogica.Interop;
using DLLNelogica.Orders;

namespace DLLNelogica.Tests;

internal sealed class OrderObservationScenario : TimeProvider
{
    private long _timestamp;
    internal AccountApiScenario Accounts { get; } = new();
    internal List<string> Lines { get; } = [];
    internal int Sent { get; private set; }
    internal int Canceled { get; private set; }
    internal int Reads { get; private set; }
    internal Func<int, (int Result, byte Status, long Filled)> Response { get; set; } = _ => (-1, 0, 0);
    internal TimeSpan Elapsed => TimeSpan.FromTicks(_timestamp);
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _timestamp;

    internal async Task RunAsync(bool enabled = true)
    {
        Accounts.Api.OnSendOrder = _ => ++Sent;
        Accounts.Api.OnCancelOrder = _ => { Canceled++; return 0; };
        Accounts.Api.OnOrderDetails = (ref TConnectorOrderOut order) =>
        {
            var result = Response(++Reads);
            order.OrderStatus = result.Status;
            order.TradedQuantity = result.Filled;
            return result.Result;
        };
        var gateway = new ProfitOrderGateway(Accounts.Api, _ => 1000);
        gateway.Start();
        await new OrderSequence(gateway, Lines.Add, (duration, _) =>
        {
            _timestamp += duration.Ticks;
            return Task.CompletedTask;
        }, this).RunAsync(new OrderSequenceOptions
        {
            Enabled = enabled, BrokerId = 15, AccountId = "0012703", RoutingPassword = "test", Ticker = "WINV26", Exchange = "F",
            TickSize = 5, BuyLimitOffset = 10, SellLimitOffset = 10, WaitSeconds = [0, 0, 0, 0, 0]
        }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
    }
}
