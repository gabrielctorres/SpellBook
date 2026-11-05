# Spec: UI-4, lista de buffs e debuffs
PRD: [Documentation~/prd/ui-lista-de-buffs.md](../prd/ui-lista-de-buffs.md) · Backlog: B-018 · ADRs: usa **0039** e **0038** (novas, Proposta), 0003, 0004, 0007, 0012, 0013, 0014, 0015, 0017, 0023, 0026

Status: Proposta para o gate do Lote E · Data: 2026-10-31 · Autor: Arquiteto

## Contexto do código

Não há lista de efeitos no pacote. Este item usa:
- B-003: `ActiveEffects` na ordem de aplicação, `FindEffects(GameplayEffect, List)`, `OnEffectApplied`, `OnEffectRemoved`, `OnEffectStacksChanged`, `TimeRemaining`, `AppliedDuration`, `CurrentStacks` (EA2 a EA4). Mudar a duração (`Extend`/`Reduce`/`Restart`) e renovar por reaplicação não disparam evento (EA spec, Design 3 e 4).
- B-005: `IsBuff`, `IsDebuff`, `IsHidden`, `IsPurgable`, `DisplayName` e `Icon` do `GameplayEffect` (AR7); `TextRole` (AR4). O `DisplayName` vira `LocalizedText` no DE5.
- B-006: `GameplayTagQuery` sobre `GrantedTags` (TG3).
- B-015 e B-016: `UIListSource`, `UIListBinder` com `maxVisible` e "+N", `UIItemContext`, `ListOverflowView` (SL1, SL8), `UIUpdateDriver`, `UITheme`.
- B-017: `EffectAggregate` e `EffectTooltipSource` (TT5), `TooltipTrigger` (TT3).

## Respostas às perguntas técnicas do PRD (seção 8)

- **P1. Campos do efeito no AR7.** Confirmado: a Spec do B-005 (emenda de 2026-10-31) põe `displayName` e `icon` no `GameplayEffect`, com leitura pública, no AR7; o DE5 troca o nome por `LocalizedText` (chave + texto padrão) sem quebrar o asset. A fila põe AR7 (Lote B) e DE5 bem antes do BF1. Este item não acrescenta campo ao GAS; só um aviso no `Validate` do efeito (BF5, R14), que a Spec do B-005 deixou para cá.
- **P2. Eventos.** `OnEffectApplied`, `OnEffectRemoved`, `OnEffectStacksChanged` bastam. Duração mudada por código e renovação não têm evento, mas não precisam: enquanto há grupo com duração, o tick lê `TimeRemaining` das instâncias a cada passo. As tags de um efeito são do asset (`GrantedTags`) e não mudam em runtime; "o efeito que deixa de casar o filtro" acontece quando o filtro muda (`SetFilter`), que refaz a lista.
- **P3. Fusão sem alocar.** A ligação tem uma tabela de grupos pré-alocada (`struct Group { GameplayEffect effect; int id; }`, na ordem em que o efeito apareceu) e uma lista de instâncias reaproveitada. Cada evento recalcula só o grupo do efeito pelo `EffectAggregate` (TT5). Grupo novo vai para o fim; grupo vazio sai; os outros não mudam de lugar (C6). O binder de lista reaproveita o objeto quando só o conteúdo muda (SL1).
- **P4. Identificador do efeito.** O item tem um id inteiro estável (o do grupo) e o `EffectIconModel.Subject` (o asset do efeito como `ScriptableObject`); o dono vem do `UIItemContext` escrito pelo binder. A view escreve o `Subject` no `UIItemContext`, e o `TooltipTrigger` do ícone lê dono e assunto de lá. Nenhuma referência ao GAS na view.
- **P5. Filtro.** Campos da fonte (`EffectListFilter`, `[Serializable]`, em linha), não asset: é configuração de cada lista (0013, item 5). Duas listas com o mesmo filtro repetem os campos.
- **P6. Tick.** A ligação conta os grupos com duração finita e chama `RequestTick(contagem > 0)` a cada mudança; o último que sai solta o tick.
- **P7. Ordem.** Fonte com fusão (BF1), filtros e fim (BF2), binder e pool (BF3), views, prefabs e tooltip (BF4), validação e prévia (BF5).

