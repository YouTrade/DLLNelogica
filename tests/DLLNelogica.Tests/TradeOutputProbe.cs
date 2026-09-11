using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

internal sealed class TradeOutputProbe : ITradeOutput
{
    internal List<TradeRecord> Records { get; } = [];
    internal Func<TradeRecord, ValueTask>? BeforeWrite { get; init; }

    public async ValueTask WriteAsync(TradeRecord trade)
    {
        if (BeforeWrite is not null)
        {
            await BeforeWrite(trade);
        }

        Records.Add(trade);
    }
}
