using System.Globalization;
using System.Text;

namespace DLLNelogica.Logging;

internal sealed class DailyFileWriter : IDisposable
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private readonly string _logDirectory;
    private readonly string _sourcePrefix;
    private readonly TextWriter _fallbackError;
    private StreamWriter? _writer;
    private string _currentFilePath = string.Empty;
    private DateOnly _currentDate;
    private bool _atLineStart = true;
    private bool _failureReported;

    internal DailyFileWriter(string binaryDirectory, string sourceName, TextWriter fallbackError)
    {
        _logDirectory = Path.Combine(binaryDirectory, "log");
        _sourcePrefix = $"[{sourceName}]";
        _fallbackError = fallbackError;

        Directory.CreateDirectory(_logDirectory);
        OpenWriter(DateTime.Now);
    }

    internal string CurrentFilePath => Volatile.Read(ref _currentFilePath);

    internal void Write(DateTime timestamp, string value)
    {
        try
        {
            EnsureWriter(timestamp);
            WriteFormatted(timestamp, value);
            _failureReported = false;
        }
        catch (Exception exception)
        {
            HandleWriteFailure(exception);
        }
    }

    internal void Flush()
    {
        try
        {
            _writer?.Flush();
        }
        catch (Exception exception)
        {
            HandleWriteFailure(exception);
        }
    }

    public void Dispose()
    {
        try
        {
            _writer?.Dispose();
        }
        catch (Exception exception)
        {
            ReportFailure(exception);
        }

        _writer = null;
    }

    internal void ReportFailure(Exception exception)
    {
        try
        {
            _fallbackError.WriteLine(
                $"Aviso: falha ao gravar o log diário ({exception.GetType().Name}).");
        }
        catch
        {
            // A indisponibilidade do log e do console não deve encerrar a aplicação.
        }
    }

    private void EnsureWriter(DateTime timestamp)
    {
        if (_writer is null || _currentDate != DateOnly.FromDateTime(timestamp))
        {
            OpenWriter(timestamp);
        }
    }

    private void OpenWriter(DateTime timestamp)
    {
        var date = DateOnly.FromDateTime(timestamp);
        var filePath = GetFilePath(date);
        Directory.CreateDirectory(_logDirectory);

        // FileMode.Append não é append atômico entre processos; um único gravador evita
        // sobrescrita silenciosa do arquivo diário.
        var stream = new FileStream(
            filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read);
        var newWriter = new StreamWriter(stream, Utf8WithoutBom)
        {
            AutoFlush = false
        };

        _writer?.Dispose();
        _writer = newWriter;
        _currentDate = date;
        _atLineStart = true;
        Volatile.Write(ref _currentFilePath, filePath);
    }

    private void WriteFormatted(DateTime timestamp, string value)
    {
        foreach (var character in value)
        {
            if (_atLineStart)
            {
                _writer!.Write(timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                _writer.Write(' ');
                _writer.Write(_sourcePrefix);
                _writer.Write(' ');
                _atLineStart = false;
            }

            _writer!.Write(character);
            if (character == '\n')
            {
                _atLineStart = true;
            }
        }
    }

    private void HandleWriteFailure(Exception exception)
    {
        Dispose();
        _atLineStart = true;

        if (_failureReported)
        {
            return;
        }

        _failureReported = true;
        ReportFailure(exception);
    }

    private string GetFilePath(DateOnly date) =>
        Path.Combine(_logDirectory, $"{date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
}
