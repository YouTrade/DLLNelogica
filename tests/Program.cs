using System.Reflection;
using DLLNelogica;

var tests = new (string Name, Func<Task> Execute)[]
{
    ("01 - sequência real de 44 eventos", TestProductionSequenceAsync),
    ("02 - estados em ordem arbitrária", TestArbitraryOrderAsync),
    ("03 - estados repetidos", TestRepeatedStatesAsync),
    ("04 - latch de roteamento após 2 para 4 para 5 para 4", TestRoutingTwoFourFiveAsync),
    ("05 - roteamento somente com 5", TestRoutingOnlyFiveAsync),
    ("06 - market 4 para 5", TestMarketFourFiveAsync),
    ("07 - market 4 para 6", TestMarketFourSixAsync),
    ("08 - recuperação de market 6 para 4", TestMarketRecoveryAsync),
    ("09 - market 5 antes do primeiro 4", TestMarketFiveBeforeFourAsync),
    ("10 - market 6 antes do primeiro 4", TestMarketSixBeforeFourAsync),
    ("11 - ativação 1 para 0", TestActivationRecoveryAsync),
    ("12 - ativação 0 para 1", TestActivationInvalidationAsync),
    ("13 - ativação inválida até timeout", TestActivationTimeoutAsync),
    ("14 - login inválido", () => TestTerminalLoginAsync(1, "Login inválido")),
    ("15 - senha inválida", () => TestTerminalLoginAsync(2, "Senha inválida")),
    ("16 - senha bloqueada", () => TestTerminalLoginAsync(3, "Senha bloqueada")),
    ("17 - senha expirada", () => TestTerminalLoginAsync(4, "Senha expirada")),
    ("18 - login desconhecido 200", () => TestTerminalLoginAsync(200, "desconhecida")),
    ("19 - ausência total de callbacks", TestNoCallbacksAsync),
    ("20 - cancelamento da espera", TestCancellationAsync),
    ("21 - canal concluído com evento tardio", TestLateEventAfterChannelCompletionAsync),
    ("22 - rajada concorrente de 160 mil eventos", TestConcurrentBurstAsync),
    ("23 - roteamento ausente impede prontidão", TestMissingRoutingAsync),
    ("24 - login ausente impede prontidão", TestMissingLoginAsync)
};

var failures = 0;

foreach (var test in tests)
{
    try
    {
        await test.Execute().ConfigureAwait(false);
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        var failure = exception is TargetInvocationException { InnerException: not null }
            ? exception.InnerException
            : exception;
        Console.Error.WriteLine($"FAIL {test.Name}: {failure.GetType().Name} - {failure.Message}");
    }
}

Console.WriteLine($"Resultado: {tests.Length - failures}/{tests.Length} testes aprovados.");
return failures == 0 ? 0 : 1;

static async Task TestProductionSequenceAsync()
{
    var machine = new ConnectionStateMachine();
    var sequence = CreateProductionSequence();

    AssertEqual(44, sequence.Count, "A sequência de produção deve conter 44 eventos.");

    foreach (var stateEvent in sequence)
    {
        machine.Process(stateEvent.StateType, stateEvent.Result);
    }

    await AssertConnectedOnceAsync(machine).ConfigureAwait(false);
}

static async Task TestArbitraryOrderAsync()
{
    var machine = new ConnectionStateMachine();
    machine.Process(2, 4);
    machine.Process(3, 0);
    machine.Process(1, 5);
    machine.Process(0, 0);

    await AssertConnectedOnceAsync(machine).ConfigureAwait(false);
}

static async Task TestRepeatedStatesAsync()
{
    var machine = new ConnectionStateMachine();

    for (var repetition = 0; repetition < 100; repetition++)
    {
        machine.Process(3, 0);
        machine.Process(0, 0);
        machine.Process(1, 2);
        machine.Process(1, 5);
        machine.Process(2, 4);
    }

    await AssertConnectedOnceAsync(machine).ConfigureAwait(false);
}

static async Task TestRoutingTwoFourFiveAsync()
{
    var machine = new ConnectionStateMachine();
    machine.Process(0, 0);
    machine.Process(2, 4);
    machine.Process(3, 0);
    machine.Process(1, 2);
    machine.Process(1, 4);
    machine.Process(1, 5);
    machine.Process(1, 4);

    AssertTrue(machine.IsRoutingReady, "Os estados 4 e 5 apagaram o latch acionado por 2.");
    await AssertConnectedOnceAsync(machine).ConfigureAwait(false);
}

