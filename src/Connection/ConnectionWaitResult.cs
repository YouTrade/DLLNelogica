namespace DLLNelogica.Connection;

internal readonly record struct ConnectionWaitResult(bool IsConnected, string Message)
{
    internal static ConnectionWaitResult Connected() =>
        new(true, "Login, roteamento, market data e ativação estão prontos.");

    internal static ConnectionWaitResult Failed(string message) => new(false, message);
}
