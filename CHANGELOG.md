# Histórico de evolução

Este arquivo registra os marcos didáticos relevantes do projeto. Ele não substitui o
`README.md`, que documenta o estado atual, nem o histórico de commits, que preserva as
alterações técnicas em detalhe.

Os marcos seguem a sequência das aulas, não versionamento semântico. Cada aula é uma entrada;
o trabalho estrutural feito logo depois de uma aula (o *retrofit*) fica registrado dentro dela,
e as etapas internas de uma aula maior aparecem como subseções, não como marcos próprios.

## [Aula 05 — contas cadastradas e relatório sem pontas soltas] - 2026-09-22

As contas que a DLL anuncia após o login passaram a ser registradas no relatório do dia, e a
preparação do Times and Trades ganhou uma linha de conclusão. As quatro sprints de Times and
Trades passam a ser referidas como Aula 04 da série.

### Por que esta etapa foi necessária

O `TAccountCallback` é entregue a `DLLInitializeLogin` desde a Aula 01, mas seu tratador
estava vazio. Antes de qualquer aula sobre ordens, o relatório precisa mostrar com quais
contas o login pode operar. Ao mesmo tempo, a linha "preparação de arquivos iniciada" abria
o log sem nenhuma linha que a fechasse: o sucesso ficava implícito na inicialização da DLL.

### Adicionado

- `AccountEvent`, com corretora (id e nome), conta e titular, e a formatação da linha
  "Conta cadastrada".
- `ConnectionPumpEvent`: o canal do pump de estados passou a transportar também linhas
  informativas, escritas pelo consumidor na ordem em que a DLL as anunciou.
- `ConnectionStateEventPump.TryPublishLine`, usado pelo callback de contas. O callback
  continua apenas publicando; nenhum I/O na thread nativa.
- Deduplicação no `ProfitCallbackBridge`: cada par corretora+conta gera uma única linha; a
  primeira ocorrência vence.
- `TradeRuntimePipeline.ReportDestinationsReady`, chamado assim que o consumidor de T&T sinaliza
  pronto e antes de `DLLInitializeLogin`, registrando "destinos prontos" com os instrumentos.

### Alterado

- README: nova seção da Aula 05, tabela de evolução com a Aula 04 (Times and Trades) e a
  Aula 05, lista de passos e estrutura atualizadas.
- Linhas informativas, como as de conta, seguem a mesma regra dos estados: nada é escrito
  depois do pedido de encerramento.

### Descobertas registradas

- A DLL reanuncia a lista completa de contas várias vezes na mesma sessão. Em uma execução
  foram duas passagens; em outra, seis, com conteúdo variável: quatro passagens com quatro
  contas (incluindo simulador) e duas passagens só com as contas reais.
- A mesma conta pode ser anunciada sob dois ids de corretora distintos. O projeto registra o
  que a DLL informa e não interpreta.
- O limite de acoplamento `CA1506` recusou a primeira versão, em que o pump conhecia
  `AccountEvent`. Publicar a linha pronta manteve o pump desacoplado do tipo de origem.

### Verificado

- Build Release em Windows x64: zero avisos e zero erros; **101 testes aprovados**, nenhum
  ignorado. SDK 9.0.318.
- Execução real em **22/09/2026, 12:49, UTC−03:00**, com 30 segundos de captura: quatro
  contas registradas uma única vez, quatro assinaturas aceitas, 364 cotações e 1.917 eventos
  T&T confirmados, nenhum descarte, pico de fila 74, drenagem em 1,801 ms, `DLLFinalize`
  retornando zero e processo encerrando com código zero após `Ctrl+C`.
- Credenciais foram apagadas da cópia de saída após cada execução; o modelo versionado
  permaneceu com campos vazios. A alteração diagnóstica que exibia os estados de roteamento
  foi revertida antes do commit.

## [Aula 04 — Times and Trades] - 2026-09-11

Captura de negócios realizados (Times and Trades) pela API V2 da ProfitDLL, do callback
nativo até um arquivo por instrumento, com confirmação após flush e conferência em pregão
real. A aula foi construída em quatro etapas no mesmo dia; cada uma resolveu uma parte da
viagem do dado.

### Por que esta aula foi necessária

Até a Aula 03 o projeto sabia *que cotação* a DLL informou. Faltava saber *que negócio* foi
feito, com preço, quantidade, participantes e tipo. O contrato V2 entrega um ponteiro para a
memória nativa, e a DLL e o C# precisam concordar sobre a ABI: um programa pode compilar com
a ABI errada e só revelar o erro ao ler um número. Receber corretamente também não autoriza
trabalhar dentro do callback: a thread pertence à DLL. E escrever em um buffer não é o mesmo
que confirmar a descarga desse buffer em disco.

