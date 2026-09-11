using System.Globalization;
using DLLNelogica.Interop;

namespace DLLNelogica.TimesAndTrades;

internal static class TradeLineFormatter
{
    internal static string Format(TradeRecord record)
    {
        var raw = record.Raw;
        var trade = raw.Trade;
        return FormattableString.Invariant(
            $"schema={TradeRecord.SchemaVersion} | sessao={raw.SessionId:D} | ticker={Escape(raw.Instrument.Ticker)} | bolsa={Escape(raw.Instrument.Exchange)} | feed={raw.Instrument.Feed} | recebidoEm={raw.ReceivedAt:O} | chegada={raw.ArrivalSequence} | dataNegocio={Date(record.TradeDate)} | dataNegocioRaw={RawDate(trade.TradeDate)} | tradeNumber={trade.TradeNumber} | preco={trade.Price:R} | quantidade={trade.Quantity} | volume={trade.Volume:R} | compradoraId={record.Buyer.Id} | compradoraNome={Escape(record.Buyer.Name)} | compradoraStatus={record.Buyer.Status} | vendedoraId={record.Seller.Id} | vendedoraNome={Escape(record.Seller.Name)} | vendedoraStatus={record.Seller.Status} | tipoCodigo={(byte)trade.TradeType} | tipo={record.Type} | agressor={record.Aggressor} | flags={(uint)raw.Flags} | evento={(record.IsEdit ? "edicao" : "adicao")}");
    }

    internal static string Escape(string? value) => value is null ? "NA" : value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("|", "\\u007C", StringComparison.Ordinal)
        .Replace("=", "\\u003D", StringComparison.Ordinal);

    private static string Date(DateTime? date) =>
        date?.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture) ?? "NA";

    private static string RawDate(SystemTime date) => FormattableString.Invariant(
        $"{date.Year},{date.Month},{date.DayOfWeek},{date.Day},{date.Hour},{date.Minute},{date.Second},{date.Milliseconds}");
}
