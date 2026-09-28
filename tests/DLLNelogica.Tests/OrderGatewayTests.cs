using DLLNelogica.Configuration;
using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

public sealed class OrderGatewayTests
{
    [Fact]
    public async Task NativeExceptionCannotExposeRoutingPassword()
    {
        var scenario = new AccountApiScenario();
        scenario.Api.OnSendOrder = order => throw new InvalidOperationException(order.Password);
        scenario.Api.OnCancelOrder = order => throw new InvalidOperationException(order.Password);
        var gateway = new ProfitOrderGateway(scenario.Api, _ => 1000);
        gateway.Start();
        var options = Options();
        await gateway.PrepareAsync(options, CancellationToken.None);
        var send = Assert.Throws<InvalidOperationException>(() => gateway.Send(options, 1, false, -1, CancellationToken.None));
        var cancel = Assert.Throws<InvalidOperationException>(() => gateway.Cancel(options, 1, CancellationToken.None));
        Assert.DoesNotContain(options.RoutingPassword, send.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(options.RoutingPassword, cancel.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationDuringAccountValidationPreventsNativeSend()
    {
        using var stopping = new CancellationTokenSource();
        var scenario = new AccountApiScenario();
        var sends = 0;
        scenario.Api.OnSendOrder = _ => ++sends;
        var gateway = new ProfitOrderGateway(scenario.Api, _ => 1000);
        gateway.Start();
        var options = Options();
        await gateway.PrepareAsync(options, stopping.Token);
        scenario.Api.OnAccountDetails = (ref TConnectorTradingAccountOut account) =>
        {
            var result = scenario.Details(ref account);
            stopping.Cancel();
            return result;
        };
        Assert.Throws<OperationCanceledException>(() => gateway.Send(options, 1, false, -1, stopping.Token));
        Assert.Equal(0, sends);
    }

    private static OrderSequenceOptions Options() => new()
    {
        Enabled = true, BrokerId = 15, AccountId = "0012703", RoutingPassword = "do-not-disclose",
        Ticker = "WINV26", Exchange = "F", TickSize = 5
    };
}
