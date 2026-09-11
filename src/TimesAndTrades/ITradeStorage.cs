namespace DLLNelogica.TimesAndTrades;

internal interface ITradeStorage : ITradeOutput, IDisposable
{
    // Executado exclusivamente pelo consumidor, inclusive em períodos sem negócios.
    IReadOnlyList<TradeRecord> Maintain(DateTimeOffset now, bool complete, bool forceFlush = false);
}

internal interface ITradeFileSystem
{
    void CreateDirectory(string path);
    Stream OpenAppend(string path);
}

internal sealed class TradeFileSystem : ITradeFileSystem
{
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public Stream OpenAppend(string path) => new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, bufferSize: 1);
}
