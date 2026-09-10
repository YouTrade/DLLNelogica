# DLLNelogica — Projeto Educacional

> Série **Programando o seu robô de trading com a DLL da Nelogica** — **Aula 03 concluída,
> com retrofit de observabilidade pós-aula: consumo de Market Data e relatórios por
> instrumento.**

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
| Retrofit pós-Aula 03 | 2026-09-10 | Relatórios por instrumento e visão contínua do mercado |

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

### O que a primeira execução em pregão revelou

A aula foi validada duas vezes, e a diferença entre elas ensina sozinha. Com o mercado fechado,
cada instrumento entregou **uma** cotação — a última do pregão anterior, em cache. Com o mercado
aberto, os mesmos quatro instrumentos entregaram **1.325 cotações em 55 segundos**.

Foi aí que ficou claro que registrar market data num arquivo único não se sustentava, e que a
aplicação não tinha nenhuma visão do mercado enquanto rodava. É o que o retrofit desta aula
resolveu, descrito em [Os relatórios do dia](#os-relatórios-do-dia).

---

## ⚠️ Leia antes de usar

**Este projeto está incompleto e não possui mecanismos de segurança.** Ele existe para
estudo, não para produção.

O que ele **não** tem:

- Nenhuma proteção de credenciais — elas ficam em texto puro no `appsettings.json`
- Nenhuma reconexão automática, retentativa ou recuperação de falha
- Nenhum tratamento de ordens, posições ou contas
- Nenhuma análise das cotações — elas são registradas em arquivo, não interpretadas
- Nenhum banco de dados, auditoria ou monitoramento
- Nenhuma suíte automatizada de testes — a validação disponível é compilação estrita e execução manual
- Nenhuma retenção ou expurgo dos relatórios — eles se acumulam indefinidamente, e passam de
  70 MB por pregão

**Não use este código para operar dinheiro real.** Use-o para aprender como a interoperabilidade
com a ProfitDLL funciona e depois construa o seu, com os cuidados que a sua operação exige.

---

## O que o projeto faz

1. Abre o diretório do dia em `Relatorios/AAAAMMDD/`, ao lado do executável
2. Lê as credenciais e a lista de instrumentos de `src/appsettings.json`
3. Carrega a `ProfitDLL.dll` (Win64) explicitamente do diretório da aplicação
4. Chama `DLLInitializeLogin` uma única vez
5. Registra os callbacks de cotação e de ticker inválido
6. Publica os estados recebidos em uma fila e os processa fora da thread nativa
7. Registra cada estado antes de aplicar a transição que ele causa
8. Anuncia a conexão apenas quando os quatro estiverem satisfeitos
9. Assina todos os instrumentos configurados, ou desfaz o que já assinou e encerra
10. Consome as cotações em segundo plano, gravando cada uma no arquivo do seu instrumento
11. Publica uma amostra do mercado a cada `ReportIntervalSeconds`
12. Mantém o processo vivo até `Ctrl+C`
13. Desassina os instrumentos em ordem reversa
14. Chama `DLLFinalize` e drena os eventos pendentes antes de sair

Cada um desses passos deixa rastro nos relatórios do dia.

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

## Os relatórios do dia

Assim que o Market Data ligou, o registro em arquivo único parou de servir. Uma execução de
55 segundos com quatro instrumentos produziu **1.325 cotações**. Despejadas no mesmo arquivo do
relato da sessão, elas soterrariam as sete linhas que explicam se a conexão subiu.

A saída é separar por **destino**, não por importância:

```
<diretório do executável>/
└── Relatorios/
    └── 20260910/
        ├── _Sessao.txt      conexão, assinaturas, falhas, encerramento
        ├── _Resumo.txt      uma amostra do mercado por intervalo
        ├── WINV26_F.txt     tick a tick
        ├── WDOV26_F.txt
        ├── PETR4_B.txt
        └── VALE3_B.txt
```

Um diretório por dia, criado sozinho na virada, sem reiniciar a aplicação.

### Por que um arquivo por instrumento

Porque as perguntas que você faz a um log de mercado são quase sempre sobre **um** ativo.
"Que preço o WIN estava marcando às 13:25?" não deveria exigir filtrar 1.208 linhas dele no
meio de 1.325. Separados, cada arquivo abre no Excel, entra num `tail -f` e é lido por um
script sem nenhum pré-processamento.

Há um ganho silencioso: cada arquivo tem seu próprio gravador, então instrumentos diferentes
não disputam a mesma posição de escrita.

### As três camadas de detalhe

| Destino | Granularidade | Vai ao console? |
|---------|---------------|-----------------|
| `_Sessao.txt` | acontecimentos | sim |
| `_Resumo.txt` | uma linha por intervalo | sim |
| `<TICKER>_<BOLSA>.txt` | uma linha por cotação | **não** |

O tick **não** vai ao console de propósito. A dezenas de linhas por segundo a tela deixa de ser
legível — e um `Console.WriteLine` por cotação seria I/O na thread que precisa esvaziar a fila.
Quem dá a visão ao vivo é o `_Resumo.txt`, controlado por `ReportIntervalSeconds`:

```
13:24:57 Market data | WINV26:F 189635 (37) | WDOV26:F 5121,5 (7) | PETR4:B 49,19 (0) | VALE3:B 77,79 (0) | descartadas=0
13:25:01 Market data | WINV26:F 189625 (87) | WDOV26:F 5122,5 (15) | PETR4:B 49,2 (1)  | VALE3:B 77,8 (0)  | descartadas=0
```

O número entre parênteses é quanto aquele instrumento negociou **naquele intervalo**. Dá para
ver o mercado respirar: o WIN pulsando de 1 a 87 negócios por segundo enquanto PETR4 passa
segundos inteiros parada. E `(0)` diz algo que a ausência de linha não diria — o instrumento
está vivo e assinado, apenas não negociou.

### O que uma linha de tick carrega

```
13:24:56.884 pwcDate=10/09/2026 13:24:56.692 | sequência=44491250 | chegada=1 | preço=189640
```

São **dois relógios na mesma linha**, e é isso que a torna interessante. O primeiro é a hora em
que a cotação chegou ao nosso consumidor; o `pwcDate` é a hora que a bolsa carimbou no negócio.
A diferença — 192 ms aqui, e entre 100 e 200 ms de forma consistente — é a latência real do
caminho B3 → Nelogica → sua aplicação. Nenhum código foi escrito para medir isso; a medida
apareceu porque os dois carimbos ficaram lado a lado.

O campo `chegada` é a ordem global entre **todos** os instrumentos. Cruzando os arquivos por
esse número você reconstrói a sequência real em que os eventos entraram, mesmo estando em
arquivos diferentes.

### O registro nunca segura a aplicação

Esta é a regra que sustenta todo o resto. `Console.Out` e `Console.Error` são redirecionados
para um *tee*, e os destinos nomeados têm uma porta própria — mas **os dois caminhos apenas
enfileiram**. Uma única thread gravadora consome a fila e escreve em disco.

Isso não é preciosismo. O consumidor de cotações lê de um canal limitado; se ele parasse para
esperar o disco, a fila encheria, o produtor começaria a descartar, e o campo `descartadas`
passaria a contar perdas causadas **pelo próprio log**. A métrica que existe para provar a
saúde do pipeline viraria mentira.

O teste em pregão confirma que a conta fecha: 1.208 + 92 + 20 + 5 linhas nos arquivos de
instrumento somam exatamente as 1.325 cotações que o resumo final reporta, com `descartadas=0`.

- **Flush por lote**: o gravador descarrega os arquivos depois de cada lote consumido. Um timer
  de um segundo cobre períodos ociosos; esperas síncronas têm limite de dois segundos para que
  um disco travado não congele o processo. Uma queda abrupta ainda pode perder o lote em andamento.
- **Callbacks não fazem I/O**: a thread nativa apenas publica eventos e retorna.
- **`stdout` e `stderr` no mesmo `_Sessao.txt`**, na ordem em que entram na fila compartilhada.
- **Falhas não tratadas entram no relatório** com tipo, mensagem e *stack trace* — o runtime
  imprimiria isso fora do `Console.Error`, e o registro se perderia.
- **UTF-8 sem BOM**, com acentuação preservada.
- **Um gravador por arquivo.** Uma segunda instância no mesmo diretório não sobrescreve o
  relatório da primeira: ela avisa e segue apenas com o console.
- **O ticker vira nome de arquivo**, então é higienizado antes de tocar o disco: separadores de
  caminho e caracteres reservados viram sublinhado.
- Se a pasta não puder ser criada, a aplicação **avisa e continua** — a ausência de relatório
  nunca derruba a execução.

> **⚠️ Os arquivos crescem, e ninguém os apaga.**
>
> Aqueles 55 segundos geraram 138 KB. Um pregão inteiro nesse ritmo passa de **70 MB por dia**,
> e um dia volátil com mais instrumentos vai muito além. Não há retenção nem expurgo: essa
> política depende do seu ambiente, e implementá-la é um bom primeiro exercício sobre este
> código.

> **`Relatorios/` é da aplicação. `Logs/` é da ProfitDLL.**
>
> A DLL da Nelogica grava os próprios arquivos em uma pasta `Logs/` ao lado do executável
> (`LogDesktop`, `LogStructuredBlb`, `LogPerf` e outros). Em poucos minutos de operação eles
> passam facilmente das dezenas de MB. Nomes distintos mantêm o seu relatório separado desse
> volume.

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

`ReportIntervalSeconds` é de quanto em quanto tempo sai a linha do `_Resumo.txt`. Com `1` você
acompanha o mercado ao vivo; valores maiores reduzem o ruído sem afetar em nada o registro
tick a tick, que é independente desse intervalo.

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

**5. Confira os relatórios.** O diretório do dia fica ao lado do executável — com `dotnet run`,
em `src/bin/<plataforma>/<configuração>/net9.0/Relatorios/AAAAMMDD/`. Comece pelo `_Sessao.txt`
para saber se a conexão subiu, abra o `_Resumo.txt` para ver o mercado, e vá ao arquivo do
instrumento quando precisar do tick exato.

## Cuidados importantes

**Nunca versione o `appsettings.json` preenchido.** O repositório já traz um `.gitignore`
que mantém fora do controle de versão a saída de compilação (`bin/`, `obj/`), os arquivos da
IDE (`.vs/`), os relatórios da aplicação (`Relatorios/`), os artefatos da ProfitDLL (`Logs/`,
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
    ├── Logging/                    fila assíncrona, tee e relatórios do dia
    └── MarketData/                 assinaturas, canais de cotação, métricas e amostragem
```

Em tempo de execução, ao lado do executável, aparecem ainda a pasta `Relatorios/` (os arquivos
do dia) e os artefatos da própria ProfitDLL — nenhum deles versionado.

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
