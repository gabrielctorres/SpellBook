# Spec: UI-2, slot, barra de habilidades e barra de cast
PRD: [Documentation~/prd/ui-slot-e-barra-de-habilidades.md](../prd/ui-slot-e-barra-de-habilidades.md) · Backlog: B-016 (e o HUD S4 do B-032) · ADRs: usa **0039** e **0038** (novas, Proposta), 0003, 0004, 0007, 0012, 0013, 0014, 0015, 0017, 0019, 0022, 0023, 0026, 0028, 0032, 0034

Status: Proposta para o gate do Lote E · Data: 2026-10-31 · Autor: Arquiteto

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`.

- `Runtime/GAS/Core/AbilitySystemComponent.cs:35-37`: `OnAbilityActivated(AbilitySpec)`, `OnAbilityEnded(AbilitySpec, bool cancelado)`, `OnAbilityFailed`; `:40` `Abilities`; `:147` `FindSpec`; `:165-180` `GetActivationResult` (pré-requisitos: inválida, não concedida, `AlreadyActive`; depois tags, recarga, custo); `:480-511` `GetCooldownTimeRemaining` e `GetCooldownNormalized`.
- Das Specs aprovadas: `OnAbilityGranted`/`OnAbilityRemoved` e `OnCooldownChanged` (EA5); `CooldownReductionPolicy` (EA6); `OnAbilityLevelChanged` (VN4); `ActivationFailure`, `CheckActivation` único e `GetActivationResult(ability, in AbilityTarget, out ActivationFailure)` (AV4); `OnAbilityFailed(GameplayAbility, ActivationFailure)` (AV5); cargas, `OnChargesChanged`, `GetChargeRechargeNormalized` (AV6, AV7); `AbilityBehavior`, `OnToggleChanged`, `IsToggledOn` (EV12); `IsChanneling`, `ChannelDuration`, `GetChannelElapsed/Remaining/Normalized` (EV13); `SetAutocast`, `OnAutocastChanged`, falha de autocast silenciosa (EV14); a base de UI (B-015, UI1 a UI14); `ActivationFailureDescriber` e as chaves `gas.activation.*` (DE11).
- Sample: `Samples~/Sandbox/` (B-032): cena sem Canvas, `SandboxCaster` com a tecla `1` pelo Input Manager legado, guarda `SampleSources_UseNoInputSystemUiOrEditor` (`Tests/Core/EditMode/Structure/SampleStructureTests.cs`).

## Respostas às perguntas técnicas do PRD (seção 8)

- **P1. Eventos que a fonte do slot escuta.** Nada novo no GAS além da consulta com escopo (abaixo). Lista: `OnTagChanged` (tags); `OnCooldownChanged` (recarga); `OnChargesChanged` (cargas); `OnValueChanged` dos atributos que o custo mexe e dos atributos de que o valor do custo depende (`Attribute`, `Formula.GetSourceDependencies`), coletados uma vez no `Attach`; `OnAbilityGranted`/`OnAbilityRemoved`; `OnAbilityLevelChanged`; `OnAbilityActivated`/`OnAbilityEnded` de qualquer habilidade (o bloqueio por habilidade ativa depende delas); `OnToggleChanged`, `OnAutocastChanged`; `OnAbilityFailed`. Uma consulta por evento, sem alocar (CA22 do B-009).
- **P2. Tempo da recarga.** Lido do ASC a cada tick do driver (`GetCooldownTimeRemaining`, `GetCooldownNormalized`, `GetChargeRechargeNormalized`), só enquanto há recarga correndo. A fonte não tem relógio próprio: o ASC tem a fonte de tempo dele e a redução (`ReduceCooldown`) já dispara `Changed`, então ler é o caminho sem divergência.
- **P3. `NoResource` sem efeito colateral.** Pela consulta do caminho único do B-009 com escopo: `GetActivationResult(ability, in AbilityTarget.None, ActivationCheckScope.WithoutConditions, out ActivationFailure)`, que para antes das regras de ativação (checagens 0 a 3 da ADR 0022). É o mesmo `CheckActivation` com um parâmetro, não um caminho paralelo. Por que sem as condições: ver a Q3.
- **P4. Slot por tag e habilidade repetida.** O ASC tem no máximo um spec por habilidade (`GiveAbility` devolve o existente, R11 do B-003). O slot é a entrada do loadout (tag de slot + habilidade) e acha o spec por `FindSpec`. A mesma habilidade em duas entradas é aviso do validador, e os dois slots mostram o mesmo estado.
- **P5. Rótulo de tecla.** Não é `[SerializeReference]`: o rótulo muda em runtime (troca de tecla) e precisa avisar, e estado não fica em instância serializada (0013, item 4). Vira `KeyLabelProvider` (`ScriptableObject` abstrato em `SpellBook.UI`, reutilizado por vários loadouts, critério do item 5 da 0013), com `bool TryWriteLabel(ScriptableObject slotKey, TextBuffer into)` e `event Action Changed`. A chave é o asset da tag de slot visto como `ScriptableObject` (a UI não referencia Tags). O pacote entrega `FixedKeyLabelProvider` (lista chave → `LocalizedText`), configurado no loadout; nenhuma implementação conhece sistema de entrada. O nome `IKeyLabelSource` do PRD muda por isso.
- **P6. Ordem.** Lista genérica (SL1), modelo e rótulo (SL2), loadout (SL3), slot por recarga, custo e tags (SL4), cargas e falha (SL5), barra (SL6), estado ativo (SL7), views (SL8), Inspector e validação (SL9), barra de cast (SL10, SL11), HUD do Sample (SL12, SL13).
- **P7. Canalização.** Leitura do EV13: `spec.IsChanneling`, `spec.ChannelDuration` (aplicada; 0 = indeterminada), `GetChannelRemaining/Normalized(spec)`. Início: `OnAbilityActivated` com `IsChanneling`; fim: `OnAbilityEnded(spec, cancelado)`. Simultâneas: a fonte mostra a que começou por último; quando ela acaba, procura em `Abilities` outra que ainda canaliza (laço sem alocar). Tick só enquanto a mostrada tem `ChannelDuration > 0`.

## Design

### 1. Lista genérica na UI (ADR 0039, item 6)

- Em `Runtime/UI/UISource.cs`: `UIListSource<TItem> : UISourceBase` (`CreateBinding()`, `CreatePreview(in UIPreviewContext, List<TItem>)`) e `UIListBinding<TItem> : UIBinding` com `AddItem(int id, in TItem)`, `UpdateItem(int id, in TItem)`, `RemoveItem(int id)`, `ClearItems()`.
- `UIListBinder<TItem> : UIBinderBase` (`Runtime/UI/UIListBinder.cs`): `[SerializeReference, TypePicker] UIListSource<TItem> source`, `GameObject itemPrefab`, `SpawnerAsset spawner`, `Transform container`, `int maxVisible` (0 = sem limite), `MonoBehaviour overflowView` (`IView<ListOverflowModel>`, opcional). Itens em ordem de chegada; cada item visível é criado pelo `SpawnerAsset` (`null`: pula e avisa uma vez) e posto no fim do container (`SetSiblingIndex`); remover devolve ao spawner; mudar conteúdo reusa o item. Acima de `maxVisible`, o item fica só no estado (sem objeto); ao liberar lugar, o primeiro oculto ganha objeto. `ListOverflowModel { int Hidden; }`. Tabelas internas pré-alocadas (crescem uma vez).
- `UIItemContext : MonoBehaviour` (`Runtime/UI/UIItemContext.cs`): `Owner` (escrito pelo binder ao ligar e ao criar o item), `Subject` (`ScriptableObject`, escrito pela view no `Render`), `event Action Changed`.

### 2. Modelo e rótulo (`SpellBook.UI`)

- `SlotState { Empty, Ready, OnCooldown, NoCharges, NoResource, Blocked }`.
- `SlotModel`: `State`, `Icon` (`Sprite`), `Subject` (a habilidade como `ScriptableObject`), `CooldownFraction`, `CooldownRemaining`, `HasCharges`, `Charges`, `MaxCharges`, `ChargeFraction`, `KeyLabel` (`TextSpan`), `FailureKey` (`string`), `FailureText` (`TextSpan`), `FailureVisible`, `FailureCount`, `ReadyPulseCount`, `IsActive`, `IsToggledOn`, `IsChanneling`, `AutocastAllowed`, `AutocastEnabled`.
- **`Active` vira marca, não estado** (R3 e R6): o PRD pede que o estado ativo "não substitua o estado de falha"; com `Active` no mesmo enum isso é impossível. `IsActive`/`IsToggledOn`/`IsChanneling` são campos à parte, e o resultado `AlreadyActive` da consulta vira `Ready` com `IsActive` verdadeiro.
- `KeyLabelProvider` e `FixedKeyLabelProvider` (P5).

### 3. Loadout e fontes (`SpellBook.GAS.UI`)

- `AbilityLoadout : ScriptableObject` (`Create > SpellBook > UI > Ability Loadout`): `List<Entry { GameplayTag slot; GameplayAbility ability; }>`, `KeyLabelProvider keyLabels`. Só lista; não concede (R1, P3 do PRD).
- `SlotReader` (interno): para uma entrada e um ASC, monta o `SlotModel`:
  - estado pela consulta com escopo (P3), mapeando `Result`: `Success`/`AlreadyActive` → `Ready`; `NotGranted` → `Empty`; `OnCooldown` → `OnCooldown`; `NoCharges` → `NoCharges`; `CannotAffordCost` → `NoResource`; tags e bloqueio por habilidade ativa → `Blocked`;
  - recarga, cargas e próxima carga pela leitura do ASC (P2); pede tick só com recarga ou carga correndo;
  - rótulo pelo `keyLabels` (assina `Changed`);
  - falha: no `OnAbilityFailed` desta habilidade, `ActivationFailureDescriber` (DE11) dá a chave, o provedor do contexto dá o texto; `FailureVisible` por `failureSeconds` (padrão 1, pelo tick), `FailureCount++`. A falha de autocast não chega aqui (é silenciosa no EV14);
  - pulso: `ReadyPulseCount++` no `OnCooldownChanged(Ended)` e quando `OnChargesChanged` aumenta as cargas; nunca na concessão nem no primeiro `Publish`;
  - ativo e autocast (B-012): `spec.IsActive`, `IsToggledOn`, `IsChanneling`, `AutocastAllowed`, `IsAutocastEnabled`.
- `AbilitySlotSource : UISource<SlotModel>`: `AbilityLoadout loadout`, `GameplayTag slot`, `float failureSeconds = 1`. Prévia: ícone da habilidade, 50 % de recarga, 2 cargas, rótulo do provedor ou "Q".
- `AbilityLoadoutSource : UIListSource<SlotModel>`: `AbilityLoadout loadout`; um `SlotReader` por entrada (id = índice); itens na ordem da lista; `Empty` para habilidade não concedida (a view decide se esconde, P2 do PRD). Trocar o loadout por `SetLoadout` refaz a lista (R2).
- Binders (`SpellBook.UI`): `AbilitySlotBinder : UIBinder<SlotModel>`, `AbilityBarBinder : UIListBinder<SlotModel>`.

### 4. GAS: consulta com escopo

- `Runtime/GAS/Abilities/ActivationCheckScope.cs`: `enum ActivationCheckScope { All, WithoutConditions }`.
- ASC: `AbilityActivationResult GetActivationResult(GameplayAbility ability, in AbilityTarget target, ActivationCheckScope scope, out ActivationFailure failure)`. O `CheckActivation` do AV4 ganha o parâmetro e para antes das regras com `WithoutConditions`. As sobrecargas existentes usam `All`; nada muda para elas.

### 5. Barra de cast (R17 a R21)

- `CastBarModel` (UI): `State` (`Idle`, `Channeling`), `Icon`, `Name` (`string` do provedor), `Fraction`, `Remaining`, `Indeterminate`, `LastResult` (`None`, `Completed`, `Cancelled`), `ResultCount`.
- `ChannelSource : UISource<CastBarModel>` (GAS.UI): regras da P7; nome pelo `DisplayName` da habilidade (`LocalizedText`, DE5). Não ativa nem cancela nada. Prévia: 60 %, nome da habilidade de prévia ou "Fireball", 1,2 s.
- `CastBarBinder : UIBinder<CastBarModel>` (UI); `CastBarView` e prefab (UI.UGUI).

### 6. Views e prefabs (`SpellBook.UI.UGUI`)

- `SlotView`: ícone, máscara radial (`Image.fillAmount`), segundos, cargas e radial da próxima carga, rótulo, escurecer em `NoResource`, `NoCharges` e `Blocked`, realce em `IsActive`, pulso e tremor quando os contadores mudam, moldura vazia em `Empty` (opção `hideWhenEmpty`). Escreve `Subject` no `UIItemContext`. Só exibição: não ativa (R11).
- `ListOverflowView` ("+N"). Prefabs `AbilitySlot` (com `UIItemContext`) e `AbilityBar` (`HorizontalLayoutGroup`, `AbilityBarBinder`).

### 7. Inspector e validação (R13, R14, R21)

- `Editor/GAS.UI/AbilityLoadoutEditor.cs`: lista com uma linha por entrada ("Slot.Q · Fireball · recarga 8 s", recarga pela `Duration` no nível 1).
- Validação: loadout vazio (aviso); entrada sem tag ou sem habilidade (erro); tag repetida (erro); habilidade repetida (aviso); habilidade sem ícone (aviso); cargas ligadas com `ChargesConfig` inválido (aviso, repete o do AV8); `AbilitySlotSource` sem loadout ou com tag fora dele (erro); barra sem prefab de slot ou prefab sem `IView<SlotModel>` (erro); `CastBarBinder` sem fonte, view ou prefab (erro).

### 8. O que não muda

- O slot não ativa a habilidade (R11); o loadout não concede (R1). O GAS não referencia UI. O Sample só muda nos SL12 e SL13.

## Quebras de compatibilidade

Nenhuma. Adicionado: lista genérica (`UIListSource`, `UIListBinder`, `UIItemContext`), `SlotModel`, `KeyLabelProvider`, `FixedKeyLabelProvider`, `AbilityLoadout`, `AbilitySlotSource`, `AbilityLoadoutSource`, binders, `CastBarModel`, `ChannelSource`, views e prefabs, `ActivationCheckScope` e a sobrecarga da consulta no ASC.

## Tickets

Todos usam a Unity e seguem o WIP 1. **Não há par paralelo sem Editor.** Mesmas regras de teste do B-015. Teto: ~500 linhas e 10 arquivos.

**Conflitos com a fila:** `UIModels.cs`, `UIEditors.cs`, `UGUIEditors.cs`, `GasUIEditors.cs`, `DocUrls.cs` e `validador.md` (B-015, B-017, B-018): em série. SL4 toca `AbilitySystemComponent.cs` (Lotes B a D, DE*). SL12 e SL13 tocam assets e a cena do Sample (VN3, EV21, DE6, SB-T8) e a página do Sample.

### SL1 — Lista genérica, `UIItemContext` e "+N"
- Escopo: `Runtime/UI/UISource.cs` (tipos de lista), `Runtime/UI/UIListBinder.cs`, `Runtime/UI/UIItemContext.cs` (novos), `UIModels.cs` (`ListOverflowModel`), `UIEditors.cs`, `Tests/UI/PlayMode/UIListBinderTests.cs` (novo; fonte de lista e spawner de teste), `visao-geral.md`, `CHANGELOG.md`.
- Testes red: `Items_FromSpawner_ArrivalOrder`; `Remove_Releases_LiveCountStable`; `ContentChange_ReusesItem`; `MaxVisible_HiddenCount_PromotesOldestHidden`; `ItemContext_OwnerFromBinder`; `NullSpawn_SkipsWithWarning`; `AddRemove1000_DoNotAllocate`.
- Critério de pronto: testes verdes.
- Tamanho estimado: ~480 linhas; 8 arquivos. Depende de: UI14.

### Detalhe (2026-11-04)
Divisão do SL1 pela ADR 0042, decidida pelo usuário em 2026-11-04. O SL1 implementado inteiro mediu 870 linhas em `.cs`, acima de +30% (650): `UISource.cs` +90, `UIModels.cs` +16, `UIEditors.cs` +4, `UIListBinder.cs` 380, `UIItemContext.cs` 50 e `UIListBinderTests.cs` 330. Ele virou o SL1a (#249) e o SL1b (#335); o SL1b se dividiu de novo em SL1b e SL1c (#336). Em comum aos dois: o manual (`visao-geral.md`) fica para o passe de documentação (emenda de 2026-11-03 da ADR 0007), e o `CHANGELOG.md` sai porque não há quebra (item 4 da mesma emenda).

#### SL1a — contratos de lista, `UIItemContext` e "+N" (#249)
- CA: nenhum CA do PRD fecha aqui. É a base do Design 1 e do item 6 da ADR 0039.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/UI/UISource.cs:87` (depois de `UISource<TModel>`) e `:200` (depois de `UIBinding<TModel>`):
    - `IUIListReceiver<TItem>`, com `AddItem`, `UpdateItem`, `RemoveItem` e `ClearItems`. É o par de lista do `IUIModelReceiver<TModel>` e entra como API nova, sem quebra.
    - `UIListSource<TItem> : UISourceBase`, com `CreateBinding()` e `CreatePreview(in UIPreviewContext, List<TItem>)`.
    - `UIListBinding<TItem> : UIBinding`, com `SetReceiver` e os quatro métodos protegidos. Eles só repassam ao receptor enquanto a ligação está ligada, como o `Publish`.
  - `Runtime/UI/UIModels.cs:302` (depois de `TagIndicatorModel`): `readonly struct ListOverflowModel`, com `Hidden`. Um valor negativo vale 0.
  - `Runtime/UI/UIItemContext.cs` (novo):
    - membros: `Owner` (`Object`, com `internal SetOwner`), `Subject` (`ScriptableObject`, com `public SetSubject`) e `event Action Changed`, que dispara só quando um valor muda;
    - atributo `[HelpURL(DocUrls.Ui + "#uiitemcontext")]`, como o do `UIUpdateDriver`. A âncora nasce no passe de documentação.
  - `Editor/UI/UIEditors.cs:34`: `UIItemContextEditor : SpellBookEditor`, para a guarda `SpellBookEditorCoverageTests`.
  - `Tests/UI/EditMode/UIListContractsTests.cs` (novo).
