namespace DLLNelogica.Application;

internal sealed class ConsoleShutdown : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private readonly ConsoleCancelEventHandler _cancelHandler;
    private int _userRequested;
    private bool _disposed;

    internal ConsoleShutdown()
    {
        _cancelHandler = HandleCancelKeyPress;
        Console.CancelKeyPress += _cancelHandler;
    }

    internal CancellationTokenSource Source => _source;

    internal bool WasRequestedByUser => Volatile.Read(ref _userRequested) != 0;

    internal async Task WaitAsync()
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, _source.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_source.IsCancellationRequested)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Console.CancelKeyPress -= _cancelHandler;
        _source.Dispose();
    }

    private void HandleCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        Interlocked.Exchange(ref _userRequested, 1);

        try
        {
            _source.Cancel();
        }
        catch
        {
            // O handler apenas sinaliza; nenhuma exceção pode escapar daqui.
        }
    }
}
