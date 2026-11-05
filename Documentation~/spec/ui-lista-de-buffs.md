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

#### BF1b — Relógio, fusão e prévia da `ActiveEffectsSource` (#367)
- CA: CA2, CA8 e CA18 (C2, C8), mais a prévia do C13 na fonte. Depende de: BF1a.
- Escopo (caminhos relativos a `Packages/com.spellbook/`; o código está em `_guardado-bf1b/completo/`):
  - `Runtime/GAS.UI/ActiveEffectsSource.cs`: o `Group` ganha `TextBuffer Text`, `ShownSeconds` e `Timed`, com os buffers reaproveitados numa lista livre. `Tick` (pedido só com grupo de duração finita, P6, por `RequestTick` a cada evento e no `Attach`) atualiza restante, fração (restante ÷ `Duration` do agregado) e texto ("N s", segundos inteiros para cima, como o tooltip, reescrito só quando o segundo muda) dos grupos com duração; os eventos publicam o mesmo. Prévia (C13): buff com 3 pilhas, debuff em 60 % com 3 s e um permanente, sem ícone e sem nome.
  - `Tests/GAS.UI/PlayMode/ActiveEffectsSourceTests.cs`: o `UIUpdateDriver` com `ManualTimeSource` no `SetUp`, o registro da ligação no driver e os testes abaixo.
- Testes red (PlayMode, `ActiveEffectsSourceTests`): `Fraction_Seconds_ManualTime_PauseStops_InfiniteNoClock` (CA2); `SameEffect_TwoSources_WithAndWithoutCanStack_OneIcon_SumStacks_MaxRemaining_InfiniteIfAny_DifferentAssetsNeverMerge` (CA8); `List_MatchesAscAfterRandomOps_NoOwnRegistry` (CA18); `Preview_BuffStacks_DebuffClock_Permanent`. O `OpenWithActiveEffects_ShowsAll` volta a conferir que abrir com duração pede tick.
- Critério de pronto: os testes do BF1b verdes, com as guardas do BF1a.
- Tamanho: 298 linhas medidas depois do green (`ActiveEffectsSource.cs` +112 −12, `ActiveEffectsSourceTests.cs` +167 −7) e 2 arquivos. Exceção: nenhuma.
- Commit: `feat(gas-ui): relógio, fusão e prévia da lista de efeitos (#367)`

### BF2 — Filtros, fim próximo e respawn
- Escopo: `ActiveEffectsSource.cs` (`EffectListFilter`, `SetFilter`, `EndingSoon`), `Tests/GAS.UI/PlayMode/ActiveEffectsFilterTests.cs` (novo), `lista-de-efeitos.md`, `CHANGELOG.md`.
- Testes red: `Filter_Debuffs_Query_Purgable_HiddenNever_FilterChangeRemoves` (CA5, C5); `EndingSoon_LastNSeconds_OfLongestInstance` (CA9, C9); `AnchorRespawn_RebuildsForNewAsc_ReleasesOldIcons` (CA12, C12).
- Critério de pronto: CA5, CA9 e CA12 verdes.
- Tamanho estimado: ~380 linhas; 4 arquivos. Depende de: BF1.

