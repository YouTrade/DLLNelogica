using DLLNelogica.Configuration;

namespace DLLNelogica.Interop;

internal sealed class ProfitOrderGateway(IProfitApi api, Func<string, double?> latestPrice)
{
    internal ProfitOrderGateway(IProfitApi api, ProfitCallbackBridge bridge) : this(api, bridge.LatestPrice) { }

    private readonly object _gate = new();
    private readonly ProfitAssetMetadata _metadata = new();
    private bool _ready;
    private bool _orderVersionRejected;
    internal bool IsStopping => Volatile.Read(ref _stopping) != 0;
    private int _stopping;

    internal void Start() { lock (_gate) { _ready = true; } }
    internal void StopAndWait()
    {
        Interlocked.Exchange(ref _stopping, 1);
        lock (_gate) { _ready = false; }
    }

    internal async Task<decimal> PrepareAsync(OrderSequenceOptions options, CancellationToken stopping)
    {
        lock (_gate)
        {
            EnsureReady(stopping);
            _metadata.Request(api, options.Ticker, options.Exchange);
        }
        return await _metadata.WaitAsync(options.Ticker, options.Exchange, stopping).ConfigureAwait(false);
    }

    internal double? LatestPrice(string key) => latestPrice(key);

    internal bool AccountExists(OrderSequenceOptions options, CancellationToken stopping)
    {
        lock (_gate)
        {
            EnsureReady(stopping);
            return new ProfitAccountQuery(api).Read(stopping).Accounts.Any(account =>
                account.BrokerId == options.BrokerId && account.AccountId == options.AccountId);
        }
    }

    internal long Send(OrderSequenceOptions options, byte side, bool limit, double price, CancellationToken stopping)
    {
        lock (_gate)
        {
            EnsureReady(stopping);
            if (!AccountExists(options, stopping))
            {
                throw new InvalidOperationException("Conta não encontrada na DLL.");
            }
            _metadata.ValidateTick(options.Ticker, options.Exchange, options.TickSize);
            var order = new TConnectorSendOrder
            {
                Version = 1, AccountId = Account(options),
                AssetId = new TConnectorAssetIdentifier { Ticker = options.Ticker, Exchange = options.Exchange },
                Password = options.RoutingPassword, OrderSide = side, OrderType = limit ? (byte)2 : (byte)1,
                Price = limit ? price : -1, StopPrice = -1, Quantity = options.Quantity
            };
            EnsureReady(stopping);
            try { return api.SendOrder(ref order); }
            catch { throw new InvalidOperationException("Falha nativa no envio; resultado não confirmado."); }
        }
    }

    internal int Cancel(OrderSequenceOptions options, long id, CancellationToken stopping)
    {
        lock (_gate)
        {
            EnsureReady(stopping);
            var order = new TConnectorCancelOrder
            {
                AccountId = Account(options), OrderId = Identifier(id), Password = options.RoutingPassword
            };
            try { return api.SendCancelOrderV2(ref order); }
            catch { throw new InvalidOperationException("Falha nativa no cancelamento; resultado não confirmado."); }
        }
    }

    internal (int Result, byte Status, long Filled) Read(long id, CancellationToken stopping)
    {
        lock (_gate)
        {
            EnsureReady(stopping);
            if (_orderVersionRejected)
            {
                return ((int)NResult.NL_VERSION_NOT_SUPPORTED, 0, 0);
            }
            // 4.0.0.41 recusa Version > 0 na entrada de GetOrderDetails, mesmo tendo
            // uma rotina interna V1. V0 já contém estado/quantidade; V1 acrescenta EventId.
            // As versões de OrderId, AccountId e AssetId são independentes e ficam em 0.
            // Só lemos escalares: textos com ponteiros/capacidades zero não são copiados.
            var order = new TConnectorOrderOut
            {
                Version = 0,
                OrderId = new TConnectorOrderIdentifier { Version = 0, LocalOrderId = id, ClOrderId = null },
                AccountId = new TConnectorAccountIdentifierOut
                {
                    Version = 0, AccountId = new char[100], SubAccountId = new char[100]
                }
            };
            var result = api.GetOrderDetails(ref order);
            _orderVersionRejected = result == (int)NResult.NL_VERSION_NOT_SUPPORTED;
            return (result, order.OrderStatus, order.TradedQuantity);
        }
    }

    private void EnsureReady(CancellationToken stopping)
    {
        stopping.ThrowIfCancellationRequested();
        if (!_ready || Volatile.Read(ref _stopping) != 0) { throw new OperationCanceledException(); }
    }

    private static TConnectorAccountIdentifier Account(OrderSequenceOptions options) => new()
    {
        BrokerId = options.BrokerId, AccountId = options.AccountId, SubAccountId = string.Empty, Reserved = -1
    };
    private static TConnectorOrderIdentifier Identifier(long id) => new() { LocalOrderId = id, ClOrderId = string.Empty };
}
