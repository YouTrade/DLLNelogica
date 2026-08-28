namespace DLLNelogica.Interop;

internal readonly record struct ProfitCallbackSet(
    TStateCallback State,
    TAccountCallback Account,
    TNewDailyCallback NewDaily,
    TProgressCallBack Progress,
    TNewTinyBookCallBack TinyBook);
