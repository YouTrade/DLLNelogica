namespace DLLNelogica.Connection;

internal sealed class ConnectionStateMachine
{
    private readonly object _sync = new();
    private readonly TaskCompletionSource<ConnectionWaitResult> _initialConnection =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _loginReady;
    private bool _routingReady;
    private bool _marketDataReached;
    private bool _activationValid;
    private bool _initialConnectionReady;
    private MarketDataHealth _marketDataHealth;
    private int _readinessTransitionCount;

    internal void Process(ConnectionStateType stateType, int result)
    {
        lock (_sync)
        {
            switch (stateType)
            {
                case ConnectionStateType.Login:
                    ProcessLogin(result);
                    break;
                case ConnectionStateType.Routing:
                    _routingReady |= result is
                        (int)RoutingStateResult.ServerConnected or
                        (int)RoutingStateResult.BrokerConnected;
                    break;
                case ConnectionStateType.MarketData:
                    ProcessMarketData(result);
                    break;
                case ConnectionStateType.Activation:
                    _activationValid = result == (int)ActivationStateResult.Valid;
                    break;
            }

            if (_loginReady &&
                _routingReady &&
                _marketDataReached &&
                _activationValid &&
                _initialConnection.TrySetResult(ConnectionWaitResult.Connected()))
            {
                _initialConnectionReady = true;
                _readinessTransitionCount++;
            }
        }
    }

    internal ConnectionStateSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new ConnectionStateSnapshot(
                _loginReady,
                _routingReady,
                _marketDataReached,
                _activationValid,
                _marketDataHealth,
                _initialConnectionReady,
                _readinessTransitionCount);
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
            return await _initialConnection.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
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
        if (result == (int)LoginStateResult.Connected)
        {
            _loginReady = true;
            return;
        }

        if (result is
            (int)LoginStateResult.InvalidLogin or
            (int)LoginStateResult.InvalidPassword or
            (int)LoginStateResult.BlockedPassword or
            (int)LoginStateResult.ExpiredPassword or
            (int)LoginStateResult.UnknownFailure)
        {
            _initialConnection.TrySetResult(ConnectionWaitResult.Failed(DescribeLoginFailure(result)));
        }
    }

    private void ProcessMarketData(int result)
    {
        _marketDataHealth = result switch
        {
            (int)MarketDataStateResult.Connected => MarketDataHealth.Connected,
            (int)MarketDataStateResult.Degraded => MarketDataHealth.Degraded,
            (int)MarketDataStateResult.Critical => MarketDataHealth.Critical,
            _ => MarketDataHealth.Unknown
        };

        _marketDataReached |= result == (int)MarketDataStateResult.Connected;
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

            if (!_marketDataReached)
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
        (int)LoginStateResult.InvalidLogin => "Login inválido (resultado 1).",
        (int)LoginStateResult.InvalidPassword => "Senha inválida (resultado 2).",
        (int)LoginStateResult.BlockedPassword => "Senha bloqueada (resultado 3).",
        (int)LoginStateResult.ExpiredPassword => "Senha expirada (resultado 4).",
        (int)LoginStateResult.UnknownFailure => "Falha de login desconhecida (resultado 200).",
        _ => $"Falha de login (resultado {result})."
    };
}
