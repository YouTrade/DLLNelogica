using DLLNelogica.Connection;

namespace DLLNelogica.Interop;

internal sealed class ProfitAccountMonitor(IProfitApi api, Action<string>? writeLine = null)
{
    private readonly object _gate = new();
    private readonly ProfitAccountQuery _query = new(api);
    private readonly AccountReport _report = new(writeLine ?? Console.WriteLine);
    private bool _ready;
    private int _stopping;

    internal bool IsStopping => Volatile.Read(ref _stopping) != 0;

    internal void Start()
    {
        lock (_gate)
        {
            _ready = true;
        }
    }

    internal async Task RunAsync(CancellationToken stopping)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            do
            {
                Refresh(stopping);
            }
            while (Volatile.Read(ref _stopping) == 0 &&
                await timer.WaitForNextTickAsync(stopping).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Encerramento normal; nenhuma linha de contas após o pedido.
        }
    }

    internal void Refresh(CancellationToken stopping)
    {
        lock (_gate)
        {
            if (!_ready || Volatile.Read(ref _stopping) != 0 || stopping.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var accounts = _query.Read(stopping);
                if (Volatile.Read(ref _stopping) == 0)
                {
                    _report.Write(accounts.Accounts, DateOnly.FromDateTime(DateTime.Now), stopping,
                        accounts.IsIncomplete);
                }
            }
            catch
            {
                // Falha acessória (inclusive exportação ausente) não interrompe os feeds.
                if (Volatile.Read(ref _stopping) == 0)
                {
                    TryReportFailure(stopping);
                }
            }
        }
    }

    internal void StopAndWait()
    {
        Interlocked.Exchange(ref _stopping, 1);
        lock (_gate)
        {
            _ready = false;
        }
    }

    private void TryReportFailure(CancellationToken stopping)
    {
        try
        {
            _report.Failure(stopping);
        }
        catch
        {
            // A saída indisponível também não pode derrubar Market Data.
        }
    }
}
