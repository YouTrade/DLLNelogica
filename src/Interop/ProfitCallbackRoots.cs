namespace DLLNelogica.Interop;

internal static class ProfitCallbackRoots
{
    /// <summary>
    /// Raiz estática obrigatória: a ProfitDLL retém este ponteiro após DLLInitializeLogin.
    /// Remover a raiz permite coleta do thunk e falha intermitente no próximo callback.
    /// </summary>
    private static readonly TStateCallback StateCallback = HandleState;

    /// <summary>
    /// Raiz estática obrigatória pelo lifetime do ponteiro nativo armazenado pela ProfitDLL.
    /// </summary>
    private static readonly TAccountCallback AccountCallback = HandleAccount;

    /// <summary>
    /// Raiz estática obrigatória pelo lifetime do ponteiro nativo armazenado pela ProfitDLL.
    /// </summary>
    private static readonly TNewDailyCallback NewDailyCallback = HandleNewDaily;

    /// <summary>
    /// Raiz estática obrigatória pelo lifetime do ponteiro nativo armazenado pela ProfitDLL.
    /// </summary>
    private static readonly TProgressCallBack ProgressCallback = HandleProgress;

    /// <summary>
    /// Raiz estática obrigatória pelo lifetime do ponteiro nativo armazenado pela ProfitDLL.
    /// </summary>
    private static readonly TNewTinyBookCallBack TinyBookCallback = HandleTinyBook;

    private static ProfitCallbackBridge? _activeBridge;

    internal static ProfitCallbackSet Callbacks { get; } = new(
        StateCallback,
        AccountCallback,
        NewDailyCallback,
        ProgressCallback,
        TinyBookCallback);

    internal static void Attach(ProfitCallbackBridge bridge)
    {
        var existing = Interlocked.CompareExchange(ref _activeBridge, bridge, null);
        if (existing is not null && !ReferenceEquals(existing, bridge))
        {
            throw new InvalidOperationException(
                "Uma segunda ponte de callbacks foi bloqueada neste processo.");
        }
    }

#pragma warning disable CA1031 // Nenhuma exceção pode atravessar um callback unmanaged -> managed.
    private static void HandleState(int stateType, int result)
    {
        var bridge = Volatile.Read(ref _activeBridge);
        try
        {
            bridge?.HandleState(stateType, result);
        }
        catch
        {
            ProfitProcessLifetime.SignalCallbackFailure();
            bridge?.SignalFailureNoThrow();
        }
    }

    private static void HandleAccount(
        int brokerId,
        string? brokerName,
        string? accountId,
        string? ownerName)
    {
        var bridge = Volatile.Read(ref _activeBridge);
        try
        {
            bridge?.HandleAccount(brokerId, brokerName, accountId, ownerName);
        }
        catch
        {
            ProfitProcessLifetime.SignalCallbackFailure();
            bridge?.SignalFailureNoThrow();
        }
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
        var bridge = Volatile.Read(ref _activeBridge);
        try
        {
            bridge?.HandleNewDaily(
                assetId,
                date,
                open,
                high,
                low,
                close,
                volume,
                adjustment,
                maxLimit,
                minLimit,
                volumeBuyer,
                volumeSeller,
                quantity,
                tradesCount,
                openContracts,
                quantityBuyer,
                quantitySeller,
                tradesBuyer,
                tradesSeller);
        }
        catch
        {
            ProfitProcessLifetime.SignalCallbackFailure();
            bridge?.SignalFailureNoThrow();
        }
    }

    private static void HandleProgress(TAssetID assetId, int progress)
    {
        var bridge = Volatile.Read(ref _activeBridge);
        try
        {
            bridge?.HandleProgress(assetId, progress);
        }
        catch
        {
            ProfitProcessLifetime.SignalCallbackFailure();
            bridge?.SignalFailureNoThrow();
        }
    }

    private static void HandleTinyBook(TAssetID assetId, double price, int quantity, int side)
    {
        var bridge = Volatile.Read(ref _activeBridge);
        try
        {
            bridge?.HandleTinyBook(assetId, price, quantity, side);
        }
        catch
        {
            ProfitProcessLifetime.SignalCallbackFailure();
            bridge?.SignalFailureNoThrow();
        }
    }
#pragma warning restore CA1031
}
