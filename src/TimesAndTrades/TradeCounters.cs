namespace DLLNelogica.TimesAndTrades;

internal readonly record struct TradeCounters
{
    internal long Callbacks { get; init; }
    internal long TranslationFailures { get; init; }
    internal long CallbackFailures { get; init; }
    internal long Uncorrelated { get; init; }
    internal long Accepted { get; init; }
    internal long RejectedFull { get; init; }
    internal long RejectedClosed { get; init; }
    internal long Delivered { get; init; }
    internal long Additions { get; init; }
    internal long Edits { get; init; }
    internal long InvalidDates { get; init; }
    internal long NameLookupFailures { get; init; }
    internal TradeRecord? LastTrade { get; init; }
    internal long Confirmed { get; init; }
    internal long ConfirmedAdditions { get; init; }
    internal long ConfirmedEdits { get; init; }
    internal long Unconfirmed => Accepted - Confirmed;
    internal long Pending => Accepted - Delivered;
}
