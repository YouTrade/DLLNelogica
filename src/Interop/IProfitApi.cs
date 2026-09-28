namespace DLLNelogica.Interop;

internal interface IProfitApi
{
    string LibraryPath { get; }

    void ConfigureLibraryResolver();

    int Initialize(
        string activationKey,
        string user,
        string password,
        ProfitCallbackSet callbacks);

    int SetChangeCotationCallback(TChangeCotation callback);

    int SetInvalidTickerCallback(TInvalidTickerCallback callback);

    int SetTradeCallbackV2(TConnectorTradeCallback callback);

    int TranslateTrade(nint tradePointer, ref TConnectorTrade trade);

    int GetAgentNameLength(int agentId, AgentNameFlags flags);

    int GetAgentName(int agentLength, int agentId, char[] agentName, AgentNameFlags flags);

    int SubscribeTicker(string ticker, string exchange);

    int UnsubscribeTicker(string ticker, string exchange);

    int SetAssetListInfoCallbackV2(TAssetListInfoCallbackV2 callback);
    int RequestTickerInfo(string ticker, string exchange);

    long SendOrder(ref TConnectorSendOrder order);
    int SendCancelOrderV2(ref TConnectorCancelOrder order);
    int GetOrderDetails(ref TConnectorOrderOut order);

    int GetAccountCount();

    int GetAccounts(int startSource, int startDest, int count, TConnectorAccountIdentifierOut[] accounts);

    int GetAccountDetails(ref TConnectorTradingAccountOut account);

    int FinalizeServices();
}
