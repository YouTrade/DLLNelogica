using System.Runtime.InteropServices;
using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

public sealed class OrderAbiTests
{
    [Fact]
    public void LayoutMatchesDelphiWin64()
    {
        Assert.Equal(24, Marshal.SizeOf<TConnectorOrderIdentifier>());
        Assert.Equal(120, Marshal.SizeOf<TConnectorSendOrder>());
        Assert.Equal((nint)80, Marshal.OffsetOf<TConnectorSendOrder>(nameof(TConnectorSendOrder.OrderType)));
        Assert.Equal((nint)88, Marshal.OffsetOf<TConnectorSendOrder>(nameof(TConnectorSendOrder.Price)));
        Assert.Equal((nint)104, Marshal.OffsetOf<TConnectorSendOrder>(nameof(TConnectorSendOrder.Quantity)));
        Assert.Equal(80, Marshal.SizeOf<TConnectorCancelOrder>());
        Assert.Equal(40, Marshal.SizeOf<TConnectorAssetIdentifierOut>());
        Assert.Equal(640, Marshal.SizeOf<TConnectorOrderOut>());
        Assert.Equal((nint)504, Marshal.OffsetOf<TConnectorOrderOut>(nameof(TConnectorOrderOut.TradedQuantity)));
        Assert.Equal((nint)546, Marshal.OffsetOf<TConnectorOrderOut>(nameof(TConnectorOrderOut.OrderStatus)));
        Assert.Equal((nint)632, Marshal.OffsetOf<TConnectorOrderOut>(nameof(TConnectorOrderOut.EventId)));
    }
}
