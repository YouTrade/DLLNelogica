using System.Globalization;
using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Tests;

public sealed class TradeFileOutputTests
{
    [Fact]
    public async Task RestartAppendsAndExclusiveWriterRejectsSecondInstance()
    {
        using var fixture = new TradeFileFixture();
        using (var first = fixture.Output())
        {
            first.Maintain(fixture.Now, false);
            using var second = fixture.Output();
            Assert.Throws<IOException>(() => second.Maintain(fixture.Now, false));
            await first.WriteAsync(fixture.Record());
            Assert.Single(first.Maintain(fixture.Now, true));
        }

        var session = Guid.NewGuid();
        using (var restarted = fixture.Output(session: session))
        {
            restarted.Maintain(fixture.Now, false);
            var record = fixture.Record();
            await restarted.WriteAsync(record with { Raw = record.Raw with { SessionId = session } });
            Assert.Single(restarted.Maintain(fixture.Now, true));
        }

        var lines = fixture.Events();
        Assert.Equal(2, lines.Length);
        Assert.Contains(fixture.SessionId.ToString(), lines[0], StringComparison.Ordinal);
        Assert.Contains(session.ToString(), lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task MidnightAndNativeDatesUseReceptionDayEvenForBacklog()
    {
        using var fixture = new TradeFileFixture();
        using var output = fixture.Output();
        output.Maintain(fixture.Now, false);
        await output.WriteAsync(fixture.Record());
        Assert.Single(output.Maintain(fixture.Now.AddDays(1), false));
        Assert.Empty(fixture.Events(day: "20260912"));
        var old = fixture.Record();
        await output.WriteAsync(old); // Consumido depois da virada, recebido ontem.
        var native = old.Raw.Trade;
        native.TradeDate.Year = 2020;
        var current = TradeTestData.Records().Create(old.Raw with { ReceivedAt = fixture.Now.AddDays(1), Trade = native });
        await output.WriteAsync(current);
        Assert.Equal(2, output.Maintain(fixture.Now.AddDays(1), true).Count);
        Assert.Equal(2, fixture.Events().Length);
        Assert.Contains("dataNegocio=2020-", Assert.Single(fixture.Events(day: "20260912")), StringComparison.Ordinal);
        Assert.True(File.Exists(fixture.PathFor("PETR4:B", "20260912")));
    }

    [Fact]
    public async Task BatchAndElapsedTimeFlushUnderContinuousEvents()
    {
        using var fixture = new TradeFileFixture();
        using var output = fixture.Output();
        output.Maintain(fixture.Now, false);
        var record = fixture.Record();
        for (var index = 0; index < 999; index++)
        {
            await output.WriteAsync(record);
            Assert.Empty(output.Maintain(fixture.Now, false));
        }

        await output.WriteAsync(record);
        Assert.Equal(1000, output.Maintain(fixture.Now, false).Count);
        await output.WriteAsync(record);
        Assert.Single(output.Maintain(fixture.Now.AddSeconds(1), false));
        await output.WriteAsync(record);
        Assert.Single(output.Maintain(fixture.Now.AddSeconds(1), false, forceFlush: true));
        Assert.Equal(1002, fixture.Events().Length);
    }
}

public sealed class TradeSerializationTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    public async Task FilesAreSeparatedEscapedAndCultureIndependent(string culture)
    {
        using var fixture = new TradeFileFixture();
        using var output = fixture.Output();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.Empty(output.Maintain(fixture.Now, false));
            Assert.Empty(fixture.Events());
            var record = fixture.Record() with { Buyer = new(123, "Ação|a=b\r\n\\", "resolvido") };
            await output.WriteAsync(record);
            await output.WriteAsync(fixture.Record("PETR4"));
            await output.WriteAsync(fixture.Record("WINV26", "F"));
            Assert.Equal(3, output.Maintain(fixture.Now, true).Count);
            var line = Assert.Single(fixture.Events());
            Assert.Contains("preco=60.25", line, StringComparison.Ordinal);
            Assert.Contains("quantidade=2147483657", line, StringComparison.Ordinal);
            Assert.Contains("Ação\\u007Ca\\u003Db\\r\\n\\\\", line, StringComparison.Ordinal);
            Assert.Contains("sessao=" + fixture.SessionId, line, StringComparison.Ordinal);
            Assert.Single(fixture.Events("PETR4:B"));
            Assert.Single(fixture.Events("WINV26:F"));
            Assert.False(SharedReportReader.Bytes(fixture.PathFor()).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.False(File.Exists(Path.Combine(fixture.Root, "Relatorios", "20260911", "VALE3_B.txt")));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

}
