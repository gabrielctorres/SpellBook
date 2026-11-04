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

### Detalhe (2026-11-04)
O TT1 passou do teto (~839 linhas medidas) e foi dividido por decisão do usuário: TT1a (#260) e TT1b (#339). O código do TT1b está guardado fora do repositório, em `C:/Users/gabri/orca/workspaces/SpellBook/_guardado-tt1b/` (`NOTA.md`, `tt1b.patch` sobre o TT1a, e o binder e o teste nas formas TT1a e completa).

#### TT1a — Modelo, binder básico e âncora (#260)
- CA: CA12 completo; partes do CA7 (um por vez, sem duplicar) e do CA13 (aberto, 10 000 atualizações sem alocar). Cobre o Design 1 e o Design 2 sem o atraso, o alvo e a posição (TT1b) e sem o `TooltipPlacement` (TT2), e a P1 e a P3.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/UI/TooltipModel.cs` (novo):
    - `enum TooltipLineKind { Description, Modifier, Cost, Cooldown, Duration, Charges, Value, Breakdown }`;
    - `readonly struct TooltipLine`, com `Label` (`string`), `Value` (`TextSpan`), `Role` (`TextRole`), `Kind` e `Subject` (`ScriptableObject`, opcional no construtor);
    - `readonly struct TooltipModel`: construtor `(string title, Sprite icon, IReadOnlyList<TooltipLine> lines, string subtitle = null, string footer = null)`, com `Lines` vazia (não nula) sem lista. As cópias `WithStacks(int)` e `WithRemaining(float seconds, TextSpan text)` ligam `HasStacks`/`Stacks` e `HasRemaining`/`Remaining`/`RemainingText`, como o `WithTrail` do `BarModel`. `IsEmpty` no `default`. Sem cor nem rich text.
  - `Runtime/UI/UISource.cs:28-49`: `UIBindContext` ganha `Subject` (`UnityEngine.Object`) e o parâmetro opcional `subject = null` no fim do construtor. As chamadas existentes (`UIBinder.cs:210` e testes) não mudam.
  - `Runtime/UI/TooltipBinder.cs` (novo): `sealed class TooltipBinder : MonoBehaviour, IValidatable, IUIModelReceiver<TooltipModel>`, `[DisallowMultipleComponent]`, `[HelpURL(DocUrls.UiTooltip + "#tooltipbinder")]`.
    - Campos `[SerializeField]` privados `view` (`MonoBehaviour`), `theme` (`UITheme`) e `driver` (`UIUpdateDriver`; vazio: o do pai, lido no `OnEnable`, como o `UIBinderBase`), com `SetView`, `SetTheme` e `SetDriver`;
    - `Show(object requester, UISource<TooltipModel> source, Object owner, Object subject)` (`TooltipBinder.cs:80`): requester, fonte ou dono nulos não fazem nada; o mesmo requester aberto não refaz nada; outro requester fecha o atual. Abrir pega a ligação da fonte num `Dictionary<UISourceBase, UIBinding<TooltipModel>>` (criada e com o receptor na primeira abertura), faz `Attach(owner, new UIBindContext(theme.Text, null, theme, subject))` e registra a ligação no driver. O TT1b troca esta assinatura pela do Design 2, com alvo e atraso, ainda dentro do lote e sem chamador fora dos testes;
    - `Hide(object requester)` só fecha se o requester é o atual; `HideAll()` fecha sempre. Fechar desliga a ligação, a tira do driver, entrega `default(TooltipModel)` à view e solta o requester;
    - receptor explícito (`void IUIModelReceiver<TooltipModel>.Receive`), como no `UIBinder`, que entrega o modelo à view;
    - `OnDisable`: `HideAll` e sai do driver. `Validate`: sem view, ou view que não implementa `IView<TooltipModel>` (erro), como o `UIBinder`. Sem `Update`.
  - `Runtime/UI/TooltipBinderAnchor.cs` (novo): `sealed class TooltipBinderAnchor : RuntimeAnchor<TooltipBinder>`, `[CreateAssetMenu(menuName = "SpellBook/UI/Tooltip Binder Anchor")]`, `[HelpURL(DocUrls.UiTooltip + "#tooltipbinderanchor")]`. O binder se publica pelo `AnchorRegistrar` existente, sem código novo nele.
  - `Editor/UI/UIEditors.cs:52-58`: `TooltipBinderEditor` e `TooltipBinderAnchorEditor : SpellBookEditor`, para a guarda `SpellBookEditorCoverageTests` (o TT6 troca o do binder pelo `TooltipBinderEditor.cs`).
  - `Runtime/Core/DocUrls.cs:33`: `UiTooltip = Base + "ui/tooltip/"`. `Tests/Core/EditMode/DocUrlsTests.cs:41`: o `TestCase` dela.
  - `Tests/UI/PlayMode/TooltipBinderTests.cs` (novo), com rig próprio: driver com `ManualTimeSource`, dono de teste com evento, fonte e ligação de teste que escrevem um valor num `TextBuffer` da ligação e numa lista de linhas reaproveitada, e uma view de teste que só conhece `SpellBook.UI` e implementa `IView<TooltipModel>`.
  - Os asmdefs não mudam: `SpellBook.UI` já referencia Core e Time; `SpellBook.UI.Editor` já referencia o runtime e as dependências dele (ADR 0012, item 2; ADR 0039, item 9); o assembly de teste PlayMode já referencia Core, Time, UI e Testing. Não há tipo de runtime oferecido por seletor (as fontes chegam no TT4 e no TT5), então a ADR 0013, item 4, não se aplica. Não há guarda de regex nova; a `GasUiSourceScanTests` já varre `Runtime/UI`.
- Decisão, com base no `UIBinding.Attach` (lança com dono nulo, `UISource.cs:158-163`, e o teste `UIContractsTests` cobre): `Show` com dono nulo não abre e não lança. O tooltip sem dono (CA10, P2 do PRD) fica com o TT4, que decide como a fonte de habilidade monta o modelo sem dono. O Examinador julga; o Design 2 não diz o que o binder faz sem dono.
- Fora deste ticket: a página `manual/ui/tooltip.md` fica para o passe de documentação (emenda de 2026-11-03 da ADR 0007); até lá, a guarda `ManualLinksTests` (categoria `Documentacao`) acusa a constante e as âncoras sem página, como no SL2. O `CHANGELOG.md` sai porque não há quebra (item 4 da mesma emenda). Sem arquivo em comum com o SL9 (#257), que toca `GasUIEditors.cs` e arquivos de `GAS.UI`.
- Testes red (PlayMode, `SpellBook.UI.Tests.PlayMode`, classe `TooltipBinderTests`):
  - `Show_OneAtATime_SecondClosesFirst` (CA7, parte): o segundo requester desliga a primeira ligação (dono sem ouvinte) e a view mostra o segundo modelo;
  - `SameRequesterTwice_NoDuplicate`: uma ligação criada, um `Attach` e um desenho;
  - `Reopen_SameSource_NoAllocation`: depois da primeira abertura, 100 `Show`/`Hide` com a mesma fonte não alocam e criam uma ligação só;
  - `CustomView_ReceivesModel` (CA12): a view de teste recebe título, ícone, linhas e o assunto do contexto, sem conhecer o GAS;
  - `OpenThenUpdates10000_NoAlloc` (CA13, parte): aberto e aquecido, 10 000 mudanças do dono com tick do driver não alocam.
- Critério de pronto: os 5 testes verdes, com o `total` do XML igual a 5; `DocUrlsTests`, `HelpUrlTests`, `SpellBookEditorCoverageTests` e `GasUiSourceScanTests` verdes.
- Tamanho: 634 linhas em 9 arquivos, pela ADR 0042, medidas por `git diff --numstat` e `wc -l` dos novos: `TooltipBinderTests.cs` 282, `TooltipBinder.cs` 183, `TooltipModel.cs` 131, `TooltipBinderAnchor.cs` 15, `UISource.cs` +10/-3, `UIEditors.cs` +8, `DocUrls.cs` +1, `DocUrlsTests.cs` +1, e esta Spec. Exceção: 634 passa de 500, abaixo de 650 (+30%). O motivo é que o modelo, o binder e os cinco testes vêm juntos, com o rig próprio de dono, fonte, ligação e view, e o modelo não se testa sem o binder.
- Commit: `feat(ui): TooltipModel, TooltipBinder e âncora (#260)`

#### TT1b — Atraso, alvo e posição no `TooltipBinder` (#339)
- Depende de: TT1a. O TT2 em diante dependem também do TT1b.
- CA: parte do CA7 (alvo destruído fecha sem exceção). Completa o Design 2 menos o `TooltipPlacement` (TT2).
- Escopo (caminhos relativos a `Packages/com.spellbook/`), já escrito e guardado em `_guardado-tt1b/tt1b.patch`:
  - `Runtime/UI/TooltipBinder.cs`:
    - `interface ITooltipPlacementTarget { void Place(RectTransform target); }`, que a view do TT2 implementa;
    - `Show` passa à assinatura do Design 2, `Show(object requester, UISource<TooltipModel> source, Object owner, Object subject, RectTransform target, float delay)`: o mesmo requester aberto ou esperando não refaz nada. Com `delay > 0` e driver, guarda o pedido e abre no tick em que o tempo do driver chega a `início + delay`; sem driver, abre na hora. Ao abrir com alvo, chama `Place(target)` se a view implementa `ITooltipPlacementTarget`;
    - `Hide` antes do tempo cancela o pedido esperando; fechar solta também o dono, o assunto e o alvo;
    - tick próprio por uma ligação interna registrada no driver (como o `BinderTicker` do `UIBinderBase`): abre o pedido vencido; aberto, fecha se o alvo dado foi destruído e chama `Place` só se a posição do alvo mudou. Sem `Update`.
  - `Tests/UI/PlayMode/TooltipBinderTests.cs`: as chamadas passam à assinatura nova; a view de teste implementa `ITooltipPlacementTarget`; `SameRequesterTwice_NoDuplicate` volta a conferir um `Place` no alvo.
- Decisão, com base no Design 2 ("o atraso conta pelo driver"): sem driver, o atraso não tem relógio e o tooltip abre na hora. O validador do TT7 pode avisar. O Examinador julga.
- Decisão: o binder pede tick só com pedido esperando ou aberto com alvo; aberto sem alvo, não há o que conferir no tick. O Examinador julga.
- Testes red (PlayMode, `TooltipBinderTests`):
  - `Delay_CountedByDriver`: com 0,3 s, fechado em 0,2 s do relógio manual e aberto em 0,4 s; `Hide` antes do tempo cancela; fechado, o binder não pede tick;
  - `TargetDestroyed_ClosesNoException` (CA7, parte): alvo destruído, o tick fecha (modelo vazio, ligação desligada) sem exceção; alvo movido chama `Place` de novo, parado não.
  - `TargetDestroyedDuringDelay_ClosesWithoutOpening` (CA7, parte; achado da revisão): alvo destruído durante o atraso, o tick vencido fecha sem abrir e sem exceção, sem ligar a fonte e com `TickingCount` 0. No `Open`, alvo dado e destruído fecha como o dono nulo.
- Critério de pronto: os 8 testes de `TooltipBinderTests` verdes, com o `total` do XML igual a 8. Antes da divisão, o código guardado passou 7 de 7, e 8 mutações derrubaram cada uma o teste esperado; o 8º falha sem a guarda do alvo no `Open` (`MissingReferenceException`).
- Tamanho: 315 linhas em 2 arquivos (mais esta Spec), medido por `git diff --numstat` sobre o TT1a: `TooltipBinder.cs` +167/-34, `TooltipBinderTests.cs` +102/-12. Exceção: nenhuma.
- Commit: `feat(ui): atraso, alvo e posição no TooltipBinder (#339)`

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
