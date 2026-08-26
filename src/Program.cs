using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace DLLNelogica;

internal static class Program
{
    private const string ConfigurationFileName = "appsettings.json";
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(90);
    // JsonElement.Deserialize reaplica as opções do serializador ao JSON do elemento.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
    private static readonly Channel<ConnectionStateEvent> StateEvents =
        Channel.CreateUnbounded<ConnectionStateEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    private static readonly TStateCallback StateCallback = HandleState;
    private static readonly TAccountCallback AccountCallback = HandleAccount;
    private static readonly TNewDailyCallback NewDailyCallback = HandleNewDaily;
    private static readonly TProgressCallBack ProgressCallback = HandleProgress;
    private static readonly TNewTinyBookCallBack TinyBookCallback = HandleTinyBook;
    private static ConnectionStateMachine? _activeConnectionState;
    private static int _initializeLoginStarted;
    private static int _nativeInitializationAccepted;
    private static int _finalizationStarted;
    private static int _userCancellationRequested;
    private static int _consumerFailureDetected;

    private static async Task<int> Main()
    {
        ConfigureConsoleOutput();
        using var dailyLog = ConfigureDailyLog();
        ConfigureUnhandledExceptionLogging();
        ConfigureWorkingDirectory();
        using var shutdownRequested = new CancellationTokenSource();
        var connectionState = new ConnectionStateMachine();
        Volatile.Write(ref _activeConnectionState, connectionState);
        var consumerTask = ConsumeStateEventsAsync(StateEvents.Reader, shutdownRequested);
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Interlocked.Exchange(ref _userCancellationRequested, 1);

            try
            {
                shutdownRequested.Cancel();
            }
            catch
            {
                // O handler apenas sinaliza; nenhuma exceção pode escapar daqui.
            }
        };
        Console.CancelKeyPress += cancelHandler;
        var exitCode = 0;
        var connectionWasConfirmed = false;

        try
        {
            var credentials = LoadCredentials();
            Console.WriteLine("Configuração carregada e validada com sucesso.");
            Console.WriteLine($"Processo x64: {Environment.Is64BitProcess}");

            if (HasOuterWhitespace(credentials))
            {
                Console.WriteLine(
                    "Aviso: uma ou mais credenciais contêm espaços nas extremidades e serão enviadas sem alteração.");
            }

            var initialization = InitializeProfitDll(credentials);
            if (!initialization.IsAccepted)
            {
                Console.Error.WriteLine(initialization.Message);
                exitCode = 1;
            }
            else
            {
                Console.WriteLine("DLLInitializeLogin retornou NL_OK; aguardando os estados de conexão.");
                var connection = await connectionState.WaitForConnectionAsync(
                    ConnectionTimeout,
                    shutdownRequested.Token).ConfigureAwait(false);
                connectionWasConfirmed = connection.IsConnected;

                if (connectionWasConfirmed && !shutdownRequested.IsCancellationRequested)
                {
                    Console.WriteLine("Conexão confirmada pelos quatro estados obrigatórios.");
                    Console.WriteLine("Pressione Ctrl+C para encerrar.");
                    await WaitForShutdownRequestAsync(shutdownRequested.Token).ConfigureAwait(false);

                    if (Volatile.Read(ref _userCancellationRequested) != 0)
                    {
                        Console.WriteLine("Encerramento solicitado pelo usuário.");
                    }
                }
                else if (Volatile.Read(ref _userCancellationRequested) != 0)
                {
                    if (connectionWasConfirmed)
                    {
                        Console.WriteLine("Conexão confirmada pelos quatro estados obrigatórios.");
                    }

                    Console.WriteLine("Encerramento solicitado pelo usuário.");
                    exitCode = connectionWasConfirmed ? 0 : 1;
                }
                else if (!connectionWasConfirmed)
                {
                    Console.Error.WriteLine(connection.Message);
                    exitCode = 1;
                }
            }
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine("O arquivo appsettings.json não foi encontrado.");
            exitCode = 1;
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Acesso negado ao appsettings.json.");
            exitCode = 1;
        }
        catch (IOException)
        {
            Console.Error.WriteLine("Não foi possível ler o appsettings.json.");
            exitCode = 1;
        }
        catch (JsonException exception)
        {
            var line = exception.LineNumber.HasValue ? exception.LineNumber.Value + 1 : 0;
            var position = exception.BytePositionInLine.HasValue ? exception.BytePositionInLine.Value + 1 : 0;
            Console.Error.WriteLine($"JSON inválido na linha {line}, posição {position}.");
            exitCode = 1;
        }
        catch (ConfigurationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            exitCode = 1;
        }
        finally
        {
            try
            {
                FinalizeProfitDllOnce();
                StateEvents.Writer.TryComplete();
                await consumerTask.ConfigureAwait(false);

                if (Volatile.Read(ref _consumerFailureDetected) != 0)
                {
                    exitCode = 1;
                }
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }

        return exitCode;
    }

