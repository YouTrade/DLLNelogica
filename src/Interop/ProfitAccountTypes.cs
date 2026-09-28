using System.Runtime.InteropServices;

namespace DLLNelogica.Interop;

// Delphi Win64, alinhamento padrão: TString0In é WideChar[100] embutido, não ponteiro.
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct TConnectorAccountIdentifierOut
{
    public byte Version;
    public int BrokerId;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100, ArraySubType = UnmanagedType.U2)]
    public char[] AccountId;
    public int AccountIdLength;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100, ArraySubType = UnmanagedType.U2)]
    public char[] SubAccountId;
    public int SubAccountIdLength;
    public long Reserved;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct TConnectorAccountIdentifier
{
    public byte Version;
    public int BrokerId;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string AccountId;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string SubAccountId;
    public long Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TConnectorTradingAccountOut
{
    public byte Version;
    public TConnectorAccountIdentifier AccountId;
    public nint BrokerName;
    public int BrokerNameLength;
    public nint OwnerName;
    public int OwnerNameLength;
    public nint SubOwnerName;
    public int SubOwnerNameLength;
    public uint AccountFlags;
    public byte AccountType;
}
