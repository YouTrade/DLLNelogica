using DLLNelogica.Connection;

namespace DLLNelogica.Interop;

internal sealed class ProfitAccountQuery(IProfitApi api)
{
    internal AccountQueryResult Read(CancellationToken stopping)
    {
        stopping.ThrowIfCancellationRequested();
        var count = api.GetAccountCount();
        if (count < 0 || count > 16_384)
        {
            throw new InvalidOperationException($"GetAccountCount retornou {count}.");
        }

        if (count == 0)
        {
            return new([], false);
        }

        var identifiers = new TConnectorAccountIdentifierOut[count];
        for (var index = 0; index < count; index++)
        {
            identifiers[index].AccountId = new char[100];
            identifiers[index].SubAccountId = new char[100];
        }

        stopping.ThrowIfCancellationRequested();
        var copied = api.GetAccounts(0, 0, count, identifiers);
        if (copied < 0 || copied > count)
        {
            throw new InvalidOperationException($"GetAccounts retornou {copied} para capacidade {count}.");
        }

        var accounts = new List<TradingAccount>(copied);
        var incomplete = copied != count;
        for (var index = 0; index < copied; index++)
        {
            stopping.ThrowIfCancellationRequested();
            try
            {
                accounts.Add(ReadDetails(identifiers[index], stopping));
            }
            catch
            {
                // Uma conta indisponível não esconde as demais; tentar novamente na próxima rodada.
                incomplete = true;
            }
        }

        return new(accounts, incomplete);
    }

    private TradingAccount ReadDetails(TConnectorAccountIdentifierOut identifier, CancellationToken stopping)
    {
        if (identifier.AccountIdLength is <= 0 or > 100 || identifier.SubAccountIdLength != 0)
        {
            throw new InvalidOperationException("Identificador de conta inválido.");
        }

        var accountId = new string(identifier.AccountId, 0, identifier.AccountIdLength).TrimEnd('\0');
        var details = new TConnectorTradingAccountOut
        {
            Version = 1,
            AccountId = new TConnectorAccountIdentifier
            {
                BrokerId = identifier.BrokerId,
                AccountId = accountId,
                SubAccountId = string.Empty,
                Reserved = -1
            }
        };
        GetDetails(ref details, stopping);
        using var broker = new AccountTextBuffer(details.BrokerNameLength);
        using var owner = new AccountTextBuffer(details.OwnerNameLength);
        using var subOwner = new AccountTextBuffer(details.SubOwnerNameLength);
        details.BrokerName = broker.Pointer;
        details.OwnerName = owner.Pointer;
        details.SubOwnerName = subOwner.Pointer;
        GetDetails(ref details, stopping);
        return new TradingAccount(identifier.BrokerId, accountId,
            broker.Read(details.BrokerNameLength), owner.Read(details.OwnerNameLength));
    }

    private void GetDetails(ref TConnectorTradingAccountOut details, CancellationToken stopping)
    {
        stopping.ThrowIfCancellationRequested();
        var result = api.GetAccountDetails(ref details);
        if (result != (int)NResult.NL_OK)
        {
            throw new InvalidOperationException($"GetAccountDetails retornou {result}.");
        }
    }
}
