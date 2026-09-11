namespace DLLNelogica.Interop;

// NativeResult é tamanho copiado ou erro nativo; um tamanho positivo não é um NResult.
internal readonly record struct AgentNameLookupResult(
    bool WasExecuted,
    string? Name,
    int? NativeResult,
    string? ExceptionType)
{
    internal bool IsResolved => !string.IsNullOrEmpty(Name);
}
