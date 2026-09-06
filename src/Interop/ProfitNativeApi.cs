using System.Reflection;
using System.Runtime.InteropServices;

namespace DLLNelogica.Interop;

internal sealed class ProfitNativeApi : IProfitApi
{
    internal ProfitNativeApi(string baseDirectory)
    {
        LibraryPath = Path.Combine(baseDirectory, "ProfitDLL.dll");
    }

    public string LibraryPath { get; }

    public void ConfigureLibraryResolver() =>
        NativeLibrary.SetDllImportResolver(typeof(ProfitFunctions).Assembly, ResolveProfitDll);

    public int Initialize(
        string activationKey,
        string user,
        string password,
        ProfitCallbackSet callbacks) =>
        ProfitFunctions.DLLInitializeLogin(
            activationKey,
            user,
            password,
            callbacks.State,
            null,
            null,
            callbacks.Account,
            null,
            callbacks.NewDaily,
            null,
            null,
            null,
            callbacks.Progress,
            callbacks.TinyBook);

    public int FinalizeServices() => ProfitFunctions.DLLFinalize();

    public int SetChangeCotationCallback(TChangeCotation callback) =>
        ProfitFunctions.SetChangeCotationCallback(callback);

    public int SetInvalidTickerCallback(TInvalidTickerCallback callback) =>
        ProfitFunctions.SetInvalidTickerCallback(callback);

    public int SubscribeTicker(string ticker, string exchange) =>
        ProfitFunctions.SubscribeTicker(ticker, exchange);

    public int UnsubscribeTicker(string ticker, string exchange) =>
        ProfitFunctions.UnsubscribeTicker(ticker, exchange);

    private nint ResolveProfitDll(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "ProfitDLL.dll", StringComparison.OrdinalIgnoreCase))
        {
            return nint.Zero;
        }

        return NativeLibrary.Load(LibraryPath);
    }
}
