using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

public sealed class OrderReadVersionTests
{
    [Fact]
    public void VersionZeroReadsScalarsWhileVersionOneReproducesTheObservedError()
    {
        var calls = 0;
        var api = new FakeProfitApi
        {
            OnOrderDetails = (ref TConnectorOrderOut order) =>
            {
                calls++;
                if (order.Version > 0) { return (int)NResult.NL_VERSION_NOT_SUPPORTED; }
                Assert.Equal(0, order.OrderId.Version);
                Assert.Equal(0, order.AccountId.Version);
                Assert.Equal(0, order.AssetId.Version);
                Assert.Equal(123, order.OrderId.LocalOrderId);
                Assert.Null(order.OrderId.ClOrderId);
                Assert.Equal(100, order.AccountId.AccountId.Length);
                Assert.Equal(100, order.AccountId.SubAccountId.Length);
                Assert.Equal(0, order.AssetId.Ticker);
                Assert.Equal(0, order.AssetId.Exchange);
                Assert.Equal(0, order.TextMessage);
                Assert.Equal(0, order.AssetId.TickerLength);
                Assert.Equal(0, order.AssetId.ExchangeLength);
                Assert.Equal(0, order.TextMessageLength);
                order.OrderStatus = 2;
                order.TradedQuantity = 3_000_000_000;
                // A fase de tamanhos também preenche os escalares. Não precisamos dos textos.
                order.AssetId.TickerLength = 6;
                order.TextMessageLength = 100;
                return 0;
            }
        };
        var oldRequest = new TConnectorOrderOut { Version = 1 };
        Assert.Equal(-2147483635, api.GetOrderDetails(ref oldRequest));
        var gateway = new ProfitOrderGateway(api, _ => null);
        gateway.Start();
        var read = gateway.Read(123, CancellationToken.None);
        Assert.Equal(0, read.Result);
        Assert.Equal(2, read.Status);
        Assert.Equal(3_000_000_000, read.Filled);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void VersionZeroRejectionStopsNativeReadsForEveryOrderInTheSession()
    {
        var calls = 0;
        var api = new FakeProfitApi
        {
            OnOrderDetails = (ref TConnectorOrderOut _) => { calls++; return (int)NResult.NL_VERSION_NOT_SUPPORTED; }
        };
        var gateway = new ProfitOrderGateway(api, _ => null);
        gateway.Start();
        for (var id = 1; id <= 4; id++)
        {
            Assert.Equal((int)NResult.NL_VERSION_NOT_SUPPORTED, gateway.Read(id, CancellationToken.None).Result);
        }
        Assert.Equal(1, calls);
    }
}
