namespace DLLNelogica.Orders;

internal sealed class SequenceOrder(long id, byte side, bool limit)
{
    internal long Id { get; } = id;
    internal byte Side { get; } = side;
    internal bool Limit { get; } = limit;
    internal byte? Status { get; set; }
    internal long Filled { get; set; }
    internal string? ReadFailure { get; set; }
    internal bool ReadDisabled { get; set; }
    internal string? LastCancellationReport { get; set; }
    internal bool CancelRequested { get; set; }
    internal bool CancelAccepted { get; set; }
    internal OrderStatusInfo StatusInfo => OrderStatusInfo.From(Status);
    internal bool IsFinal => StatusInfo.Classification == OrderStatusClassification.Final;
}
