# 0004 — Testes primeiro (red → green)

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

O pacote não tem nenhum teste nem asmdef de teste (`Documentation~/planos/gas-estudo-e-roadmap.md`, seção D.1). O tempo vem direto de `UnityEngine.Time` (`Runtime/GAS/Core/AbilitySystemComponent.cs:56`, `Runtime/GAS/Effects/ActiveEffectInstance.cs:31`), o que impede testes determinísticos.

O projeto host local na raiz do repositório (pastas `Assets/`, `Packages/`, `ProjectSettings/`, todas no `.gitignore`) usa a Unity 6000.3.5f2. O `Packages/manifest.json` dele não referencia o `com.spellbook` nem declara o pacote como testável, e o Odin não está instalado nele. Hoje, portanto, nenhum teste do pacote rodaria.

## Decisão

1. Todo ticket lista na Spec os testes red. A Fábrica:
   1. escreve esses testes primeiro;
   2. roda e mostra que **falham pelo motivo esperado** (não por erro de compilação de outra coisa);
   3. escreve o mínimo de código para passarem;
   4. roda os testes do ticket e depois a suíte inteira do pacote.
2. Bug: primeiro o teste que reproduz o bug falhando, depois a correção.
3. Ferramenta: Unity Test Framework (NUnit). EditMode por padrão; PlayMode só quando o comportamento depende de frame, física ou cena.
4. Os testes ficam no pacote, em `Tests/<Módulo>/EditMode` e `Tests/<Módulo>/PlayMode`, com os asmdefs `SpellBook.<Módulo>.Tests.EditMode` e, quando preciso, `SpellBook.<Módulo>.Tests.PlayMode` (decisão I27). Eles referenciam o módulo testado, o `SpellBook.Time` (0014) e o assembly de factories `SpellBook.Testing` (`Tests/Testing/`, sem testes, só factories).
5. Testes determinísticos: tempo por `ManualTimeSource` (0014), aleatoriedade com semente injetada, nada de rede. Dados montados por factories pequenas; fixtures de asset só quando o formato serializado é o que se testa.
6. Um teste EditMode roda todos os validadores de asset (0007) e falha se algum acusar erro.
7. O Examinador não aprova ticket sem rodar os testes e vê-los verdes.

## Motivo

- O teste red prova que o teste testa algo; o green prova que o código entrega o critério de pronto.
- Com o tempo injetável e testes EditMode, a suíte é rápida e roda em batchmode.

## Consequências

- O primeiro épico (Fase 0-M) precisa criar a infraestrutura de teste antes de qualquer comportamento: asmdefs de teste, `ManualTimeSource` e um projeto host capaz de rodar a suíte.
- O projeto host precisa referenciar o pacote e listá-lo em `testables`, e ter o Odin instalado (0011).

## O que os agentes NÃO podem fazer

- Escrever código de produção antes do teste red do ticket.
- Apagar, marcar com `[Ignore]`, afrouxar asserção ou aumentar tolerância para um teste passar.
- Usar `Time.time`, `Time.deltaTime`, `Random` sem semente, `Thread.Sleep` ou espera real em teste.
- Fazer um teste depender de outro ou de ordem de execução.
- Dar o ticket como pronto, ou aprová-lo, sem a saída dos testes.

## Decisões incorporadas (2026-10-29)

- **Host de testes em `HostProject~/`** (decisão I41, que substitui a I32 "raiz atual"). Motivo técnico: a raiz do repositório é ao mesmo tempo a pasta do pacote (`package.json` na raiz) e a do projeto host. Um `"com.spellbook": "file:.."` no `Packages/manifest.json` da raiz faria a Unity importar como conteúdo do pacote as pastas `Assets/`, `Library/`, `Packages/` e `ProjectSettings/` do próprio host: `Library/PackageCache` traria asmdefs duplicados e os `.meta` de `Assets/` apareceriam duas vezes com o mesmo GUID. A Unity só ignora, dentro de um pacote, pastas terminadas em `~` ou começadas por `.`. Por isso o host fica em **`HostProject~/`**, dentro do repositório:
  - `HostProject~/Packages/manifest.json` com o pacote por `file:` apontando para a raiz do repositório e `"testables": ["com.spellbook"]`, versionado. A I41 escreve `file:../`; como a Unity resolve o caminho relativo a partir da pasta `Packages/` do projeto, o valor esperado é `file:../..`. O ticket 0M-06 confirma qual dos dois resolve para a raiz;
  - `HostProject~/ProjectSettings/ProjectVersion.txt` versionado; `Library/`, `Logs/`, `Temp/`, `UserSettings/` e o resto de `ProjectSettings/` no `.gitignore`;
  - o Odin é importado localmente em `HostProject~/Assets/Plugins/Sirenix`, que fica no `.gitignore` (o espírito da I32 se mantém: Odin local, nunca versionado).
  
  O host antigo na raiz (pastas já ignoradas pelo git) não é apagado por nenhum ticket. (Emendado em 2026-10-29; ver a emenda do host antigo abaixo.)
