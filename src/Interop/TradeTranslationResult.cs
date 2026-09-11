using DLLNelogica.TimesAndTrades;

namespace DLLNelogica.Interop;

internal readonly record struct TradeTranslationResult(NativeCallResult Call, RawTrade? Trade)
{
    internal bool IsSuccessful => Call.IsSuccessful && Trade.HasValue;
}
