# 0027 — Estrutura do repositório: projeto Unity na raiz e pacote embutido em `Packages/com.spellbook/`

Status: Aceita (gate 2026-10-30)
Data: 2026-10-30

> Aceita no gate de 2026-10-30: **alternativa A**. As respostas às perguntas do gate e o que elas mudam no texto proposto estão em "Decisões do gate (2026-10-30)" e em "Decisões do usuário sobre as perguntas abertas (2026-10-30)", no fim. Onde essas seções divergem do corpo (item 6 da Decisão, a exceção de contagem do M1 e o item de `Runtime/Systems` do 0M-40), valem elas.

## Contexto

Hoje o repositório tem duas coisas na mesma raiz: a pasta do pacote UPM e o projeto host de testes.

- **Pacote UPM:** `package.json`, `Runtime/`, `Editor/`, `Tests/`, `README.md` e `CHANGELOG.md`.
- **Projeto host de testes:** fica em `HostProject~/` (0004, decisão I41). Estão versionados o `Packages/manifest.json` (com `"com.spellbook": "file:../.."` e `testables`), o `packages-lock.json`, o `ProjectSettings/ProjectVersion.txt` e o `Assets/.gitkeep`.

O host ganhou o til porque a Unity importaria como conteúdo do pacote qualquer projeto que estivesse na pasta do pacote (0004, emenda do host antigo). Pela mesma regra, a raiz do repositório não pode ter projeto Unity.

O usuário estranha versionar uma pasta `HostProject~/`, e o layout atual tem outros custos:

1. **O usuário do pacote leva o que não é dele.** A instalação por URL do git baixa a raiz inteira para o `PackageCache` do usuário. Isso inclui `Documentation~/` (PRDs, Specs, ADRs, backlog, `TIME.md`, manual) e `HostProject~/`. O `CLAUDE.md` tem `.meta` (`CLAUDE.md.meta`) e por isso é importado como asset do pacote. As pastas com til e com ponto não geram asset, mas os arquivos são baixados mesmo assim.
2. **O host é "meio versionado".** O `.gitignore` tem 7 regras só para ele (`.gitignore:15-22`). O `ProjectSettings/` dele não é versionado, então cada máquina tem configurações diferentes. Os restos de PlayMode interrompido, como `HostProject~/Assets/InitTestScene*.unity` (B-026), não estão cobertos.
3. **Caminhos frágeis.** O `file:../..`, o `-projectPath HostProject~` e os logs relativos do batchmode (B-025) são exemplos. O cenário C1 do 0M-40 também precisa morar dentro de `HostProject~/Assets/`.

### Referências estudadas (2026-10-30)

| Referência | O que diz sobre o caso |
|---|---|
| angry-shark-studio, "create Unity package / GitHub / UPM" | É a única que trata de pacote. Regra: o pacote sempre se desenvolve dentro de um projeto Unity, que compila, testa e gera os `.meta`. Mostra três layouts: (a) pacote dentro do projeto, instalado com `?path=`; (b) "Opção A", o pacote na raiz do repositório, com URL limpa; (c) "Opção B", monorepo com `PackageDevProject/` ao lado de `com.x.package/`, descrito como "clean separation between package and development project". Versiona `.meta`, `package.json`, `Runtime/`, `Editor/`, `Tests/`, `Samples~` e `Documentation~`, e ignora `Library/`, `Temp/` e `obj/`. |
| unity.com, "Organizing your project" (guia oficial) | Não versionar o que é gerado. Versionar os `.meta`. Usar `.keep` em pasta vazia. Separar os assets próprios dos de terceiros. Poucas pastas na raiz de `Assets/`. Ter uma sandbox separada. |
| getcreativetoday, "Project organization" | Organização por tipo de asset para projetos pequenos e por feature para os grandes. Nenhuma pasta vazia. |
| discussions.unity.com, "Best practices folder structure" (2010–2017) | Não há consenso. O meio-termo mais citado é feature ou categoria na raiz e tipo de asset dentro. |
| github `themorfeus/unity_project_structure` (Unity 2018.3) | Tudo em `Assets/_Codename/`, por tipo, com pastas de conteúdo nos projetos grandes. Traz `.gitignore` e `.gitattributes` de exemplo, que ignoram `Library/`, `Temp/`, `obj/` e `Build/`. |