- Testes red (EditMode, `SpellBook.UI.Tests.EditMode`, classe `UIListContractsTests`):
  - `ListBinding_ForwardsOnlyWhileAttached`;
  - `ItemContext_Changed_OnlyWhenValueChanges`;
  - `ListOverflowModel_NegativeIsZero`.

  Nenhum precisa do binder. O `ItemContext_OwnerFromBinder` precisa e foi para o SL1b.
- Critério de pronto: os 3 testes verdes, com `total` = 3 no XML; `UIContractsTests`, `HelpUrlTests` e `SpellBookEditorCoverageTests` verdes.
- Tamanho medido: 276 linhas em 6 arquivos. Em `.cs`: `UISource.cs` +85, `UIModels.cs` +16, `UIEditors.cs` +4, `UIItemContext.cs` 50 e `UIListContractsTests.cs` 121; a Spec conta só como arquivo. Exceção: nenhuma.
- Commit: `feat(ui): contratos de lista, UIItemContext e "+N" (#249)`

#### SL1b — `UIListBinder<TItem>`: ordem, remoção e troca de conteúdo (#335)
- Depende do SL1a. O SL2 e os tickets seguintes passam a depender também do SL1b e do SL1c (a linha "Depende de" da parte fixa não muda; vale esta nota).
- Divisão de novo pela ADR 0042, decidida pelo usuário em 2026-11-04: o binder guardado mediu ~700 linhas. O SL1b é o núcleo, sem limite de visíveis (todos os itens ganham objeto). O SL1c (#336) põe o `maxVisible`, o "+N" e a promoção do oculto. O código do SL1c está guardado em `C:/Users/gabri/orca/workspaces/SpellBook/_guardado-sl1c/`, e o `NOTA.md` da pasta diz como reaplicar.
- CA: nenhum CA do PRD fecha aqui. Prepara o CA1 (ordem), o CA13 (spawner e vivos) e o CA14 (sem alocar), que fecham na barra (SL6).
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/UI/UIListBinder.cs` (novo, 328 linhas):
    - campos `source` (`[SerializeReference, TypePicker]`), `itemPrefab`, `spawner` e `container`, e `SetSource`. Os campos `maxVisible` e `overflowView` do Design 1 entram no SL1c; o binder do SL1b não os conhece;
    - o binder recebe a lista só pela ligação: `IUIListReceiver<TItem>` é implementado de forma explícita, como o `IUIModelReceiver<TModel>` no `UIBinder<TModel>`, e `AddItem`, `UpdateItem`, `RemoveItem` e `ClearItems` não são públicos. Uma chamada direta fora do Play criaria itens salvos na cena;
    - entradas em ordem de chegada, numa lista pré-alocada;
    - cada entrada ganha um objeto do `SpawnerAsset`, posto sob o `container` (vazio: o próprio binder) com `SetAsLastSibling`;
    - remover devolve o objeto ao spawner; `UpdateItem` reaproveita o objeto;
    - `AddItem` com um id que já existe vale como `UpdateItem`; `UpdateItem` e `RemoveItem` com um id ausente não fazem nada;
    - sem prefab, com prefab sem `IView<TItem>` ou com o spawner sem instância, o item é pulado com um aviso por motivo, só em desenvolvimento. A entrada fica sem objeto, e o próximo `UpdateItem` tenta de novo;
    - views e `UIItemContext` são lidos uma vez por instância e guardados;
    - `DetachBinding` e `ShowEmpty` devolvem todos os itens;
    - `DrawPreview` não faz nada: o binder não cria itens fora do Play, porque um spawn em edição salvaria instâncias na cena (o `FloatingTextBinder` já evita isso). A ordem dos slots em edição (R12 do PRD) é pendência do SL6 e do SL8.

    Para o SL1c encaixar, a entrada já aceita ficar sem objeto (o mesmo caminho do spawn nulo), e `Show` e `Release` trabalham por índice.
  - `Runtime/UI/UISource.cs:150-152`, `:167` e `:182` (`UIBinding`): `internal Object Owner`, escrito no `Attach` antes do `OnAttach` e limpo no `Detach`. O binder o escreve no `UIItemContext`. São as linhas que saíram do SL1a para não ficarem sem uso.
  - `Tests/UI/PlayMode/UIListBinderTests.cs` (novo): fonte, ligação, dono, view e binder de teste aninhados; spawner pelo `RecordingSpawnerAsset`. Os `.meta` dos dois arquivos novos são os que a Unity gerou antes da divisão.
- Testes red (PlayMode, `SpellBook.UI.Tests.PlayMode`, classe `UIListBinderTests`):
  - `Items_FromSpawner_ArrivalOrder`;
  - `Remove_Releases_LiveCountStable`;
  - `ContentChange_ReusesItem`;
  - `ItemContext_OwnerFromBinder`;
  - `NullSpawn_SkipsWithWarning`;
  - `AddRemove1000_DoNotAllocate`, sem limite: de quatro a cinco itens vivos, 4 no fim;
  - `Receiver_NotPublicOnBinder`: por reflexão, o binder não expõe `AddItem`, `UpdateItem`, `RemoveItem` nem `ClearItems` como membros públicos.

  Red contra um esqueleto que liga a fonte e não cria item: 6 de 6 falham (`SpawnCount` 0, índice fora da lista de vivos, aviso esperado que não veio, vivos 0). O `Receiver_NotPublicOnBinder` veio da revisão: red com os quatro métodos públicos (1 de 7 falha, com os quatro nomes na lista), green com a implementação explícita.
- Critério de pronto: os 7 testes verdes, com `total` = 7 no XML; `UIListContractsTests`, `UIContractsTests`, `UIBinderTests`, `HelpUrlTests` e `SpellBookEditorCoverageTests` verdes.
- Tamanho medido: 623 linhas em 4 arquivos. Em `.cs`: `UIListBinder.cs` 328, `UIListBinderTests.cs` 290 e `UISource.cs` +5; a Spec conta só como arquivo. Exceção: 623 passa do teto de 500, dentro de +30% (650). Motivo: o arquivo de teste tem 290 linhas, com a fonte, a ligação, a view e o binder de teste que o PlayMode precisa. Dividir de novo separaria o binder dos testes dele.
- Commit: `feat(ui): UIListBinder com ordem, remoção e troca de conteúdo (#335)`

#### SL1c — `maxVisible` e "+N" no `UIListBinder<TItem>` (#336)
- Depende do SL1b.
- CA: nenhum CA do PRD fecha aqui. Fecha a parte de `maxVisible` e "+N" do Design 1 e prepara o CA14 (sem alocar com ocultos).
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/UI/UIListBinder.cs` (+55 −7):
    - campos `[SerializeField, Min(0)] int maxVisible` (0 = sem limite) e `MonoBehaviour overflowView` (`IView<ListOverflowModel>`, opcional), com as propriedades `MaxVisible` e `OverflowView`. São campos novos: o formato do SL1b continua lido;
    - os visíveis são os `maxVisible` primeiros (`IsVisible`, `HiddenCount`); `AddItem` e `UpdateItem` só criam ou desenham objeto para índice visível;
    - `RemoveItem` promove o primeiro oculto ao último lugar visível, no fim do container;
    - `AddItem`, `RemoveItem` e `ClearItems` entregam o `ListOverflowModel` ao "+N" só quando a contagem de ocultos muda;
    - `DrawPreview` chama `CreatePreview` numa lista pré-alocada e entrega ao "+N" a contagem de ocultos da prévia, sem criar itens.
  - `Tests/UI/PlayMode/UIListBinderTests.cs` (+44 −11): `CreateRig(int maxVisible)` com a view de "+N" de teste; os testes do SL1b passam `maxVisible: 0`.
- Testes red (PlayMode, `SpellBook.UI.Tests.PlayMode`, classe `UIListBinderTests`):
  - `MaxVisible_HiddenCount_PromotesOldestHidden` (novo);
  - `AddRemove1000_DoNotAllocate` muda para `maxVisible: 3`, para cobrir oculto e promoção sem alocar: 3 vivos e `Hidden` = 1 no fim.
- Critério de pronto: os 7 testes da classe verdes, com `total` = 7 no XML; `HelpUrlTests` e `SpellBookEditorCoverageTests` verdes.
- Tamanho medido no código guardado: 117 linhas em 3 arquivos. Em `.cs`: `UIListBinder.cs` +55 −7 e `UIListBinderTests.cs` +44 −11; a Spec conta só como arquivo. Exceção: nenhuma.
- Commit: `feat(ui): maxVisible e "+N" no UIListBinder (#336)`

### SL2 — `SlotModel` e `KeyLabelProvider`
- Escopo: `UIModels.cs` (`SlotModel`, `SlotState`), `Runtime/UI/KeyLabelProvider.cs`, `Runtime/UI/FixedKeyLabelProvider.cs` (novos), `UIEditors.cs`, `Tests/UI/EditMode/KeyLabelProviderTests.cs` (novo), `DocUrls.cs` (`UiAbilitySlot`), `DocUrlsTests.cs`, `Documentation~/manual/ui/slot-de-habilidade.md` (nova), `CHANGELOG.md`.
- Testes red: `Fixed_LabelForKey_MissingKeyFalse`; `Fixed_LocalizedThroughProvider`; `Changed_RaisedOnEdit`; `WriteLabel_DoesNotAllocate`.
- Critério de pronto: testes verdes.
- Tamanho estimado: ~300 linhas; 9 arquivos. Depende de: SL1.

### SL3 — `AbilityLoadout`
- Escopo: `Runtime/GAS.UI/AbilityLoadout.cs` (novo), `Editor/GAS.UI/GasUIEditors.cs`, `Tests/GAS.UI/EditMode/AbilityLoadoutTests.cs` (novo), `DocUrls.cs` (`UiAbilityBar`), `DocUrlsTests.cs`, `Documentation~/manual/ui/barra-de-habilidades.md` (nova), `CHANGELOG.md`.
- Testes red: `Entries_InListOrder`; `FindEntryBySlot_NoAllocation`; `DoesNotGrant`.
- Critério de pronto: testes verdes.
- Tamanho estimado: ~240 linhas; 7 arquivos. Depende de: SL2.

### SL4 — Fonte do slot: tags, recarga, custo, rótulo e pulso; consulta com escopo
- Escopo: `Runtime/GAS/Abilities/ActivationCheckScope.cs` (novo), `AbilitySystemComponent.cs` (sobrecarga), `Tests/GAS/PlayMode/ActivationScopeTests.cs` (novo), `Runtime/GAS.UI/SlotReader.cs`, `Runtime/GAS.UI/AbilitySlotSource.cs`, `Runtime/UI/AbilitySlotBinder.cs` (novos), `UIEditors.cs`, `Tests/GAS.UI/PlayMode/AbilitySlotSourceTests.cs` (novo), `slot-de-habilidade.md`, `CHANGELOG.md`.
- Testes red: `Scope_WithoutConditions_StopsBeforeRules_SameOrder`; `Cooldown8s_ReadyCooldownReady_FractionSeconds_PulseOnce_PauseStops` (CA2, C2); `ReducedCooldown_ShowsRealDuration` (CA3, C3); `LowMana_NoResource_BackWhenManaRises` (CA5, C5); `BlockingTag_Blocked_BackOnRemove` (CA6, C6); `KeyLabel_FromProvider_UpdatesOnChanged` (CA10, C10); `SlotState_MatchesTryActivate_UpToCost` (CA16); `SlotUpdates10000_DoNotAllocate` (CA14, parte).
- Critério de pronto: CA2, CA3, CA5, CA6, CA10 e CA16 verdes.
- Tamanho estimado: ~490 linhas; 10 arquivos. Depende de: SL3, AV4, EA5, EA6.

### SL5 — Cargas, motivo de falha e nível
- Escopo: `SlotReader.cs`, `AbilitySlotSource.cs` (`failureSeconds`), `Tests/GAS.UI/PlayMode/SlotChargesAndFailureTests.cs` (novo), `slot-de-habilidade.md`, `CHANGELOG.md`.
- Testes red: `Charges3_Spend_Shows2_NextRadial_NoCharges` (CA4, C4); `ChargeBack_PulsesOnce`; `LowMana_TryActivate_FailureKeyTagText_OneSecond_CountUp` (CA7, C7, sem autocast); `FailureOrder_FollowsAdr0022`; `LevelChange_UpdatesCooldownAndCost`.
- Critério de pronto: CA4 e CA7 (menos autocast) verdes.
- Tamanho estimado: ~400 linhas; 5 arquivos. Depende de: SL4, AV7, DE11.

### SL6 — `AbilityLoadoutSource` e `AbilityBarBinder`
- Escopo: `Runtime/GAS.UI/AbilityLoadoutSource.cs`, `Runtime/UI/AbilityBarBinder.cs` (novos), `UIEditors.cs`, `Tests/GAS.UI/PlayMode/AbilityBarTests.cs` (novo), `barra-de-habilidades.md`, `CHANGELOG.md`.
- Testes red: `TwoEntries_TwoSlots_ListOrder_ReorderChangesOrder` (CA1, C1); `GrantRemove_EmptyByEvent_NoUpdate` (CA8, C8); `AnchorRespawn_Rebinds` (CA11, C11); `Slots_FromSpawner_Released_LiveStable` (CA13, C13); `SetLoadout_Rebuilds`.
- Critério de pronto: CA1, CA8, CA11 e CA13 verdes.
- Tamanho estimado: ~420 linhas; 6 arquivos. Depende de: SL5.

### SL7 — Estado ativo, toggle e autocast no slot
- Escopo: `SlotReader.cs`, `Tests/GAS.UI/PlayMode/SlotActiveStateTests.cs` (novo), `slot-de-habilidade.md`, `CHANGELOG.md`.
- Testes red: `Channel_Toggle_Autocast_SetActiveFlags_StateUnchanged` (CA9, C9); `AutocastFailure_NotShown` (CA7 completo); `ActiveFlags_ByEvent`.
- Critério de pronto: CA7 e CA9 verdes.
- Tamanho estimado: ~280 linhas; 4 arquivos. Depende de: SL6, EV14.

### SL8 — Views e prefabs do slot e da barra
- Escopo: `Runtime/UI.UGUI/SlotView.cs`, `ListOverflowView.cs` (novos), `UGUIEditors.cs`, `Runtime/UI.UGUI/Prefabs/AbilitySlot.prefab`, `AbilityBar.prefab` (fixtures), `Tests/UI.UGUI/EditMode/SlotViewTests.cs` (novo), `slot-de-habilidade.md`, `CHANGELOG.md`.
- Testes red: `SlotView_RadialSecondsChargesLabel`; `DimOnNoResourceNoChargesBlocked_HighlightOnActive`; `PulseAndShake_OncePerCounterChange`; `Empty_FrameOrHidden`; `WritesSubjectToItemContext`; `Overflow_ShowsPlusN`.
- Critério de pronto: testes verdes; CA12 (S) no host (ícone, 50 % e "2" sem Play).
- Tamanho estimado: ~380 linhas; 8 arquivos (2 fixtures). Depende de: SL7.

### SL9 — Inspector e validação do loadout e do slot
- Escopo: `AbilityLoadout.cs`, `AbilitySlotSource.cs`, `AbilityLoadoutSource.cs` (`Validate`), `Editor/GAS.UI/AbilityLoadoutEditor.cs` (novo), `GasUIEditors.cs`, `Tests/GAS.UI/EditMode/SlotValidationTests.cs`, `Tests/GAS.UI/EditMode/AbilityLoadoutEditorTests.cs` (novos), `manual/ferramentas/validador.md`, `CHANGELOG.md`.
- Testes red: um por regra do Design 7 (menos a barra de cast), com o objeto clicável (CA18); `LoadoutRow_Summary_SlotAbilityCooldown`.
- Critério de pronto: CA18 verde; CA15 (S) no host.
- Tamanho estimado: ~420 linhas; 9 arquivos. Depende de: SL8.

### SL10 — Barra de cast: modelo, binder e `ChannelSource`
- Escopo: `UIModels.cs` (`CastBarModel`), `Runtime/UI/CastBarBinder.cs`, `Runtime/GAS.UI/ChannelSource.cs` (novos), `UIEditors.cs`, `Tests/GAS.UI/PlayMode/ChannelSourceTests.cs` (novo), `DocUrls.cs` (`UiCastBar`), `DocUrlsTests.cs`, `Documentation~/manual/ui/barra-de-cast.md` (nova), `CHANGELOG.md`.
- Testes red: `Channel3s_FractionUp_SecondsDown_CompletedThenIdle_PauseStops` (CA21, C16); `CancelByCodeAndByTag_Cancelled_CountUp` (CA22, C17); `NoDuration_Indeterminate` (CA23, C18); `InstantTogglePassive_NoBar` (CA24, C19); `AnchorRespawn_Rebinds_Ticks10000_NoAlloc_NeverActivatesOrCancels` (CA25, C20); `TwoChannels_MostRecentShown_ThenTheOther` (R19).
- Critério de pronto: CA21 a CA25 verdes.
- Tamanho estimado: ~470 linhas; 9 arquivos. Depende de: SL9, EV13.

### SL11 — View, prefab e validação da barra de cast
- Escopo: `Runtime/UI.UGUI/CastBarView.cs` (novo), `Runtime/UI.UGUI/Prefabs/CastBar.prefab` (fixture), `UGUIEditors.cs`, `CastBarBinder.cs`, `ChannelSource.cs` (`Validate`), `Tests/UI.UGUI/EditMode/CastBarViewTests.cs`, `Tests/GAS.UI/EditMode/CastBarValidationTests.cs` (novos), `barra-de-cast.md`, `CHANGELOG.md` (R15 e R21 completos).
- Testes red: `CastBarView_FillNameIconSeconds_Indeterminate_CompletedVsCancelled`; `CastBar_Validate_NoSourceViewPrefab`.
- Critério de pronto: testes verdes; CA26 (S/E) conferido; CA17, CA19 e CA20 conferidos pelo Examinador.
- Tamanho estimado: ~360 linhas; 9 arquivos (1 fixture). Depende de: SL10.

### SL12 — HUD do Sample (S4): dados e âncoras
Complemento do B-032, planejado aqui a pedido do Controlador. Segue a emenda do PRD do Sample (seção 10 de [prd/sample-sandbox.md](../prd/sample-sandbox.md), aprovada no gate de 2026-11-01: R4, R6, R11, R21 e R23 revistos, R24 a R33, CA22 a CA30) e o D7 da [Spec do Sample](sample-sandbox.md). Sem script novo no Sample: a asmdef e as guardas do SB-T1 não mudam (R25). A detecção do HUD para o Console (R21) é do SB-T7 e do SB-T8. Autoria pela ADR 0028.
- Escopo (fixtures em `Samples~/Sandbox/`): `Data/UI/ANC_Jogador.asset`, `ANC_Boneco.asset` (`AbilitySystemAnchor`), `HUD_Tema.asset` (`UITheme`), `HUD_Textos.asset` (`TableTextProvider` com as tabelas `GasTexts` do pacote, cultura `pt-BR`, R31), `HUD_Teclas.asset` (`FixedKeyLabelProvider`: `Slot.1` → "1", R27), `HUD_Loadout.asset` (`Slot.1` → `GA_BolaDeFogo`), `Data/Tags/TAG_Slot_1.asset`, `Prefabs/Conjurador.prefab` e `BonecoDeTreino.prefab` (+`AnchorRegistrar` em `ANC_Jogador` e `ANC_Boneco`, para o `R` religar o HUD, R30).
- Testes red: nenhum novo; as guardas do SB-T1 (`.meta`, GUID único, sem `UnityEngine.UI`/`TMPro` em `.cs`) seguem verdes (CA29, parte).
- Critério de pronto: com a pasta renomeada, os assets abrem sem "Missing" e a `SpellBook > Validar projeto` não acusa erro neles (CA29, parte). S1 a S3 seguem como antes (os prefabs só ganham o `AnchorRegistrar`).
- Tamanho estimado: 0 linhas de código; 9 arquivos (fixtures). Depende de: SL11, UI14, DE11, DE6, EV21, SB-T8.

### SL13 — HUD do Sample (S4): prefab, cena e documentação
- Escopo: `Samples~/Sandbox/Data/UI/HUD_Regras.asset` (`FloatingTextRules`: Vida negativa → dano, positiva → cura, R28), `Data/Pooling/SP_Numeros.asset` (`PooledSpawnerAsset`), `Prefabs/HUD.prefab` (raiz com `AnchorRegistrar` na `ANC_HUD` do SB-T8, `value` vazio; Canvas em tela sem `GraphicRaycaster`; `UIUpdateDriver` com `TS_Gameplay`; barras de Vida e Mana do jogador por `ANC_Jogador`, barra de Vida do boneco por `ANC_Boneco`; `AbilityBar` com o loadout; `FloatingTextBinder` do boneco com `TS_Gameplay`), `Scenes/Sandbox.unity` (+HUD; sem `EventSystem`, R32), `Documentation~/manual/exemplos/sandbox.md` (seção HUD: passo do TMP Essentials, Q14; o que volta ao Console sem o prefab, R21), `Samples~/Sandbox/README.md`, `CHANGELOG.md`.
- Testes red: nenhum automático (a cena é conferida à mão, como no SB-T5).
- Critério de pronto: no host 6000.3.13f1, num projeto vazio, sem a pasta renomeada (import pela aba Samples), CA22 a CA30 do PRD do Sample conferidos à mão: as barras acompanham custo, dano e queimadura; o slot mostra a tecla, a recarga e o "sem mana"; os números sobem e voltam ao pool; `P` e `T` valem para o HUD; o `R` religa o HUD ao jogador e ao boneco novos; com o HUD, o Console segue o CA30; apagado o prefab da cena, CA6 a CA21 continuam valendo, o tique e a falha voltam ao Console e não há "Missing" (CA28). Entrada só pelo Input Manager legado (o `SandboxCaster` existente). Resultado na seção 9 do PRD do Sample.
- Tamanho estimado: 0 linhas de código; 7 arquivos (4 fixtures, 2 páginas). Depende de: SL12, UI12.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| SL1 | sim | UI14 | ~480 / 8 |
| SL2 | sim | SL1 | ~300 / 9 |
| SL3 | sim | SL2 | ~240 / 7 |
| SL4 | sim | SL3, AV4, EA5, EA6 | ~490 / 10 |
| SL5 | sim | SL4, AV7, DE11 | ~400 / 5 |
| SL6 | sim | SL5 | ~420 / 6 |
| SL7 | sim | SL6, EV14 | ~280 / 4 |
| SL8 | sim | SL7 | ~380 / 8 |
| SL9 | sim | SL8 | ~420 / 9 |
| SL10 | sim | SL9, EV13 | ~470 / 9 |
| SL11 | sim | SL10 | ~360 / 9 |
| SL12 | sim | SL11, UI14, DE11, DE6, EV21, SB-T8 | ~0 / 9 |
| SL13 | sim | SL12, UI12 | ~0 / 7 |

Fila: SL1 → SL13 em série. A barra de cast (SL10, SL11) só depende do EV13 e pode adiantar logo depois do SL9. O B-017 e o B-018 começam depois do SL8 (precisam da lista genérica, do `UIItemContext` e do slot com assunto). CA14 e CA17 rodam em todos.

## Perguntas ao usuário

- **Q3. Estado do slot e as condições de ativação.** Proposta: o estado do slot reflete as checagens até o custo (pré-requisitos, tags, cargas ou recarga, custo), pela consulta com escopo `WithoutConditions`; uma condição de ativação que falha aparece no slot só como motivo, depois da tentativa (o CA16 vale "até o custo"). Motivo: as condições podem depender do alvo (alcance), que o slot não tem. Alternativa: incluir as condições avaliadas sem alvo; habilidades com `CastRangeCondition` ou que exigem alvo aparecem sempre como "bloqueado".

## Respostas do gate (2026-11-01)

Spec aprovada no gate do Lote E, com as ADRs 0038 e 0039 (Aceitas).
- Q3: aprovada como proposta. O estado do slot reflete as checagens até o custo (consulta com escopo `WithoutConditions`, SL4); uma condição de ativação que falha aparece só como motivo, depois da tentativa (SL5). O CA16 vale "até o custo".
- SL12 e SL13 continuam dependendo da emenda do PRD do Sample (B-032), que é do Criativo e passa pelo gate antes de as issues deles serem criadas.
- As respostas Q1, Q2, Q4, Q5 e Q6 do mesmo gate estão nas Specs do B-008, do B-015 e do B-018.

**Emenda de 2026-11-01 (depois do gate):** a emenda do PRD do Sample (seção 10) foi aprovada no gate de 2026-11-01 (Q11 a Q14). O SL12 e o SL13 deixam de esperar por ela e passam a depender do SB-T8 (Console sem HUD, [spec/sample-sandbox.md](sample-sandbox.md), D7), que entra antes deles. Escopo e tamanho dos dois não mudam; o SL13 ganha o `AnchorRegistrar` na raiz do `HUD.prefab` e os critérios CA22 a CA30.
