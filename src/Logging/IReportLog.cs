namespace DLLNelogica.Logging;

// Porta de escrita nos relatórios nomeados. Quem registra não conhece o canal, o gravador
// nem o diretório do dia: depende apenas de "escreva esta linha neste arquivo".
internal interface IReportLog
{
    // Só arquivo. Usado pelo fluxo de ticks, que não pode inundar o console.
    void WriteToFile(string fileName, string message);

    // Arquivo e console, sem passar pelo relato da sessão.
    void WriteToFileAndConsole(string fileName, string message);
}
