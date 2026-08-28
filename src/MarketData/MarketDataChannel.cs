using System.Threading.Channels;

namespace DLLNelogica.MarketData;

internal static class MarketDataChannel
{
    /// <summary>
    /// Cria um canal limitado cuja saturação é observável pelo produtor. O modo Wait é
    /// intencional: os callbacks devem usar somente <see cref="TryPublish{T}"/>, nunca
    /// WriteAsync, de modo que a thread nativa não espere e uma rejeição seja contabilizada.
    /// </summary>
    internal static Channel<T> Create<T>(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        return Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Publicação não bloqueante para callback nativo. Quando o canal está cheio ou fechado,
    /// o evento é rejeitado e o contador permite alarmar perda sem executar I/O no callback.
    /// </summary>
    internal static bool TryPublish<T>(
        ChannelWriter<T> writer,
        T item,
        ref long rejectedEventCount)
    {
        if (writer.TryWrite(item))
        {
            return true;
        }

        Interlocked.Increment(ref rejectedEventCount);
        return false;
    }
}
