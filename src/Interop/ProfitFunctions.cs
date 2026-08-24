using System.Runtime.InteropServices;

namespace DLLNelogica;

internal static class ProfitFunctions
{
    private const string DllPath = "ProfitDLL.dll";

    [DllImport(DllPath, CallingConvention = CallingConvention.StdCall)]
    internal static extern int DLLInitializeLogin(
        [MarshalAs(UnmanagedType.LPWStr)] string activationKey,
        [MarshalAs(UnmanagedType.LPWStr)] string user,
        [MarshalAs(UnmanagedType.LPWStr)] string password,
        TStateCallback stateCallback,
        THistoryCallBack? historyCallBack,
        TOrderChangeCallBack? orderChangeCallBack,
        TAccountCallback accountCallback,
        TTradeCallback? newTradeCallback,
        TNewDailyCallback newDailyCallback,
        TPriceBookCallback? priceBookCallback,
        TOfferBookCallback? offerBookCallback,
        THistoryTradeCallback? newHistoryCallback,
        TProgressCallBack progressCallBack,
        TNewTinyBookCallBack newTinyBookCallBack);

    [DllImport(DllPath, CallingConvention = CallingConvention.StdCall)]
    internal static extern int DLLFinalize();
}
