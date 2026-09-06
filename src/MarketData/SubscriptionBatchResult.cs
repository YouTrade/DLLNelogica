using DLLNelogica.Interop;

namespace DLLNelogica.MarketData;

internal sealed class SubscriptionBatchResult
{
    internal SubscriptionBatchResult(
        bool isSuccessful,
        MarketInstrument? failedInstrument,
        IReadOnlyList<NativeCallResult> rollbackResults)
    {
        IsSuccessful = isSuccessful;
        FailedInstrument = failedInstrument;
        RollbackResults = rollbackResults;
    }

    internal bool IsSuccessful { get; }

    internal MarketInstrument? FailedInstrument { get; }

    internal IReadOnlyList<NativeCallResult> RollbackResults { get; }
}
