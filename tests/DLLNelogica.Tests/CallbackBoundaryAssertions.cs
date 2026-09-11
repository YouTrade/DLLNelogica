using System.Runtime.InteropServices;
using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

internal static class CallbackBoundaryAssertions
{
    internal static void NamesAreStopped(ProfitSession session) =>
        Assert.False(session.LookupAgentName(123, AgentNameFlags.ShortName).WasExecuted);

    internal static void UnsubscribedOnce(FakeProfitApi api) => Assert.Equal("VALE3:B", Assert.Single(api.Unsubscribed));

    internal static TConnectorTradeCallback CollectAndRecoverTradeCallback()
    {
        var pointer = Marshal.GetFunctionPointerForDelegate(ProfitCallbackRoots.Callbacks.TradeV2);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return Marshal.GetDelegateForFunctionPointer<TConnectorTradeCallback>(pointer);
    }

    internal static void SignalsFailure(ProfitCallbackBridge bridge, TConnectorTradeCallback callback)
    {
        using var shutdown = new CancellationTokenSource();
        bridge.AttachShutdown(shutdown);
        try
        {
            var error = Record.Exception(() => callback(
                TradeTranslationTests.Asset(), (nint)1, TConnectorTradeCallbackFlags.None));
            Assert.Null(error);
            Assert.True(shutdown.IsCancellationRequested);
        }
        finally
        {
            bridge.DetachShutdown(shutdown);
        }
    }
}
