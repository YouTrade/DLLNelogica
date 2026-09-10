namespace DLLNelogica.MarketData;

// Último preço e contagem de ticks por instrumento, entre dois relatórios. O consumidor de
// cotações grava e o relator lê em outra thread, então o acesso é sincronizado — mas o
// bloqueio cobre apenas duas atribuições, nunca I/O.
internal sealed class MarketDataSnapshot
{
    private readonly object _sync = new();
    private readonly Dictionary<string, InstrumentState> _instruments =
        new(StringComparer.OrdinalIgnoreCase);

    internal void Record(RawPriceChange priceChange)
    {
        lock (_sync)
        {
            if (!_instruments.TryGetValue(priceChange.Instrument.Key, out var state))
            {
                state = new InstrumentState();
                _instruments[priceChange.Instrument.Key] = state;
            }

            state.Price = priceChange.Price;
            state.IntervalTicks++;
        }
    }

    // Devolve a amostra e zera a contagem: cada relatório fala do seu próprio intervalo, não
    // do acumulado. O total da execução já é responsabilidade de MarketDataMetrics.
    internal IReadOnlyList<InstrumentSnapshot> TakeInterval()
    {
        lock (_sync)
        {
            var snapshots = new List<InstrumentSnapshot>(_instruments.Count);
            foreach (var entry in _instruments)
            {
                snapshots.Add(new InstrumentSnapshot(entry.Key, entry.Value.Price, entry.Value.IntervalTicks));
                entry.Value.IntervalTicks = 0;
            }

            return snapshots;
        }
    }

    private sealed class InstrumentState
    {
        internal double Price { get; set; }

        internal long IntervalTicks { get; set; }
    }
}