### Detalhe (2026-11-04)
- CA: CA5, CA9 e CA12 (C5, C9, C12). Issue #269.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/GAS.UI/ActiveEffectsSource.cs`: `enum EffectRoleFilter { Both, Buffs, Debuffs }` e `[Serializable] sealed class EffectListFilter` (`role`, `query` = `GameplayTagQuery`, `purgableOnly`; `Accepts(GameplayEffect)`: `IsHidden` nunca, papel pelo mesmo `RoleOf` do ícone (`Buffs` só `Beneficial`, `Debuffs` só `Harmful`; o efeito neutro só aparece em `Both`), consulta sobre `GrantedTags` (vazia aceita tudo), `purgableOnly` exige `IsPurgable`; imutável pelo código: construtor vazio, que aceita todos os visíveis, e `EffectListFilter(EffectRoleFilter, GameplayTagQuery = null, bool = false)`, que copia as tags da consulta; `Role` e `PurgableOnly` só de leitura e a consulta não é exposta, para que a lista só mude por `SetFilter`, que a refaz (C5; achado 1 da revisão de 2026-11-04)). Na fonte (depois da linha 29): campos `filter` e `endingSoonSeconds` (3, `Min(0)`), propriedades `Filter` (o filtro aplicado, imutável), `EndingSoonSeconds`, e `SetFilter(EffectListFilter)` (nulo vale o filtro vazio), que avisa as ligações vivas por um `Action` não serializado, como o `SetLoadout` do `AbilityLoadoutSource`. Na ligação: assina o aviso no `OnAttach` (linha 104) e solta no `OnDetach` (linha 122); `Rebuild` devolve os textos, `ClearItems` e remonta pela ordem de `ActiveEffects`; o `if (effect.IsHidden) return;` do `Refresh` (linha 166) passa a `!_source.Filter.Accepts(effect)`; o `Publish` (linha 210) liga `EndingSoon` com duração e restante ≤ `endingSoonSeconds` (a maior restante do agregado; nunca em infinito).
  - `Tests/GAS.UI/PlayMode/ActiveEffectsFilterTests.cs` (novo): receptor de teste para CA5 e CA9; para o CA12, um binder de lista de teste (`UIListBinder<EffectIconModel>`, aninhado no teste, como o `TestListBinder` do `UIListBinderTests`) com `RecordingSpawnerAsset` e `ActorResolver.ForAnchor`, porque o `EffectListBinder` é do BF3.
  - Fora do escopo da Spec original: `lista-de-efeitos.md` (só no passe de documentação, emenda de 2026-11-03 da ADR 0007) e `CHANGELOG.md` (nada quebra).
  - Nota do CA12: a ligação já solta o ASC antigo no `OnDetach` (BF1a) e o `UIListBinder` devolve os itens no `DetachBinding`; o teste prova o comportamento de ponta a ponta e pode passar sem código novo depois do BF2 compilar.
- Testes red (PlayMode, `ActiveEffectsFilterTests`): `Filter_Debuffs_Query_Purgable_HiddenNever_FilterChangeRemoves` (CA5; com um efeito `IsBuff` e `IsDebuff` juntos, que não entra em `Buffs` nem em `Debuffs`, e com a consulta mudada por fora depois do `SetFilter`, que não muda a lista); `EndingSoon_LastNSeconds_OfLongestInstance` (CA9); `AnchorRespawn_RebuildsForNewAsc_ReleasesOldIcons` (CA12).
- Critério de pronto: os três testes verdes, com `ActiveEffectsSourceTests` (BF1a e BF1b) e as guardas `GasUiSourceScanTests`, `AsmdefRulesTests`, `SourceRulesTests` e `HelpUrlTests`.
- Tamanho: 495 linhas medidas depois do green da correção (`ActiveEffectsSource.cs` +140 −13, `ActiveEffectsFilterTests.cs` 342) e 2 arquivos. Exceção: nenhuma.
- Commit: `feat(gas-ui): filtros, fim próximo e respawn da lista de efeitos (#269)`

### BF3 — `EffectListBinder`, limite visível e pool
- Escopo: `Runtime/UI/EffectListBinder.cs` (novo), `UIEditors.cs`, `Tests/GAS.UI/PlayMode/EffectListBinderTests.cs` (novo; spawner de teste que conta vivos), `lista-de-efeitos.md`, `CHANGELOG.md`.
- Testes red: `MaxVisible8_11Effects_8PlusThree_PromotesOldestHidden` (CA7, C7); `Icons_FromSpawner_100ApplyRemove_LiveStable` (CA14, C14); `Ticks10000With8Icons_1000ApplyRemove_NoAlloc` (CA15, C15).
- Critério de pronto: CA7, CA14 e CA15 verdes.
- Tamanho estimado: ~330 linhas; 5 arquivos. Depende de: BF2.

