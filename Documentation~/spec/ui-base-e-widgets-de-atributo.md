# Spec: UI-1, base de UI e widgets de atributo, tag e números flutuantes
PRD: [Documentation~/prd/ui-base-e-widgets-de-atributo.md](../prd/ui-base-e-widgets-de-atributo.md) · Backlog: B-015 · ADRs: **0039 (nova, Proposta)**, **0038 (nova, Proposta)**; usa 0003, 0004, 0007, 0012, 0013, 0014, 0015, 0016, 0017, 0023, 0026, 0037

Status: Proposta para o gate do Lote E · Data: 2026-10-31 · Autor: Arquiteto

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`. Não há código de UI no pacote.

- `Runtime/Core/Spawning/SpawnerAsset.cs:37-39`: `Spawn(prefab, posição, rotação, pai)` (pode devolver `null`, RP-07) e `Release(instância)`.
- `Runtime/Time/TimeSourceAsset.cs:10-68`: fonte de tempo em asset, `Scaled` como padrão, reset de sessão.
- `Runtime/GAS/Core/AbilitySystemComponent.cs:34`: `OnTagChanged(GameplayTag, bool)`; `:39` `AttributeSet`; `:40` `Abilities`. `AttributeState.OnValueChanged(antigo, novo)` (`AttributeState.cs:17`).
- Das Specs aprovadas: `RuntimeAnchor<T>` (CU1, `Runtime/Core/Anchors/RuntimeAnchor.cs`); `TextRole`/`TextRoleTheme` (AR4); limites efetivos `MinValue`/`MaxValue` e fonte de limite por atributo (AR1); `AttributeDisplay` (AR5); `TryGetState`, `States`, `TryGetAttributeValue` (EA1); `HasMatching(GameplayTagQuery)` (AV2); `OnEffectBlocked` e `OnBlocked` (RL1); `OnAttributeChangeApplied` com `AttributeChangeReport` (DM1); `GameplayEffect.DamageTags` (DM2); texto do B-008 (`TextBuffer`, `ITextProvider`, `AttributeDisplay.Format` em `TextBuffer`: DE1, DE7).
- Testes de estrutura que mudam: `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:18-25` (listas de módulos), `:341-376`; `Tests/Core/EditMode/Inspector/SpellBookEditorCoverageTests.cs:18-21` (assemblies cobertos). `HelpUrlTests` acha os assemblies de runtime sozinho.
- O projeto host não tem `com.unity.ugui` (`Packages/manifest.json`). A versão embutida na 6000.3.13f1 é a 2.0.0, com os asmdefs `UnityEngine.UI` e `Unity.TextMeshPro`.

## Respostas às perguntas técnicas do PRD (seção 8)

- **P1. Contrato da ligação.** ADR 0039, item 1: a fonte cria uma vez um `UIBinding<TModel>` por binder; o binder chama `Attach`/`Detach` a cada troca de dono, sem alocar. A ligação pede tick com `RequestTick(bool)`; o driver guarda as ligações que pediram numa lista e chama `Tick(tempo)` por laço de índice, sem alocar. Não é `IDisposable`.
- **P2. Genéricos e texto.** Campo do tipo base fechado (`UISource<BarModel>`), fontes concretas não genéricas, binder concreto por modelo (ADR 0039, item 2). O texto do modelo é um `TextSpan` num `TextBuffer` da ligação (ADR 0038 e 0039, item 4): a view entrega os caracteres ao TextMeshPro (`SetCharArray`, API conferida no ticket da view) sem criar `string`.
- **P3. Âncora.** O `RuntimeAnchor<T>` do CU1 (`Value`, `HasValue`, `OnChanged`, `Provide`, `Unset`) ganha a base não genérica `RuntimeAnchor` (`UntypedValue`, `ValueType`, `event Action<RuntimeAnchor> Changed`, `ProvideUntyped`, `UnsetUntyped`), sem mudar a API dele. O `AnchorRegistrar` (Core) publica um componente numa âncora no `OnEnable` e sai no `OnDisable` (`Unset` só limpa se for o mesmo, então o respawn não apaga o novo). O binder assina `Changed` só enquanto está ligado e desassina no `Detach`: sem ouvinte vazado.
- **P4. uGUI.** `"com.unity.ugui": "2.0.0"` no `package.json` (ADR 0039, item 8; Q1 do gate). Versão conferida no pacote embutido da 6000.3.13f1; o UI8 confere a 6000.0 pelo CI ou registra como não verificada.
- **P5. Eventos do GAS.** Nada falta. A barra e o texto usam `AttributeState.OnValueChanged` do atributo e do estado-fonte do máximo (o atributo explícito da fonte ou, sem ele, a fonte de limite do AR1; constante não precisa de evento). O indicador usa `OnTagChanged` e reavalia `HasMatching`. Os números usam `OnAttributeChangeApplied` (DM1; tem o efeito e as tags de dano), `OnEffectBlocked` e `OnBlocked` (RL1). Nenhuma fonte usa o relay nem o Bridges.
- **P6. Ordem.** Âncora (UI1), contratos e binder (UI2 a UI4), modelos (UI5), fontes do GAS (UI6, UI7), views e prefabs (UI8, UI9), números flutuantes (UI10 a UI12), Inspector e validação (UI13, UI14).

## Design

### 1. Âncoras (Core e GAS)

- `RuntimeAnchor` (`abstract`, `ScriptableObject`) no mesmo arquivo do CU1; `RuntimeAnchor<T> : RuntimeAnchor`. Disparo de `OnChanged` e `Changed` no mesmo ponto.
- `Runtime/Core/Anchors/AnchorRegistrar.cs` (`[HelpURL(DocUrls.Concepts + "#ancoras")]`): `RuntimeAnchor anchor`, `Component value` (vazio: o componente do tipo `anchor.ValueType` no mesmo objeto, lido uma vez no `Awake`). `IValidatable`: sem âncora, erro; `value` de tipo errado, erro.
- `Runtime/GAS/Core/AbilitySystemAnchor.cs`: `AbilitySystemAnchor : RuntimeAnchor<AbilitySystemComponent>` (`Create > SpellBook > GAS > Ability System Anchor`).

### 2. Contratos (`SpellBook.UI`, `Runtime/UI/`; refs Core e Time)

```csharp
public interface IView<TModel> where TModel : struct { void Render(in TModel model); }

public readonly struct UIBindContext { public ITextProvider Text { get; } public IFormatProvider Culture { get; } public UITheme Theme { get; } }
public readonly struct UIPreviewContext { public int Level { get; } public ITextProvider Text { get; } public IFormatProvider Culture { get; } }

[Serializable] public abstract class UISourceBase
{
    public abstract Type OwnerType { get; }                          // ex.: typeof(AbilitySystemComponent)
    public virtual void Validate(ValidationResult result, string propertyPath) { }
}
[Serializable] public abstract class UISource<TModel> : UISourceBase where TModel : struct
{
    public abstract UIBinding<TModel> CreateBinding();               // uma vez por binder
    public abstract TModel CreatePreview(in UIPreviewContext context);
}

