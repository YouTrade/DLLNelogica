using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

public sealed class AccountConcurrencyTests
{
    [Fact]
    public async Task StopWaitsForInFlightQueryBeforeFinalizationAndSuppressesItsReport()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var calls = 0;
        var api = new FakeProfitApi
        {
            OnAccountCount = () =>
            {
                calls++;
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
                return 0;
            }
        };
        var lines = new List<string>();
        var monitor = new ProfitAccountMonitor(api, lines.Add);
        monitor.Start();
        var query = Task.Run(() => monitor.Refresh(CancellationToken.None));
        Task? finalize = null;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            finalize = Task.Run(() =>
            {
                monitor.StopAndWait();
                api.FinalizeServices();
            });
            Assert.True(SpinWait.SpinUntil(() => monitor.IsStopping, TimeSpan.FromSeconds(10)));
            Assert.False(finalize.IsCompleted);
            Assert.Equal(0, api.FinalizationCalls);
        }
        finally
        {
            release.Set();
            await query;
            if (finalize is not null)
            {
                await finalize;
            }
        }

        monitor.Start();
        monitor.Refresh(CancellationToken.None);
        Assert.Equal(1, calls);
        Assert.Equal(1, api.FinalizationCalls);
        Assert.Empty(lines);
    }

    [Fact]
    public async Task WorkerConsultsImmediatelyAndExitsOnCancellation()
    {
        using var stopping = new CancellationTokenSource();
        var api = new FakeProfitApi { OnAccountCount = () => { stopping.Cancel(); return 0; } };
        var monitor = new ProfitAccountMonitor(api, _ => Assert.Fail("Não deve registrar após cancelar."));
        monitor.Start();
        await monitor.RunAsync(stopping.Token).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(stopping.IsCancellationRequested);
    }

    [Fact]
    public async Task WorkerFindsLateAccountsWithoutNotifications()
    {
        using var stopping = new CancellationTokenSource();
        var scenario = new AccountApiScenario();
        var calls = 0;
        var lines = new List<string>();
        scenario.Api.OnAccountCount = () => ++calls == 1 ? 0 : 4;
        var monitor = new ProfitAccountMonitor(scenario.Api, line =>
        {
            lines.Add(line);
            if (line.Contains("51401", StringComparison.Ordinal))
            {
                stopping.Cancel();
            }
        });
        monitor.Start();
        await monitor.RunAsync(stopping.Token).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(2, calls);
        Assert.Equal(4, lines.Count(line => line.StartsWith("Conta cadastrada", StringComparison.Ordinal)));
    }
}