    private static void ConfigureConsoleOutput()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // A codificação padrão será mantida quando a saída não permitir alteração.
        }
    }

    private static DailyLog? ConfigureDailyLog()
    {
        try
        {
            var dailyLog = DailyLog.Start(AppContext.BaseDirectory, "DLLNelogica");
            Console.WriteLine($"Log diário inicializado em: {dailyLog.CurrentFilePath}");
            return dailyLog;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Aviso: não foi possível inicializar o log diário ({exception.GetType().Name}). " +
                "A execução continuará somente com a saída no console.");
            return null;
        }
    }

    private static void ConfigureUnhandledExceptionLogging()
    {
        // O runtime imprime a falha diretamente no descritor nativo de erro, fora de Console.Error,
        // o que deixaria o log diário sem registro do encerramento anormal.
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            try
            {
                var failure = eventArgs.ExceptionObject as Exception;
                Console.Error.WriteLine(
                    $"Falha não tratada (encerrando={eventArgs.IsTerminating}): " +
                    (failure?.ToString() ?? eventArgs.ExceptionObject?.ToString() ?? "detalhes indisponíveis"));
                Console.Error.Flush();
            }
            catch
            {
                // O registro da falha não pode gerar uma segunda exceção durante o encerramento.
            }
        };
    }

    private static void ConfigureWorkingDirectory()
    {
        try
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        }
        catch (IOException)
        {
            Console.WriteLine(
                "Aviso: não foi possível direcionar os artefatos da ProfitDLL para o diretório da aplicação.");
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine(
                "Aviso: acesso negado ao configurar o diretório de artefatos da ProfitDLL.");
        }
    }

    private static CredenciaisOptions LoadCredentials()
    {
        var configurationPath = Path.Combine(AppContext.BaseDirectory, ConfigurationFileName);
        var json = File.ReadAllText(configurationPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ConfigurationException("O arquivo appsettings.json está vazio.");
        }

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ConfigurationException("A raiz do appsettings.json deve ser um objeto JSON.");
        }

        if (!root.TryGetProperty("Credenciais", out var credentialsElement))
        {
            throw new ConfigurationException("A seção Credenciais não foi encontrada no appsettings.json.");
        }

        if (credentialsElement.ValueKind != JsonValueKind.Object)
        {
            throw new ConfigurationException("A seção Credenciais deve ser um objeto JSON.");
        }

        EnsureRequiredStringProperty(credentialsElement, "Key");
        EnsureRequiredStringProperty(credentialsElement, "User");
        EnsureRequiredStringProperty(credentialsElement, "Password");

        var credentials = credentialsElement.Deserialize<CredenciaisOptions>(SerializerOptions)
            ?? throw new ConfigurationException("A seção Credenciais não pôde ser interpretada.");

        var issues = ConfigurationValidator.GetIssues(credentials);
        if (issues.Count > 0)
        {
            throw new ConfigurationException(string.Join(' ', issues));
        }

        return credentials;
    }

    private static NativeInitializationResult InitializeProfitDll(CredenciaisOptions credentials)
    {
        if (!OperatingSystem.IsWindows())
        {
            return NativeInitializationResult.Failed(
                "A ProfitDLL somente pode ser carregada em um processo Windows.");
        }

        if (!Environment.Is64BitProcess)
        {
            return NativeInitializationResult.Failed(
                "A ProfitDLL Win64 exige que o processo seja executado em arquitetura x64.");
        }

        var dllPath = Path.Combine(AppContext.BaseDirectory, "ProfitDLL.dll");
        if (!File.Exists(dllPath))
        {
            return NativeInitializationResult.Failed(
                $"ProfitDLL.dll não foi encontrada no diretório da aplicação: {dllPath}");
        }

        if (Interlocked.CompareExchange(ref _initializeLoginStarted, 1, 0) != 0)
        {
            return NativeInitializationResult.Failed(
                "Uma segunda chamada a DLLInitializeLogin foi bloqueada neste processo.");
        }

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(ProfitFunctions).Assembly, ResolveProfitDll);
        }
        catch (InvalidOperationException)
        {
            return NativeInitializationResult.Failed(
                "Não foi possível configurar a resolução exclusiva de ProfitDLL.dll neste processo.");
        }

        try
        {
            var nativeResult = ProfitFunctions.DLLInitializeLogin(
                credentials.Key,
                credentials.User,
                credentials.Password,
                StateCallback,
                null,
                null,
                AccountCallback,
                null,
                NewDailyCallback,
                null,
                null,
                null,
                ProgressCallback,
                TinyBookCallback);

            if (nativeResult == (int)NResult.NL_OK)
            {
                Volatile.Write(ref _nativeInitializationAccepted, 1);
                return NativeInitializationResult.Accepted();
            }

            return NativeInitializationResult.Failed(
                $"DLLInitializeLogin falhou: {FormatNativeResult(nativeResult)}.");
        }
        catch (DllNotFoundException)
        {
            return File.Exists(dllPath)
                ? NativeInitializationResult.Failed(
                    "ProfitDLL.dll foi encontrada, mas não pôde ser carregada. Verifique as dependências nativas.")
                : NativeInitializationResult.Failed(
                    $"ProfitDLL.dll não foi encontrada no diretório da aplicação: {dllPath}");
        }
        catch (BadImageFormatException)
        {
            return NativeInitializationResult.Failed(
                "ProfitDLL.dll possui arquitetura incompatível ou formato de imagem inválido.");
        }
        catch (EntryPointNotFoundException)
        {
            return NativeInitializationResult.Failed(
                "A exportação DLLInitializeLogin não foi encontrada em ProfitDLL.dll.");
        }
    }

    private static void FinalizeProfitDllOnce()
    {
        if (Volatile.Read(ref _nativeInitializationAccepted) == 0 ||
            Interlocked.CompareExchange(ref _finalizationStarted, 1, 0) != 0)
        {
            return;
        }

        try
        {
            Console.WriteLine("Finalizando serviços da DLL...");
        }
        catch
        {
            // A ausência de saída não pode impedir a finalização nativa.
        }

        try
        {
            var nativeResult = ProfitFunctions.DLLFinalize();
            Console.WriteLine(
                $"DLLFinalize retornou {nativeResult} (0x{unchecked((uint)nativeResult):X8}).");
        }
        catch (Exception exception)
        {
            try
            {
                Console.Error.WriteLine(
                    $"Falha ao executar DLLFinalize ({exception.GetType().Name}).");
            }
            catch
            {
                // O fluxo de drenagem do canal deve continuar mesmo sem saída disponível.
            }
        }
    }

    private static nint ResolveProfitDll(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "ProfitDLL.dll", StringComparison.OrdinalIgnoreCase))
        {
            return nint.Zero;
        }

        var dllPath = Path.Combine(AppContext.BaseDirectory, "ProfitDLL.dll");
        return NativeLibrary.Load(dllPath);
    }

    private static string FormatNativeResult(int result)
    {
        var numericResult = $"{result} (0x{unchecked((uint)result):X8})";
        return Enum.IsDefined(typeof(NResult), result)
            ? $"{(NResult)result} — {numericResult}"
            : $"NResult desconhecido — {numericResult}";
    }

    private static bool HasOuterWhitespace(CredenciaisOptions credentials) =>
        HasOuterWhitespace(credentials.Key) ||
        HasOuterWhitespace(credentials.User) ||
        HasOuterWhitespace(credentials.Password);

    private static bool HasOuterWhitespace(string value) =>
        value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]));

    private static void EnsureRequiredStringProperty(JsonElement credentials, string propertyName)
    {
        if (!credentials.TryGetProperty(propertyName, out var property))
        {
            throw new ConfigurationException($"A propriedade Credenciais.{propertyName} não foi encontrada.");
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new ConfigurationException($"A propriedade Credenciais.{propertyName} deve ser uma string.");
        }
    }

    private static void HandleState(int stateType, int result)
    {
        try
        {
            Volatile.Read(ref _activeConnectionState)?.Process(stateType, result);
        }
        catch
        {
            // Exceções não podem atravessar a fronteira do callback nativo.
        }

        try
        {
            _ = StateEvents.Writer.TryWrite(new ConnectionStateEvent(
                DateTimeOffset.Now,
                stateType,
                result));
        }
        catch
        {
            // Exceções não podem atravessar a fronteira do callback nativo.
        }
    }

    private static void HandleAccount(int brokerId, string? brokerName, string? accountId, string? ownerName)
    {
    }

    private static void HandleNewDaily(
        TAssetID assetId,
        string? date,
        double open,
        double high,
        double low,
        double close,
        double volume,
        double adjustment,
        double maxLimit,
        double minLimit,
        double volumeBuyer,
        double volumeSeller,
        int quantity,
        int tradesCount,
        int openContracts,
        int quantityBuyer,
        int quantitySeller,
        int tradesBuyer,
        int tradesSeller)
    {
    }

    private static void HandleProgress(TAssetID assetId, int progress)
    {
    }

    private static void HandleTinyBook(TAssetID assetId, double price, int quantity, int side)
    {
    }

    private static async Task ConsumeStateEventsAsync(
        ChannelReader<ConnectionStateEvent> reader,
        CancellationTokenSource shutdownRequested)
    {
        try
        {
            await foreach (var stateEvent in reader.ReadAllAsync())
            {
                Console.WriteLine(
                    $"{stateEvent.Timestamp:O} Estado: tipo={stateEvent.StateType}, resultado={stateEvent.Result}");
                ReportMarketDataHealth(stateEvent.StateType, stateEvent.Result);
            }
        }
        catch (Exception exception)
        {
            try
            {
                Console.Error.WriteLine(
                    $"Falha no consumidor de estados ({exception.GetType().Name}); encerramento solicitado.");
            }
            catch
            {
                // A solicitação de encerramento ainda será feita se a saída de erro falhar.
            }

            try
            {
                Interlocked.Exchange(ref _consumerFailureDetected, 1);
                shutdownRequested.Cancel();
            }
            catch
            {
                // O consumidor nunca deve propagar uma segunda falha durante o encerramento.
            }
        }
    }

    private static async Task WaitForShutdownRequestAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static void ReportMarketDataHealth(int stateType, int result)
    {
        if (stateType != 2)
        {
            return;
        }

        if (result == 5)
        {
            Console.WriteLine("Aviso: market data conectado com desempenho degradado.");
        }
        else if (result == 6)
        {
            Console.WriteLine(
                "Aviso crítico: market data conectado, mas a entrega local de callbacks está parada.");
        }
    }

    private readonly record struct ConnectionStateEvent(
        DateTimeOffset Timestamp,
        int StateType,
        int Result);

    private readonly record struct NativeInitializationResult(bool IsAccepted, string Message)
    {
        internal static NativeInitializationResult Accepted() => new(true, string.Empty);

        internal static NativeInitializationResult Failed(string message) => new(false, message);
    }
}
