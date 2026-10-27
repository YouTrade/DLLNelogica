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

    // FileShare.Read exclui segundo escritor no Windows, mas no Linux/Unix não.
    // Trava sidecar com FileShare.None dá exclusividade portátil e mantém leitura do .txt.
    public Stream OpenAppend(string path) => ExclusiveAppendStream.Open(path);
}

internal sealed class ExclusiveAppendStream : Stream
{
    private readonly FileStream _data;
    private readonly FileStream _writerLock;

    private ExclusiveAppendStream(FileStream data, FileStream writerLock)
    {
        _data = data;
        _writerLock = writerLock;
    }

    internal static Stream Open(string path)
    {
        FileStream? writerLock = null;
        FileStream? data = null;
        try
        {
            writerLock = new FileStream(
                path + ".writer.lock",
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1);
            data = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, bufferSize: 1);
            return new ExclusiveAppendStream(data, writerLock);
        }
        catch
        {
            data?.Dispose();
            writerLock?.Dispose();
            throw;
        }
    }

    public override bool CanRead => _data.CanRead;
    public override bool CanSeek => _data.CanSeek;
    public override bool CanWrite => _data.CanWrite;
    public override long Length => _data.Length;
    public override long Position { get => _data.Position; set => _data.Position = value; }
    public override void Flush() => _data.Flush();
    public override int Read(byte[] buffer, int offset, int count) => _data.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _data.Seek(offset, origin);
    public override void SetLength(long value) => _data.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _data.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<byte> buffer) => _data.Write(buffer);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        _data.WriteAsync(buffer, cancellationToken);
    public override Task FlushAsync(CancellationToken cancellationToken) => _data.FlushAsync(cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _data.Dispose();
            _writerLock.Dispose();
        }

        base.Dispose(disposing);
    }
}
