namespace DLLNelogica.Interop;

internal static class ProfitProcessLifetime
{
    /// <summary>
    /// Esta guarda precisa ser process-wide: a ProfitDLL aceita somente uma inicialização útil
    /// por processo. Torná-la estado de sessão permite uma segunda chamada que retorna NL_OK,
    /// mas nunca completa roteamento, market data e ativação.
    /// </summary>
    private static int _initializeLoginStarted;

    /// <summary>
    /// Impede DLLFinalize sem uma inicialização nativa aceita. Pode ser propriedade de uma
    /// sessão exclusiva somente enquanto <see cref="_initializeLoginStarted"/> continuar sendo
    /// uma guarda de processo.
    /// </summary>
    private static int _nativeInitializationAccepted;

    /// <summary>
    /// Garante finalização única. Sua segurança depende da mesma exclusividade de processo
    /// estabelecida por <see cref="_initializeLoginStarted"/>.
    /// </summary>
    private static int _finalizationStarted;

    private static int _callbackFailureDetected;

    internal static bool HasFinalizationStarted => Volatile.Read(ref _finalizationStarted) != 0;

    internal static bool HasCallbackFailure => Volatile.Read(ref _callbackFailureDetected) != 0;

    internal static bool IsFinalizationRequired =>
        Volatile.Read(ref _nativeInitializationAccepted) != 0 &&
        Volatile.Read(ref _finalizationStarted) == 0;

    internal static bool TryBeginInitialization() =>
        Interlocked.CompareExchange(ref _initializeLoginStarted, 1, 0) == 0;

    internal static void MarkInitializationAccepted() =>
        Volatile.Write(ref _nativeInitializationAccepted, 1);

    internal static bool TryBeginFinalization() =>
        Volatile.Read(ref _nativeInitializationAccepted) != 0 &&
        Interlocked.CompareExchange(ref _finalizationStarted, 1, 0) == 0;

    internal static void SignalCallbackFailure() =>
        Interlocked.Exchange(ref _callbackFailureDetected, 1);
}
