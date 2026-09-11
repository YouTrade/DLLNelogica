# Histórico de evolução

Este arquivo registra os marcos didáticos relevantes do projeto. Ele não substitui o
`README.md`, que documenta o estado atual, nem o histórico de commits, que preserva as
alterações técnicas em detalhe.

Os marcos seguem a sequência das aulas, não versionamento semântico. Trabalhos estruturais
entre aulas recebem um nome próprio e não avançam artificialmente a numeração da série.

## [Times and Trades — Sprint 4: captura real e conferência dos arquivos] - 2026-09-11

As três primeiras sprints construíram a recepção, o processamento e a gravação. Faltava
observar a DLL conectada ao feed e conferir se os registros produzidos fechavam com os
contadores da aplicação. Nesta etapa, uma execução real isolada produziu os oito arquivos
esperados e encerrou normalmente. A homologação integral do roteiro ainda tem pendências.

### Por que esta etapa foi necessária

Um teste com API simulada permite provocar uma falha de disco ou uma edição na hora exata,
mas não demonstra o conteúdo que uma licença real entrega. Uma sessão em pregão mostra esse
conteúdo, mas pode terminar sem nenhuma edição ou falha de disco. As duas verificações
respondem a perguntas diferentes; por isso usamos ambas e identificamos a origem de cada
resultado.

A conferência também precisava ir além de “o arquivo existe”. Um arquivo pode existir e
estar incompleto. Depois do encerramento, contamos as linhas de negócio de cada instrumento,
excluindo os comentários de cabeçalho, e comparamos com seus eventos confirmados após flush.

### Adicionado

- Resumo final com contadores por instrumento: callbacks, aceitações, adições, edições,
  recusas, falhas e confirmações. A divisão permite localizar um saldo em um ativo específico.
- Pico conservador da ocupação da fila e duração da drenagem do consumidor. A medição começa
  depois da finalização nativa; não deve ser apresentada como duração total de encerramento.
- Teste do novo resumo com recusa explícita, reconciliação por instrumento e formatação do
  tempo de drenagem.

### Alterado

- Janela real reduzida de 15 para três minutos por solicitação do mantenedor. Entre o primeiro
  e o último recebimento foram observados aproximadamente **3 min 8 s**.
- README ampliado com a evolução em quatro etapas, diferenças entre cotação e negócio,
  significado de cada contador, leitura dos campos e roteiro de execução com os quatro ativos.
- Documentação publicada passou a conter as explicações e os resultados essenciais sem
  depender de links para a pasta `docs/`, mantida localmente e excluída do versionamento.

### Corrigido na explicação didática

- Contar callbacks de `TChangeCotation` não equivale a contar negócios. Os dois fluxos têm
  notificações e contadores próprios, mesmo sendo alimentados pela mesma assinatura.
- O horário da linha de cotação é registrado no consumidor. Sua diferença para `pwcDate`
  inclui processamento/fila local e possíveis diferenças entre relógios; não mede com
  precisão a latência da B3 até a aplicação.
- A sequência `chegada` é local ao fluxo. A sequência de cotação e a de T&T não formam
  uma chave comum para unir registros dos dois arquivos.

### Verificado

- Build Release em Windows x64: zero avisos e zero erros; **101 testes aprovados**, nenhum
  ignorado. SDK 9.0.318 e runtime 9.0.20, mantendo o perfil estrito dos analisadores.
- ProfitDLL 4.0.0.41 Win64 preservada. Execução real em **11/09/2026, de 13:03:45 a
  13:06:53, UTC−03:00**, com quatro assinaturas aceitas e dois fluxos simultâneos.

| Instrumento | Cotações gravadas | Eventos T&T aceitos e confirmados | Recusas T&T | Saldo não confirmado |
|---|---:|---:|---:|---:|
| WINV26:F | 5.464 | 30.893 | 0 | 0 |
| WDOV26:F | 144 | 1.525 | 0 | 0 |
| PETR4:B | 66 | 202 | 0 | 0 |
| VALE3:B | 65 | 149 | 0 | 0 |
| **Total** | **5.739** | **32.769** | **0** | **0** |

- Todas as linhas T&T dessa janela eram adições. A soma por instrumento coincidiu com os
  contadores finais; nenhuma falha de tradução, callback, publicação ou consumidor foi registrada.
- Pico conservador da fila: **237 eventos**, para capacidade 16.384. Drenagem do consumidor:
  **1,494 ms**. Sem timeout. Desassinaturas concluídas, DLLFinalize retornou zero e o processo
  encerrou com código zero após `Ctrl+C`.
