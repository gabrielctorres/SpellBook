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

### UI4 — `UIUpdateDriver`, `UITheme` e Editor do módulo
- Escopo: `Runtime/UI/UIUpdateDriver.cs`, `Runtime/UI/UITheme.cs` (novos), `UIBinder.cs` (registro no driver), `Editor/UI/SpellBook.UI.Editor.asmdef`, `Editor/UI/UIEditors.cs` (novos; uma linha por tipo), `Tests/UI/EditMode/UIUpdateDriverTests.cs` (novo), `AsmdefRulesTests.cs` (`UI` em `EditorModules`), `SpellBookEditorCoverageTests.cs` (+`SpellBook.UI`), `visao-geral.md`, `CHANGELOG.md`.
- Testes red: `NoRequester_DriverDisabled_TickDoesNothing` (C7); `TicksOnlyRequesters`; `PausedSource_NoTimeAdvance` (CA7); `UnregisterDuringTick_Safe`; `Theme_NullRoles_UsesDefault_NullText_Invariant`; `Tick10000_DoesNotAllocate` (CA13, parte).
- Critério de pronto: CA7 verde.
- Tamanho estimado: ~420 linhas; 10 arquivos. Depende de: UI3, AR4.

### UI5 — Modelos e binders de barra, texto e indicador
- Escopo: `Runtime/UI/UIModels.cs`, `BarBinder.cs`, `TextBinder.cs`, `TagIndicatorBinder.cs` (novos), `UIEditors.cs`, `Tests/UI/PlayMode/WidgetBindersTests.cs` (novo), `visao-geral.md`, `CHANGELOG.md`.
- Testes red: `Bar_Fraction_ZeroMax_Zero`; `Bar_TrailHoldsThenFalls_TickOnlyWhileAbove`; `TwoBars_Independent_NoShieldField` (CA3, com fontes de teste); `ModelUpdates10000_DoNotAllocate` (CA13, parte).
- Critério de pronto: CA3 verde (lado da UI).
- Tamanho estimado: ~380 linhas; 8 arquivos. Depende de: UI4.

### UI6 — Módulo `SpellBook.GAS.UI` e fontes de atributo
- Escopo: `Runtime/GAS.UI/SpellBook.GAS.UI.asmdef` (novo; Core, Tags, Time, GAS, UI), `Runtime/GAS.UI/AttributeSources.cs` (novo), `Tests/GAS.UI/EditMode/SpellBook.GAS.UI.Tests.EditMode.asmdef`, `Tests/GAS.UI/EditMode/AttributeSourcePreviewTests.cs`, `Tests/GAS.UI/PlayMode/SpellBook.GAS.UI.Tests.PlayMode.asmdef`, `Tests/GAS.UI/PlayMode/AttributeSourcesTests.cs` (novos), `AsmdefRulesTests.cs` (`GAS.UI` nas listas; referências exatas; sem Bridges, uGUI e TMP; o GAS não referencia UI), `CHANGELOG.md`.
- Testes red: `Bar_VidaAndVidaMax_ByEvent_NoUpdate` (CA1, C1); `Bar_MaxFromLimitSource_FollowsAR1`; `Text_FormatAndInjectedCulture_NoMarkup` (CA2, C2); `TwoBars_VidaAndEscudo_Independent` (CA3, C3); `Preview_50Of100`; `GasUiAsmdef_ReferencesExactly_NoBridgesNoUgui` (CA12); `Updates10000_DoNotAllocate` (CA13, parte).
- Critério de pronto: CA1 a CA3 e CA12 verdes.
- Tamanho estimado: ~470 linhas; 8 arquivos. Depende de: UI5, DE7, AR1, EA1.

### UI7 — `TagSource` e páginas dos widgets de atributo e tag
- Escopo: `Runtime/GAS.UI/TagSource.cs` (novo), `Tests/GAS.UI/PlayMode/TagSourceTests.cs` (novo), `DocUrls.cs` (`UiAttributeBar`, `UiAttributeText`, `UiTagIndicator`), `DocUrlsTests.cs`, `Documentation~/manual/ui/barra-de-atributo.md`, `texto-de-atributo.md`, `indicador-de-tag.md` (novas), `CHANGELOG.md`.
- Testes red: `Tag_OnOffByEvent` (CA4, C4); `Query_AllAnyNone_ByEvent`; `Preview_On`; três casos de `DocUrls_PageConstantPointsToPageUnderBase`.
- Critério de pronto: CA4 verde; `ManualLinksTests` verde.
- Tamanho estimado: ~280 linhas; 8 arquivos. Depende de: UI6, AV2.

