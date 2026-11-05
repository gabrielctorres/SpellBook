# Design doc: Menu de Editor do GAS (assistente de criação e gerenciador)
Épico: `epic:menu-de-editor-do-gas` · Backlog: B-019a e a parte de criação do B-041 · ADRs: nenhuma nova (segue 0026 e 0043) · Status: Aprovada (gate de 2026-11-05)

Referência visual aprovada: [artefato do menu de Editor](https://claude.ai/artifact/94QTsdexDe1eAtYyPzdDuA?sk=wUikJrY-YHgCHtBS16TfKw), aba 2 ("Criar habilidade ou efeito") e aba 3 ("Gerenciador do GAS"). O DS é o [ds/ferramentas-do-editor.md](../ds/ferramentas-do-editor.md).

**Um épico só.** O gerenciador duplica e renomeia com o mesmo motor de cópia e de nomes do assistente. Em dois épicos, esse motor ficaria num e seria usado pelo outro.

**PRD do B-019a.** O [PRD](../prd/ferramentas-templates-de-habilidade.md) continua sendo a parte fixa do assistente (AGENTS.md, Fluxo, item 6). Os CA1 a CA6 abaixo repetem os cenários C1 a C7 dele em forma verificável, com os ajustes do gate (seção "Decisões do épico"). O gerenciador e a janela são novos.

## Problema
Para criar uma habilidade, o designer monta à mão a habilidade, os efeitos de custo, de recarga e de dano e as tags, liga um no outro e inventa os nomes. O pacote não tem um ponto de partida: o único atalho, `Tools/Create Default Project Folders` (`Editor/Core/FolderStructureCreator.cs`), cria pastas de um jogo. Depois de criados, os assets ficam espalhados no Project window. Não há como ver quais habilidades e efeitos existem, filtrar por tag ou saber quem usa um efeito antes de mudá-lo ou apagá-lo. Renomear um conjunto (habilidade, efeitos e tags) também é feito um asset por vez.

## Critérios de aceite
Assistente (PRD B-019a):
1. **CA1.** O assistente abre por `SpellBook/Criar habilidade ou efeito…` e por `Create > SpellBook > GAS > Ability from Template…`, que já preenche a pasta selecionada. Ele mostra os modelos disponíveis, cada um com uma frase do que cria. Um modelo cujo asset modelo ou tipo não existe fica fora da lista, e o `ProjectValidator` acusa o modelo quebrado (PRD C7).
2. **CA2.** Com o nome e a pasta escolhidos, antes de criar, o assistente lista cada asset com o nome final e cada tag marcada como "nova" ou "já existe". A tag é um asset com o nome inteiro (`Ability.IceBolt`) e o pai (`Ability`). Nome vazio ou inválido, ou asset que já existe, desliga "Criar" e diz qual é o problema (PRD C3 e C4).
3. **CA3.** "Criar" gera o conjunto com as referências apontando para as cópias, inclusive dentro das listas de ações (`[SerializeReference]`), reutiliza as tags existentes, seleciona o asset principal e não muda os modelos. Os prefabs (projétil, thinker) não são copiados: a cópia aponta para o prefab do pacote. O validador não acusa erro no conjunto, e duas criações com nomes diferentes dão conjuntos independentes (PRD C1 e C2).
4. **CA4.** Se um passo da criação falha, nenhum asset nem tag do conjunto fica no projeto. Depois de criar, o botão "Desfazer criação" do assistente apaga o conjunto inteiro enquanto a janela está aberta, e o Ctrl+Z também desfaz o conjunto se a Undo da Unity cobrir os arquivos (o red do ME2 confere) (PRD R4 e C5).
5. **CA5.** Um modelo criado pelo projeto, do mesmo tipo dos do pacote, aparece no assistente sem código (PRD C6).
6. **CA6.** O `FolderStructureCreator` e o menu `Tools/Create Default Project Folders` saem do pacote, e o `CHANGELOG.md` registra a remoção (PRD R8).

Gerenciador (`SpellBook/Gerenciador do GAS`):

7. **CA7.** O gerenciador lista as habilidades, os efeitos, os atributos e as tags do projeto, com a contagem de cada tipo. A lista acompanha a criação, a remoção e a renomeação de assets sem reabrir a janela.
8. **CA8.** Os filtros por tipo, por tag e por texto se combinam. O filtro de tag usa a hierarquia: filtrar por `Ability` mostra as habilidades com `Ability.Fireball`.
9. **CA9.** Com um item selecionado, o gerenciador mostra o que ele usa e quem o usa: assets, prefabs e objetos das cenas abertas. Cenas fechadas não são lidas, como no Find References dos canais. Clicar numa referência a destaca no Project window ou na Hierarchy.
10. **CA10.** O gerenciador não edita campos. As ações são Abrir no Inspector, Duplicar, Renomear, criar tag e Excluir; "Abrir no Inspector" seleciona o asset.
11. **CA11.** Duplicar uma habilidade cria uma habilidade nova com custo e recarga próprios e tags novas, e os outros efeitos ficam compartilhados com a original. Duplicar um efeito copia só o efeito. O nome novo não colide, e a cópia usa o motor do CA3.
12. **CA12.** Renomear uma habilidade lista os derivados (`GE_Fireball_Cost`…) e as tags (`Ability.Fireball`, `Cooldown.Fireball`), todos marcados; o designer desmarca qualquer item. Os marcados são renomeados junto, sem quebrar referência. Nome que já existe é recusado.
13. **CA13.** As tags aparecem em árvore pela hierarquia. O designer cria uma tag filha e renomeia uma tag pela janela. Renomear uma tag com filhas lista as filhas com o nome novo (`Skill.IceBolt`, ao trocar `Ability` por `Skill`), todas marcadas e desmarcáveis como no CA12. As marcadas são renomeadas junto. Mudar o pai de uma tag não é oferecido.
14. **CA14.** "Excluir", num asset ou numa tag, abre uma confirmação com a lista de quem o usa (a busca do CA9, mais as tags filhas, no caso de tag). Só depois da confirmação o item é apagado. As referências que ficaram vazias aparecem no validador.

Comum:

15. **CA15.** As janelas seguem a 0026 (UI Toolkit, sem IMGUI e sem Odin) e o DS. As mensagens ficam em português. A lógica (plano de cópia, nomes, índice, referências, exclusão) fica em classes sem UI, testadas em EditMode.

## Cenário no Sandbox
Com o Sandbox importado (`Packages/com.spellbook/Samples~/Sandbox`):
1. Abra `SpellBook/Criar habilidade ou efeito…`. Escolha Projétil, digite "Fireball" e escolha `Data/Abilities`. A lista mostra `GA_Fireball` como "já existe" e "Criar" fica desligado (CA1, CA2).
2. Troque o nome para "IceBolt". A lista mostra a habilidade, os efeitos de custo, de recarga e de dano e as tags `Ability.IceBolt` e `Cooldown.IceBolt` como novas. Crie (CA2, CA3).
3. Rode `SpellBook/Validar projeto`: nenhum erro. Abra `GA_IceBolt`: o custo e a recarga apontam para os `GE_IceBolt_*`, a ação de dano aplica o `GE_IceBolt_Damage` e o projétil é o prefab do pacote. Clique em "Desfazer criação" e confira que o conjunto sumiu; crie de novo (CA3, CA4).
4. Abra `SpellBook/Gerenciador do GAS`. Filtre por "Efeitos" e pela tag `TAG_Status_Burning`: aparece o `GE_Burn`. Filtre por `Ability`: aparece o `GA_IceBolt` (CA7, CA8).
5. Selecione o `GE_Fireball_Cost`: em "Usado por" aparece o `GA_Fireball`. Clique nele e confira o destaque no Project window (CA9).
6. Duplique o `GA_IceBolt`: a cópia tem custo, recarga e tags próprios e aplica o mesmo `GE_IceBolt_Damage` (CA11).
7. Renomeie o `GA_Fireball` para "Firebolt" com a lista toda marcada e entre em Play: a habilidade é conjurada como antes (CA12).
8. Na árvore de tags, crie a filha `Ability.IceBolt.Charged` e, abaixo dela, `Ability.IceBolt.Charged.Max`. Renomeie `Ability.IceBolt.Charged` para `Ability.IceBolt.Heavy`: a lista traz a filha como `Ability.IceBolt.Heavy.Max`, marcada. Confirme (CA13). Exclua `Ability.IceBolt.Heavy`: a confirmação lista a filha `Ability.IceBolt.Heavy.Max` (CA14).
9. Exclua o `GE_IceBolt_Damage`: a confirmação lista o `GA_IceBolt` e a cópia do passo 6. Confirme. O efeito sai da lista, e o validador acusa a ação de dano vazia nas duas habilidades (CA14).

## Fora do escopo
- O Inspector da habilidade e do efeito, que é o épico [inspector-de-habilidade](inspector-de-habilidade.md). O gerenciador só abre o asset nele.
- Editar campos no gerenciador; mudar o pai de uma tag.
- Template de Dash e de Escudo; tabela de balanceamento (B-019b); simulador (B-019c); editor visual de modelos.
- Ler cenas fechadas para achar referências.
- O manual e o catálogo, que ficam para o passe de documentação (0007 e 0044).

## Decisões do épico
Gate de 2026-11-05 (as perguntas Q1 a Q14 do rascunho):
- **Q1, menu.** As duas janelas ficam no menu `SpellBook/` da barra, junto do `Validar projeto`: `SpellBook/Criar habilidade ou efeito…` e `SpellBook/Gerenciador do GAS`.
- **Q2, Create.** Um item só, `Create > SpellBook > GAS > Ability from Template…`, que abre o assistente com a pasta selecionada já preenchida. Não há um item por modelo.
- **Q3, Passiva.** Sai. Os modelos são Projétil, Área, Buff, Só efeito, Aura e Em branco. O Só efeito com duração infinita cobre o bônus permanente, que é o caso comum. A Passiva condicional (liga com a concessão, barra por tag, usa o nível da habilidade) é caso avançado: o designer parte do Em branco e troca o comportamento para `Passive`. O runtime não muda.
- **Q4, Aura.** Entra, no ME5.
- **Q5, Em branco.** Entra: habilidade, recarga, custo e as duas tags, sem ações.
- **Q6, prefabs.** O Projétil e a Aura apontam para o prefab do pacote; o assistente não copia prefab. Risco: o designer que trocar o visual no prefab do pacote muda todas as habilidades que o usam, e num pacote instalado pelo registro o prefab é só leitura. Para ter um visual próprio, ele duplica o prefab no projeto e troca a referência na habilidade. O DS mostra esse aviso na lista do assistente. O pacote ainda não tem prefab de projétil nem de thinker fora do `Samples~`: o ME3 e o ME5 os acrescentam.
- **Q7, tag.** A tag `Ability.IceBolt` é um asset chamado `Ability.IceBolt`, com pai `Ability`.
- **Q8, desfazer.** O botão "Desfazer criação" entra, além do Ctrl+Z (CA4).
- **Q9, edição.** O gerenciador não edita campos: só Abrir no Inspector, Duplicar, Renomear, criar tag e Excluir.
- **Q10, excluir.** Entra, com a lista de quem usa o asset e confirmação (CA14, ticket ME10).
- **Q11, duplicar.** Habilidade: custo e recarga próprios, tags novas, demais efeitos compartilhados. Efeito: só o efeito.
- **Q12, renomear o conjunto.** Renomeia derivados e tags, com a lista marcada por padrão e cada item desmarcável.
- **Q13, tags.** Criar tag filha, renomear tag e excluir tag (com lista de usos e confirmação) entram. Mudar o pai fica fora.
- **Q14, renomear tag com filhas.** Renomeia as filhas também (`Ability` → `Skill` leva `Ability.IceBolt` a `Skill.IceBolt`), pela lista marcada e desmarcável da Q12 (CA13, ticket ME9).

Do rascunho, sem mudança:
- **Código de hoje contra o PRD.** O B-006 já está no código (`GameplayTagDefinition.Parent` público). Ele não tem API para criar tag, porque uma tag é um asset `GameplayTagDefinition` com pai, e o nome dela é o nome do asset. O assistente cria a tag como asset, e o R5 do PRD vale como a regra de nome da Q7. O `AuraBehaviour` com o `CreateThinkerAction` já existe, então o B-014 não bloqueia mais a Aura.
- **O modelo é um asset de dados no runtime do GAS** (a Fábrica confirma no Detalhe do ME1). Assim ele entra nas regras da 0043 (`SpellBook/GAS/Ability Template`, `New Ability Template`) e da 0026 (Inspector de uma linha) sem emenda, e ganha a família "Modelo de habilidade" na tabela da 0043, no mesmo ticket (item 6).
- **Busca de referências no Core.Editor.** A parte genérica do `ChannelReferenceFinder` (`Editor/Events/ChannelReferenceFinder.cs`) vai para o `SpellBook.Core.Editor`, porque o `GAS.Editor` não pode referenciar o `Events.Editor` (0026, item 5). O Find References dos canais passa a usar a versão do Core sem mudar o comportamento. O Excluir usa a mesma busca.
- **Prefixos.** O pacote não impõe prefixo (0043, item 3). Os prefixos `GA_` e `GE_` são regras de nome dos modelos do pacote, e o projeto pode trocá-los no modelo dele.

Depois do gate:
- **2026-11-05, tags do modelo (opção B).** Cada tag do `AbilityTemplate` vira uma entrada explícita `{source: tag-modelo opcional, rule}`. Isso muda o formato serializado do ME1, que ainda não saiu em versão, e a mudança entra no próprio ME2 (~+40 linhas).
- **2026-11-05, lotes.** O trabalho corre em dois lotes, e este épico fica no Lote J (branch `feat/lote-j`).

## Tickets
Tamanho estimado pela 0042; a Fábrica confirma no Detalhe. Os `.asset` e os prefabs contam só como arquivo. Para caber no teto, os modelos podem compartilhar os assets modelo de custo, de recarga e de dano; a cópia sai separada de qualquer jeito.

| Ticket | Objetivo | CA | Depende de | Unity |
|---|---|---|---|---|
| ME1 | `AbilityTemplate` (modelos, regras de nome, tags), Inspector de uma linha e regra do validador para modelo ausente. ~200 linhas, ~6 arquivos | CA1, CA5 | gate; OA1 (0043 aceita) | sim |
| ME2 | Motor de criação sem UI: plano de nomes, colisão, cópia com troca de referências (também em `[SerializeReference]`), referência a prefab mantida sem cópia, tags com nome inteiro e pai, criadas ou reutilizadas, desfazer tudo se um passo falha e apagar o conjunto criado. Cada tag do `AbilityTemplate` vira uma entrada explícita `{source: tag-modelo opcional, rule}`, o que muda o formato serializado do ME1 (ver "Decisões do épico"). ~490 linhas, ~5 arquivos | CA2, CA3, CA4 | ME1 | sim |
| ME3 | Modelos Projétil, Área e Buff, o prefab de projétil do pacote e o teste de que cada modelo cria um conjunto sem erro no validador. ~80 linhas, ~11 arquivos | CA1, CA3 | ME2 | sim |
| ME4 | Janela do assistente (modelos, nome, pasta, lista do que será criado, Criar, Desfazer criação), `SpellBook/Criar habilidade ou efeito…` e `Create > SpellBook > GAS > Ability from Template…`; saída do `FolderStructureCreator` com o `CHANGELOG.md`. ~400 linhas, ~7 arquivos | CA1, CA2, CA4, CA6, CA15 | ME3; DS | sim |
| ME5 | Modelos Aura (com o prefab de thinker do pacote), Em branco e Só efeito. ~60 linhas, ~9 arquivos | CA1, CA3 | ME3 | sim |
| ME6 | Busca de referências genérica no `Core.Editor`; o Find References dos canais passa a usá-la. ~250 linhas, ~5 arquivos | CA9 | gate | sim |
| ME7 | Índice do GAS sem UI: habilidades, efeitos, atributos e tags; filtros por tipo, tag e texto; atualização quando assets mudam. ~350 linhas, ~4 arquivos | CA7, CA8 | gate | sim |
| ME8a | Lógica do gerenciador sem UI: `GasManagerModel` (lista e filtros) e `GasReferenceList` (linhas do painel de referências). 499 linhas, 4 arquivos (issue #398) | CA7 a CA10 e CA15, na parte de lógica | ME6, ME7 | sim |
| ME8b | Janela do gerenciador (`SpellBook/Gerenciador do GAS`) em UI Toolkit e o USS dela. ~264 linhas, 2 arquivos (issue #405) | CA7 a CA10 e CA15, na parte de UI | ME8a; DS | sim |
| ME9 | Duplicar (habilidade e efeito) e Renomear (asset, conjunto com a lista de derivados, e tag com a lista das filhas), pelo motor do ME2. ~450 linhas, ~6 arquivos | CA11, CA12, CA13 | ME2, ME8b | sim |
| ME10 | Excluir asset e tag com a lista de usos e a confirmação, e criar tag filha. ~300 linhas, ~5 arquivos | CA13, CA14 | ME2, ME6, ME8b | sim |

O ME8 foi medido em 763 linhas, acima dos +30% da 0042, e se dividiu em ME8a e ME8b. O ME6 e o ME7 não dependem do assistente e podem correr em paralelo com o ME1 a ME3, se os Detalhes não tiverem arquivo em comum. O ME9 e o ME10 tocam a mesma janela do ME8b; rodam em sequência, a não ser que os Detalhes separem os arquivos. O `Editor/GAS/GasEditors.cs` também é tocado pelo IH1 do Inspector; quem detalhar depois confere a ordem.

## Validação de uso
| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|

<!-- Parte JIT: uma seção por ticket, escrita pela Fábrica quando o ticket entra na fila, lendo o código na hora.
     Limites e contagem: ADR 0042. -->

## ME1 — Modelo de habilidade, Inspector e regra do validador
### Detalhe (2026-11-05)
- CA: CA1 (o modelo quebrado fica fora da lista e o validador o acusa) e CA5 (um modelo do projeto aparece sem código). Os menus e a janela do CA1 são do ME4; os nomes finais e a cópia, do ME2.
- Confirmação do design doc: o `AbilityTemplate` fica no runtime do GAS (`SpellBook.GAS`). É um `ScriptableObject` de dados, sem `UnityEditor`. Assim a guarda `CreateAssetMenuRulesTests` já o aceita em `SpellBook/GAS/`, e o asset do projeto é validado pelo `ProjectValidator`, que lê os `ScriptableObject` de `Assets/`. No Editor, ele ficaria fora da guarda da 0043 e pediria uma linha nova nela.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/GAS/Abilities/AbilityTemplate.cs` (novo): `[CreateAssetMenu(fileName = "New Ability Template", menuName = "SpellBook/GAS/Ability Template")]`, `[HelpURL(DocUrls.Gas)]`, `IValidatable`. Campos: `displayName` (nome no assistente; vazio usa o nome do asset), `description` (a frase do DS 4.3), `assets` (lista de `AbilityTemplate.Entry`, com `source`, o `ScriptableObject` modelo, e `nameRule`, como `GA_{Name}`; a primeira entrada é o asset principal) e `tags` (regras como `Ability.{Name}`; o pai é o trecho antes do último ponto, Q7). API: `DisplayName`, `Description`, `Assets`, `Tags`, `NameToken = "{Name}"` e `IsAvailable` (tem entrada e nenhum modelo ausente). O `Validate` dá erro para lista de assets vazia, modelo ausente (o asset foi apagado ou o tipo dele não existe mais, e a referência volta nula), regra de nome ou de tag sem `{Name}`. Cada erro aponta o campo.
  - `Editor/GAS/AbilityTemplateCatalog.cs` (novo, `public static`): `Find(List<AbilityTemplate> available, List<AbilityTemplate> broken)` lê todos os `t:AbilityTemplate` do projeto e dos pacotes pelo `AssetDatabase` e separa por `IsAvailable`, em ordem de `DisplayName`. É o que o ME4 mostra no passo 1 e na linha "N modelos com asset ausente".
  - `Editor/GAS/GasEditors.cs:29`: `[CustomEditor(typeof(AbilityTemplate))] AbilityTemplateEditor : SpellBookEditor`, de uma linha (0026).
  - `Documentation~/adr/0043-menu-create-nome-padrao-e-icone-dos-scriptableobjects.md:46`: linha `| Modelo de habilidade | AbilityTemplate |` na tabela do item 3, pelo item 6 e pelas decisões do épico. O ícone da família nova não entra: o item 4 segue em aberto (Q3 e Q4), e nenhum tipo tem ícone ainda.
  - Sem `CHANGELOG.md` (nada quebra) e sem manual (passe de documentação).
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`):
  - `AbilityTemplateTests` (`Tests/GAS/EditMode/AbilityTemplateTests.cs`, novo): `Validate_Complete_NoMessages`; `Validate_NoAssets_Error`; `Validate_MissingSource_ErrorOnField`; `Validate_NameRuleWithoutToken_ErrorOnField`; `Validate_TagRuleWithoutToken_ErrorOnField`; `IsAvailable_FalseWhenSourceMissing`; `DisplayName_EmptyUsesAssetName`; `ProjectValidator_MissingSource_ReportsTemplate`; `Inspector_IsSpellBookEditor`.
  - `AbilityTemplateCatalogTests` (`Tests/GAS/EditMode/AbilityTemplateCatalogTests.cs`, novo, com assets numa pasta temporária de `Assets/`): `Find_ProjectTemplate_IsAvailable`; `Find_TemplateWithDeletedSource_IsBroken`.
- Critério de pronto: os 11 verdes no filtro (`\.AbilityTemplateTests\.;\.AbilityTemplateCatalogTests\.`), mais `CreateAssetMenuRulesTests` verde com o tipo novo. A suíte completa roda na `/revisao`.
- Tamanho: 420 linhas medidas (+420 −0) e 6 arquivos (os 5 `.cs`, 4 deles novos, e a ADR 0043), pela ADR 0042. Os `.meta` e este Detalhe não contam, e a linha da ADR conta só como arquivo. Exceção: nenhuma.
- Commit: `feat(gas): modelo de habilidade com Inspector e validação`

## ME2a — Formato TagEntry do AbilityTemplate (#406)
### Detalhe (2026-11-05)
- Origem: o ME2 passou do teto de +30% da ADR 0042 depois do conserto pedido na revisão (666 linhas). O Controlador o dividiu em ME2a (#406), com a mudança de formato do ME1, e ME2b (#392), com o motor. O ME2a vem antes, porque o ME2b lê o formato novo.
- CA: nenhum CA novo. É a parte do CA2 e do CA3 que o modelo precisa guardar: qual tag-modelo vira a tag nova nas cópias.
- Mudança de formato do ME1 (decisão do usuário, 2026-11-05, opção B): o `AbilityTemplate` não dizia qual referência de tag dos modelos vira a tag nova. Antes, `tags` era `List<string>`, só com a regra (`Ability.{Name}`). Agora é `List<AbilityTemplate.TagEntry>`, com `source` (a tag-modelo que os assets modelo usam, um `GameplayTagDefinition` opcional) e `rule` (a regra do nome). Nenhum asset de modelo existe ainda (o ME3 traz os primeiros), então não há o que migrar. O `CHANGELOG.md` fica de fora, porque o formato do ME1 não foi lançado.
- Validador: acusa regra vazia ou sem `{Name}` em `tags.Array.data[i].rule` e a mesma tag-modelo em duas entradas em `tags.Array.data[i].source`. O campo já garante o tipo da `source`.
- Escopo (caminhos de código em `Packages/com.spellbook/`):
  - `Runtime/GAS/Abilities/AbilityTemplate.cs:36-49,60-69,118-135`: `TagEntry`, o campo `tags` e a validação nova das tags.
  - `Tests/GAS/EditMode/AbilityTemplateTests.cs`: o ajuste do ME1, sem tirar asserção, e o helper `AddTag`.
  - Este design doc: os Detalhes do ME2a e do ME2b.
  - Fora do motor do ME2b, nenhum código lê o `AbilityTemplate.Tags`, então o ME2a compila sozinho.
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`, `AbilityTemplateTests`): `Validate_TagRuleWithoutToken_ErrorOnField` passa a esperar `tags.Array.data[1].rule`; novo `Validate_SameTagSourceTwice_ErrorOnField`.
- Critério de pronto: 12 verdes no filtro `\.AbilityTemplateTests\.;\.AbilityTemplateCatalogTests\.` (10 do `AbilityTemplateTests` e 2 do catálogo), com o `total` do XML igual a 12.
- Tamanho: 74 linhas (+62 −12) e 3 arquivos (`AbilityTemplate.cs`, `AbilityTemplateTests.cs` e este design doc), pela ADR 0042. Os Detalhes não contam. Exceção: nenhuma.
- Commit: `feat(editor): formato TagEntry do AbilityTemplate (#406)`

## ME2b — Motor de criação do conjunto (#392)
### Detalhe (2026-11-05)
- Depende do ME2a: o commit do ME2b vem depois do commit do ME2a.
- CA: CA2 (plano com nomes finais e tag "nova" ou "já existe" com o pai; nome vazio ou inválido, asset existente e conjunto sem asset desligam a criação com o motivo), CA3 (cópia ligada, também em `[SerializeReference]`, prefab mantido, tags reutilizadas, asset principal selecionado, modelos intactos, conjuntos independentes) e CA4 (nada fica se um passo falha; apagar o conjunto criado). A janela, a lista na tela e o botão "Desfazer criação" são do ME4.
- Padrões confirmados pelo usuário (2026-11-05): as tags novas ficam na pasta escolhida, junto dos assets. Um pai ausente (`Ability`) também é criado e entra no plano como novo. A tag existente é procurada pelo nome do asset em todo o projeto (`Assets/` e pacotes).
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Editor/GAS/Creation/AbilitySetPlan.cs` (novo, `public`, sem UI): `AbilitySetPlan.CheckName` ("Digite um nome.", "Use só letras, números e _."), `Build(AbilityTemplate, nome, pasta)` e `Build(nome, pasta, assets, tags)`, a forma geral que o ME9 usa para duplicar e renomear sem modelo. O plano tem `Assets` (`PlannedAsset`: modelo, nome final, caminho, principal, `New` ou `Collision`), `Tags` (`PlannedTag`: nome inteiro, pai, `New` ou `Exists`, tag existente, tag-modelo), `References` (os prefabs que os modelos usam e que não são copiados) e `Problem` (a primeira mensagem que desliga a criação; `CanCreate` é `Problem == null`). A pasta precisa estar em `Assets/` ("Escolha uma pasta dentro de Assets."), porque os pacotes do registro são só leitura. A colisão diz "Já existe um asset GE_IceBolt_Cost nesta pasta. Escolha outro nome." (DS, 10). Um modelo com asset ausente também desliga a criação, e um conjunto sem asset diz "O conjunto precisa de pelo menos um asset.".
  - `Editor/GAS/Creation/AbilitySetCreator.cs` (novo, `public`): `Create(plan)` confere o plano antes de gravar qualquer coisa. Depois cria as tags novas (o pai antes da filha), copia cada modelo (`Object.Instantiate` e `AssetDatabase.CreateAsset`) e troca, em cada cópia, as referências aos modelos e às tags-modelo pelo mapa. O iterador do `SerializedObject` desce nas listas `[SerializeReference]`. Uma referência fora do mapa, como o prefab, fica como está. Se um passo lança exceção, o motor apaga o que já criou e relança. No fim, ainda dentro dessa proteção, seleciona o asset principal e devolve um `CreatedSet` com os caminhos criados e o asset principal. `Delete(set)` apaga os assets e as tags criados, nunca as reutilizadas.
  - `Tests/GAS/EditMode/AbilitySetCreationTests.cs` (novo): os testes abaixo, com os assets numa pasta temporária de `Assets/`.
  - Sem `CHANGELOG.md` e sem manual (passe de documentação).
- Achado da revisão (Examinador, 2026-11-05): um plano sem assets tinha `CanCreate` verdadeiro. O `Create` gravava as tags novas e lançava `ArgumentOutOfRangeException` em `copies[0]`, fora do `try`, e as tags ficavam no projeto, contra o CA4. Correção: o `Build` dá o problema "O conjunto precisa de pelo menos um asset." quando a lista está vazia, e o `Create` recusa o plano sem assets antes de gravar qualquer tag. A seleção e o retorno com `copies[0]` passaram para dentro do `try` que desfaz tudo.
- Undo da Unity e arquivos do `AssetDatabase` (Q8): a Undo não cobre esses arquivos. Com `Undo.RegisterCreatedObjectUndo` antes do `AssetDatabase.CreateAsset`, o `Undo.PerformUndo` deixa o arquivo no disco, sem erro no log (`Undo_DoesNotCoverAssetDatabaseFiles`, Unity 6000.3.13f1). Por isso o motor não registra Undo, e o Ctrl+Z não desfaz a criação. O "Desfazer criação" do ME4 usa o `AbilitySetCreator.Delete`. O CA4 já previa esse caso ("se a Undo da Unity cobrir os arquivos").
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`, `AbilitySetCreationTests`): `CheckName_EmptyOrInvalid_SaysWhy`; `Build_FinalNamesMainFirstAndPrefabAsReference`; `Build_ExistingAsset_CollisionDisablesCreate`; `Build_FolderOutsideAssets_DisablesCreate`; `Build_TagsWithParent_NewOrExisting`; `Create_CopiesPointToCopies_InsideSerializeReference`; `Create_TagSourceSwapped_ParentCreated_ExistingReused`; `Create_KeepsPrefab_TemplatesUnchanged_SetsIndependent`; `Create_SelectsMain_CopyValidatesWithoutErrors`; `Create_StepFails_NothingLeft`; `Create_NoAssets_ProblemAndNothingLeft` (o achado da revisão); `Delete_RemovesCreatedKeepsReusedTags`; `Undo_DoesNotCoverAssetDatabaseFiles` (Q8).
- Critério de pronto: 25 verdes no filtro `\.AbilityTemplateTests\.;\.AbilitySetCreationTests\.;\.AbilityTemplateCatalogTests\.` (os 12 do ME2a e os 13 do `AbilitySetCreationTests`), com o `total` do XML igual a 25. A suíte completa roda na `/revisao`.
- Tamanho: 592 linhas, todas em arquivos novos (`AbilitySetPlan.cs` 188, `AbilitySetCreator.cs` 108, `AbilitySetCreationTests.cs` 296), e 3 arquivos, pela ADR 0042. Os `.meta` não contam. Exceção: 92 linhas acima do teto (+18%, dentro dos +30%). Os testes de cópia precisam de modelos reais (uma habilidade, três efeitos, duas tags-modelo e um prefab) numa pasta temporária, e o conserto da revisão somou 16 linhas.
- Commit: `feat(editor): motor de criação do conjunto de habilidade (#392)`
- Arquivos por commit, na ordem (caminhos de código em `Packages/com.spellbook/`):
  1. `feat(editor): formato TagEntry do AbilityTemplate (#406)`: `Runtime/GAS/Abilities/AbilityTemplate.cs`, `Tests/GAS/EditMode/AbilityTemplateTests.cs` e `Documentation~/spec/menu-de-editor-do-gas.md` inteiro, com os dois Detalhes.
  2. `feat(editor): motor de criação do conjunto de habilidade (#392)`: `Editor/GAS/Creation.meta`, `Editor/GAS/Creation/AbilitySetPlan.cs` e o `.meta`, `Editor/GAS/Creation/AbilitySetCreator.cs` e o `.meta`, `Tests/GAS/EditMode/AbilitySetCreationTests.cs` e o `.meta`. O `ProjectSettings/ProjectSettings.asset` que aparece modificado no checkout não é deste ticket e fica fora dos dois commits.

## ME3a — Modelo Projétil e prefab de projétil do pacote (#393)
### Detalhe (2026-11-05)
- Divisão: o ME3 foi medido em 14 arquivos, acima dos +30% da ADR 0042, e se dividiu em ME3a (#393) e ME3b (#407), por decisão do Controlador em 2026-11-05. O ME3a traz o Projétil e os assets que os três modelos compartilham. O ME3b traz a Área e o Buff.
- CA: CA1 (o modelo do pacote aparece no catálogo, com nome e frase) e CA3 (o conjunto criado não tem erro no validador, aponta para o prefab do pacote sem copiá-lo e não muda o modelo). A lista na janela é do ME4.
- Local (aprovado pelo Controlador, 2026-11-05; o design doc e o PRD não diziam): os modelos e os assets modelo ficam em `Editor/GAS/Templates/`, fora do build, porque só o assistente os lê. O prefab fica em `Runtime/GAS/Prefabs/Projectile.prefab`, porque as cópias em `Assets/` apontam para ele e ele precisa ir para o build. O pacote não tem atributo, então o custo e o dano vêm com o modificador sem atributo (`Add -10` e `Add -20`); o validador não acusa, e o designer escolhe a Mana e a Vida. As tags-modelo não têm pai e têm nomes que um projeto não usa (`TAG_Template_Ability` e `TAG_Template_Cooldown`). Assim, o pai `Ability` das cópias nunca é uma tag de dentro do pacote.
- Escopo (caminhos em `Packages/com.spellbook/`, todos novos; os `.asset` e o prefab foram criados pela Unity, por um script de Editor rodado uma vez e apagado):
  - `Editor/GAS/Templates/Projectile.asset` (`AbilityTemplate`): nome "Projétil", a frase do que cria, os assets `GA_{Name}`, `GE_{Name}_Cost`, `GE_{Name}_Cooldown` e `GE_{Name}_Damage` e as tags `Ability.{Name}` e `Cooldown.{Name}`, ligadas às tags-modelo.
  - `Editor/GAS/Templates/GA_Template_Projectile.asset`: `AbilityTag` com a tag-modelo de habilidade, custo, recarga e, em `OnActivate`, um `SpawnProjectileAction` com o prefab do pacote e um `ApplyEffectAction` do dano em `OnHitActions`.
  - `Editor/GAS/Templates/GE_Template_Cost.asset`, `GE_Template_Cooldown.asset` (1,5 s, concede a tag-modelo de recarga) e `GE_Template_Damage.asset`: compartilhados com o ME3b.
  - `Editor/GAS/Templates/TAG_Template_Ability.asset` e `TAG_Template_Cooldown.asset`.
  - `Runtime/GAS/Prefabs/Projectile.prefab`: esfera de 0,3 com colisor trigger, `Rigidbody` cinemático sem gravidade e `Projectile` (velocidade 12, duração 3 s, relógio padrão).
  - `Tests/GAS/EditMode/PackageTemplatesTests.cs`: os testes abaixo, com as cópias numa pasta temporária de `Assets/`.
  - Sem `CHANGELOG.md`, sem manual e sem mudança de código, de API ou de formato serializado.
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`, `PackageTemplatesTests`): `Catalog_ListsTemplateAsAvailable("Projectile", "Projétil")`; `PackageAssets_ValidateWithoutErrors` (os modelos e o prefab, que o `ProjectValidator` não lê fora de `Assets/`); `Template_CreatesSetWithoutErrors_TemplateUnchanged("Projectile")`; `Projectile_CopiesLinked_PrefabFromPackage`. Os dois `TestCase` recebem os modelos do ME3b.
- Critério de pronto: 19 verdes no filtro `\.PackageTemplatesTests\.;\.AbilityTemplateCatalogTests\.;\.AbilitySetCreationTests\.` (os 4 novos, os 2 do catálogo e os 13 do motor), com o `total` do XML igual a 19. A suíte completa roda na `/revisao`.
- Tamanho: 104 linhas (+104 −0, só o teste) e 9 arquivos (o teste, 7 `.asset` e o prefab), pela ADR 0042. Os `.meta` e este Detalhe não contam. Exceção: nenhuma.
- Commit: `feat(editor): modelo Projétil e prefab de projétil do pacote (#393)`
- Arquivos do commit (caminhos de código em `Packages/com.spellbook/`): `Editor/GAS/Templates.meta`; em `Editor/GAS/Templates/`, `Projectile.asset`, `GA_Template_Projectile.asset`, `GE_Template_Cost.asset`, `GE_Template_Cooldown.asset`, `GE_Template_Damage.asset`, `TAG_Template_Ability.asset` e `TAG_Template_Cooldown.asset`, cada um com o `.meta`; `Runtime/GAS/Prefabs.meta`, `Runtime/GAS/Prefabs/Projectile.prefab` e o `.meta`; `Tests/GAS/EditMode/PackageTemplatesTests.cs` e o `.meta`; e `Documentation~/spec/menu-de-editor-do-gas.md`.

## ME3b — Modelos Área e Buff (#407)
- Depende do ME3a: usa os `GE_Template_Cost`, `GE_Template_Cooldown` e `GE_Template_Damage`, as tags-modelo e o `PackageTemplatesTests` dele.
### Detalhe (2026-11-05)
- CA: CA1 (a Área e o Buff aparecem no catálogo, com nome e frase) e CA3 (cada um cria um conjunto sem erro no validador, ligado às cópias, sem mudar o modelo). A lista na janela é do ME4. A Passiva saiu (Q3), e a Aura, o Em branco e o Só efeito são do ME5.
- Escopo (caminhos em `Packages/com.spellbook/`; os `.asset` foram criados pela Unity, por um script de Editor temporário rodado uma vez e apagado):
  - `Editor/GAS/Templates/Area.asset` (`AbilityTemplate`): nome "Área", a frase do DS 4.3, os assets `GA_{Name}`, `GE_{Name}_Cost`, `GE_{Name}_Cooldown` e `GE_{Name}_Damage` (os efeitos do ME3a) e as tags `Ability.{Name}` e `Cooldown.{Name}`, ligadas às tags-modelo do ME3a.
  - `Editor/GAS/Templates/GA_Template_Area.asset`: a tag-modelo de habilidade, o custo, a recarga e, em `OnActivate`, um `ApplyEffectAction` do dano com um `AreaSelector` (origem em quem lança, raio 5, 3D). Sem prefab.
  - `Editor/GAS/Templates/Buff.asset` (`AbilityTemplate`): nome "Buff", a frase do DS 4.3, os assets `GA_{Name}`, `GE_{Name}_Cost`, `GE_{Name}_Cooldown` e `GE_{Name}_Buff` e as mesmas duas tags.
  - `Editor/GAS/Templates/GA_Template_Buff.asset`: como a Área, mas o `ApplyEffectAction` aplica o buff com um `CasterSelector`.
  - `Editor/GAS/Templates/GE_Template_Buff.asset`: `HasDuration` de 5 s, `Is Buff` e um modificador `Add +10` sem atributo, pelo mesmo motivo do custo e do dano do ME3a: o pacote não tem atributo, e o designer escolhe o dele.
  - `Tests/GAS/EditMode/PackageTemplatesTests.cs:38-39,65-66,99-124`: os casos `Area` e `Buff` nos dois `TestCase` e dois testes da ligação.
  - Sem `CHANGELOG.md`, sem manual e sem mudança de código, de API ou de formato serializado.
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`, `PackageTemplatesTests`): `Catalog_ListsTemplateAsAvailable("Area", "Área")` e `("Buff", "Buff")`; `Template_CreatesSetWithoutErrors_TemplateUnchanged("Area")` e `("Buff")`; `Area_DamagesAroundCaster`; `Buff_AppliesTimedBuffToCaster`. O `PackageAssets_ValidateWithoutErrors` do ME3a passa a ler os assets novos da pasta.
- Critério de pronto: 25 verdes no filtro `\.PackageTemplatesTests\.;\.AbilityTemplateCatalogTests\.;\.AbilitySetCreationTests\.` (os 10 do `PackageTemplatesTests`, 6 deles novos, os 2 do catálogo e os 13 do motor), com o `total` do XML igual a 25. A suíte completa roda na `/revisao`.
- Tamanho: 32 linhas (+32 −0, só o teste) e 6 arquivos (o teste e 5 `.asset`), pela ADR 0042. Os `.meta` e este Detalhe não contam. Exceção: nenhuma.
- Commit: `feat(editor): modelos Área e Buff (#407)`
- Arquivos do commit (caminhos de código em `Packages/com.spellbook/`): em `Editor/GAS/Templates/`, `Area.asset`, `Buff.asset`, `GA_Template_Area.asset`, `GA_Template_Buff.asset` e `GE_Template_Buff.asset`, cada um com o `.meta`; `Tests/GAS/EditMode/PackageTemplatesTests.cs`; e `Documentation~/spec/menu-de-editor-do-gas.md`.

## ME4a — Estado do assistente de criação e fim do atalho de pastas (#394)
### Detalhe (2026-11-05)
- Divisão: o ME4 foi medido em 686 linhas, acima dos +30% da ADR 0042, e se dividiu em ME4a (#394) e ME4b (#411), por decisão do Controlador em 2026-11-05. O ME4a traz o estado do assistente sem UI e a saída do atalho de pastas. O ME4b traz a janela e os dois menus.
- CA: CA2 (a lista do que será criado, com nome final, "nova", "já existe", "colisão" e "referência", e o motivo que desliga "Avançar" e "Criar"), CA4 (o "Desfazer criação" apaga o conjunto pelo `AbilitySetCreator.Delete`; a Undo da Unity não cobre os arquivos, Q8 e achado do ME2b), CA6 (sai o `FolderStructureCreator`, com a entrada no `CHANGELOG.md`) e CA15 (a lógica fica numa classe sem UI, testada em EditMode). O CA1 (menus e lista na tela) é do ME4b. O CA3 e o resto do CA4 já estão no motor do ME2b.
- Fora deste ticket: o botão "Criar habilidade ou efeito…" no estado vazio do gerenciador fica no ME10 (pedido do Controlador, 2026-11-05), e não no ME4, como dizia a pendência do ME8b. A `GasManagerWindow` não muda.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Editor/GAS/Creation/AbilityWizardModel.cs` (novo, `internal`, sem UI): os quatro passos do DS 4.1 (`Step`, `StepTitle`, `StepLabel` "Passo N de 4", `Next`, `Back`), os modelos do `AbilityTemplateCatalog` com a frase (`Describe`: a descrição, ou "Modelo do projeto"), a linha dos quebrados ("1 modelo com asset ausente: veja SpellBook/Validar projeto"), `SelectTemplate`, `SetName`, `SetFolder`, o `AbilitySetPlan` remontado a cada mudança e o `Reason` de cada passo ("Escolha um modelo.", o `CheckName`, o `CheckFolder` e, na revisão, o `Problem` do plano). `Rows` dá uma linha por asset, tag e prefab do DS 4.2 (nome, tipo, marcador com ícone e texto, ★ no principal, tooltip da Q6 na referência). `Create` chama o motor e vai ao passo 4 ("Conjunto criado: N assets e M tags"). `UndoConfirm` dá "Apagar os N assets e M tags criados?", e `UndoCreation` apaga o conjunto e volta ao passo 2 com o mesmo nome. `FolderOf(caminho)` dá a pasta do item selecionado no Project window, para o menu Create do ME4b.
  - `Editor/GAS/Creation/AbilitySetPlan.cs:119`: o `CheckFolder` passa de `private` a `internal`, para o passo 2 dizer o motivo da pasta sem repetir a regra. É consequência direta do ticket.
  - `Editor/Core/FolderStructureCreator.cs` e o `.meta`: saem, com o menu `Tools/Create Default Project Folders` (CA6).
  - `CHANGELOG.md`: entrada em "Quebras" e passo 2 em "Migração". O menu e a classe `SpellBook.Editor.FolderStructureCreator` saem sem substituto, e quem os usava cria as pastas à mão. A entrada cita o `SpellBook > Criar habilidade ou efeito…`, que chega com o ME4b no mesmo lote.
  - `Tests/GAS/EditMode/AbilityWizardModelTests.cs` (novo).
  - Sem manual (passe de documentação) e sem mudança de formato serializado.
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`, `AbilityWizardModelTests`, com o modelo Projétil do pacote e uma pasta temporária de `Assets/`): `Load_ListsPackageTemplates_BrokenLeftOutWithNote`; `Describe_EmptyDescription_SaysProjectTemplate`; `Steps_AdvanceOnlyWhenValid_WithReason`; `Rows_FinalNamesTagsNewOrExistingAndPrefabReference`; `Collision_DisablesCreate_SaysWhichAsset`; `Create_SelectsMain_ThenUndoDeletesSetAndReturnsToName`; `FolderOf_FolderAssetOrNothing`. O red foi a compilação: `AbilityWizardModel` não existia.
- Critério de pronto: 20 verdes no filtro `\.AbilityWizardModelTests\.;\.AbilitySetCreationTests\.` (os 7 novos e os 13 do motor), com o `total` do XML igual a 20, o pacote compilando sem a janela e sem warning de compilação. A suíte completa roda na `/revisao`.
- Tamanho: 375 linhas medidas (+374 −1): `AbilityWizardModel.cs` 191, `AbilityWizardModelTests.cs` 182 e `AbilitySetPlan.cs` +1 −1. São 5 arquivos (os 3 de código, o `CHANGELOG.md` e este design doc), pela ADR 0042. A remoção do `FolderStructureCreator` é remoção pura e não conta. Os `.meta` e este Detalhe não contam. Exceção: nenhuma.
- Commit: `feat(editor)!: estado do assistente de criação e fim do atalho de pastas (#394)`
- Arquivos do commit (caminhos de código em `Packages/com.spellbook/`): `Editor/GAS/Creation/AbilityWizardModel.cs` e o `.meta`; `Editor/GAS/Creation/AbilitySetPlan.cs`; a remoção de `Editor/Core/FolderStructureCreator.cs` e do `.meta`; `CHANGELOG.md`; `Tests/GAS/EditMode/AbilityWizardModelTests.cs` e o `.meta`; e `Documentation~/spec/menu-de-editor-do-gas.md`.

## ME4b — Janela do assistente de criação (#411)
- Depende do ME4a mesclado no lote: a janela só mostra o `AbilityWizardModel`.
### Detalhe (2026-11-05)
- CA: CA1 (os dois menus e os modelos com a frase, com a linha dos quebrados), CA2 e CA4 na tela, e CA15 na parte de UI (UI Toolkit, sem IMGUI e sem Odin, 0026).
- Escopo (caminhos em `Packages/com.spellbook/`, todos novos; escritos e verdes com o ME4a e guardados à parte, com os `.meta`, em `C:/Users/gabri/AppData/Local/Temp/me4b-arquivos/`, que tem um `LEIA-ME.txt`):
  - `Editor/GAS/Creation/AbilityWizardWindow.cs` (`internal`): a janela nas quatro faixas do DS 3, com `SpellBook/Criar habilidade ou efeito…` e `Assets/Create/SpellBook/GAS/Ability from Template…` (Q1 e Q2; este abre com a pasta selecionada, pelo `FolderOf`). O `CreateGUI` é `internal` para o teste de fumaça, como no ME8b.
  - `Editor/GAS/Creation/AbilityWizard.uss`: os tokens `--spellbook-text-secondary` e `--spellbook-error` nos dois temas, nos valores do `ActionInspector.uss`, e o contorno de foco de 2 px.
  - `Tests/GAS/EditMode/AbilityWizardWindowTests.cs`: `Open_StepOne_ListsTemplates_AdvanceDisabledWithReason` e `Menus_WizardEntriesExist_FolderCreatorGone`, que confere também a saída do menu do ME4a.
- Critério de pronto: os 2 verdes com os 20 do ME4a, no filtro `\.AbilityWizardModelTests\.;\.AbilityWizardWindowTests\.;\.AbilitySetCreationTests\.`, com `total` 22 no XML (já conferido assim em 2026-11-05, antes da divisão).
- Tamanho: 311 linhas (`AbilityWizardWindow.cs` 257 e `AbilityWizardWindowTests.cs` 54) e 4 arquivos (os 3 novos e este design doc), pela ADR 0042. Exceção: nenhuma.
- Commit: `feat(editor): janela do assistente de criação (#411)`

## ME6 — Busca de referências genérica no Core.Editor
### Detalhe (2026-11-05)
- CA: CA9, na parte sem UI: quem usa o item (assets, prefabs e objetos das cenas abertas, sem ler cena fechada), o que ele usa e o destaque no Project window ou na Hierarchy. O painel que mostra isso é do ME8.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Editor/Core/ReferenceFinder.cs` (novo, `public`, porque o `GAS.Editor` e o `Events.Editor` o usam, 0026 item 5): `FindUsers(Object)` devolve um `ReferenceUser` (dono do campo, caminho do campo, local) por campo que aponta para o alvo, primeiro nos `.prefab` e `.asset` que dependem dele e depois nas cenas abertas, inclusive dentro de `[SerializeReference]`. `FindUses(Object)` devolve os objetos que o alvo referencia, uma vez cada, sem ele mesmo e sem o `m_Script`. `Reveal(Object)` seleciona e destaca. É a varredura do `ChannelReferenceFinder` de hoje, sem o papel.
  - `Editor/Events/ChannelReferenceFinder.cs:45-116`: `Find` passa a chamar o `ReferenceFinder.FindUsers` e só calcula o papel (`RoleOf`) e junta por objeto e papel (`Contains`), na mesma ordem; os dois não mudam. A varredura própria (`IsScannedAsset`, `DependsOn`, `Scan`) sai. A API `internal` dos canais não muda.
  - `Editor/Events/EventDebuggerWindow.cs:281-288`: `Reveal` chama o `ReferenceFinder.Reveal`.
  - `Tests/Core/EditMode/References/ReferenceHolderAsset.cs` (novo): ScriptableObject com um campo de objeto e um `[SerializeReference]`. `Tests/Testing/ReferenceHolderComponent.cs` (novo): componente com um campo de objeto, no `SpellBook.Testing`, porque um assembly só de Editor não pode ter componente adicionado a um GameObject. Cada um fica em arquivo próprio para a Unity associar o script.
  - Sem `CHANGELOG.md` (nada quebra) e sem manual (passe de documentação).
- Testes red (EditMode, `SpellBook.Core.Tests.EditMode`, classe `ReferenceFinderTests`, em `Tests/Core/EditMode/References/ReferenceFinderTests.cs`, novo): `FindUsers_AssetAndPrefab_OnePerField_WithLocation`; `FindUsers_InsideSerializeReference_ReportsPath`; `FindUsers_OpenScene_LocationIsScene`; `FindUsers_ClosedScene_NotIncluded`; `FindUsers_Null_Empty`; `FindUses_ReferencedObjects_OnceEach_WithoutScriptOrSelf`; `Reveal_SelectsTarget`.
- Critério de pronto: os sete verdes, mais o `FindReferencesTests` dos canais verde no filtro sem mudar asserção. A suíte completa roda na `/revisao`.
- Tamanho: 436 linhas medidas (+380 −56) e 7 arquivos (4 novos e este design doc), pela ADR 0042. Os `.meta` e este Detalhe não contam. Exceção: nenhuma.
- Commit: `feat(editor): busca de referências genérica no Core.Editor`

## ME7 — Índice do GAS sem UI
### Detalhe (2026-11-05)
- CA: CA7 e CA8, e a parte "índice" do CA15 (lógica sem UI, testada em EditMode). A lista na janela, os chips e a árvore de tags são do ME8.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Editor/GAS/Manager/GasIndex.cs` (novo, `internal`): `GasIndexKind` (Habilidade, Efeito, Atributo, Tag), `GasIndexEntry` (asset, nome, tipo e tags do item), `GasIndexFilter` (tipo opcional, tag opcional e texto) e `GasIndex`, que monta as entradas a partir de uma lista de objetos, ordena por tipo e por nome (os grupos do DS, 5.1), conta por tipo e filtra. Os três filtros se combinam (E). O texto procura no nome, sem diferenciar maiúscula. A tag casa pela hierarquia (`GameplayTagDefinition.Matches`): o filtro `Ability` aceita o item com `Ability.Fireball`. O filtro olha só as tags próprias do item (decisão do usuário, 2026-11-05): na habilidade, a `AbilityTag`; no efeito, as `GrantedTags` e as `DamageTags`; no atributo, a `StateTagWhileDepleted`; numa tag, a própria tag. A tag que o item só exige (`RequiredTags`, `BlockedTags`…) ou que uma ação dele usa fica fora: filtrar por `Status.Burning` não traz a habilidade que só a exige. Quem usa uma tag aparece no painel de referências (CA9, ME6). Uma subclasse que acrescente uma tag própria nova não entra no filtro; incluí-la fica como melhoria futura.
  - `Editor/GAS/Manager/GasProjectIndex.cs` (novo, `internal`): lê o projeto pelo `AssetDatabase.FindAssets` (`GameplayAbility`, `GameplayEffect`, `AttributeDefinition` e `GameplayTagDefinition`, com subclasses), numa lista opcional de pastas, e remonta o `GasIndex` só quando algo mudou. Um `AssetPostprocessor` interno marca o índice como desatualizado e dispara o evento `Changed` quando um `.asset` é importado, removido ou movido (a renomeação é um movimento), para a janela do ME8 se atualizar sem reabrir.
  - Sem `CHANGELOG.md`, sem manual e sem mudança de runtime, de API pública ou de formato serializado.
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`):
  - `Tests/GAS/EditMode/GasIndexTests.cs` (novo), com objetos em memória: `Build_ListsFourKinds_SortedByKindThenName`; `Counts_PerKind`; `KindFilter_KeepsOnlyKind`; `TextFilter_IgnoresCase`; `TagFilter_IncludesChildTags`; `TagFilter_IgnoresTagTheAssetOnlyRequires`; `TagFilter_OnTags_IncludesTagAndChildren`; `Filters_Combine`; `EmptyFilter_ReturnsAll`.
  - `Tests/GAS/EditMode/GasProjectIndexTests.cs` (novo), com assets numa pasta temporária: `Entries_ReadAssetsInFolder`; `CreatedAsset_AppearsAndRaisesChanged`; `DeletedAsset_Disappears`; `RenamedAsset_ShowsNewName`.
- Critério de pronto: os 13 testes verdes no filtro `\.GasIndexTests\.;\.GasProjectIndexTests\.` (total 13 no XML). A suíte completa roda na `/revisao`.
- Tamanho: 530 linhas medidas (+530) e 4 arquivos, todos novos, pela ADR 0042. Os `.meta` e este Detalhe não contam. Exceção: 30 linhas acima do teto (6%), dentro dos +30%. Os testes de criação, remoção e renomeação precisam de assets reais numa pasta temporária, e o resto do ticket não tem o que cortar.
- Commit: `feat(editor): índice do GAS com filtros`

## ME8a — Estado do gerenciador e painel de referências, sem UI
### Detalhe (2026-11-05)
- Divisão: o ME8 passou dos +30% da ADR 0042 (763 linhas medidas) e se dividiu em ME8a (issue #398) e ME8b (issue #405), por decisão do Controlador em 2026-11-05.
- CA: CA7 a CA9 e CA15, na parte sem UI. O ME8a monta o estado da lista e dos filtros sobre o `GasIndex` do ME7 e as linhas do painel sobre o `ReferenceFinder` do ME6. A janela que os mostra é o ME8b.
- Escopo (caminhos em `Packages/com.spellbook/`, todos novos):
  - `Editor/GAS/Manager/GasManagerModel.cs` (`internal`): o estado da janela sem UI. Guarda o `GasIndex` e o `GasIndexFilter`; `SetIndex`, `SetKind`, `SetTag`, `SetText` e `ClearFilters` remontam as `Rows`. Sem filtro de tipo, as `Rows` trazem um cabeçalho por grupo (Habilidades, Efeitos, Atributos, Tags; DS 5.1). Dá os rótulos da barra e dos chips (`KindLabel` "Habilidades 12", `KindName`, `TagChip` "Ability e filhas"), a mensagem de estado vazio (DS 7), o resumo de tags da linha (`TagSummary`: até duas e "+N"), a árvore de tags pelo pai (`TagTree`; a tag cujo pai saiu do filtro vira raiz) e os caminhos do seletor de tag em árvore (`TagMenu`: a tag com filhas aparece dentro do próprio submenu). `UsedByCount` guarda a contagem "usado por N" até o índice mudar.
  - `Editor/GAS/Manager/GasReferenceList.cs` (`internal static`): as linhas do painel. `UsedBy` e `Uses` passam o resultado do `ReferenceFinder` para uma linha por objeto, com o nome, a localização ("Asset", "Prefab" ou "Cena aberta: Sandbox") e o objeto que o clique destaca (o prefab inteiro, ou o GameObject da cena). `ClosedScenesNote` é a linha fixa do grupo "Usado por".
  - Sem `CHANGELOG.md`, sem manual e sem mudança de runtime, de API pública ou de formato serializado.
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`):
  - `Tests/GAS/EditMode/GasManagerModelTests.cs`, com objetos em memória: `Rows_AllKinds_HeaderBeforeEachGroup`; `Rows_KindFilter_NoHeaders`; `Labels_KindCountAndChips`; `ClearFilters_ShowsAll`; `EmptyMessage_NoAssetsNoResultsOrNone`; `TagSummary_TwoTagsAndRest`; `TagTree_NestsByParent_OrphanIsRoot`; `TagMenu_TagWithChildren_InsideOwnSubmenu`.
  - `Tests/GAS/EditMode/GasReferenceListTests.cs`, com assets numa pasta temporária e um objeto na cena aberta: `UsedBy_AssetAndPrefab_LocationAndRevealTarget`; `UsedBy_OpenScene_LocationNamesScene`; `Uses_ListsReferencedAsset`.
- Critério de pronto: os 11 testes verdes no filtro `\.GasManagerModelTests\.;\.GasReferenceListTests\.` (total 11 no XML), com o pacote compilando sem a janela. A suíte completa roda na `/revisao`.
- Tamanho: 499 linhas medidas (+499 −0) e 5 arquivos (os 4 `.cs` novos e este design doc), pela ADR 0042. Os `.meta` e este Detalhe não contam. Exceção: nenhuma.
- Commit: `feat(editor): estado do gerenciador do GAS e painel de referências`

## ME8b — Janela do gerenciador do GAS
### Detalhe (2026-11-05)
- CA: CA7 a CA10 e CA15, na parte da UI. A janela `SpellBook/Gerenciador do GAS` mostra o estado do `GasManagerModel` e as linhas do `GasReferenceList` (ME8a), com "Abrir no Inspector". Duplicar e Renomear ficam para o ME9, e Excluir e a tag filha, para o ME10.
- Depende do ME8a, já na branch do lote. Os arquivos da janela, escritos com o ME8a e guardados à parte, foram restaurados e conferidos contra o `GasManagerModel`, o `GasReferenceList`, o `ReferenceFinder` (ME6) e o `GasProjectIndex` (ME7) da hora.
- Conferência com o DS (seção 3, 5, 7 e 8): barra com busca, botões de tipo com contagem e seletor de tag em árvore, desligada enquanto o índice carrega ("Lendo o projeto…"); chips com ✕ e "Limpar filtros"; lista com grupos e, no tipo Tags, árvore pelo pai; painel com "Usa" e "Usado por", a linha fixa das cenas fechadas e o clique que destaca a referência; rodapé com "Abrir no Inspector" à direita (Enter ou duplo clique). A janela não edita campos. A fileira do rodapé e a linha da tag já têm lugar para Duplicar e Renomear (ME9) e para Excluir e o "+" da tag filha (ME10), que este ticket não implementa. Ajuste da conferência: o botão desligado agora mostra o motivo também no tooltip (DS 3).
- Modelos do pacote no índice (decisão do usuário, 2026-11-05, opção A): o índice deixa de fora tudo que está em `Packages/com.spellbook/Editor/GAS/Templates/`, os assets modelo e as tags-modelo (`TAG_Template_Ability`, `TAG_Template_Cooldown`) do ME3a. As outras opções eram mostrá-los por um botão "Mostrar modelos do pacote" ou ler só `Assets/`. A mudança fica no `GasProjectIndex` do ME7, como consequência direta deste ticket, e entra na contagem.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Editor/GAS/Manager/GasManagerWindow.cs` (novo, `internal`): a janela `SpellBook/Gerenciador do GAS` em UI Toolkit. O `CreateGUI` é `internal` para o teste de fumaça montar a UI sem mostrar a janela, porque o batchmode roda sem placa de vídeo.
  - `Editor/GAS/Manager/GasManager.uss` (novo): o estilo, com o token `--spellbook-text-secondary` nos dois temas, nos valores do DS do Inspector de ações, e o contorno de foco de 2 px.
  - `Editor/GAS/Manager/GasProjectIndex.cs:20-21,60-62`: a pasta dos modelos do pacote fica fora do `LoadAssets`.
  - `Tests/GAS/EditMode/GasProjectIndexTests.cs` e `Tests/GAS/EditMode/GasManagerWindowTests.cs` (novo).
  - Sem `CHANGELOG.md`, sem manual e sem mudança de API pública ou de formato serializado.
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`): `GasProjectIndexTests.PackageTemplates_AreLeftOut` (uma tag em `Editor/GAS/Templates/` não entra no índice e as do projeto entram); `GasManagerWindowTests.Open_BuildsToolbarListPanelAndFooter` (barra, busca, lista, painel com "Selecione um item para ver o que ele usa e quem o usa." e "Abrir no Inspector" desligado com o motivo no tooltip).
- Critério de pronto: 17 verdes no filtro `\.GasManagerWindowTests\.;\.GasProjectIndexTests\.;\.GasManagerModelTests\.;\.GasReferenceListTests\.` (os 2 novos, os 4 do índice e os 11 do ME8a), com o `total` do XML igual a 17 e sem warning de compilação.
- Pendência para o ME4: o estado vazio do DS 7 pede o botão "Criar habilidade ou efeito…", que abre o assistente do ME4, ainda fora da branch. A janela mostra só a mensagem; o ME4 acrescenta o botão.
- Tamanho: 335 linhas (+333 −2): `GasManagerWindow.cs` 265, `GasManagerWindowTests.cs` 42, `GasProjectIndexTests.cs` +20, `GasProjectIndex.cs` +6 −2. São 6 arquivos (os 5 de código, contando o `.uss`, e este design doc), pela ADR 0042. Os `.meta` e este Detalhe não contam. Exceção: nenhuma.
- Commit: `feat(editor): janela do gerenciador do GAS (#405)`
