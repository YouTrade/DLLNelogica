using System.Globalization;
using System.Text;

namespace DLLNelogica.Logging;

internal sealed class ReportFileWriter : IDisposable
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private readonly string _reportsDirectory;
    private readonly string _sourcePrefix;
    private readonly TextWriter _fallbackError;

    // Um gravador por arquivo, aberto sob demanda e mantido aberto. Reabrir a cada linha
    // custaria mais que o próprio tick; e todo o acesso acontece na única thread gravadora
    // do DailyLogSink, então o dicionário dispensa sincronização.
    private readonly Dictionary<string, ReportFile> _files = new(StringComparer.OrdinalIgnoreCase);
    private string _currentDirectory = string.Empty;
    private DateOnly _currentDate;
    private bool _failureReported;

    internal ReportFileWriter(string binaryDirectory, string sourceName, TextWriter fallbackError)
    {
        _reportsDirectory = Path.Combine(binaryDirectory, "Relatorios");
        _sourcePrefix = $"[{sourceName}]";
        _fallbackError = fallbackError;

        OpenDirectory(DateTime.Now);
    }

    internal string CurrentDirectory => Volatile.Read(ref _currentDirectory);

    internal void Write(DateTime timestamp, string fileName, LogLinePrefix prefix, string value)
    {
        try
        {
            EnsureDirectory(timestamp);
            WriteFormatted(GetOrOpen(fileName), timestamp, prefix, value);
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
            foreach (var file in _files.Values)
            {
                file.Writer.Flush();
            }
        }
        catch (Exception exception)
        {
            HandleWriteFailure(exception);
        }
    }

    public void Dispose() => CloseFiles();

    internal void ReportFailure(Exception exception)
    {
        try
        {
            _fallbackError.WriteLine(
                $"Aviso: falha ao gravar o relatório do dia ({exception.GetType().Name}).");
        }
        catch
        {
            // A indisponibilidade do relatório e do console não deve encerrar a aplicação.
        }
    }

    private void EnsureDirectory(DateTime timestamp)
    {
        if (_currentDate != DateOnly.FromDateTime(timestamp))
        {
            OpenDirectory(timestamp);
        }
    }

    // A virada do dia fecha todos os arquivos: o diretório seguinte recomeça do zero, e
    // manter gravadores apontando para a pasta de ontem espalharia linhas nas duas datas.
    private void OpenDirectory(DateTime timestamp)
    {
        var date = DateOnly.FromDateTime(timestamp);
        var directory = Path.Combine(
            _reportsDirectory,
            date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);

        CloseFiles();
        _currentDate = date;
        Volatile.Write(ref _currentDirectory, directory);
    }

    private ReportFile GetOrOpen(string fileName)
    {
        if (_files.TryGetValue(fileName, out var existing))
        {
            return existing;
        }

        // FileMode.Append não é append atômico entre processos; um único gravador por
        // arquivo evita sobrescrita silenciosa do relatório.
        var stream = new FileStream(
            Path.Combine(_currentDirectory, fileName),
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read);
        var opened = new ReportFile(new StreamWriter(stream, Utf8WithoutBom) { AutoFlush = false });
        _files[fileName] = opened;
        return opened;
    }

    private void WriteFormatted(ReportFile file, DateTime timestamp, LogLinePrefix prefix, string value)
    {
        foreach (var character in value)
        {
            if (file.AtLineStart)
            {
                WritePrefix(file.Writer, timestamp, prefix);
                file.AtLineStart = false;
            }

            file.Writer.Write(character);
            if (character == '\n')
            {
                file.AtLineStart = true;
            }
        }
    }

    private void WritePrefix(StreamWriter writer, DateTime timestamp, LogLinePrefix prefix)
    {
        if (prefix == LogLinePrefix.TimeOfDay)
        {
            writer.Write(timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
            writer.Write(' ');
            return;
        }

        writer.Write(timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        writer.Write(' ');
        writer.Write(_sourcePrefix);
        writer.Write(' ');
    }

    private void HandleWriteFailure(Exception exception)
    {
        CloseFiles();

        if (_failureReported)
        {
            return;
        }

        _failureReported = true;
        ReportFailure(exception);
    }

    private void CloseFiles()
    {
        foreach (var entry in _files)
        {
            DisposeWriter(entry.Key, entry.Value.Writer);
        }

        _files.Clear();
    }

    private void DisposeWriter(string fileName, StreamWriter writer)
    {
        try
        {
            writer.Dispose();
        }
        catch (Exception exception)
        {
            _ = fileName;
            ReportFailure(exception);
        }
    }

    private sealed class ReportFile(StreamWriter writer)
    {
        internal StreamWriter Writer { get; } = writer;

        internal bool AtLineStart { get; set; } = true;
    }
}