### As quatro etapas

1. **Receber o dado corretamente.** Importações `SetTradeCallbackV2`, `TranslateTrade`,
   `GetAgentNameLength` e `GetAgentName`; `SystemTime`, `TConnectorTrade` e o delegate V2
   enraizado pelo processo. Tradução síncrona para `RawTrade` dentro do callback, sem ponteiro
   nativo na saída; quantidade em 64 bits. O projeto `DLLNelogica.Tests` voltou à solução,
   verificando tamanho e posição dos campos na memória.
2. **Separar quem recebe de quem trabalha.** `TradeEventPump` com canal limitado, leitor único
   e `TryWrite` sem espera; fila saturada conta recusa e solicita encerramento. Mapeamento dos
   tipos de negócio com preservação de códigos desconhecidos, cache de participantes e métricas
   globais e por instrumento com snapshots imutáveis. Carga sintética de 8.000 eventos.
3. **Transformar eventos em arquivos verificáveis.** Arquivos `<TICKER>_<BOLSA>_TimesAndTrades.txt`
   preparados antes do login, gravador exclusivo com lotes e flush periódico, rotação pela data
   local, confirmações por instrumento e saldo não confirmado. Drenagem de dez segundos com
   timeout explícito; o consumidor é o único dono do gravador.
4. **Conferir em pregão real.** Resumo final por instrumento, pico de fila e duração da
   drenagem. Execução real de três minutos com os oito arquivos reconciliados contra os
   contadores.

### Alterado

- Modelo `appsettings.json` passa a habilitar T&T; configurações antigas sem a seção continuam
  funcionando só com cotação.
- Inicialização e registro extraídos para `MarketDataSessionStartup`. Falha de registro de
  callback obrigatório bloqueia assinaturas; `Enabled=true` sem consumidor falha antes do login.
- `DLLFinalize` aguarda consultas de nomes em andamento e impede novas; a aplicação finaliza a
  DLL antes de drenar.
- A sinalização de encerramento usa `CancelAsync`, sem trabalho síncrono na thread nativa.
- README ampliado com a diferença entre cotação e negócio, o significado de cada contador e a
  leitura dos campos.

### Corrigido na explicação didática

- Contar callbacks de `TChangeCotation` não equivale a contar negócios: são fluxos e contadores
  próprios, mesmo alimentados pela mesma assinatura.
- A diferença entre o horário do consumidor e `pwcDate` inclui fila local e relógios distintos;
  não mede a latência da B3 até a aplicação.
- A sequência `chegada` é local ao fluxo; cotação e T&T não têm chave comum.

### Verificado

- Build Release em Windows x64: zero avisos e zero erros; **101 testes aprovados**, nenhum
  ignorado. SDK 9.0.318, runtime 9.0.20. ProfitDLL 4.0.0.41 Win64 preservada.
- Execução real em **11/09/2026, de 13:03:45 a 13:06:53, UTC−03:00**, quatro assinaturas
  aceitas e dois fluxos simultâneos.

| Instrumento | Cotações gravadas | Eventos T&T aceitos e confirmados | Recusas T&T | Saldo não confirmado |
|---|---:|---:|---:|---:|
| WINV26:F | 5.464 | 30.893 | 0 | 0 |
| WDOV26:F | 144 | 1.525 | 0 | 0 |
| PETR4:B | 66 | 202 | 0 | 0 |
| VALE3:B | 65 | 149 | 0 | 0 |
| **Total** | **5.739** | **32.769** | **0** | **0** |

- Todas as linhas T&T eram adições. Pico conservador da fila: **237 eventos** para capacidade
  16.384. Drenagem: **1,494 ms**, sem timeout. `DLLFinalize` retornou zero e o processo
  encerrou com código zero após `Ctrl+C`.
- Hashes SHA-256 dos oito arquivos conferidos. Credenciais e logs nativos removidos; o modelo
  permaneceu com campos vazios. Os arquivos de pregão são entrega local, não conteúdo do repositório.

### O que a aula ensinou

**Os números de cotação e de negócio não precisam coincidir.** Aqui foram 5.739 e 32.769;
compará-los como a mesma contagem produziria um diagnóstico errado de perda.

**Nome de corretora é enriquecimento, não condição.** Código e flags brutos sobrevivem à
interpretação: um tipo desconhecido continua disponível para investigação em vez de ganhar
um significado inventado. Em futuros, nesta licença e janela, os dois lados vieram com IDs
e nomes resolvidos, inclusive RLP com agressor `nao_classificado`; a observação não foi
generalizada.

