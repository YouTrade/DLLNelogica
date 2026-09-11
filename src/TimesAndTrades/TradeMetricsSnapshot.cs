using System.Collections.Immutable;

namespace DLLNelogica.TimesAndTrades;

// Toda leitura vê um único estado imutável; não mistura gerações de contadores.
internal sealed record TradeMetricsSnapshot(
    TradeCounters Total,
    ImmutableDictionary<string, TradeCounters> Instruments,
    int QueueOccupancy,
    int PeakQueueOccupancy,
    int Publishing,
    bool IsClosed,
    bool IsIncomplete,
    long ConsumerFailures,
    long PublicationFailures)
{
    internal bool DrainTimedOut { get; init; }

    internal string? ConsumerFailureType { get; init; }

    internal bool CanCompleteWriter => IsClosed && Publishing == 0;
}
