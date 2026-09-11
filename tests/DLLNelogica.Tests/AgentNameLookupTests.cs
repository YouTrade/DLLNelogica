using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

public sealed class AgentNameLookupTests
{
    [Fact]
    public void LookupIsAllowedOnlyBetweenStartAndStop()
    {
        var calls = 0;
        var api = new FakeProfitApi
        {
            OnNameLength = (agent, flags) =>
            {
                calls++;
                Assert.Equal(123, agent);
                Assert.Equal(AgentNameFlags.ShortName, flags);
                return 4;
            },
            OnName = (length, agent, buffer, flags) =>
            {
                Assert.Equal(4, length);
                Assert.Equal(5, buffer.Length);
                Assert.Equal(123, agent);
                Assert.Equal(AgentNameFlags.ShortName, flags);
                "Ação".CopyTo(0, buffer, 0, 4);
                return 4;
            }
        };
        var lookup = new ProfitAgentNameLookup(api);
        Assert.False(lookup.Lookup(123, AgentNameFlags.ShortName).WasExecuted);
        lookup.Start();
        var result = lookup.Lookup(123, AgentNameFlags.ShortName);
        Assert.True(result.IsResolved);
        Assert.Equal("Ação", result.Name);
        Assert.Equal(4, result.NativeResult);
        lookup.StopAndWait();
        lookup.Start(); // A parada é definitiva, mesmo que Start seja chamado novamente.
        Assert.False(lookup.Lookup(123, AgentNameFlags.ShortName).WasExecuted);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(-2147483636)]
    [InlineData(0)]
    [InlineData(32769)]
    public void InvalidNativeLengthDoesNotAllocateOrRequestName(int length)
    {
        var api = new FakeProfitApi
        {
            OnNameLength = (_, _) => length,
            OnName = (_, _, _, _) => throw new InvalidOperationException("Não deveria consultar nome.")
        };
        var lookup = new ProfitAgentNameLookup(api);
        lookup.Start();
        var result = lookup.Lookup(123, AgentNameFlags.ShortName);
        Assert.True(result.WasExecuted);
        Assert.False(result.IsResolved);
        Assert.Equal(length, result.NativeResult);
        Assert.Null(result.ExceptionType);
    }

    [Fact]
    public void NameErrorAndMissingExportRemainExplicitResults()
    {
        var api = new FakeProfitApi
        {
            OnNameLength = (_, _) => 4,
            OnName = (_, _, _, _) => (int)NResult.NL_NOT_FOUND
        };
        var lookup = new ProfitAgentNameLookup(api);
        lookup.Start();
        var result = lookup.Lookup(123, AgentNameFlags.ShortName);
        Assert.False(result.IsResolved);
        Assert.Equal((int)NResult.NL_NOT_FOUND, result.NativeResult);
        api.OnNameLength = (_, _) => throw new EntryPointNotFoundException();
        result = lookup.Lookup(123, AgentNameFlags.ShortName);
        Assert.False(result.IsResolved);
        Assert.Equal(nameof(EntryPointNotFoundException), result.ExceptionType);
    }

    [Fact]
    public async Task StopWaitsForWholeNativeQueryAndRejectsNewQueries()
    {
        using var lengthEntered = new ManualResetEventSlim();
        using var releaseLength = new ManualResetEventSlim();
        var nameCompleted = false;
        var api = new FakeProfitApi
        {
            OnNameLength = (_, _) =>
            {
                lengthEntered.Set();
                Assert.True(releaseLength.Wait(TimeSpan.FromSeconds(10)));
                return 4;
            },
            OnName = (_, _, buffer, _) =>
            {
                "Test".CopyTo(0, buffer, 0, 4);
                nameCompleted = true;
                return 4;
            }
        };
        var lookup = new ProfitAgentNameLookup(api);
        lookup.Start();
        var query = Task.Run(() => lookup.Lookup(123, AgentNameFlags.ShortName));
        Task? stop = null;
        try
        {
            Assert.True(lengthEntered.Wait(TimeSpan.FromSeconds(10)));
            stop = Task.Run(lookup.StopAndWait);
            Assert.True(SpinWait.SpinUntil(() => lookup.IsStopping, TimeSpan.FromSeconds(10)));
            Assert.False(stop.IsCompleted); // Consulta está retida pela barreira, não por Sleep.
        }
        finally
        {
            releaseLength.Set();
            await query;
            if (stop is not null)
            {
                await stop;
            }
        }

        Assert.True(nameCompleted);
        Assert.True((await query).IsResolved);
        Assert.False(lookup.Lookup(456, AgentNameFlags.ShortName).WasExecuted);
    }
}