static async Task TestRoutingOnlyFiveAsync()
{
    var machine = new ConnectionStateMachine();
    ProcessReadyStates(machine, routingResult: 5);

    AssertTrue(machine.IsRoutingReady, "Roteamento 5 não acionou o latch.");
    await AssertConnectedOnceAsync(machine).ConfigureAwait(false);
}

static async Task TestMarketFourFiveAsync()
{
    var machine = CreateReadyExceptMarket();
    machine.Process(2, 4);
    machine.Process(2, 5);

    AssertTrue(machine.IsMarketDataReady, "Market 5 apagou o latch acionado por 4.");
    AssertEqual(MarketDataHealth.Degraded, machine.CurrentMarketDataHealth, "Saúde de market incorreta.");
    await AssertConnectedOnceAsync(machine).ConfigureAwait(false);
}

static async Task TestMarketFourSixAsync()
{
    var machine = CreateReadyExceptMarket();
    machine.Process(2, 4);
    machine.Process(2, 6);

    AssertTrue(machine.IsMarketDataReady, "Market 6 apagou o latch acionado por 4.");
    AssertEqual(MarketDataHealth.Critical, machine.CurrentMarketDataHealth, "Saúde de market incorreta.");
    await AssertConnectedOnceAsync(machine).ConfigureAwait(false);
}

static async Task TestMarketRecoveryAsync()
{
    var machine = CreateReadyExceptMarket();
    machine.Process(2, 6);

    AssertTrue(!machine.IsMarketDataReady, "Market 6 acionou o latch antes do primeiro 4.");
    machine.Process(2, 4);

    AssertEqual(MarketDataHealth.Connected, machine.CurrentMarketDataHealth, "Market não recuperou para conectado.");
    await AssertConnectedOnceAsync(machine).ConfigureAwait(false);
}

static async Task TestMarketFiveBeforeFourAsync()
{
    var machine = CreateReadyExceptMarket();
    machine.Process(2, 5);

    var timeout = await machine.WaitForConnectionAsync(TimeSpan.FromMilliseconds(40)).ConfigureAwait(false);
    AssertTrue(!timeout.IsConnected, "Market 5 conectou antes do primeiro 4.");
    AssertContains(timeout.Message, "market data", "Timeout não indicou market data pendente.");
    AssertReadinessCount(machine, 0);

    machine.Process(2, 4);
    await AssertConnectedOnceAsync(machine).ConfigureAwait(false);
}

static async Task TestMarketSixBeforeFourAsync()
{
    var machine = CreateReadyExceptMarket();
    machine.Process(2, 6);

    var timeout = await machine.WaitForConnectionAsync(TimeSpan.FromMilliseconds(40)).ConfigureAwait(false);
    AssertTrue(!timeout.IsConnected, "Market 6 conectou antes do primeiro 4.");
    AssertContains(timeout.Message, "market data", "Timeout não indicou market data pendente.");
    AssertReadinessCount(machine, 0);

    machine.Process(2, 4);
    await AssertConnectedOnceAsync(machine).ConfigureAwait(false);
}

static async Task TestActivationRecoveryAsync()
{
    var machine = CreateReadyExceptActivation();
    machine.Process(3, 1);

    var timeout = await machine.WaitForConnectionAsync(TimeSpan.FromMilliseconds(40)).ConfigureAwait(false);
    AssertTrue(!timeout.IsConnected, "Ativação inválida conectou.");
    AssertReadinessCount(machine, 0);

    machine.Process(3, 0);
    await AssertConnectedOnceAsync(machine).ConfigureAwait(false);
}

static async Task TestActivationInvalidationAsync()
{
    var machine = new ConnectionStateMachine();
    machine.Process(3, 0);
    machine.Process(0, 0);
    machine.Process(1, 2);
    machine.Process(3, 1);
    machine.Process(2, 4);

    var timeout = await machine.WaitForConnectionAsync(TimeSpan.FromMilliseconds(40)).ConfigureAwait(false);
    AssertTrue(!timeout.IsConnected, "Ativação 0 seguida de 1 anunciou prontidão.");
    AssertContains(timeout.Message, "ativação válida corrente", "Timeout não indicou ativação pendente.");
    AssertReadinessCount(machine, 0);
}

