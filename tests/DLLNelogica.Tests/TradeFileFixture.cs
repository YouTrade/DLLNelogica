using DLLNelogica.Logging;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

internal sealed class TradeFileFixture : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("DLLNelogica-files-");
    internal string Root => _directory.FullName;
    internal Guid SessionId { get; } = Guid.NewGuid();
    internal DateTimeOffset Now { get; } = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);
    internal Dictionary<string, string> Destinations { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VALE3:B"] = ReportFileNames.Trades("VALE3", "B"),
        ["PETR4:B"] = ReportFileNames.Trades("PETR4", "B"),
        ["WINV26:F"] = ReportFileNames.Trades("WINV26", "F")
    };

    internal TradeFileOutput Output(ITradeFileSystem? fileSystem = null, Guid? session = null) =>
        new(Root, Destinations, session ?? SessionId, fileSystem ?? new TradeFileSystem());

    internal TradeRecord Record(string ticker = "VALE3", string exchange = "B") => TradeTestData.Records().Create(
        TradeTestData.Create(ticker, exchange) with { SessionId = SessionId, ReceivedAt = Now });

    internal string PathFor(string instrument = "VALE3:B", string day = "20260911") =>
        Path.Combine(Root, "Relatorios", day, Destinations[instrument]);

    internal string[] Events(string instrument = "VALE3:B", string day = "20260911") =>
        SharedReportReader.Events(PathFor(instrument, day));

    internal string[] AllEvents() => Directory.GetFiles(Root, "*TimesAndTrades.txt", SearchOption.AllDirectories)
        .SelectMany(SharedReportReader.Events).ToArray();

    public void Dispose() => _directory.Delete(true);
}
