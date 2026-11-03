# Spec: UI-3, tooltip e descrição estruturada na tela
PRD: [Documentation~/prd/ui-tooltip-e-descricao.md](../prd/ui-tooltip-e-descricao.md) · Backlog: B-017 · ADRs: usa **0039** e **0038** (novas, Proposta), 0003, 0004, 0007, 0012, 0013, 0014, 0017, 0023, 0026

Status: Proposta para o gate do Lote E · Data: 2026-10-31 · Autor: Arquiteto

## Contexto do código

Não há tooltip no pacote. Este item usa:
- B-008: `DescriptionContext`, `DescriptionLine`, `EffectDescriber`, `DescriptionWriter`, `ModifierTextFormatter`, elementos de custo, recarga e cargas, `GasTextKeys` e as tabelas `GasTexts` (DE7 a DE13); `LocalizedText` nos nomes (DE5).
- B-015: contratos, `UIBinderBase`, `ActorResolver`, `UIUpdateDriver`, `UITheme`, `AnchorRegistrar`, Editors `SpellBook.UI.Editor`, `SpellBook.UI.UGUI.Editor`, `SpellBook.GAS.UI.Editor` (UI1 a UI14).
- B-016: `UIItemContext` (SL1) e o prefab `AbilitySlot` com `SlotView`, que escreve o assunto (SL8).
- GAS: `OnAbilityLevelChanged` (VN4), `OnCooldownChanged` (EA5), `OnChargesChanged` (AV6), `OnEffectApplied/Removed/StacksChanged` e `FindEffects(GameplayEffect, List)` (EA2, EA3), `AttributeState.Modifiers` e `GetAttributeBaseValue` (EA1), `CooldownReductionAttribute` da habilidade.
- `UnityEngine.EventSystems` (`IPointerEnterHandler`, `ISelectHandler`, `ExecuteEvents`) vem do `com.unity.ugui`: só o `SpellBook.UI.UGUI` o usa.

## Respostas às perguntas técnicas do PRD (seção 8)

- **P1. `TooltipModel` e o buffer de linhas.** `struct` com `IReadOnlyList<TooltipLine> Lines`; a lista e o `TextBuffer` dos valores pertencem à ligação e são reaproveitados. A view lê tudo dentro do `Render` e não guarda o modelo (regra escrita no `IView<T>` desde o UI2). Combina com o `IView<T>` por `in`: o struct só carrega referências.
- **P2. Fontes por `[SerializeReference]`.** Campo `UISource<TooltipModel>` (base fechada) no `TooltipTrigger`; fontes concretas não genéricas (ADR 0039, item 2). O trigger guarda só configuração; a ligação fica no `TooltipBinder`, em cache por fonte (criada na primeira abertura dela).
- **P3. Atualização e tick.** A ligação assina os eventos do item no `Attach` (abrir) e desassina no `Detach` (fechar); só pede tick enquanto aberta e com valor no tempo (tempo restante do efeito). O tooltip de habilidade não tem valor no tempo e não pede tick.
- **P4. Posição.** `TooltipPlacement.Compute(Rect alvo, Vector2 tamanho, Rect limites, TooltipSide preferido)` é função pura em `SpellBook.UI`, testada sem Canvas: lado preferido, inverte ao encostar na borda e limita na vertical. A view uGUI só converte os retângulos do Canvas e aplica.
- **P5. Entrada.** `TooltipTrigger` (UI.UGUI) implementa `IPointerEnterHandler`, `IPointerExitHandler`, `ISelectHandler`, `IDeselectHandler` da `EventSystem` da uGUI; nada de Input System nem Input Manager.
- **P6. Prévia no Editor.** A fonte monta a prévia por `CreatePreview(in UIPreviewContext)` com o nível do contexto (ADR 0039, item 7). O `TooltipTriggerEditor` (em `SpellBook.UI.UGUI.Editor`, que referencia o runtime de UI) tem o controle de nível e desenha o `TooltipModel` em texto simples (UI Toolkit, cor do papel pelo `TextRoleTheme`). No asset de habilidade, a prévia é a de descrição do DE13 (as mesmas linhas). Nenhum `.Editor` referencia outro.
- **P7. Ordem.** Modelo e binder (TT1), posição e view (TT2), trigger (TT3), fonte de habilidade (TT4), de efeito e atributo (TT5), Inspector e prévia (TT6), validação (TT7), tooltip no slot (TT8).

## Design

### 1. Modelo (`SpellBook.UI`, `Runtime/UI/TooltipModel.cs`)

