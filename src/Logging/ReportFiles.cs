namespace DLLNelogica.Logging;

internal static class ReportFiles
{
    // O relato da sessão: conexão, assinaturas, falhas e encerramento. É o que aparece no
    // console, e o que alguém lê para entender o que aconteceu na execução.
    internal const string Session = "_Sessao.txt";

    // O relatório periódico de market data. Fica fora do relato da sessão porque é uma
    // amostragem contínua, não um acontecimento.
    internal const string Summary = "_Resumo.txt";
}
