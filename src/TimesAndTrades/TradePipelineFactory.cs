using DLLNelogica.Configuration;
using DLLNelogica.Interop;
using DLLNelogica.Logging;

namespace DLLNelogica.TimesAndTrades;

internal sealed class TradePipelineFactory(
    ProfitCallbackBridge bridge,
    ProfitSession session,
    IReportLog reports,
    string baseDirectory)
{
    internal TradeRuntimePipeline? Start(ApplicationOptions options, CancellationTokenSource shutdown)
    {
        if (!options.TimesAndTrades.Enabled)
        {
            return null;
        }

        var destinations = options.MarketData.Instruments.ToDictionary(
            instrument => $"{instrument.Ticker}:{instrument.Exchange}",
            instrument => ReportFileNames.Trades(instrument.Ticker, instrument.Exchange),
            StringComparer.OrdinalIgnoreCase);
        var storage = new TradeFileOutput(baseDirectory, destinations, bridge.TradeSessionId, new TradeFileSystem());
        var records = TradeRecordFactory.ForSession(options.TimesAndTrades.ResolveAgentNames, session);
        var reporter = new TradeReporter(reports);
        var pump = new TradeEventPump(options.TimesAndTrades.ChannelCapacity, destinations.Keys,
            records, storage, shutdown, observed: reporter.Observe);
        return new TradeRuntimePipeline(bridge, pump, reporter, options.MarketData.ReportIntervalSeconds, TimeProvider.System);
    }
}
