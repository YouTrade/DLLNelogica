using DLLNelogica.Interop;

namespace DLLNelogica.TimesAndTrades;

// Acesso exclusivo pelo consumidor; o lookup deve ser o serviço coordenado de ProfitSession.
internal sealed class TradeParticipantCache(
    bool resolveNames,
    Func<int, AgentNameFlags, AgentNameLookupResult> lookup)
{
    internal const int Capacity = 4096;
    private readonly Dictionary<int, TradeParticipant> _participants = [];

    internal int Count => _participants.Count;
    internal long LookupFailures { get; private set; }

    internal TradeParticipant Resolve(int id)
    {
        if (!resolveNames || id <= 0)
        {
            return new(id, null, "nao_consultado");
        }

        if (_participants.TryGetValue(id, out var cached))
        {
            return cached;
        }

        if (_participants.Count >= Capacity)
        {
            return new(id, null, "nao_consultado");
        }

        var result = Query(id);
        var participant = result.IsResolved
            ? new TradeParticipant(id, result.Name, "resolvido")
            : new TradeParticipant(id, null, result.WasExecuted ? "nao_resolvido" : "nao_consultado");
        if (result.WasExecuted && !result.IsResolved)
        {
            LookupFailures++;
        }

        _participants.Add(id, participant);
        return participant;
    }

    private AgentNameLookupResult Query(int id)
    {
        try
        {
            return lookup(id, AgentNameFlags.ShortName);
        }
#pragma warning disable CA1031 // Falha de consulta de participante não pode perder o negócio.
        catch (Exception exception)
        {
            return new(true, null, null, exception.GetType().Name);
        }
#pragma warning restore CA1031
    }
}
