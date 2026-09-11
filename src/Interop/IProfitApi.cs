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

    int FinalizeServices();
}
