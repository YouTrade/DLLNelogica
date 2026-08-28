using System.Threading.Channels;

namespace DLLNelogica.Connection;

internal sealed class ConnectionStateEventPump
{
    private readonly ConnectionStateMachine _stateMachine;
    private readonly Channel<ConnectionStateEvent> _events =
        Channel.CreateUnbounded<ConnectionStateEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    private int _failureDetected;

    internal ConnectionStateEventPump(ConnectionStateMachine stateMachine)
    {
        _stateMachine = stateMachine;
    }

    internal bool HasFailed => Volatile.Read(ref _failureDetected) != 0;

    internal bool TryPublish(int stateType, int result) =>
        _events.Writer.TryWrite(new ConnectionStateEvent(
            DateTimeOffset.Now,
            (ConnectionStateType)stateType,
            result));

    internal void Complete() => _events.Writer.TryComplete();

    internal async Task RunAsync(CancellationTokenSource shutdownRequested)
    {
        try
        {
            await foreach (var stateEvent in _events.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                // O tee enfileira arquivo e console antes de Process liberar a espera. Assim, o
                // estado causador sempre precede a confirmação sem bloquear esta thread em I/O.
                TryWriteLine(
                    $"{stateEvent.Timestamp:O} Estado: tipo={(int)stateEvent.StateType}, resultado={stateEvent.Result}");
                _stateMachine.Process(stateEvent.StateType, stateEvent.Result);

                ReportMarketDataHealth(stateEvent);
            }
        }
        catch (Exception exception)
        {
            ReportConsumerFailure(exception);
            Interlocked.Exchange(ref _failureDetected, 1);

            try
            {
                shutdownRequested.Cancel();
            }
            catch
            {
                // O consumidor nunca deve propagar uma segunda falha durante o encerramento.
            }
        }
    }

    private void ReportMarketDataHealth(ConnectionStateEvent stateEvent)
    {
        if (stateEvent.StateType != ConnectionStateType.MarketData)
        {
            return;
        }

        var snapshot = _stateMachine.GetSnapshot();
        switch (snapshot.CurrentMarketDataHealth)
        {
            case MarketDataHealth.Degraded:
                TryWriteLine("Aviso: market data conectado com desempenho degradado.");
                break;
            case MarketDataHealth.Critical:
                TryWriteLine(
                    "Aviso crítico: market data conectado, mas a entrega local de callbacks está parada.");
                break;
            case MarketDataHealth.Unknown when snapshot.HasReachedMarketData:
                TryWriteLine(
                    $"Aviso: market data informou o estado {stateEvent.Result}; " +
                    "a prontidão inicial permanece confirmada, mas a saúde corrente é desconhecida.");
                break;
        }
    }

    private static void TryWriteLine(string message)
    {
        try
        {
            Console.WriteLine(message);
        }
#pragma warning disable CA1031 // Console indisponível não pode impedir a transição de estado.
        catch (Exception)
#pragma warning restore CA1031
        {
            // A máquina continua funcional mesmo sem tee, console ou pipe de saída.
        }
    }

    private static void ReportConsumerFailure(Exception exception)
    {
        try
        {
            Console.Error.WriteLine(
                $"Falha no consumidor de estados ({exception.GetType().Name}); encerramento solicitado.");
        }
        catch
        {
            // A sinalização de falha não depende da disponibilidade da saída de erro.
        }
    }
}
