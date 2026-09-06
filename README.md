# DLLNelogica — Projeto Educacional

> Série **Programando o seu robô de trading com a DLL da Nelogica** — **Aula 03 concluída:
> consumo de Market Data.**

Exemplo didático em C# que demonstra, do zero, como estabelecer uma conexão com a
**ProfitDLL da Nelogica**: autenticar, confirmar que todos os serviços subiram, assinar
instrumentos, receber cotações e finalizar a sessão de forma limpa.

Este material foi escrito para ensino. O objetivo é que você entenda **cada decisão** —
por que um callback não pode bloquear, por que um retorno `NL_OK` não significa "conectado",
por que um estado precisa ser travado e não reavaliado.

---

## Evolução da série

| Marco | Data | Resultado principal |
|-------|------|---------------------|
| Aula 01 | 2026-08-24 | Conexão, quatro estados obrigatórios e ciclo de vida da ProfitDLL |
| Aula 02 | 2026-08-26 | Relatório diário e observabilidade antes do Market Data |
| Retrofit pós-Aula 02 | 2026-08-28 | Arquitetura, DI manual, resiliência de callbacks e logging assíncrono |
| Aula 03 | 2026-09-06 | Consumo de Market Data: assinatura de instrumentos e cotações |

O histórico curado, incluindo o estado original da Aula 01 e as decisões do retrofit, está
em [CHANGELOG.md](CHANGELOG.md). O README descreve sempre o comportamento atual do projeto.

---

## Aula 03 — o mercado passando por dentro

A Aula 02 foi preparatória. Receber dados de mercado sem conseguir registrar de forma
determinística *o que* chegou, *quando* chegou e em *qual estado* a aplicação estava seria
construir a etapa seguinte sem base para observação e diagnóstico. Primeiro uma base
confiável — depois o mercado passando por dentro dela.

Com a base pronta, a Aula 03 liga o **Market Data**. A aplicação lê uma lista de instrumentos
da configuração, assina cada um deles, recebe as cotações e desassina em ordem reversa no
encerramento.

Trecho de uma execução real, com quatro instrumentos:

```
2026-09-06 10:30:15 [DLLNelogica] Login: conectado.
2026-09-06 10:30:16 [DLLNelogica] Market Data: conectado e pronto para receber cotações.
2026-09-06 10:30:17 [DLLNelogica] Conexão confirmada pelos quatro estados obrigatórios.
2026-09-06 10:30:17 [DLLNelogica] SubscribeTicker(WINV26:F) retornou NL_OK — 0 (0x00000000).
2026-09-06 10:30:17 [DLLNelogica] SubscribeTicker(PETR4:B) retornou NL_OK — 0 (0x00000000).
2026-09-06 10:30:17 [DLLNelogica] Primeira TChangeCotation recebida | instrumento=WINV26:F | pwcDate=04/09/2026 18:31:28.827 | sequência=52875360 | preço=187600.
2026-09-06 10:30:17 [DLLNelogica] Primeira TChangeCotation recebida | instrumento=PETR4:B | pwcDate=04/09/2026 18:39:38.653 | sequência=462610 | preço=46,97.
2026-09-06 10:31:00 [DLLNelogica] Resumo de market data | cotações recebidas=4 | descartadas=0 | tickers inválidos=0
```

Três decisões desta aula valem mais que o código que as implementa:

**A assinatura é tudo ou nada.** Se um instrumento for recusado, os que já foram aceitos são
desassinados e a aplicação encerra. Uma sessão parcial — em que você acredita estar observando
quatro ativos mas recebe três — é pior que uma falha explícita.

**A thread de callback nunca espera.** As cotações entram em um canal limitado e são consumidas
fora da thread nativa. Quando o canal enche, a cotação é descartada e contabilizada: travar a
thread da DLL para não perder um tick colocaria a sessão inteira em risco.

**Ticker inválido em uma assinatura aceita é falha terminal.** Se a DLL avisa que um ticker
que você assinou não existe, continuar rodando seria fingir que a configuração está correta.

> A validação desta aula foi feita com o mercado fechado: cada instrumento entregou a última
> cotação do pregão anterior. O fluxo contínuo em pregão ainda não foi exercitado.

---

## ⚠️ Leia antes de usar

**Este projeto está incompleto e não possui mecanismos de segurança.** Ele existe para
estudo, não para produção.

O que ele **não** tem:

