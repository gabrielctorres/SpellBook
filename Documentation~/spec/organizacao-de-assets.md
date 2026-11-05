# Design doc: Organização dos assets (menu Create, nome padrão e ícones)
Épico: `epic:organizacao-de-assets` · Backlog: B-038 e B-039 · ADRs: 0043 (Proposta) · Status: Rascunho (gate parcial de 2026-11-04: Q1 e Q2 respondidas; Q3 e Q4 em aberto)

## Problema
Quem cria um asset do pacote pelo `Create` encontra a raiz, o grupo e o nome padrão diferentes em cada módulo:
- `GAS/Tag Definition` gera `NewTag`;
- `SpellBook/Teams/Team` gera `NewTeam`;
- `SpellBook/Events/Int Channel` gera `New Int Event Channel`;
- o `AbilityLoadout` está em `UI`, e o `FloatingTextRules`, do mesmo módulo, em `GAS/UI`.

No Project window, todo asset do pacote tem o mesmo ícone genérico de ScriptableObject. O designer só diferencia uma habilidade de um efeito ou de um canal pelo nome. A issue #344 (B-034) corrige só a raiz `SpellBook/` e entra antes deste épico.

## Critérios de aceite
1. **CA1.** Todo tipo do pacote com `[CreateAssetMenu]` aparece em `Create > SpellBook > <Grupo>`, com o grupo da tabela da ADR 0043, e nenhum caminho se repete.
2. **CA2.** O asset novo nasce com o nome `New <Nome>`, sem prefixo, com o mesmo nome do item do menu. Exemplo: `New Ability` em `Create > SpellBook > GAS > Ability`.
3. **CA3.** Uma guarda EditMode falha quando um tipo do pacote sai da regra de menu ou de `fileName`, e quando dois tipos têm o mesmo menu. Os tipos de exemplo em `Tests/` ficam fora da guarda.
4. **CA4.** Cada família da ADR 0043 tem um ícone próprio. Ele aparece no Project window (lista e grade) e no cabeçalho do Inspector, nos temas claro e escuro.
5. **CA5.** A guarda do CA3 falha também quando um tipo do pacote fica sem ícone.
6. **CA6.** Os assets que já existem (os do Sandbox, por exemplo) continuam com o nome e as referências, sem `Missing`, e ganham o ícone da família sem nenhuma ação manual.

## Cenário no Sandbox
Com o Sandbox importado (`Packages/com.spellbook/Samples~/Sandbox`):
1. Abra `Create > SpellBook`. Os grupos seguem a tabela da 0043, e `GAS` tem os subgrupos `Attributes`, `Events` e `UI` (CA1).
2. Crie um `Ability` e um `Void Channel`. Os arquivos nascem como `New Ability` e `New Void Channel` (CA2).
3. Abra `Data/` do Sandbox no Project window, em lista e em grade. `GA_BolaDeFogo`, os `GE_*`, os `CH_*`, os `SP_*` e as `TAG_*` mostram o ícone da família, e nenhum asset aparece como `Missing` (CA4, CA6).
4. Troque o tema do Editor (Preferences > General > Editor Theme) e repita o passo 3 (CA4).

## Fora do escopo
- Renomear assets que já existem no projeto do usuário. A regra vale para os assets novos.
- Ícone por componente (`MonoBehaviour`) e ícone de Gizmo na cena.
- A ordem dos itens dentro do grupo (`order` do `CreateAssetMenu`).
- O menu do wizard de templates. Ele é do B-019a e segue a 0043.

## Decisões do épico
- **Gate parcial de 2026-11-04 (usuário):**
  - Q1: o nome padrão fica sem prefixo (`New Ability`);
  - Q2: `Text` e `Spawn` ficam como grupos por função.

  Com isso, o nome do item no menu perde o `Gameplay` (`Ability`, `Effect`, `Tag`; ADR 0043, item 1).
