using System.Runtime.InteropServices;

namespace DLLNelogica.Interop;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct TConnectorOrderIdentifier
{
    public byte Version;
    public long LocalOrderId;
    [MarshalAs(UnmanagedType.LPWStr)] public string? ClOrderId;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct TConnectorSendOrder
{
    public byte Version;
    public TConnectorAccountIdentifier AccountId;
    public TConnectorAssetIdentifier AssetId;
    [MarshalAs(UnmanagedType.LPWStr)] public string Password;
    public byte OrderType;
    public byte OrderSide;
    public double Price;
    public double StopPrice;
    public long Quantity;
    public long MessageId;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct TConnectorCancelOrder
{
    public byte Version;
    public TConnectorAccountIdentifier AccountId;
    public TConnectorOrderIdentifier OrderId;
    [MarshalAs(UnmanagedType.LPWStr)] public string Password;
    public long MessageId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TConnectorAssetIdentifierOut
{
    public byte Version;
    public nint Ticker;
    public int TickerLength;
    public nint Exchange;
    public int ExchangeLength;
    public byte FeedType;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TConnectorOrderOut
{
    public byte Version;
    public TConnectorOrderIdentifier OrderId;
    public TConnectorAccountIdentifierOut AccountId;
    public TConnectorAssetIdentifierOut AssetId;
    public long Quantity;
    public long TradedQuantity;
    public long LeavesQuantity;
    public double Price;
    public double StopPrice;
    public double AveragePrice;
    public byte OrderSide;
    public byte OrderType;
    public byte OrderStatus;
    public byte ValidityType;
    public SystemTime Date;
    public SystemTime LastUpdate;
    public SystemTime CloseDate;
    public SystemTime ValidityDate;
    public nint TextMessage;
    public int TextMessageLength;
    public long EventId;
}