- Nenhuma proteção de credenciais — elas ficam em texto puro no `appsettings.json`
- Nenhuma reconexão automática, retentativa ou recuperação de falha
- Nenhum tratamento de ordens, posições ou contas
- Nenhuma persistência das cotações recebidas — elas são contabilizadas e descartadas
- Nenhuma auditoria, persistência de dados ou monitoramento
- Nenhuma suíte automatizada de testes — a validação disponível é compilação estrita e execução manual
- Nenhuma retenção ou expurgo do relatório — os arquivos diários se acumulam indefinidamente

**Não use este código para operar dinheiro real.** Use-o para aprender como a interoperabilidade
com a ProfitDLL funciona e depois construa o seu, com os cuidados que a sua operação exige.

---

## O que o projeto faz

1. Abre o relatório diário em `log/AAAAMMDD.log`, ao lado do executável
2. Lê as credenciais e a lista de instrumentos de `src/appsettings.json`
3. Carrega a `ProfitDLL.dll` (Win64) explicitamente do diretório da aplicação
4. Chama `DLLInitializeLogin` uma única vez
5. Registra os callbacks de cotação e de ticker inválido
6. Publica os estados recebidos em uma fila e os processa fora da thread nativa
7. Registra cada estado antes de aplicar a transição que ele causa
8. Anuncia a conexão apenas quando os quatro estiverem satisfeitos
9. Assina todos os instrumentos configurados, ou desfaz o que já assinou e encerra
10. Consome as cotações em segundo plano, contabilizando recebidas e descartadas
11. Mantém o processo vivo até `Ctrl+C`
12. Desassina os instrumentos em ordem reversa
13. Chama `DLLFinalize` e drena os eventos pendentes antes de sair

Cada um desses passos deixa rastro no relatório.

### Os quatro estados da conexão

A DLL informa o progresso da conexão pelo `TStateCallback(nConnStateType, nResult)`:

| Tipo | Serviço | Valor esperado |
|------|---------|----------------|
| 0 | Login | `0` — conectado |
| 1 | Roteamento | `2` (servidor) **ou** `5` (corretora) |
| 2 | Market Data | `4` — conectado |
| 3 | Ativação | `0` — licença válida |

Três detalhes que só se descobrem observando a DLL em execução, e que este projeto trata:

- **Os estados chegam em qualquer ordem.** Em sessões reais a ativação chegou antes do login.
- **Os estados oscilam.** O roteamento vai e volta entre 1, 2, 4 e 5 antes de estabilizar —
  por isso cada estado é *travado* na primeira vez que fica válido, e não reavaliado a cada evento.
- **Market Data 5 e 6 continuam sendo "conectado".** São avisos de degradação e de fila local
  parada, não desconexão.

O handshake de roteamento é reemitido uma vez por servidor e por corretora: em uma conexão
comum ele produz dezenas de eventos alternando entre os resultados 2 e 5. A máquina de estados
recebe todos eles, mas o relatório registra apenas as transições que mudam de resultado, e o
roteamento fica de fora — um roteamento que não sobe já aparece na lista de estados pendentes
da mensagem de timeout.

Durante o `DLLFinalize` a DLL reemite os mesmos códigos para anunciar a sessão sendo derrubada.
Ali eles **não** significam o que significam na subida: o resultado 1 de login, que na conexão
seria "usuário inválido", é apenas a sessão terminando. Por isso o relatório para de registrar
estados assim que o encerramento é solicitado.

---

## O relatório diário

Não há uma API de log espalhada pela aplicação: `Console.Out` e `Console.Error` são
redirecionados para um *tee*. Cada escrita entra primeiro em uma fila e segue para o console;
um gravador dedicado persiste a fila em segundo plano com carimbo de data e hora.

```
<diretório do executável>/
└── log/
    └── 20260826.log
```

- **Um arquivo por dia**, nomeado `AAAAMMDD.log`. A rotação acontece sozinha na virada do
  dia, sem reiniciar a aplicação.
- **Cada linha carimbada** com `AAAA-MM-DD HH:mm:ss [DLLNelogica]`.
- **Callbacks não fazem I/O**: a thread nativa apenas publica eventos e retorna.
- **Flush por lote**: o gravador descarrega o arquivo depois de cada lote consumido. Um timer de
  um segundo cobre períodos ociosos; esperas síncronas têm limite de dois segundos para que um
  disco travado não congele o processo. Uma queda abrupta ainda pode perder o lote em andamento.