- **Em aberto até o protótipo:** Q3 (estilo dos ícones: SVG pelo Claude ou PNG pelo Codex) e Q4 (uma versão para os dois temas ou uma por tema). Os OA5 a OA7 só vão para issue depois dessas respostas.
- Os tickets de menu (OA1 a OA4) não esperam a decisão do estilo dos ícones. Os de ícone (OA5 a OA7) esperam o protótipo e a resposta do usuário (ADR 0043, "Em aberto").
- A guarda nasce no OA1 com a lista dos módulos já conferidos. Cada ticket seguinte acrescenta os módulos dele, e o OA4 tira a lista, porque a guarda passa a valer para o pacote inteiro.

## Tickets
Tamanho estimado pela 0042. A Fábrica confirma no Detalhe.

| Ticket | Objetivo | CA | Depende de | Unity |
|---|---|---|---|---|
| OA1 | Guarda de menu e `fileName` (módulos GAS e Tags); menus e `fileName` do GAS e do Tags pela 0043. ~150 linhas, ~11 arquivos | CA1, CA2, CA3 | #344 (B-034), 0043 aceita | sim |
| OA2 | Menus e `fileName` de Core, Time e Pooling (grupos `Core`, `Text`, `Spawn`, `Time`, `Pooling`); a guarda passa a incluir esses módulos. ~60 linhas, ~12 arquivos | CA1, CA2 | OA1 | sim |
| OA3 | Menus e `fileName` de Events, Teams e Localization; guardas do menu de canais alinhadas à 0043. ~60 linhas, ~12 arquivos | CA1, CA2 | OA2 | sim |
| OA4 | Menus e `fileName` de GAS.Bridges, GAS.UI, UI e Cues; a guarda passa a valer para o pacote inteiro. ~80 linhas, ~13 arquivos (exceção de +30% possível; dividir se passar) | CA1, CA2, CA3 | OA3 | sim |
| OA5 | Ícones das famílias do GAS e das Tags (habilidade, efeito, atributo, categoria, fórmula, política de recarga, tag, âncora) e a guarda de ícone. ~60 linhas, ~10 arquivos (as imagens contam como arquivo; os `.meta`, não) | CA4, CA5, CA6 | OA4, Q3 e Q4 respondidas (abertas) | sim |
| OA6 | Ícones de canal, fonte de tempo, spawner, configuração de pool, time e relação entre times. ~10 linhas, ~7 arquivos | CA4, CA6 | OA5 | sim |
| OA7 | Ícones de cue, biblioteca de cues, texto, tema e regras de UI; a guarda de ícone passa a valer para o pacote inteiro. ~20 linhas, ~7 arquivos | CA4, CA5, CA6 | OA6 | sim |

Antes do OA5, e fora dos tickets, vem o protótipo dos estilos: as famílias habilidade, efeito e canal desenhadas nos dois estilos (SVG pelo Claude e PNG pelo Codex) e mostradas ao usuário num artefato, no tamanho do Project window.

## Validação de uso
| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|

<!-- Parte JIT: uma seção por ticket, escrita pela Fábrica quando o ticket entra na fila, lendo o código na hora.
     Limites e contagem: ADR 0042. -->

