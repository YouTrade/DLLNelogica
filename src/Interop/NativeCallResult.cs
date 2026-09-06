namespace DLLNelogica.Interop;

internal readonly record struct NativeCallResult(
    string Operation,
    int? NativeResult,
    string? ExceptionType)
{
    internal bool IsSuccessful => NativeResult == (int)NResult.NL_OK && ExceptionType is null;

    internal string Message => ExceptionType is null
        ? $"{Operation} retornou {FormatNativeResult(NativeResult.GetValueOrDefault())}."
        : $"{Operation} falhou antes de retornar ({ExceptionType}).";

    internal static NativeCallResult Completed(string operation, int nativeResult) =>
        new(operation, nativeResult, null);

    internal static NativeCallResult Failed(string operation, Exception exception) =>
        new(operation, null, exception.GetType().Name);

    private static string FormatNativeResult(int result)
    {
        var numericResult = $"{result} (0x{unchecked((uint)result):X8})";
        return Enum.IsDefined(typeof(NResult), result)
            ? $"{(NResult)result} — {numericResult}"
            : $"NResult desconhecido — {numericResult}";
    }
}
