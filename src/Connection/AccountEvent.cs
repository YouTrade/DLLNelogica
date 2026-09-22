namespace DLLNelogica.Connection;

// Conta de roteamento anunciada pela DLL via TAccountCallback, uma vez por conta, logo após o
// login. Chega na thread nativa e só vira linha de log depois de atravessar o canal do pump.
internal readonly record struct AccountEvent(
    DateTimeOffset Timestamp,
    int BrokerId,
    string? BrokerName,
    string? AccountId,
    string? OwnerName)
{
    internal string Describe() =>
        $"Conta cadastrada | corretora={BrokerId} ({BrokerName ?? "?"}) | " +
        $"conta={AccountId ?? "?"} | titular={OwnerName ?? "?"}";
}
