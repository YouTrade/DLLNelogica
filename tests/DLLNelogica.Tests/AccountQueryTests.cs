using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

public sealed class AccountQueryTests
{
    [Fact]
    public void ReadsFourAccountsAndDetailsWithoutAnyAccountCallback()
    {
        var scenario = new AccountApiScenario();
        var result = new ProfitAccountQuery(scenario.Api).Read(CancellationToken.None);
        Assert.Equal(scenario.Accounts, result.Accounts);
        Assert.False(result.IsIncomplete);
        Assert.Equal(8, scenario.DetailsCalls);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16385)]
    public void InvalidCountsAreRejectedBeforeAllocation(int count)
    {
        var api = new FakeProfitApi
        {
            OnAccountCount = () => count,
            OnAccounts = (_, _, _, _) => throw new Xunit.Sdk.XunitException("Não deve enumerar.")
        };
        Assert.Throws<InvalidOperationException>(() => new ProfitAccountQuery(api).Read(CancellationToken.None));
    }

    [Fact]
    public void ShrinkingListUsesOnlyReturnedEntriesAndMarksPartialRead()
    {
        var scenario = new AccountApiScenario();
        scenario.Api.OnAccountCount = () => 5;
        var result = new ProfitAccountQuery(scenario.Api).Read(CancellationToken.None);
        Assert.Equal(4, result.Accounts.Count);
        Assert.True(result.IsIncomplete);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void InvalidCopiedCountsAreRejected(int copied)
    {
        var scenario = new AccountApiScenario();
        scenario.Api.OnAccounts = (_, _, _, _) => copied;
        Assert.Throws<InvalidOperationException>(() => new ProfitAccountQuery(scenario.Api).Read(CancellationToken.None));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(32769)]
    public void InvalidTextLengthsLeaveOtherAccountsAvailable(int length)
    {
        var scenario = new AccountApiScenario();
        scenario.Api.OnAccountDetails = (ref TConnectorTradingAccountOut details) =>
        {
            if (details.AccountId.BrokerId != 15)
            {
                return scenario.Details(ref details);
            }

            details.BrokerNameLength = length;
            return 0;
        };
        var result = new ProfitAccountQuery(scenario.Api).Read(CancellationToken.None);
        Assert.True(result.IsIncomplete);
        Assert.Equal(3, result.Accounts.Count);
    }

    [Fact]
    public void ChangedLengthBetweenDetailCallsIsRetriedWithoutReadingPastBuffer()
    {
        var scenario = new AccountApiScenario();
        scenario.Api.OnAccountDetails = (ref TConnectorTradingAccountOut details) =>
        {
            if (details.AccountId.BrokerId != 15)
            {
                return scenario.Details(ref details);
            }

            details.BrokerNameLength = details.BrokerName == 0 ? 4 : 5;
            return 0;
        };
        var result = new ProfitAccountQuery(scenario.Api).Read(CancellationToken.None);
        Assert.True(result.IsIncomplete);
        Assert.Equal(3, result.Accounts.Count);
    }

    [Fact]
    public void CancellationBetweenDetailCallsStopsNativeWork()
    {
        using var stopping = new CancellationTokenSource();
        var scenario = new AccountApiScenario();
        scenario.Accounts = [scenario.Accounts[0]];
        scenario.Api.OnAccountDetails = (ref TConnectorTradingAccountOut details) =>
        {
            stopping.Cancel();
            return scenario.Details(ref details);
        };
        var result = new ProfitAccountQuery(scenario.Api).Read(stopping.Token);
        Assert.Empty(result.Accounts);
        Assert.Equal(1, scenario.DetailsCalls);
    }
    [Fact]
    public void FullLengthIdentifierDoesNotRequireTerminatingNull()
    {
        var scenario = new AccountApiScenario();
        scenario.Accounts = [scenario.Accounts[0] with { AccountId = new string('9', 100) }];
        var result = new ProfitAccountQuery(scenario.Api).Read(CancellationToken.None);
        Assert.Equal(scenario.Accounts, result.Accounts);
        Assert.False(result.IsIncomplete);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void InvalidIdentifierLengthIsNotPassedToDetails(int length)
    {
        var scenario = new AccountApiScenario();
        scenario.Api.OnAccounts = (_, _, _, destination) =>
        {
            destination[0].AccountIdLength = length;
            return 1;
        };
        var result = new ProfitAccountQuery(scenario.Api).Read(CancellationToken.None);
        Assert.Empty(result.Accounts);
        Assert.True(result.IsIncomplete);
        Assert.Equal(0, scenario.DetailsCalls);
    }

}
