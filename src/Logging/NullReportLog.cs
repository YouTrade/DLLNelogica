namespace DLLNelogica.Logging;

// Usado quando o relatório do dia não pôde ser inicializado. A ausência de arquivo nunca
// derruba a execução, então as escritas em disco viram no-op — mas o que era destinado ao
// console continua aparecendo, porque é o que resta para acompanhar a sessão.
internal sealed class NullReportLog : IReportLog
{
    internal static readonly NullReportLog Instance = new();

    private NullReportLog()
    {
    }

    public void WriteToFile(string fileName, string message)
    {
        _ = fileName;
        _ = message;
    }

    public void WriteToFileAndConsole(string fileName, string message)
    {
        _ = fileName;

        try
        {
            Console.WriteLine(message);
        }
        catch
        {
            // A indisponibilidade do console não pode interromper quem registra.
        }
    }
}
