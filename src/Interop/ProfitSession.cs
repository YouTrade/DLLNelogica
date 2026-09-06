using DLLNelogica.Configuration;

namespace DLLNelogica.Interop;

internal sealed class ProfitSession
{
    private readonly IProfitApi _profitApi;
    private readonly ProfitCallbackBridge _callbackBridge;

    internal ProfitSession(IProfitApi profitApi, ProfitCallbackBridge callbackBridge)
    {
        _profitApi = profitApi;
        _callbackBridge = callbackBridge;
    }

    internal ProfitInitializationResult Initialize(CredentialsOptions credentials)
    {
        if (!OperatingSystem.IsWindows())
        {
            return ProfitInitializationResult.Failed(
                "A ProfitDLL somente pode ser carregada em um processo Windows.");
        }

        if (!Environment.Is64BitProcess)
        {
            return ProfitInitializationResult.Failed(
                "A ProfitDLL Win64 exige que o processo seja executado em arquitetura x64.");
        }

        if (!File.Exists(_profitApi.LibraryPath))
        {
            return ProfitInitializationResult.Failed(
                $"ProfitDLL.dll não foi encontrada no diretório da aplicação: {_profitApi.LibraryPath}");
        }

        if (!ProfitProcessLifetime.TryBeginInitialization())
        {
            return ProfitInitializationResult.Failed(
                "Uma segunda chamada a DLLInitializeLogin foi bloqueada neste processo.");
        }

        ProfitCallbackRoots.Attach(_callbackBridge);

        try
        {
            _profitApi.ConfigureLibraryResolver();
        }
        catch (InvalidOperationException)
        {
            return ProfitInitializationResult.Failed(
                "Não foi possível configurar a resolução exclusiva de ProfitDLL.dll neste processo.");
        }

        try
        {
            var nativeResult = _profitApi.Initialize(
                credentials.Key,
                credentials.User,
                credentials.Password,
                ProfitCallbackRoots.Callbacks);

            if (nativeResult == (int)NResult.NL_OK)
            {
                ProfitProcessLifetime.MarkInitializationAccepted();
                return ProfitInitializationResult.Accepted();
            }

            return ProfitInitializationResult.Failed(
                $"DLLInitializeLogin falhou: {FormatNativeResult(nativeResult)}.");
        }
        catch (DllNotFoundException)
        {
            return File.Exists(_profitApi.LibraryPath)
                ? ProfitInitializationResult.Failed(
                    "ProfitDLL.dll foi encontrada, mas não pôde ser carregada. Verifique as dependências nativas.")
                : ProfitInitializationResult.Failed(
                    $"ProfitDLL.dll não foi encontrada no diretório da aplicação: {_profitApi.LibraryPath}");
        }
        catch (BadImageFormatException)
        {
            return ProfitInitializationResult.Failed(
                "ProfitDLL.dll possui arquitetura incompatível ou formato de imagem inválido.");
        }
        catch (EntryPointNotFoundException)
        {
            return ProfitInitializationResult.Failed(
                "A exportação DLLInitializeLogin não foi encontrada em ProfitDLL.dll.");
        }
    }

    internal ProfitFinalizationResult FinalizeOnce()
    {
        if (!ProfitProcessLifetime.TryBeginFinalization())
        {
            return ProfitFinalizationResult.NotRequired();
        }

        try
        {
            return ProfitFinalizationResult.Completed(_profitApi.FinalizeServices());
        }
        catch (Exception exception)
        {
            return ProfitFinalizationResult.Failed(exception);
        }
    }

    internal MarketDataCallbackRegistrationResult RegisterMarketDataCallbacks()
    {
        var changeCotation = ExecuteNativeCall(
            "SetChangeCotationCallback",
            () => _profitApi.SetChangeCotationCallback(ProfitCallbackRoots.Callbacks.ChangeCotation));
        var invalidTicker = ExecuteNativeCall(
            "SetInvalidTickerCallback",
            () => _profitApi.SetInvalidTickerCallback(ProfitCallbackRoots.Callbacks.InvalidTicker));
        return new MarketDataCallbackRegistrationResult(changeCotation, invalidTicker);
    }

    internal NativeCallResult Subscribe(string ticker, string exchange) =>
        ExecuteNativeCall(
            $"SubscribeTicker({ticker}:{exchange})",
            () => _profitApi.SubscribeTicker(ticker, exchange));

    internal NativeCallResult Unsubscribe(string ticker, string exchange) =>
        ExecuteNativeCall(
            $"UnsubscribeTicker({ticker}:{exchange})",
            () => _profitApi.UnsubscribeTicker(ticker, exchange));

    private static string FormatNativeResult(int result)
    {
        var numericResult = $"{result} (0x{unchecked((uint)result):X8})";
        return Enum.IsDefined(typeof(NResult), result)
            ? $"{(NResult)result} — {numericResult}"
            : $"NResult desconhecido — {numericResult}";
    }

#pragma warning disable CA1031 // Toda falha nativa precisa virar resultado para permitir DLLFinalize.
    private static NativeCallResult ExecuteNativeCall(string operation, Func<int> call)
    {
        try
        {
            return NativeCallResult.Completed(operation, call());
        }
        catch (Exception exception)
        {
            return NativeCallResult.Failed(operation, exception);
        }
    }
#pragma warning restore CA1031
}
