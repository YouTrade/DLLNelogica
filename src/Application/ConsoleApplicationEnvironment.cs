using System.Text;
using DLLNelogica.Logging;

namespace DLLNelogica.Application;

internal sealed class ConsoleApplicationEnvironment : IDisposable
{
    private readonly DailyLog? _dailyLog;
    private readonly UnhandledExceptionEventHandler _unhandledExceptionHandler;
    private bool _disposed;

    private ConsoleApplicationEnvironment(DailyLog? dailyLog)
    {
        _dailyLog = dailyLog;
        _unhandledExceptionHandler = HandleUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += _unhandledExceptionHandler;
    }

    internal static ConsoleApplicationEnvironment Start()
    {
        ConfigureConsoleOutput();
        var dailyLog = ConfigureDailyLog();
        ConfigureWorkingDirectory();
        return new ConsoleApplicationEnvironment(dailyLog);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AppDomain.CurrentDomain.UnhandledException -= _unhandledExceptionHandler;
        _dailyLog?.Dispose();
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

    private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
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
    }
}
