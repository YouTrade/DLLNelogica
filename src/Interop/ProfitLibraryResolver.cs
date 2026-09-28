using System.Reflection;
using System.Runtime.InteropServices;

namespace DLLNelogica.Interop;

internal sealed class ProfitLibraryResolver(string libraryPath)
{
    internal void Configure() => NativeLibrary.SetDllImportResolver(typeof(ProfitFunctions).Assembly, ResolveProfitDll);

    private nint ResolveProfitDll(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "ProfitDLL.dll", StringComparison.OrdinalIgnoreCase))
        {
            return nint.Zero;
        }

        return NativeLibrary.Load(libraryPath);
    }
}
