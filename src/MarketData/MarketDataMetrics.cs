namespace DLLNelogica.MarketData;

internal sealed class MarketDataMetrics
{
    private long _receivedPriceChanges;
    private long _processedPriceChanges;
    private long _droppedPriceChanges;
    private long _invalidPriceCount;
    private long _invalidTimestampCount;
    private long _invalidTickerCount;

    internal long ReceivedPriceChanges => Volatile.Read(ref _receivedPriceChanges);

    internal long ProcessedPriceChanges => Volatile.Read(ref _processedPriceChanges);

    internal long DroppedPriceChanges => Volatile.Read(ref _droppedPriceChanges);

    internal long InvalidPriceCount => Volatile.Read(ref _invalidPriceCount);

    internal long InvalidTimestampCount => Volatile.Read(ref _invalidTimestampCount);

    internal long InvalidTickerCount => Volatile.Read(ref _invalidTickerCount);

    internal void IncrementReceivedPriceChanges() => Interlocked.Increment(ref _receivedPriceChanges);

    internal void IncrementProcessedPriceChanges() => Interlocked.Increment(ref _processedPriceChanges);

    internal void IncrementDroppedPriceChanges() => Interlocked.Increment(ref _droppedPriceChanges);

    internal void IncrementInvalidPriceCount() => Interlocked.Increment(ref _invalidPriceCount);

    internal void IncrementInvalidTimestampCount() => Interlocked.Increment(ref _invalidTimestampCount);

    internal void IncrementInvalidTickerCount() => Interlocked.Increment(ref _invalidTickerCount);
}
