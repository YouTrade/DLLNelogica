namespace DLLNelogica.Logging;

// O console recebido aqui é o original, nunca o tee. Escrever pelo tee faria a linha
// voltar para o relato da sessão, que é exatamente o que estes destinos evitam.
internal sealed class ReportLog(DailyLogSink sink, TextWriter consoleWriter) : IReportLog
{
    public void WriteToFile(string fileName, string message) =>
        sink.WriteLine(fileName, LogLinePrefix.TimeOfDay, null, message);

    public void WriteToFileAndConsole(string fileName, string message) =>
        sink.WriteLine(fileName, LogLinePrefix.SessionStamp, consoleWriter, message);
}