- **`stdout` e `stderr` no mesmo arquivo**, na ordem em que entram na fila compartilhada.
- **Falhas não tratadas entram no relatório** com tipo, mensagem e *stack trace* — o runtime
  imprimiria isso fora do `Console.Error`, e o registro se perderia.
- **A fila do arquivo vem primeiro, o console depois**: se o console falhar, a entrada já foi
  entregue ao gravador dedicado.
- **UTF-8 sem BOM**, com acentuação preservada.
- **Um gravador por arquivo.** Uma segunda instância no mesmo diretório não sobrescreve o
  relatório da primeira: ela avisa e segue apenas com o console.
- Se a pasta não puder ser criada, a aplicação **avisa e continua** — a ausência de log nunca
  derruba a execução.

> **`log/` é da aplicação. `Logs/` é da ProfitDLL.**
>
> A DLL da Nelogica grava os próprios arquivos em uma pasta `Logs/` ao lado do executável
> (`LogDesktop`, `LogStructuredBlb`, `LogPerf` e outros). Em poucos minutos de operação eles
> passam facilmente das dezenas de MB. Os nomes diferentes mantêm o seu relatório separado
> desse volume — e é por isso que a pasta da aplicação é `log`, no singular.

---

## Requisitos

- Windows **x64**
- **.NET 9 SDK**
- `ProfitDLL.dll` versão **4.0.0.41**, variante **Win64** — **já incluída** em `src/`
- Conta Nelogica com **roteamento habilitado** e licença ativa

> A DLL de 32 bits **não funciona** neste projeto. O processo é compilado como x64 e a
> arquitetura precisa coincidir.

> O repositório inclui a `ProfitDLL.dll` (~49 MB), então o clone é proporcionalmente maior.
> Ter o binário **não dispensa** a licença Nelogica: sem conta ativa a conexão não completa.

---

## Como executar

**1. Preencha as credenciais e escolha os instrumentos** em `src/appsettings.json`:

```json
{
  "Credenciais": {
    "Key": "sua-chave-de-ativacao",
    "User": "seu-usuario",
    "Password": "sua-senha"
  },
  "MarketData": {
    "ChannelCapacity": 4096,
    "HistoryCapacityPerInstrument": 1000,
    "ReportIntervalSeconds": 1,
    "Instruments": [
      { "Ticker": "WINV26", "Exchange": "F" },
      { "Ticker": "PETR4", "Exchange": "B" }
    ]
  }
}
```

`Exchange` é a bolsa do instrumento: `F` para os futuros da BM&F, `B` para as ações da Bovespa.
`ChannelCapacity` é o tamanho da fila de cotações — quando ela enche, o excedente é descartado
e contabilizado, para que a thread da DLL nunca fique esperando.

> Os tickers de futuros carregam o vencimento no nome (`WINV26`, `WDOV26`) e portanto vencem.
> Se o contrato configurado não existir mais, a assinatura é recusada e a aplicação encerra
> por inteiro — troque o vencimento antes de rodar.

**2. Nada a baixar:** a `src/ProfitDLL.dll` (Win64) já acompanha o repositório.

**3. Compile e execute:**

```
dotnet build DLLNelogica.sln
dotnet run --project src/DLLNelogica.csproj
```

**4. Encerre com `Ctrl+C`.** O encerramento é controlado: a aplicação chama `DLLFinalize`,
aguarda o retorno e só então termina.

**5. Confira o relatório.** O arquivo do dia fica ao lado do executável — com `dotnet run`,
em `src/bin/<plataforma>/<configuração>/net9.0/log/AAAAMMDD.log`.

## Cuidados importantes

**Nunca versione o `appsettings.json` preenchido.** O repositório já traz um `.gitignore`
que mantém fora do controle de versão a saída de compilação (`bin/`, `obj/`), os arquivos da
IDE (`.vs/`), o relatório da aplicação (`log/`), os artefatos da ProfitDLL (`Logs/`,
`database/`, `PopupManagerV2/`, `roteamento/`, `MarketHours2/` e os `.dat` que ela gera) e os
arquivos de credenciais locais.

O `src/appsettings.json` versionado é apenas o **modelo, com os campos vazios**. Como ele já
está rastreado pelo Git, o `.gitignore` não o protege: preencha-o só na sua cópia e confira
antes de cada commit. Credenciais commitadas continuam no histórico mesmo depois de apagadas
do arquivo.

