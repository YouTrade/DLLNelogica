using System.Globalization;
using System.Text;

namespace DLLNelogica.TimesAndTrades;

internal sealed class TradeFileSet(
    string baseDirectory,
    IReadOnlyDictionary<string, string> destinations,
    Guid sessionId,
    ITradeFileSystem fileSystem)
{
    private static readonly UTF8Encoding Encoding = new(false);
    private readonly Dictionary<string, StreamWriter> _files = new(StringComparer.OrdinalIgnoreCase);
    private DateOnly? _date;

    internal void Prepare(DateOnly date)
    {
        CloseFiles(false);
        foreach (var key in destinations.Keys)
        {
            _files.Add(key, Open(date, key));
        }

        Flush();
        _date = date;
    }

    internal void Flush()
    {
        foreach (var writer in _files.Values)
        {
            writer.Flush();
        }
    }

    internal void Write(TradeRecord trade)
    {
        var key = trade.Raw.Instrument.Key;
        var date = DateOnly.FromDateTime(trade.Raw.ReceivedAt.Date);
        if (_date == date)
        {
            _files[key].WriteLine(TradeLineFormatter.Format(trade));
            return;
        }

        var historical = Open(date, key);
        try
        {
            historical.WriteLine(TradeLineFormatter.Format(trade));
            historical.Dispose();
        }
        catch
        {
            historical.BaseStream.Dispose();
            throw;
        }
    }

    private StreamWriter Open(DateOnly date, string key)
    {
        var directory = Path.Combine(baseDirectory, "Relatorios", date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        fileSystem.CreateDirectory(directory);
        var stream = fileSystem.OpenAppend(Path.Combine(directory, destinations[key]));
        StreamWriter? writer = null;
        try
        {
            writer = new StreamWriter(stream, Encoding);
            writer.WriteLine(FormattableString.Invariant(
                $"# schema=1 | sessao={sessionId:D} | instrumento={TradeLineFormatter.Escape(key)}"));
            return writer;
        }
        catch
        {
            stream.Dispose(); // Não repetir flush de um StreamWriter que acabou de falhar.

            throw;
        }
    }

    internal void CloseFiles(bool abandon)
    {
        Exception? failure = null;
        foreach (var writer in _files.Values)
        {
            try
            {
                if (abandon)
                {
                    writer.BaseStream.Dispose(); // Abandonar buffer ambíguo, sem repetir escrita.
                }
                else
                {
                    writer.Dispose();
                }
            }
#pragma warning disable CA1031 // Tentar fechar todos os destinos, reportando a primeira falha.
            catch (Exception exception)
            {
                failure ??= exception;
            }
#pragma warning restore CA1031
        }

        _files.Clear();
        if (failure is not null)
        {
            throw new IOException("Falha ao fechar os arquivos de Times and Trades.", failure);
        }
    }
}
