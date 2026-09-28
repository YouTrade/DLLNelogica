namespace DLLNelogica.Configuration;

internal sealed class OrderSequenceOptions
{
    public bool Enabled { get; init; }
    public int BrokerId { get; init; }
    public string AccountId { get; init; } = string.Empty;
    public string RoutingPassword { get; init; } = string.Empty;
    public string Ticker { get; init; } = string.Empty;
    public string Exchange { get; init; } = string.Empty;
    public long Quantity { get; init; } = 1;
    public decimal TickSize { get; init; }
    public bool BuyMarket { get; init; } = true;
    public bool SellMarket { get; init; } = true;
    public bool BuyLimit { get; init; } = true;
    public bool SellLimit { get; init; } = true;
    public decimal BuyLimitOffset { get; init; }
    public decimal SellLimitOffset { get; init; }
    public int[] WaitSeconds { get; init; } = [5, 5, 5, 5, 5];
}
