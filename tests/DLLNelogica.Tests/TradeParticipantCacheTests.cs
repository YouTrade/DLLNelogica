using DLLNelogica.Interop;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

public sealed class TradeParticipantCacheTests
{
    [Fact]
    public void PositiveAndNegativeLookupsAreCachedWithoutChangingIdsOrText()
    {
        var calls = 0;
        const string name = "Ação|origem=um\r\n\\";
        var cache = new TradeParticipantCache(true, (id, flags) =>
        {
            calls++;
            Assert.Equal(AgentNameFlags.ShortName, flags);
            return id == 123 ? new(true, name, name.Length, null) : new(true, null, -1, null);
        });
        Assert.Equal(new TradeParticipant(123, name, "resolvido"), cache.Resolve(123));
        Assert.Equal(new TradeParticipant(456, null, "nao_resolvido"), cache.Resolve(456));
        cache.Resolve(123);
        cache.Resolve(456);
        Assert.Equal(2, calls);
        Assert.Equal(1, cache.LookupFailures);
    }

    [Fact]
    public void DisabledNonpositiveAndOverCapacityIdsAreNotQueried()
    {
        var calls = 0;
        AgentNameLookupResult Lookup(int id, AgentNameFlags flags)
        {
            calls++;
            return new(true, "Name", 4, null);
        }

        var disabled = new TradeParticipantCache(false, Lookup);
        Assert.Equal("nao_consultado", disabled.Resolve(123).Status);
        var cache = new TradeParticipantCache(true, Lookup);
        Assert.Equal(0, cache.Resolve(0).Id);
        Assert.Equal(-1, cache.Resolve(-1).Id);
        Assert.Equal(0, calls);
        for (var id = 1; id <= TradeParticipantCache.Capacity; id++)
        {
            cache.Resolve(id);
        }

        Assert.Equal(4096, calls);
        Assert.Equal(4096, cache.Count);
        Assert.Equal("nao_consultado", cache.Resolve(4097).Status);
        Assert.Equal("resolvido", cache.Resolve(1).Status);
        Assert.Equal(4096, calls);
    }

    [Fact]
    public void EmptyNamesExceptionsAndStoppedLookupAreUnavailableWithoutThrowing()
    {
        var cache = new TradeParticipantCache(true, (id, _) => id switch
        {
            1 => new(true, "", 0, null),
            2 => throw new InvalidOperationException("Falha sintética"),
            _ => new(false, null, null, null)
        });
        Assert.Equal("nao_resolvido", cache.Resolve(1).Status);
        Assert.Equal("nao_resolvido", cache.Resolve(2).Status);
        Assert.Equal("nao_consultado", cache.Resolve(3).Status);
        Assert.Equal(2, cache.LookupFailures);
        cache.Resolve(2);
        Assert.Equal(2, cache.LookupFailures);
    }

    [Fact]
    public void FuturesKeepParticipantsAndCacheIsSharedBetweenBothSides()
    {
        var calls = 0;
        var cache = new TradeParticipantCache(true, (_, _) =>
        {
            calls++;
            return new(true, "Broker", 6, null);
        });
        var raw = TradeTestData.Create("WINV26", "F");
        var native = raw.Trade;
        native.SellAgent = native.BuyAgent;
        var record = new TradeRecordFactory(cache).Create(raw with { Trade = native });
        Assert.Equal("F", record.Raw.Instrument.Exchange);
        Assert.Equal(123, record.Buyer.Id);
        Assert.Equal(record.Buyer, record.Seller);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void CachedParticipantsRemainAvailableAfterNativeLookupStops()
    {
        var api = new FakeProfitApi
        {
            OnNameLength = (_, _) => 4,
            OnName = (_, _, buffer, _) =>
            {
                "Test".CopyTo(0, buffer, 0, 4);
                return 4;
            }
        };
        var lookup = new ProfitAgentNameLookup(api);
        lookup.Start();
        var cache = new TradeParticipantCache(true, lookup.Lookup);
        Assert.Equal("Test", cache.Resolve(1).Name);
        lookup.StopAndWait();
        Assert.Equal("Test", cache.Resolve(1).Name);
        Assert.Equal("nao_consultado", cache.Resolve(2).Status);
    }
}
