namespace DLLNelogica.Interop;

internal readonly record struct MarketDataCallbackRegistrationResult(
    NativeCallResult ChangeCotation,
    NativeCallResult InvalidTicker)
{
    internal bool IsSuccessful => ChangeCotation.IsSuccessful && InvalidTicker.IsSuccessful;
}
