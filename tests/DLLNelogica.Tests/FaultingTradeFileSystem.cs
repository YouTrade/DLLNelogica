using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

internal sealed class FaultingTradeFileSystem : ITradeFileSystem
{
    internal bool DenyDirectory { get; init; }
    internal int FailFlushAfter { get; init; } = int.MaxValue;
    internal bool PartialWrite { get; set; }
    internal List<ProbeStream> Streams { get; } = [];
    public void CreateDirectory(string path)
    {
        if (DenyDirectory)
        {
            throw new UnauthorizedAccessException("Pasta sintética indisponível.");
        }
    }

    public Stream OpenAppend(string path)
    {
        var stream = new ProbeStream(this);
        Streams.Add(stream);
        return stream;
    }

    internal sealed class ProbeStream(FaultingTradeFileSystem owner) : MemoryStream
    {
        internal int Flushes { get; private set; }
        internal int Writes { get; private set; }
        internal bool WasDisposed { get; private set; }
        public override void Flush()
        {
            Flushes++;
            if (Flushes > owner.FailFlushAfter)
            {
                throw new IOException("Flush sintético falhou.");
            }
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Writes++;
            if (owner.PartialWrite)
            {
                base.Write(buffer[..(buffer.Length / 2)]);
                throw new IOException("Escrita parcial sintética.");
            }

            base.Write(buffer);
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
