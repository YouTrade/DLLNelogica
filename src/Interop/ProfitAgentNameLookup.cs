namespace DLLNelogica.Interop;

internal sealed class ProfitAgentNameLookup(IProfitApi profitApi)
{
    private const int MaximumNameLength = 32_768;
    private readonly object _gate = new();
    private bool _ready;
    private int _stopping;

    internal bool IsStopping => Volatile.Read(ref _stopping) != 0;

    internal void Start()
    {
        lock (_gate)
        {
            _ready = true;
        }
    }

    internal AgentNameLookupResult Lookup(int agentId, AgentNameFlags flags)
    {
        lock (_gate)
        {
            if (!_ready || IsStopping)
            {
                return new(false, null, null, null);
            }

            return Query(agentId, flags);
        }
    }

    internal void StopAndWait()
    {
        // Impedir novas consultas antes de esperar a consulta completa (tamanho + nome).
        Interlocked.Exchange(ref _stopping, 1);
        lock (_gate)
        {
            _ready = false;
        }
    }

    private AgentNameLookupResult Query(int agentId, AgentNameFlags flags)
    {
        try
        {
            var length = profitApi.GetAgentNameLength(agentId, flags);
            if (length <= 0 || length > MaximumNameLength)
            {
                return new(true, null, length, null);
            }

            var buffer = new char[length + 1];
            var copied = profitApi.GetAgentName(length, agentId, buffer, flags);
            var actualLength = Array.IndexOf(buffer, '\0');
            return copied > 0 && copied <= length && actualLength > 0 && actualLength <= length
                ? new(true, new string(buffer, 0, actualLength), copied, null)
                : new(true, null, copied, null);
        }
#pragma warning disable CA1031 // Consulta acessória indisponível não pode perder o negócio.
        catch (Exception exception)
        {
            return new(true, null, null, exception.GetType().Name);
        }
#pragma warning restore CA1031
    }
}