**A ProfitDLL escreve arquivos no diretório de trabalho.** Ao inicializar, ela cria `Logs/`,
`database/`, `PopupManagerV2/`, `MarketHours2/`, `roteamento/`, algumas DLLs do OpenSSL e
arquivos `.dat`. Isso é esperado — só não deixe esses artefatos entrarem no seu controle de
versão.

**Atenção ao `Erro.log`.** Em caso de falha, a ProfitDLL pode gravar um arquivo de erro que
**contém a sua chave de ativação em texto puro**. Se ele aparecer, apague — e nunca o envie
para ninguém nem o publique em um repositório.

**Uma inicialização por processo.** Experimentos realizados durante o desenvolvimento mostraram
que, após um `DLLFinalize`,
uma nova chamada a `DLLInitializeLogin` no **mesmo processo** retorna `NL_OK` mas nunca
completa: apenas o estado de login chega, e roteamento, market data e ativação não retornam.
Para reconectar, inicie um processo novo.

---

## Estrutura

```
DLLNelogica.sln
├── .editorconfig                  namespaces e regras dos analisadores
├── CHANGELOG.md                   evolução por aula e marcos intermediários
├── LICENSE                        MIT — cobre o código deste projeto
├── Directory.Build.props          perfil estrito compartilhado pela solução
├── CodeMetricsConfig.txt          limites de complexidade e acoplamento
├── THIRD-PARTY-NOTICES.md         titularidade e redistribuição da ProfitDLL
└── src/
    ├── Program.cs                  composition root
    ├── appsettings.json            credenciais (preencha)
    ├── ProfitDLL.dll               biblioteca nativa da Nelogica
    ├── Application/                execução, console e encerramento
    ├── Configuration/              leitura e validação do JSON
    ├── Connection/                 estados, fila e máquina de conexão
    ├── Interop/                    P/Invoke, sessão, callbacks e guardas de processo
    ├── Logging/                    fila assíncrona, tee e arquivo diário
    └── MarketData/                 assinaturas, canais de cotação e métricas
```

Em tempo de execução, ao lado do executável, aparecem ainda a pasta `log/` (o relatório da
aplicação) e os artefatos da própria ProfitDLL — nenhum deles versionado.

A camada `Interop/` importa **apenas** o necessário para o ciclo de vida da conexão e para o
Market Data: `DLLInitializeLogin`, `DLLFinalize`, `SetChangeCotationCallback`,
`SetInvalidTickerCallback`, `SubscribeTicker`, `UnsubscribeTicker`, os 13 delegates exigidos
pelas assinaturas, o struct `TAssetID` e o enum `NResult`. As sete instâncias de delegate
usadas pela aplicação ficam enraizadas em `ProfitCallbackRoots` até o processo terminar. Nada
de ordens ou posições.

Continua proibido fazer I/O, bloquear ou executar regra de negócio diretamente na thread de
callback. Os callbacks de cotação e de ticker inválido apenas publicam em canais e retornam;
todo o consumo acontece em `MarketData/`, fora da thread nativa.

---

## Licença

O **código deste projeto** é distribuído sob a licença [MIT](LICENSE): você pode copiar,
adaptar e usar, inclusive comercialmente, mantendo o aviso de copyright.

A licença MIT **não se estende à `ProfitDLL.dll`**, que pertence à Nelogica e permanece
sujeita aos termos dela — veja [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

---

## Sobre a ProfitDLL

A `ProfitDLL.dll` é propriedade da **Nelogica** e está sujeita aos termos de licenciamento
dela. Este projeto não concede nenhum direito sobre a biblioteca e não a modifica.

**A biblioteca é distribuída publicamente pela Nelogica e a sua redistribuição é
permitida.** Por isso a `ProfitDLL.dll` acompanha este repositório, em `src/`, para que o
exemplo compile e execute sem depender de um download externo.

Ter o binário não substitui a licença: o uso exige conta Nelogica com licença ativa. Para a
versão oficial mais recente, a documentação e o suporte, procure a Nelogica diretamente.

As atribuições de terceiros estão em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

---

## Dúvidas

Ficou com dúvida sobre qualquer parte do código ou do funcionamento da DLL? Entre em contato:

**Marcelo Coutinho**
📧 mcoutinho@youtrade.pro.br

---

*Projeto educacional. Sem garantias. Use por sua conta e risco.*
