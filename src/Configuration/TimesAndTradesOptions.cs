namespace DLLNelogica.Configuration;

internal sealed class TimesAndTradesOptions
{
    public bool Enabled { get; init; }

    public int ChannelCapacity { get; init; } = 16_384;

    public bool ResolveAgentNames { get; init; } = true;
}