static async Task TestActivationTimeoutAsync()
{
    var machine = CreateReadyExceptActivation();
    machine.Process(3, 1);

    var timeout = await machine.WaitForConnectionAsync(TimeSpan.FromMilliseconds(40)).ConfigureAwait(false);
    AssertTrue(!timeout.IsConnected, "Ativação inválida foi tratada como terminal positivo.");
    AssertContains(timeout.Message, "ativação válida corrente", "Timeout não preservou o diagnóstico de ativação.");
    AssertReadinessCount(machine, 0);
}

static async Task TestTerminalLoginAsync(int loginResult, string expectedMessage)
{
    var machine = new ConnectionStateMachine();
    machine.Process(0, loginResult);
    ProcessReadyStates(machine);

    var result = await machine.WaitForConnectionAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
    AssertTrue(!result.IsConnected, $"Login {loginResult} não terminou com falha.");
    AssertContains(result.Message, expectedMessage, $"Mensagem incorreta para login {loginResult}.");
    AssertContains(result.Message, loginResult.ToString(), $"Código {loginResult} não foi preservado.");
    AssertReadinessCount(machine, 0);
}

static async Task TestNoCallbacksAsync()
{
    var machine = new ConnectionStateMachine();
    var result = await machine.WaitForConnectionAsync(TimeSpan.FromMilliseconds(40)).ConfigureAwait(false);

    AssertTrue(!result.IsConnected, "Ausência de callbacks conectou.");
    AssertContains(result.Message, "login", "Login não apareceu como pendente.");
    AssertContains(result.Message, "roteamento", "Roteamento não apareceu como pendente.");
    AssertContains(result.Message, "market data", "Market data não apareceu como pendente.");
    AssertContains(result.Message, "ativação válida corrente", "Ativação não apareceu como pendente.");
    AssertReadinessCount(machine, 0);
}

static async Task TestCancellationAsync()
{
    var machine = new ConnectionStateMachine();
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    var result = await machine.WaitForConnectionAsync(
        TimeSpan.FromSeconds(1),
        cancellation.Token).ConfigureAwait(false);

    AssertTrue(!result.IsConnected, "Cancelamento retornou conexão.");
    AssertContains(result.Message, "interrompida", "Cancelamento não retornou diagnóstico próprio.");
    AssertReadinessCount(machine, 0);
}

static async Task TestLateEventAfterChannelCompletionAsync()
{
    var programType = typeof(ConnectionStateMachine).Assembly.GetType("DLLNelogica.Program", throwOnError: true)!;
    const BindingFlags privateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    var machine = new ConnectionStateMachine();
    var activeStateField = programType.GetField("_activeConnectionState", privateStatic)
        ?? throw new MissingFieldException(programType.FullName, "_activeConnectionState");
    var stateEventsField = programType.GetField("StateEvents", privateStatic)
        ?? throw new MissingFieldException(programType.FullName, "StateEvents");
    var handleState = programType.GetMethod("HandleState", privateStatic)
        ?? throw new MissingMethodException(programType.FullName, "HandleState");
    var channel = stateEventsField.GetValue(null)
        ?? throw new InvalidOperationException("Canal de estados não inicializado.");
    var writer = channel.GetType().GetProperty("Writer")?.GetValue(channel)
        ?? throw new InvalidOperationException("Writer não encontrado.");
    var reader = channel.GetType().GetProperty("Reader")?.GetValue(channel)
        ?? throw new InvalidOperationException("Reader não encontrado.");
    var tryComplete = writer.GetType().GetMethod("TryComplete", new[] { typeof(Exception) })
        ?? throw new MissingMethodException(writer.GetType().FullName, "TryComplete");
    var tryRead = reader.GetType().GetMethods()
        .SingleOrDefault(method => method.Name == "TryRead" && method.GetParameters().Length == 1)
        ?? throw new MissingMethodException(reader.GetType().FullName, "TryRead");

    activeStateField.SetValue(null, machine);
    var completed = (bool)(tryComplete.Invoke(writer, new object?[] { null }) ?? false);
    AssertTrue(completed, "O canal não foi concluído.");

    foreach (var stateEvent in new[] { (3, 0), (0, 0), (1, 5), (2, 4) })
    {
        handleState.Invoke(null, new object[] { stateEvent.Item1, stateEvent.Item2 });
    }

    await AssertConnectedOnceAsync(machine).ConfigureAwait(false);
    var readArguments = new object?[] { null };
    var eventWasQueued = (bool)(tryRead.Invoke(reader, readArguments) ?? false);
    AssertTrue(!eventWasQueued, "Evento tardio entrou no canal concluído.");
}

