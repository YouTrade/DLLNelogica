using System.Threading.Channels;

namespace DLLNelogica.Logging;

internal sealed class DailyLogSink : IDisposable
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SynchronousWaitTimeout = TimeSpan.FromSeconds(2);
    private readonly Channel<LogCommand> _commands =
        Channel.CreateUnbounded<LogCommand>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    private readonly DailyFileWriter _fileWriter;
    private readonly Task _writerTask;
    private int _disposed;

    internal DailyLogSink(string binaryDirectory, string sourceName, TextWriter fallbackError)
    {
        _fileWriter = new DailyFileWriter(binaryDirectory, sourceName, fallbackError);
        _writerTask = Task.Run(ProcessCommandsAsync);
    }

    internal string CurrentFilePath => _fileWriter.CurrentFilePath;

    internal void Write(TextWriter consoleWriter, string? value)
    {
        if (string.IsNullOrEmpty(value) || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _commands.Writer.TryWrite(LogCommand.Write(DateTime.Now, consoleWriter, value));
    }

    internal void Flush(TextWriter consoleWriter)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_commands.Writer.TryWrite(LogCommand.Flush(consoleWriter, completion)))
        {
            WaitWithTimeout(completion.Task);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _commands.Writer.TryComplete();

        try
        {
            WaitWithTimeout(_writerTask);
        }
        catch (Exception exception)
        {
            _fileWriter.ReportFailure(exception);
        }
    }

    private async Task ProcessCommandsAsync()
    {
        using var flushTimer = new PeriodicTimer(FlushInterval);
        var readReady = _commands.Reader.WaitToReadAsync().AsTask();
        var flushReady = flushTimer.WaitForNextTickAsync().AsTask();

        try
        {
            while (true)
            {
                await Task.WhenAny(readReady, flushReady).ConfigureAwait(false);

                if (readReady.IsCompleted)
                {
                    if (!await readReady.ConfigureAwait(false))
                    {
                        break;
                    }

                    DrainAvailableCommands();
                    _fileWriter.Flush();
                    readReady = _commands.Reader.WaitToReadAsync().AsTask();
                }

                if (flushReady.IsCompleted)
                {
                    if (!await flushReady.ConfigureAwait(false))
                    {
                        break;
                    }

                    _fileWriter.Flush();
                    flushReady = flushTimer.WaitForNextTickAsync().AsTask();
                }
            }

            DrainAvailableCommands();
            _fileWriter.Flush();
        }
        catch (Exception exception)
        {
            _fileWriter.ReportFailure(exception);
            _commands.Writer.TryComplete(exception);
            CompletePendingFlushes();
        }
        finally
        {
            _commands.Writer.TryComplete();
            _fileWriter.Dispose();
        }
    }

    private void DrainAvailableCommands()
    {
        while (_commands.Reader.TryRead(out var command))
        {
            if (command.FlushCompletion is null)
            {
                _fileWriter.Write(command.Timestamp, command.Value!);
                WriteToConsole(command.ConsoleWriter, command.Value!);
                continue;
            }

            try
            {
                _fileWriter.Flush();
                FlushConsole(command.ConsoleWriter);
            }
            finally
            {
                command.FlushCompletion.TrySetResult();
            }
        }
    }

    private void CompletePendingFlushes()
    {
        while (_commands.Reader.TryRead(out var command))
        {
            command.FlushCompletion?.TrySetResult();
        }
    }

    private static void WaitWithTimeout(Task completion)
    {
        _ = completion.Wait(SynchronousWaitTimeout);
    }

    private static void WriteToConsole(TextWriter consoleWriter, string value)
    {
        try
        {
            consoleWriter.Write(value);
        }
        catch
        {
            // Console indisponível não pode interromper o gravador em disco.
        }
    }

    private static void FlushConsole(TextWriter consoleWriter)
    {
        try
        {
            consoleWriter.Flush();
        }
        catch
        {
            // Console indisponível não pode interromper o gravador em disco.
        }
    }
}
