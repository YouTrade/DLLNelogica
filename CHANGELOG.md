# Histórico de evolução

Este arquivo registra os marcos didáticos relevantes do projeto. Ele não substitui o
`README.md`, que documenta o estado atual, nem o histórico de commits, que preserva as
alterações técnicas em detalhe.

Os marcos seguem a sequência das aulas, não versionamento semântico. Trabalhos estruturais
entre aulas recebem um nome próprio e não avançam artificialmente a numeração da série.

## [Retrofit pós-Aula 03] - 2026-09-10

A primeira execução em pregão aberto entregou 1.325 cotações em 55 segundos, contra as quatro
do teste com mercado fechado. O registro em arquivo único deixou de servir, e a aplicação não
tinha nenhuma visão do mercado enquanto rodava. Este marco reorganiza a observabilidade em
torno desse volume. Ele **não é a Aula 04** e não altera o consumo de Market Data em si.

### Adicionado

- Diretório por dia em `Relatorios/AAAAMMDD/`, com rotação automática na virada.
- Um arquivo por instrumento, nomeado `<TICKER>_<BOLSA>.txt`, com uma linha por cotação.
- `_Sessao.txt` para o relato da execução e `_Resumo.txt` para a amostragem periódica.
- Relatório periódico de market data, finalmente governado por `ReportIntervalSeconds`: último
  preço e contagem de negócios de cada instrumento no intervalo.
- `IReportLog`, porta de escrita em destinos nomeados, com `NullReportLog` para quando o
  relatório do dia não pôde ser inicializado.
- `ReportFileWriter`, que mantém um gravador por arquivo aberto sob demanda.
- `MarketDataSnapshot` e `MarketDataReporter`, responsáveis pela amostra de cada intervalo.
- Higienização do ticker antes de virar nome de arquivo.
- Carimbo de chegada ao lado do `pwcDate` da bolsa em cada linha de tick.

### Alterado

- `DailyFileWriter` deu lugar a `ReportFileWriter`: de um arquivo diário para vários destinos
  dentro do diretório do dia.
- O relato da sessão passou de `log/AAAAMMDD.log` para `Relatorios/AAAAMMDD/_Sessao.txt`.
- O carimbo de linha passou a depender do destino: data, hora e origem no relato; apenas hora
  com milissegundos nos arquivos de tick.
- `MarketDataRuntimePipeline` passou a receber `MarketDataOptions` inteiro e a ser
  `IDisposable`.

### Removido

- `MarketData.HistoryCapacityPerInstrument` do `appsettings.json` e do código. Era exigido e
  validado desde a Aula 03 sem que nada o consumisse: configuração que promete o que o
  programa não entrega é pior que configuração ausente.

### Descobertas registradas

- A diferença entre o carimbo de chegada e o `pwcDate` da bolsa expõe a latência real do
  caminho até a aplicação: entre 100 e 200 ms de forma consistente.
- A sequência de chegada é global entre instrumentos, então cruzar os arquivos por esse número
  reconstrói a ordem real dos eventos.
- Um relatório periódico preso ao token de encerramento travaria a drenagem no caminho de
  falha, onde esse token nunca é cancelado. O relator precisa da própria parada.
- `LoginResult=MaxHID` na ProfitDLL significa "todos os seus logins estão em uso". O callback
  de estado devolve apenas o resultado 200; o motivo só aparece no `LogDesktop` da própria DLL.

### Verificado

- Build estrito com zero avisos e zero erros.
- Execução em pregão aberto com quatro instrumentos: 1.325 cotações recebidas, zero descartadas
  e zero tickers inválidos.
- As linhas gravadas nos arquivos de instrumento — 1.208, 92, 20 e 5 — somam exatamente as
  1.325 cotações contabilizadas, comprovando que a gravação distribuída não perde eventos.
- Volume medido: 138 KB em 55 segundos, o equivalente a mais de 70 MB por pregão. Não há
  política de retenção, por decisão explícita do mantenedor.
- Credenciais, relatórios e artefatos dessa validação foram descartados após a execução.

## [Aula 03] - 2026-09-06

Primeiro consumo real de Market Data: assinatura de instrumentos, recebimento de cotações e
desassinatura ordenada no encerramento.

