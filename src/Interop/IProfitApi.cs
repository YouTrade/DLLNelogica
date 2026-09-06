namespace DLLNelogica.Interop;

internal interface IProfitApi
{
    string LibraryPath { get; }

    void ConfigureLibraryResolver();

    int Initialize(
        string activationKey,
        string user,
        string password,
        ProfitCallbackSet callbacks);

    int SetChangeCotationCallback(TChangeCotation callback);

    int SetInvalidTickerCallback(TInvalidTickerCallback callback);

    int SubscribeTicker(string ticker, string exchange);

    int UnsubscribeTicker(string ticker, string exchange);

    int FinalizeServices();
}
