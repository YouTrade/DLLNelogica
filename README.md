# DLLNelogica — Projeto Educacional

> Série **Programando o seu robô de trading com a DLL da Nelogica** — **Aula 02**

Exemplo didático em C# que demonstra, do zero, como estabelecer uma conexão com a
**ProfitDLL da Nelogica**: autenticar, confirmar que todos os serviços subiram e finalizar
a sessão de forma limpa.

Este material foi escrito para ensino. O objetivo é que você entenda **cada decisão** —
por que um callback não pode bloquear, por que um retorno `NL_OK` não significa "conectado",
por que um estado precisa ser travado e não reavaliado.

---

## Aula 02 — antes do Market Data, é preciso provar o que o sistema está fazendo

Na **Aula 03** entra o **Market Data**. Mas receber dados de mercado sem conseguir registrar
de forma determinística *o que* chegou, *quando* chegou e em *qual estado* a aplicação estava
seria construir a etapa seguinte sem base para observação e diagnóstico.

Por isso a Aula 02 é preparatória: aqui a aplicação ganhou um **relatório diário** e uma
instrumentação mais completa do próprio ciclo de vida da conexão. A partir de agora ficam
registrados:

- a inicialização do arquivo diário;
- o retorno de `DLLInitializeLogin`;
- a evolução dos estados de conexão, um a um;
- a confirmação dos quatro estados obrigatórios;
- a solicitação de encerramento pelo usuário;
- a finalização dos serviços e o retorno de `DLLFinalize`.

Trecho de uma execução real:

```
2026-08-26 13:56:21 [DLLNelogica] DLLInitializeLogin retornou NL_OK; aguardando os estados de conexão.
2026-08-26 13:56:23 [DLLNelogica] Conexão confirmada pelos quatro estados obrigatórios.
2026-08-26 13:57:51 [DLLNelogica] Encerramento solicitado pelo usuário.
2026-08-26 13:57:52 [DLLNelogica] DLLFinalize retornou 0 (0x00000000).
```

Pode parecer detalhe. Não é. Primeiro uma base confiável — depois o mercado passando por
dentro dela.

---

## ⚠️ Leia antes de usar

**Este projeto está incompleto e não possui mecanismos de segurança.** Ele existe para
estudo, não para produção.

O que ele **não** tem:

- Nenhuma proteção de credenciais — elas ficam em texto puro no `appsettings.json`
- Nenhuma reconexão automática, retentativa ou recuperação de falha
- Nenhum tratamento de ordens, posições, contas ou dados de mercado
- Nenhuma auditoria, persistência de dados ou monitoramento
- Nenhuma suíte automatizada de testes — a validação disponível é compilação estrita e execução manual
- Nenhuma retenção ou expurgo do relatório — os arquivos diários se acumulam indefinidamente

**Não use este código para operar dinheiro real.** Use-o para aprender como a interoperabilidade
com a ProfitDLL funciona e depois construa o seu, com os cuidados que a sua operação exige.

---

## O que o projeto faz

1. Abre o relatório diário em `log/AAAAMMDD.log`, ao lado do executável
2. Lê as credenciais de `src/appsettings.json`
3. Carrega a `ProfitDLL.dll` (Win64) explicitamente do diretório da aplicação
4. Chama `DLLInitializeLogin` uma única vez
5. Publica os estados recebidos em uma fila e os processa fora da thread nativa
6. Registra cada estado antes de aplicar a transição que ele causa
7. Anuncia a conexão apenas quando os quatro estiverem satisfeitos
8. Mantém o processo vivo até `Ctrl+C`
9. Chama `DLLFinalize` e drena os eventos pendentes antes de sair

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
- **Flush periódico**: o gravador descarrega o arquivo no máximo a cada segundo e também
  durante `Flush` explícito e encerramento ordenado. Uma queda abrupta pode perder o último intervalo.
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
- `ProfitDLL.dll` versão **4.0.0.41**, variante **Win64**
- Conta Nelogica com **roteamento habilitado** e licença ativa

> A DLL de 32 bits **não funciona** neste projeto. O processo é compilado como x64 e a
> arquitetura precisa coincidir.

---

## Como executar

**1. Preencha as credenciais** em `src/appsettings.json`:

```json
{
  "Credenciais": {
    "Key": "sua-chave-de-ativacao",
    "User": "seu-usuario",
    "Password": "sua-senha"
  }
}
```

**2. Confirme que `src/ProfitDLL.dll` é a versão Win64.**

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
├── Directory.Build.props          perfil estrito compartilhado pela solução
├── CodeMetricsConfig.txt          limites de complexidade e acoplamento
├── src/
│   ├── Program.cs                  composition root
│   ├── appsettings.json            credenciais (preencha)
│   ├── ProfitDLL.dll               biblioteca nativa da Nelogica
│   ├── Application/                execução, console e encerramento
│   ├── Configuration/              leitura e validação do JSON
│   ├── Connection/                 estados, fila e máquina de conexão
│   ├── Interop/                    P/Invoke, sessão, callbacks e guardas de processo
│   ├── Logging/                    fila assíncrona, tee e arquivo diário
│   └── MarketData/                 política de canal limitado para a Aula 03
```

Em tempo de execução, ao lado do executável, aparecem ainda a pasta `log/` (o relatório da
aplicação) e os artefatos da própria ProfitDLL — nenhum deles versionado.

A camada `Interop/` importa **apenas** o necessário para o ciclo de vida da conexão:
`DLLInitializeLogin`, `DLLFinalize`, os 11 delegates exigidos pela assinatura, o struct
`TAssetID` e o enum `NResult`. As cinco instâncias de delegate usadas pela aplicação ficam
enraizadas em `ProfitCallbackRoots` até o processo terminar. Nada de ordens ou posições.

Os callbacks de market data ainda não processam conteúdo. `MarketDataChannel` prepara canais
limitados com publicação não bloqueante e contador de rejeições; na Aula 03, cada tipo de
evento ainda deverá definir capacidade e tratamento de overflow próprios. É proibido fazer
I/O, bloquear ou executar regra de negócio diretamente na thread de callback.

---

## Sobre a ProfitDLL

A `ProfitDLL.dll` é propriedade da **Nelogica** e está sujeita aos termos de licenciamento
dela. Este projeto não concede nenhum direito sobre a biblioteca. Para obter a versão
oficial, a documentação e o suporte, procure a Nelogica diretamente.

---

## Dúvidas

Ficou com dúvida sobre qualquer parte do código ou do funcionamento da DLL? Entre em contato:

**Marcelo Coutinho**
📧 mcoutinho@youtrade.pro.br

---

*Projeto educacional. Sem garantias. Use por sua conta e risco.*