public abstract class UIBinding
{
    public bool WantsTick { get; }
    protected UIBindContext Context { get; }
    protected void RequestTick(bool on);
    public void Attach(UnityEngine.Object owner, in UIBindContext context);   // chama OnAttach
    public void Detach();                                                      // chama OnDetach
    protected abstract void OnAttach(UnityEngine.Object owner);               // assina e publica o estado atual
    protected abstract void OnDetach();                                        // desassina
    public virtual void Tick(double time) { }
}
public abstract class UIBinding<TModel> : UIBinding where TModel : struct
{
    protected void Publish(in TModel model);                                   // entrega ao binder
}
```

- `ActorResolver` (`[Serializable] sealed class`): `ActorResolveMode mode` (`SelfOrParent`, `FromAnchor`, `Explicit`), `RuntimeAnchor anchor`, `UnityEngine.Object explicitOwner`. `bool TryResolve(Component from, Type ownerType, out UnityEngine.Object owner)`: `SelfOrParent` por `GetComponentInParent(ownerType)` (uma vez, ao ligar); `FromAnchor` pelo `UntypedValue` (tipo conferido); `Explicit` pelo campo. Falha não lança: o binder mostra o estado vazio e avisa uma vez em desenvolvimento (`Debug.isDebugBuild`).

### 3. Binder, driver e tema

- `UIBinderBase : MonoBehaviour` (`[ExecuteAlways]`, `IValidatable`): `ActorResolver owner`, `UITheme theme`, `UIUpdateDriver driver` (vazio: o do pai, lido uma vez no `OnEnable`). No Play, `OnEnable` resolve o dono, cria a ligação na primeira vez, chama `Attach` e, com `FromAnchor`, assina `anchor.Changed` (religa: `Detach` + `Attach` com o novo valor, ou estado vazio). `OnDisable`: `Detach`, desassina e sai do driver. Fora do Play: desenha a prévia e não liga nada. `public void Rebind()` (troca de fonte ou de dono por código).
- `UIBinder<TModel> : UIBinderBase`: `[SerializeReference, TypePicker] UISource<TModel> source`, `MonoBehaviour view` (convertido para `IView<TModel>` uma vez), `SetSource(UISource<TModel>)`, `SetView(IView<TModel>)` (para código e testes). O `Publish` da ligação chega em `protected virtual void OnModel(in TModel model)`, que por padrão chama `view.Render(model)`.
- `UIUpdateDriver : MonoBehaviour`: `TimeSourceAsset timeSource` (vazio: `TimeSourceAsset.Scaled`), `Register(UIBinding)`/`Unregister(UIBinding)`, `public void Tick()` (o `Update` só chama isso). Sem ligação registrada, `enabled = false`: nem o `Update` roda (C7). Lista com remoção segura durante o laço (guarda de índice).
- `UITheme : ScriptableObject` (`Create > SpellBook > UI > Theme`): `TextRoleTheme roles` (vazio: `TextRoleTheme.Default`), `TextProviderAsset text` (vazio: textos padrão, cultura invariante). `Color GetColor(TextRole)`. Fontes e tamanhos ficam nas views (o tema não conhece TextMeshPro).

### 4. Modelos e binders de atributo e tag (R7)

`Runtime/UI/UIModels.cs`:
- `BarModel`: `Current`, `Max`, `Fraction` (0 a 1, `Max ≤ 0` dá 0), `TrailFraction`, `Text` (`TextSpan`), `Role` (`TextRole`), `IsEmpty`. Sem campo de escudo (P1).
- `TextModel`: `Text` (`TextSpan`), `Role`, `IsEmpty`. `TagIndicatorModel`: `Active`, `IsEmpty`.
- `BarBinder` (rastro atrasado: `trailDelay` e `trailSpeed`; o binder segura o `TrailFraction` no valor antigo e o baixa por tick depois do atraso; pede tick só enquanto o rastro está acima da fração), `TextBinder`, `TagIndicatorBinder`. Cada um com `[HelpURL]` para a página do widget.

### 5. Fontes do GAS (`SpellBook.GAS.UI`, `Runtime/GAS.UI/`; refs Core, Tags, Time, GAS, UI)

- `AttributeBarSource : UISource<BarModel>` e `AttributeTextSource : UISource<TextModel>`: `AttributeDefinition current`, `AttributeDefinition max` (opcional), `AttributeTextMode` (`Current`, `CurrentOfMax`). Uma classe interna `AttributeReader` assina o `OnValueChanged` do estado atual e do estado do máximo e escreve o texto num `TextBuffer` pelo `AttributeDisplay.Format(TextBuffer, ...)` com a cultura do contexto (`CurrentOfMax`: "50 / 100", com o separador num `LocalizedText separator` da fonte, padrão `" / "`). Papel: `Display.Role` do atributo. `Hidden` não impede a leitura (R8). Prévia: 50 de 100.
- `TagSource : UISource<TagIndicatorModel>`: `GameplayTag tag` ou `GameplayTagQuery query` (um dos dois, pelo modo); assina `OnTagChanged` e reavalia `HasTag`/`HasMatching`. Prévia: aceso.

### 6. Números flutuantes (R13, R14)

- `FloatingTextStyle : ScriptableObject` (UI): `TextRole role`, `float duration`, `Vector2 rise`, `Vector2 randomOffset`, `LocalizedText format` (opcional; ex. "Imune").
- `FloatingTextRequest` (UI, `struct`): `Vector3 WorldPosition`, `TextSpan Text`, `TextRole Role`, `FloatingTextStyle Style`.
- `IFloatingTextView` (UI): `void Show(in FloatingTextRequest request)`, `void SetProgress(float normalized)`.
- `FloatingTextBinder : UIBinder<FloatingTextRequest>` (UI): `GameObject prefab`, `SpawnerAsset spawner`, `Transform container`. Cada `Publish` cria um número pelo `SpawnerAsset` (`null`: pula e avisa uma vez em desenvolvimento, como o RP-07), lê o `IFloatingTextView` uma vez por instância nova (cache por instância numa lista paralela pré-alocada), guarda o início e pede tick; no tick chama `SetProgress` e devolve ao spawner no fim. Nunca `Instantiate`, `Destroy` nem corrotina.
- `FloatingTextRules : ScriptableObject` (GAS.UI): `List<AttributeRule { AttributeDefinition attribute; ChangeSign sign; GameplayTagContainer damageTags; FloatingTextStyle style; }>` (primeira que casa vence; tags vazias casam qualquer efeito) e `FloatingTextStyle blocked`.
- `FloatingTextSource : UISource<FloatingTextRequest>` (GAS.UI): `FloatingTextRules rules`, `Vector3 offset`. Assina `OnAttributeChangeApplied` (relatório com `Applied ≠ 0`, ou `Requested ≠ 0` e `Final == 0`, que vira "bloqueado"), `OnEffectBlocked` (`BlockedByImmunity`) e `OnBlocked`. Texto do valor pelo `AttributeDisplay` com sinal; o do bloqueio pelo `format` do estilo. Crítico não existe no pacote (P2).
- `FloatingTextView` (UI.UGUI): converte a posição do mundo para o `RectTransform` do container (câmera do Canvas), move pela subida e some no fim.

### 7. Views e prefabs (`SpellBook.UI.UGUI`, `Runtime/UI.UGUI/`)

- `BarView` (`Image` de preenchimento, `Image` de rastro, `TMP_Text` opcional, cor pelo papel via `UITheme`), `TextView` (`TMP_Text`), `TagIndicatorView` (`GameObject` mostrado e escondido), `FloatingTextView`. Prefabs em `Runtime/UI.UGUI/Prefabs/`: `AttributeBar`, `AttributeText`, `TagIndicator`, `FloatingText`. Texto com fonte nula (cai na fonte padrão do `TMP_Settings`); o manual pede os TMP Essentials.

### 8. Inspector e validação (R16, R17)

- `Editor/UI/UIBinderEditor.cs` (base dos Inspectors de binder, herda de `SpellBookEditor`): fonte pelo seletor de tipo, resolvedor, view, linha de resumo (`SummaryFormatter` do AV1 sobre a fonte, mais o modo do dono: "Barra · Vida / VidaMax · dono: âncora Jogador") e redesenho da prévia ao mudar.
- Validação: binder sem fonte ou sem view; view que não implementa `IView<TModel>`; `FromAnchor` sem âncora ou com âncora de tipo diferente do `OwnerType`; `Explicit` sem referência; `SelfOrParent` sem dono no pai (aviso, só em cena aberta); fonte de atributo sem atributo; máximo igual ao atual (aviso); tag e consulta vazias; regra de número sem estilo; `FloatingTextBinder` sem prefab, ou prefab sem `IFloatingTextView`.

### 9. O que não muda

- O GAS não referencia UI (CA12). Nenhuma fonte usa o relay (R10). O Sample não muda neste item (CA18).

## Quebras de compatibilidade

Nenhuma. Adicionado (CHANGELOG): base `RuntimeAnchor` e `AnchorRegistrar` (Core), `AbilitySystemAnchor` (GAS), módulos `SpellBook.UI`, `SpellBook.UI.UGUI`, `SpellBook.GAS.UI` e os `.Editor`, contratos, `ActorResolver`, `UIUpdateDriver`, `UITheme`, modelos, fontes, views, prefabs, números flutuantes, dependência `com.unity.ugui` 2.0.0.

## Tickets

Todos usam a Unity e seguem o WIP 1. **Não há par paralelo sem Editor.** Testes de lógica com fonte, view e spawner de teste (sem Canvas); views com Canvas em EditMode; tempo por `ManualTimeSource`; ASC pela `GasFactory`; alocação por `Is.Not.AllocatingGCMemory()` com aquecimento. Teto: ~500 linhas e 10 arquivos.

**Conflitos com a fila:** `AsmdefRulesTests.cs`, `SpellBookEditorCoverageTests.cs`, `DocUrls.cs` e `DocUrlsTests.cs` são tocados por quase todo ticket daqui e por DE2 a DE4, CU2, CU7, TM*: em série. UI1 toca `RuntimeAnchor.cs` (CU1), `CoreEditors.cs` (DE2) e `GasEditors.cs` (Lotes B a D). `package.json` (UI8) é tocado também pelo B-032.

### UI1 — Âncora não genérica, `AnchorRegistrar` e âncora do ASC
- Escopo: `Runtime/Core/Anchors/RuntimeAnchor.cs` (base), `Runtime/Core/Anchors/AnchorRegistrar.cs` (novo), `Editor/Core/Inspector/CoreEditors.cs`, `Runtime/GAS/Core/AbilitySystemAnchor.cs` (novo), `Editor/GAS/GasEditors.cs`, `Tests/Core/EditMode/RuntimeAnchorBaseTests.cs` (novo), `Tests/Core/PlayMode/AnchorRegistrarTests.cs` (novo), `Documentation~/manual/inicio/conceitos.md` (âncoras), `gas.md` (`#abilitysystemanchor`), `CHANGELOG.md`.
- Testes red: `Base_UntypedValue_ValueType_ChangedWithTyped`; `ProvideUntyped_WrongType_Rejected`; `Registrar_ProvidesOnEnable_UnsetsOnDisable`; `Respawn_NewProvides_OldDisableDoesNotClear` (C5, parte); `Registrar_EmptyValue_UsesComponentOfAnchorType`; `Registrar_Validate_NoAnchorOrWrongType_Error`.
- Critério de pronto: testes verdes; os testes do CU1 passam sem edição.
- Tamanho estimado: ~360 linhas; 10 arquivos. Depende de: CU1.