- Oito arquivos copiados, hashes SHA-256 conferidos e ZIP validado antes da remoção do ambiente
  temporário. Configuração com credenciais e logs nativos foram removidos; o modelo do projeto
  permaneceu com campos vazios. Os arquivos de pregão preservados são uma entrega local,
  não conteúdo publicado no repositório.

### O que a execução ensinou

**Os números de cotação e de negócio não precisam coincidir.** Aqui foram 5.739 e 32.769.
Compará-los como se fossem a mesma contagem produziria um diagnóstico errado de perda.
A reconciliação compara cada arquivo com os contadores do seu próprio fluxo.

**Participantes em futuros devem seguir o dado recebido.** Nesta licença/feed/janela,
os dois lados vieram com IDs não zero e nomes resolvidos em todos os eventos, incluindo
WIN e WDO. Recebemos compra por agressão, venda por agressão e RLP. O código preservou os
participantes e manteve RLP com agressor `nao_classificado`. Não generalizamos essa
observação para todos os contratos, licenças ou configurações de feed.

**Zero saldo é uma evidência local delimitada.** Ele comprova que os eventos aceitos nesta
execução foram confirmados pelo gravador. Não demonstra que o provedor nunca omitiu um
negócio, que o armazenamento resistirá a falta de energia ou que qualquer carga futura
será suportada.

### Ainda pendente no roteiro de homologação

- Comparação independente de pelo menos 20 negócios de uma ação e 20 de um futuro com
  terminal de referência, registrando filtros e diferenças de apresentação.
- Carga sintética de 30 minutos calibrada pelo pico observado, com memória, taxas e drenagem.
  O pico medido nos arquivos foi 2.008 eventos em um segundo civil; o patamar mínimo proposto
  para essa carga é 4.016 eventos/s. Essa carga não foi executada nesta entrega.
- Execuções operacionais adicionais sem negócios e com T&T desabilitado. Há testes
  determinísticos dessas condições; a observação operacional permanece separada.

## [Times and Trades — Sprint 3] - 2026-09-11

Captura de negócios integrada à aplicação, com arquivos próprios e confirmação após flush.
Neste marco, a validação real em pregão ainda ficava para a sprint 4.

### Por que esta etapa foi necessária

Na sprint 2 o consumidor conseguia interpretar e entregar um evento a uma saída substituível.
Faltava tornar essa saída um arquivo operacional e definir quando era correto afirmar que
um evento estava gravado. Escrever em um buffer e confirmar a descarga desse buffer são
momentos diferentes.

A solução acrescentou um gravador exclusivo de T&T e contadores de confirmação. Se 100 eventos
foram aceitos e somente 80 confirmados, o saldo é 20, mesmo que o consumidor já tenha chamado
a escrita dos 100. Essa distinção permite detectar falha de flush e encerramento incompleto.

### Adicionado

- Arquivos `<TICKER>_<BOLSA>_TimesAndTrades.txt`, UTF-8 sem BOM, schema 1, números invariant,
  escape de textos e identificação da sessão. Preparação dos destinos antes do login.
- `TradeFileOutput`/`TradeFileSet`, com escrita exclusiva e append, sem usar o `DailyLogSink`
  para o detalhe dos negócios. Lotes limitados a 1.000 eventos e flush periódico de um segundo.
- Rotação pela data local e roteamento de backlog pela data de recebimento, independente da
  data nativa. Destinos sem eventos também são preparados.
- Confirmações globais e por instrumento, adições/edições confirmadas e saldo não confirmado.
- Primeiro negócio por instrumento, resumo periódico e balanço final de T&T nos relatórios.
- `TradeRuntimePipeline`/factory, preparação verificável, drenagem de dez segundos e timeout
  explícito sem descartar recursos usados por um worker ativo.
- Validação antecipada de colisões entre arquivos de cotação/T&T e destinos reservados.

### Alterado

- Modelo `appsettings.json` passa a habilitar T&T; credenciais permanecem vazias e configurações
  antigas sem a seção continuam funcionando somente com cotação.
- Aplicação espera a preparação dos arquivos antes do login e finaliza a DLL antes de drenar.
- Consultas de nomes param antes das desassinaturas e da finalização.
- `TradeConsumer` concentra consumo, manutenção periódica e descarte do gravador. A publicação
  mantém a reserva atômica e não executa disco, console ou espera por capacidade.
- Regras de nomes Windows passam a ser compartilhadas com o fluxo de cotação.

### Verificado

