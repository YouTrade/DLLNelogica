namespace DLLNelogica.Interop;

internal readonly record struct ProfitInitializationResult(bool IsAccepted, string Message)
{
    internal static ProfitInitializationResult Accepted() => new(true, string.Empty);

    internal static ProfitInitializationResult Failed(string message) => new(false, message);
}