- `TooltipLineKind { Description, Modifier, Cost, Cooldown, Duration, Charges, Value, Breakdown }`.
- `TooltipLine` (`readonly struct`): `Label` (`string`, já resolvida pelo provedor), `Value` (`TextSpan`), `Role` (`TextRole`), `Kind`, `Subject` (`ScriptableObject`, o atributo da linha quando há).
- `TooltipModel`: `Title`, `Subtitle`, `Footer` (`string`), `Icon` (`Sprite`), `Lines`, `HasStacks`/`Stacks`, `HasRemaining`/`Remaining` (`TextSpan` mais o número), `IsEmpty`. Sem cor nem rich text.
- `UIBindContext` ganha `Subject` (`UnityEngine.Object`): o assunto do tooltip (habilidade, efeito, atributo) vindo do item.

### 2. Binder, âncora e posição

- `TooltipBinder : MonoBehaviour` (UI): `MonoBehaviour view` (`IView<TooltipModel>` e `ITooltipPlacementTarget` com `Place(RectTransform alvo)`), `UITheme theme`, `UIUpdateDriver driver`. `Show(object requester, UISource<TooltipModel> source, UnityEngine.Object owner, UnityEngine.Object subject, RectTransform target, float delay)`, `Hide(object requester)`, `HideAll()`. Um aberto por vez: abrir fecha o anterior; o mesmo `requester` não duplica. O atraso conta pelo driver. A ligação de cada fonte fica num `Dictionary<UISourceBase, UIBinding>` (uma alocação na primeira abertura de cada fonte, P4 do PRD). Enquanto aberto, reposiciona a cada tick só se o alvo se moveu (compara a posição guardada). Requester destruído: o `TooltipTrigger` avisa no `OnDisable`; o binder também confere `target == null` no tick e fecha sem exceção.
- `TooltipBinderAnchor : RuntimeAnchor<TooltipBinder>` (UI). O binder se publica pelo `AnchorRegistrar` (UI1): um caminho só para cena e para prefab criado em runtime.
- `TooltipPlacement` (P4).

### 3. Gatilho (`SpellBook.UI.UGUI`)

`TooltipTrigger : MonoBehaviour` (`IPointerEnterHandler`, `IPointerExitHandler`, `ISelectHandler`, `IDeselectHandler`):
- `[SerializeReference, TypePicker] UISource<TooltipModel> source`, `ActorResolver owner`, `TooltipOpenMode mode` (`Pointer`, `Focus`, `PointerAndFocus`, `Code`), `float delay = 0.3f`, `TooltipBinderAnchor binder`, `RectTransform target` (vazio: o próprio).
- Dono e assunto: se o objeto tem `UIItemContext` (lido no `Awake`), usa o `Owner` e o `Subject` dele; senão, o `ActorResolver`. Uma regra só: o item, quando existe, manda.
- `Show()` e `Hide()` públicos (C6, chamada por código). `OnDisable`: `Hide`. Âncora vazia ou sem binder: não faz nada (o validador avisa).

### 4. Fontes do GAS (`SpellBook.GAS.UI`)

- `TooltipLineWriter` (interno): converte `DescriptionLine` em `TooltipLine` (rótulo pelo provedor, valor pelo `DescriptionWriter` no `TextBuffer` da ligação, papel e assunto).
- `AbilityTooltipSource : UISource<TooltipModel>`: `GameplayAbility ability` (vazio: o assunto do item), `LocalizedText subtitle`, `int levelWithoutOwner = 1`, `[SerializeReference] ModifierTextFormatter formatter`, `bool showCost = true`, `showCooldown = true`, `showCharges = true`. Monta o `DescriptionContext` (nível do `AbilitySpec` do dono, ou `levelWithoutOwner` sem dono, P2 do PRD), roda `ability.Describe` e os elementos de custo, recarga e cargas do B-008, e converte as linhas. Eventos: `OnAbilityLevelChanged`, `OnCooldownChanged`, `OnChargesChanged`, `OnValueChanged` do `CooldownReductionAttribute` e dos atributos do custo. Cada evento refaz as linhas na mesma lista e buffer. Não altera o ASC. Título: `DisplayName.Resolve(provedor)`.
- `EffectAggregate` (interno, estático): para um ASC e um `GameplayEffect`, soma as pilhas (instância sem `CanStack` conta 1), acha a maior duração restante e a duração aplicada dela, e marca infinito se alguma instância é infinita, por `FindEffects` numa lista reaproveitada. É a mesma regra que o B-018 usa (R7 e R10 dele).
- `EffectTooltipSource : UISource<TooltipModel>`: `GameplayEffect effect` (vazio: o assunto do item). Título e ícone do efeito (`DisplayName`, `Icon`, AR7 e DE5), linhas do `EffectDescriber` com as pilhas somadas, pilhas e tempo restante pelo `EffectAggregate`. Eventos `OnEffectApplied`, `OnEffectRemoved`, `OnEffectStacksChanged` filtrados pelo efeito; tick só enquanto aberto e com duração finita (C4).
- `AttributeTooltipSource : UISource<TooltipModel>`: `AttributeDefinition attribute` (vazio: o assunto), `bool breakdown = false` (P3 do PRD). Linha de valor pelo `AttributeDisplay`; com `breakdown`, uma linha "base" (`GetAttributeBaseValue`) e uma por modificador de `AttributeState.Modifiers`, com o rótulo do efeito de origem quando a fonte é uma `ActiveEffectInstance` e a chave `gas.tooltip.other_source` quando não é. Chaves novas `gas.tooltip.base` e `gas.tooltip.other_source` entram em `GasTextKeys` e nas duas tabelas.