- Build Release: zero avisos/erros; 100 testes aprovados em Windows x64, nenhum ignorado.
- Arquivos, culturas pt-BR/en-US, virada do dia, append, bloqueio de segunda instância,
  escrita parcial, erro de flush, timer ocioso, rollback e timeout de drenagem.
- Callback durante a finalização simulada confirmado em arquivo real temporário.
- Modelo sem credenciais preenchidas; DLL intacta; nenhum login real executado.

### O que ficou aprendido

O consumidor é o único dono do gravador. Se o encerramento atingir o timeout enquanto uma
escrita estiver travada, outra thread não pode descartar esse gravador em uso. O processo
sinaliza falha, e o worker continua responsável por liberar o recurso quando puder terminar.
Também não repetimos um lote após escrita parcial: sem saber quanto foi gravado, repetir
poderia criar duplicatas.

## [Times and Trades — Sprint 2] - 2026-09-11

Implementado o pipeline de recepção e interpretação de negócios, com saída substituível
para validação sintética. Neste marco, a integração operacional e os arquivos ficavam para
a sprint 3.

### Por que esta etapa foi necessária

Receber um evento corretamente não autoriza executar qualquer trabalho dentro do callback.
A thread pertence à DLL; consultar nomes, aguardar disco ou esperar uma vaga na fila prolongaria
o tempo em que ela fica ocupada com nosso código.

A fila limitada separa o produtor, que recebe, do consumidor, que interpreta. O limite evita
crescimento indefinido em memória. Quando o consumidor não acompanha a entrada e a fila
satura, o T&T conta a recusa e solicita encerramento com captura incompleta. Também precisamos
contar cada desfecho uma única vez, mesmo com vários callbacks concorrentes e com o encerramento
ocorrendo ao mesmo tempo. Por isso esta sprint concentrou testes de concorrência e balanço.

### Adicionado

- `TradeEventPump`: canal limitado, leitor único, múltiplos produtores e `TryWrite` sem espera
  por capacidade; fechamento aguarda publicações já reservadas antes de concluir o writer.
- Contrato `TradeRecord`/`ITradeOutput`, preservando o evento bruto e acrescentando data,
  tipo, agressor, edição e participantes. Entrega à saída não significa flush em disco.
- Mapeamento dos tipos documentados e preservação de códigos/flags desconhecidos. Edições
  e repetições permanecem eventos independentes.
- Cache positivo/negativo de até 4.096 IDs de participantes, nomes opcionais e IDs preservados
  em qualquer bolsa; dados indisponíveis não impedem o consumo.
- Métricas globais e por instrumento, com snapshots imutáveis e atualização atômica:
  callbacks, aceitações, recusas, tradução, falhas, entregas, pendências, adições/edições,
  anomalias de data, consultas de nome, último evento e ocupação da fila.
- 44 testes novos, incluindo carga de 8.000 eventos com quatro produtores, saturação com
  barreiras, falha de saída, drenagem e encerramento concorrente com publicação.

### Alterado

- A ponte informa falhas de tradução e exceções ao destino de negócios para contabilização.
- A sinalização de encerramento usa `CancelAsync`: registros de cancelamento não executam
  trabalho síncrono na thread nativa. A mudança também se aplica à sinalização compartilhada
  pelos callbacks existentes; sua regressão passou na suíte.

### Verificado

- Build Release em Windows x64 com zero avisos/erros; 84 testes aprovados, nenhum ignorado.
- Na carga sintética, 8.000 eventos entregues, 4.000 por instrumento, zero pendências.
- Configuração e DLL preservadas. Nenhum login real ou escrita de arquivo T&T executado.

### O que ficou aprendido

Nome de corretora é um enriquecimento opcional, não uma condição para registrar o negócio.
Código e flags brutos também precisam sobreviver à interpretação. Assim, um tipo desconhecido
ou uma data inválida continua disponível para investigação, em vez de desaparecer ou ganhar
um significado inventado. Nesta etapa, “entregue” significava aceito pela saída do consumidor;
a confirmação de flush ainda não existia.

## [Times and Trades — Sprint 1] - 2026-09-11

Preparação dos contratos e da integração nativa para receber negócios pela API V2.
Neste marco, a captura operacional de T&T e os arquivos por instrumento ainda estavam
previstos para as próximas sprints; o fluxo de cotações permanecia disponível.

### Por que esta etapa foi necessária

Uma DLL nativa e o C# precisam concordar sobre como cada campo ocupa a memória. Essa combinação
de tamanhos, posições e convenção de chamada é a ABI. Um programa pode compilar com a ABI
errada e só revelar o erro ao ler um número ou receber um callback real.

