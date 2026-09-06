namespace DLLNelogica.Configuration;

internal sealed class MarketDataOptions
{
    public int ChannelCapacity { get; init; }

    public int HistoryCapacityPerInstrument { get; init; }

    public int ReportIntervalSeconds { get; init; }

    public List<InstrumentOptions> Instruments { get; init; } = [];
}