## Design

### 1. Modelo (`SpellBook.UI`, `UIModels.cs`)

`EffectIconModel`: `Icon` (`Sprite`, pode ser nulo), `Name` (`string`, já resolvido pelo provedor), `Role` (`TextRole`: `Beneficial`, `Harmful`, `Neutral`), `Stacks`, `ShowStacks` (pilhas ≥ `minStacksToShow`), `Fraction`, `Remaining`, `RemainingText` (`TextSpan`), `IsInfinite`, `EndingSoon`, `Subject` (`ScriptableObject`).

### 2. Fonte (`SpellBook.GAS.UI`, `Runtime/GAS.UI/ActiveEffectsSource.cs`)

- `ActiveEffectsSource : UIListSource<EffectIconModel>`: `EffectListFilter filter`, `int minStacksToShow = 2`, `float endingSoonSeconds = 3`.
- `EffectListFilter`: `EffectRoleFilter role` (`Both`, `Buffs`, `Debuffs`), `GameplayTagQuery query` (vazia: aceita tudo), `bool purgableOnly`. `IsHidden` sempre esconde, sem opção (P4 do PRD). **"Categoria" (R4) é a consulta de tags** sobre `GrantedTags` (ex.: `Status.Veneno`); não há campo de categoria no efeito (Q6).
- Papel: `IsBuff` → `Beneficial`; `IsDebuff` → `Harmful`; nenhum ou os dois → `Neutral`.
- `Attach`: lê `ActiveEffects` e monta os grupos na ordem de aplicação (C11); assina os três eventos. `OnEffectApplied`: grupo existente é atualizado, senão nasce no fim se passa no filtro. `OnEffectRemoved`/`OnEffectStacksChanged`: atualiza o grupo; sem instâncias, remove. Tick: atualiza `Remaining`, `Fraction` (restante ÷ duração aplicada da instância de maior restante), `EndingSoon` (restante ≤ `endingSoonSeconds`, nunca em infinito) dos grupos com duração.
- `SetFilter(EffectListFilter)`: refaz a lista (C5).
- Prévia (C13): buff com 3 pilhas, debuff em 60 % com 3 s, permanente.

### 3. Binder e views

- `EffectListBinder : UIListBinder<EffectIconModel>` (`SpellBook.UI`), com `maxVisible` e a view de "+N" do SL1 (R6, C7).
- `EffectIconView` (`SpellBook.UI.UGUI`): imagem (`defaultIcon` quando o modelo não tem, CA17), moldura pela cor do papel (`UITheme`), radial, segundos (`SetCharArray`), pilhas, sinal de fim (a view pisca enquanto `EndingSoon`, pela própria animação do `Animator` ou `CanvasGroup` configurado no prefab, sem `Update` de código), escreve `Subject` no `UIItemContext`.
- Prefabs: `EffectIcon` (`UIItemContext`, `EffectIconView`, `TooltipTrigger` com `EffectTooltipSource` sem efeito, assunto do item, R10) e `EffectList` (`EffectListBinder`, `HorizontalLayoutGroup`, `ListOverflowView`).

### 4. Validação e Inspector (R13, R14)

- `EffectListBinder`: sem fonte, sem prefab, prefab sem `IView<EffectIconModel>` (erro); `maxVisible` negativo (erro).
- `ActiveEffectsSource`: consulta com tag inválida (pelo `GameplayTagQuery.Validate`, TG4); `endingSoonSeconds` negativo (erro). O aviso do PRD "filtro por papel e por `IsBuff` contraditório" não se aplica: o filtro tem um campo de papel só.
- `GameplayEffect.Validate` ganha: efeito com duração ou infinito, visível (não `IsHidden`) e sem `Icon` (aviso: "cai no ícone padrão da lista"). O aviso de `IsBuff` e `IsDebuff` juntos já é do AR7.
- Resumo de uma linha pelo `UIBinderEditor` (UI13) e o `[TypeCategory(Line = ...)]` da fonte: "Lista de efeitos · só debuffs · até 8".

