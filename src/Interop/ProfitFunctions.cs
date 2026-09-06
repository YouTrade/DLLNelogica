using System.Runtime.InteropServices;

namespace DLLNelogica.Interop;

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

    [DllImport(DllPath, CallingConvention = CallingConvention.StdCall)]
    internal static extern int SetChangeCotationCallback(TChangeCotation callback);

    [DllImport(DllPath, CallingConvention = CallingConvention.StdCall)]
    internal static extern int SetInvalidTickerCallback(TInvalidTickerCallback callback);

    [DllImport(DllPath, CallingConvention = CallingConvention.StdCall)]
    internal static extern int SubscribeTicker(
        [MarshalAs(UnmanagedType.LPWStr)] string ticker,
        [MarshalAs(UnmanagedType.LPWStr)] string exchange);

    [DllImport(DllPath, CallingConvention = CallingConvention.StdCall)]
    internal static extern int UnsubscribeTicker(
        [MarshalAs(UnmanagedType.LPWStr)] string ticker,
        [MarshalAs(UnmanagedType.LPWStr)] string exchange);
}