O contrato V2 também entrega um ponteiro para o negócio. A tradução e a cópia precisam ocorrer
dentro do callback; a fila recebe valores gerenciados, não o endereço nativo para leitura futura.
A quantidade foi preservada em 64 bits, e o delegate ficou referenciado durante a vida do
processo para a DLL não chamar uma função que o coletor de lixo já tenha liberado.

Esta etapa voltou a incluir um projeto de testes, que havia sido removido no retrofit da
Aula 02. O novo marco passou a verificar contratos de memória, tradução e ciclo de vida;
assim, a remoção antiga continua registrada como história e a suíte atual tem sua origem explícita.

### Adicionado

- Seção opcional `TimesAndTrades`, com defaults compatíveis e validação de tipos/limites.
- Importações `SetTradeCallbackV2`, `TranslateTrade`, `GetAgentNameLength` e `GetAgentName`.
- `SystemTime`, `TConnectorTrade`, tipos/flags e delegate V2 enraizado pelo processo.
- Tradução síncrona para `RawTrade`, sem ponteiro nativo na saída; erros explícitos na tradução.
- Porta `ITradeEventSink` para a fila da próxima sprint e contenção de falhas no callback.
- Consulta de nomes com buffer UTF-16 limitado e coordenação entre consulta e finalização.
- Projeto `DLLNelogica.Tests` na solução, com acesso restrito aos tipos internos.

### Alterado

- Inicialização/registro foram extraídos para `MarketDataSessionStartup`, preservando o
  limite de acoplamento dos analisadores e o fluxo existente com T&T desabilitado.
- Falha de registro de callback obrigatório bloqueia chamadas subsequentes de assinatura.
- `Enabled=true` sem consumidor integrado falha explicitamente antes de inicializar a DLL.
- `DLLFinalize` aguarda consultas de nomes em andamento e impede novas consultas.

### Verificado

- Build Release em Windows x64: zero avisos e zero erros, sem relaxar os analisadores.
- 40 testes aprovados, nenhum ignorado: configuração, ABI x64, tradução, registro,
  proteção após GC, callbacks durante finalização e concorrência de consultas de nomes.
- Binário da DLL e `appsettings.json` não alterados; nenhum login real executado nos testes.

### O que ficou aprendido

Ter as funções importadas não significa que a captura esteja operacional. Com T&T habilitado
e sem consumidor, a aplicação dessa sprint rejeitava a configuração antes do login. Esse
comportamento identificava a dependência ainda não implementada e foi superado pela integração
da sprint 3. Configurações antigas, sem T&T, continuavam usando o caminho anterior.

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
  preço e contagem de callbacks de cotação de cada instrumento no intervalo
  (terminologia esclarecida na sprint 4 de T&T).
- `IReportLog`, porta de escrita em destinos nomeados, com `NullReportLog` para quando o
  relatório do dia não pôde ser inicializado.
- `ReportFileWriter`, que mantém um gravador por arquivo aberto sob demanda.
- `MarketDataSnapshot` e `MarketDataReporter`, responsáveis pela amostra de cada intervalo.
- Higienização do ticker antes de virar nome de arquivo.
- Horário registrado no consumidor ao lado do `pwcDate` recebido da DLL em cada linha
  de cotação (interpretação esclarecida na sprint 4 de T&T).

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

- Na época, a diferença de 100–200 ms entre o horário do consumidor e `pwcDate` foi
  interpretada como latência. Correção na sprint 4: inclui processamento/fila local e
  diferenças de relógio; não permite concluir a latência exata do caminho da B3.
- A sequência local de chegada é compartilhada entre os instrumentos do fluxo de cotações.
  O fluxo T&T, acrescentado posteriormente, tem sua própria sequência.
- Um relatório periódico preso ao token de encerramento travaria a drenagem no caminho de
  falha, onde esse token nunca é cancelado. O relator precisa da própria parada.
- `LoginResult=MaxHID` na ProfitDLL significa "todos os seus logins estão em uso". O callback
  de estado devolve apenas o resultado 200; o motivo só aparece no `LogDesktop` da própria DLL.

### Verificado

- Build estrito com zero avisos e zero erros.
- Execução em pregão aberto com quatro instrumentos: 1.325 cotações recebidas, zero descartadas
  e zero tickers inválidos.
- As linhas gravadas nos arquivos de instrumento — 1.208, 92, 20 e 5 — somam exatamente as
  1.325 cotações contabilizadas, sem diferença entre esses totais na execução medida.
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
