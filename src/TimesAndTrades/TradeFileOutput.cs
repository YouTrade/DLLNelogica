namespace DLLNelogica.TimesAndTrades;

// Um proprietário: o consumidor. Nunca descartar este objeto enquanto o worker estiver ativo.
internal sealed class TradeFileOutput(
    string baseDirectory,
    IReadOnlyDictionary<string, string> destinations,
    Guid sessionId,
    ITradeFileSystem fileSystem) : ITradeStorage
{
    internal const int BatchSize = 1000;
    private readonly TradeFileSet _files = new(baseDirectory, destinations, sessionId, fileSystem);
    private readonly List<TradeRecord> _pending = new(BatchSize);
    private bool _failed;
    private DateOnly? _date;
    private DateTimeOffset _lastFlush;

    public ValueTask WriteAsync(TradeRecord trade)
    {
        try
        {
            Write(trade);
            return ValueTask.CompletedTask;
        }
        catch
        {
            _failed = true;
            throw;
        }
    }

    private void Write(TradeRecord trade)
    {
        if (_pending.Count >= BatchSize)
        {
            throw new InvalidOperationException("O lote de negócios precisa de flush antes de receber novos eventos.");
        }

        _files.Write(trade);
        _pending.Add(trade);
    }

    public IReadOnlyList<TradeRecord> Maintain(DateTimeOffset now, bool complete, bool forceFlush = false)
    {
        try
        {
            return MaintainCore(now, complete, forceFlush);
        }
        catch
        {
            _failed = true;
            throw;
        }
    }

    private TradeRecord[] MaintainCore(DateTimeOffset now, bool complete, bool forceFlush)
    {
        var date = DateOnly.FromDateTime(now.Date);
        var rotate = _date != date;
        if (!complete && !forceFlush && !rotate && _pending.Count < BatchSize &&
            now >= _lastFlush && now - _lastFlush < TimeSpan.FromSeconds(1))
        {
            return Array.Empty<TradeRecord>();
        }

        _files.Flush();
        // Não confirmar nem repetir lote quando flush/rotação lançar exceção.
        if (rotate && !complete)
        {
            _files.Prepare(date);
            _date = date;
        }

        var confirmed = _pending.ToArray();
        _pending.Clear();
        _lastFlush = now;
        return confirmed;
    }

    public void Dispose() => _files.CloseFiles(_failed);

}