Quatro das cinco referências tratam da organização de **jogo** dentro de `Assets/`, que não se aplica a um pacote. O que se aproveita delas:

- versionar os `.meta` e as configurações do projeto;
- ignorar o que é gerado (`Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/`, builds), como o template oficial do GitHub para Unity;
- não deixar pastas vazias.

A organização **por módulo** do pacote (`Runtime/<Módulo>/`, `Editor/<Módulo>/`, `Tests/<Módulo>/`, 0012) é a forma "por feature" que as referências recomendam para projetos com fronteiras claras, e não muda em nenhuma alternativa.

Um fato técnico decide a comparação. Os testes leem os arquivos do pacote pelo caminho real, `PackageInfo.FindForAssembly(...).resolvedPath` (`Tests/Core/EditMode/Structure/PackageFiles.cs:22-29`, `Tests/GAS/EditMode/GasSourceScanTests.cs:46-53`), e não pela raiz do repositório. Os caminhos que eles verificam (`"Runtime/GAS/SpellBook.GAS.asmdef"`, `AsmdefRulesTests.cs:148`) são relativos à pasta do pacote. Por isso, mudar a pasta do pacote de lugar não muda nenhum teste.

## Alternativas

### A — Projeto Unity na raiz e pacote embutido em `Packages/com.spellbook/` (recomendada)

```
SpellBook/                          (raiz do repositório = projeto Unity de desenvolvimento)
  Assets/                           cenários de verificação (ex.: Scenarios/C1, 0M-40); .keep se vazio
  Packages/
    manifest.json                   test-framework, módulos, "testables": ["com.spellbook"]
    packages-lock.json
    com.spellbook/                  pacote embutido (embedded): tudo o que o usuário instala
      package.json, README.md, CHANGELOG.md, LICENSE (quando houver)
      Runtime/, Editor/, Tests/, Samples~/ (B-019)
  ProjectSettings/                  versionado inteiro (é o projeto de desenvolvimento)
  Documentation~/                   igual a hoje (docs internas e manual); fora do pacote
  .claude/, .github/, CLAUDE.md, README.md (do repositório)
```