### 5. View (`SpellBook.UI.UGUI`)

`TooltipView`: painel, título, ícone, linhas (rótulo e valor; cor pelo papel, lida do `UITheme`), bloco de pilhas e tempo, rodapé. Monta o texto só no `Render` (abrir ou valor vivo mudou), com `SetCharArray` nos valores; posição pelo `TooltipPlacement`. Prefab `Tooltip` com `TooltipBinder` e `AnchorRegistrar` (âncora a configurar pelo projeto).

### 6. Inspector, prévia e validação (R12 a R14)

- `TooltipTriggerEditor` (UI.UGUI.Editor): fonte, resolvedor, modo e atraso; resumo ("Tooltip · Habilidade Fireball · ponteiro, 0,3 s"); prévia por nível (P6). As duas checagens que dependem da cena (modo ponteiro sem `EventSystem` na cena; mais de um `TooltipBinder` na cena) ficam neste Inspector e no do `TooltipBinder`, só no Editor.
- Validação (`IValidatable`): trigger sem fonte, sem âncora de binder; atraso negativo; modo ponteiro sem `Graphic` com `raycastTarget` no objeto (aviso); fonte sem assunto configurado e sem `UIItemContext` no objeto (erro); `TooltipBinder` sem view ou com view que não implementa a interface.

### 7. O que não muda

- O tooltip não altera o ASC (CA1). O GAS não referencia UI. O Sample não muda (CA18).

## Quebras de compatibilidade

Nenhuma. Adicionado: `TooltipModel`, `TooltipLine`, `TooltipBinder`, `TooltipBinderAnchor`, `TooltipPlacement`, `TooltipTrigger`, `AbilityTooltipSource`, `EffectTooltipSource`, `AttributeTooltipSource`, `TooltipView` e prefab, chaves `gas.tooltip.*`.

## Tickets

Todos usam a Unity e seguem o WIP 1. **Não há par paralelo sem Editor.** Mesmas regras de teste do B-015; cultura injetada; eventos de ponteiro por `ExecuteEvents` nos testes. Teto: ~500 linhas e 10 arquivos.

**Conflitos com a fila:** `UISource.cs`, `UIEditors.cs`, `UGUIEditors.cs`, `DocUrls.cs`, `AsmdefRulesTests.cs`, `validador.md` (B-015, B-016, B-018): em série. TT5 toca `GasTextKeys.cs` e as tabelas `GasTexts` (DE11). TT8 toca o prefab `AbilitySlot` (SL8) e a página do slot.

### TT1 — Modelo, `TooltipBinder` e âncora
- Escopo: `Runtime/UI/TooltipModel.cs`, `TooltipBinder.cs`, `TooltipBinderAnchor.cs` (novos), `Runtime/UI/UISource.cs` (`UIBindContext.Subject`), `UIEditors.cs`, `Tests/UI/PlayMode/TooltipBinderTests.cs` (novo; fonte e view de teste), `DocUrls.cs` (`UiTooltip`), `DocUrlsTests.cs`, `Documentation~/manual/ui/tooltip.md` (nova), `CHANGELOG.md`.
- Testes red: `Show_OneAtATime_SecondClosesFirst` (CA7, parte); `SameRequesterTwice_NoDuplicate`; `Delay_CountedByDriver`; `TargetDestroyed_ClosesNoException`; `Reopen_SameSource_NoAllocation`; `CustomView_ReceivesModel` (CA12); `OpenThenUpdates10000_NoAlloc` (CA13, parte).
- Critério de pronto: CA12 verde; partes do CA7 e do CA13.
- Tamanho estimado: ~480 linhas; 10 arquivos. Depende de: SL8.

