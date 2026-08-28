namespace DLLNelogica.Connection;

internal enum LoginStateResult
{
    Connected = 0,
    InvalidLogin = 1,
    InvalidPassword = 2,
    BlockedPassword = 3,
    ExpiredPassword = 4,
    UnknownFailure = 200
}

internal enum RoutingStateResult
{
    ServerConnected = 2,
    BrokerConnected = 5
}

internal enum MarketDataStateResult
{
    Connected = 4,
    Degraded = 5,
    Critical = 6
}

internal enum ActivationStateResult
{
    Valid = 0
}