### UI8 — Módulo `SpellBook.UI.UGUI`, `com.unity.ugui` e views de atributo e tag
- Escopo: `package.json` (`dependencies`), `Runtime/UI.UGUI/SpellBook.UI.UGUI.asmdef`, `BarView.cs`, `TextView.cs`, `TagIndicatorView.cs` (novos), `Tests/UI.UGUI/EditMode/SpellBook.UI.UGUI.Tests.EditMode.asmdef`, `Tests/UI.UGUI/EditMode/WidgetViewsTests.cs` (novos; Canvas criado no teste), `AsmdefRulesTests.cs` (`UI.UGUI`: Core, Time, UI, `UnityEngine.UI`, `Unity.TextMeshPro`), `CHANGELOG.md`.
- Testes red: `BarView_FillTrailTextColorByRole`; `TextView_WritesSpan_NoStringPerRender`; `TagIndicatorView_ShowsHides`; `PackageJson_DeclaresUgui200`; `UiAndGasUi_DoNotReferenceUgui` (CA12 completo).
- Critério de pronto: testes verdes. Risco registrado: se o TextMeshPro exigir os TMP Essentials no host para os testes, a Fábrica para e devolve (sem importar recursos por conta própria).
- Tamanho estimado: ~430 linhas; 9 arquivos. Depende de: UI7, ADR 0039 aceita, Q1.

### UI9 — Editor do UI.UGUI e prefabs dos widgets
- Escopo: `Editor/UI.UGUI/SpellBook.UI.UGUI.Editor.asmdef`, `Editor/UI.UGUI/UGUIEditors.cs` (novos), `Runtime/UI.UGUI/Prefabs/AttributeBar.prefab`, `AttributeText.prefab`, `TagIndicator.prefab` (fixtures), `Tests/UI.UGUI/EditMode/WidgetPrefabsTests.cs` (novo: cada prefab tem binder e view ligados), `AsmdefRulesTests.cs` (`UI.UGUI` em `EditorModules`), `SpellBookEditorCoverageTests.cs` (+`SpellBook.UI.UGUI`), `barra-de-atributo.md` (prefab e receita de barra sobre a cabeça), `CHANGELOG.md`.
- Testes red: `Prefabs_HaveBinderAndView_Wired`; cobertura de Inspector verde.
- Critério de pronto: CA10 e CA14 (S) no host: barra em 50 % e "50 / 100" sem Play, prévia some no Play.
- Tamanho estimado: ~240 linhas; 10 arquivos (3 fixtures). Depende de: UI8.

### UI10 — Números flutuantes: estilo, pedido e binder com pool
- Escopo: `Runtime/UI/FloatingTextStyle.cs`, `Runtime/UI/FloatingTextBinder.cs` (novos), `UIModels.cs` (`FloatingTextRequest`, `IFloatingTextView`), `UIEditors.cs`, `Tests/UI/PlayMode/FloatingTextBinderTests.cs` (novo; spawner de teste que conta vivos), `DocUrls.cs` (`UiFloatingText`), `DocUrlsTests.cs`, `Documentation~/manual/ui/numeros-flutuantes.md` (nova), `CHANGELOG.md`.
- Testes red: `Publish_SpawnsThroughSpawner_ReleasedAtEnd_LiveCountStable` (CA8, parte); `NullSpawn_SkipsWithWarning_NoException`; `PausedTime_NumberStays`; `Numbers1000_DoNotAllocate` (CA13, parte).
- Critério de pronto: partes do CA8 e do CA13 verdes.
- Tamanho estimado: ~450 linhas; 9 arquivos. Depende de: UI9.

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
| UI5 | sim | UI4 | ~380 / 8 |
| UI6 | sim | UI5, DE7, AR1, EA1 | ~470 / 8 |
| UI7 | sim | UI6, AV2 | ~280 / 8 |
| UI8 | sim | UI7, ADR 0039, Q1 | ~430 / 9 |
| UI9 | sim | UI8 | ~240 / 10 |
| UI10 | sim | UI9 | ~450 / 9 |
| UI11 | sim | UI10, DM2, RL1 | ~470 / 9 |
| UI12 | sim | UI11 | ~250 / 6 |
| UI13 | sim | UI12, AV1 | ~430 / 10 |
| UI14 | sim | UI13 | ~380 / 9 |

Fila: UI1 → UI14 em série, depois do DE7 (o B-015 não espera o resto do B-008). O B-016 começa depois do UI14 (usa o Editor do GAS.UI, criado no UI11, e o Inspector de binder do UI13). CA12 e CA15 rodam em todos.

## Perguntas ao usuário

- **Q1. `com.unity.ugui` no `package.json`.** Proposta: declarar `"com.unity.ugui": "2.0.0"` (uGUI e TextMeshPro, embutidos na Unity 6), dependência nova de todo projeto que instala o SpellBook. Alternativa: deixar o `SpellBook.UI.UGUI` opcional, compilado só com a uGUI presente (`versionDefines`, como o adaptador do Localization), sem dependência declarada; quem quiser os widgets instala a uGUI.
- As ADRs 0038 e 0039 passam pelo gate junto com esta Spec.

## Respostas do gate (2026-11-01)

Spec aprovada no gate do Lote E, com as ADRs 0038 e 0039 (Aceitas).
- Q1: aprovada como proposta. O UI8 declara `"com.unity.ugui": "2.0.0"` nas `dependencies` do `package.json`.
- As respostas Q2 a Q6 do mesmo gate estão nas Specs do B-008, do B-016 e do B-018.
