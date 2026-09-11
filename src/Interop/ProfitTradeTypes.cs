using System.Runtime.InteropServices;

namespace DLLNelogica.Interop;

// Layouts do exemplo oficial C# da DLL 4.0.0.41; alinhamento nativo padrão, sem Pack=1.
[StructLayout(LayoutKind.Sequential)]
internal struct SystemTime
{
    public ushort Year;
    public ushort Month;
    public ushort DayOfWeek;
    public ushort Day;
    public ushort Hour;
    public ushort Minute;
    public ushort Second;
    public ushort Milliseconds;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TConnectorTrade
{
    public byte Version;
    public SystemTime TradeDate;
    public uint TradeNumber;
    public double Price;
    public long Quantity;
    public double Volume;
    public int BuyAgent;
    public int SellAgent;
    public TradeType TradeType;
}

internal enum TradeType : byte
{
    CrossTrade = 1,
    AggressorBuyer = 2,
    AggressorSeller = 3,
    Auction = 4,
    Surveillance = 5,
    Expit = 6,
    OptionExercise = 7,
    OverTheCounter = 8,
    DerivativeTerm = 9,
    Index = 10,
    BTC = 11,
    OnBehalf = 12,
    RLP = 13,
    BBT = 14,
    RFQ = 15,
    MPT = 16,
    TAC = 17,
    TAA = 18,
    Unknown = 32,
    Update = 33,
    Mid = 34,
    OffExchange = 35
}

[Flags]
internal enum TConnectorTradeCallbackFlags : uint
{
    None = 0,
    IsEdit = 1,
    LastPacket = 2
}

internal enum AgentNameFlags : uint
{
    FullName = 0,
    ShortName = 1
}
