using DLLNelogica.Connection;

namespace DLLNelogica.Interop;

internal readonly record struct AccountQueryResult(IReadOnlyList<TradingAccount> Accounts, bool IsIncomplete);
