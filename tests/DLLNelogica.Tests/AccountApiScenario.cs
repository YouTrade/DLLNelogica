using System.Runtime.InteropServices;
using DLLNelogica.Connection;
using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

internal sealed class AccountApiScenario
{
    internal AccountApiScenario()
    {
        Api.OnAccountCount = () => Accounts.Length;
        Api.OnAccounts = (_, _, count, destination) =>
        {
            var copied = Math.Min(count, Accounts.Length);
            for (var index = 0; index < copied; index++)
            {
                var account = Accounts[index];
                destination[index].BrokerId = account.BrokerId;
                account.AccountId.CopyTo(0, destination[index].AccountId, 0, account.AccountId.Length);
                destination[index].AccountIdLength = account.AccountId.Length;
            }

            return copied;
        };
        Api.OnAccountDetails = Details;
    }

    internal FakeProfitApi Api { get; } = new();
    internal TradingAccount[] Accounts { get; set; } =
    [
        new(15, "0012703", "Simulador", "João"),
        new(18, "7319", "TERRA", "João"),
        new(51301, "1416607", "Elliot-Warren", "João"),
        new(51401, "1416607", "Elliot-Warren", "João")
    ];
    internal int DetailsCalls { get; private set; }

    internal int Details(ref TConnectorTradingAccountOut details)
    {
        DetailsCalls++;
        Assert.Equal(1, details.Version);
        Assert.Equal(0, details.AccountId.Version);
        Assert.Equal(-1, details.AccountId.Reserved);
        Assert.Equal(string.Empty, details.AccountId.SubAccountId);
        var id = details.AccountId;
        var account = Accounts.Single(value => value.BrokerId == id.BrokerId && value.AccountId == id.AccountId);
        CopyText(account.BrokerName, details.BrokerName, ref details.BrokerNameLength);
        CopyText(account.OwnerName, details.OwnerName, ref details.OwnerNameLength);
        return 0;
    }

    private static void CopyText(string text, nint pointer, ref int length)
    {
        if (pointer != 0)
        {
            Assert.Equal(text.Length, length);
            Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);
        }

        length = text.Length;
    }
}