- **Instalação pelo usuário:** a URL passa a ser `https://github.com/gabrielctorres/SpellBook.git?path=/Packages/com.spellbook`, com `#<tag>` opcional. O `?path=` existe desde a Unity 2019.3.4 e funciona na 6000.0 (0010). As tags 2.x continuam instaláveis pela URL antiga. A troca de URL é uma quebra de **instalação**: entra no CHANGELOG da 3.0 e na página Instalação. O usuário passa a baixar só o pacote, sem `Documentation~/`, host ou `CLAUDE.md`.
- **Batchmode e testes:** `-projectPath "$PWD"`, ou seja, a raiz. Os caminhos do B-025 viram `"$PWD/Logs/TestResults-<EditMode|PlayMode>.xml"` e `"$PWD/Logs/unity-tests.log"`. Nenhum teste muda (`resolvedPath`; para um pacote embutido ele é `Packages/com.spellbook`). O `testables` fica no manifest: é inofensivo para um pacote embutido e garante que os testes carreguem. O ticket de migração confirma que a suíte inteira continua com o mesmo número de testes.
- **Editor-lock (0005):** não muda. A trava continua em `.claude/editor.lock` na raiz, uma por checkout. Só o "caminho do host" escrito na trava passa a ser a raiz.
- **Tickets 0M-26 a 0M-40 (e 0M-45, 0M-49, 0M-50, que estão no meio deles):** o conteúdo não muda, só o prefixo. Uma emenda única na Spec diz que todo caminho de pacote (`Runtime/`, `Editor/`, `Tests/`, `package.json`, `README.md` do pacote, `CHANGELOG.md`) é relativo a `Packages/com.spellbook/` a partir do ticket de migração. Nenhuma linha de ticket é reescrita. Exceção: o 0M-40 troca `HostProject~/Assets/Scenarios/C1/...` por `Assets/Scenarios/C1/...`, e o item do 0M-40 que fala de `Runtime/Systems` não muda.
- **Tamanho da migração:** grande em arquivos e pequena em conteúdo. São cerca de 2 tickets:
  - **M1, movimento puro**, com `git mv` preservando os `.meta` e os GUIDs. Vão para `Packages/com.spellbook/`: `Runtime/`, `Editor/`, `Tests/`, `package.json`, `README.md` e `CHANGELOG.md`. O conteúdo de `HostProject~/` sobe para a raiz. O `CLAUDE.md.meta` é apagado, e o `.gitignore` passa a ser o padrão Unity. Todos os movimentos têm similaridade de 100% (`git diff -M --stat` só com `R100`), e a suíte inteira fica verde no host novo. Passa dos 10 arquivos da 0003, que conta o arquivo movido uma vez. Por isso precisa de **exceção aprovada no gate**, com o critério "só renomes R100, mais os ~4 arquivos editados listados".
  - **M2, caminhos dos papéis, sem Editor.** Edita `/implementar`, `/revisao`, `fabrica.md`, `examinador.md`, `unity-test-reader.md`, `unity-console-reader.md`, `unity-explorer.md` e os 3 `*-programmer.md` (`HostProject~` → raiz), além de `TIME.md`, `Documentation~/README.md` e `manual/inicio/instalacao.md` (URL com `?path=`). São cerca de 13 arquivos de texto, que podem virar 2 tickets pela regra dos 10 arquivos.
  - **Emendas:** 0004, 0005, 0001 (item 2: "raiz do pacote" passa a ser `Packages/com.spellbook/`), 0012 (item 4: pastas relativas ao pacote) e Spec (seção 4.1, D1, 0M-40).
  - **Momento:** depois do APROVADO do 0M-23, que está com mudanças não commitadas em `Runtime/Pooling/` e `Tests/Pooling/`, e antes do 0M-24, na branch `feat/0m-lote-19-em-diante`. Um `git mv` com trabalho pendente mistura os dois diffs.
  - **`Library/`:** a `HostProject~/Library/` pode ser movida para a raiz à mão, para evitar uma reimportação completa. É local e opcional.

### B — Pacote na raiz e host fora do git

O layout do pacote fica como está. `HostProject~/` sai do versionamento: entra `/HostProject~/` no `.gitignore` e saem os 4 arquivos versionados com `git rm --cached`. Uma variante coloca o host numa pasta irmã (`../SpellBook-host/`) com `file:../../SpellBook`.

- **Instalação pelo usuário:** não muda (URL sem `?path=`). O usuário continua baixando `Documentation~/` e importando o `CLAUDE.md`. Esse custo pode ser reduzido apagando o `CLAUDE.md.meta`, mas a Unity geraria o `.meta` de novo ao abrir o pacote. A solução real seria mover o `CLAUDE.md`.
- **Batchmode e testes:** o comando não muda. O host deixa de ser reproduzível: o manifest com `testables`, a versão da Unity (D5) e o cenário C1 do 0M-40 não ficam mais no git, e cada máquina (ou cada agente, numa cópia de trabalho própria pela emenda da 0003) precisa recriar o host. A prova dos tickets (0004, "sem CI") passa a depender de um ambiente que ninguém revisa. Seria preciso um script ou um passo a passo para criar o host, ou seja, ferramenta nova.
- **Editor-lock:** não muda dentro do repositório. Na variante da pasta irmã, a trava continua no checkout, mas o host fica fora dele, e dois checkouts apontando para o mesmo host voltam a ter o problema que a 0005 resolve.
- **Tickets 0M-26 a 0M-40:** os caminhos não mudam. O cenário C1 do 0M-40 precisa de outro lugar (`Tests/` ou `Samples~`), o que muda o desenho do CA6.
- **Tamanho:** o menor, com cerca de 5 arquivos e um ticket sem Editor. Não resolve os itens 1 e 3 do Contexto, e piora o item 2: o host passa de "meio versionado" a "não versionado".

### C — Monorepo com o pacote e o host lado a lado

