namespace DLLNelogica.Connection;

internal readonly record struct TradingAccount(int BrokerId, string AccountId, string BrokerName, string OwnerName)
{
    internal string Describe() =>
        $"Conta cadastrada | corretora={BrokerId} ({Clean(BrokerName)}) | " +
        $"conta={Clean(AccountId)} | titular={Clean(OwnerName)}";

    private static string Clean(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
}
