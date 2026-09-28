namespace DLLNelogica.Interop;

internal sealed class ProfitNativeApi : IProfitApi
{
    internal ProfitNativeApi(string baseDirectory)
    {
        LibraryPath = Path.Combine(baseDirectory, "ProfitDLL.dll");
    }

    public string LibraryPath { get; }

    public void ConfigureLibraryResolver() =>
        new ProfitLibraryResolver(LibraryPath).Configure();

    public int Initialize(
        string activationKey,
        string user,
        string password,
        ProfitCallbackSet callbacks) =>
        ProfitFunctions.DLLInitializeLogin(
            activationKey,
            user,
            password,
            callbacks.State,
            null,
            null,
            callbacks.Account,
            null,
            callbacks.NewDaily,
            null,
            null,
            null,
            callbacks.Progress,
            callbacks.TinyBook);

    public int SetAssetListInfoCallbackV2(TAssetListInfoCallbackV2 callback) => ProfitFunctions.SetAssetListInfoCallbackV2(callback);
    public int RequestTickerInfo(string ticker, string exchange) => ProfitFunctions.RequestTickerInfo(ticker, exchange);

    public long SendOrder(ref TConnectorSendOrder order) => ProfitFunctions.SendOrder(ref order);
    public int SendCancelOrderV2(ref TConnectorCancelOrder order) => ProfitFunctions.SendCancelOrderV2(ref order);
    public int GetOrderDetails(ref TConnectorOrderOut order) => ProfitFunctions.GetOrderDetails(ref order);

    public int GetAccountCount() => ProfitFunctions.GetAccountCount();

    public int GetAccounts(int startSource, int startDest, int count, TConnectorAccountIdentifierOut[] accounts) =>
        ProfitFunctions.GetAccounts(startSource, startDest, count, accounts);

    public int GetAccountDetails(ref TConnectorTradingAccountOut account) =>
        ProfitFunctions.GetAccountDetails(ref account);

    public int FinalizeServices() => ProfitFunctions.DLLFinalize();

    public int SetChangeCotationCallback(TChangeCotation callback) =>
        ProfitFunctions.SetChangeCotationCallback(callback);

    public int SetInvalidTickerCallback(TInvalidTickerCallback callback) =>
        ProfitFunctions.SetInvalidTickerCallback(callback);

    public int SetTradeCallbackV2(TConnectorTradeCallback callback) =>
        ProfitFunctions.SetTradeCallbackV2(callback);

    public int TranslateTrade(nint tradePointer, ref TConnectorTrade trade) =>
        ProfitFunctions.TranslateTrade(tradePointer, ref trade);

    public int GetAgentNameLength(int agentId, AgentNameFlags flags) =>
        ProfitFunctions.GetAgentNameLength(agentId, flags);

    public int GetAgentName(int agentLength, int agentId, char[] agentName, AgentNameFlags flags) =>
        ProfitFunctions.GetAgentName(agentLength, agentId, agentName, flags);

    public int SubscribeTicker(string ticker, string exchange) =>
        ProfitFunctions.SubscribeTicker(ticker, exchange);

    public int UnsubscribeTicker(string ticker, string exchange) =>
        ProfitFunctions.UnsubscribeTicker(ticker, exchange);

}
