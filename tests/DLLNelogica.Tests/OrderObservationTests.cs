using DLLNelogica.Interop;

namespace DLLNelogica.Tests;

public sealed class OrderObservationTests
{
    [Fact]
    public async Task IncompatibleReadStopsWithoutResendingAcceptedOrdersOrCancellations()
    {
        var scenario = new OrderObservationScenario
        {
            Response = _ => ((int)NResult.NL_VERSION_NOT_SUPPORTED, 0, 0)
        };
        await scenario.RunAsync();
        Assert.Equal(1, scenario.Reads);
        Assert.Equal(4, scenario.Sent);
        Assert.Equal(2, scenario.Canceled);
        Assert.Equal(TimeSpan.Zero, scenario.Elapsed);
        Assert.Equal(4, scenario.Lines.Count(line => line.Contains("executada=não confirmada", StringComparison.Ordinal)));
        Assert.Contains(scenario.Lines, line => line.Contains("saldo executado não confirmado", StringComparison.Ordinal));
        Assert.DoesNotContain(scenario.Lines, line => line.Contains("saldo executado observado nesta sequência=0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RepeatedErrorsHaveOneDiagnosticPerOrderAndFinalObservationIsBounded()
    {
        var scenario = new OrderObservationScenario();
        await scenario.RunAsync();
        Assert.Equal(TimeSpan.FromSeconds(30), scenario.Elapsed);
        Assert.Equal(4, scenario.Sent);
        Assert.Equal(2, scenario.Canceled);
        Assert.Equal(4, scenario.Lines.Count(line => line.StartsWith("Ordens: id=", StringComparison.Ordinal) &&
            line.Contains("| leitura=GetOrderDetails retornou -1", StringComparison.Ordinal) &&
            !line.Contains("| estado=", StringComparison.Ordinal)));
        Assert.Equal(5, scenario.Lines.Count(line => line.StartsWith("Ordens: espera solicitada=", StringComparison.Ordinal)));
        Assert.Single(scenario.Lines, line => line.Contains("limite de acompanhamento final", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnchangingNonFinalStateIsAlsoBoundedAndReportedOnce()
    {
        var scenario = new OrderObservationScenario { Response = _ => (0, 0, 0) };
        await scenario.RunAsync();
        Assert.Equal(TimeSpan.FromSeconds(30), scenario.Elapsed);
        Assert.Equal(4, scenario.Lines.Count(line => line.Contains("classificação=intermediário", StringComparison.Ordinal)));
        Assert.Equal(2, scenario.Canceled);
    }

    [Fact]
    public async Task DisabledSequenceProducesExactlyOneLineAndNoNativeCalls()
    {
        var scenario = new OrderObservationScenario();
        scenario.Accounts.Api.OnAccountCount = () => throw new InvalidOperationException("Não consultar.");
        await scenario.RunAsync(false);
        Assert.Equal(["Ordens: sequência desligada na configuração."], scenario.Lines);
        Assert.Equal(0, scenario.Sent);
        Assert.Equal(0, scenario.Canceled);
        Assert.Equal(0, scenario.Reads);
    }

    [Fact]
    public async Task ChangingErrorsAndRecoveryAreReported()
    {
        var scenario = new OrderObservationScenario
        {
            Response = call => call switch
            {
                <= 14 => (-1, 0, 0),
                <= 18 => (-2, 0, 0),
                _ => (0, 2, 1)
            }
        };
        await scenario.RunAsync();
        Assert.True(scenario.Elapsed < TimeSpan.FromSeconds(30));
        Assert.Contains(scenario.Lines, line => line.Contains("GetOrderDetails retornou -2", StringComparison.Ordinal));
        Assert.Equal(4, scenario.Lines.Count(line => line.Contains("classificação=final", StringComparison.Ordinal)));
        Assert.Equal(4, scenario.Sent);
        Assert.Equal(2, scenario.Canceled);
    }
}
