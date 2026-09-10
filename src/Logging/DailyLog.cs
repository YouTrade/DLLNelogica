namespace DLLNelogica.Logging;

internal sealed class DailyLog : IDisposable
{
    private readonly TextWriter _originalOutput;
    private readonly TextWriter _originalError;
    private readonly DailyLogSink _sink;
    private readonly TeeTextWriter _redirectedOutput;
    private readonly TeeTextWriter _redirectedError;
    private bool _disposed;

    private DailyLog(string binaryDirectory, string sourceName)
    {
        _originalOutput = Console.Out;
        _originalError = Console.Error;
        _sink = new DailyLogSink(binaryDirectory, sourceName, _originalError);
        _redirectedOutput = new TeeTextWriter(_originalOutput, _sink);
        _redirectedError = new TeeTextWriter(_originalError, _sink);

        Console.SetOut(_redirectedOutput);
        Console.SetError(_redirectedError);

        // A porta recebe a saída original, não o tee: destinos nomeados não voltam para o
        // relato da sessão.
        Reports = new ReportLog(_sink, _originalOutput);
    }

    internal string CurrentDirectory => _sink.CurrentDirectory;

    internal IReportLog Reports { get; }

    internal static DailyLog Start(string binaryDirectory, string sourceName) =>
        new(binaryDirectory, sourceName);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Restaura primeiro para impedir novas entradas enquanto a fila é drenada.
        Console.SetOut(_originalOutput);
        Console.SetError(_originalError);
        _sink.Dispose();
    }
}
