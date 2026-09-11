using System.Reflection;
using System.Runtime.InteropServices;
using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

public sealed class TradeAbiTests
{
    [Fact]
    public void LayoutMatchesOfficialWin64Contract()
    {
        Assert.True(OperatingSystem.IsWindows());
        Assert.True(Environment.Is64BitProcess);
        Assert.Equal(16, Marshal.SizeOf<SystemTime>());
        Assert.Equal(64, Marshal.SizeOf<TConnectorTrade>());
        Assert.Equal(32, Marshal.SizeOf<TConnectorAssetIdentifier>());

        AssertOffset<TConnectorTrade>(nameof(TConnectorTrade.Version), 0);
        AssertOffset<TConnectorTrade>(nameof(TConnectorTrade.TradeDate), 2);
        AssertOffset<TConnectorTrade>(nameof(TConnectorTrade.TradeNumber), 20);
        AssertOffset<TConnectorTrade>(nameof(TConnectorTrade.Price), 24);
        AssertOffset<TConnectorTrade>(nameof(TConnectorTrade.Quantity), 32);
        AssertOffset<TConnectorTrade>(nameof(TConnectorTrade.Volume), 40);
        AssertOffset<TConnectorTrade>(nameof(TConnectorTrade.BuyAgent), 48);
        AssertOffset<TConnectorTrade>(nameof(TConnectorTrade.SellAgent), 52);
        AssertOffset<TConnectorTrade>(nameof(TConnectorTrade.TradeType), 56);
        AssertOffset<TConnectorAssetIdentifier>(nameof(TConnectorAssetIdentifier.Version), 0);
        AssertOffset<TConnectorAssetIdentifier>(nameof(TConnectorAssetIdentifier.Ticker), 8);
        AssertOffset<TConnectorAssetIdentifier>(nameof(TConnectorAssetIdentifier.Exchange), 16);
        AssertOffset<TConnectorAssetIdentifier>(nameof(TConnectorAssetIdentifier.FeedType), 24);

        var fields = new[] { "Year", "Month", "DayOfWeek", "Day", "Hour", "Minute", "Second", "Milliseconds" };
        for (var index = 0; index < fields.Length; index++)
        {
            AssertOffset<SystemTime>(fields[index], index * 2);
        }
    }

    [Fact]
    public void UnmanagedBufferPreservesWidthsAndUnknownCodes()
    {
        var pointer = Marshal.AllocHGlobal(64);
        try
        {
            Marshal.Copy(new byte[64], 0, pointer, 64);
            Marshal.WriteInt16(pointer, 2, 2026);
            Marshal.WriteInt16(pointer, 16, 999);
            Marshal.WriteInt32(pointer, 20, -1);
            Marshal.WriteInt64(pointer, 24, BitConverter.DoubleToInt64Bits(60.25));
            Marshal.WriteInt64(pointer, 32, (long)int.MaxValue + 123);
            Marshal.WriteInt64(pointer, 40, BitConverter.DoubleToInt64Bits(123456.75));
            Marshal.WriteInt32(pointer, 48, -1);
            Marshal.WriteInt32(pointer, 52, 987);
            Marshal.WriteByte(pointer, 56, 255);

            var trade = Marshal.PtrToStructure<TConnectorTrade>(pointer);
            Assert.Equal((ushort)2026, trade.TradeDate.Year);
            Assert.Equal((ushort)999, trade.TradeDate.Milliseconds);
            Assert.Equal(uint.MaxValue, trade.TradeNumber);
            Assert.Equal(60.25, trade.Price);
            Assert.Equal((long)int.MaxValue + 123, trade.Quantity);
            Assert.Equal(123456.75, trade.Volume);
            Assert.Equal(-1, trade.BuyAgent);
            Assert.Equal(987, trade.SellAgent);
            Assert.Equal((TradeType)255, trade.TradeType);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [Theory]
    [InlineData("SetTradeCallbackV2")]
    [InlineData("TranslateTrade")]
    [InlineData("GetAgentNameLength")]
    [InlineData("GetAgentName")]
    public void NativeImportsUseStdCallAndExpectedLibrary(string name)
    {
        var method = typeof(ProfitFunctions).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        var import = method!.GetCustomAttribute<DllImportAttribute>();
        Assert.NotNull(import);
        Assert.Equal("ProfitDLL.dll", import.Value);
        Assert.Equal(CallingConvention.StdCall, import.CallingConvention);
    }

    [Fact]
    public void CallbackAndNameBufferUseNativeWidths()
    {
        var callback = typeof(TConnectorTradeCallback);
        Assert.Equal(CallingConvention.StdCall,
            callback.GetCustomAttribute<UnmanagedFunctionPointerAttribute>()!.CallingConvention);
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(TConnectorTradeCallbackFlags)));
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(TradeType)));
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(AgentNameFlags)));
        var nameBuffer = typeof(ProfitFunctions).GetMethod("GetAgentName",
            BindingFlags.Static | BindingFlags.NonPublic)!.GetParameters()[2];
        Assert.True(nameBuffer.IsOut);
        Assert.Equal(typeof(char[]), nameBuffer.ParameterType);
        Assert.Equal(UnmanagedType.U2, nameBuffer.GetCustomAttribute<MarshalAsAttribute>()!.ArraySubType);
    }

    private static void AssertOffset<T>(string field, int offset) where T : struct =>
        Assert.Equal((nint)offset, Marshal.OffsetOf<T>(field));
}