### TT2 — `TooltipPlacement`, view e prefab
- Escopo: `Runtime/UI/TooltipPlacement.cs`, `Tests/UI/EditMode/TooltipPlacementTests.cs`, `Runtime/UI.UGUI/TooltipView.cs` (novos), `Runtime/UI.UGUI/Prefabs/Tooltip.prefab` (fixture), `UGUIEditors.cs`, `Tests/UI.UGUI/EditMode/TooltipViewTests.cs` (novo), `tooltip.md`, `CHANGELOG.md`.
- Testes red: `Placement_PreferredSide_FlipsAtEdge_ClampsVertical` (CA8, parte automatizável); `View_TitleIconLinesFooter_ColorByRole_NoMarkupFromModel` (CA9, parte da view); `View_StacksAndRemaining_OnlyWhenPresent`.
- Critério de pronto: testes verdes; CA8 (S) no host.
- Tamanho estimado: ~430 linhas; 8 arquivos (1 fixture). Depende de: TT1.

### TT3 — `TooltipTrigger`: ponteiro, foco e código
- Escopo: `Runtime/UI.UGUI/TooltipTrigger.cs` (novo), `UGUIEditors.cs`, `Tests/UI.UGUI/PlayMode/SpellBook.UI.UGUI.Tests.PlayMode.asmdef` (novo), `Tests/UI.UGUI/PlayMode/TooltipTriggerTests.cs` (novo), `AsmdefRulesTests.cs` (`UI.UGUI` em `PlayModeModules`), `tooltip.md`, `CHANGELOG.md`.
- Testes red: `PointerEnter_AfterDelay_Opens_Exit_Closes`, `Select_Opens_Deselect_Closes`, `Code_ShowHide` (CA6, C6); `ItemDisabled_Closes_NoException` (CA7); `ItemContext_OwnerAndSubjectWin`; `NoBinderInAnchor_DoesNothing`.
- Critério de pronto: CA6 e CA7 verdes.
- Tamanho estimado: ~420 linhas; 7 arquivos. Depende de: TT2.

### TT4 — `AbilityTooltipSource`
- Escopo: `Runtime/GAS.UI/AbilityTooltipSource.cs`, `Runtime/GAS.UI/TooltipLineWriter.cs` (novos), `Tests/GAS.UI/PlayMode/AbilityTooltipSourceTests.cs` (novo), `tooltip.md`, `CHANGELOG.md`.
- Testes red: `Fireball_Level3_TitleIconSubtitle_DescriptionCostCooldownCharges_AscUnchanged` (CA1, C1); `ReductionChangesWhileOpen_CooldownUpdates` (CA2, C2); `LevelChangesWhileOpen_LinesUpdate` (CA3, C3); `EnUs_ProviderKeys_NoMarkup_RoleOnLines` (CA9, C9); `NoOwner_ConfiguredLevel_AssetValues_NoException` (CA10, C10); `Updates10000_NoAlloc_NoTickRequested` (CA13, parte).
- Critério de pronto: CA1 a CA3, CA9 e CA10 verdes.
- Tamanho estimado: ~480 linhas; 5 arquivos. Depende de: TT3, DE13.

### TT5 — `EffectTooltipSource`, `AttributeTooltipSource` e `EffectAggregate`
- Escopo: `Runtime/GAS.UI/EffectTooltipSource.cs`, `AttributeTooltipSource.cs`, `EffectAggregate.cs` (novos), `Runtime/GAS/Text/GasTextKeys.cs`, `GasTexts.pt-BR.asset`, `GasTexts.en.asset` (chaves `gas.tooltip.*`), `Tests/GAS.UI/PlayMode/EffectAndAttributeTooltipTests.cs` (novo), `tooltip.md`, `CHANGELOG.md`.
- Testes red: `Effect_TitleIconModifiers_Stacks_Remaining_TickOnlyWhileOpen` (CA4, C4); `Aggregate_SumStacks_MaxRemaining_InfiniteIfAny`; `Attribute_ValueFormatted_BreakdownOffByDefault_BaseAndSourcesWhenOn` (CA5, C5); `EveryGasTextKey_HasTextInPtBrAndEn` (o teste do DE11 cobre as chaves novas).
- Critério de pronto: CA4, CA5 e CA13 completos.
- Tamanho estimado: ~470 linhas; 9 arquivos (2 fixtures). Depende de: TT4.

