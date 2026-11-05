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

## Tickets
Tamanho estimado pela 0042; a Fábrica confirma no Detalhe. Os `.asset` e os prefabs contam só como arquivo. Para caber no teto, os modelos podem compartilhar os assets modelo de custo, de recarga e de dano; a cópia sai separada de qualquer jeito.

| Ticket | Objetivo | CA | Depende de | Unity |
|---|---|---|---|---|
| ME1 | `AbilityTemplate` (modelos, regras de nome, tags), Inspector de uma linha e regra do validador para modelo ausente. ~200 linhas, ~6 arquivos | CA1, CA5 | gate; OA1 (0043 aceita) | sim |
| ME2 | Motor de criação sem UI: plano de nomes, colisão, cópia com troca de referências (também em `[SerializeReference]`), referência a prefab mantida sem cópia, tags com nome inteiro e pai, criadas ou reutilizadas, desfazer tudo se um passo falha e apagar o conjunto criado. ~450 linhas, ~5 arquivos | CA2, CA3, CA4 | ME1 | sim |
| ME3 | Modelos Projétil, Área e Buff, o prefab de projétil do pacote e o teste de que cada modelo cria um conjunto sem erro no validador. ~80 linhas, ~11 arquivos | CA1, CA3 | ME2 | sim |
| ME4 | Janela do assistente (modelos, nome, pasta, lista do que será criado, Criar, Desfazer criação), `SpellBook/Criar habilidade ou efeito…` e `Create > SpellBook > GAS > Ability from Template…`; saída do `FolderStructureCreator` com o `CHANGELOG.md`. ~400 linhas, ~7 arquivos | CA1, CA2, CA4, CA6, CA15 | ME3; DS | sim |
| ME5 | Modelos Aura (com o prefab de thinker do pacote), Em branco e Só efeito. ~60 linhas, ~9 arquivos | CA1, CA3 | ME3 | sim |
| ME6 | Busca de referências genérica no `Core.Editor`; o Find References dos canais passa a usá-la. ~250 linhas, ~5 arquivos | CA9 | gate | sim |
| ME7 | Índice do GAS sem UI: habilidades, efeitos, atributos e tags; filtros por tipo, tag e texto; atualização quando assets mudam. ~350 linhas, ~4 arquivos | CA7, CA8 | gate | sim |
| ME8 | Janela do gerenciador (`SpellBook/Gerenciador do GAS`): lista, filtros, painel com uso e referências, Abrir no Inspector, árvore de tags. ~450 linhas, ~6 arquivos | CA7 a CA10, CA15 | ME6, ME7; DS | sim |
| ME9 | Duplicar (habilidade e efeito) e Renomear (asset, conjunto com a lista de derivados, e tag com a lista das filhas), pelo motor do ME2. ~450 linhas, ~6 arquivos | CA11, CA12, CA13 | ME2, ME8 | sim |
| ME10 | Excluir asset e tag com a lista de usos e a confirmação, e criar tag filha. ~300 linhas, ~5 arquivos | CA13, CA14 | ME2, ME6, ME8 | sim |

O ME6 e o ME7 não dependem do assistente e podem correr em paralelo com o ME1 a ME3, se os Detalhes não tiverem arquivo em comum. O ME9 e o ME10 tocam a mesma janela do ME8; rodam em sequência, a não ser que os Detalhes separem os arquivos. O `Editor/GAS/GasEditors.cs` também é tocado pelo IH1 do Inspector; quem detalhar depois confere a ordem.

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
