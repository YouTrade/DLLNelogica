namespace DLLNelogica.Connection;

internal readonly record struct ConnectionStateSnapshot(
    bool IsLoginReady,
    bool IsRoutingReady,
    bool HasReachedMarketData,
    bool IsActivationValid,
    MarketDataHealth CurrentMarketDataHealth,
    bool IsInitialConnectionReady,
    int ReadinessTransitionCount);