### TT6 — Inspector e prévia por nível
- Escopo: `Editor/UI.UGUI/TooltipTriggerEditor.cs`, `Editor/UI/TooltipBinderEditor.cs` (novos), `UGUIEditors.cs`, `UIEditors.cs` (tiram as linhas de uma linha), `Tests/UI.UGUI/EditMode/TooltipTriggerEditorTests.cs` (novo), `tooltip.md` (prévia, fonte e view próprias), `CHANGELOG.md`.
- Testes red: `TriggerEditor_PickerResolverModeDelay_Summary`; `Preview_LevelControl_ChangesLines`; `PointerMode_NoEventSystemInScene_Warns`; `SecondBinderInScene_Warns`.
- Critério de pronto: testes verdes; CA11 e CA14 (S) no host.
- Tamanho estimado: ~380 linhas; 7 arquivos. Depende de: TT5.

### TT7 — Validação do tooltip
- Escopo: `TooltipTrigger.cs`, `TooltipBinder.cs`, `AbilityTooltipSource.cs`, `EffectTooltipSource.cs`, `AttributeTooltipSource.cs` (`Validate`), `Tests/UI.UGUI/EditMode/TooltipValidationTests.cs`, `Tests/GAS.UI/EditMode/TooltipSourceValidationTests.cs` (novos), `manual/ferramentas/validador.md`, `CHANGELOG.md`.
- Testes red: um por regra do Design 6, com o objeto clicável (CA16).
- Critério de pronto: CA16 verde; CA15 pela guarda do UI14.
- Tamanho estimado: ~380 linhas; 9 arquivos. Depende de: TT6.

### TT8 — Tooltip no slot de habilidade
- Escopo: `Runtime/UI.UGUI/Prefabs/AbilitySlot.prefab` (+`TooltipTrigger` com `AbilityTooltipSource` sem habilidade, assunto do item), `Tests/UI.UGUI/EditMode/WidgetPrefabsTests.cs` (caso novo), `slot-de-habilidade.md` (seção "Tooltip em slot"), `tooltip.md`, `CHANGELOG.md` (R15 completo).
- Testes red: `AbilitySlotPrefab_HasTooltipTrigger_WithAbilitySourceFromItem`.
- Critério de pronto: no host, passar o mouse num slot da barra abre o tooltip da habilidade do slot (S); CA17 (E).
- Tamanho estimado: ~40 linhas; 5 arquivos (1 fixture). Depende de: TT7.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| TT1 | sim | SL8 | ~480 / 10 |
| TT2 | sim | TT1 | ~430 / 8 |
| TT3 | sim | TT2 | ~420 / 7 |
| TT4 | sim | TT3, DE13 | ~480 / 5 |
| TT5 | sim | TT4 | ~470 / 9 |
| TT6 | sim | TT5 | ~380 / 7 |
| TT7 | sim | TT6 | ~380 / 9 |
| TT8 | sim | TT7 | ~40 / 5 |

Fila: TT1 → TT8 em série, depois do SL8 (podem intercalar com SL9 a SL13 a critério do Controlador, porque não tocam os mesmos arquivos até o TT8). O B-018 usa o TT5 (`EffectAggregate`, `EffectTooltipSource`) e o TT3 (`TooltipTrigger`). CA15 roda em todos.

## Perguntas ao usuário

Nenhuma decisão nova além das do lote (Q1 a Q6, nas Specs do B-008, B-015, B-016 e B-018). Uma leitura do PRD para o gate conferir: a "categoria" do C1 vira um `subtitle` (`LocalizedText`) da fonte, porque a habilidade não tem campo de categoria.

## Respostas do gate (2026-11-01)

Spec aprovada no gate do Lote E, com as ADRs 0038 e 0039 (Aceitas), incluindo a leitura do C1 (categoria como `subtitle` da fonte).
- Q1 a Q6 aprovadas como propostas (registradas nas Specs do B-008, do B-015, do B-016 e do B-018). Para esta Spec valem: Q4 (`DisplayName` como `LocalizedText`, usado no título do TT4 e do TT5) e Q5 (as chaves `gas.tooltip.*` do TT5 entram nas tabelas `pt-BR` e `en` do pacote).