O pacote fica em `com.spellbook/`, na raiz do repositório, e o host em `SpellBook.Dev/`, sem til e versionado inteiro, com `"com.spellbook": "file:../../com.spellbook"`. É a "Opção B" da angry-shark.

- **Instalação:** `?path=/com.spellbook`. O ganho é o mesmo da A (o usuário baixa só o pacote).
- **Batchmode e testes:** `-projectPath "$PWD/SpellBook.Dev"`. Os testes não mudam. O pacote entra como **local** (`file:`), não como embutido. Na prática isso dá o mesmo resultado, mas com o `file:` relativo que já deu trabalho no 0M-06 (D1).
- **Editor-lock:** não muda.
- **Tickets:** emenda de prefixo igual à da A (`com.spellbook/`). O C1 vai para `SpellBook.Dev/Assets/`.
- **Tamanho:** igual ao da A. Cria um nível a mais sem ganho sobre a A: a raiz do repositório não é nem o pacote nem o projeto, e quem abre o repositório no Hub precisa saber qual subpasta abrir.

## Decisão

Adotar a **alternativa A** (aceita no gate de 2026-10-30):

1. A raiz do repositório é o projeto Unity de desenvolvimento do SpellBook, versionado como qualquer projeto Unity: `Assets/`, `Packages/manifest.json`, `Packages/packages-lock.json` e `ProjectSettings/`. O `.gitignore` segue o padrão Unity (`Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/`, `Build/`, `Builds/`, arquivos de IDE) e cobre `Assets/InitTestScene*.unity` (B-026).
2. O pacote é embutido em `Packages/com.spellbook/`. O nome da pasta é igual ao `name` do `package.json`. Tudo o que é publicado fica ali, e só ali: `package.json`, `README.md`, `CHANGELOG.md`, `LICENSE`, `Runtime/`, `Editor/`, `Tests/` e `Samples~/`.
3. `Documentation~/` fica na raiz do repositório, fora do pacote. O nome com til se mantém, para não mudar as dezenas de referências a `Documentation~/...` nos agentes, nas skills, nas ADRs e no `docs.yml`. A configuração da 0025 não muda.
4. Instalação documentada: `https://github.com/gabrielctorres/SpellBook.git?path=/Packages/com.spellbook#<tag>`.
5. `HostProject~/` deixa de existir. O host de testes da 0004 passa a ser a raiz.
6. A migração é feita em tickets próprios (M1 movimento puro, M2 caminhos dos papéis), entre o 0M-23 e o 0M-24, na branch `feat/0m-lote-19-em-diante`. O M1 tem exceção de contagem aprovada no gate, com o critério de renomes R100.

## Motivo

- O usuário instala só o pacote. Docs internas, o host e o `CLAUDE.md` deixam de ir para o `PackageCache` dele.
- O projeto de desenvolvimento passa a ser um projeto Unity comum, versionado inteiro e aberto direto pelo Hub. Saem o til, as regras especiais do `.gitignore` e o `file:../..`.
- Nenhum teste muda (leitura por `resolvedPath`), e a organização por módulo (0012) é preservada.
- É o layout "pacote dentro do projeto" que a referência de pacote descreve, com o pacote em `Packages/` (embutido), e não em `Assets/`. O guia oficial recomenda não misturar código de pacote com os assets do projeto.

## Consequências

- **Quebra de instalação:** a URL do git ganha `?path=/Packages/com.spellbook`. Isso entra no CHANGELOG da `3.0.0-pre.1` (seção 7 da Spec) e na página Instalação. A URL antiga continua servindo às tags 2.x.
- **Emendas a ADRs aceitas**, cada uma datada, sem reescrever o corpo:
  - 0004: o host é a raiz, e não `HostProject~/`; comando do batchmode;
  - 0005: o "caminho do host" na trava é a raiz;
  - 0001: o item 2 passa a falar da "raiz do pacote = `Packages/com.spellbook/`";
  - 0012: o item 4 passa a ter pastas relativas ao pacote;
  - 0010: a menção ao `ProjectVersion.txt` do host passa a apontar para a raiz;
  - 0003: exceção do M1. Alternativa: uma regra nova, "movimento puro R100 conta como 1 arquivo", se o usuário preferir regra em vez de exceção.
