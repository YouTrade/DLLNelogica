using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

internal sealed class FakeProfitApi : IProfitApi
{
    public string LibraryPath => typeof(FakeProfitApi).Assembly.Location;

    internal int InitializationCalls { get; private set; }
    internal int SubscriptionCalls { get; private set; }
    internal int FinalizationCalls { get; private set; }
    internal int TradeRegistrationCalls { get; private set; }
    internal int TranslationCalls { get; private set; }
    internal int TradeRegistrationResult { get; set; }
    internal int TranslationResult { get; set; }
    internal Exception? RegistrationException { get; set; }
    internal Exception? TranslationException { get; set; }
    internal byte? RequestedVersion { get; private set; }
    internal nint LastTradePointer { get; private set; }
    internal int TranslationThread { get; private set; }
    internal TConnectorTrade Trade { get; set; }
    internal Func<string, string, int>? OnSubscribe { get; set; }
    internal List<string> Unsubscribed { get; } = [];
    internal Action? OnFinalize { get; set; }
    internal Func<int, AgentNameFlags, int>? OnNameLength { get; set; }
    internal Func<int, int, char[], AgentNameFlags, int>? OnName { get; set; }

    public void ConfigureLibraryResolver() { }

    public int Initialize(string activationKey, string user, string password, ProfitCallbackSet callbacks)
    {
        InitializationCalls++;
        return 0;
    }

    public int SetChangeCotationCallback(TChangeCotation callback) => 0;
    public int SetInvalidTickerCallback(TInvalidTickerCallback callback) => 0;

    public int SetTradeCallbackV2(TConnectorTradeCallback callback)
    {
        TradeRegistrationCalls++;
        if (RegistrationException is { } exception)
        {
            throw exception;
        }

        return TradeRegistrationResult;
    }

    public int TranslateTrade(nint tradePointer, ref TConnectorTrade trade)
    {
        TranslationCalls++;
        TranslationThread = Environment.CurrentManagedThreadId;
        LastTradePointer = tradePointer;
        RequestedVersion = trade.Version;
        if (TranslationException is { } exception)
        {
            throw exception;
        }

        trade = Trade;
        return TranslationResult;
    }

    public int GetAgentNameLength(int agentId, AgentNameFlags flags) =>
        OnNameLength?.Invoke(agentId, flags) ?? (int)NResult.NL_NOT_FOUND;

    public int GetAgentName(int agentLength, int agentId, char[] agentName, AgentNameFlags flags) =>
        OnName?.Invoke(agentLength, agentId, agentName, flags) ?? (int)NResult.NL_NOT_FOUND;

    public int SubscribeTicker(string ticker, string exchange)
    {
        SubscriptionCalls++;
        return OnSubscribe?.Invoke(ticker, exchange) ?? 0;
    }

    public int UnsubscribeTicker(string ticker, string exchange)
    {
        Unsubscribed.Add($"{ticker}:{exchange}");
        return 0;
    }

    public int FinalizeServices()
    {
        FinalizationCalls++;
        OnFinalize?.Invoke();
        return 0;
    }
}
