namespace DLLNelogica.Interop;

internal readonly record struct ProfitFinalizationResult(
    bool WasExecuted,
    bool IsSuccessful,
    string Message)
{
    internal static ProfitFinalizationResult NotRequired() => new(false, true, string.Empty);

    internal static ProfitFinalizationResult Completed(int nativeResult) =>
        new(true, true, $"DLLFinalize retornou {nativeResult} (0x{unchecked((uint)nativeResult):X8}).");

    internal static ProfitFinalizationResult Failed(Exception exception) =>
        new(true, false, $"Falha ao executar DLLFinalize ({exception.GetType().Name}).");
}
