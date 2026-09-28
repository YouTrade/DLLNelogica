using System.Reflection;
using System.Runtime.InteropServices;
using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

public sealed class AccountAbiTests
{
    [Fact]
    public void LayoutMatchesDelphiWin64IncludingEmbeddedWideCharArrays()
    {
        Assert.True(Environment.Is64BitProcess);
        Assert.Equal(424, Marshal.SizeOf<TConnectorAccountIdentifierOut>());
        AssertOffsets<TConnectorAccountIdentifierOut>(
            ["Version", "BrokerId", "AccountId", "AccountIdLength", "SubAccountId", "SubAccountIdLength", "Reserved"],
            [0, 4, 8, 208, 212, 412, 416]);
        Assert.Equal(32, Marshal.SizeOf<TConnectorAccountIdentifier>());
        AssertOffsets<TConnectorAccountIdentifier>(
            ["Version", "BrokerId", "AccountId", "SubAccountId", "Reserved"], [0, 4, 8, 16, 24]);
        Assert.Equal(96, Marshal.SizeOf<TConnectorTradingAccountOut>());
        AssertOffsets<TConnectorTradingAccountOut>(
            ["Version", "AccountId", "BrokerName", "BrokerNameLength", "OwnerName", "OwnerNameLength",
                "SubOwnerName", "SubOwnerNameLength", "AccountFlags", "AccountType"],
            [0, 8, 40, 48, 56, 64, 72, 80, 84, 88]);
    }

    [Fact]
    public void NativeArrayStrideAndUnicodeArePreserved()
    {
        var pointer = Marshal.AllocHGlobal(848);
        try
        {
            Marshal.Copy(new byte[848], 0, pointer, 848);
            Marshal.WriteInt32(pointer, 4, 15);
            Marshal.WriteInt16(pointer, 8, 'ã');
            Marshal.WriteInt32(pointer, 208, 1);
            Marshal.WriteInt32(pointer, 424 + 4, 51401);
            Marshal.WriteInt64(pointer, 424 + 416, -1);
            var first = Marshal.PtrToStructure<TConnectorAccountIdentifierOut>(pointer);
            var second = Marshal.PtrToStructure<TConnectorAccountIdentifierOut>(pointer + 424);
            Assert.Equal(15, first.BrokerId);
            Assert.Equal('ã', first.AccountId[0]);
            Assert.Equal(100, first.AccountId.Length);
            Assert.Equal(1, first.AccountIdLength);
            Assert.Equal(51401, second.BrokerId);
            Assert.Equal(-1, second.Reserved);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [Theory]
    [InlineData("GetAccountCount")]
    [InlineData("GetAccounts")]
    [InlineData("GetAccountDetails")]
    public void ImportsUseStdCall(string name)
    {
        var method = typeof(ProfitFunctions).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
        var import = method.GetCustomAttribute<DllImportAttribute>()!;
        Assert.Equal("ProfitDLL.dll", import.Value);
        Assert.Equal(CallingConvention.StdCall, import.CallingConvention);
    }

    [Fact]
    public void AccountArrayAndDetailsArePassedAsWritableNativeBuffers()
    {
        var accounts = typeof(ProfitFunctions).GetMethod("GetAccounts",
            BindingFlags.NonPublic | BindingFlags.Static)!.GetParameters()[3];
        Assert.True(accounts.IsIn);
        Assert.True(accounts.IsOut);
        var marshal = accounts.GetCustomAttribute<MarshalAsAttribute>()!;
        Assert.Equal(UnmanagedType.LPArray, marshal.Value);
        Assert.Equal(2, marshal.SizeParamIndex);
        var details = typeof(ProfitFunctions).GetMethod("GetAccountDetails",
            BindingFlags.NonPublic | BindingFlags.Static)!.GetParameters()[0];
        Assert.Equal(typeof(TConnectorTradingAccountOut).MakeByRefType(), details.ParameterType);
    }

    private static void AssertOffsets<T>(string[] fields, int[] offsets) where T : struct
    {
        for (var index = 0; index < fields.Length; index++)
        {
            Assert.Equal((nint)offsets[index], Marshal.OffsetOf<T>(fields[index]));
        }
    }
}
