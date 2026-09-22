namespace DLLNelogica.Connection;

// União mínima para o canal: exatamente um dos dois campos está preenchido.
internal readonly record struct ConnectionPumpEvent(ConnectionStateEvent? State, string? Line)
{
    internal static ConnectionPumpEvent FromState(ConnectionStateEvent state) => new(state, null);

    internal static ConnectionPumpEvent FromLine(string line) => new(null, line);
}