### 5. O que não muda

- Nenhum registro paralelo de efeitos (N3): o estado vem do ASC; a tabela de grupos guarda só o asset e o id. O GAS não referencia UI. O Sample não muda (CA22).

## Quebras de compatibilidade

Nenhuma. Adicionado: `EffectIconModel`, `ActiveEffectsSource`, `EffectListFilter`, `EffectListBinder`, `EffectIconView` e prefabs, aviso de efeito visível sem ícone.

## Tickets

Todos usam a Unity e seguem o WIP 1. **Não há par paralelo sem Editor.** Mesmas regras de teste do B-015. Teto: ~500 linhas e 10 arquivos.

**Conflitos com a fila:** `UIModels.cs`, `UIEditors.cs`, `UGUIEditors.cs`, `DocUrls.cs`, `WidgetPrefabsTests.cs` e `validador.md` (B-015 a B-017): em série. BF5 toca `GameplayEffect.cs` (Lotes B a D, DE5).

### BF1 — `EffectIconModel` e `ActiveEffectsSource` com fusão por efeito
- Escopo: `UIModels.cs` (`EffectIconModel`), `Runtime/GAS.UI/ActiveEffectsSource.cs` (novo, sem filtros além de `IsHidden`), `Tests/GAS.UI/PlayMode/ActiveEffectsSourceTests.cs` (novo), `DocUrls.cs` (`UiEffectList`), `DocUrlsTests.cs`, `Documentation~/manual/ui/lista-de-efeitos.md` (nova), `CHANGELOG.md`.
- Testes red: `Apply_AddsIcon_Expire_RemovedAndReleased_RemoveByCodeTagStacks_OnlyWhenLast` (CA1, C1); `Fraction_Seconds_ManualTime_PauseStops_InfiniteNoClock` (CA2, C2); `Stacks_ByEvent_HiddenAtOneByDefault` (CA3, C3); `Role_FromBuffDebuff_NeutralDefault` (CA4, C4); `ApplicationOrder_NewAtEnd_PositionFixed` (CA6, C6); `SameEffect_TwoSources_WithAndWithoutCanStack_OneIcon_SumStacks_MaxRemaining_InfiniteIfAny_DifferentAssetsNeverMerge` (CA8, C8); `OpenWithActiveEffects_ShowsAll` (CA11, C11); `IconAndName_FromEffectFields` (CA17); `List_MatchesAscAfterRandomOps_NoOwnRegistry` (CA18).
- Critério de pronto: CA1 a CA4, CA6, CA8, CA11, CA17 e CA18 verdes.
- Tamanho estimado: ~490 linhas; 7 arquivos. Depende de: TT5, SL1c (o SL1 se dividiu em SL1a, SL1b e SL1c em 2026-11-04, ADR 0042).

