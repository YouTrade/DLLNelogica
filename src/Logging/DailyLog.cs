using System.Globalization;
using System.Text;

namespace DLLNelogica;

internal sealed class DailyLog : IDisposable
{
    private readonly TextWriter _originalOutput;
    private readonly TextWriter _originalError;
    private readonly DailyLogSink _sink;
    private bool _disposed;

    private DailyLog(string binaryDirectory, string sourceName)
    {
        _originalOutput = Console.Out;
        _originalError = Console.Error;
        _sink = new DailyLogSink(binaryDirectory, sourceName, _originalError);

        Console.SetOut(new TeeTextWriter(_originalOutput, _sink));
        Console.SetError(new TeeTextWriter(_originalError, _sink));
    }

    internal string CurrentFilePath => _sink.CurrentFilePath;

    internal static DailyLog Start(string binaryDirectory, string sourceName) =>
        new(binaryDirectory, sourceName);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Console.SetOut(_originalOutput);
        Console.SetError(_originalError);
        _sink.Dispose();
    }

    // O arquivo e sempre alimentado antes do console: um console indisponivel (pipe fechado,
    // execucao sem janela) nao pode impedir o registro em disco. A falha do console continua
    // sendo propagada ao chamador exatamente como antes.
    private sealed class TeeTextWriter(TextWriter consoleWriter, DailyLogSink sink) : TextWriter
    {
        public override Encoding Encoding => consoleWriter.Encoding;

        public override void Flush()
        {
            sink.Flush();
            consoleWriter.Flush();
        }

        public override void Write(char value)
        {
            sink.Write(value.ToString(CultureInfo.InvariantCulture));
            consoleWriter.Write(value);
        }

        public override void Write(string? value)
        {
            sink.Write(value);
            consoleWriter.Write(value);
        }

        public override void Write(char[] buffer, int index, int count)
        {
            sink.Write(new string(buffer, index, count));
            consoleWriter.Write(buffer, index, count);
        }

        public override void WriteLine()
        {
            sink.Write(Environment.NewLine);
            consoleWriter.WriteLine();
        }

        public override void WriteLine(string? value)
        {
            sink.Write(string.Concat(value, Environment.NewLine));
            consoleWriter.WriteLine(value);
        }
    }

    private sealed class DailyLogSink : IDisposable
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new(false);
        private readonly object _syncRoot = new();
        private readonly string _logDirectory;
        private readonly string _sourcePrefix;
        private readonly TextWriter _fallbackError;
        private StreamWriter? _writer;
        private DateOnly _currentDate;
        private bool _atLineStart = true;
        private bool _failureReported;
        private bool _disposed;

        internal DailyLogSink(string binaryDirectory, string sourceName, TextWriter fallbackError)
        {
            // A ProfitDLL grava os próprios arquivos em "logs"; usar "log" mantém o log da
            // aplicação isolado desse conteúdo, no mesmo nível do executável.
            _logDirectory = Path.Combine(binaryDirectory, "log");
            _sourcePrefix = $"[{sourceName}]";
            _fallbackError = fallbackError;

            Directory.CreateDirectory(_logDirectory);
            OpenWriter(DateTime.Now);
        }

        internal string CurrentFilePath
        {
            get
            {
                lock (_syncRoot)
                {
                    return GetFilePath(_currentDate);
                }
            }
        }

        internal void Write(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    var timestamp = DateTime.Now;
                    EnsureWriter(timestamp);
                    WriteFormatted(timestamp, value);
                    _writer!.Flush();
                    _failureReported = false;
                }
                catch (Exception exception)
                {
                    HandleWriteFailure(exception);
                }
            }
        }

        internal void Flush()
        {
            lock (_syncRoot)
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
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
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

            // FileMode.Append no .NET nao e append atomico de SO: cada processo mantem o proprio
            // deslocamento. Permitir um segundo gravador causaria sobrescrita silenciosa do log.
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
            try
            {
                _writer?.Dispose();
            }
            catch
            {
                // A falha secundária de descarte não deve interromper a aplicação.
            }

            _writer = null;
            _atLineStart = true;

            if (_failureReported)
            {
                return;
            }

            _failureReported = true;
            ReportFailure(exception);
        }

        private void ReportFailure(Exception exception)
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

        private string GetFilePath(DateOnly date) =>
            Path.Combine(_logDirectory, $"{date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
    }
}
