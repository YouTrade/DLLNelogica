using DLLNelogica.Connection;

namespace DLLNelogica.Interop;

internal sealed class ProfitCallbackBridge
{
    private readonly ConnectionStateEventPump _stateEvents;
    private CancellationTokenSource? _shutdownRequested;

    internal ProfitCallbackBridge(ConnectionStateEventPump stateEvents)
    {
        _stateEvents = stateEvents;
    }

    internal void AttachShutdown(CancellationTokenSource shutdownRequested) =>
        Volatile.Write(ref _shutdownRequested, shutdownRequested);

    internal void DetachShutdown(CancellationTokenSource shutdownRequested) =>
        Interlocked.CompareExchange(ref _shutdownRequested, null, shutdownRequested);

    internal void HandleState(int stateType, int result)
    {
        if (!_stateEvents.TryPublish(stateType, result) && !ProfitProcessLifetime.HasFinalizationStarted)
        {
            SignalFailureNoThrow();
        }
    }

#pragma warning disable CA1822 // Pontos de extensão de instância para o pipeline limitado da Aula 03.
    internal void HandleAccount(int brokerId, string? brokerName, string? accountId, string? ownerName)
    {
        // Aula futura: somente publicar em pipeline; callback nativo nunca executa I/O.
    }

    internal void HandleNewDaily(
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
        // Aula futura: publicar em canal limitado; callback nativo nunca executa I/O.
    }

    internal void HandleProgress(TAssetID assetId, int progress)
    {
        // Aula futura: publicar em canal limitado; callback nativo nunca executa I/O.
    }

    internal void HandleTinyBook(TAssetID assetId, double price, int quantity, int side)
    {
        // Aula futura: publicar em canal limitado; callback nativo nunca executa I/O.
    }
#pragma warning restore CA1822

    internal void SignalFailureNoThrow()
    {
        ProfitProcessLifetime.SignalCallbackFailure();

        try
        {
            Volatile.Read(ref _shutdownRequested)?.Cancel();
        }
        catch
        {
            // Esta sinalização roda na fronteira nativa e não pode propagar uma segunda falha.
        }
    }
}
