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
    private readonly Dictionary<int, int> _lastLoggedResults = [];
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
                // Nada é registrado depois do pedido de encerramento: a DLL reemite os estados
                // da sessão sendo derrubada e traduzi-los como falha de login ou de licença
                // faria uma execução bem-sucedida parecer um erro de credencial.
                // IsTransition fica por último para só memorizar o que de fato virou linha.
                var isReportable =
                    !shutdownRequested.IsCancellationRequested &&
                    IsReportable(stateEvent) &&
                    IsTransition(stateEvent);

                // O tee enfileira arquivo e console antes de Process liberar a espera. Assim, o
                // estado causador sempre precede a confirmação sem bloquear esta thread em I/O.
                if (isReportable)
                {
                    TryWriteLine(Describe(stateEvent));
                }

                // Process recebe todos os eventos, inclusive os silenciados: a máquina de
                // estados depende do fluxo íntegro para decidir a prontidão da conexão.
                _stateMachine.Process(stateEvent.StateType, stateEvent.Result);

                if (isReportable)
                {
                    ReportMarketDataHealth(stateEvent);
                }
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

    // O handshake de roteamento não entra no log: a DLL o reemite por servidor e por corretora,
    // alternando entre os dois resultados dezenas de vezes, e um roteamento que não sobe já
    // aparece nos estados pendentes do timeout de conexão. Do Market Data só interessam os
    // resultados de saúde; os intermediários de "conectando" são ruído do mesmo handshake.
    private static bool IsReportable(ConnectionStateEvent stateEvent) => stateEvent.StateType switch
    {
        ConnectionStateType.Routing => false,
        ConnectionStateType.MarketData => stateEvent.Result is
            (int)MarketDataStateResult.Connected or
            (int)MarketDataStateResult.Degraded or
            (int)MarketDataStateResult.Critical,
        _ => true
    };

    // O último resultado é memorizado por tipo de estado, não globalmente: a DLL intercala os
    // quatro tipos, então comparar só com a linha anterior deixa passar repetições do mesmo
    // estado separadas por outro. Guardar por tipo ainda preserva oscilações reais de saúde do
    // market data (4 -> 5 -> 4 continua rendendo três linhas).
    // O canal é SingleReader, então o dicionário dispensa sincronização.
    private bool IsTransition(ConnectionStateEvent stateEvent)
    {
        var stateType = (int)stateEvent.StateType;
        if (_lastLoggedResults.TryGetValue(stateType, out var lastResult) &&
            lastResult == stateEvent.Result)
        {
            return false;
        }

        _lastLoggedResults[stateType] = stateEvent.Result;
        return true;
    }

    private static string Describe(ConnectionStateEvent stateEvent) => stateEvent.StateType switch
    {
        ConnectionStateType.Login => $"Login: {DescribeLogin(stateEvent.Result)}",
        ConnectionStateType.Routing => $"Roteamento: {DescribeRouting(stateEvent.Result)}",
        ConnectionStateType.MarketData => $"Market Data: {DescribeMarketData(stateEvent.Result)}",
        ConnectionStateType.Activation => $"Ativação: {DescribeActivation(stateEvent.Result)}",
        _ => $"Estado não mapeado (tipo={(int)stateEvent.StateType}, resultado={stateEvent.Result})."
    };

    private static string DescribeLogin(int result) => result switch
    {
        (int)LoginStateResult.Connected => "conectado.",
        (int)LoginStateResult.InvalidLogin => "usuário inválido.",
        (int)LoginStateResult.InvalidPassword => "senha inválida.",
        (int)LoginStateResult.BlockedPassword => "senha bloqueada.",
        (int)LoginStateResult.ExpiredPassword => "senha expirada.",
        (int)LoginStateResult.UnknownFailure => "falha desconhecida.",
        _ => $"resultado {result} não mapeado."
    };

    private static string DescribeRouting(int result) => result switch
    {
        (int)RoutingStateResult.ServerConnected => "conectado ao servidor.",
        (int)RoutingStateResult.BrokerConnected => "conectado à corretora.",
        _ => $"resultado {result} não mapeado."
    };

    private static string DescribeMarketData(int result) => result switch
    {
        (int)MarketDataStateResult.Connected => "conectado e pronto para receber cotações.",
        (int)MarketDataStateResult.Degraded => "degradado.",
        (int)MarketDataStateResult.Critical => "crítico.",
        _ => $"resultado {result} não mapeado."
    };

    private static string DescribeActivation(int result) =>
        result == (int)ActivationStateResult.Valid
            ? "licença válida."
            : $"licença inválida (resultado {result}).";

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
