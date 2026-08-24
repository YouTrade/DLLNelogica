# DLLNelogica — Projeto Educacional

Exemplo didático em C# que demonstra, do zero, como estabelecer uma conexão com a
**ProfitDLL da Nelogica**: autenticar, confirmar que todos os serviços subiram e finalizar
a sessão de forma limpa.

Este material foi escrito para ensino. O objetivo é que você entenda **cada decisão** —
por que um callback não pode bloquear, por que um retorno `NL_OK` não significa "conectado",
por que um estado precisa ser travado e não reavaliado.

---

## ⚠️ Leia antes de usar

**Este projeto está incompleto e não possui mecanismos de segurança.** Ele existe para
estudo, não para produção.

O que ele **não** tem:

- Nenhuma proteção de credenciais — elas ficam em texto puro no `appsettings.json`
- Nenhuma reconexão automática, retentativa ou recuperação de falha
- Nenhum tratamento de ordens, posições, contas ou dados de mercado
- Nenhuma auditoria, persistência ou monitoramento

**Não use este código para operar dinheiro real.** Use-o para aprender como a interoperabilidade
com a ProfitDLL funciona e depois construa o seu, com os cuidados que a sua operação exige.

---

## O que o projeto faz

1. Lê as credenciais de `src/appsettings.json`
2. Carrega a `ProfitDLL.dll` (Win64) explicitamente do diretório da aplicação
3. Chama `DLLInitializeLogin` uma única vez
4. Acompanha os **quatro estados** que a DLL informa por callback
5. Anuncia a conexão apenas quando os quatro estiverem satisfeitos
6. Mantém o processo vivo até `Ctrl+C`
7. Chama `DLLFinalize` e drena os eventos pendentes antes de sair

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

### Suíte de testes

```
dotnet run --project tests/DLLNelogica.StateTests.csproj -c Release
```

São 24 cenários determinísticos da máquina de estados. **Não** carregam a DLL, não acessam
o mercado e não usam rede — rodam em qualquer máquina, a qualquer hora, sem credenciais.

---

## Cuidados importantes

**Nunca versione o `appsettings.json` preenchido.** Se for colocar este projeto em um
repositório Git, crie um `.gitignore` antes do primeiro commit contendo pelo menos:

```
appsettings.json
ProfitDLL.dll
Logs/
database/
PopupManagerV2/
*.log
bin/
obj/
.vs/
```

Credenciais commitadas continuam no histórico mesmo depois de apagadas do arquivo.

**A ProfitDLL escreve arquivos no diretório de trabalho.** Ao inicializar, ela cria
`Logs/`, `database/`, `PopupManagerV2/`, algumas DLLs do OpenSSL e arquivos `.dat`. Isso é
esperado — só não deixe esses artefatos entrarem no seu controle de versão.

**Atenção ao `Erro.log`.** Em caso de falha, a ProfitDLL pode gravar um arquivo de erro que
**contém a sua chave de ativação em texto puro**. Se ele aparecer, apague — e nunca o envie
para ninguém nem o publique em um repositório.

**Uma inicialização por processo.** Testes deste projeto mostraram que, após um `DLLFinalize`,
uma nova chamada a `DLLInitializeLogin` no **mesmo processo** retorna `NL_OK` mas nunca
completa: apenas o estado de login chega, e roteamento, market data e ativação não retornam.
Para reconectar, inicie um processo novo.

---

## Estrutura

```
DLLNelogica.sln
├── src/
│   ├── Program.cs                  fluxo principal: config, conexão, encerramento
│   ├── appsettings.json            credenciais (preencha)
│   ├── ProfitDLL.dll               biblioteca nativa da Nelogica
│   ├── Configuration/              leitura e validação do JSON
│   ├── Connection/                 máquina de estados da conexão
│   ├── Interop/                    P/Invoke, delegates e tipos nativos
│   └── Properties/
└── tests/                          suíte determinística (24 cenários)
```

A camada `Interop/` importa **apenas** o necessário para o ciclo de vida da conexão:
`DLLInitializeLogin`, `DLLFinalize`, os 11 delegates exigidos pela assinatura, o struct
`TAssetID` e o enum `NResult`. Nada de ordens, posições ou livro.

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