- **Emenda na Spec da modularização:**
  - seção 1.2;
  - seção 4.1 (mapa);
  - D1 (deixa de ter `file:`);
  - D5 (caminho do `ProjectVersion.txt`);
  - risco 35 / B-025 (caminhos dos logs);
  - 0M-40 (C1 em `Assets/Scenarios/`);
  - regra de prefixo para os tickets a partir do 0M-24;
  - os tickets M1 e M2.
- **Backlog:** o B-025 e a parte de `.gitignore` do B-026 são absorvidos pelo M1 e pelo M2.
- `ProjectSettings/` versionado passa a ser revisado em PR. Mudanças nele exigem cuidado, porque são do projeto de desenvolvimento e não do pacote.
- Quem abre o repositório no Hub abre o projeto inteiro. Abrir com uma versão diferente de `ProjectSettings/ProjectVersion.txt` gera diff em `ProjectSettings/`, que não deve ser commitado fora de um ticket.

## O que os agentes NÃO podem fazer

- Colocar no pacote (`Packages/com.spellbook/`) qualquer coisa que não seja publicada: docs internas, cenários de verificação ou configuração de agente.
- Colocar código do pacote em `Assets/`. `Assets/` guarda só cenários de verificação do projeto de desenvolvimento.
- Mover arquivo do pacote sem o `.meta` dele ou trocar GUID na migração.
- Commitar `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/` ou cenas `InitTestScene*`.
- Fazer a migração (M1) com mudanças pendentes de outro ticket na cópia de trabalho.
- ~~Tratar esta ADR como regra antes do gate.~~ (Sem objeto: aceita em 2026-10-30.)
- Escrever instruções de instalação no `README.md` da raiz. Elas ficam só no `README.md` do pacote e na página Instalação do manual.

## Decisões do gate (2026-10-30)

As perguntas que estavam "Em aberto" foram respondidas pelo usuário (1 a 4). A 5 foi delegada ao Arquiteto.

1. **Alternativa A.** B e C recusadas pelos motivos das próprias alternativas: a B deixa o host fora do git e não resolve os itens 1 e 3 do Contexto; a C cria um nível a mais sem ganho sobre a A.
2. **Nome do pacote: `com.spellbook` continua.** A pasta é `Packages/com.spellbook/`. O nome em domínio reverso não é adotado. Trocar o `name` depois da `3.0.0` é quebra MAJOR e precisa de ADR nova (0008, 0009).
3. **Regra de contagem, não exceção.** A 0003 ganha uma regra permanente (emenda de 2026-10-30 da 0003): **um arquivo movido sem alteração conta como 1 linha**. Leitura do Arquiteto, que precisa de confirmação do usuário: o movimento sem alteração também **não conta no limite de 10 arquivos**, como a remoção pura (I42). Sem essa leitura, o M1 continua impossível: são cerca de 120 movimentos. Com isso:
   - o item 6 da Decisão deixa de pedir exceção: o M1 segue a regra nova;
   - na lista de emendas das Consequências, o item "0003: exceção do M1" passa a ser "0003: regra do movimento puro".
4. **Momento: agora.** A migração entra logo depois do APROVADO do 0M-23 e antes do próximo ticket da 0-M, na branch de lote `feat/0m-lote-19-em-diante`. O 0M-24 e o 0M-25 já foram entregues, então o próximo é o 0M-26. Por isso a regra de prefixo vale para os tickets que faltam: 0M-26 a 0M-40, mais 0M-45, 0M-49 e 0M-50, que estão no meio deles.
5. **READMEs: dois, com uma fonte só para a instalação** (decisão do Arquiteto, a pedido do usuário):
   - `Packages/com.spellbook/README.md`: é o README publicado e o único texto para quem **usa** o pacote (requisitos, instalação com `?path=`, link do manual). É o que vai para o `PackageCache` do usuário.
   - `README.md` da raiz: é novo e curto, para quem **chega ao repositório** (o GitHub mostra este). A primeira linha manda quem quer instalar para o README do pacote e para o manual. Depois vêm o layout (pacote, projeto de desenvolvimento, `Documentation~/`), como abrir o projeto e os links para `Documentation~/README.md` e `TIME.md`. Ele **não repete** os passos de instalação.
   - Motivo: o GitHub mostra só o da raiz, e o usuário instalado vê só o do pacote. Uma cópia dos passos em cada um seria uma segunda verdade, que já divergiu uma vez (a URL sem `?path=`).