static async Task TestConcurrentBurstAsync()
{
    var machine = new ConnectionStateMachine();

    Parallel.For(0, 8, worker =>
    {
        for (var eventIndex = 0; eventIndex < 20_000; eventIndex++)
        {
            switch ((worker + eventIndex) % 7)
            {
                case 0:
                    machine.Process(0, 0);
                    break;
                case 1:
                    machine.Process(1, 2);
                    break;
                case 2:
                    machine.Process(1, 4);
                    break;
                case 3:
                    machine.Process(1, 5);
                    break;
                case 4:
                    machine.Process(2, 4);
                    break;
                case 5:
                    machine.Process(2, 6);
                    break;
                default:
                    machine.Process(3, 0);
                    break;
            }
        }
    });

    AssertTrue(machine.IsLoginReady, "Rajada não preservou login.");
    AssertTrue(machine.IsRoutingReady, "Rajada não preservou roteamento.");
    AssertTrue(machine.IsMarketDataReady, "Rajada não preservou market data.");
    AssertTrue(machine.IsActivationValid, "Rajada não preservou ativação.");
    await AssertConnectedOnceAsync(machine).ConfigureAwait(false);
}

static async Task TestMissingRoutingAsync()
{
    var machine = new ConnectionStateMachine();
    machine.Process(0, 0);
    machine.Process(2, 4);
    machine.Process(3, 0);

    var result = await machine.WaitForConnectionAsync(TimeSpan.FromMilliseconds(40)).ConfigureAwait(false);
    AssertTrue(!result.IsConnected, "Prontidão foi anunciada sem roteamento.");
    AssertContains(result.Message, "roteamento", "Timeout não indicou roteamento pendente.");
    AssertReadinessCount(machine, 0);
}

static async Task TestMissingLoginAsync()
{
    var machine = new ConnectionStateMachine();
    machine.Process(1, 2);
    machine.Process(2, 4);
    machine.Process(3, 0);

    var result = await machine.WaitForConnectionAsync(TimeSpan.FromMilliseconds(40)).ConfigureAwait(false);
    AssertTrue(!result.IsConnected, "Prontidão foi anunciada sem login.");
    AssertContains(result.Message, "login", "Timeout não indicou login pendente.");
    AssertReadinessCount(machine, 0);
}

static ConnectionStateMachine CreateReadyExceptMarket()
{
    var machine = new ConnectionStateMachine();
    machine.Process(0, 0);
    machine.Process(1, 2);
    machine.Process(3, 0);
    return machine;
}

static ConnectionStateMachine CreateReadyExceptActivation()
{
    var machine = new ConnectionStateMachine();
    machine.Process(0, 0);
    machine.Process(1, 2);
    machine.Process(2, 4);
    return machine;
}

static void ProcessReadyStates(ConnectionStateMachine machine, int routingResult = 2)
{
    machine.Process(0, 0);
    machine.Process(1, routingResult);
    machine.Process(2, 4);
    machine.Process(3, 0);
}

static async Task AssertConnectedOnceAsync(ConnectionStateMachine machine)
{
    var firstResult = await machine.WaitForConnectionAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
    AssertTrue(firstResult.IsConnected, firstResult.Message);

    for (var repetition = 0; repetition < 10; repetition++)
    {
        ProcessReadyStates(machine, routingResult: 5);
    }

    var repeatedResult = await machine.WaitForConnectionAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
    AssertTrue(repeatedResult.IsConnected, "Prontidão deixou de ser terminal.");
    AssertReadinessCount(machine, 1);
}

static void AssertReadinessCount(ConnectionStateMachine machine, int expected) =>
    AssertEqual(expected, machine.ReadinessTransitionCount, "Quantidade de anúncios de prontidão incorreta.");

static List<(int StateType, int Result)> CreateProductionSequence()
{
    var sequence = new List<(int StateType, int Result)>
    {
        (3, 0),
        (0, 0),
        (1, 1),
        (1, 2),
        (2, 1),
        (2, 2),
        (1, 2),
        (1, 4)
    };

    sequence.AddRange(Enumerable.Repeat((1, 5), 19));
    sequence.Add((1, 2));
    sequence.AddRange(Enumerable.Repeat((1, 5), 2));
    sequence.AddRange(Enumerable.Repeat((0, 0), 13));
    sequence.Add((2, 4));
    return sequence;
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertContains(string actual, string expected, string message)
{
    if (!actual.Contains(expected, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"{message} Conteúdo: {actual}");
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(actual, expected))
    {
        throw new InvalidOperationException($"{message} Esperado: {expected}; atual: {actual}.");
    }
}
