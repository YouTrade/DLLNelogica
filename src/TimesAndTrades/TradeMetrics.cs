using System.Collections.Immutable;

namespace DLLNelogica.TimesAndTrades;

internal sealed class TradeMetrics
{
    private readonly int _capacity;
    private TradeMetricsSnapshot _state;

    internal TradeMetrics(int capacity, IEnumerable<string> instrumentKeys)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _state = new(default, instrumentKeys.ToImmutableDictionary(
            key => key, _ => default(TradeCounters), StringComparer.OrdinalIgnoreCase),
            0, 0, 0, false, false, 0, 0);
    }

    internal TradeMetricsSnapshot Snapshot => Volatile.Read(ref _state);

    // Reserva de capacidade antes de expor o evento ao consumidor. Não espera espaço nem usa lock.
    internal bool TryAccept(string key)
    {
        while (true)
        {
            var before = Snapshot;
            var accepted = !before.IsClosed && before.QueueOccupancy < _capacity;
            var next = UpdateCounters(before, key, counters => counters with
            {
                Callbacks = counters.Callbacks + 1,
                Accepted = counters.Accepted + (accepted ? 1 : 0),
                RejectedClosed = counters.RejectedClosed + (before.IsClosed ? 1 : 0),
                RejectedFull = counters.RejectedFull + (!before.IsClosed && !accepted ? 1 : 0)
            }) with
            {
                QueueOccupancy = before.QueueOccupancy + (accepted ? 1 : 0),
                PeakQueueOccupancy = Math.Max(before.PeakQueueOccupancy, before.QueueOccupancy + (accepted ? 1 : 0)),
                Publishing = before.Publishing + (accepted ? 1 : 0),
                IsIncomplete = before.IsIncomplete || !accepted
            };
            if (ReferenceEquals(Interlocked.CompareExchange(ref _state, next, before), before))
            {
                return accepted;
            }
        }
    }

    internal void FinishPublish(bool successful) => Change(state => state with
    {
        Publishing = state.Publishing - 1,
        QueueOccupancy = state.QueueOccupancy - (successful ? 0 : 1),
        PublicationFailures = state.PublicationFailures + (successful ? 0 : 1),
        IsIncomplete = state.IsIncomplete || !successful
    });

    internal void Dequeued() => Change(state => state with { QueueOccupancy = state.QueueOccupancy - 1 });

    internal void Delivered(string key, TradeRecord trade) => Change(state =>
        UpdateCounters(state, key, counters => counters with
        {
            Delivered = counters.Delivered + 1,
            LastTrade = trade,
            Additions = counters.Additions + (trade.IsEdit ? 0 : 1),
            Edits = counters.Edits + (trade.IsEdit ? 1 : 0)
        }));

    internal void Confirmed(TradeRecord trade) => Change(state =>
        UpdateCounters(state, trade.Raw.Instrument.Key, counters => counters with
        {
            Confirmed = counters.Confirmed + 1,
            ConfirmedAdditions = counters.ConfirmedAdditions + (trade.IsEdit ? 0 : 1),
            ConfirmedEdits = counters.ConfirmedEdits + (trade.IsEdit ? 1 : 0)
        }));

    internal void Interpreted(string key, TradeRecord trade, long nameFailures) => Change(state =>
        UpdateCounters(state, key, counters => counters with
        {
            InvalidDates = counters.InvalidDates + (trade.TradeDate.HasValue ? 0 : 1),
            NameLookupFailures = counters.NameLookupFailures + nameFailures
        }));

    internal void TranslationFailed(string key) => Change(state =>
        UpdateCounters(state, key, counters => counters with
        {
            Callbacks = counters.Callbacks + 1,
            TranslationFailures = counters.TranslationFailures + 1
        }) with { IsIncomplete = true });

    internal void CallbackFailed(string key) => Change(state =>
        UpdateCounters(state, key, counters => counters with
        {
            Callbacks = counters.Callbacks + 1,
            CallbackFailures = counters.CallbackFailures + 1
        }) with { IsIncomplete = true });

    internal void Uncorrelated() => Change(state => state with
    {
        Total = state.Total with
        {
            Callbacks = state.Total.Callbacks + 1,
            Uncorrelated = state.Total.Uncorrelated + 1
        }
    });

    internal void DrainTimedOut() => Change(state => state with { DrainTimedOut = true, IsIncomplete = true });

    internal void Close() => Change(state => state with { IsClosed = true });

    internal void ConsumerFailed(Exception exception) => Change(state => state with
    {
        ConsumerFailures = state.ConsumerFailures + 1,
        ConsumerFailureType = exception.GetType().Name,
        IsIncomplete = true
    });

    private void Change(Func<TradeMetricsSnapshot, TradeMetricsSnapshot> update)
    {
        while (true)
        {
            var before = Snapshot;
            if (ReferenceEquals(Interlocked.CompareExchange(ref _state, update(before), before), before))
            {
                return;
            }
        }
    }

    private static TradeMetricsSnapshot UpdateCounters(
        TradeMetricsSnapshot state, string key, Func<TradeCounters, TradeCounters> update) => state with
    {
        Total = update(state.Total),
        Instruments = state.Instruments.TryGetValue(key, out var counters)
            ? state.Instruments.SetItem(key, update(counters))
            : state.Instruments
    };
}
