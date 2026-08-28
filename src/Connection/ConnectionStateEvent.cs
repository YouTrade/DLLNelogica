namespace DLLNelogica.Connection;

internal readonly record struct ConnectionStateEvent(
    DateTimeOffset Timestamp,
    ConnectionStateType StateType,
    int Result);