### Detalhe (2026-11-04)
O BF1 passou do teto (796 linhas medidas depois do green, ~59 % acima) e foi dividido por decisão do Controlador: BF1a (#268) e BF1b (issue nova). A implementação completa, com o Detalhe de antes da divisão, está guardada fora do repositório, em `C:/Users/gabri/orca/workspaces/SpellBook/_guardado-bf1b/` (`completo/` com os arquivos inteiros e `bf1-completo-rastreados.patch` com o diff dos arquivos rastreados).

Comum aos dois:
- Fora do escopo da Spec original: `Documentation~/manual/ui/lista-de-efeitos.md` (só no passe de documentação, emenda de 2026-11-03 da ADR 0007) e `CHANGELOG.md` (nada quebra).
- Nota do C8: o ASC mantém uma instância por asset (`FindActiveInstance`): reaplicar de outra fonte empilha (`CanStack`) ou renova (sem `CanStack`). O teste do BF1b cobre a fusão observável pelo ASC (duas fontes, com e sem `CanStack`, um ícone; assets diferentes com o mesmo nome não se fundem). A regra entre várias instâncias (soma, maior restante, infinito se alguma) é do `EffectAggregate`, testada no TT5a, e a fonte a usa sem regra própria.
- A devolução ao pool do CA1 aqui é o `RemoveItem` do id; o pool em si é do binder (BF3, CA14).

#### BF1a — `EffectIconModel` e `ActiveEffectsSource` por evento (#268)
- CA: CA1, CA3, CA4, CA6, CA11 e CA17 (C1, C3, C4, C6, C11), na fonte.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/UI/UIModels.cs` (depois do `ListOverflowModel`, ~linha 117): `readonly struct EffectIconModel` com construtor `(Sprite icon, string name, TextRole role, int stacks, bool showStacks, float fraction, float remaining, TextSpan remainingText, bool isInfinite, bool endingSoon, ScriptableObject subject)`; fração entre 0 e 1, restante e pilhas negativos valem 0.
  - `Runtime/GAS.UI/ActiveEffectsSource.cs` (novo): `sealed class ActiveEffectsSource : UIListSource<EffectIconModel>`, `[Serializable, TypeCategory("Efeitos", Summary = ...)]` (o `Line` é do BF5), campo `minStacksToShow` (2, `Min(1)`). `OwnerType` = ASC. A ligação guarda os grupos (`struct Group { GameplayEffect Effect; int Id; string Name; }`, na ordem em que o efeito apareceu), a lista de instâncias de rascunho (sai vazia de cada agregado) e o contador de ids; nada mais do ASC (N3). Cada evento (`OnEffectApplied`, `OnEffectRemoved`, `OnEffectStacksChanged`) refaz só o grupo do efeito pelo `EffectAggregate.Of(asc, efeito, lista)`: sem instância, `RemoveItem`; grupo existente, `UpdateItem`; senão, se o efeito não é `IsHidden`, nasce no fim com `AddItem`. `Attach` monta os grupos pela ordem de `ActiveEffects` (C11). Papel: só `IsBuff` → `Beneficial`; só `IsDebuff` → `Harmful`; senão `Neutral`. Nome por `DisplayName.Resolve(Context.Text)` ao nascer, ícone por `Icon`, `Subject` = o asset, `IsInfinite` pelo agregado. Sem tick: fração, restante e texto do tempo ficam em 0 e vazio até o BF1b; `EndingSoon` falso (BF2). Prévia vazia (BF1b).
  - `Runtime/Core/DocUrls.cs:35`: `UiEffectList = Base + "ui/lista-de-efeitos/"`; `Tests/Core/EditMode/DocUrlsTests.cs:43`: o caso dele. A página fica para o passe de documentação, como a do `UiTooltip`.
  - `Tests/GAS.UI/PlayMode/ActiveEffectsSourceTests.cs` (novo): ligação direta com um receptor de teste, `GasFactory` e `ManualTimeSource`.
- Testes red (PlayMode, `ActiveEffectsSourceTests`): `Apply_AddsIcon_Expire_RemovedAndReleased_RemoveByCodeTagStacks_OnlyWhenLast` (CA1); `Stacks_ByEvent_HiddenAtOneByDefault` (CA3); `Role_FromBuffDebuff_NeutralDefault` (CA4); `ApplicationOrder_NewAtEnd_PositionFixed` (CA6); `OpenWithActiveEffects_ShowsAll` (CA11); `IconAndName_FromEffectFields` (CA17). EditMode: o caso `UiEffectList` em `DocUrlsTests`.
- Critério de pronto: os testes acima verdes, com as guardas `GasUiSourceScanTests`, `AsmdefRulesTests`, `SourceRulesTests` e `HelpUrlTests` (N2, CA20).
- Tamanho: 536 linhas medidas (`UIModels.cs` +61, `ActiveEffectsSource.cs` 169, `ActiveEffectsSourceTests.cs` 304, `DocUrls.cs` +1, `DocUrlsTests.cs` +1) e 5 arquivos. Exceção: até +30% (~7 % acima do teto); motivo: o modelo inteiro do Design 1 entra junto da primeira fonte que o publica, e os seis testes dividem um receptor e uma fábrica de efeitos de ~100 linhas.
- Commit: `feat(gas-ui): ActiveEffectsSource por evento (#268)`

#### BF1b — Relógio, fusão e prévia da `ActiveEffectsSource` (issue nova)
- CA: CA2, CA8 e CA18 (C2, C8), mais a prévia do C13 na fonte. Depende de: BF1a.
- Escopo (caminhos relativos a `Packages/com.spellbook/`; o código está em `_guardado-bf1b/completo/`):
  - `Runtime/GAS.UI/ActiveEffectsSource.cs`: o `Group` ganha `TextBuffer Text`, `ShownSeconds` e `Timed`, com os buffers reaproveitados numa lista livre. `Tick` (pedido só com grupo de duração finita, P6, por `RequestTick` a cada evento e no `Attach`) atualiza restante, fração (restante ÷ `Duration` do agregado) e texto ("N s", segundos inteiros para cima, como o tooltip, reescrito só quando o segundo muda) dos grupos com duração; os eventos publicam o mesmo. Prévia (C13): buff com 3 pilhas, debuff em 60 % com 3 s e um permanente, sem ícone e sem nome.
  - `Tests/GAS.UI/PlayMode/ActiveEffectsSourceTests.cs`: o `UIUpdateDriver` com `ManualTimeSource` no `SetUp`, o registro da ligação no driver e os testes abaixo.
- Testes red (PlayMode, `ActiveEffectsSourceTests`): `Fraction_Seconds_ManualTime_PauseStops_InfiniteNoClock` (CA2); `SameEffect_TwoSources_WithAndWithoutCanStack_OneIcon_SumStacks_MaxRemaining_InfiniteIfAny_DifferentAssetsNeverMerge` (CA8); `List_MatchesAscAfterRandomOps_NoOwnRegistry` (CA18); `Preview_BuffStacks_DebuffClock_Permanent`. O `OpenWithActiveEffects_ShowsAll` volta a conferir que abrir com duração pede tick.
- Critério de pronto: os testes do BF1b verdes, com as guardas do BF1a.
- Tamanho: ~260 linhas (fonte +100, testes +160, pela diferença entre a versão guardada e o BF1a) e 2 arquivos.
- Commit: `feat(gas-ui): relógio, fusão e prévia da lista de efeitos (#<issue nova>)`

### BF2 — Filtros, fim próximo e respawn
- Escopo: `ActiveEffectsSource.cs` (`EffectListFilter`, `SetFilter`, `EndingSoon`), `Tests/GAS.UI/PlayMode/ActiveEffectsFilterTests.cs` (novo), `lista-de-efeitos.md`, `CHANGELOG.md`.
- Testes red: `Filter_Debuffs_Query_Purgable_HiddenNever_FilterChangeRemoves` (CA5, C5); `EndingSoon_LastNSeconds_OfLongestInstance` (CA9, C9); `AnchorRespawn_RebuildsForNewAsc_ReleasesOldIcons` (CA12, C12).
- Critério de pronto: CA5, CA9 e CA12 verdes.
- Tamanho estimado: ~380 linhas; 4 arquivos. Depende de: BF1.

### BF3 — `EffectListBinder`, limite visível e pool
- Escopo: `Runtime/UI/EffectListBinder.cs` (novo), `UIEditors.cs`, `Tests/GAS.UI/PlayMode/EffectListBinderTests.cs` (novo; spawner de teste que conta vivos), `lista-de-efeitos.md`, `CHANGELOG.md`.
- Testes red: `MaxVisible8_11Effects_8PlusThree_PromotesOldestHidden` (CA7, C7); `Icons_FromSpawner_100ApplyRemove_LiveStable` (CA14, C14); `Ticks10000With8Icons_1000ApplyRemove_NoAlloc` (CA15, C15).
- Critério de pronto: CA7, CA14 e CA15 verdes.
- Tamanho estimado: ~330 linhas; 5 arquivos. Depende de: BF2.

### BF4 — Views, prefabs e tooltip do ícone
- Escopo: `Runtime/UI.UGUI/EffectIconView.cs` (novo), `UGUIEditors.cs`, `Runtime/UI.UGUI/Prefabs/EffectIcon.prefab`, `EffectList.prefab` (fixtures), `Tests/UI.UGUI/EditMode/EffectIconViewTests.cs` (novo), `Tests/UI.UGUI/EditMode/WidgetPrefabsTests.cs` (casos novos), `Tests/GAS.UI/PlayMode/EffectIconTooltipTests.cs` (novo), `lista-de-efeitos.md` (seção "Tooltip em buff"), `CHANGELOG.md`.
- Testes red: `IconView_FrameByRole_RadialSecondsStacks_EndingSoon_Infinite`; `NullIcon_UsesDefaultIcon` (CA17); `WritesSubjectToItemContext`; `EffectIconPrefab_HasTooltipWithEffectSourceFromItem`; `HoverIcon_OpensEffectTooltip_SameStacksAndRemaining_NoBinderStillWorks` (CA10, C10).
- Critério de pronto: CA10 verde; C13 (S) prévia conferida no host.
- Tamanho estimado: ~380 linhas; 9 arquivos (2 fixtures). Depende de: BF3, TT8.

### BF5 — Validação, Inspector e fechamento da documentação
- Escopo: `ActiveEffectsSource.cs`, `EffectListBinder.cs` (`Validate`, `[TypeCategory(Line)]`), `Runtime/GAS/Effects/GameplayEffect.cs` (aviso de efeito visível sem ícone), `Tests/GAS.UI/EditMode/EffectListValidationTests.cs`, `Tests/GAS/EditMode/EffectIconValidationTests.cs` (novos), `manual/ferramentas/validador.md`, `lista-de-efeitos.md` (fonte e view próprias), `CHANGELOG.md` (R15 completo).
- Testes red: um por regra do Design 4, com o objeto clicável (CA19); `VisibleEffectWithDurationWithoutIcon_Warning_InstantOrHiddenNot`.
- Critério de pronto: CA19 verde; CA13 e CA16 (S) no host; CA20 pela guarda do UI14; CA21 e CA22 (E) conferidos.
- Tamanho estimado: ~320 linhas; 8 arquivos. Depende de: BF4.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| BF1 | sim | TT5, SL1c | ~490 / 7 |
| BF2 | sim | BF1 | ~380 / 4 |
| BF3 | sim | BF2 | ~330 / 5 |
| BF4 | sim | BF3, TT8 | ~380 / 9 |
| BF5 | sim | BF4 | ~320 / 8 |

Fila: BF1 → BF5 em série, depois do TT5 (BF4 depois do TT8). CA18 e CA20 rodam em todos.

## Perguntas ao usuário

- **Q6. "Categoria" no filtro da lista.** Proposta: o filtro por categoria é a `GameplayTagQuery` sobre as tags do efeito (ex.: `Status.Veneno`), sem campo novo no GAS, como o R1 pede. Alternativa: um campo de categoria no `GameplayEffect` (asset `EffectCategory`, como a `AttributeCategory`), o que muda o AR7 e põe um segundo jeito de classificar efeitos além das tags.

## Respostas do gate (2026-11-01)

Spec aprovada no gate do Lote E, com as ADRs 0038 e 0039 (Aceitas).
- Q6: aprovada como proposta. O filtro por categoria é uma `GameplayTagQuery` sobre as tags do efeito (BF2); nenhum campo novo no `GameplayEffect`, e o AR7 não muda.
- As respostas Q1 a Q5 do mesmo gate estão nas Specs do B-008, do B-015 e do B-016.