### Adicionado

- Assinatura e desassinatura de instrumentos por `SubscribeTicker` e `UnsubscribeTicker`.
- Callbacks `TChangeCotation` e `TInvalidTickerCallback`, registrados por
  `SetChangeCotationCallback` e `SetInvalidTickerCallback`.
- Seção `MarketData` no `appsettings.json`: capacidade do canal, capacidade de histórico por
  instrumento, intervalo de relatório e a lista de instrumentos.
- `MarketDataSubscriptionManager`, com assinatura tudo ou nada, rollback na primeira recusa e
  desassinatura em ordem reversa.
- `MarketPriceEventPump`, com canal limitado para cotações e canal de controle separado para
  tickers inválidos.
- `MarketDataMetrics`, contabilizando cotações recebidas, cotações descartadas e tickers
  inválidos.
- `MarketDataRuntimePipeline` e `MarketDataPipelineFactory`, que iniciam e drenam os três
  consumidores do ciclo de Market Data.
- `NativeCallResult`, uniformizando no relatório o resultado de cada chamada nativa.
- Registro da primeira cotação de cada instrumento, com data nativa, sequência e preço.

### Alterado

- `JsonCredentialsLoader` deu lugar a `JsonConfigurationLoader`, que valida a forma do JSON
  antes da desserialização e passou a cobrir também a seção `MarketData`.
- `ApplicationRunner` ficou restrito à leitura e validação da configuração; o ciclo de Market
  Data passou para `MarketDataApplication` e `MarketDataSessionCoordinator`.
- Um ticker inválido correlacionado a uma assinatura aceita passou a ser falha terminal, com
  encerramento solicitado.
- Os estados de conexão passaram a ser relatados por mensagens descritivas, no lugar dos pares
  numéricos `tipo` e `resultado`.

### Corrigido

- A primeira cotação passou a ser registrada por instrumento. Um único indicador por processo
  fazia a primeira cotação a chegar silenciar todos os demais instrumentos, dando a impressão
  de que apenas um ticker havia sido entregue.

### Removido

- Repetições de estado no relatório: o handshake de roteamento e os estados intermediários de
  Market Data deixaram de ser registrados, e cada estado só reaparece quando o resultado muda.
- Registro de estados depois do pedido de encerramento. Durante o `DLLFinalize` a DLL reemite
  códigos que, traduzidos pela tabela de login e de ativação, sugeririam credencial inválida
  em uma execução correta.

### Descobertas registradas

- O handshake de roteamento é reemitido uma vez por servidor e por corretora, alternando entre
  os resultados 2 e 5 dezenas de vezes antes de estabilizar.
- Os códigos de estado não têm o mesmo significado durante o encerramento: os mesmos valores
  que indicam falha de login na subida apenas sinalizam a sessão sendo derrubada.
- Futuros e ações chegam com escalas de preço diferentes no mesmo callback.

### Verificado

- Build estrito com zero avisos e zero erros.
- Conexão real ponta a ponta com quatro instrumentos — `WINV26`, `WDOV26`, `PETR4` e `VALE3` —,
  todos aceitos por `SubscribeTicker`, com cotação recebida de cada um, desassinatura em ordem
  reversa, `DLLFinalize` retornando zero e processo encerrando com código zero.
- A validação ocorreu com o mercado fechado: cada instrumento entregou a última cotação do
  pregão anterior. O fluxo contínuo em pregão ainda não foi exercitado.
- Credenciais, logs e artefatos dessa validação foram descartados após a execução.

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

[Retrofit pós-Aula 03]: https://github.com/YouTrade/DLLNelogica/compare/aula-03...aula-03-retrofit
[Aula 03]: https://github.com/YouTrade/DLLNelogica/compare/aula-02-retrofit...aula-03
[Retrofit pós-Aula 02]: https://github.com/YouTrade/DLLNelogica/compare/aula-02...aula-02-retrofit
[Aula 02]: https://github.com/YouTrade/DLLNelogica/compare/aula-01...aula-02
[Aula 01]: https://github.com/YouTrade/DLLNelogica/releases/tag/aula-01
