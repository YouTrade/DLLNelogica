using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace DLLNelogica.Interop;

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate void TAssetListInfoCallbackV2(TAssetID asset,
    [MarshalAs(UnmanagedType.LPWStr)] string? name, [MarshalAs(UnmanagedType.LPWStr)] string? description,
    long minimumQuantity, long maximumQuantity, long lot, int securityType, int securitySubType,
    double tick, double multiplier, [MarshalAs(UnmanagedType.LPWStr)] string? date,
    [MarshalAs(UnmanagedType.LPWStr)] string? isin, [MarshalAs(UnmanagedType.LPWStr)] string? sector,
    [MarshalAs(UnmanagedType.LPWStr)] string? subsector, [MarshalAs(UnmanagedType.LPWStr)] string? segment);

internal sealed class ProfitAssetMetadata
{
    // Delegates entregues à DLL sobrevivem inclusive a callbacks tardios após a finalização.
    private static readonly ConcurrentBag<TAssetListInfoCallbackV2> Roots = [];
    private readonly Channel<AssetTick> _events = Channel.CreateBounded<AssetTick>(64);
    private int _overflow;
    private decimal _tick;

    internal void Request(IProfitApi api, string ticker, string exchange)
    {
        TAssetListInfoCallbackV2 callback = Publish;
        Roots.Add(callback);
        if (api.SetAssetListInfoCallbackV2(callback) != 0 || api.RequestTickerInfo(ticker, exchange) != 0)
        {
            throw new InvalidOperationException("Metadados do ativo indisponíveis; envio bloqueado.");
        }
    }

    internal async Task<decimal> WaitAsync(string ticker, string exchange, CancellationToken stopping)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await foreach (var value in _events.Reader.ReadAllAsync(timeout.Token).ConfigureAwait(false))
        {
            if (Volatile.Read(ref _overflow) != 0) { throw new InvalidOperationException("Metadados incompletos."); }
            if (value.Ticker == ticker && value.Exchange == exchange)
            {
                if (!double.IsFinite(value.Tick) || value.Tick <= 0)
                {
                    throw new InvalidOperationException("Tick nativo inválido.");
                }
                _tick = (decimal)value.Tick;
                return _tick;
            }
        }
        throw new InvalidOperationException("Tick não recebido.");
    }

    internal void ValidateTick(string ticker, string exchange, decimal expected)
    {
        while (_events.Reader.TryRead(out var value))
        {
            if (value.Ticker == ticker && value.Exchange == exchange)
            {
                _tick = (decimal)value.Tick;
            }
        }
        if (Volatile.Read(ref _overflow) != 0 || _tick <= 0 || _tick != expected)
        {
            throw new InvalidOperationException("Tick nativo não confirmado ou alterado; envio bloqueado.");
        }
    }

    private void Publish(TAssetID asset, string? name, string? description, long minimumQuantity,
        long maximumQuantity, long lot, int securityType, int securitySubType, double tick,
        double multiplier, string? date, string? isin, string? sector, string? subsector, string? segment)
    {
        try
        {
            // Somente copiar/publicar; interpretação e validação acontecem no consumidor.
            if (!_events.Writer.TryWrite(new AssetTick(asset.Ticker, asset.Exchange, tick)))
            {
                Interlocked.Exchange(ref _overflow, 1);
            }
        }
        catch { Interlocked.Exchange(ref _overflow, 1); }
    }

    private readonly record struct AssetTick(string Ticker, string Exchange, double Tick);
}
