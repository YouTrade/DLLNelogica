using System.Runtime.InteropServices;

namespace DLLNelogica.Interop;

// Memória nossa: a DLL preenche os PWideChar; nunca liberamos ponteiros recebidos dela.
internal sealed class AccountTextBuffer : IDisposable
{
    internal AccountTextBuffer(int length)
    {
        if (length < 0 || length > 32_768)
        {
            throw new InvalidOperationException("Tamanho de texto de conta inválido.");
        }

        Length = length;
        Pointer = Marshal.AllocHGlobal(checked((length + 1) * sizeof(char)));
        Marshal.Copy(new char[length + 1], 0, Pointer, length + 1);
    }

    internal int Length { get; }
    internal nint Pointer { get; }

    internal string Read(int length)
    {
        if (length < 0 || length > Length)
        {
            throw new InvalidOperationException("Texto de conta mudou durante a consulta.");
        }

        return (Marshal.PtrToStringUni(Pointer, length) ?? string.Empty).TrimEnd('\0');
    }

    public void Dispose() => Marshal.FreeHGlobal(Pointer);
}
