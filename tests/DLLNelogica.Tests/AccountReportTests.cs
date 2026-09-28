using DLLNelogica.Connection;

namespace DLLNelogica.Tests;

public sealed class AccountReportTests
{
    [Fact]
    public void RepeatsOnlyWhenDetailsChangeOrDayChanges()
    {
        var lines = new List<string>();
        var report = new AccountReport(lines.Add);
        var day = new DateOnly(2026, 9, 28);
        var account = new TradingAccount(15, "01", "Simulador", "João");
        report.Write([account], day, CancellationToken.None);
        report.Write([], day, CancellationToken.None);
        report.Write([account], day, CancellationToken.None);
        Assert.Equal(2, lines.Count); // Conta mais aviso da lista vazia; sem falsa remoção.
        account = account with { OwnerName = "José\r\nSilva" };
        report.Write([account], day, CancellationToken.None);
        Assert.Equal(3, lines.Count);
        Assert.DoesNotContain('\n', lines[^1]);
        report.Write([account], day.AddDays(1), CancellationToken.None);
        Assert.Equal(4, lines.Count);
        using var stopping = new CancellationTokenSource();
        stopping.Cancel();
        report.Write([account], day.AddDays(2), stopping.Token);
        report.Failure(stopping.Token);
        Assert.Equal(4, lines.Count);
    }
}
