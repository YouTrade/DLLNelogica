namespace DLLNelogica.Connection;

internal sealed class AccountReport(Action<string> writeLine)
{
    private readonly Dictionary<(int Broker, string Account), TradingAccount> _reported = [];
    private DateOnly _day;
    private bool _failed;
    private bool _empty;

    internal void Write(IReadOnlyList<TradingAccount> accounts, DateOnly day, CancellationToken stopping, bool incomplete = false)
    {
        if (_day != day)
        {
            _reported.Clear();
            _day = day;
        }

        foreach (var account in accounts)
        {
            var key = (account.BrokerId, account.AccountId);
            if (!stopping.IsCancellationRequested &&
                (!_reported.TryGetValue(key, out var previous) || previous != account))
            {
                writeLine(account.Describe());
                _reported[key] = account;
            }
        }

        if (incomplete)
        {
            Failure(stopping);
            return;
        }

        if (!stopping.IsCancellationRequested && (accounts.Count == 0 && !_empty || _failed))
        {
            writeLine(accounts.Count == 0
                ? "Consulta de contas: nenhuma conta disponível; nova tentativa em 2 segundos."
                : "Consulta de contas restabelecida.");
        }

        _empty = accounts.Count == 0;
        _failed = false;
    }

    internal void Failure(CancellationToken stopping)
    {
        if (!_failed && !stopping.IsCancellationRequested)
        {
            writeLine("Consulta de contas indisponível; nova tentativa em 2 segundos. Market Data permanece ativo.");
            _failed = true;
        }
    }
}
