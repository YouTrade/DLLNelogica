namespace DLLNelogica.Interop;

internal readonly record struct MarketDataCallbackRegistrationResult(
    NativeCallResult ChangeCotation,
    NativeCallResult InvalidTicker,
    NativeCallResult? TradeV2)
{
    internal bool IsSuccessful =>
        ChangeCotation.IsSuccessful && InvalidTicker.IsSuccessful &&
        (!TradeV2.HasValue || TradeV2.Value.IsSuccessful);
}
