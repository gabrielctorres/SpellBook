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
- Divisão de 2026-11-04 (ADR 0042): o TT1 passou do teto e virou dois tickets, em série. Os testes red e o escopo acima se repartem entre eles, como diz o Detalhe.
  - TT1a (#260): `TooltipModel`, `TooltipBinder` básico e âncora. Depende de: SL8. Medido: 634 linhas; 9 arquivos (exceção de até +30% registrada no Detalhe).
  - TT1b (#339): atraso, posição e alvo do tooltip. Depende de: TT1a. Medido: 315 linhas; 2 arquivos.
  - Quem dependia do TT1 passa a depender do TT1b.

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
- Tamanho estimado: ~430 linhas; 8 arquivos (1 fixture). Depende de: TT1b (o TT1 se dividiu em TT1a e TT1b em 2026-11-04, ADR 0042).

### Detalhe (2026-11-04)
- CA: parte automatizável do CA8 (não sai da área e inverte na borda) e parte da view do CA9 (cor pelo papel, sem markup do modelo). Cobre a P4 e o Design 5. O CA8 (S) no host fica como pendência manual.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/UI/TooltipPlacement.cs` (novo): `enum TooltipSide { Right, Left, Above, Below }` e `static class TooltipPlacement` com `Rect Compute(Rect target, Vector2 size, Rect bounds, TooltipSide preferred, float gap = 0f)`, pura, com y para cima (espaço local do Canvas). O tooltip fica no lado preferido, a `gap` do alvo. Se não cabe ali e cabe no oposto, inverte; se não cabe em nenhum, fica no lado com mais espaço. À direita e à esquerda, o topo alinha com o topo do alvo; em cima e embaixo, a borda esquerda alinha com a do alvo. Por fim, o retângulo é limitado aos `bounds` nos dois eixos (maior que os `bounds`: encosta no mínimo). O parâmetro `gap` é opcional e não muda a chamada da P4.
  - `Runtime/UI.UGUI/TooltipView.cs` (novo): `sealed class TooltipView : MonoBehaviour, IView<TooltipModel>, ITooltipPlacementTarget`, `[HelpURL(DocUrls.UiTooltip + "#tooltipview")]`. Campos `[SerializeField]` privados: `panel` (`RectTransform`, escondido no modelo vazio e movido pelo `Place`), `title`, `subtitle`, `footer`, `stacks`, `remaining` (`TMP_Text`), `icon` (`Image`), `lineTemplate` (`TooltipLineView`, linha-modelo desligada), `spawner` (`SpawnerAsset`; vazio, o padrão), `theme` (`UITheme`; vazio, o padrão), `area` (`RectTransform`; vazio, o pai do painel), `side` (`TooltipSide.Right`) e `gap` (8).
    - `Render`: vazio esconde o painel. Com valor, escreve título, subtítulo e rodapé (texto nulo ou vazio esconde o objeto), ícone (desligado sem sprite), uma linha por `TooltipLine` e pilhas e tempo só com `HasStacks` e `HasRemaining`. Os valores vão por `SetCharArray` (`RoleColors.Write`); a cor do valor vem do papel pelo tema; o rótulo guarda a cor própria. Todo texto escrito fica com `richText = false`: o markup que viesse do modelo sai literal.
    - Linhas: clones da linha-modelo, criados só quando faltam (alocação na primeira abertura, P4 do PRD) pelo `SpawnerAsset.OrDefault(spawner)` (ADR 0015, como o `UIListBinder`) e reaproveitados. As sobrando são desligadas. Um clone destruído conta como nulo e é recriado; se o spawner não criar a instância, ou ela não tiver `TooltipLineView` (devolvida ao spawner), as linhas param ali. Uma linha-modelo vazia ou destruída não desenha linhas.
    - Correção da revisão (2026-11-04): o `Instantiate` direto quebrava a guarda `GasUiSourceScanTests.UiSources_NoFindInstantiateDestroyUnityTimeOrCoroutine` (CA15, UI14, ADR 0039). Escolhido o spawner, e não linhas autoradas no prefab, porque é a menor mudança: mantém os testes de linhas e o número de linhas aberto, e não toca o prefab nem os asmdefs (o teste usa o `RecordingSpawnerAsset` de `SpellBook.Testing`, já referenciado). O campo `spawner` é novo numa classe nova, sem quebra de formato.
    - `Place(RectTransform target)`: alvo, painel ou área nulos ou destruídos não fazem nada. Converte os cantos do alvo para o espaço da área, refaz o layout do painel para ler o tamanho, chama `TooltipPlacement.Compute` com os limites da área e põe o pivô do painel no retângulo calculado.
  - `Runtime/UI.UGUI/TooltipLineView.cs` (novo): `sealed class TooltipLineView : MonoBehaviour`, `[HelpURL(DocUrls.UiTooltip + "#tooltiplineview")]`, com `label` e `value` (`TMP_Text`) e as propriedades `Label` e `Value`. É a linha-modelo que a view clona.
  - `Editor/UI.UGUI/UGUIEditors.cs:31`: `TooltipViewEditor` e `TooltipLineViewEditor : SpellBookEditor`, para a guarda `SpellBookEditorCoverageTests`.
  - `Runtime/UI.UGUI/Prefabs/Tooltip.prefab` (fixture, autorado pela Unity por script descartável em batchmode, apagado no fim). A raiz se estica no pai e tem `TooltipBinder` (view ligada), `AnchorRegistrar` (âncora vazia, a configurar pelo projeto) e `TooltipView`. O filho `Panel` tem `Image`, `VerticalLayoutGroup` e `ContentSizeFitter`, com título, subtítulo, ícone, `Lines` (com a linha-modelo `Line` desligada, que tem `Label` e `Value`), pilhas, tempo e rodapé. Os textos não têm fonte própria e nenhum gráfico tem `raycastTarget` (ADR 0039).
  - `Tests/UI/EditMode/TooltipPlacementTests.cs` e `Tests/UI.UGUI/EditMode/TooltipViewTests.cs` (novos).
  - Os asmdefs não mudam: o `SpellBook.UI.UGUI` já referencia UI, uGUI e TMP; os assemblies de teste EditMode de UI e de UI.UGUI já referenciam o que os testes usam; o `SpellBook.UI.UGUI.Editor` já referencia o runtime e as dependências dele (ADR 0012, item 2; ADR 0039, item 9). Não há tipo oferecido por seletor (ADR 0013, item 4, não se aplica), e não há guarda de regex nova. `DocUrls.UiTooltip` já existe (TT1a).
- Sem arquivo em comum com o SL10 (#258), que toca `UIEditors.cs`, `DocUrls.cs`, `DocUrlsTests.cs`, `UIModels.cs` e arquivos novos de `GAS.UI` e de `UI`.
- Fora deste ticket: a página `manual/ui/tooltip.md` e as âncoras novas ficam para o passe de documentação (emenda de 2026-11-03 da ADR 0007). Até lá, a `ManualLinksTests` (categoria `Documentacao`) acusa as âncoras sem página. O `CHANGELOG.md` sai porque não há quebra.
- Testes red:
  - EditMode, `SpellBook.UI.Tests.EditMode`, classe `TooltipPlacementTests`: `Placement_PreferredSide_FlipsAtEdge_ClampsVertical` (CA8, parte), com casos para o lado preferido que cabe, a inversão à direita e à esquerda, o limite vertical, nenhum lado que cabe e em cima ou embaixo com limite horizontal.
  - EditMode, `SpellBook.UI.UGUI.Tests.EditMode`, classe `TooltipViewTests`:
    - `View_TitleIconLinesFooter_ColorByRole_NoMarkupFromModel` (CA9, parte da view);
    - `View_StacksAndRemaining_OnlyWhenPresent`;
    - `View_Lines_SpawnedBySpawner_ReuseRows_HideExtra_EmptyHidesPanel`, com o `RecordingSpawnerAsset`: 3 spawns na primeira abertura, nenhum no reuso e 1 só para o clone destruído, que é recriado na mesma posição;
    - `View_Place_PutsPanelByPlacement_DestroyedTargetDoesNothing` (CA8, parte);
    - `TooltipPrefab_BinderViewRegistrar_Wired`.
- Critério de pronto: os 16 casos verdes (11 da posição e 5 da view e do prefab). Com `HelpUrlTests` e `SpellBookEditorCoverageTests` no mesmo filtro, o `total` do XML dá 23. Cada asserção provada por mutação e desfeita em seguida (o reuso, por mutação que sempre cria a linha). A guarda `GasUiSourceScanTests` fica verde sem mudar a guarda. Pendência manual: CA8 (S) no host, com a conferência visual do prefab `Tooltip` (perto do item, sem sair da tela e invertendo na borda).
- Tamanho: 642 linhas em 8 arquivos, contando esta Spec, pela ADR 0042 (606 antes da correção da revisão). Medido por `git diff --numstat` e `wc -l` dos novos: `TooltipViewTests.cs` 314, `TooltipView.cs` 194, `TooltipPlacement.cs` 61, `TooltipPlacementTests.cs` 37, `TooltipLineView.cs` 28 e `UGUIEditors.cs` +8. O prefab e a Spec contam só nos 8 arquivos. Exceção: +28% nas linhas, dentro dos +30%. Motivo: a view tem quatro comportamentos (texto e cor, pilhas e tempo, linhas reaproveitadas, posição), cada um com o próprio teste, e o rig de Canvas com a linha-modelo e o texto que conta strings soma 314 linhas de teste. A correção da revisão (spawner e contagem de spawns no teste) somou 36 linhas. Tirar a posição da view deixaria o CA8 sem a parte da view.
- Commit: `feat(ui): TooltipPlacement, view e prefab do tooltip (#261)`

### TT3 — `TooltipTrigger`: ponteiro, foco e código
- Escopo: `Runtime/UI.UGUI/TooltipTrigger.cs` (novo), `UGUIEditors.cs`, `Tests/UI.UGUI/PlayMode/SpellBook.UI.UGUI.Tests.PlayMode.asmdef` (novo), `Tests/UI.UGUI/PlayMode/TooltipTriggerTests.cs` (novo), `AsmdefRulesTests.cs` (`UI.UGUI` em `PlayModeModules`), `tooltip.md`, `CHANGELOG.md`.
- Testes red: `PointerEnter_AfterDelay_Opens_Exit_Closes`, `Select_Opens_Deselect_Closes`, `Code_ShowHide` (CA6, C6); `ItemDisabled_Closes_NoException` (CA7); `ItemContext_OwnerAndSubjectWin`; `NoBinderInAnchor_DoesNothing`.
- Critério de pronto: CA6 e CA7 verdes.
- Tamanho estimado: ~420 linhas; 7 arquivos. Depende de: TT2.

### Detalhe (2026-11-04)
- CA: CA6 (ponteiro com atraso, foco e código abrem e fecham) e a parte do CA7 que cabe ao gatilho (item desativado ou destruído fecha sem exceção; um por vez e sem duplicar já são do binder, TT1a e TT1b). Cobre o Design 3 e a P5.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/UI.UGUI/TooltipTrigger.cs` (novo):
    - `enum TooltipOpenMode { Pointer, Focus, PointerAndFocus, Code }`;
    - `sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler`, `[DisallowMultipleComponent]`, `[HelpURL(DocUrls.UiTooltip + "#tooltiptrigger")]`. Campos `[SerializeField]` privados: `source` (`[SerializeReference, TypePicker] UISource<TooltipModel>`), `owner` (`ActorResolver`, padrão `ForSelfOrParent`), `mode` (padrão `PointerAndFocus`), `delay` (0,3), `binder` (`TooltipBinderAnchor`) e `target` (`RectTransform`; vazio, o do próprio objeto). Propriedades de leitura e `SetSource`, `SetOwner`, `SetMode`, `SetDelay`, `SetBinder` e `SetTarget`, como no `UIBinder` (o TT6 lê os campos);
    - `Awake`: guarda o `UIItemContext` e o `RectTransform` do objeto (`TryGetComponent`, uma vez);
    - `Show()`: gatilho desativado (`!isActiveAndEnabled`, como no `TooltipBinder.Show`; o `OnDisable` já passou ou o `Awake` não rodou), sem fonte, âncora vazia ou âncora sem binder (destruído conta como nulo), não faz nada. Se o tooltip deste gatilho está num binder guardado diferente do atual, fecha-o nele (`Hide(this)`) antes de abrir no novo, com o `!=` da Unity (destruído conta como nulo). Com `UIItemContext`, dono e assunto vêm dele, mesmo nulos (o item manda); sem ele, o dono vem do `ActorResolver` pelo `OwnerType` da fonte e o assunto é nulo. Chama `TooltipBinder.Show(this, source, dono, assunto, alvo, delay)` e guarda o binder usado;
    - `Hide()`: fecha no binder guardado (`Hide(this)`), se ele não foi destruído, e o solta;
    - entrada: `OnPointerEnter`/`OnPointerExit` abrem e fecham só nos modos `Pointer` e `PointerAndFocus`; `OnSelect`/`OnDeselect` só em `Focus` e `PointerAndFocus`; `Code` ignora os eventos. Receptores explícitos (`void IPointerEnterHandler.OnPointerEnter`…), para não pôr os quatro na API pública;
    - `OnDisable`: `Hide()` (cobre também o destruir). Sem `Update`, sem `Find`, `Instantiate`, `Destroy`, `Time` ou corrotina (guarda CA15).
  - `Editor/UI.UGUI/UGUIEditors.cs:37`: `TooltipTriggerEditor : SpellBookEditor`, para a guarda `SpellBookEditorCoverageTests` (o TT6 o troca pelo `TooltipTriggerEditor.cs`).
  - `Tests/UI.UGUI/PlayMode/SpellBook.UI.UGUI.Tests.PlayMode.asmdef` (novo): Core, Time, UI, UI.UGUI, Testing, `UnityEngine.UI` (a `EventSystems`) e os dois TestRunner, sem `includePlatforms`, como o `SpellBook.UI.Tests.PlayMode`. É PlayMode porque o `Awake` e o `OnDisable` do gatilho só rodam em Play.
  - `Tests/UI.UGUI/PlayMode/TooltipTriggerTests.cs` (novo), com rig próprio: driver com `ManualTimeSource`, `TooltipBinder` com view de teste (`IView<TooltipModel>` e `ITooltipPlacementTarget`), âncora com o binder, fonte e ligação de teste que guardam o dono e o assunto do `Attach`; eventos por `ExecuteEvents`.
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:40`: `UI.UGUI` em `PlayModeModules`.
  - Os asmdefs de runtime e de Editor não mudam: o `SpellBook.UI.UGUI` já referencia UI e uGUI; o `SpellBook.UI.UGUI.Editor` já referencia o runtime e as dependências dele (ADR 0012, item 2; ADR 0039, item 9). Não há tipo concreto novo oferecido por seletor (as fontes chegam no TT4 e no TT5; ADR 0013, item 4, não se aplica). `DocUrls.UiTooltip` já existe; não há guarda de regex nova.
- Sem arquivo em comum com o SL11 (#259), conferido no worktree dele: ele põe o editor no `CastBarViewEditor.cs` próprio e não toca `UGUIEditors.cs`, `DocUrls.cs`, `DocUrlsTests.cs`, `AsmdefRulesTests.cs` nem asmdef.
- Decisões, com base no Design 3 e no C6 do PRD ("a chamada por código funciona igual"); o Examinador julga:
  - `Show()` por código usa o mesmo atraso dos eventos, e `Show()`/`Hide()` funcionam em qualquer modo; o modo só escolhe quais eventos abrem. O padrão `PointerAndFocus` abre pelo mouse e pela navegação sem configurar nada.
  - Sair com o ponteiro e perder o foco fecham só nos modos em que esse caminho abre.
  - Os `Set…` e as propriedades são API nova, aditiva, numa classe nova: sem quebra.
- Fora deste ticket: a página `manual/ui/tooltip.md` (âncora `#tooltiptrigger`) fica para o passe de documentação (emenda de 2026-11-03 da ADR 0007); até lá a `ManualLinksTests` (categoria `Documentacao`) acusa a âncora sem página. O `CHANGELOG.md` sai porque não há quebra. A validação do gatilho é do TT7.
- Testes red (PlayMode, `SpellBook.UI.UGUI.Tests.PlayMode`, classe `TooltipTriggerTests`):
  - `PointerEnter_AfterDelay_Opens_Exit_Closes` (CA6): com 0,3 s, fechado em 0,2 s e aberto em 0,4 s do driver; sair fecha; no modo `Pointer`, selecionar não abre;
  - `Select_Opens_Deselect_Closes` (CA6): no modo `Focus`, selecionar abre e perder o foco fecha; o ponteiro não abre;
  - `Code_ShowHide` (CA6, C6): no modo `Code`, o ponteiro e o foco não abrem; `Show()` abre e `Hide()` fecha;
  - `ItemDisabled_Closes_NoException` e `ItemDestroyed_Closes_NoException` (CA7);
  - `ItemContext_OwnerAndSubjectWin`: com `UIItemContext`, a ligação recebe o dono e o assunto do item, e não o dono do resolvedor; `NoItemContext_UsesResolver`: sem ele, o dono do resolvedor e assunto nulo;
  - `Target_EmptyUsesOwnRect_ElseConfigured`;
  - `NoBinderInAnchor_DoesNothing`: âncora nula, vazia ou com binder destruído, `Show()`, eventos, `Hide()` e desativar não lançam (cada caso listado à parte) e nenhuma ligação é criada;
  - `DisabledTrigger_Show_DoesNotOpen` (CA7, achado 1 da revisão): `Show()` num gatilho que nunca ficou ativo, num desativado depois de ativo e num com o componente desligado não abre nem cria ligação;
  - `Show_OnOtherBinder_ClosesPrevious` (achado 2 da revisão): aberto no binder A, `SetBinder` para a âncora de B e `Show()` abre em B e fecha A; `Hide()` fecha B.
  Red contra um esqueleto que compila (a API com corpos vazios): 9 de 9 rodam e 8 falham por não abrir (as duas de dono e assunto com `NullReferenceException` no `LastBinding`, porque nenhuma ligação foi criada); o `NoBinderInAnchor_DoesNothing` passa no esqueleto, porque testa a ausência, e fica provado por mutação. A guarda `SpellBookEditorCoverageTests` falhou sem o `TooltipTriggerEditor`.
  Correção da revisão (2026-11-04, REPROVADO com 2 achados): os dois testes novos rodaram red contra o código aprovado antes, 11 rodam e 2 falham na asserção esperada ("um gatilho desativado não deveria abrir o tooltip", Expected False, But was True; "abrir em outro binder deveria fechar o anterior", Expected False, But was True), o que vale como mutação: cada um cai sem a própria correção, e um não depende da outra (o primeiro usa um binder só; no segundo o gatilho está ativo). Green: 11 de 11 (`total` = 11); guardas `AsmdefRulesTests`, `HelpUrlTests`, `SpellBookEditorCoverageTests` e `GasUiSourceScanTests` 52 de 52.
  Green da primeira entrega: 9 de 9 (`total` = 9) e as guardas `AsmdefRulesTests`, `HelpUrlTests`, `SpellBookEditorCoverageTests` e `GasUiSourceScanTests` 52 de 52. Mutações em quatro rodadas, desfeitas em seguida, cada uma caindo na asserção que diz provar: atraso ignorado ("0,2 s"); sair e perder o foco sem fechar; selecionar sem abrir; foco abrindo no modo `Pointer`; ponteiro abrindo no modo `Focus`; modo `Code` aceitando eventos; `OnDisable` sem `Hide` (desativado e destruído); item ignorado (dono) e assunto nulo; dono fora do resolvedor; alvo sempre o próprio e alvo sem o próprio; sem a guarda da âncora nula e sem a do binder nulo (os três casos lançam). Um teste de binder destruído com o tooltip aberto saiu: nenhuma mutação o derrubava, porque o `Hide` num binder destruído não lança; o `Hide` confere o binder guardado com o `!=` da Unity.
- Critério de pronto: os 11 testes verdes, com o `total` do XML igual a 11; `AsmdefRulesTests`, `HelpUrlTests`, `SpellBookEditorCoverageTests` e `GasUiSourceScanTests` verdes.
- Tamanho: 640 linhas em 6 arquivos (contando esta Spec), pela ADR 0042, medido por `git diff --numstat` e `wc -l` dos novos: `TooltipTriggerTests.cs` 398, `TooltipTrigger.cs` 209, o asmdef de teste 27, `UGUIEditors.cs` +4 e `AsmdefRulesTests.cs` +1/-1. Exceção: +28% nas linhas, dentro dos +30% (era 588, +17,6%, antes da correção da revisão). Motivo: o asmdef PlayMode novo e o rig próprio (driver, binder, view, âncora, fonte e ligação de teste) vêm com os onze testes, um por caminho de entrada e de dono, mais os dois da revisão; o gatilho não se testa sem eles.
- Commit: `feat(ugui): TooltipTrigger por ponteiro, foco e código (#262)`

### TT4 — `AbilityTooltipSource`
- Escopo: `Runtime/GAS.UI/AbilityTooltipSource.cs`, `Runtime/GAS.UI/TooltipLineWriter.cs` (novos), `Tests/GAS.UI/PlayMode/AbilityTooltipSourceTests.cs` (novo), `tooltip.md`, `CHANGELOG.md`.
- Testes red: `Fireball_Level3_TitleIconSubtitle_DescriptionCostCooldownCharges_AscUnchanged` (CA1, C1); `ReductionChangesWhileOpen_CooldownUpdates` (CA2, C2); `LevelChangesWhileOpen_LinesUpdate` (CA3, C3); `EnUs_ProviderKeys_NoMarkup_RoleOnLines` (CA9, C9); `NoOwner_ConfiguredLevel_AssetValues_NoException` (CA10, C10); `Updates10000_NoAlloc_NoTickRequested` (CA13, parte).
- Critério de pronto: CA1 a CA3, CA9 e CA10 verdes.
- Tamanho estimado: ~480 linhas; 5 arquivos. Depende de: TT3, DE13.
- Observação da revisão do TT1a (Examinador, 2026-11-04) sobre o CA10 (tooltip sem dono): pelo contrato do binder, `Show` com dono nulo não abre. Sem ASC, o dono chega nulo, então o tooltip não abre. O TT4 precisa definir o dono nesse caso (por exemplo, passar o assunto ou o asset como dono) ou mudar o binder. Senão, o CA10 passa só na fonte, e não de ponta a ponta.

### Detalhe (2026-11-04)
O TT4 passou do teto (707 linhas medidas) e foi dividido por decisão do usuário: TT4a (#263) e TT4b (#341), em série. O TT5 em diante dependem também do TT4b. O código do TT4b está guardado fora do repositório, em `C:/Users/gabri/orca/workspaces/SpellBook/_guardado-tt4b/` (`NOTA.md`, `tt4b.patch` sobre o TT4a, e a fonte e o teste nas formas TT4a e completa).

#### TT4a — Fonte de habilidade sem eventos e tooltip sem dono (#263)
- CA: CA1, CA9 (parte do GAS: cultura, provedor, sem marcação, papel nas linhas) e CA10 de ponta a ponta pelo `TooltipBinder`. Cobre o Design 4 (`TooltipLineWriter` e `AbilityTooltipSource`) sem a atualização por evento (TT4b) e a P2 do PRD.
- **Mudança de comportamento aprovado do TT1a**, para o Examinador julgar contra o contrato do `UIBinding.Attach` e as ADRs 0038 e 0039: o Detalhe do TT1a decidiu que o `TooltipBinder.Show` com dono nulo não abre e não lança. O TT4a muda essa decisão: sem dono (nulo ou destruído, também durante o atraso), o binder abre e liga a fonte ao próprio binder. A fonte recebe um dono que não é do `OwnerType` dela e mostra só o que o asset sabe, como o `AbilitySlotSource` já faz com dono que não é ASC. Base: Design 4 ("nível do `AbilitySpec` do dono, ou `levelWithoutOwner` sem dono"), R3 do PRD ("e o ASC dono, opcional") e ADR 0039 (item 1, `Attach(dono, contexto)`; "não criar um segundo mecanismo de dono"). Motivos:
  - o `UIBinding.Attach` continua recusando o nulo (API pública coberta pelo `UIContractsTests`): o contrato do B-015 não muda, sem ADR nova e sem quebra. A ADR 0038 (texto) não é afetada: o provedor e a cultura seguem pelo contexto;
  - o binder é o único ponto por onde passam o gatilho (dono do `UIItemContext` ou do resolvedor, Design 3) e a chamada por código (C6), então uma regra só cobre os dois, sem mexer no `TooltipTrigger`;
  - passar o assunto como dono não cobre a fonte com habilidade configurada e sem item (assunto nulo), e mudar o gatilho deixaria o `Show` do binder por código sem o CA10;
  - o `TooltipBinder` é do lote (TT1a e TT1b, ainda não na `main`): mudar a regra não quebra API publicada. O TT5 segue a mesma regra (dono que não é ASC: só o asset).
  Nenhum teste do TT1a esperava a recusa do dono nulo (nenhum chama `Show` com dono nulo); os 8 seguem com as mesmas asserções. O rig deles acompanha: a ligação de teste guarda o dono do `Attach` e aceita um dono que não é o `Owner` dela (valor 0), em vez de lançar no cast. Um teste novo do binder, `NullOwner_AttachesToBinder_NoException`, fixa a regra nova.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/GAS.UI/TooltipLineWriter.cs` (novo): `internal sealed class TooltipLineWriter`, dono do `DescriptionContext`, da lista de `TooltipLine` e do `TextBuffer` de uma ligação (ou da prévia), reaproveitados. `Begin(ability, level, asc, text, formatter)` limpa e monta o contexto; `Has(kind)` diz se a descrição já tem linha do tipo; `Convert()` converte as linhas. Rótulo pelo provedor (`Label.Resolve`, nulo vira vazio) e valor pelo `DescriptionWriter.WriteValue`; a linha de template e o rótulo com `{0}`/`{1}` (cargas) saem inteiros no valor pelo `WriteLine`, com rótulo vazio. Tipo: `Text` e `Failure` → `Description`, `Attribute` → `Value`, os outros pelo mesmo nome; papel e atributo (assunto) da linha. O TT5 o reaproveita.
  - `Runtime/GAS.UI/AbilityTooltipSource.cs` (novo): `[Serializable, TypeCategory("Tooltip", Summary = …, Line = "{ability}")] sealed class AbilityTooltipSource : UISource<TooltipModel>`, com os campos do Design 4 (`ability`, `subtitle`, `levelWithoutOwner` com `[Min(1)]`, `[SerializeReference, TypePicker] formatter`, `showCost`, `showCooldown`, `showCharges`), propriedades de leitura e escrita e `OwnerType` = `AbilitySystemComponent`.
    - Ligação: no `OnAttach`, habilidade do campo ou, vazio, o `Context.Subject` (destruído conta como nulo; sem habilidade, publica o modelo vazio). Dono ASC: nível do `AbilitySpec` (`FindSpec`; não concedida, `levelWithoutOwner`) e ASC de origem no contexto. Dono que não é ASC: `levelWithoutOwner`, sem ASC. Monta e publica uma vez; não pede tick e não altera o ASC. A atualização por evento é do TT4b.
    - Linhas: `ability.Describe` e, com `showCost`, `showCooldown` e `showCharges`, as do `CostElement`, `CooldownElement` e `ChargesElement` do B-008 (instâncias `static readonly`), só quando a descrição não trouxe linha daquele tipo (decisão: sem linha repetida quando o designer já pôs o elemento na descrição). Título pelo `DisplayName.Resolve(provedor)`, ícone da habilidade e subtítulo pelo provedor (vazio: nulo).
    - `CreatePreview`: o mesmo caminho, sem dono, no nível do contexto, com um escritor `[NonSerialized]` (como o `_preview` do `AbilitySlotSource`).
  - `Runtime/UI/TooltipBinder.cs:16-22,92-103,214-224`: `Show` não recusa mais o dono nulo; `Open` liga com `_owner != null ? _owner : this`, e o alvo destruído no atraso continua fechando sem abrir. Os comentários de documentação dizem a regra nova.
  - `Tests/UI/PlayMode/TooltipBinderTests.cs`: o teste `NullOwner_AttachesToBinder_NoException` e o rig da ligação de teste (dono guardado, dono de outro tipo aceito), como acima.
  - `Tests/GAS.UI/PlayMode/AbilityTooltipSourceTests.cs` (novo), com rig próprio: driver com `ManualTimeSource`, `TooltipBinder` com view de teste (`IView<TooltipModel>`), tema com provedor de teste em `en-US`, e uma Fireball de três níveis (texto, modificador de dano, custo de mana, recarga, cargas e atributo de redução).
  - Os asmdefs não mudam: o `SpellBook.GAS.UI` já referencia Core, Tags, Time, GAS e UI; o assembly de teste PlayMode do GAS.UI já referencia o que o teste usa (o `TooltipBinder` está em `SpellBook.UI`). A fonte é tipo concreto oferecido pelo seletor do `TooltipTrigger`: tem `TypeCategory` com caminho, `Summary` e `Line` (ADR 0013, item 4). Não há Editor novo nem guarda de regex nova; a `GasUiSourceScanTests` já varre `Runtime/GAS.UI` e `Runtime/UI`.
- Sem arquivo em comum com o SL13 (#276), que toca só `Samples~` e a Spec do slot.
- Fora deste ticket: a página `manual/ui/tooltip.md` (fonte de habilidade e a regra do dono ausente) fica para o passe de documentação (emenda de 2026-11-03 da ADR 0007). O `CHANGELOG.md` sai porque não há quebra. A validação da fonte é do TT7 e a prévia no Inspector, do TT6.
- Testes red (PlayMode):
  - `SpellBook.GAS.UI.Tests.PlayMode`, classe `AbilityTooltipSourceTests`:
    - `Fireball_Level3_TitleIconSubtitle_DescriptionCostCooldownCharges_AscUnchanged` (CA1): título, ícone e subtítulo; linhas de texto, modificador, custo, recarga e cargas com os valores do nível 3; mana, recarga, cargas e nível do ASC iguais antes e depois;
    - `EnUs_ProviderKeys_NoMarkup_RoleOnLines` (CA9): decimal com ponto em `en-US`, título, subtítulo e rótulos trocados pelo provedor, nenhum `<` nas saídas e o papel em cada linha;
    - `NoOwner_ConfiguredLevel_AssetValues_NoException` (CA10): `TooltipBinder.Show` com dono nulo abre a fonte com habilidade configurada no `levelWithoutOwner`, com os valores do asset (recarga sem redução), sem exceção; o assunto do item sem habilidade configurada também abre; a prévia no mesmo nível dá as mesmas linhas;
    - `DescriptionWithCostElement_NoDuplicateLine`: com o `CostElement` na descrição, uma linha de custo só.
  - `SpellBook.UI.Tests.PlayMode`, classe `TooltipBinderTests`: `NullOwner_AttachesToBinder_NoException` (CA10): sem dono, abre sem exceção, a ligação recebe o próprio binder como dono, o assunto chega e a fonte mostra só o que sabe.
- Red (esqueleto da fonte que publica o modelo vazio, binder na forma do TT1a e TT1b): 13 rodam, os 5 novos falham (3 em "o tooltip deveria estar aberto", o do CA10 da fonte em "sem dono, o tooltip abre" e o do binder em "sem dono, o tooltip abre (CA10)"), e os 8 do TT1a passam com o rig adaptado.
- Green: 24 de 24 no PlayMode (`total` = 24: 4 de `AbilityTooltipSourceTests`, 9 de `TooltipBinderTests` e 11 de `TooltipTriggerTests`); `TypeCatalogSummaryTests` e `GasUiSourceScanTests` 3 de 3.
- Mutações, desfeitas em seguida (conferidas por `cmp`), cada uma caindo na asserção esperada. Sobre o código do TT4a: binder recusando o dono nulo (os dois testes do CA10); binder ligando ao assunto em vez de a si mesmo ("liga ao próprio binder"); nível sem dono fixo em 1 (CA10); ativar a habilidade no `OnAttach` (CA1, mana 70 em vez de 100); assunto do item ignorado (CA10). Sobre o escritor e a montagem, iguais aos da forma completa, provadas antes da divisão: sem o filtro de linha repetida; título sem o provedor (CA9); linha sem o atributo (CA1); papel `None` (CA9); prévia no nível 1 (CA10); rótulo com `{0}` sem a linha inteira.
- Critério de pronto: os 5 testes novos verdes, com o `total` do XML igual a 24 no filtro `AbilityTooltipSourceTests`, `TooltipBinderTests` e `TooltipTriggerTests`; `TypeCatalogSummaryTests` e `GasUiSourceScanTests` verdes.
- Tamanho medido (`git diff --numstat` e `wc -l` dos novos): 589 linhas em 6 arquivos (contando esta Spec): `AbilityTooltipSourceTests.cs` 320, `AbilityTooltipSource.cs` 127, `TooltipLineWriter.cs` 92, `TooltipBinderTests.cs` +27/−6 e `TooltipBinder.cs` +10/−7. Exceção: 589 passa de 500, abaixo de 650 (+18%). Motivo: a fonte vem com o escritor de linhas que ela usa, com a Fireball de três níveis e o rig do binder nos testes, e o CA10 de ponta a ponta exige a mudança e o teste do binder no mesmo ticket.
- Commit: `feat(gas-ui): AbilityTooltipSource e tooltip sem dono (#263)`

#### TT4b — Atualização por evento da `AbilityTooltipSource` (#341)
- Depende de: TT4a. O TT5 em diante dependem também do TT4b.
- CA: CA2, CA3 e parte do CA13 (fonte de habilidade: 10 000 atualizações sem alocar e sem tick). Completa o Design 4 da fonte de habilidade.
- Escopo (caminhos relativos a `Packages/com.spellbook/`), já escrito e guardado em `_guardado-tt4b/tt4b.patch` (conferido com `patch --dry-run` sobre o TT4a e reproduzindo a forma completa byte a byte):
  - `Runtime/GAS.UI/AbilityTooltipSource.cs`: a ligação guarda o ASC e a habilidade e, com dono ASC, assina `OnAbilityLevelChanged`, `OnCooldownChanged`, `OnChargesChanged` e o `OnValueChanged` do `CooldownReductionAttribute` e dos atributos de que o custo depende (fonte `Attribute` e dependências de `Formula`), com delegates criados uma vez. Cada evento refaz as linhas na mesma lista e buffer e publica. O `OnDetach` solta tudo pelo `ReferenceEquals` (um ASC destruído ainda guarda os delegates); o `Refresh` confere o ASC e a habilidade com o `!=` da Unity. Não pede tick.
  - `Tests/GAS.UI/PlayMode/AbilityTooltipSourceTests.cs`: os seis testes abaixo, o auxiliar `State`, o atributo de teste `Mana máxima` no ASC do rig e o auxiliar `CostFromMaxMana`. Dois entraram na implementação (2026-11-04): o Examinador do TT4a notou que nenhum teste cobria habilidade ou assunto destruídos, e o Controlador pediu a prova de que o `Detach` desassina os eventos (lição do SL4c). O do custo entrou na correção da reprovação do Examinador do TT4b (2026-11-04): a assinatura dos atributos do custo (`ListenCost`, ramos `Attribute` e `Formula`) não tinha teste.
- Testes red (PlayMode, `AbilityTooltipSourceTests`):
  - `ReductionChangesWhileOpen_CooldownUpdates` (CA2): a redução muda com o tooltip aberto e a recarga e as cargas mostradas mudam;
  - `CostAttributeChangesWhileOpen_CostUpdates_OneRenderPerChange` (CA2, custo; dois casos): custo de mana lido da `Mana máxima` do ASC, pela fonte `Attribute` (o atributo inteiro: 200 → 300) e pela `Formula` (`SimpleAttributeFormula` com metade dele: 100 → 150); a `Mana máxima` muda com o tooltip aberto, a linha Cost mostra o valor novo e o tooltip redesenha uma vez;
  - `LevelChangesWhileOpen_LinesUpdate` (CA3): o nível 1 e o 3 dão valores diferentes, e `SetAbilityLevel` com o tooltip aberto atualiza as linhas;
  - `Updates10000_NoAlloc_NoTickRequested` (CA13, parte): aberto e aquecido, 10 000 mudanças da redução não alocam, cada uma redesenha, e nem a ligação nem o binder pedem tick;
  - `ClosedThenReopenedOnOtherAsc_OldAscIgnored_OneRenderPerChange`: a mesma fonte (mesma ligação no cache do binder) abre no ASC A, fecha e reabre no B; redução, nível e ativação (recarga e cargas) no A não redesenham, e uma mudança da redução no B redesenha uma vez só;
  - `DestroyedAbilityOrSubject_CountsAsNull_EmptyModel_NoException`: habilidade configurada destruída (sem assunto) e assunto destruído abrem o modelo vazio, sem exceção; a habilidade destruída com o tooltip aberto também dá o modelo vazio no evento seguinte.
- Red (fonte na forma do TT4a, testes na forma final), 29 rodam e 5 falham pelo evento ausente: CA2 "4 s" em vez de "2 s"; CA3 "-20" em vez de "-40"; CA13 0 de 10 000 redesenhos; reabertura 0 de 1 redesenho; habilidade destruída com o tooltip aberto sem modelo vazio. As partes de destruído na abertura já passavam no TT4a e só fixam o comportamento.
- Red da correção (2026-11-04), por mutação sobre a forma final: sem a chamada `ListenCost(_ability.CostEffect)`, 11 rodam e os 2 casos do custo falham em "o custo vivo, com o tooltip aberto" ("200" em vez de "300"; "100" em vez de "150"), com o valor antes já certo.
- Green: 31 de 31 no PlayMode (`total` = 31: 11 de `AbilityTooltipSourceTests`, 9 de `TooltipBinderTests` e 11 de `TooltipTriggerTests`); `TypeCatalogSummaryTests` e `GasUiSourceScanTests` 3 de 3, sobre o código final.
- Mutações sobre a forma final, desfeitas em seguida (conferidas por `cmp`), cada uma rodada sozinha no filtro `AbilityTooltipSourceTests` (9): sem assinar a redução derruba CA2 ("a recarga real, após a redução"), CA13 ("cada mudança da redução refaz o tooltip"), a reabertura e o destruído aberto; sem assinar o nível derruba só o CA3 ("o nível 3 chega pelo evento"); sem soltar, no `OnDetach`, o `OnValueChanged`, o `OnAbilityLevelChanged`, o `OnCooldownChanged` ou o `OnChargesChanged` (quatro rodadas) derruba a reabertura ("o Detach desassina o ASC anterior"); `Build` com `ReferenceEquals` no lugar do `==` da Unity derruba o destruído ("assunto destruído: modelo vazio"). Antes da divisão, o pedido de tick e a alocação no `Refresh` derrubaram o CA13. Na correção, rodadas no mesmo filtro (11): sem a chamada `ListenCost` derruba os dois casos do custo; sem o `Listen` do ramo `Attribute`, só o caso `Attribute` ("300" esperado, "200"); sem o `Listen` das dependências do ramo `Formula`, só o caso `Formula` ("150" esperado, "100"). O código foi restaurado (conferido por `cmp`). Sem teste, lacuna aceita pelo Examinador: o `OnCooldownChanged` e o `OnChargesChanged` com o tooltip aberto, porque o texto mostra durações, não o tempo restante (a soltura deles no `Detach` está coberta).
- Critério de pronto: os 11 testes de `AbilityTooltipSourceTests` verdes, com o `total` do XML igual a 31 no mesmo filtro do TT4a; guardas verdes.
- Tamanho medido (`git diff --numstat`): 270 linhas em 2 arquivos (mais esta Spec): `AbilityTooltipSource.cs` +87/−9 e `AbilityTooltipSourceTests.cs` +171/−3. Dos 97 a mais que os 173 do patch guardado, 48 são os dois testes da implementação e 49 o teste do custo da correção. Exceção: nenhuma.
- Commit: `feat(gas-ui): atualização por evento da AbilityTooltipSource (#341)`

### TT5 — `EffectTooltipSource`, `AttributeTooltipSource` e `EffectAggregate`
- Escopo: `Runtime/GAS.UI/EffectTooltipSource.cs`, `AttributeTooltipSource.cs`, `EffectAggregate.cs` (novos), `Runtime/GAS/Text/GasTextKeys.cs`, `GasTexts.pt-BR.asset`, `GasTexts.en.asset` (chaves `gas.tooltip.*`), `Tests/GAS.UI/PlayMode/EffectAndAttributeTooltipTests.cs` (novo), `tooltip.md`, `CHANGELOG.md`.
- Testes red: `Effect_TitleIconModifiers_Stacks_Remaining_TickOnlyWhileOpen` (CA4, C4); `Aggregate_SumStacks_MaxRemaining_InfiniteIfAny`; `Attribute_ValueFormatted_BreakdownOffByDefault_BaseAndSourcesWhenOn` (CA5, C5); `EveryGasTextKey_HasTextInPtBrAndEn` (o teste do DE11 cobre as chaves novas).
- Critério de pronto: CA4, CA5 e CA13 completos.
- Tamanho estimado: ~470 linhas; 9 arquivos (2 fixtures). Depende de: TT4.

### Detalhe (2026-11-04)
**Proposta de divisão, à espera do Controlador (ADR 0042).** Lido o código, o TT5 passa dos +30%. A estimativa vem da medida do TT4, que tem uma fonte só: `AbilityTooltipSource.cs` com 214 linhas e os testes com 491 (TT4a mais TT4b). O TT5 tem duas fontes do mesmo porte, com eventos (uma delas também com tick), mais o `EffectAggregate`. Dá ~1 000 linhas; mesmo com uma conta otimista, passa de 680. Nada foi implementado e a trava não foi criada.

Fatos do código que entram no Detalhe de cada parte:
- O ASC guarda no máximo uma instância por asset (`AbilitySystemComponent.cs:1645-1650`, `FindActiveInstance`). Pela API pública do ASC, a soma de várias instâncias do `EffectAggregate` (Design 4, e o B-018 depende dela) fica sem teste. Para cobrir a soma, o agregado ganha uma entrada por lista de instâncias. O teste monta as instâncias pelo construtor público de `ActiveEffectInstance` e chega ao tipo interno por um `Runtime/GAS.UI/AssemblyInfo.cs` novo (`InternalsVisibleTo("SpellBook.GAS.UI.Tests.PlayMode")`), como já fazem Time e Events.
- A `GasTextTablesTests.GasTextKeys_FollowConvention` (`Tests/GAS/EditMode/GasTextTablesTests.cs:55-66`) espera 19 chaves com os prefixos `gas.activation.` e `gas.description.`. As chaves `gas.tooltip.*` levam a contagem a 21 e acrescentam o prefixo, como consequência direta do ticket (item 6 da ADR 0042). As outras guardas do DE11 continuam valendo para as chaves novas nas duas tabelas.
- O `TooltipLineWriter` (TT4a) converte tudo de uma vez. A decomposição do atributo pede linhas `Breakdown`, então ele ganha um `Convert(TooltipLineKind)` que converte só as linhas ainda não convertidas, com o tipo dado.

Divisão proposta, em série ou em worktrees separados (não há arquivo em comum, e as duas dependem só do TT4b):
- **TT5a: `EffectAggregate` e `EffectTooltipSource`.** CA4 e a parte do efeito no CA13. Escopo: `Runtime/GAS.UI/EffectAggregate.cs` (~65; `internal readonly struct` com `Count`, `Stacks`, `Remaining`, `Duration`, `IsInfinite` e `First`, e as entradas `Of(asc, efeito, lista reaproveitada)` e `Of(lista)`, para o BF1 usar no grupo e no tick), `EffectTooltipSource.cs` (~190), `AssemblyInfo.cs` (~4), `Tests/GAS.UI/PlayMode/EffectTooltipSourceTests.cs` (~330). Total: ~590 linhas, 4 arquivos mais esta Spec, com exceção de até +30% se a medida passar de 500.
  - Detalhe da implementação (2026-11-04, #264). CA: CA4 e a parte do efeito no CA13. Caminhos relativos a `Packages/com.spellbook/`:
    - `Runtime/GAS.UI/EffectAggregate.cs` (novo): `internal readonly struct` com `Count`, `Stacks` (soma; instância sem `CanStack` conta 1), `Remaining` e `Duration` (a maior restante entre as instâncias com duração e a duração aplicada dela), `IsInfinite` (alguma instância de efeito `Infinite`), `First` e `HasDuration`. `Of(asc, efeito, lista)` usa o `FindEffects` na lista reaproveitada; `Of(lista)` agrega uma lista dada. Não aloca.
    - `Runtime/GAS.UI/EffectTooltipSource.cs` (novo): `[Serializable, TypeCategory("Tooltip", Summary = …, Line = "{effect}")] sealed class EffectTooltipSource : UISource<TooltipModel>`, campo `effect` com propriedade, `OwnerType` = `AbilitySystemComponent`. Título pelo `DisplayName.Resolve(provedor)`, ícone do efeito, linhas do `EffectDescriber` pelo `TooltipLineWriter` (sem formatador próprio), no nível, instigador e habilidade do `EffectContext` da primeira instância e com as pilhas somadas. Pilhas no modelo só quando a soma passa de 1 (decisão: 1 pilha não diz nada, como o `minStacksToShow` do B-018); tempo restante só com duração finita, em segundos inteiros arredondados para cima (como o `SlotView`), num `TextBuffer` da ligação. Sem instância ativa, dono que não é ASC ou na prévia: só o asset (nível padrão, 1 pilha, sem tempo). Efeito nulo ou destruído: modelo vazio.
    - Ligação: efeito do campo ou, vazio, o `Context.Subject`. Com dono ASC, assina `OnEffectApplied`, `OnEffectRemoved` e `OnEffectStacksChanged` (delegates criados uma vez) e só refaz quando a instância é do efeito. Pede tick só com instância de duração finita; o tick relê o agregado (renovação e `Extend` não têm evento) e publica só quando o segundo mostrado muda. `OnDetach` solta pelo `ReferenceEquals`.
    - `Runtime/GAS.UI/AssemblyInfo.cs` (novo): `InternalsVisibleTo("SpellBook.GAS.UI.Tests.PlayMode")`.
    - `Tests/GAS.UI/PlayMode/EffectTooltipSourceTests.cs` (novo), rig como o do `AbilityTooltipSourceTests` (driver com `ManualTimeSource`, binder com view de teste, provedor em `en-US`).
    - Os asmdefs não mudam. A fonte é tipo do seletor do `TooltipTrigger`, com `TypeCategory` (ADR 0013, item 4); a `TypeCatalogSummaryTests` e a `GasUiSourceScanTests` a cobrem. O manual fica para o passe de documentação; não há quebra nem `CHANGELOG.md`.
  - Testes red (PlayMode, `SpellBook.GAS.UI.Tests.PlayMode`, classe `EffectTooltipSourceTests`):
    - `Effect_TitleIconModifiers_Stacks_Remaining_TickOnlyWhileOpen` (CA4): título, ícone, linha do modificador com 3 pilhas, pilhas 3 e "10 s"; o tempo corre pelo tick com o tooltip aberto; fechado, o driver não tem tick e nada redesenha;
    - `EffectEvents_FilteredByEffect_AppliedStacksRemoved`: aberto sem instância mostra o asset sem pilhas nem tempo e sem tick; outro efeito não redesenha; aplicar, empilhar e remover o efeito atualizam o modelo;
    - `ClosedThenReopenedOnOtherAsc_OldAscIgnored_OneRenderPerChange`: aberto e fechado num ASC e reaberto em outro, o ASC anterior não redesenha (aplicar, pilhas e remover), e cada evento do novo dá um só redesenho;
    - `InfiniteEffect_NoRemaining_NoTick` e `NoOwnerOrPreview_AssetOnly_DestroyedEffect_Empty`;
    - `Updates10000_NoAlloc` (CA13, parte do efeito): aberto e aquecido, 10 000 mudanças de pilha com tick não alocam;
    - `Aggregate_SumStacks_MaxRemaining_InfiniteIfAny`: duas instâncias do mesmo efeito em dois ASCs (uma com `CanStack`, uma sem, com pilhas forçadas), soma, maior restante e duração dela, `First`; efeito infinito marca `IsInfinite`; ASC sem o efeito dá `Count` 0.
  - Critério de pronto: os 7 testes de `EffectTooltipSourceTests` verdes, com o `total` do XML igual a 7; `TypeCatalogSummaryTests` e `GasUiSourceScanTests` verdes.
  - Red (esqueletos: agregado `default`, fonte que publica o modelo vazio), com os 6 testes iniciais: 6 rodam e os 6 falham (4 em "o tooltip deveria estar aberto", o sem dono em "Burn" esperado e nulo, o agregado em "uma instância de 3 pilhas"). O 7º, `ClosedThenReopenedOnOtherAsc_OldAscIgnored_OneRenderPerChange`, entrou depois. Green: 7 de 7 (`total` = 7); `TypeCatalogSummaryTests` e `GasUiSourceScanTests` 3 de 3.
  - Tamanho medido (`wc -l` dos novos): 616 linhas em 4 arquivos (mais esta Spec): `EffectTooltipSourceTests.cs` 346, `EffectTooltipSource.cs` 176, `EffectAggregate.cs` 91 e `AssemblyInfo.cs` 3. Exceção: 616 passa de 500, abaixo de 650 (+23%). Motivo: o agregado vem com a fonte que o usa, e o teste cobre os dois, com o rig do binder.
  - Commit: `feat(gas-ui): EffectAggregate e EffectTooltipSource (#264)`
- **TT5b: `AttributeTooltipSource` e chaves `gas.tooltip.*`.** CA5, a parte do atributo no CA13 e a guarda do DE11. Escopo: `AttributeTooltipSource.cs` (~150), `TooltipLineWriter.cs` (+15), `GasTextKeys.cs` (+6), `GasTexts.pt-BR.asset` e `GasTexts.en.asset` (só contam como arquivo), `GasTextTablesTests.cs` (+2/−2), `Tests/GAS.UI/PlayMode/AttributeTooltipSourceTests.cs` (~260). Total: ~435 linhas, 7 arquivos mais esta Spec.
- O TT6 passa a depender do TT5a e do TT5b. O `EffectAndAttributeTooltipTests.cs` da parte fixa vira os dois arquivos acima, um por ticket. O manual e o `CHANGELOG.md` ficam para o passe de documentação (emenda de 2026-11-03 da ADR 0007); não há quebra.

#### TT5b — `AttributeTooltipSource` e chaves `gas.tooltip.*` (#342)
Detalhe (2026-11-04), da Fábrica, sobre a divisão acima.
- CA: CA5 completo; a parte do atributo no CA13 (10 000 atualizações abertas sem alocar e sem tick); a guarda do DE11 para as chaves novas (Q5). Cobre a `AttributeTooltipSource` do Design 4.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/GAS.UI/AttributeTooltipSource.cs` (novo): `[Serializable, TypeCategory("Tooltip", Summary = …, Line = "{attribute}")] sealed class AttributeTooltipSource : UISource<TooltipModel>`, com `AttributeDefinition attribute` (vazio: o `Context.Subject`; destruído conta como nulo) e `bool breakdown = false`, propriedades de leitura e escrita e `OwnerType` = `AbilitySystemComponent`. Título pelo `DisplayName.Resolve(provedor)` e ícone do atributo.
    - Linhas, montadas no `TooltipLineWriter` da ligação: uma linha `Value` com o valor atual no formato do atributo (`AttributeDisplay`, pelo formatador padrão), rótulo vazio (o nome já é o título), papel do `Display.Role` e o atributo como assunto. Com `breakdown`, linhas `Breakdown`: "base" (`GetAttributeBaseValue`, rótulo `gas.tooltip.base`) e uma por modificador de `AttributeState.Modifiers`, na ordem da lista, com o valor e a operação no formato do modificador (`+5`, `+50%`), papel pela variação (`GetDeltaRole`; `Override` → `Value`, como o `EffectDescriber`) e rótulo pelo `DisplayName` do efeito quando a fonte é uma `ActiveEffectInstance` com nome; sem nome ou com outra fonte, `gas.tooltip.other_source`.
    - Sem atributo: modelo vazio. Dono que não é ASC (binder sem dono) ou ASC sem o atributo: só o asset (título e ícone, sem linhas), sem assinar nada (regra do TT4a). A prévia (`CreatePreview`) segue o caminho sem dono.
    - Eventos, com dono ASC: o `OnValueChanged` do atributo; com `breakdown`, também `OnEffectApplied`, `OnEffectRemoved` e `OnEffectStacksChanged` do ASC, porque um modificador que não muda o valor (atributo no limite) não dispara o `OnValueChanged` e a decomposição ficaria velha. Delegates criados uma vez; cada evento refaz as linhas na mesma lista e buffer e publica; não pede tick. O `OnDetach` solta tudo pelo `ReferenceEquals` (como o TT4b) e o `Refresh` confere ASC e atributo com o `!=` da Unity. Não altera o ASC.
  - `Runtime/GAS.UI/TooltipLineWriter.cs:50-57,60-76`: `Convert()` passa a converter só as linhas ainda não convertidas (a partir de `Lines.Count`; logo depois do `Begin`, todas, como hoje), e entra `Convert(TooltipLineKind kind)`, que converte as restantes com o tipo dado (para as linhas `Breakdown`).
  - `Runtime/GAS/Text/GasTextKeys.cs:63`: `TooltipBase = "gas.tooltip.base"` e `TooltipOtherSource = "gas.tooltip.other_source"`.
  - `Runtime/GAS/Text/GasTexts.pt-BR.asset` e `GasTexts.en.asset`: as duas entradas ("Base" e "Outra fonte"; "Base" e "Other source"), no fim da lista.
  - `Tests/GAS/EditMode/GasTextTablesTests.cs:60,65`: 21 chaves e o prefixo `gas.tooltip.` (consequência direta, item 6 da ADR 0042).
  - `Tests/GAS.UI/PlayMode/AttributeTooltipSourceTests.cs` (novo), rig próprio no molde do `AbilityTooltipSourceTests`: driver com `ManualTimeSource`, `TooltipBinder` com view de teste, tema com provedor de teste em `en-US` (troca o nome do atributo e as duas chaves novas), atributo "Força" com uma casa e efeito "Bênção" infinito de +5.
  - Os asmdefs não mudam. A fonte é tipo concreto do seletor do `TooltipTrigger`, com `TypeCategory`, `Summary` e `Line` (ADR 0013, item 4); a `TypeCatalogSummaryTests` e a `GasUiSourceScanTests` a cobrem. Não há Editor novo; a validação é do TT7 e a prévia no Inspector, do TT6.
- Sem arquivo em comum com o TT5a (#264), salvo esta Spec, cada um na própria seção.
- Testes red:
  - PlayMode, `SpellBook.GAS.UI.Tests.PlayMode`, classe `AttributeTooltipSourceTests`:
    - `Attribute_ValueFormatted_BreakdownOffByDefault_BaseAndSourcesWhenOn` (CA5): título e ícone; sem decomposição, só a linha `Value` com o valor formatado, papel e assunto; com ela, a base, a linha do efeito pelo nome e a de outra fonte pela chave, com papel; o ASC não muda;
    - `ValueAndModifiersChangeWhileOpen_LinesUpdate`: a base muda e um efeito entra e sai com o tooltip aberto, e as linhas acompanham;
    - `ModifierAtLimit_BreakdownUpdatesByEffectEvent`: com o atributo no máximo, um efeito novo não muda o valor e aparece na decomposição;
    - `ModifierAtLimit_EffectRemoved_BreakdownUpdates`: com o atributo no máximo, um efeito que sai não muda o valor e some da decomposição;
    - `ModifierAtLimit_StacksChanged_BreakdownUpdates`: com o atributo no máximo, `SetEffectStacks` não muda o valor e o modificador segue as pilhas;
    - `Updates10000_NoAlloc_NoTickRequested` (CA13, parte): aberto com decomposição e aquecido, 10 000 mudanças da base não alocam, cada uma redesenha e ninguém pede tick;
    - `ClosedThenReopenedOnOtherAsc_OldAscIgnored_OneRenderPerChange`: o `Detach` solta o atributo e os três eventos de efeito do ASC anterior (aplicar, mudar pilhas e remover);
    - `NoOwnerOrNoAttribute_AssetOnly_NoException`: sem dono, com o atributo do item, título sem linhas; ASC sem o atributo, idem; atributo destruído e sem assunto, modelo vazio; a prévia igual ao caminho sem dono.
  - EditMode, `SpellBook.GAS.Tests.EditMode`, classe `GasTextTablesTests` (DE11): `GasTextKeys_FollowConvention` com 21 chaves e o prefixo novo; `EveryGasTextKey_HasTextInPtBrAndEn` e `Tables_OnlyGasTextKeys_NoValidationIssues` cobrem as chaves novas nas duas tabelas.
- Red: esqueleto da fonte que publica o modelo vazio e as duas constantes em `GasTextKeys`, sem as entradas nas tabelas. EditMode `GasTextTablesTests`: 8 rodam e 4 falham (`EveryGasTextKey_HasTextInPtBrAndEn` nas duas tabelas, em "gas.tooltip.base em GasTexts.…asset", e `Tables_OnlyGasTextKeys_NoValidationIssues` nas duas, na lista de chaves). PlayMode `AttributeTooltipSourceTests`: 6 rodam e os 6 falham (5 em "o tooltip deveria estar aberto" e o sem dono em "sem dono: o atributo do item").
- Green: EditMode 11 de 11 (`total` = 11: 8 de `GasTextTablesTests`, mais `TypeCatalogSummaryTests` e `GasUiSourceScanTests`); PlayMode 17 de 17 (`total` = 17: 6 de `AttributeTooltipSourceTests` e 11 de `AbilityTooltipSourceTests`, que cobrem a mudança do `Convert()`).
- Mutação, desfeita em seguida (conferida por `cmp`): sem assinar o `OnEffectApplied`, só o `ModifierAtLimit_BreakdownUpdatesByEffectEvent` cai, em "o modificador novo aparece pelo evento do efeito".
- Correção da revisão (2026-11-04): o `OnEffectRemoved` e o `OnEffectStacksChanged` ganharam teste. Mutações, desfeitas em seguida: sem assinar os dois, PlayMode 8 rodam e só `ModifierAtLimit_EffectRemoved_BreakdownUpdates` (linha "Fúria" que sobra) e `ModifierAtLimit_StacksChanged_BreakdownUpdates` ("+5.0" em vez de "+10.0") caem; sem soltá-los no `Detach`, só `ClosedThenReopenedOnOtherAsc_OldAscIgnored_OneRenderPerChange` cai (5 desenhos em vez de 3). Green: `AttributeTooltipSourceTests` 8 de 8.
- Critério de pronto: os 8 testes de `AttributeTooltipSourceTests` e os de `GasTextTablesTests` verdes, com o `total` do XML conferido; `TypeCatalogSummaryTests` e `GasUiSourceScanTests` verdes.
- Tamanho medido (`git diff --numstat` e `wc -l` dos novos): 574 linhas em 8 arquivos (contando esta Spec): `AttributeTooltipSourceTests.cs` 368, `AttributeTooltipSource.cs` 172, `TooltipLineWriter.cs` +18/−5, `GasTextKeys.cs` +6, `GasTextTablesTests.cs` +3/−2; as duas tabelas contam só como arquivo. Exceção: 574 passa de 500, abaixo de 650 (+15%). Motivo: a assinatura dos eventos de efeito, que a decomposição pede para não ficar velha com o atributo no limite, trouxe o código e os testes de cada evento.
- Commit: `feat(gas-ui): AttributeTooltipSource e chaves gas.tooltip.* (#342)`

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
| TT1a | sim | SL8 | 634 / 9 |
| TT1b | sim | TT1a | 315 / 2 |
| TT2 | sim | TT1b | ~430 / 8 |
| TT3 | sim | TT2 | ~420 / 7 |
| TT4 | sim | TT3, DE13 | ~480 / 5 |
| TT5 | sim | TT4 | ~470 / 9 |
| TT6 | sim | TT5 | ~380 / 7 |
| TT7 | sim | TT6 | ~380 / 9 |
| TT8 | sim | TT7 | ~40 / 5 |

Fila: TT1a → TT1b → TT2 → … → TT8 em série, depois do SL8 (o TT1 se dividiu em 2026-11-04, ADR 0042; podem intercalar com SL9 a SL13 a critério do Controlador, porque não tocam os mesmos arquivos até o TT8). O B-018 usa o TT5 (`EffectAggregate`, `EffectTooltipSource`) e o TT3 (`TooltipTrigger`). CA15 roda em todos.

## Perguntas ao usuário

Nenhuma decisão nova além das do lote (Q1 a Q6, nas Specs do B-008, B-015, B-016 e B-018). Uma leitura do PRD para o gate conferir: a "categoria" do C1 vira um `subtitle` (`LocalizedText`) da fonte, porque a habilidade não tem campo de categoria.

## Respostas do gate (2026-11-01)

Spec aprovada no gate do Lote E, com as ADRs 0038 e 0039 (Aceitas), incluindo a leitura do C1 (categoria como `subtitle` da fonte).
- Q1 a Q6 aprovadas como propostas (registradas nas Specs do B-008, do B-015, do B-016 e do B-018). Para esta Spec valem: Q4 (`DisplayName` como `LocalizedText`, usado no título do TT4 e do TT5) e Q5 (as chaves `gas.tooltip.*` do TT5 entram nas tabelas `pt-BR` e `en` do pacote).
