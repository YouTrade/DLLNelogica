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

    int FinalizeServices();
}