#### Detalhe (2026-11-04)
- CA: nenhum CA do PRD diretamente; base do P3, do Design 1 e do CA5 (C5, parte: o respawn não apaga o novo). ADR 0039, item 5.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/Core/Anchors/RuntimeAnchor.cs:13-67`: nova base `public abstract class RuntimeAnchor : ScriptableObject` no mesmo arquivo, com `UntypedValue`, `ValueType`, `event Action<RuntimeAnchor> Changed`, `bool ProvideUntyped(Object)` (tipo errado: devolve `false`, sem mudar o valor e sem evento; `null` esvazia como `Provide(null)`) e `void UnsetUntyped(Object)` (tipo errado: nada). O registro de sessão (`IAnchor`, `OnEnable`/`OnDisable` protegidos virtuais) sobe para a base, e o reset limpa também `Changed`. `RuntimeAnchor<T>` passa a derivar da base; `Provide` e `Unset` disparam `OnChanged` e `Changed` no mesmo ponto. A API pública do `RuntimeAnchor<T>` não muda. A base só é derivável pelo `RuntimeAnchor<T>` (membro abstrato `private protected`).
  - `Runtime/Core/Anchors/AnchorRegistrar.cs` (novo; `[HelpURL(DocUrls.Concepts + "#ancoras")]`, `IValidatable`): campos `anchor` (`RuntimeAnchor`) e `value` (`Component`); vazio, lê no `Awake` o `GetComponent(anchor.ValueType)` (só se o tipo for `Component` ou interface). `OnEnable` → `ProvideUntyped`; `OnDisable` → `UnsetUntyped` (só limpa se for o mesmo). Validação: sem âncora, erro; `value` que não é do `ValueType`, erro; `value` vazio sem componente do tipo no objeto, erro.
  - `Editor/Core/Inspector/CoreEditors.cs:7-22`: `AnchorRegistrarEditor`, uma linha.
  - `Runtime/GAS/Core/AbilitySystemAnchor.cs` (novo): `AbilitySystemAnchor : RuntimeAnchor<AbilitySystemComponent>`, `[HelpURL(DocUrls.Gas + "#abilitysystemanchor")]`, menu `SpellBook/GAS/Ability System Anchor`.
  - `Editor/GAS/GasEditors.cs:15-33`: `AbilitySystemAnchorEditor`, uma linha (cobertura de Inspector).
  - `Tests/Core/EditMode/RuntimeAnchorBaseTests.cs` (novo; usa o `TestObjectAnchor` do CU1).
  - `Tests/Core/PlayMode/AnchorRegistrarTests.cs` (novo; âncora de teste `RuntimeAnchor<BoxCollider>` no arquivo, campos pelo `SerializedObject` com o objeto inativo, como `Tests/Testing/CuesFactory.cs:79-92`).
  - Sem `conceitos.md`, `gas.md` nem `CHANGELOG.md`: manual só no passe de documentação (emenda de 2026-11-03 da 0007) e o ticket não quebra nada. As guardas de âncora do manual são da categoria `Documentacao`.
- Testes red: EditMode (`SpellBook.Core.Tests.EditMode`, `RuntimeAnchorBaseTests`): `Base_UntypedValue_ValueType_ChangedWithTyped`; `ProvideUntyped_WrongType_Rejected`. PlayMode (`SpellBook.Core.Tests.PlayMode`, `AnchorRegistrarTests`): `Registrar_ProvidesOnEnable_UnsetsOnDisable`; `Respawn_NewProvides_OldDisableDoesNotClear`; `Registrar_EmptyValue_UsesComponentOfAnchorType`; `Registrar_Validate_NoAnchorOrWrongType_Error`.
- Critério de pronto: os 6 testes verdes pelos filtros `\.RuntimeAnchorBaseTests\.` e `\.AnchorRegistrarTests\.`; os 5 `RuntimeAnchorTests` do CU1 verdes sem edição; `SpellBookEditorCoverageTests` e `HelpUrlTests` verdes.
- Tamanho: ~440 linhas e 7 arquivos, pela ADR 0042. Exceção: nenhuma.
- Commit: `feat(core): âncora não genérica, AnchorRegistrar e âncora do ASC`

### UI2 — Módulo `SpellBook.UI`: contratos e `ActorResolver`
- Escopo: `Runtime/UI/SpellBook.UI.asmdef` (novo; Core, Time), `Runtime/UI/UISource.cs` (contratos do Design 2), `Runtime/UI/ActorResolver.cs` (novos), `Tests/UI/EditMode/SpellBook.UI.Tests.EditMode.asmdef`, `Tests/UI/EditMode/UIContractsTests.cs` (novos), `AsmdefRulesTests.cs` (`UI` em `RuntimeModules`; referências exatas Core e Time; nada de `UnityEngine.UI`, `Unity.TextMeshPro` nem `SpellBook.GAS*`), `Runtime/Core/DocUrls.cs` (`Ui = Base + "ui/visao-geral/"`), `DocUrlsTests.cs`, `Documentation~/manual/ui/visao-geral.md` (nova: três camadas, dono, contratos), `CHANGELOG.md`.
- Testes red: `Resolver_SelfOrParent_FindsOwnerInParent`; `Resolver_Explicit`; `Resolver_Anchor_ReadsUntypedValue_TypeChecked`; `Resolver_Missing_FalseNoException` (C6); `Binding_AttachPublishesCurrent_DetachStops`; `Binding_RequestTick_TogglesWantsTick`; `UIAsmdef_ReferencesOnlyCoreAndTime` (C12, CA12).
- Critério de pronto: CA6 (parte do resolvedor) e CA12 (parte do UI) verdes.
- Tamanho estimado: ~450 linhas; 10 arquivos. Depende de: UI1, DE1.

#### Detalhe (2026-11-04)
- CA: CA6 (parte do resolvedor: `SelfOrParent`, `FromAnchor` e `Explicit` acham o dono; dono ausente não lança) e CA12 (parte do `SpellBook.UI`: só Core e Time, sem uGUI, TMP nem GAS). Base do CA11 (`IView<T>`) e do P1 (ciclo `Attach`/`Detach`, `RequestTick`). ADR 0039, itens 1 a 5.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/UI/SpellBook.UI.asmdef` (novo): `SpellBook.UI`, referências `SpellBook.Core` e `SpellBook.Time`, `overrideReferences` e `autoReferenced` como os outros módulos.
  - `Runtime/UI/UISource.cs` (novo; namespace `SpellBook.UI`): os contratos do Design 2. `IView<TModel>`; `UIBindContext` (`Text`, `Culture`; cultura nula cai na do provedor e depois na invariante); `UIPreviewContext` (`Level`, `Text`, `Culture`, com a mesma regra); `UISourceBase` (`OwnerType`, `Validate`); `UISource<TModel>` (`CreateBinding`, `CreatePreview`); `UIBinding` (`WantsTick`, `IsAttached`, `Context`, `RequestTick`, `Attach`, `Detach`, `OnAttach`, `OnDetach`, `Tick`); `UIBinding<TModel>` (`Publish`, `SetReceiver`). `Attach` com a ligação já ligada desliga antes; `Attach` com dono nulo lança `ArgumentNullException` (o binder só liga depois do resolvedor achar o dono); `Detach` solta o tick; `Publish` fora de ligação ou sem receptor não faz nada.
  - Decisões do detalhe, aditivas e sem mudar CA: (1) o `Publish` "entrega ao binder" por uma interface nova `IUIModelReceiver<TModel>` (`void Receive(in TModel model)`), registrada uma vez por `UIBinding<TModel>.SetReceiver`, porque o `Attach` é da base não genérica e um delegate com `in` não é `Action<T>`; o `UIBinder<TModel>` do UI3 a implementa e chama o `OnModel`. (2) O `UIBindContext` sai sem `Theme`: o `UITheme` é do UI4, que acrescenta a propriedade e o construtor (sem quebra, o módulo ainda não foi publicado).
  - `Runtime/UI/ActorResolver.cs` (novo): `enum ActorResolveMode { SelfOrParent, FromAnchor, Explicit }` e `[Serializable] sealed class ActorResolver` com os campos `mode`, `anchor` (`RuntimeAnchor`) e `explicitOwner` (`UnityEngine.Object`), propriedades só de leitura e fábricas `ForSelfOrParent()`, `ForAnchor(RuntimeAnchor)` e `ForExplicit(Object)` para código e testes. `TryResolve(Component from, Type ownerType, out Object owner)`: `SelfOrParent` por `from.GetComponentInParent(ownerType)`; `FromAnchor` pelo `UntypedValue`, aceito só se for do `ownerType`; `Explicit` pelo campo, aceito se for do `ownerType` ou, sendo `GameObject`, pelo componente do `ownerType` nele (o arrasto comum no Inspector). `from` ou `ownerType` nulos, âncora vazia ou valor de outro tipo: `false`, sem exceção. O aviso uma vez em desenvolvimento é do binder (UI3).
  - `Tests/UI/EditMode/SpellBook.UI.Tests.EditMode.asmdef` (novo): `SpellBook.Core`, `SpellBook.Time`, `SpellBook.UI`, `SpellBook.Testing` e o test runner, como o de Cues.
  - `Tests/UI/EditMode/UIContractsTests.cs` (novo): fonte, ligação, receptor e âncora de teste no próprio arquivo (`RuntimeAnchor<BoxCollider>`), objetos criados e destruídos no teste.
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:18-21`: `"UI"` em `RuntimeModules` (o `AllExpectedAssembliesExist` e o `EachRuntimeModuleHasEditModeTests` passam a exigir `SpellBook.UI` e `SpellBook.UI.Tests.EditMode`); novo `UIAsmdef_ReferencesOnlyCoreAndTime` depois do `CuesAsmdef_ReferencesCoreTagsTime` (`:394-406`): caminho `Runtime/UI/SpellBook.UI.asmdef`, tipo runtime, referências exatamente `SpellBook.Core` e `SpellBook.Time` (todas, não só as `SpellBook.*`), nenhuma `UnityEngine.UI`, `Unity.TextMeshPro` nem `SpellBook.GAS*`, e o assembly compilado sem referência a `SpellBook.GAS*`, `UnityEngine.UI` ou `Unity.TextMeshPro`.
  - Fora deste ticket: `DocUrls.cs` e `DocUrlsTests.cs` (nenhum tipo do UI2 é `MonoBehaviour` nem `ScriptableObject`, então nenhum tem `[HelpURL]`; uma constante `DocUrls.Ui` sem a página quebraria a guarda `Documentacao` do `ManualLinksTests`), `manual/ui/visao-geral.md` (manual só no passe de documentação, emenda de 2026-11-03 da 0007) e `CHANGELOG.md` (o ticket não quebra nada). O `UI3` é o primeiro com `[HelpURL]` (binders).
- Testes red: EditMode (`SpellBook.UI.Tests.EditMode`, `UIContractsTests`): `Resolver_SelfOrParent_FindsOwnerInParent`; `Resolver_Explicit` (objeto do tipo e `GameObject` com o componente); `Resolver_Anchor_ReadsUntypedValue_TypeChecked`; `Resolver_Missing_FalseNoException` (C6: os três modos sem dono, `from` nulo e tipo errado); `Binding_AttachPublishesCurrent_DetachStops`; `Binding_AttachAgain_MovesToNewOwner` (religação na mesma ligação, base do respawn); `Binding_RequestTick_TogglesWantsTick`. EditMode (`SpellBook.Core.Tests.EditMode`, `AsmdefRulesTests`): `UIAsmdef_ReferencesOnlyCoreAndTime` (C12, CA12).
- Critério de pronto: os 8 testes verdes pelos filtros `\.UIContractsTests\.` e `\.AsmdefRulesTests\.`; toda a `AsmdefRulesTests` verde (inclui `AllExpectedAssembliesExist` e `EachRuntimeModuleHasEditModeTests` com o `UI`); `HelpUrlTests` e `SpellBookEditorCoverageTests` verdes sem mudança.
- Tamanho: ~642 linhas e 6 arquivos, pela ADR 0042 (medido no diff: `.asmdef` 44, `UISource.cs` 177, `ActorResolver.cs` 113, `UIContractsTests.cs` 288, `AsmdefRulesTests.cs` 20). Exceção: +28% sobre o teto de ~500 linhas (a estimativa inicial era ~480). Motivo: os contratos são a API pública de um módulo novo e levam a documentação XML completa, e os dublês de teste (dono, fonte, ligação, receptor e âncora) ficam no arquivo de teste porque ainda não há factory de UI no `SpellBook.Testing`. Dividir separaria o `UIBinding` do `ActorResolver`, que o UI3 usa juntos.
- Commit: `feat(ui): módulo SpellBook.UI, contratos e ActorResolver`

### UI3 — Binder base: ligação, religação pela âncora, view e prévia
- Escopo: `Runtime/UI/UIBinder.cs` (novo: `UIBinderBase`, `UIBinder<TModel>`), `Tests/UI/PlayMode/SpellBook.UI.Tests.PlayMode.asmdef`, `Tests/UI/PlayMode/UIBinderTests.cs` (novos; binder, fonte e view de teste), `Tests/UI/EditMode/UIBinderPreviewTests.cs` (novo), `AsmdefRulesTests.cs` (`UI` em `PlayModeModules`), `visao-geral.md`, `CHANGELOG.md`.
- Testes red: `OnEnable_BindsAndRendersCurrent_NoUpdate` (CA1, parte); `AnchorChanges_Rebinds_OldOwnerListenerRemoved` (CA5); `Disable_Detaches_NoMoreRenders`; `OwnerMissing_RendersEmpty_WarnsOnce_NoException` (CA6); `CustomView_ReceivesModel_SourceUnchanged` (CA11, C11); `EditMode_RendersPreview_DoesNotBind` (CA10, parte automatizável); `Rebind_SameBinding_NoAllocation`.
- Critério de pronto: CA5, CA6 e CA11 verdes.
- Tamanho estimado: ~470 linhas; 7 arquivos. Depende de: UI2.

#### Detalhe (2026-11-04)
- CA: CA5 (o dono pela âncora é achado sem busca e o binder religa na troca), CA6 (parte do binder: dono ausente não lança, a view mostra o estado vazio e o aviso sai uma vez em desenvolvimento), CA11 (uma `IView<T>` própria recebe o modelo; trocar a view não muda a fonte), CA1 (parte: liga e desenha o estado atual por evento, sem `Update`) e CA10 (parte automatizável: prévia em edição sem ligar). ADR 0039, itens 1, 3, 5 e 7.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/UI/UIBinder.cs` (novo; namespace `SpellBook.UI`): `[ExecuteAlways] public abstract class UIBinderBase : MonoBehaviour` com `[SerializeField] ActorResolver owner` (padrão `SelfOrParent`), propriedade `Owner`, `SetOwner(ActorResolver)` e `Rebind()`. No Play, `OnEnable` resolve o dono pelo `OwnerType` da fonte, cria a ligação na primeira vez (e de novo só se a fonte for trocada), chama `Attach` com o contexto e, no modo `FromAnchor`, assina `anchor.Changed` com um delegate criado uma vez (no `Changed`: `Detach` e nova resolução, `Attach` ou estado vazio). `OnDisable`: `Detach` e desassina. Fora do Play, `OnEnable` e `Rebind()` desenham `CreatePreview(new UIPreviewContext(1))` e nada liga (o Inspector do UI13 redesenha pelo `Rebind()`). Sem `Update`. `public abstract class UIBinder<TModel> : UIBinderBase, IUIModelReceiver<TModel>` com `[SerializeReference, TypePicker] UISource<TModel> source` e `[SerializeField] MonoBehaviour view` (convertido para `IView<TModel>` uma vez), `Source`, `View`, `SetSource(UISource<TModel>)` e `SetView(IView<TModel>)` (ligado, religam na hora; em edição, redesenham a prévia) e `protected virtual void OnModel(in TModel model)`, que por padrão chama `View.Render(model)`.
  - Decisões do detalhe, aditivas e sem mudar CA: (1) o estado vazio é `default(TModel)` entregue ao `OnModel`; os modelos do UI5 ficam com `IsEmpty` verdadeiro no `default`. (2) O aviso "dono não encontrado" sai uma vez por binder, só com `Debug.isDebugBuild`; no modo `FromAnchor`, âncora sem valor não avisa (o ator ainda não nasceu; o binder espera o `Changed`), mas campo de âncora vazio ou valor de outro tipo avisa. (3) Sem fonte, o binder mostra o estado vazio, sem aviso (a regra é do validador, UI13). (4) Ficam para o UI4: `theme`, `driver` e o registro no driver (o contexto sai com `new UIBindContext(null)`); para o UI13: `IValidatable` e o Inspector. (5) `UIBinderBase` e `UIBinder<T>` são abstratos: sem `[HelpURL]` e sem `DocUrls` neste ticket (os binders concretos e as páginas chegam no UI5 e no UI7).
  - `Tests/UI/PlayMode/SpellBook.UI.Tests.PlayMode.asmdef` (novo): como o de Cues (Core, Time, UI, Testing e o test runner).
  - `Tests/UI/PlayMode/UIBinderTests.cs` (novo): dono, âncora, modelo, fonte, ligação, view e binder de teste no próprio arquivo.
  - `Tests/UI/EditMode/UIBinderPreviewTests.cs` (novo): fonte, view e binder de teste mínimos no próprio arquivo.
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:37`: `"UI"` em `PlayModeModules` (o `AllExpectedAssembliesExist` passa a exigir `SpellBook.UI.Tests.PlayMode`).
  - Fora deste ticket: `visao-geral.md` (manual só no passe de documentação, emenda de 2026-11-03 da 0007) e `CHANGELOG.md` (o ticket não quebra nada).
- Testes red: PlayMode (`SpellBook.UI.Tests.PlayMode`, `UIBinderTests`): `OnEnable_BindsAndRendersCurrent_NoUpdate` (CA1, parte); `AnchorChanges_Rebinds_OldOwnerListenerRemoved` (CA5); `Disable_Detaches_NoMoreRenders`; `OwnerMissing_RendersEmpty_WarnsOnce_NoException` (CA6); `CustomView_ReceivesModel_SourceUnchanged` (CA11, C11); `Rebind_SameBinding_NoAllocation`. EditMode (`SpellBook.UI.Tests.EditMode`, `UIBinderPreviewTests`): `EditMode_RendersPreview_DoesNotBind` (CA10, parte automatizável).
- Critério de pronto: os 7 testes verdes pelos filtros `\.UIBinderTests\.` e `\.UIBinderPreviewTests\.`; `AsmdefRulesTests`, `HelpUrlTests`, `TypePickerUsageTests` e `UIContractsTests` verdes.
- Tamanho: ~647 linhas e 5 arquivos, pela ADR 0042 (medido: `UIBinder.cs` 247, asmdef 25, `UIBinderTests.cs` 290, `UIBinderPreviewTests.cs` 83, `AsmdefRulesTests.cs` 2). Exceção: +29% sobre o teto de ~500 linhas (a estimativa era ~520). Motivo: o binder base é API pública do módulo, com documentação XML, e os dublês de teste (dono, âncora, modelo, fonte, ligação, view e binder) ficam em cada arquivo de teste, porque o EditMode e o PlayMode são assemblies separados e ainda não há factory de UI no `SpellBook.Testing`. Dividir separaria a religação pela âncora da ligação básica, que só fazem sentido juntas no `OnEnable`.
- Commit: `feat(ui): binder base com religação pela âncora, view e prévia`

### UI4 — `UIUpdateDriver`, `UITheme` e Editor do módulo
- Escopo: `Runtime/UI/UIUpdateDriver.cs`, `Runtime/UI/UITheme.cs` (novos), `UIBinder.cs` (registro no driver), `Editor/UI/SpellBook.UI.Editor.asmdef`, `Editor/UI/UIEditors.cs` (novos; uma linha por tipo), `Tests/UI/EditMode/UIUpdateDriverTests.cs` (novo), `AsmdefRulesTests.cs` (`UI` em `EditorModules`), `SpellBookEditorCoverageTests.cs` (+`SpellBook.UI`), `visao-geral.md`, `CHANGELOG.md`.
- Testes red: `NoRequester_DriverDisabled_TickDoesNothing` (C7); `TicksOnlyRequesters`; `PausedSource_NoTimeAdvance` (CA7); `UnregisterDuringTick_Safe`; `Theme_NullRoles_UsesDefault_NullText_Invariant`; `Tick10000_DoesNotAllocate` (CA13, parte).
- Critério de pronto: CA7 verde.
- Tamanho estimado: ~420 linhas; 10 arquivos. Depende de: UI3, AR4.

#### Detalhe (2026-11-04)
- CA: CA7 (o driver só roda com ligação pedindo tick e respeita a pausa do `ITimeSource`; C7) e CA13 (parte: 10 000 ticks do driver sem alocar). ADR 0039, item 1 (P1: "o driver guarda as ligações que pediram numa lista").
- Escopo (caminhos em `Packages/com.spellbook/`):
  - Pendência do UI2 (o driver não sabe quando `WantsTick` muda): `Runtime/UI/UISource.cs:99-147`. O `UIBinding` ganha um campo `internal UIUpdateDriver Driver` (posto pelo `Register`, limpo pelo `Unregister`); o `RequestTick` e o `Detach` passam por um `SetWantsTick` privado que, quando o valor muda, chama `Driver.OnWantsTickChanged(this)` (interno; conferência de nulo da Unity). Assim a lista do driver tem só as ligações que pediram, e nenhuma consulta por quadro. Sem API pública nova no `UIBinding`. `UIBindContext` (`:27-42`) ganha `Theme` e o parâmetro opcional `UITheme theme = null` no construtor (anunciado no Detalhe do UI2).
  - `Runtime/UI/UIUpdateDriver.cs` (novo): `[HelpURL(DocUrls.Ui + "#uiupdatedriver")]`, `[DisallowMultipleComponent]`, `sealed`. `[SerializeField] TimeSourceAsset timeSource` (vazio: `TimeSourceAsset.Scaled`), `TimeSource`, `SetTimeSource`, `TickingCount`, `Register(UIBinding)` (nulo lança; registrada em outro driver, sai dele antes; entra na lista se já pede tick), `Unregister(UIBinding)` (sem efeito se não é deste driver), `Tick()` (lê `ElapsedTime` uma vez e chama `Tick(tempo)` de cada ligação da lista por índice; remoção durante o laço ajusta o cursor; entrada durante o laço roda no mesmo quadro), `Update` só chama `Tick()`. `enabled` fica verdadeiro só com a lista não vazia (no `Awake` e a cada entrada e saída): sem ligação pedindo, nem o `Update` roda (C7). Em pausa, a fonte não avança e o tempo entregue fica parado (CA7).
  - `Runtime/UI/UITheme.cs` (novo): `[CreateAssetMenu("SpellBook/UI/Theme")]`, `[HelpURL(DocUrls.Ui + "#uitheme")]`. `[SerializeField] TextRoleTheme roles`, `[SerializeField] TextProviderAsset text`; `Roles` (vazio: `TextRoleTheme.Default`), `Text` (`ITextProvider`, pode ser nulo), `Culture` (do provedor, ou invariante), `GetColor(TextRole)`.
  - `Runtime/UI/UIBinder.cs:17-24, 57-140`: campos `[SerializeField] UITheme theme` e `[SerializeField] UIUpdateDriver driver`; `Theme`, `SetTheme(UITheme)` (religa, como o `SetOwner`), `Driver`. No `OnEnable` do Play, o driver ativo é o campo ou, vazio, `GetComponentInParent<UIUpdateDriver>()`, lido uma vez. `AttachToOwner` liga com `new UIBindContext(theme?.Text, null, theme)` e registra a ligação no driver ativo; `Unbind` a tira do driver.
  - `Runtime/Core/DocUrls.cs`: `Ui = Base + "ui/visao-geral/"` (a página e as âncoras `#uiupdatedriver` e `#uitheme` são do passe de documentação; emenda de 2026-11-03 da 0007, item 3). Consequência direta: `UIUpdateDriver` e `UITheme` são os primeiros tipos concretos do módulo, e o `HelpUrlTests` exige o `[HelpURL]`.
  - `Editor/UI/SpellBook.UI.Editor.asmdef` (novo: Core, Time, UI, Core.Editor) e `Editor/UI/UIEditors.cs` (novo: `UIUpdateDriverEditor` e `UIThemeEditor`, uma linha cada, derivados de `SpellBookEditor`).
  - `Tests/UI/EditMode/UIUpdateDriverTests.cs` (novo): ligação de teste que conta ticks e guarda o tempo, no próprio arquivo; `ManualTimeSource`.
  - `Tests/UI/PlayMode/UIBinderTests.cs`: a `CountSource`/`CountBinding` de teste ganham pedido de tick, contagem de ticks e o tema do contexto; um teste novo para o registro do binder no driver do pai.
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:32-35`: `"UI"` em `EditorModules`; teste novo `UIEditorAsmdef_References` (Core, Time, UI, Core.Editor) depois do `UIAsmdef_ReferencesOnlyCoreAndTime`.
  - `Tests/Core/EditMode/Inspector/SpellBookEditorCoverageTests.cs:18-22`: `+"SpellBook.UI"`.
  - Fora deste ticket: `visao-geral.md` (manual só no passe) e `CHANGELOG.md` (nada quebra). `DocUrlsTests.cs` não muda (as constantes de módulo novas não têm `TestCase` lá, como `Cues` e `Teams`).
- Testes red: EditMode (`SpellBook.UI.Tests.EditMode`, `UIUpdateDriverTests`): `NoRequester_DriverDisabled_TickDoesNothing` (C7); `TicksOnlyRequesters` (inclui soltar o tick e o `Detach` saírem da lista); `PausedSource_NoTimeAdvance` (CA7); `UnregisterDuringTick_Safe`; `Theme_NullRoles_UsesDefault_NullText_Invariant`; `Theme_Roles_UsesItsColors`; `Tick10000_DoesNotAllocate` (CA13, parte). PlayMode (`SpellBook.UI.Tests.PlayMode`, `UIBinderTests`): `Binder_RegistersInParentDriver_ThemeInContext_LeavesOnDisable`. EditMode (`SpellBook.Core.Tests.EditMode`, `AsmdefRulesTests`): `UIEditorAsmdef_References`.
- Critério de pronto: os 9 testes verdes pelos filtros `\.UIUpdateDriverTests\.`, `\.UIBinderTests\.` e `\.AsmdefRulesTests\.`; `UIContractsTests`, `UIBinderPreviewTests`, `HelpUrlTests` e `SpellBookEditorCoverageTests` verdes.
- Tamanho: ~624 linhas e 11 arquivos, pela ADR 0042 (medido no diff: `UIUpdateDriver.cs` 148, `UITheme.cs` 37, `UISource.cs` 36, `UIBinder.cs` 45, `DocUrls.cs` 1, asmdef 21, `UIEditors.cs` 14, `UIUpdateDriverTests.cs` 242, `UIBinderTests.cs` 63, `AsmdefRulesTests.cs` 15, `SpellBookEditorCoverageTests.cs` 2). Exceção: +25% nas linhas e +10% nos arquivos (a estimativa era ~590). Motivo: o `[HelpURL]` obrigatório dos dois tipos concretos traz o `DocUrls.cs`, e o teste do registro do binder no driver precisa do Play (`UIBinderTests.cs`); a pendência do `WantsTick` cabe no `UISource.cs`. Dividir separaria o driver do registro do binder, que só se prova junto.
- Commit: `feat(ui): UIUpdateDriver, UITheme e Editor do módulo`

### UI5 — Modelos e binders de barra, texto e indicador
- Escopo: `Runtime/UI/UIModels.cs`, `BarBinder.cs`, `TextBinder.cs`, `TagIndicatorBinder.cs` (novos), `UIEditors.cs`, `Tests/UI/PlayMode/WidgetBindersTests.cs` (novo), `visao-geral.md`, `CHANGELOG.md`.
- Testes red: `Bar_Fraction_ZeroMax_Zero`; `Bar_TrailHoldsThenFalls_TickOnlyWhileAbove`; `TwoBars_Independent_NoShieldField` (CA3, com fontes de teste); `ModelUpdates10000_DoNotAllocate` (CA13, parte).
- Critério de pronto: CA3 verde (lado da UI).
- Tamanho estimado: ~380 linhas; 8 arquivos. Depende de: UI4.
- Divisão (2026-11-04, ADR 0042): o UI5 medido passou de +30% do teto e virou dois tickets. **UI5a** (#239): modelos e os três binders, sem rastro na barra (CA3 e CA13, parte); depende de UI4. **UI5b** (#333): rastro atrasado do `BarBinder` e tick do próprio binder (CA13, parte); depende de UI5a. O UI6 e o UI10 dependem também do UI5b.

#### Detalhe (2026-11-04) — UI5a (#239)
- Divisão pela ADR 0042: o UI5 medido deu ~704 linhas (+41%) e foi dividido em UI5a (esta issue, #239) e UI5b (#333). O UI5a entrega os modelos e os três binders; o `BarBinder` não tem rastro, e a fração entra direto (o `TrailFraction` do `BarModel` sai igual à `Fraction`). O rastro e o tick do binder ficam no UI5b.
- CA: CA3 (lado da UI: duas barras com fontes diferentes atualizam de forma independente; o `BarModel` não tem campo de escudo, P1) e CA13 (parte: 10 000 atualizações de modelo pelos três binders sem alocar). R7. ADR 0039, itens 1 a 4.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/UI/UIModels.cs` (novo; `SpellBook.UI`): `readonly struct BarModel` (`Current`, `Max`, `Fraction` de 0 a 1 com `Max ≤ 0` dando 0, `TrailFraction` igual à `Fraction` ao criar, `Text` (`TextSpan`), `Role` (`TextRole`), `IsEmpty` verdadeiro só no `default`; construtor `(current, max, text, role)` e `WithTrail(float)`, que devolve a cópia com o rastro entre 0 e 1, para o UI5b); `readonly struct TextModel` (`Text`, `Role`, `IsEmpty`); `readonly struct TagIndicatorModel` (`Active`, `IsEmpty`). Sem campo de escudo.
  - `Runtime/UI/BarBinder.cs`, `TextBinder.cs` e `TagIndicatorBinder.cs` (novos; `UIBinder<BarModel>`, `UIBinder<TextModel>` e `UIBinder<TagIndicatorModel>`, sem lógica; `[HelpURL(DocUrls.UiAttributeBar)]`, `[HelpURL(DocUrls.UiAttributeText)]` e `[HelpURL(DocUrls.UiTagIndicator)]`).
  - `Runtime/Core/DocUrls.cs:26`: `UiAttributeBar` (`ui/barra-de-atributo/`), `UiAttributeText` (`ui/texto-de-atributo/`) e `UiTagIndicator` (`ui/indicador-de-tag/`). Consequência direta: o `HelpUrlTests` exige `[HelpURL]` nos três binders concretos, e o design doc pede a página do widget. As páginas e os `TestCase` do `DocUrlsTests` continuam no UI7 e no passe de documentação (emenda de 2026-11-03 da 0007), como a `DocUrls.Ui` do UI4.
  - `Editor/UI/UIEditors.cs:14`: `BarBinderEditor`, `TextBinderEditor` e `TagIndicatorBinderEditor`, uma linha cada (cobertura de Inspector).
  - `Tests/UI/PlayMode/WidgetBindersTests.cs` (novo): dono com valores por índice, fontes de barra, texto e tag, e views de teste no próprio arquivo; driver com `ManualTimeSource`.
  - `UIBinder.cs` não muda neste ticket. Fora deste ticket: `visao-geral.md` (manual só no passe) e `CHANGELOG.md` (nada quebra).
