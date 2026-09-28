using DLLNelogica.Configuration;
using DLLNelogica.Interop;
using DLLNelogica.Orders;

namespace DLLNelogica.Tests;

public sealed class OrderSafetyTests
{
    [Theory]
    [InlineData(false, 15, "0012703")]
    [InlineData(true, 15, "ausente")]
    [InlineData(true, 18, "0012703")]
    public async Task DisabledAndMissingAccountsSendNothing(bool enabled, int broker, string account)
    {
        var scenario = new AccountApiScenario();
        var calls = 0;
        scenario.Api.OnSendOrder = _ => { calls++; return 1; };
        var gateway = new ProfitOrderGateway(scenario.Api, _ => 1000);
        gateway.Start();
        var options = new OrderSequenceOptions { Enabled = enabled, BrokerId = broker, AccountId = account };
        await new OrderSequence(gateway, _ => { }).RunAsync(options, CancellationToken.None);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(18)]
    [InlineData(15005)]
    public async Task AnyConfiguredBrokerCanSendWhenAccountExists(int broker)
    {
        var scenario = new AccountApiScenario
        {
            Accounts = [new(broker, "configured-account", "Corretora", "Titular")]
        };
        var sends = 0;
        scenario.Api.OnSendOrder = order =>
        {
            Assert.Equal(broker, order.AccountId.BrokerId);
            Assert.Equal("configured-account", order.AccountId.AccountId);
            return ++sends;
        };
        scenario.Api.OnOrderDetails = (ref TConnectorOrderOut order) => { order.OrderStatus = 2; return 0; };
        var gateway = new ProfitOrderGateway(scenario.Api, _ => 1000);
        gateway.Start();
        await new OrderSequence(gateway, _ => { }, (_, _) => Task.CompletedTask)
            .RunAsync(Options(true, broker, "configured-account"), CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(4, sends);
    }

    [Fact]
    public async Task ShutdownDuringSequencePreventsFurtherSendsAndCancels()
    {
        using var stopping = new CancellationTokenSource();
        var scenario = new AccountApiScenario();
        var sends = 0;
        scenario.Api.OnSendOrder = _ => { sends++; stopping.Cancel(); return 1; };
        scenario.Api.OnCancelOrder = _ => throw new InvalidOperationException("Não deveria cancelar.");
        var gateway = new ProfitOrderGateway(scenario.Api, _ => 1000);
        gateway.Start();
        await new OrderSequence(gateway, _ => { }, (_, _) => Task.CompletedTask).RunAsync(Options(true), stopping.Token);
        Assert.Equal(1, sends);
    }

    private static OrderSequenceOptions Options(bool markets, int broker = 15, string account = "0012703") => new()
    {
        Enabled = true, BrokerId = broker, AccountId = account, RoutingPassword = "secret",
        Ticker = "WINV26", Exchange = "F", Quantity = 1, TickSize = 5,
        BuyLimitOffset = 10, SellLimitOffset = 10, BuyMarket = markets, SellMarket = markets,
        WaitSeconds = [0, 0, 0, 0, 0]
    };
}