6. **Tickets** (Spec da modularização, seção 8):
   - **0M-54 (M1):** movimento, com Unity;
   - **0M-55 (M2):** skills e agentes que rodam a Unity, mais o `TIME.md`, sem Unity;
   - **0M-56 (M3):** agentes de leitura, instalação e READMEs, sem Unity.

   O M2 da proposta foi dividido em 0M-55 e 0M-56 porque tinha 17 arquivos, e a 0003 permite 10.
7. **`.github/workflows/docs.yml` não muda.** Todos os caminhos dele são `Documentation~/...`, que fica na raiz (item 3 da Decisão). A 0025 também não muda.
8. **`.gitignore` do projeto** (detalha o item 1 da Decisão). Também cobre `Assets/__SpellBookTestTemp*`, a pasta temporária dos testes que criam assets (risco 23 da Spec), para quando um run for interrompido.
9. **Leitura do manual pelos testes.** O manual sai da pasta do pacote. O teste que lê as páginas (`ManualLinksTests`, CA54, 0M-40) passa a procurá-las na raiz do repositório, dois níveis acima do `resolvedPath`. Só os testes que leem o manual mudam. Os que leem arquivos do pacote continuam iguais (Contexto).

### ~~Pendente do usuário (achado ao escrever o M1)~~ Resolvido em 2026-10-30

- ~~**`ProjectSettings/` versionado inteiro** (item 1 da Decisão).~~ O host tinha 22 arquivos em `ProjectSettings/`, e só o `ProjectVersion.txt` estava no git. Os outros 21, gerados pela Unity, entram como arquivos criados, e a regra do movimento puro não os cobre. Um deles, o `SceneTemplateSettings.json`, contaria nas linhas, porque `.json` é código pela contagem da 0003. Decisão do usuário no item 11 abaixo.

### Decisões do usuário sobre as perguntas abertas (2026-10-30)

10. **Leitura da regra do movimento puro: confirmada.** Um arquivo movido sem alteração (`R100`) conta como 1 linha e **fica fora do limite de 10 arquivos**, como a remoção pura (I42). A emenda de 2026-10-30 da 0003 deixa de ter a ressalva "pendente de confirmação".
11. **`ProjectSettings/`: exceção só no 0M-54.** Os 21 arquivos de `ProjectSettings/` gerados pela Unity, sem edição à mão, entram no git nesse ticket e não contam em nenhum dos dois limites (linhas e arquivos). O Examinador revisa pela lista de caminhos e confere que o `ProjectVersion.txt` é o da D5. A exceção não vale para nenhum outro ticket. Depois do 0M-54, toda mudança em `ProjectSettings/` conta normalmente e só entra em ticket `chore(host)` (Consequências).
12. **`Runtime/Systems/` já não existe.** O 0M-23 (commit `c59a68f`) apagou a pasta `Runtime/Systems/` e o `Runtime/Systems.meta` antes do previsto, porque o `.meta` órfão gerava aviso num clone limpo. Isso muda dois pontos desta ADR:
    - na alternativa A, a frase "o item do 0M-40 que fala de `Runtime/Systems` não muda" fica sem objeto: o item saiu do 0M-40 (Spec, emenda de 2026-10-30 do 0M-40);
    - o 0M-54 não move nada de `Runtime/Systems/`. O `Runtime/` movido tem só o que existe depois do 0M-23.

## Relacionadas

- 0001 (pastas de docs), 0003 (tamanho de ticket), 0004 (host de testes), 0005 (editor-lock), 0009 (quebras na 3.0), 0010 (versão da Unity e host), 0012 (pastas por módulo), 0025 (manual).