- Testes red: PlayMode (`SpellBook.UI.Tests.PlayMode`, `WidgetBindersTests`): `Bar_Fraction_ZeroMax_Zero`; `TwoBars_Independent_NoShieldField` (CA3); `TextAndTag_Binders_DeliverModel_EmptyByDefault`; `ModelUpdates10000_DoNotAllocate` (CA13, parte).
- Critério de pronto: os 4 testes verdes pelo filtro `\.WidgetBindersTests\.`; `UIBinderTests`, `UIBinderPreviewTests`, `UIUpdateDriverTests`, `HelpUrlTests` e `SpellBookEditorCoverageTests` verdes.
- Tamanho: ~447 linhas e 7 arquivos, pela ADR 0042 (medido: `UIModels.cs` 99, `BarBinder.cs` 14, `TextBinder.cs` 11, `TagIndicatorBinder.cs` 11, `DocUrls.cs` 3, `UIEditors.cs` 12, `WidgetBindersTests.cs` 297). Exceção: nenhuma (dentro do teto de ~500).
- Commit: `feat(ui): modelos e binders de barra, texto e indicador`

#### Detalhe (2026-11-04) — UI5b (#333)
- Rastro atrasado do `BarBinder` e tick do próprio binder. Depende do UI5a (#239). O UI6 e o UI10 passam a depender também do UI5b (o UI10 usa o tick do binder para os números flutuantes).
- O código já foi escrito e revisado no UI5 e está guardado fora do repositório, em `C:/Users/gabri/orca/workspaces/SpellBook/_guardado-ui5b/` (`BarBinder.cs`, `UIBinder.cs`, `UIBinder-tick.diff`, `WidgetBindersTests.cs` e `NOTA.md` com o que reaplicar). Inclui a correção da revisão de 2026-11-04 (queda só com fração menor que a anterior; `trailSpeed` 0 durante a queda leva o rastro à fração e solta o tick).
- CA: CA13 (parte: o binder só pede tick enquanto o rastro está acima da fração). R7. ADR 0039, itens 1 a 4.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/UI/UIBinder.cs:217-397` (`UIBinderBase`): `protected bool RequestBinderTick(bool on)` (pede ou solta; devolve `false` sem driver ativo ou fora de ligação, para o binder cair no comportamento sem animação) e `protected virtual void OnBinderTick(double time)`. Por dentro, uma ligação privada criada uma vez (`BinderTicker : UIBinding`, ligada ao próprio binder, sem fonte), registrada no driver ativo e solta no `Unbind`. Sem `Update`; o tempo continua vindo só do `UIUpdateDriver` (ADR 0039, "não pode").
  - `Runtime/UI/BarBinder.cs`: `[SerializeField, Min(0)] float trailDelay = 0.5f` (segundos) e `trailSpeed = 1f` (fração por segundo; 0 desliga o rastro), `TrailDelay`, `TrailSpeed` e `SetTrail(delay, speed)`. Na queda da fração, segura o `TrailFraction` no valor antigo, pede tick, conta o atraso a partir do primeiro tick depois da queda (nova queda reinicia o atraso) e baixa o rastro pela velocidade até a fração, quando solta o tick. Ao desligar a fonte (desabilitar, `Rebind`, `SetTheme`, `SetOwner`, `SetSource` ou troca de âncora), sobrescreve `DetachBinding` e zera o estado do rastro, e o estado republicado na religação entra como primeiro valor, com o rastro na fração (correção da revisão do UI5b: o rastro não fica acima da fração sem tick). Subida, estado vazio, prévia, `trailSpeed` 0 ou falta de driver: rastro igual à fração na hora.
  - `Tests/UI/PlayMode/WidgetBindersTests.cs`: os 7 testes de rastro.
- Testes red: PlayMode (`WidgetBindersTests`): `Bar_TrailHoldsThenFalls_TickOnlyWhileAbove`; `Bar_NoDriver_TrailFollowsFraction`; `Bar_RiseBelowTrail_TrailSnapsToFraction`; `Bar_RepublishSameFraction_KeepsFalling`; `Bar_SpeedZeroWhileFalling_TrailSnapsAndReleasesTick`; `Bar_RebindWhileFalling_TrailDoesNotStick`; `Bar_ReenableWhileFalling_TrailDoesNotStick`.
- Critério de pronto: os 11 testes verdes pelo filtro `\.WidgetBindersTests\.`; `UIBinderTests` e `UIUpdateDriverTests` verdes.
- Tamanho: ~315 linhas e 4 arquivos, pela ADR 0042 (medido com a correção da revisão: `UIBinder.cs` 58, `BarBinder.cs` 106, `WidgetBindersTests.cs` 146, `UIModels.cs` 5). `UIModels.cs` entra como consequência direta: o XML doc de `BarModel.TrailFraction` volta a citar o `BarBinder`. Exceção: nenhuma (dentro do teto de ~500).
- Commit: `feat(ui): rastro atrasado da barra e tick do binder`

### UI6 — Módulo `SpellBook.GAS.UI` e fontes de atributo
- Escopo: `Runtime/GAS.UI/SpellBook.GAS.UI.asmdef` (novo; Core, Tags, Time, GAS, UI), `Runtime/GAS.UI/AttributeSources.cs` (novo), `Tests/GAS.UI/EditMode/SpellBook.GAS.UI.Tests.EditMode.asmdef`, `Tests/GAS.UI/EditMode/AttributeSourcePreviewTests.cs`, `Tests/GAS.UI/PlayMode/SpellBook.GAS.UI.Tests.PlayMode.asmdef`, `Tests/GAS.UI/PlayMode/AttributeSourcesTests.cs` (novos), `AsmdefRulesTests.cs` (`GAS.UI` nas listas; referências exatas; sem Bridges, uGUI e TMP; o GAS não referencia UI), `CHANGELOG.md`.
- Testes red: `Bar_VidaAndVidaMax_ByEvent_NoUpdate` (CA1, C1); `Bar_MaxFromLimitSource_FollowsAR1`; `Text_FormatAndInjectedCulture_NoMarkup` (CA2, C2); `TwoBars_VidaAndEscudo_Independent` (CA3, C3); `Preview_50Of100`; `GasUiAsmdef_ReferencesExactly_NoBridgesNoUgui` (CA12); `Updates10000_DoNotAllocate` (CA13, parte).
- Critério de pronto: CA1 a CA3 e CA12 verdes.
- Tamanho estimado: ~470 linhas; 8 arquivos. Depende de: UI5a, UI5b, DE7, AR1, EA1.

#### Detalhe (2026-11-04)
- Depende do UI5a (#239) e do UI5b (#333), ambos na branch do lote.
- CA: CA1 (C1), CA2 (C2), CA3 (C3), CA12 e CA13 (parte: 10 000 atualizações de atributo pelas fontes sem alocar). R8. ADR 0039, itens 1, 2 e 4; ADR 0038, item 1.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/GAS.UI/SpellBook.GAS.UI.asmdef` (novo): `SpellBook.GAS.UI`, referências Core, Tags, Time, GAS e UI (por GUID, como os outros), `autoReferenced`, sem restrição.
  - `Runtime/GAS.UI/AttributeSources.cs` (novo; `SpellBook.GAS.UI`): `enum AttributeTextMode { Current, CurrentOfMax }`; base `public abstract class AttributeSource<TModel> : UISource<TModel>` (nome do R8; construtor e `Build` `private protected`, então só as duas fontes do pacote derivam dela), com `AttributeDefinition current`, `AttributeDefinition max` (opcional), `AttributeTextMode textMode` e `LocalizedText separator` (padrão `" / "`, sem chave), expostos por propriedades `Current`, `Max`, `TextMode` e `Separator`, e `OwnerType` = `AbilitySystemComponent`; `AttributeBarSource : AttributeSource<BarModel>` (padrão `CurrentOfMax`) e `AttributeTextSource : AttributeSource<TextModel>` (padrão `Current`), `[Serializable] sealed`, não genéricas, com `[TypeCategory("Atributos", Summary = ...)]` para o seletor de tipos do campo de fonte (ADR 0013, item 4). A base junta o que seria a classe interna `AttributeReader` do design numa ligação privada: assina o `OnValueChanged` do estado atual e do estado do máximo (o `max` explícito ou, sem ele, o `MaxAttribute` da definição quando `MaxSource = Attribute`; o mesmo estado não é assinado duas vezes), lê o máximo do estado explícito ou o `MaxValue` efetivo (AR1) e escreve o texto num `TextBuffer` da ligação pelo `AttributeDisplay.Format(TextBuffer, ...)` do atributo atual (os dois números) com a cultura do contexto e o separador pelo provedor do contexto. Papel: `Display.Role` do atual. `Hidden` não muda nada. Dono que não é ASC, sem `AttributeSet`, sem o atributo atual ou sem o `max` explícito: publica o estado vazio. A configuração vale a partir do próximo `Attach`. Prévia: 50 de 100, no mesmo formato, num buffer da fonte criado na primeira prévia.
  - `Runtime/Core/Text/TextBuffer.cs:79` (consequência direta): `public TextSpan SpanFrom(int start)`, o trecho escrito de `start` até o fim (fora de 0..`Length`: `ArgumentOutOfRangeException`). Sem ele, a fonte não tem como ter o `TextSpan` do que o `AttributeDisplay.Format(TextBuffer, ...)` escreveu (o construtor de `TextSpan` é interno ao Core). API aditiva, nada quebra.
  - `Tests/Core/EditMode/Text/TextBufferTests.cs`: testes do `SpanFrom`.
  - `Tests/GAS.UI/EditMode/SpellBook.GAS.UI.Tests.EditMode.asmdef` e `Tests/GAS.UI/PlayMode/SpellBook.GAS.UI.Tests.PlayMode.asmdef` (novos; Core, Tags, Time, GAS, UI, GAS.UI, Testing e test runners).
  - `Tests/GAS.UI/EditMode/AttributeSourcePreviewTests.cs` e `Tests/GAS.UI/PlayMode/AttributeSourcesTests.cs` (novos): ASC pela `GasFactory`, binders `BarBinder`/`TextBinder` com view de teste e dono `Explicit`, sem driver (por evento).
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:20,37` (`GAS.UI` em `RuntimeModules` e `PlayModeModules`; não em `EditorModules`, que o UI11 cria) e o teste novo do CA12.
  - Fora deste ticket: Editor do GAS.UI (UI11), validação das fontes (UI14), `CHANGELOG.md` (nada quebra) e manual (passe de documentação).
- Testes red:
  - PlayMode (`SpellBook.GAS.UI.Tests.PlayMode`, `AttributeSourcesTests`): `Bar_VidaAndVidaMax_ByEvent_NoUpdate` (CA1, C1); `Bar_MaxFromLimitSource_FollowsAR1`; `Text_FormatAndInjectedCulture_NoMarkup` (CA2, C2); `TwoBars_VidaAndEscudo_Independent` (CA3, C3); `Bar_MissingAttribute_PublishesEmpty`; `Updates10000_DoNotAllocate` (CA13, parte).
  - EditMode (`SpellBook.GAS.UI.Tests.EditMode`, `AttributeSourcePreviewTests`): `Preview_50Of100`; `EveryConcreteUISource_HasTypeCategoryWithSummary` (toda fonte concreta de UI dos assemblies do pacote tem `[TypeCategory]` com `Summary`; a `TypeCatalogSummaryTests` não pega esse caso porque o catálogo pula o campo genérico de fonte do `UIBinder`).
  - EditMode (`SpellBook.Core.Tests.EditMode`): `AsmdefRulesTests.GasUiAsmdef_ReferencesExactly_NoBridgesNoUgui` (CA12: referências exatas; o assembly não compila com Bridges, uGUI nem TMP; o `SpellBook.GAS` não referencia UI); `TextBufferTests.SpanFrom_CoversWrittenSinceStart` e `SpanFrom_OutOfRange_Throws`.
- Critério de pronto: os testes acima verdes pelos filtros `\.AttributeSourcesTests\.`, `\.AttributeSourcePreviewTests\.`, `\.AsmdefRulesTests\.` e `\.TextBufferTests\.`; `HelpUrlTests` e `SpellBookEditorCoverageTests` verdes (as fontes não são `MonoBehaviour` nem `ScriptableObject`).
- Tamanho: ~647 linhas e 9 arquivos, pela ADR 0042 (medido: `AttributeSources.cs` 191, `AttributeSourcesTests.cs` 239, `AttributeSourcePreviewTests.cs` 68, `SpellBook.GAS.UI.asmdef` 20, `SpellBook.GAS.UI.Tests.EditMode.asmdef` 30, `SpellBook.GAS.UI.Tests.PlayMode.asmdef` 28, `AsmdefRulesTests.cs` 30, `TextBuffer.cs` 12, `TextBufferTests.cs` 29). Exceção: +29% sobre o teto de ~500 (dentro dos +30%), pelos três asmdefs que o módulo novo exige, pelo `TextBuffer.SpanFrom` com os testes dele, consequência direta do texto em `TextSpan`, e pela guarda do `[TypeCategory]` das fontes, pedida na revisão. A primeira versão, com as duas fontes separadas, deu ~697 (+39%); a base comum tirou a duplicação.
- Commit: `feat(gas-ui): módulo GAS.UI e fontes de barra e texto de atributo`

### UI7 — `TagSource` e páginas dos widgets de atributo e tag
- Escopo: `Runtime/GAS.UI/TagSource.cs` (novo), `Tests/GAS.UI/PlayMode/TagSourceTests.cs` (novo), `DocUrls.cs` (`UiAttributeBar`, `UiAttributeText`, `UiTagIndicator`), `DocUrlsTests.cs`, `Documentation~/manual/ui/barra-de-atributo.md`, `texto-de-atributo.md`, `indicador-de-tag.md` (novas), `CHANGELOG.md`.
- Testes red: `Tag_OnOffByEvent` (CA4, C4); `Query_AllAnyNone_ByEvent`; `Preview_On`; três casos de `DocUrls_PageConstantPointsToPageUnderBase`.
- Critério de pronto: CA4 verde; `ManualLinksTests` verde.
- Tamanho estimado: ~280 linhas; 8 arquivos. Depende de: UI6, AV2.

#### Detalhe (2026-11-04)
- CA: CA4 (C4: o indicador acende e apaga pela entrada e saída da tag, ou pela consulta, por evento). R9. ADR 0039, itens 1 e 2. As páginas da barra, do texto e do indicador ficam para o passe de documentação (abaixo).
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/GAS.UI/TagSource.cs` (novo; `SpellBook.GAS.UI`): `enum TagSourceMode { Tag, Query }` e `[Serializable, TypeCategory("Tags", Summary = ...)] public sealed class TagSource : UISource<TagIndicatorModel>`, não genérica, com `[SerializeField] TagSourceMode mode`, `GameplayTag tag` e `GameplayTagQuery query` (instância nova por padrão), propriedades `Mode`, `Tag` e `Query` (só leitura; edita-se pelas listas da consulta), e `OwnerType` = `AbilitySystemComponent`. A ligação privada cria o delegate uma vez, assina o `OnTagChanged` do ASC no `OnAttach`, publica o estado atual e, a cada mudança de tag, reavalia `HasTag(tag)` (modo `Tag`) ou `HasMatching(query)` (modo `Query`) e publica só quando o `Active` muda. Dono que não é ASC: estado vazio, sem assinar. `OnDetach` desassina. Prévia: aceso. O `[TypeCategory]` com `Summary` é exigido pela guarda do UI6 (ADR 0013, item 4; o catálogo pula o campo genérico de fonte).
  - `Tests/GAS.UI/PlayMode/TagSourceTests.cs` (novo): ASC e tags pela `GasFactory`, `TagIndicatorBinder` com view de teste e dono `Explicit`, sem driver (por evento).
  - `Tests/GAS.UI/EditMode/AttributeSourcePreviewTests.cs:58` (consequência direta): `typeof(TagSource)` no `SupersetOf` de `EveryConcreteUISource_HasTypeCategoryWithSummary`, para a guarda provar que acha a fonte nova.
  - `Tests/Core/EditMode/DocUrlsTests.cs:35`: três `TestCase` de `DocUrls_PageConstantPointsToPageUnderBase` (`UiAttributeBar`, `UiAttributeText`, `UiTagIndicator`). As constantes já existem desde o UI5a, então os três casos nascem verdes; entram como guarda do endereço.
  - Fora deste ticket, com o motivo: `Documentation~/manual/ui/barra-de-atributo.md`, `texto-de-atributo.md` e `indicador-de-tag.md`. Pela emenda de 2026-11-03 da 0007 (item 1 e a regra do `AGENTS.md`, "Manual, catálogo e referência da API só no passe de documentação"), nenhum ticket de código escreve em `Documentation~/manual/`; as páginas e as âncoras das constantes `DocUrls.Ui*` são do passe, como no UI4 e no UI5a. Por isso o `ManualLinksTests` (categoria `Documentacao`) sai do critério de pronto: ele só fica verde no passe. `CHANGELOG.md`: nada quebra.
- Testes red: PlayMode (`SpellBook.GAS.UI.Tests.PlayMode`, `TagSourceTests`): `Tag_OnOffByEvent` (CA4, C4: acende, apaga, outra tag não redesenha, tag filha acende o pai); `Query_AllAnyNone_ByEvent`; `Preview_On`; `Disabled_StopsListening`. EditMode (`SpellBook.Core.Tests.EditMode`, `DocUrlsTests`): os três casos novos de `DocUrls_PageConstantPointsToPageUnderBase` (verdes desde o início, ver acima).
- Critério de pronto: os 4 testes de `\.TagSourceTests\.` verdes; `\.DocUrlsTests\.` e `\.AttributeSourcePreviewTests\.` verdes; `\.AttributeSourcesTests\.` verde sem mudança.
- Tamanho: ~261 linhas e 4 arquivos, pela ADR 0042 (medido: `TagSource.cs` 104, `TagSourceTests.cs` 152, `DocUrlsTests.cs` 3, `AttributeSourcePreviewTests.cs` 2). Exceção: nenhuma.
- Commit: `feat(gas-ui): TagSource do indicador de tag`

### UI8 — Módulo `SpellBook.UI.UGUI`, `com.unity.ugui` e views de atributo e tag
- Escopo: `package.json` (`dependencies`), `Runtime/UI.UGUI/SpellBook.UI.UGUI.asmdef`, `BarView.cs`, `TextView.cs`, `TagIndicatorView.cs` (novos), `Tests/UI.UGUI/EditMode/SpellBook.UI.UGUI.Tests.EditMode.asmdef`, `Tests/UI.UGUI/EditMode/WidgetViewsTests.cs` (novos; Canvas criado no teste), `AsmdefRulesTests.cs` (`UI.UGUI`: Core, Time, UI, `UnityEngine.UI`, `Unity.TextMeshPro`), `CHANGELOG.md`.
- Testes red: `BarView_FillTrailTextColorByRole`; `TextView_WritesSpan_NoStringPerRender`; `TagIndicatorView_ShowsHides`; `PackageJson_DeclaresUgui200`; `UiAndGasUi_DoNotReferenceUgui` (CA12 completo).
- Critério de pronto: testes verdes. Risco registrado: se o TextMeshPro exigir os TMP Essentials no host para os testes, a Fábrica para e devolve (sem importar recursos por conta própria).
- Tamanho estimado: ~430 linhas; 9 arquivos. Depende de: UI7, ADR 0039 aceita, Q1.

#### Detalhe (2026-11-04)
- CA: CA12 (completo: com a uGUI no projeto, `SpellBook.UI` e `SpellBook.GAS.UI` continuam sem uGUI e sem TMP, nos asmdefs e nos assemblies compilados) e CA17 (parte: as views têm `[HelpURL]`). R11 (views padrão de barra, texto e indicador; prefabs no UI9), R12. ADR 0039, itens 3, 4 e 8; Q1 do gate.
- Versão da uGUI: `com.unity.ugui` 2.0.0 conferida no pacote embutido da 6000.3.13f1 do host (e da 6000.4.10f1, também instalada), com os asmdefs `UnityEngine.UI` (GUID `2bafac87e7f4b9b418d9448d219b01ab`) e `Unity.TextMeshPro` (GUID `6055be8ebefd69e48b49212b09b47b2f`) e `TMP_Text.SetCharArray(char[], int, int)`. A 6000.0 (o mínimo do `package.json`) não está instalada e o projeto não tem CI de testes: fica registrada como não verificada.
- Escopo (caminhos em `Packages/com.spellbook/`, salvo o lock):
  - `package.json:6`: `"dependencies": { "com.unity.ugui": "2.0.0" }`.
  - `../packages-lock.json` (consequência direta: a Unity grava a uGUI e as dependências dela ao resolver o pacote; gerado, sem edição à mão).
  - `Runtime/UI.UGUI/SpellBook.UI.UGUI.asmdef` (novo): Core, Time, UI, `UnityEngine.UI` e `Unity.TextMeshPro`, por GUID, `autoReferenced`, sem restrição.
  - `Runtime/UI.UGUI/BarView.cs` (novo; `SpellBook.UI.UGUI`, `[HelpURL(DocUrls.UiAttributeBar)]`): `IView<BarModel>` com `[SerializeField] Image fill`, `Image trail` (opcional), `TMP_Text label` (opcional) e `UITheme theme` (opcional). `Render`: `fill.fillAmount = Fraction`, `trail.fillAmount = TrailFraction`, texto pelo `SetCharArray` do trecho do buffer e cor do preenchimento pelo papel (`theme.GetColor`, ou `TextRoleTheme.Default` sem tema). Estado vazio: preenchimento e rastro em 0 e texto vazio.
  - `Runtime/UI.UGUI/TextView.cs` (novo; `[HelpURL(DocUrls.UiAttributeText)]`): `IView<TextModel>` com `TMP_Text text` e `UITheme theme`; texto pelo `SetCharArray` (sem `string` da view) e cor do texto pelo papel. Vazio: texto vazio.
  - `Runtime/UI.UGUI/TagIndicatorView.cs` (novo; `[HelpURL(DocUrls.UiTagIndicator)]`): `IView<TagIndicatorModel>` com `GameObject target`; mostra com `Active` e esconde apagado ou vazio (`SetActive` só quando muda).
  - Decisões do detalhe, aditivas e sem mudar CA: (1) a view tem o próprio campo `UITheme` (a view não busca componente nem binder; ADR 0039, "não pode"); (2) na barra, o papel pinta o preenchimento, e o texto da barra fica com a cor do próprio `TMP_Text` (texto da mesma cor do preenchimento sumiria); o `TextView` pinta o texto; (3) sem setters públicos de componentes neste ticket: os prefabs do UI9 ligam os campos, e os testes usam `SerializedObject`.
  - `Tests/UI.UGUI/EditMode/SpellBook.UI.UGUI.Tests.EditMode.asmdef` (novo): Core, Time, UI, UI.UGUI, Testing, `UnityEngine.UI`, `Unity.TextMeshPro` e os test runners.
  - `Tests/UI.UGUI/EditMode/WidgetViewsTests.cs` (novo): Canvas, `Image` e `TextMeshProUGUI` criados no teste, tema por `ScriptableObject.CreateInstance`, e um `TextMeshProUGUI` de teste que conta as `string` recebidas pelo setter `text`.
  - `Assets/TextMesh Pro/` (projeto host, nunca em `Packages/com.spellbook/`): os TMP Essentials importados do `TMP Essential Resources.unitypackage` da uGUI 2.0.0 (decisão do usuário, 2026-11-04, opção (a), depois da parada pelo risco abaixo: sem o `TMP Settings`, o `TMP_Text.SetCharArray` lança `NullReferenceException` em `TMP_Settings.defaultStyleSheet`). São 82 arquivos (37 assets e 45 `.meta`, contando o `Assets/TextMesh Pro.meta`), saída de ferramenta externa: não contam no teto e vão para o commit com os `.meta`.
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:20`: `"UI.UGUI"` em `RuntimeModules` (o `AllExpectedAssembliesExist` e o `EachRuntimeModuleHasEditModeTests` passam a exigir o módulo e o asmdef de teste EditMode); não em `EditorModules` (UI9) nem em `PlayModeModules`. Testes novos `UGuiAsmdef_ReferencesExactly`, `UiAndGasUi_DoNotReferenceUgui` e `PackageJson_DeclaresUgui200`.
  - Fora deste ticket, com o motivo: `CHANGELOG.md` (nada quebra: dependência nova é aditiva; o `AGENTS.md` põe no CHANGELOG só a quebra com a migração) e manual (só no passe de documentação, emenda de 2026-11-03 da 0007; o pedido dos TMP Essentials vai para a página da barra no passe). `SpellBookEditorCoverageTests` e o Editor do módulo ficam no UI9.
- Testes red: EditMode (`SpellBook.UI.UGUI.Tests.EditMode`, `WidgetViewsTests`): `BarView_FillTrailTextColorByRole`; `BarView_Empty_ZeroFillAndEmptyText`; `TextView_WritesSpan_NoStringPerRender` (o texto sai do trecho no meio do buffer, e a view não entrega `string` ao TMP: o setter `text` do `TextMeshProUGUI` de teste não é chamado. A primeira versão contava alocações, mas no Editor o próprio `SetCharArray` cria a `string` do `m_text`, e a mutação `text = buffer.ToString(span)` passava; a versão final reprova essa mutação nos dois testes de texto); `TagIndicatorView_ShowsHides`. EditMode (`SpellBook.Core.Tests.EditMode`, `AsmdefRulesTests`): `UGuiAsmdef_ReferencesExactly`; `UiAndGasUi_DoNotReferenceUgui` (CA12 completo: falha sem a uGUI carregada, para a guarda não passar vazia); `PackageJson_DeclaresUgui200`.
- Critério de pronto: os 7 testes verdes pelos filtros `\.WidgetViewsTests\.` e `\.AsmdefRulesTests\.`; `HelpUrlTests` verde. Risco do design doc: se o TMP exigir os TMP Essentials no host para os testes, paro e devolvo.
- Tamanho: ~441 linhas e 9 arquivos, pela ADR 0042 (medido: `package.json` 3, `packages-lock.json` 19, `SpellBook.UI.UGUI.asmdef` 20, `BarView.cs` 53, `TextView.cs` 60, `TagIndicatorView.cs` 26, asmdef de teste 30, `WidgetViewsTests.cs` 173, `AsmdefRulesTests.cs` 57). Os 82 arquivos dos TMP Essentials ficam fora da conta (acima). Exceção: nenhuma.
- Commit: `feat(ui-ugui): módulo UI.UGUI, uGUI declarada e views de barra, texto e indicador`

### UI9 — Editor do UI.UGUI e prefabs dos widgets
- Escopo: `Editor/UI.UGUI/SpellBook.UI.UGUI.Editor.asmdef`, `Editor/UI.UGUI/UGUIEditors.cs` (novos), `Runtime/UI.UGUI/Prefabs/AttributeBar.prefab`, `AttributeText.prefab`, `TagIndicator.prefab` (fixtures), `Tests/UI.UGUI/EditMode/WidgetPrefabsTests.cs` (novo: cada prefab tem binder e view ligados), `AsmdefRulesTests.cs` (`UI.UGUI` em `EditorModules`), `SpellBookEditorCoverageTests.cs` (+`SpellBook.UI.UGUI`), `barra-de-atributo.md` (prefab e receita de barra sobre a cabeça), `CHANGELOG.md`.
- Testes red: `Prefabs_HaveBinderAndView_Wired`; cobertura de Inspector verde.
- Critério de pronto: CA10 e CA14 (S) no host: barra em 50 % e "50 / 100" sem Play, prévia some no Play.
- Tamanho estimado: ~240 linhas; 10 arquivos (3 fixtures). Depende de: UI8.

#### Detalhe (2026-11-04)
- CA: CA10 (S; a parte automática: o prefab da barra, aberto em edição, mostra 50 % e "50 / 100" pela prévia da fonte; "some no Play" já é coberto pelos testes de prévia do UI3) e CA14 (S; parte: as views do UI.UGUI ganham Inspector do pacote; o Inspector do binder com seletor, resolvedor e validação é do UI13). CA17 (parte: os prefabs entregam as views do R11). R11 (prefab pronto de cada view). ADR 0039, itens 3, 4 e 8; ADR 0013 (sem tipo novo no seletor de tipos neste ticket).
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Editor/UI.UGUI/SpellBook.UI.UGUI.Editor.asmdef` (novo): Core, Time, UI, UI.UGUI, Core.Editor, `UnityEngine.UI` e `Unity.TextMeshPro` (ADR 0039, item 9), por GUID, só Editor, `autoReferenced`.
  - `Editor/UI.UGUI/UGUIEditors.cs` (novo; `SpellBook.UI.UGUI.Editor`): `BarViewEditor`, `TextViewEditor` e `TagIndicatorViewEditor`, de uma linha, derivados de `SpellBookEditor` (padrão do `Editor/UI/UIEditors.cs`).
  - `Runtime/UI.UGUI/Prefabs/AttributeBar.prefab` (novo): raiz `RectTransform` com `BarBinder` e `BarView`; filhos `Background` (`Image`), `Trail` e `Fill` (`Image` Filled horizontal, sprite embutido `UI/Skin/UISprite.psd`) e `Label` (`TextMeshProUGUI`, fonte nula). Fonte do binder: `AttributeBarSource` (texto `CurrentOfMax`) sem atributo.
  - `Runtime/UI.UGUI/Prefabs/AttributeText.prefab` (novo): um objeto com `TextMeshProUGUI` (fonte nula), `TextView` e `TextBinder`; fonte `AttributeTextSource` sem atributo.
  - `Runtime/UI.UGUI/Prefabs/TagIndicator.prefab` (novo): raiz com `TagIndicatorBinder` e `TagIndicatorView`; filho `Icon` (`Image`) como alvo; fonte `TagSource` vazia.
  - Os três prefabs são autorados pela Unity em batchmode por um script descartável em `Assets/__SpellBookTestTemp_UI9/Editor/` (pasta já ignorada), apagado no fim com os `.meta`; nenhum YAML à mão (ADR 0028, por analogia; não há `Samples`, então nem renomeação nem `Samples.meta`). Tema e driver vazios (padrões do pacote e o driver do pai); dono `SelfOrParent`.
  - `Tests/UI.UGUI/EditMode/WidgetPrefabsTests.cs` (novo) e `Tests/UI.UGUI/EditMode/SpellBook.UI.UGUI.Tests.EditMode.asmdef` (+`SpellBook.GAS.UI`, para conferir o tipo da fonte; o asmdef de teste do UI.UGUI fica fora da guarda do CA12, que exclui `SpellBook.UI.UGUI*`).
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:34-37`: `"UI.UGUI"` em `EditorModules`; teste novo `UGuiEditorAsmdef_References` junto do `UIEditorAsmdef_References` (`:510`).
  - `Tests/Core/EditMode/Inspector/SpellBookEditorCoverageTests.cs:20-21`: `+"SpellBook.UI.UGUI"`.
  - Decisões do detalhe, aditivas e sem mudar CA: (1) os prefabs saem com as fontes do GAS.UI já escolhidas e sem atributo ou tag (o PRD pede "arrastar um prefab de barra, escolher atributo e dono, ver a prévia", e o C10 pede "50 / 100" sem Play, que só a `AttributeBarSource` dá). É referência de asset por `[SerializeReference]`, não de assembly: o `SpellBook.UI.UGUI` continua sem referência ao GAS (CA12 intacto); (2) o texto dos prefabs tem `m_fontAsset` nulo (ADR 0039: cai na fonte padrão do `TMP_Settings`), conferido pelo teste.
  - Fora deste ticket, com o motivo: `barra-de-atributo.md` (prefab e receita da barra sobre a cabeça) só no passe de documentação (emenda de 2026-11-03 da 0007); `CHANGELOG.md` (nada quebra; o `AGENTS.md` põe no CHANGELOG só a quebra com a migração).
- Testes red: EditMode (`SpellBook.UI.UGUI.Tests.EditMode`, `WidgetPrefabsTests`): `Prefabs_HaveBinderAndView_Wired` (os três: binder na raiz, `View` é a view do próprio prefab, campos da view ligados, fonte do tipo esperado, texto com fonte nula); `AttributeBarPrefab_InEditMode_ShowsPreviewHalfAnd50Of100` (CA10, parte automática). EditMode (`SpellBook.Core.Tests.EditMode`): `SpellBookEditorCoverageTests.EveryPublicConcreteMbAndSo_InRuntimeAssemblies_UsesSpellBookEditor` (cobertura de Inspector com `SpellBook.UI.UGUI`), `AsmdefRulesTests.UGuiEditorAsmdef_References` e `AsmdefRulesTests.AllExpectedAssembliesExist`.
- Critério de pronto: os testes acima verdes pelos filtros `\.WidgetPrefabsTests\.`, `\.SpellBookEditorCoverageTests\.` e `\.AsmdefRulesTests\.`, com o `total` do XML conferido. Conferência manual no host (S), pendência para o usuário: abrir os três prefabs e ver a barra em 50 % com "50 / 100" sem Play e a prévia desligar no Play (CA10), e o Inspector das views (CA14, parte).
- Tamanho: ~178 linhas e 9 arquivos, pela ADR 0042 (medido: asmdef do Editor 24, `UGUIEditors.cs` 18, `WidgetPrefabsTests.cs` 115, asmdef de teste 1, `AsmdefRulesTests.cs` 18, `SpellBookEditorCoverageTests.cs` 2; os três `.prefab` contam só nos arquivos). Exceção: nenhuma.
- Commit: `feat(ui-ugui): Editor do UI.UGUI e prefabs dos widgets`

### UI10 — Números flutuantes: estilo, pedido e binder com pool
- Escopo: `Runtime/UI/FloatingTextStyle.cs`, `Runtime/UI/FloatingTextBinder.cs` (novos), `UIModels.cs` (`FloatingTextRequest`, `IFloatingTextView`), `UIEditors.cs`, `Tests/UI/PlayMode/FloatingTextBinderTests.cs` (novo; spawner de teste que conta vivos), `DocUrls.cs` (`UiFloatingText`), `DocUrlsTests.cs`, `Documentation~/manual/ui/numeros-flutuantes.md` (nova), `CHANGELOG.md`.
- Testes red: `Publish_SpawnsThroughSpawner_ReleasedAtEnd_LiveCountStable` (CA8, parte); `NullSpawn_SkipsWithWarning_NoException`; `PausedTime_NumberStays`; `Numbers1000_DoNotAllocate` (CA13, parte).
- Critério de pronto: partes do CA8 e do CA13 verdes.
- Tamanho estimado: ~450 linhas; 9 arquivos. Depende de: UI9, UI5b.

#### Detalhe (2026-11-04)
- CA: CA8 (parte da UI: o número nasce pelo `SpawnerAsset`, volta ao fim da duração e a contagem de vivos não cresce; dano, cura e "bloqueado" são do UI11) e CA13 (parte: 1000 números em pool sem alocar depois do aquecimento). R13 (estilo), R14 (spawner: só `SpawnerAsset`, tempo do `ITimeSource` pelo driver, `null` pula com aviso, sem `Instantiate`, `Destroy` nem corrotina). ADR 0039, itens 1 a 4; ADR 0014; ADR 0015.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/UI/FloatingTextStyle.cs` (novo; `ScriptableObject`, `[CreateAssetMenu("SpellBook/UI/Floating Text Style")]`, `[HelpURL(DocUrls.UiFloatingText)]`): `TextRole role`, `float duration` (`Min(0)`, padrão 1 s), `Vector2 rise`, `Vector2 randomOffset` e `LocalizedText format`, só por propriedades de leitura. Constante `DefaultDuration` (1 s), usada pelo binder quando o pedido vem sem estilo.
  - `Runtime/UI/UIModels.cs:102`: `readonly struct FloatingTextRequest` (`WorldPosition`, `Text`, `Role`, `Style`, `IsEmpty` verdadeiro só no `default`, como os outros modelos) e `interface IFloatingTextView` (`Show(in FloatingTextRequest)`, `SetProgress(float)`).
  - `Runtime/UI/FloatingTextBinder.cs` (novo; `sealed`, `UIBinder<FloatingTextRequest>`, `[HelpURL(DocUrls.UiFloatingText)]`): `[SerializeField] GameObject prefab`, `SpawnerAsset spawner` (vazio: `SpawnerAsset.Default`, ADR 0015) e `Transform container` (vazio: o próprio transform), com propriedades de leitura. Sobrescreve `OnModel`: fora do Play ou com o pedido vazio (estado vazio e prévia), nada nasce; sem prefab, sem driver ativo (o `RequestBinderTick(true)` do UI5b devolve `false`) ou com o `Spawn` devolvendo `null`, pula o número e avisa uma vez por motivo, só com `Debug.isDebugBuild`, sem exceção. Senão, cria pelo spawner na posição do pedido, sob o container, lê o `IFloatingTextView` só na primeira vez que vê a instância (cache em listas paralelas `_known`/`_knownViews` com capacidade inicial; o pool devolve as mesmas instâncias) e chama `Show`. O `OnBinderTick` marca o início no primeiro tick depois do nascimento (o tempo só vem do driver, como o rastro do UI5b), chama `SetProgress(0..1)` pela duração do estilo e devolve ao spawner no fim (remoção por troca com o último, sem alocar); sem números vivos, solta o tick. Ao desligar a fonte (desabilitar, religar, troca de âncora), sobrescreve `DetachBinding` e devolve todos os vivos ao spawner, para nenhum ficar sem tick. Instância sem `IFloatingTextView`: devolve na hora e avisa uma vez.
  - `Runtime/Core/DocUrls.cs:29`: `UiFloatingText = Base + "ui/numeros-flutuantes/"`. Consequência direta: o `HelpUrlTests` exige o `[HelpURL]` nos dois tipos concretos.
  - `Tests/Core/EditMode/DocUrlsTests.cs:37`: `TestCase("UiFloatingText", "ui/numeros-flutuantes/")`.
  - `Editor/UI/UIEditors.cs:26`: `FloatingTextStyleEditor` e `FloatingTextBinderEditor`, uma linha cada (cobertura de Inspector).
  - `Tests/UI/PlayMode/FloatingTextBinderTests.cs` (novo): `RecordingSpawnerAsset` do `SpellBook.Testing` (conta criações, devoluções e vivos), dono com evento de teste, fonte, ligação e view de número de teste no próprio arquivo, driver com `ManualTimeSource`; campos privados pelo `SerializedObject` (`#if UNITY_EDITOR`, como `AnchorRegistrarTests`).
  - Decisões do detalhe, aditivas e sem mudar CA: (1) `FloatingTextRequest.IsEmpty`, para o binder distinguir o estado vazio (`default`) de um pedido; (2) sem driver, o número é pulado com aviso, porque sem tick ele nunca voltaria ao pool; (3) a subida, o deslocamento aleatório e a conversão de posição são da view (UI12): o binder entrega o pedido e o progresso; (4) sem setters públicos novos além do que o `UIBinder<T>` já tem: o Inspector e os prefabs ligam os campos, e os testes usam `SerializedObject`.
  - Fora deste ticket, com o motivo: `Documentation~/manual/ui/numeros-flutuantes.md` (manual só no passe de documentação, emenda de 2026-11-03 da 0007; por isso o `ManualLinksTests`, categoria `Documentacao`, fica para o passe) e `CHANGELOG.md` (nada quebra). Validação do binder (sem prefab, prefab sem view): UI13. `TypeCategory`: nenhum tipo novo vai ao seletor de tipos (o estilo é asset e o binder é componente).
- Testes red: PlayMode (`SpellBook.UI.Tests.PlayMode`, `FloatingTextBinderTests`): `Publish_SpawnsThroughSpawner_ReleasedAtEnd_LiveCountStable` (CA8, parte: `Show` recebe o pedido, progresso pelo tempo, devolução no fim, instâncias reaproveitadas e vivos sem crescer); `NullSpawn_SkipsWithWarning_NoException` (aviso uma vez); `NoDriver_SkipsWithWarning`; `PausedTime_NumberStays`; `Disable_ReleasesLiveNumbers`; `Numbers1000_DoNotAllocate` (CA13, parte). EditMode (`SpellBook.Core.Tests.EditMode`, `DocUrlsTests`): o caso novo de `DocUrls_PageConstantPointsToPageUnderBase`.
- Critério de pronto: os testes verdes pelos filtros `\.FloatingTextBinderTests\.` e `\.DocUrlsTests\.`, com o `total` do XML conferido; `HelpUrlTests`, `SpellBookEditorCoverageTests` e `WidgetBindersTests` verdes.
- Tamanho: ~638 linhas e 7 arquivos, pela ADR 0042 (medido: `FloatingTextBinder.cs` 217, `FloatingTextStyle.cs` 54, `UIModels.cs` 47, `UIEditors.cs` 8, `DocUrls.cs` 1, `DocUrlsTests.cs` 1, `FloatingTextBinderTests.cs` 310; a estimativa era ~560). Exceção: +28% sobre o teto de ~500 linhas (dentro dos +30%). Motivo: o binder é API pública com documentação XML e cache de views que não cresce com o spawner padrão (que destrói), e os dublês de teste (dono, fonte, ligação e view) ficam no arquivo de teste, como nos outros tickets do módulo; dividir separaria o nascimento da devolução, que o CA8 só prova juntos.
- Commit: `feat(ui): números flutuantes com estilo, pedido e binder com pool`

### UI11 — `FloatingTextSource`, regras e Editor do GAS.UI
- Escopo: `Runtime/GAS.UI/FloatingTextRules.cs`, `FloatingTextSource.cs` (novos), `Editor/GAS.UI/SpellBook.GAS.UI.Editor.asmdef`, `Editor/GAS.UI/GasUIEditors.cs` (novos), `Tests/GAS.UI/PlayMode/FloatingTextSourceTests.cs` (novo), `AsmdefRulesTests.cs` (`GAS.UI` em `EditorModules`), `SpellBookEditorCoverageTests.cs` (+`SpellBook.GAS.UI`), `numeros-flutuantes.md`, `CHANGELOG.md`.
- Testes red: `Damage30_Minus30_DamageStyle_Heal20_Plus20_HealStyle_Blocked_Immune` (CA8, C8); `RuleAsset_StyleChange_AffectsNext` (CA9, C9); `DamageTagRule_FireStyle` (P2); `ZeroFinal_FromImmunity_BlockedStyle`; `NoBridgesNoRelay` (guarda textual).
- Critério de pronto: CA8 e CA9 verdes.
- Tamanho estimado: ~470 linhas; 9 arquivos. Depende de: UI10, DM2, RL1.

### UI12 — View e prefab do número flutuante
- Escopo: `Runtime/UI.UGUI/FloatingTextView.cs` (novo), `Runtime/UI.UGUI/Prefabs/FloatingText.prefab` (fixture), `UGUIEditors.cs`, `Tests/UI.UGUI/EditMode/FloatingTextViewTests.cs` (novo), `numeros-flutuantes.md`, `CHANGELOG.md`.
- Testes red: `Show_PlacesAtWorldPoint_ColorByRole`; `Progress_RisesAndFades`.
- Critério de pronto: testes verdes; número visto no host (S).
- Tamanho estimado: ~250 linhas; 6 arquivos (1 fixture). Depende de: UI11.

### UI13 — Inspector do binder e validação da UI
- Escopo: `Editor/UI/UIBinderEditor.cs` (novo), `UIEditors.cs` (binders passam a derivar dele), `UIBinder.cs`, `ActorResolver.cs`, `FloatingTextBinder.cs` (`Validate`), `Tests/UI/EditMode/UIBinderValidationTests.cs`, `Tests/UI/EditMode/UIBinderEditorTests.cs` (novos), `manual/ferramentas/validador.md`, `visao-geral.md`, `CHANGELOG.md`.
- Testes red: um por regra do lado da UI no Design 8, com o objeto clicável (CA16, parte); `Editor_ShowsPickerResolverViewAndSummary`; `Editor_ConfigChange_RedrawsPreview`.
- Critério de pronto: parte da UI do CA16 verde; CA14 (S) completo.
- Tamanho estimado: ~430 linhas; 10 arquivos. Depende de: UI12, AV1.

### UI14 — Validação das fontes do GAS e fechamento da documentação
- Escopo: `AttributeSources.cs`, `TagSource.cs`, `FloatingTextRules.cs`, `FloatingTextSource.cs` (`Validate`), `Tests/GAS.UI/EditMode/GasUIValidationTests.cs` (novo), `Tests/GAS.UI/EditMode/GasUiSourceScanTests.cs` (novo: nenhum `.cs` de `Runtime/UI*` e `Runtime/GAS.UI` com `FindObjectOfType`, `Instantiate`, `Destroy`, `Time.time`, `StartCoroutine` ou estático mutável; CA15), `validador.md`, `visao-geral.md` (fonte e view próprias), `CHANGELOG.md` (R18 completo).
- Testes red: um por regra do lado do GAS no Design 8 (CA16 completo); a guarda do CA15 (red forçado).
- Critério de pronto: CA15, CA16 e CA17 verdes.
- Tamanho estimado: ~380 linhas; 9 arquivos. Depende de: UI13.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| UI1 | sim | CU1 | ~360 / 10 |
| UI2 | sim | UI1, DE1 | ~450 / 10 |
| UI3 | sim | UI2 | ~470 / 7 |
| UI4 | sim | UI3, AR4 | ~420 / 10 |
| UI5a (#239) | sim | UI4 | ~447 / 7 |
| UI5b (#333) | sim | UI5a | ~290 / 3 |
| UI6 | sim | UI5a, UI5b, DE7, AR1, EA1 | ~470 / 8 |
| UI7 | sim | UI6, AV2 | ~280 / 8 |
| UI8 | sim | UI7, ADR 0039, Q1 | ~430 / 9 |
| UI9 | sim | UI8 | ~240 / 10 |
| UI10 | sim | UI9, UI5b | ~450 / 9 |
| UI11 | sim | UI10, DM2, RL1 | ~470 / 9 |
| UI12 | sim | UI11 | ~250 / 6 |
| UI13 | sim | UI12, AV1 | ~430 / 10 |
| UI14 | sim | UI13 | ~380 / 9 |

Fila: UI1 → UI14 em série (UI5a antes do UI5b), depois do DE7 (o B-015 não espera o resto do B-008). O B-016 começa depois do UI14 (usa o Editor do GAS.UI, criado no UI11, e o Inspector de binder do UI13). CA12 e CA15 rodam em todos.

## Perguntas ao usuário

- **Q1. `com.unity.ugui` no `package.json`.** Proposta: declarar `"com.unity.ugui": "2.0.0"` (uGUI e TextMeshPro, embutidos na Unity 6), dependência nova de todo projeto que instala o SpellBook. Alternativa: deixar o `SpellBook.UI.UGUI` opcional, compilado só com a uGUI presente (`versionDefines`, como o adaptador do Localization), sem dependência declarada; quem quiser os widgets instala a uGUI.
- As ADRs 0038 e 0039 passam pelo gate junto com esta Spec.

## Respostas do gate (2026-11-01)

Spec aprovada no gate do Lote E, com as ADRs 0038 e 0039 (Aceitas).
- Q1: aprovada como proposta. O UI8 declara `"com.unity.ugui": "2.0.0"` nas `dependencies` do `package.json`.
- As respostas Q2 a Q6 do mesmo gate estão nas Specs do B-008, do B-016 e do B-018.
