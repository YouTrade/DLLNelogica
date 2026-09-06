using DLLNelogica.Configuration;
using DLLNelogica.Interop;

namespace DLLNelogica.MarketData;

internal sealed class MarketDataSubscriptionManager
{
    private readonly object _sync = new();
    private readonly ProfitSession _profitSession;
    private readonly List<MarketInstrument> _activeSubscriptions = [];
    private readonly HashSet<string> _acceptedSubscriptionKeys = new(StringComparer.OrdinalIgnoreCase);

    internal MarketDataSubscriptionManager(ProfitSession profitSession)
    {
        _profitSession = profitSession;
    }

    internal SubscriptionBatchResult SubscribeAll(IEnumerable<InstrumentOptions> configuredInstruments)
    {
        foreach (var options in configuredInstruments)
        {
            var instrument = MarketInstrument.FromOptions(options);
            NativeCallResult result;
            lock (_sync)
            {
                result = _profitSession.Subscribe(instrument.Ticker, instrument.Exchange);
                if (result.IsSuccessful)
                {
                    _activeSubscriptions.Add(instrument);
                    _acceptedSubscriptionKeys.Add(instrument.Key);
                }
            }

            ReportResult(result);
            if (!result.IsSuccessful)
            {
                return new SubscriptionBatchResult(false, instrument, UnsubscribeAll());
            }
        }

        return new SubscriptionBatchResult(true, null, Array.Empty<NativeCallResult>());
    }

    internal IReadOnlyList<NativeCallResult> UnsubscribeAll()
    {
        List<MarketInstrument> subscriptions;
        lock (_sync)
        {
            subscriptions = [.. _activeSubscriptions];
        }

        subscriptions.Reverse();
        var results = new List<NativeCallResult>(subscriptions.Count);
        foreach (var instrument in subscriptions)
        {
            NativeCallResult result;
            lock (_sync)
            {
                result = _profitSession.Unsubscribe(instrument.Ticker, instrument.Exchange);
                _activeSubscriptions.Remove(instrument);
            }

            results.Add(result);
            ReportResult(result);
        }

        return results.AsReadOnly();
    }

    internal bool IsCorrelatedWithAcceptedSubscription(MarketInstrument instrument)
    {
        lock (_sync)
        {
            // O registro é deliberadamente append-only: callbacks já enfileirados continuam
            // correlacionáveis depois que a lista operacional é esvaziada no encerramento.
            return _acceptedSubscriptionKeys.Contains(instrument.Key);
        }
    }

    private static void ReportResult(NativeCallResult result)
    {
        try
        {
            if (result.IsSuccessful)
            {
                Console.WriteLine(result.Message);
            }
            else
            {
                Console.Error.WriteLine(result.Message);
            }
        }
        catch
        {
            // A falha de saída não pode interromper assinatura, rollback ou desassinatura.
        }
    }
}
