namespace DLLNelogica;

internal enum MarketDataHealth
{
    Unknown,
    Connected,
    Degraded,
    Critical
}

internal readonly record struct ConnectionWaitResult(bool IsConnected, string Message)
{
    internal static ConnectionWaitResult Connected() =>
        new(true, "Login, roteamento, market data e ativação estão prontos.");

    internal static ConnectionWaitResult Failed(string message) => new(false, message);
}

internal sealed class ConnectionStateMachine
{
    private const int LoginStateType = 0;
    private const int RoutingStateType = 1;
    private const int MarketDataStateType = 2;
    private const int ActivationStateType = 3;
    private readonly object _sync = new();
    private readonly TaskCompletionSource<ConnectionWaitResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _loginReady;
    private bool _routingReady;
    private bool _marketDataReady;
    private bool _activationValid;
    private MarketDataHealth _marketDataHealth;
    private int _readinessTransitionCount;

    internal bool IsLoginReady
    {
        get
        {
            lock (_sync)
            {
                return _loginReady;
            }
        }
    }

    internal bool IsRoutingReady
    {
        get
        {
            lock (_sync)
            {
                return _routingReady;
            }
        }
    }

    internal bool IsMarketDataReady
    {
        get
        {
            lock (_sync)
            {
                return _marketDataReady;
            }
        }
    }

    internal bool IsActivationValid
    {
        get
        {
            lock (_sync)
            {
                return _activationValid;
            }
        }
    }

    internal MarketDataHealth CurrentMarketDataHealth
    {
        get
        {
            lock (_sync)
            {
                return _marketDataHealth;
            }
        }
    }

    internal int ReadinessTransitionCount
    {
        get
        {
            lock (_sync)
            {
                return _readinessTransitionCount;
            }
        }
    }

    internal void Process(int stateType, int result)
    {
        lock (_sync)
        {
            switch (stateType)
            {
                case LoginStateType:
                    ProcessLogin(result);
                    break;
                case RoutingStateType:
                    _routingReady |= result is 2 or 5;
                    break;
                case MarketDataStateType:
                    ProcessMarketData(result);
                    break;
                case ActivationStateType:
                    _activationValid = result == 0;
                    break;
            }

            if (_loginReady &&
                _routingReady &&
                _marketDataReady &&
                _activationValid &&
                _completion.TrySetResult(ConnectionWaitResult.Connected()))
            {
                _readinessTransitionCount++;
            }
        }
    }

    internal async Task<ConnectionWaitResult> WaitForConnectionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "O timeout deve ser maior que zero.");
        }

        try
        {
            return await _completion.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return ConnectionWaitResult.Failed(
                $"Tempo limite de conexão atingido. Estados pendentes: {DescribePendingStates()}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ConnectionWaitResult.Failed("Espera de conexão interrompida para encerramento ordenado.");
        }
    }

    private void ProcessLogin(int result)
    {
        if (result == 0)
        {
            _loginReady = true;
            return;
        }

        if (result is 1 or 2 or 3 or 4 or 200)
        {
            _completion.TrySetResult(ConnectionWaitResult.Failed(DescribeLoginFailure(result)));
        }
    }

    private void ProcessMarketData(int result)
    {
        _marketDataHealth = result switch
        {
            4 => MarketDataHealth.Connected,
            5 => MarketDataHealth.Degraded,
            6 => MarketDataHealth.Critical,
            _ => MarketDataHealth.Unknown
        };

        _marketDataReady |= result == 4;
    }

    private string DescribePendingStates()
    {
        lock (_sync)
        {
            var pendingStates = new List<string>(4);

            if (!_loginReady)
            {
                pendingStates.Add("login");
            }

            if (!_routingReady)
            {
                pendingStates.Add("roteamento");
            }

            if (!_marketDataReady)
            {
                pendingStates.Add("market data");
            }

            if (!_activationValid)
            {
                pendingStates.Add("ativação válida corrente");
            }

            return pendingStates.Count == 0
                ? "nenhum; aguardando resultado terminal"
                : string.Join(", ", pendingStates);
        }
    }

    private static string DescribeLoginFailure(int result) => result switch
    {
        1 => "Login inválido (resultado 1).",
        2 => "Senha inválida (resultado 2).",
        3 => "Senha bloqueada (resultado 3).",
        4 => "Senha expirada (resultado 4).",
        200 => "Falha de login desconhecida (resultado 200).",
        _ => $"Falha de login (resultado {result})."
    };
}