### Detalhe (2026-11-04)
- CA: CA7, CA14 e CA15 (C7, C14, C15). Issue #270.
- **Leitura do CA15 ajustada (decisão do Controlador, 2026-11-04).** O CA15 ao pé da letra ("1000 aplicações e remoções alocam 0 bytes") não se cumpre sem mudar o GAS: o ASC cria um `ActiveEffectInstance` novo a cada aplicação com duração (`Runtime/GAS/Core/AbilitySystemComponent.cs:1882`, `ApplyDurationEffect`). Medido pelo contador `GC.Alloc` (o mesmo do `Is.Not.AllocatingGCMemory`), 1000 remoções e reaplicações dão 5000 blocos com a lista e 5000 sem ela, e 10 000 ticks com 8 ícones dão 0. A lista não acrescenta alocação; quem aloca é o ASC. Por isso o CA15 vale como: **10 000 ticks com 8 ícones alocam 0; 1000 aplicações e remoções alocam o mesmo com e sem a lista.** O N1 ("a UI não aloca") continua valendo. Melhoria futura, fora do BF3 e fora do GAS neste lote: pool de `ActiveEffectInstance` no ASC.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/UI/EffectListBinder.cs` (novo): `public sealed class EffectListBinder : UIListBinder<EffectIconModel>`, vazia, com `[HelpURL(DocUrls.UiEffectList + "#effectlistbinder")]`, como o `AbilityBarBinder`. O limite visível, o "+N", a promoção do oculto mais antigo e o pool já são do `UIListBinder` (SL1); o ticket prova o comportamento de ponta a ponta com a `ActiveEffectsSource` e não muda o binder genérico. Sem API nova: nada de estado exposto além do que o `UIListBinder` já expõe. O `Validate` próprio e o `[TypeCategory(Line)]` são do BF5.
  - `Editor/UI/UIEditors.cs:50` (depois do `AbilityBarBinderEditor`): `EffectListBinderEditor : UIBinderEditor`.
  - `Tests/GAS.UI/PlayMode/EffectListBinderTests.cs` (novo): `EffectListBinder` sob um `UIUpdateDriver` com `ManualTimeSource`, ícones do `RecordingSpawnerAsset` (conta vivos), view de ícone de teste e view de "+N" de teste (`IView<ListOverflowModel>`).
  - Consequência direta (ADR 0042, item 6): `Tests/GAS.UI/PlayMode/ActiveEffectsFilterTests.cs:233` e `:329` trocam o `IconListBinder` de teste, que esperava este ticket, pelo `EffectListBinder`.
  - Fora do escopo da Spec original: `lista-de-efeitos.md` (só no passe de documentação, emenda de 2026-11-03 da ADR 0007) e `CHANGELOG.md` (nada quebra).
- Testes red (PlayMode, `EffectListBinderTests`): `MaxVisible8_11Effects_8PlusThree_PromotesOldestHidden` (CA7); `Icons_FromSpawner_100ApplyRemove_LiveStable` (CA14); `Ticks10000With8Icons_NoAlloc_1000ApplyRemove_SameAllocsAsAscAlone` (CA15, na leitura acima; o nome da Spec, `Ticks10000With8Icons_1000ApplyRemove_NoAlloc`, mudou para dizer o que o teste mede). Red: o tipo `EffectListBinder` não existe (erro de compilação). Mutações conferidas depois do green, todas desfeitas: sem a promoção no `RemoveItem` do `UIListBinder` (CA7 cai: 7 ícones em vez de 8); sem o `Release` ao spawner (CA14 cai: 18 vivos em vez de 8); uma alocação por grupo no `Tick` da `ActiveEffectsSource` (CA15 cai no tick); uma alocação a mais ao nascer o grupo (CA15 cai: 6000 blocos contra 5000 do ASC sozinho).
- Critério de pronto: os três testes verdes, com `ActiveEffectsFilterTests` e as guardas `HelpUrlTests`, `AsmdefRulesTests` e `SourceRulesTests`.
- Tamanho: 317 linhas medidas depois do green (`EffectListBinder.cs` 16, `UIEditors.cs` +4, `EffectListBinderTests.cs` 290, `ActiveEffectsFilterTests.cs` +1 −6) e 4 arquivos. Exceção: nenhuma.
- Commit: `feat(ui): EffectListBinder com limite visível e pool (#270)`

### BF4 — Views, prefabs e tooltip do ícone
- Escopo: `Runtime/UI.UGUI/EffectIconView.cs` (novo), `UGUIEditors.cs`, `Runtime/UI.UGUI/Prefabs/EffectIcon.prefab`, `EffectList.prefab` (fixtures), `Tests/UI.UGUI/EditMode/EffectIconViewTests.cs` (novo), `Tests/UI.UGUI/EditMode/WidgetPrefabsTests.cs` (casos novos), `Tests/GAS.UI/PlayMode/EffectIconTooltipTests.cs` (novo), `lista-de-efeitos.md` (seção "Tooltip em buff"), `CHANGELOG.md`.
- Testes red: `IconView_FrameByRole_RadialSecondsStacks_EndingSoon_Infinite`; `NullIcon_UsesDefaultIcon` (CA17); `WritesSubjectToItemContext`; `EffectIconPrefab_HasTooltipWithEffectSourceFromItem`; `HoverIcon_OpensEffectTooltip_SameStacksAndRemaining_NoBinderStillWorks` (CA10, C10).
- Critério de pronto: CA10 verde; C13 (S) prévia conferida no host.
- Tamanho estimado: ~380 linhas; 9 arquivos (2 fixtures). Depende de: BF3, TT8.

### Detalhe (2026-11-04)
- CA: CA10 (C10) e a parte de exibição do R9 e do R10 (moldura por papel, radial, segundos, pilhas, sinal de fim, ícone padrão do CA17, assunto no `UIItemContext`, tooltip do ícone). Issue #271. O C13 (S) segue manual no host; nota abaixo.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/UI.UGUI/EffectIconView.cs` (novo): `public sealed class EffectIconView : MonoBehaviour, IView<EffectIconModel>`, `[HelpURL(DocUrls.UiEffectList + "#effecticonview")]`, no molde do `SlotView`. Campos `[SerializeField] private`: `frame` (`Image`, recebe a cor do papel por `RoleColors.Get(theme, Role)`), `icon` (`Image`; sprite do modelo ou `defaultIcon`, desligado sem nenhum dos dois), `defaultIcon` (`Sprite`, CA17), `durationFill` (`Image` Filled radial, recebe `Fraction`; 0 no infinito), `secondsText` (`TMP_Text`, `RemainingText` por `SetCharArray`; vazio no infinito), `stacksText` (`TMP_Text`, pilhas com `ShowStacks`, por `TextBuffer`), `theme` (`UITheme`), `itemContext` (`UIItemContext`, recebe `Subject`), `animator` (`Animator`) e `endingSoonParameter` (`"EndingSoon"`, bool ligado com `EndingSoon` e fora do infinito). Sem `Update`, sem corrotina, sem busca de componente e sem API pública além do `Render` (ADR 0039).
  - `Editor/UI.UGUI/UGUIEditors.cs:29` (depois do `ListOverflowViewEditor`): `EffectIconViewEditor : SpellBookEditor`.
  - `Runtime/UI.UGUI/Prefabs/EffectIcon.prefab` (novo, fixture, salvo pela Unity por script descartável em `Assets/__SpellBookTestTemp*`, ignorado pelo git e apagado no fim): raiz 48×48 com `EffectIconView`, `UIItemContext`, `CanvasGroup`, `Animator` (controller abaixo) e `TooltipTrigger` com `EffectTooltipSource` sem efeito (vale o assunto do item, R10), modo `PointerAndFocus`, alvo vazio e âncora do binder vazia (é do projeto, como no `AbilitySlot` do TT8). Filhos `Frame` (`Image` fatiada, **o único `Graphic` com `raycastTarget`**, para o ponteiro abrir o tooltip, como no TT8), `Icon`, `Duration` (radial 360, preto a 60 %), `Seconds` e `Stacks` (TMP sem fonte própria, ADR 0039). `defaultIcon` = o sprite embutido `Knob` (protótipo, R9). Sem `Selectable` nem `IPointerClickHandler`: o ícone só exibe.
  - `Runtime/UI.UGUI/Prefabs/EffectIcon.controller` (novo, fixture): parâmetro bool `EndingSoon`; estados `Idle` (sem clipe, volta o alfa pelos valores padrão) e `Blink` (clipe em laço no `m_Alpha` do `CanvasGroup`), com as transições pelo bool. É o "piscar" do Design 3, sem código por quadro.
  - `Runtime/UI.UGUI/Prefabs/EffectList.prefab` (novo, fixture): raiz com `HorizontalLayoutGroup` e `EffectListBinder` (fonte `ActiveEffectsSource`, `itemPrefab` = `EffectIcon`, dono `SelfOrParent`, spawner vazio, `maxVisible` 8, `overflowView` = o `ListOverflowView`); filhos `Icons` (o `container`, com `HorizontalLayoutGroup`) e `Overflow` (`ListOverflowView` com o alvo `Badge` desligado e o texto "+N"). O "+N" fica fora do container porque o binder põe cada ícone no fim dele (nota do SL8).
  - `Tests/UI.UGUI/EditMode/EffectIconViewTests.cs` (novo): view num Canvas do teste, textos que contam `string` (`WidgetViewsTests.RecordingText`) e `Animator` iniciado em edição, como o `SlotViewTests`.
  - `Tests/UI.UGUI/EditMode/WidgetPrefabsTests.cs:98` (depois do caso do `AbilitySlot`): os dois casos de prefab.
  - `Tests/UI.UGUI/PlayMode/EffectIconTooltipTests.cs` (novo) e `Tests/UI.UGUI/PlayMode/SpellBook.UI.UGUI.Tests.PlayMode.asmdef:5-9` (+`SpellBook.GAS`, `SpellBook.GAS.UI`; `SpellBook.Tags` só se a compilação pedir). **Desvio de caminho da Spec:** a Spec põe o teste em `Tests/GAS.UI/PlayMode`, mas a guarda `AsmdefRulesTests.UiAndGasUi_DoNotReferenceUgui` proíbe o `SpellBook.GAS.UI.Tests.PlayMode` de referenciar a uGUI, e o teste precisa do `TooltipTrigger` e do prefab. O `SpellBook.UI.UGUI.Tests.EditMode` já referencia o GAS e o GAS.UI; o de PlayMode passa a fazer o mesmo. O CA não muda.
  - Fora deste ticket: `lista-de-efeitos.md` (seção "Tooltip em buff") fica para o passe de documentação (emenda de 2026-11-03 da ADR 0007); a guarda `ManualLinksTests` (categoria `Documentacao`) acusa a âncora `#effecticonview` sem página até lá. O `CHANGELOG.md` sai porque nada quebra.
  - Nota do C13: o `UIListBinder` não cria itens fora do Play (SL1b; só o "+N" mostra a prévia), então o `EffectList` em edição não desenha os três ícones da prévia da fonte. A conferência do C13 no host fica como pendência para o Controlador (o critério de pronto do BF5 já leva o CA13 ao host); este ticket não muda o binder genérico.
- Testes red:
  - EditMode, `EffectIconViewTests`: `IconView_FrameByRole_RadialSecondsStacks_EndingSoon_Infinite` (cor da moldura pelo tema em cada papel; radial pela fração; segundos pelo trecho, sem `string`; pilhas só com `ShowStacks`; bool `EndingSoon` ligado e desligado; infinito sem radial, sem segundos e sem piscar); `NullIcon_UsesDefaultIcon` (CA17; sem os dois, o ícone desliga); `WritesSubjectToItemContext`.
  - EditMode, `WidgetPrefabsTests`: `EffectIconPrefab_HasTooltipWithEffectSourceFromItem` (gatilho na raiz ao lado do `UIItemContext`, fonte `EffectTooltipSource` sem efeito, `PointerAndFocus`, alvo e âncora vazios, `CollectMessages` só com o erro em `binder`; só o `Frame` com raycast; sem `Selectable` nem `IPointerClickHandler`; partes da view ligadas, radial 360, `defaultIcon` presente; textos sem fonte; controller com o bool `EndingSoon`); `EffectListPrefab_BinderLayoutOverflow_Wired`.
  - PlayMode, `EffectIconTooltipTests`: `HoverIcon_OpensEffectTooltip_SameStacksAndRemaining_NoBinderStillWorks` (CA10: o `EffectList` cria o `EffectIcon` do pacote; o ponteiro no ícone abre o tooltip do efeito do item no ASC do item, com as mesmas pilhas e o mesmo texto de tempo que o ícone, também depois do tempo andar; com a âncora sem binder, o ponteiro não lança e o ícone segue desenhando).
  - Red contra um esqueleto que compila (a view com os campos e o `Render` vazio, o editor completo, sem os assets).
- Critério de pronto: os 6 testes verdes, com o `total` do XML conferido, junto de `SlotViewTests`, `WidgetViewsTests`, `TooltipValidationTests` e das guardas `AsmdefRulesTests`, `GasUiSourceScanTests`, `HelpUrlTests` e `SpellBookEditorCoverageTests`; mutações da view e do prefab caindo cada uma na asserção que diz provar. A conferência visual no host (piscar, tooltip pelo mouse) é manual.
- Red: contra o esqueleto, 6 de 6 falham pelo motivo esperado (EditMode, `total` = 8 com os 3 casos antigos do `WidgetPrefabsTests`: moldura sem a cor do papel, ícone nulo, assunto nulo e os dois prefabs inexistentes; PlayMode, `total` = 1: o `EffectList` inexistente).
- Green: EditMode 79 de 79 (os 5 do ticket, `SlotViewTests`, `WidgetViewsTests`, `TooltipValidationTests` e as guardas `AsmdefRulesTests`, `GasUiSourceScanTests`, `HelpUrlTests` e `SpellBookEditorCoverageTests`); PlayMode 12 de 12 (`EffectIconTooltipTests` e `TooltipTriggerTests`, pelo asmdef mudado).
- Mutações da view, desfeitas em seguida (arquivo restaurado e conferido), cada uma caindo na asserção que diz provar: moldura sem a cor do papel; sem o ícone padrão (CA17); ícone sempre ligado; radial no infinito; segundos no infinito; segundos como `string`; pilhas sem `ShowStacks`; pilhas como `string`; sem o bool do fim ("perto do fim, pisca"); piscar no infinito; sem assunto no item (`WritesSubjectToItemContext` e, no PlayMode, o título do tooltip do CA10). As asserções de estrutura dos prefabs têm a prova do red (prefab ausente), como no SL8 e no TT8.
- Os prefabs e o controller foram salvos pela Unity (`PrefabUtility.SaveAsPrefabAsset` e `AnimatorController.CreateAnimatorControllerAtPath`, por um script descartável em `Assets/__SpellBookTestTempBF4/`, já apagado). Ao salvar, o TMP gravou a fonte padrão nos 3 textos; essas linhas voltaram ao `{fileID: 0}` (ADR 0039), como no TT8. O `git diff --check` aponta só os espaços finais que a Unity grava nos assets.
- Tamanho medido (`git diff --numstat` e `wc -l` dos novos): 600 linhas em 9 arquivos (`EffectIconView.cs` 90, `UGUIEditors.cs` +4, `EffectIconViewTests.cs` 232, `WidgetPrefabsTests.cs` +83, `EffectIconTooltipTests.cs` 187, asmdef +4; os 3 assets contam só como arquivo). Exceção: 600 passa do teto de 500, dentro de +30% (650). Motivo: os três testes da view dividem um rig de canvas, textos que contam `string`, tema e `Animator` iniciado em edição, e o teste do CA10 precisa de driver, relógio, ASC, a lista do pacote, spawner e binder de tooltip. Dividir separaria a view dos testes dela ou o prefab do teste que prova o tooltip por ele.
- Commit: `feat(ugui): views e prefabs da lista de efeitos (#271)`

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