**Zero saldo é evidência local.** Comprova que os eventos aceitos nesta execução foram
confirmados pelo gravador; não demonstra que o provedor nunca omitiu um negócio nem que o
armazenamento resistirá a falta de energia.

### Ainda pendente no roteiro de homologação

- Comparação independente de pelo menos 20 negócios de uma ação e 20 de um futuro com terminal
  de referência.
- Carga sintética de 30 minutos calibrada pelo pico observado (2.008 eventos em um segundo
  civil; patamar mínimo proposto de 4.016 eventos/s).
- Execuções operacionais adicionais sem negócios e com T&T desabilitado.

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

### Retrofit — 2026-09-10

A primeira execução em pregão aberto entregou 1.325 cotações em 55 segundos, contra as quatro
do teste com mercado fechado. O registro em arquivo único deixou de servir, e a aplicação não
tinha visão do mercado enquanto rodava. Este retrofit reorganizou a observabilidade em torno
desse volume, sem alterar o consumo de Market Data em si.

- **Adicionado:** diretório por dia em `Relatorios/AAAAMMDD/` com rotação na virada; um arquivo
  por instrumento (`<TICKER>_<BOLSA>.txt`) com uma linha por cotação; `_Sessao.txt` para o
  relato e `_Resumo.txt` para a amostragem periódica governada por `ReportIntervalSeconds`;
  `IReportLog` com `NullReportLog`; `ReportFileWriter`, `MarketDataSnapshot` e
  `MarketDataReporter`; higienização do ticker como nome de arquivo; horário do consumidor ao
  lado do `pwcDate` em cada linha.
- **Alterado:** `DailyFileWriter` deu lugar a `ReportFileWriter`; o relato passou de
  `log/AAAAMMDD.log` para `Relatorios/AAAAMMDD/_Sessao.txt`; carimbo de linha por destino;
  `MarketDataRuntimePipeline` recebe `MarketDataOptions` inteiro e é `IDisposable`.
- **Removido:** `MarketData.HistoryCapacityPerInstrument`, exigido e validado sem que nada o
  consumisse. Configuração que promete o que o programa não entrega é pior que ausente.
- **Descobertas:** um relatório periódico preso ao token de encerramento travaria a drenagem no
  caminho de falha, por isso o relator tem a própria parada; `LoginResult=MaxHID` significa
  "todos os seus logins estão em uso" e o motivo só aparece no `LogDesktop` da DLL; a diferença
  de 100–200 ms para `pwcDate` foi lida como latência e corrigida na Aula 04.
- **Verificado:** build estrito sem avisos; 1.325 cotações em pregão aberto, zero descartadas,
  e as linhas gravadas (1.208, 92, 20 e 5) somam exatamente esse total; 138 KB em 55 segundos,
  mais de 70 MB por pregão, sem política de retenção por decisão do mantenedor.

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

### Retrofit — 2026-08-28

Reorganização e endurecimento da base das duas primeiras aulas, sem consumo de Market Data.

- **Adicionado:** composition root com injeção de dependências manual; fronteiras explícitas
  para aplicação, configuração, conexão, interop e logging; `ProfitCallbackRoots` com os cinco
  delegates nativos enraizados; `ProfitProcessLifetime` com as guardas por processo; pipeline
  assíncrono do relatório com ordenação FIFO; governança com `.editorconfig`,
  `Directory.Build.props` e métricas CA1502, CA1505 e CA1506.
- **Alterado:** `Program.cs` passou de 583 para 28 linhas; credenciais extraídas para
  `JsonCredentialsLoader`; API nativa acessada por `IProfitApi`, `ProfitNativeApi` e
  `ProfitSession`; estados processados fora da thread nativa, com o evento causador no
  relatório antes da transição; flush por lote com timer de segurança; prontidão histórica de
  Market Data e saúde corrente viraram conceitos distintos.
- **Corrigido:** falhas de console não interrompem a máquina de estados; nenhuma exceção
  gerenciada atravessa a fronteira do callback nativo; ordem de encerramento explícita:
  `DLLFinalize`, conclusão do canal e drenagem; nenhum I/O na thread dos callbacks.
- **Removido:** a suíte de 24 testes determinísticos, por decisão explícita do mantenedor
  (uma nova suíte voltou na Aula 04); o canal antecipado de Market Data sem produtor nem
  consumidor.
- **Verificado:** build estrito sem avisos, regras comprovadas por builds negativos, conexão
  real ponta a ponta com os quatro estados, `DLLFinalize` zero e saída zero após `Ctrl+C`.

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
