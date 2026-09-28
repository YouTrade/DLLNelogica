using DLLNelogica.Connection;
using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

public sealed class AccountMonitorTests
{
    [Fact]
    public void EmptyPartialAndLateListsEventuallyReportEachAccountOnce()
    {
        var scenario = new AccountApiScenario();
        var all = scenario.Accounts;
        var lines = new List<string>();
        var monitor = new ProfitAccountMonitor(scenario.Api, lines.Add);
        monitor.Refresh(CancellationToken.None);
        Assert.Empty(lines);
        monitor.Start();
        scenario.Accounts = [];
        monitor.Refresh(CancellationToken.None);
        monitor.Refresh(CancellationToken.None);
        Assert.Single(lines);
        scenario.Accounts = [all[1], all[2]];
        monitor.Refresh(CancellationToken.None);
        scenario.Accounts = all;
        monitor.Refresh(CancellationToken.None);
        monitor.Refresh(CancellationToken.None);
        Assert.Equal(4, lines.Count(line => line.StartsWith("Conta cadastrada", StringComparison.Ordinal)));
        Assert.Contains(lines, line => line.Contains("15 (Simulador)", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("conta=0012703 | titular=João", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingExportAndNativeErrorsAreRetriedWithoutStoppingFeed()
    {
        var scenario = new AccountApiScenario();
        var lines = new List<string>();
        var monitor = new ProfitAccountMonitor(scenario.Api, lines.Add);
        monitor.Start();
        scenario.Api.OnAccountCount = () => throw new EntryPointNotFoundException();
        monitor.Refresh(CancellationToken.None);
        scenario.Api.OnAccountCount = () => -1;
        monitor.Refresh(CancellationToken.None);
        Assert.Single(lines);
        Assert.Equal(0, scenario.Api.SubscribeTicker("VALE3", "B"));
        scenario.Api.OnAccountCount = () => 4;
        monitor.Refresh(CancellationToken.None);
        Assert.Equal(6, lines.Count); // Aviso, quatro contas, recuperação.
    }

    [Fact]
    public void FailedDetailIsRetriedWhileOtherAccountsAreReported()
    {
        var scenario = new AccountApiScenario();
        var lines = new List<string>();
        var monitor = new ProfitAccountMonitor(scenario.Api, lines.Add);
        monitor.Start();
        scenario.Api.OnAccountDetails = (ref TConnectorTradingAccountOut details) =>
            details.AccountId.BrokerId == 15 ? -1 : scenario.Details(ref details);
        monitor.Refresh(CancellationToken.None);
        monitor.Refresh(CancellationToken.None);
        Assert.Equal(4, lines.Count);
        scenario.Api.OnAccountDetails = scenario.Details;
        monitor.Refresh(CancellationToken.None);
        Assert.Equal(6, lines.Count);
    }

    [Fact]
    public void CancellationDuringQueryPreventsReportingAndFurtherQueries()
    {
        using var stopping = new CancellationTokenSource();
        var calls = 0;
        var api = new FakeProfitApi
        {
            OnAccountCount = () =>
            {
                calls++;
                stopping.Cancel();
                return 0;
            }
        };
        var lines = new List<string>();
        var monitor = new ProfitAccountMonitor(api, lines.Add);
        monitor.Start();
        monitor.Refresh(stopping.Token);
        monitor.Refresh(stopping.Token);
        Assert.Equal(1, calls);
        Assert.Empty(lines);
    }

    [Fact]
    public void LegacyCallbackDoesNoWorkEvenDuringConcurrentNotifications()
    {
        var callbacks = ProfitCallbackRoots.Callbacks;
        Parallel.For(0, 1000, _ => callbacks.Account(15, "Simulador", "0012703", "João"));
        // Não requer bridge, fila, consulta ou sessão inicializada.
    }
}
