using DLLNelogica.Configuration;
using DLLNelogica.Interop;

namespace DLLNelogica.Application;

internal static class MarketDataSessionStartup
{
    internal static bool TryStart(ProfitSession session, ApplicationOptions options)
    {
        if (options.TimesAndTrades.Enabled && !session.CanReceiveTrades)
        {
            Report(
                "Times and Trades habilitado sem pipeline conectado; inicialização bloqueada.",
                true);
            return false;
        }

        var initialization = session.Initialize(options.Credenciais);
        if (!initialization.IsAccepted)
        {
            Report(initialization.Message, true);
            return false;
        }

        Report("DLLInitializeLogin retornou NL_OK; registrando callbacks de Market Data.");
        var registration = session.RegisterMarketDataCallbacks(options.TimesAndTrades.Enabled);
        Report(registration.ChangeCotation.Message, !registration.ChangeCotation.IsSuccessful);
        Report(registration.InvalidTicker.Message, !registration.InvalidTicker.IsSuccessful);
        if (registration.TradeV2 is { } tradeV2)
        {
            Report(tradeV2.Message, !tradeV2.IsSuccessful);
        }

        return registration.IsSuccessful;
    }

    private static void Report(string message, bool isError = false)
    {
        try
        {
            var output = isError ? Console.Error : Console.Out;
            output.WriteLine(message);
        }
#pragma warning disable CA1031 // Falha de saída não pode alterar o resultado da inicialização.
        catch (Exception)
        {
        }
#pragma warning restore CA1031
    }
}