- **Odin no host e no CI:** local no host; no CI, baixado de um local privado por secret (0025, decisão I33), condicionado à licença do Odin (PRD P21).
- ~~**CI:** existe, com GameCI, na 6000.0 e na última 6000.x (decisão I30, 0010). A licença da Unity entra como secret do repositório (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`).~~ (Revogado pela emenda "sem CI de testes" abaixo.)
- **Testes de ciclo de vida** (decisão técnica): em EditMode, `AddComponent` não chama `Awake`/`OnEnable`. Por isso testes que dependem de `Awake`, `OnEnable`, `OnDisable` ou `Update` (ASC, `EventListener`, `Projectile`) ficam no asmdef `PlayMode`; o código de produção não ganha inicialização preguiçosa só para caber em EditMode.
- **Testes de guarda:** testes estruturais que verificam uma regra já cumprida (por exemplo, nenhum runtime referencia `UnityEditor`) não têm red natural. A Spec os marca como "guarda"; o red é mostrado uma vez apontando o teste para um alvo que viola a regra (por exemplo, um asmdef temporário), sem commitar esse alvo.

## Emenda (2026-10-29): sem Odin, pela 0026

Esta emenda vale quando a 0026 for aceita. As decisões I45 a I47 do usuário substituem a 0011.

- **Host e CI:** o host de testes (`HostProject~/`) e o CI **não** têm o Odin. Deixam de valer:
  - a frase "ter o Odin instalado (0011)" nas Consequências;
  - o item "o Odin é importado localmente em `HostProject~/Assets/Plugins/Sirenix`";
  - o item "Odin no host e no CI".

  Nada do Odin vai para o `.gitignore`, e o CI não baixa nada privado.
- **Red de compilação:** a remoção do Odin (ticket 0M-44) acontece enquanto o host ainda não compila o pacote. Nesse caso, o red do ticket é a lista de erros de compilação `Sirenix` registrada no ticket anterior (0M-06), e o green é o host compilando sem erro do SpellBook. É o único ticket da 0-M com red de compilação. O teste de estrutura `NoOdinTests` vem no ticket seguinte (0M-07) como guarda.
- **Validadores** (item 6 da Decisão): os validadores são os da interface `IValidatable` (0026), não o `ISelfValidator`.

## Emenda (2026-10-29): host antigo fora do repositório

Decisão do usuário, a partir do 0M-06: o `file:../..` do host aponta para a raiz do repositório, que é a pasta do pacote. Ali ainda estava o host antigo (`Assets/`, `Library/`, `Packages/`, `ProjectSettings/`, `UserSettings/`, `Logs/`), ignorado pelo git mas presente no disco, e a Unity o importaria como conteúdo do `com.spellbook`. O host antigo foi movido, sem ser apagado, para `C:/Users/gabri/MeusProjetos/SpellBook-host-antigo/`. A frase "o host antigo na raiz não é apagado por nenhum ticket" continua valendo para a cópia movida. Regra nova: a raiz do repositório não pode ter projeto Unity, porque ela é a pasta do pacote; o único host é o `HostProject~/`.

## Emenda (2026-10-29): sem CI de testes

Decisão do usuário (2026-10-29, emenda da 0010 com o mesmo nome): o item "**CI:** existe, com GameCI, na 6000.0 e na última 6000.x" deixa de valer. Não há workflow de testes nem secrets da Unity no repositório. A prova de um ticket é a saída da suíte no host `HostProject~/`, rodada pela Fábrica e conferida pelo Examinador. O resto desta ADR não muda.

## Em aberto

Nada.

## Relacionadas

- 0003 (ticket), 0005 (editor-lock), 0007 (validadores), 0011 (Odin), 0014 (tempo injetável).