## OA1 — Guarda de menu e `fileName`; GAS e Tags pela 0043
### Detalhe (2026-11-04)
- CA: CA1 e CA2 (módulos `SpellBook.GAS` e `SpellBook.Tags`), CA3 (guarda com a lista dos módulos conferidos; a guarda de raiz e de menu repetido da #344 continua valendo para o pacote inteiro).
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/GAS/Abilities/GameplayAbility.cs:35`: `SpellBook/GAS/Ability`, `New Ability`.
  - `Runtime/GAS/Effects/GameplayEffect.cs:46`: `SpellBook/GAS/Effect`, `New Effect`.
  - `Runtime/GAS/Abilities/CooldownReductionPolicy.cs:21`: `SpellBook/GAS/Cooldown Reduction Policy`, `New Cooldown Reduction Policy`.
  - `Runtime/GAS/Core/AbilitySystemAnchor.cs:11`: `SpellBook/GAS/Ability System Anchor`, `New Ability System Anchor`.
  - `Runtime/GAS/Attributes/AttributeDefinition.cs:33`: `SpellBook/GAS/Attributes/Attribute`, `New Attribute`.
  - `Runtime/GAS/Attributes/AttributeCategory.cs:9`: `SpellBook/GAS/Attributes/Attribute Category`, `New Attribute Category`.
  - `Runtime/GAS/Attributes/Formulas/SimpleAttributeFormula.cs:9`: `SpellBook/GAS/Attributes/Formulas/Simple Attribute Formula`, `New Simple Attribute Formula`.
  - `Runtime/GAS/Attributes/Formulas/ExpressionFormula.cs:10`: `SpellBook/GAS/Attributes/Formulas/Expression Formula`, `New Expression Formula`.
  - `Runtime/Tags/GameplayTagDefinition.cs:7`: `SpellBook/Tags/Tag`, `New Tag`.
  - `Tests/Core/EditMode/Structure/CreateAssetMenuRulesTests.cs:46-61`: a varredura passa a ler também o `fileName`; tabela `CheckedModuleMenus` (assembly → pastas de menu permitidas, pelo item 2 da 0043) só com `SpellBook.GAS` (`SpellBook/GAS/`, `SpellBook/GAS/Attributes/`, `SpellBook/GAS/Attributes/Formulas/`) e `SpellBook.Tags` (`SpellBook/Tags/`); três testes novos.
  - Sem `CHANGELOG.md` e sem manual: menu e `fileName` não são API nem formato serializado, e os assets existentes não mudam (0043, "Consequências"). Os caminhos citados no manual e no catálogo ficam para o passe de documentação.
- Testes red (EditMode, `SpellBook.Core.Tests.EditMode`, classe `CreateAssetMenuRulesTests`): `CheckedModuleMenu_UsesFolderAllowedForItsAssembly`; `CheckedModuleMenuItem_DropsGameplayPrefix`; `CheckedModuleFileName_IsNewPlusMenuItemName`.
- Critério de pronto: os 5 testes de `\.CreateAssetMenuRulesTests\.` verdes (os 2 da #344 e os 3 novos), com `total` 5 no XML.
- Tamanho: ~91 linhas (18 nos 9 tipos e 73 na guarda) e 10 arquivos, pela ADR 0042. Exceção: nenhuma.
- Commit: `chore(menu): guarda de menu e fileName; GAS e Tags pela ADR 0043`

## OA2 — Core, Time e Pooling pela 0043
### Detalhe (2026-11-04)
- CA: CA1 e CA2 para os módulos `SpellBook.Core`, `SpellBook.Time` e `SpellBook.Pooling`. O `LocalizationTextProvider` (grupo `Text`, módulo Localization) fica para o OA3.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/Core/Anchors/TransformAnchor.cs:11`: menu igual, `New Transform Anchor`.
  - `Runtime/Core/Spawning/InstantiateSpawnerAsset.cs:6`: menu igual, `New Instantiate Spawner`.
  - `Runtime/Core/Text/TableTextProvider.cs:15`: menu igual, `New Table Text Provider`.
  - `Runtime/Core/Text/TextTable.cs:13`: menu igual, `New Text Table`.
  - `Runtime/Core/Text/TextRoleTheme.cs:20`: `SpellBook/Text/Text Role Theme`, `New Text Role Theme`.
  - `Runtime/Pooling/PoolConfiguration.cs:9`: menu igual, `New Pool Configuration`.
  - `Runtime/Pooling/PooledSpawnerAsset.cs:13`: `SpellBook/Spawn/Pooled Spawner`, `New Pooled Spawner`.
  - `Runtime/Time/ScaledTimeSource.cs:7`: menu igual, `New Scaled Time Source`.
  - `Runtime/Time/UnscaledTimeSource.cs:7`: menu igual, `New Unscaled Time Source`.
  - `Runtime/Time/PausableTimeSource.cs:10`: `SpellBook/Time/Pausable Time Source`, `New Pausable Time Source` (item 1: nome do tipo sem sufixo técnico; o antigo `Pausable Time Group` fugia dele).
  - `Tests/Time/EditMode/TimeSourceTests.cs:16`: `PausableMenu` passa a `SpellBook/Time/Pausable Time Source` (a ADR 0043 manda ajustar esse teste).
  - `Tests/Core/EditMode/Structure/CreateAssetMenuRulesTests.cs:24-28`: `CheckedModuleMenus` ganha `SpellBook.Core` (`SpellBook/Core/`, `SpellBook/Text/`, `SpellBook/Spawn/`), `SpellBook.Time` (`SpellBook/Time/`) e `SpellBook.Pooling` (`SpellBook/Pooling/`, `SpellBook/Spawn/`); `:61-62`: a varredura passa a exigir também `TransformAnchor`, `PausableTimeSource` e `PoolConfiguration`.
  - Sem `CHANGELOG.md` e sem manual (mesmo motivo do OA1).
- Testes red (EditMode, `SpellBook.Core.Tests.EditMode`, classe `CreateAssetMenuRulesTests`): com a tabela ampliada, `CheckedModuleMenu_UsesFolderAllowedForItsAssembly` (`TextRoleTheme`, `PooledSpawnerAsset`) e `CheckedModuleFileName_IsNewPlusMenuItemName` (os 10 tipos) falham. A guarda não confere o nome contra o tipo; o red do `Pausable Time Source` é `TimeSourceTests.Menu_HasThreeVariants` (EditMode, `SpellBook.Time.Tests`).
- Critério de pronto: os 5 testes de `\.CreateAssetMenuRulesTests\.` e os de `\.TimeSourceTests\.` verdes, com o `total` conferido no XML.
- Tamanho: ~30 linhas (20 nos 10 tipos, ~8 na guarda e 2 no `TimeSourceTests`) e 12 arquivos, pela ADR 0042. Exceção: 12 arquivos, +20% sobre o teto de 10; o ticket toca um atributo por tipo, o `TimeSourceTests` fixa o menu do `PausableTimeSource` e muda junto, e dividir deixaria um módulo fora da guarda.
- Commit: `chore(menu): menus e fileName de Core, Time e Pooling pela ADR 0043`

## OA3 — Events, Teams e Localization pela 0043
### Detalhe (2026-11-04)
- CA: CA1 e CA2 para os módulos `SpellBook.Events`, `SpellBook.Teams` e `SpellBook.Localization`. O `GasChannelsTests` (GAS.Bridges) fica para o OA4, junto do módulo dele.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/Events/Channels/{Void,Int,Float,Bool,String,Vector3,GameObject}EventChannel.cs:7`: menu igual, `fileName` `New <Tipo> Channel` (a ADR 0043, item 3, dá `New Int Channel` como exemplo).
  - `Runtime/Teams/TeamDefinition.cs:9`: menu igual (`Team`, sem o `Definition`), `New Team`.
  - `Runtime/Teams/TeamRelationMatrix.cs:14`: `SpellBook/Teams/Team Relation Matrix`, `New Team Relation Matrix` (item 1: nome do tipo sem sufixo técnico; o antigo `Relation Matrix` cortava o `Team`, que não é sufixo).
  - `Runtime/Localization/LocalizationTextProvider.cs:19`: menu igual, `New Localization Text Provider`.
  - `Tests/Core/EditMode/Structure/CreateAssetMenuRulesTests.cs:24-31`: `CheckedModuleMenus` ganha `SpellBook.Events` (`SpellBook/Events/`), `SpellBook.Teams` (`SpellBook/Teams/`) e `SpellBook.Localization` (`SpellBook/Text/`); `:64-66`: a varredura passa a exigir também `IntEventChannel`, `TeamRelationMatrix` e `LocalizationTextProvider`; teste novo que confere o item 1 contra o nome do tipo: o nome do item, sem espaços, é o nome do tipo sem o prefixo `Gameplay`, com o `EventChannel` final lido como `Channel` (exemplo do item 3) e, opcionalmente, sem o sufixo `Asset` ou `Definition`. Todos os tipos já conferidos no OA1 e no OA2 passam nessa regra.
  - `Tests/Events/EditMode/ChannelMenuTests.cs:16-34`: a guarda dos canais passa a conferir também o `fileName` de cada um dos sete (0043, "Consequências").
  - Sem `CHANGELOG.md` e sem manual (mesmo motivo do OA1).
- Testes red (EditMode):
  - `SpellBook.Core.Tests.EditMode`, `CreateAssetMenuRulesTests`: com a tabela ampliada, `CheckedModuleFileName_IsNewPlusMenuItemName` falha (os 10 tipos); o novo `CheckedModuleMenuItem_IsTypeNameWithoutTechnicalParts` falha no `TeamRelationMatrix`.
  - `SpellBook.Events.Tests` (`ChannelMenuTests`): o novo `Menu_ChannelFileName_IsNewPlusItemName` falha nos sete canais.
- Critério de pronto: os 6 testes de `\.CreateAssetMenuRulesTests\.` e os 2 de `\.ChannelMenuTests\.` verdes, com o `total` conferido no XML.
- Tamanho: ~85 linhas (20 nos 10 tipos, ~43 na guarda e ~22 no `ChannelMenuTests`) e 12 arquivos, pela ADR 0042. Exceção: 12 arquivos, +20% sobre o teto de 10; o ticket toca um atributo por tipo (7 canais, 2 de Teams, 1 de Localization), e a guarda dos canais muda junto pela 0043; dividir deixaria um módulo fora da guarda.
- Commit: `chore(menu): menus e fileName de Events, Teams e Localization pela ADR 0043`

## OA4a — GAS.Bridges e GAS.UI pela 0043
### Detalhe (2026-11-04)
- Divisão: o OA4 (~17 arquivos, acima do teto de 13 com os +30%) foi dividido com a aprovação do Controlador. O OA4a fica na issue #348, renomeada. O OA4b, com Cues e UI e a guarda para o pacote inteiro (~9 arquivos, depende do OA4a), vai para uma issue nova. Os nomes novos do OA4b pelo item 1 (`UI Theme`, `Fixed Key Label Provider`) já foram aprovados.
- CA: CA1 e CA2 para os módulos `SpellBook.GAS.Bridges` e `SpellBook.GAS.UI`. CA3 segue com a lista dos módulos conferidos, e o OA4b a tira.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/GAS.Bridges/Channels/{Ability,AttributeChanged,AttributeLimit,Blocked,Effect,TagChanged}EventChannel.cs:8`: o menu fica igual, e o `fileName` passa a `New <Tipo> Channel`.
  - `Runtime/GAS.UI/AbilityLoadout.cs:18`: `SpellBook/GAS/UI/Ability Loadout` (item 2: o GAS.UI fica em `GAS/UI`), `New Ability Loadout`.
  - `Runtime/GAS.UI/FloatingTextRules.cs:72`: o menu fica igual, e o `fileName` passa a `New Floating Text Rules`.
  - `Tests/Core/EditMode/Structure/CreateAssetMenuRulesTests.cs:27-37`: `CheckedModuleMenus` ganha `SpellBook.GAS.Bridges` (`SpellBook/GAS/Events/`) e `SpellBook.GAS.UI` (`SpellBook/GAS/UI/`). `:70-73`: a varredura passa a exigir também `AbilityEventChannel` e `AbilityLoadout`.
  - `Tests/GAS.Bridges/EditMode/GasChannelsTests.cs`: teste novo que confere o `fileName` dos seis canais (0043, "Consequências").
  - Sem `CHANGELOG.md` e sem manual (mesmo motivo do OA1). As Specs antigas que citam `SpellBook/UI/Ability Loadout` são histórico e não mudam.
- Testes red (EditMode):
  - `SpellBook.Core.Tests.EditMode`, `CreateAssetMenuRulesTests`: com a tabela ampliada, `CheckedModuleMenu_UsesFolderAllowedForItsAssembly` falha no `AbilityLoadout`, e `CheckedModuleFileName_IsNewPlusMenuItemName` falha nos 8 tipos.
  - `SpellBook.GAS.Bridges.Tests.EditMode`, `GasChannelsTests`: o novo `GasChannels_FileName_IsNewPlusItemName` falha nos seis canais.
- Critério de pronto: os 6 testes de `\.CreateAssetMenuRulesTests\.` e os de `\.GasChannelsTests\.` verdes, com o `total` conferido no XML.
- Tamanho: ~36 linhas (16 nos 8 tipos, ~6 na guarda e ~14 no `GasChannelsTests`) e 10 arquivos, pela ADR 0042. Exceção: nenhuma.
- Commit: `chore(menu): menus e fileName de GAS.Bridges e GAS.UI pela ADR 0043`
