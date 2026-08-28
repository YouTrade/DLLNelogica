# Histórico de evolução

Este arquivo registra os marcos didáticos relevantes do projeto. Ele não substitui o
`README.md`, que documenta o estado atual, nem o histórico de commits, que preserva as
alterações técnicas em detalhe.

Os marcos seguem a sequência das aulas, não versionamento semântico. Trabalhos estruturais
entre aulas recebem um nome próprio e não avançam artificialmente a numeração da série.

## [Retrofit pós-Aula 02] - 2026-08-28

Este marco reorganiza e endurece a base construída nas duas primeiras aulas. Ele **não é a
Aula 03** e não implementa consumo de Market Data.

### Adicionado

- Composition root com injeção de dependências manual.
- Fronteiras explícitas para aplicação, configuração, conexão, interoperabilidade e logging.
- `ProfitCallbackRoots`, com os cinco delegates nativos enraizados até o fim do processo.
- `ProfitProcessLifetime`, documentando as guardas e invariantes válidas por processo.
- Pipeline assíncrono para relatório diário, com gravador dedicado e ordenação FIFO.
- Governança automática com `.editorconfig`, `Directory.Build.props` e métricas CA1502,
  CA1505 e CA1506.

### Alterado

- `Program.cs` passou de 583 para 28 linhas e ficou restrito à composição da aplicação.
- Namespaces passaram a acompanhar a estrutura de diretórios.
- Leitura e validação das credenciais foram extraídas para `JsonCredentialsLoader`.
- A API nativa passou a ser acessada por `IProfitApi`, `ProfitNativeApi` e `ProfitSession`.
- Estados recebidos por callback passaram a ser processados fora da thread nativa.
- O evento causador é colocado no relatório antes da transição que libera a confirmação da
  conexão.
- Logging passou a fazer flush por lote, com timer de segurança e limite de dois segundos
  para esperas síncronas.
- Prontidão histórica de Market Data e saúde corrente passaram a ser conceitos distintos.

### Corrigido

- Falhas de console não interrompem mais a máquina de estados.
- Nenhuma exceção gerenciada pode atravessar a fronteira de callback nativo.
- A ordem de encerramento ficou explícita: `DLLFinalize`, conclusão do canal e drenagem do
  consumidor.
- O relatório não executa I/O na thread dos callbacks nativos.

### Removido

- A suíte de 24 testes determinísticos, por decisão explícita do mantenedor.
- `InternalsVisibleTo` e os resíduos da solution relacionados ao projeto de testes.
- O canal antecipado de Market Data que ainda não possuía produtor nem consumidor real.

### Verificado

- Build estrito com zero avisos e zero erros.
- Regras IDE0130, CA1502, CA1505 e CA1506 comprovadas por builds negativos temporários.
- Conexão real ponta a ponta: quatro estados obrigatórios confirmados, encerramento por
  `Ctrl+C`, `DLLFinalize` retornando zero e processo encerrando com código zero.
- Credenciais, logs e artefatos dessa validação foram descartados após a execução.

## [Aula 02] - 2026-08-26

### Adicionado

- Relatório diário em `log/AAAAMMDD.log`, separado dos arquivos produzidos pela ProfitDLL.
- Rotação automática por data e gravação em UTF-8 sem BOM.
- Redirecionamento de `stdout` e `stderr` para console e arquivo.
- Registro de falhas não tratadas com tipo, mensagem e stack trace.
- Instrumentação do ciclo completo: inicialização, estados, conexão, `Ctrl+C` e finalização.
- `.gitignore` para saídas de build, logs, credenciais locais e artefatos nativos.

### Alterado

- O README passou a identificar formalmente a série e a Aula 02.
- O fluxo de conexão ganhou evidências persistentes antes da entrada de Market Data prevista
  para a aula seguinte.

### Estado daquele marco

- O relatório ainda fazia flush síncrono em cada escrita.
- A aplicação ainda concentrava composição, configuração, callbacks e ciclo de vida em
  `Program.cs`.
- A suíte determinística de 24 cenários ainda fazia parte da solution.

## [Aula 01] - 2026-08-24

### Adicionado

- Solution e aplicação console em .NET 9, compiladas exclusivamente para x64.
- Modelo de credenciais em JSON, leitura e validação de campos obrigatórios.
- Carregamento explícito da `ProfitDLL.dll` Win64 pelo diretório da aplicação.
- Contratos P/Invoke para `DLLInitializeLogin` e `DLLFinalize`.
- Delegates e tipos necessários para a fronteira nativa.
- Máquina de estados que aguarda login, roteamento, Market Data e ativação em qualquer ordem.
- Travamento dos estados obrigatórios após a primeira confirmação válida.
- Timeout de conexão, tratamento de falhas terminais de login e encerramento com `Ctrl+C`.
- Finalização controlada da ProfitDLL e drenagem dos eventos antes da saída.
- Suíte console com 24 cenários determinísticos da máquina de estados, sem acesso à rede ou
  à biblioteca nativa.

### Descobertas registradas

- `NL_OK` confirma que a inicialização foi aceita, não que os quatro serviços conectaram.
- Roteamento pode confirmar pelos resultados 2 ou 5.
- Estados podem chegar fora de ordem e oscilar durante a inicialização.
- Uma nova inicialização após `DLLFinalize` no mesmo processo não completa todos os estados;
  a reconexão exige outro processo.

[Retrofit pós-Aula 02]: https://github.com/YouTrade/DLLNelogica/compare/aula-02...aula-02-retrofit
[Aula 02]: https://github.com/YouTrade/DLLNelogica/compare/aula-01...aula-02
[Aula 01]: https://github.com/YouTrade/DLLNelogica/releases/tag/aula-01
