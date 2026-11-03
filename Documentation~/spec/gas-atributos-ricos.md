# Spec: Atributos ricos
PRD: [Documentation~/prd/gas-atributos-ricos.md](../prd/gas-atributos-ricos.md) · Backlog: B-005 · ADRs: **0029 (nova, Proposta)**; usa 0003, 0004, 0007, 0009, 0012, 0016, 0026

Status: Aprovada no gate de 2026-10-31 (AR1–AR7; ADR 0029 aceita; Q3: sim) · Data: 2026-10-31 · Autor: Arquiteto
Emenda de 2026-10-31 (Lote E, decisão do usuário): o AR7 passa a entregar `DisplayName` e `Icon` do `GameplayEffect` (Design 7 e ticket AR7).

Nota de fila: se VN1–VN3 (B-007) entrarem antes, os testes daqui usam `ScalableFloat` em `Duration` e `ValueSource.Value` na constante do modificador.

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`.

- `Runtime/GAS/Attributes/AttributeDefinition.cs:15-31`: campos públicos `Icon`, `DisplayName`, `Type`, `Formula`, `MinValue`, `MaxValue`. Não implementa `IValidatable`.
- `Runtime/GAS/Attributes/AttributeState.cs`: o NC1 cria `ClampToLimits` (único ponto de limite, "gancho para o B-005") e o NC2 transforma o `Recalculate` num `for` sem alocação, onde "as operações novas entram como mais casos" (Spec do B-002, 1.1 e 1.3). `OnValueChanged(antigo, novo)` dispara quando o valor muda (`:90-93`).
- `Runtime/GAS/Attributes/AttributeSet.cs:26-54`: cria os estados e liga os derivados ao `OnValueChanged` das dependências por lambda (uma alocação por dependência, no `Awake`). O EA1 move a criação para `EnsureInitialized` e cria `States`.
- `Runtime/GAS/Attributes/AttributeModifier.cs:3-8`: `ModifierOperation { Add, Multiply, Override }`.
- `Runtime/GAS/Core/AbilitySystemComponent.cs:563-586`: `ApplyModifiersToBase` aplica instantâneos e tiques na base, em sequência (ADR 0029, Contexto). O RL1 cria `TryApplyEffect`.
- `Runtime/GAS/Effects/GameplayEffect.cs`: campos públicos, sem `IValidatable`.
- `Runtime/Core/`: não tem nada de texto ou cor. A 0012 põe o `TextRoleTheme` no Core.
- `Editor/GAS/GasEditors.cs` e `Editor/Core/Inspector/CoreEditors.cs`: Inspectors de uma linha. A guarda `SpellBookEditorCoverageTests` exige um para cada `ScriptableObject` público novo.

## Respostas às perguntas técnicas do PRD (seção 8)

- **P4. Nível dos efeitos de partida.** Confirmado: aqui o efeito de partida é aplicado sem nível, como `ApplyEffect(efeito, this)`. O B-007 (ADR 0030) define que efeito sem habilidade usa o `DefaultLevel` do próprio efeito (padrão 1); não há "nível do dono".
- **P5. Regra exata do R13.** ADR 0029, item 4: num instantâneo ou num tique, os modificadores **daquela aplicação** que miram o mesmo atributo passam pela fórmula do R12 **uma vez**, partindo da base atual, e o resultado vira a nova base. Aplicações sucessivas compõem porque cada uma parte da base deixada pela anterior.
- **P6. Onde moram os limites dinâmicos.** O `AttributeSet` liga, depois de criar todos os estados, cada estado dependente ao `OnValueChanged` do estado-fonte, no mesmo ASC (como faz com os derivados). Uma alocação por ligação na inicialização, nenhuma por mudança. Não há desassinatura: fonte e dependente vivem e morrem juntos no mesmo `AttributeSet`.
- **P7. `OnDepleted` e o piso.** A transição usa o **valor atual** (`CurrentValue`, depois de modificadores e limite), guardada em dois indicadores (`_atMin`, `_atMax`) recalculados a cada `Recalculate`. O evento dispara só quando o indicador passa de falso a verdadeiro. Dano instantâneo e modificador no mesmo quadro geram dois recálculos, mas só uma transição.
- **P8. Cultura padrão.** Cultura nula usa `CultureInfo.InvariantCulture`, fixa, sem configuração global (estado estático mudável quebraria o isolamento dos testes e a sessão sem domain reload). O jogo passa a cultura dele; o `ITextProvider` do B-008 é quem vai fornecê-la. Ver a pergunta Q3 ao usuário.
- **P9. Categoria e tema.** `AttributeCategory` é asset do GAS (é conceito de atributo). `TextRoleTheme` e o enum `TextRole` ficam no Core, como na 0012, sem conhecer o GAS.

## Design

### 1. Limites dinâmicos e política (R1 a R6)

`AttributeDefinition` ganha (campos públicos, no estilo da classe):
- `public enum LimitSource { Constant, Attribute }`; `MinSource`, `MinAttribute`, `MaxSource`, `MaxAttribute`. `MinValue` e `MaxValue` continuam: são a constante e o valor reserva (R6). Padrão `Constant`: asset antigo igual (R1, CA5).
- `public enum MaxChangePolicy { KeepValue, KeepRatio, AddDelta }`; campo `OnMaxChange`, padrão `KeepValue`.

`AttributeState`:
- `float MinValue` e `float MaxValue` públicos de leitura: o limite **efetivo**. Com fonte ligada, o `CurrentValue` da fonte; senão a constante.
- `ClampToLimits(v)`: se `min > max`, devolve `min` (R4); senão `Mathf.Clamp(v, min, max)`.
- `internal void BindLimitSources(AttributeState minSource, AttributeState maxSource)`: guarda as fontes e o máximo atual em `_lastMax`.
- Quando a fonte muda (`OnLimitSourceChanged`), com trava de reentrada `_updatingLimits`:
  1. lê o novo máximo; se mudou e `base > novoMáximo`, a base vai ao novo máximo, qualquer que seja a política (R4, C3);
  2. senão aplica a política à base: `KeepValue` não mexe; `KeepRatio`: `base = min + (base - min) × (novo - min) / (antigo - min)`, quando `antigo - min > 0`; `AddDelta`: `base += novo - antigo`;
  3. grava a base sem recalcular e chama **um** `Recalculate` (uma notificação só, C3, CA3). O mínimo que muda só limita (R4).

`AttributeSet.EnsureInitialized`, depois de criar os estados:
- para cada estado com fonte `Attribute`: fonte ausente no ASC usa a constante e avisa **uma vez por definição** (R6, CA6, mesmo padrão de `_warnedMissing`);
- detecção de ciclo pelas arestas `MinAttribute`/`MaxAttribute` a partir de cada definição (busca em profundidade com conjunto de visitados, alocação só na inicialização). A aresta que fecha um ciclo não é ligada: aquele limite usa a constante e há um aviso único (R5, CA7). A trava `_updatingLimits` é a segunda barreira;
- ligação: `fonte.OnValueChanged += (_, _) => dependente.OnLimitSourceChanged()`.

Sem polling (N3, CA9): só por evento da fonte.

### 2. `OnDepleted` e `OnReachedMax` (R7 a R9)

- `event Action<AttributeState> OnDepleted` e `event Action<AttributeState> OnReachedMax` no `AttributeState`.
- O construtor calcula `_atMin = CurrentValue <= MinValue` e `_atMax = CurrentValue >= MaxValue` sem disparar (R7, CA12). O `Clamp` devolve o limite exato, então a comparação direta basta.
- Cada `Recalculate`: calcula os indicadores novos; guarda em locais quais transições houve (falso → verdadeiro); grava; dispara `OnValueChanged` (se o valor mudou), depois `OnDepleted` e por último `OnReachedMax` (R9). Mudança de limite que deixa o valor no limite passa pelo mesmo `Recalculate`, então dispara mesmo sem mudança de valor (R8, CA13).
- Ouvinte que altera o atributo dentro do evento: o `Recalculate` interno roda inteiro; o externo só dispara o que já tinha decidido. Sem exceção (CA14).

### 3. Composição (R11 a R14, ADR 0029)

- `ModifierOperation`: `MultiplyCompound` e `AddFinal` no fim.
- `internal struct ModifierAccumulator` (`Runtime/GAS/Attributes/ModifierAccumulator.cs`): campos `add`, `multiplySum`, `compoundProduct = 1`, `addFinal`, `hasOverride`, `overrideValue`; `void Accumulate(ModifierOperation op, float value)`; `float Apply(float baseValue)` com a fórmula da 0029.
- `AttributeState.Recalculate` passa a usar o acumulador no `for` do NC2. A ordem das somas por grupo é a da lista, então sem as operações novas o resultado é idêntico bit a bit (CA17).
- `ApplyModifiersToBase` (ASC): para cada índice `i`, pula se um índice anterior já mira o mesmo atributo; senão, acumula todos os modificadores de `i` em diante com esse atributo, aplica `Apply(state.BaseValue)` e chama `SetBaseValue` uma vez. Laços por índice, sem alocação.
- **Testes que mudam (N6):** nenhum da suíte atual (nenhum teste usa `Multiply` ou `Override` em instantâneo, busca de 2026-10-31). Se um teste do Lote A passar a usar até o AR3, o Fábrica o lista no PR e no CHANGELOG.

### 4. Tema de cores (R19, R20)

`Runtime/Core/Text/TextRoleTheme.cs`:
- `public enum TextRole { None, Beneficial, Harmful, Neutral, Value }`;
- `TextRoleTheme : ScriptableObject`, `[CreateAssetMenu("SpellBook/Text Role Theme")]`, `[HelpURL(DocUrls.Gas + "#textroletheme")]`, com quatro cores privadas serializadas e `Color GetColor(TextRole role)` (`None` usa `Neutral`), sem alocação;
- `public static TextRoleTheme Default`: criado sob demanda por `CreateInstance`, com `hideFlags = HideAndDontSave` e as cores padrão do pacote. Esse é o "tema padrão do pacote" (CA25). Não há asset em disco, porque o runtime não carrega asset do pacote por caminho.

### 5. Exibição (R15 a R18)

`Runtime/GAS/Attributes/AttributeDisplay.cs`, `[Serializable] public struct AttributeDisplay`:
- `ValueKind Kind` (`Flat`, `Percent`, `PercentPoints`, `Multiplier`, `Seconds`), `int Decimals`, `string Prefix`, `string Suffix`, `bool LowerIsBetter` (serializado) com `HigherIsBetter => !LowerIsBetter`, `int Order`, `bool Hidden`, `AttributeCategory Category`, `TextRole Role`. O campo serializado é `LowerIsBetter` porque struct não tem inicializador de campo: o padrão `false` dá "mais é melhor" em asset antigo (CA24).
- `string Format(float value, IFormatProvider culture = null)`: valor pelo tipo (`Percent` multiplica por 100 e põe "%"; `PercentPoints` põe "%"; `Multiplier` põe "x" na frente; `Seconds` põe " s"), com `Decimals` limitado a 0..6 por uma tabela estática de formatos `"F0"` a `"F6"` (sem montar texto de formato), mais `Prefix` e `Suffix`.
- `string Format(float value, ModifierOperation op, IFormatProvider culture = null)`: `Add` e `AddFinal` com sinal explícito ("+" ou "-"); `Multiply` e `MultiplyCompound` sempre em porcentagem com sinal (0,5 → "+50%"); `Override` sem sinal, pelo tipo.
- `TextRole GetDeltaRole(float delta)`: zero dá `Neutral`; senão `Beneficial` se o sinal concorda com `HigherIsBetter`, `Harmful` se não. `bool IsBeneficial(float delta, ModifierOperation op)`: `GetDeltaRole(delta) == Beneficial`. A operação entra na assinatura do R17, mas o sinal do delta já decide para todas as operações. Sem alocação.
- `AttributeCategory : ScriptableObject` (`Runtime/GAS/Attributes/AttributeCategory.cs`), `[CreateAssetMenu("GAS/Attribute Category")]`, `[HelpURL(DocUrls.Gas + "#attributecategory")]`, campos `DisplayName` e `Order`.
- `AttributeDefinition` ganha `public AttributeDisplay Display;` e a propriedade de leitura `Category => Display.Category` (R18). `Icon` e `DisplayName` continuam.

### 6. Validação e Inspector do `AttributeDefinition` (R25, R26)

- `AttributeDefinition : IValidatable`: mínimo maior que máximo (constantes), erro; fonte de limite que é o próprio atributo, erro; ciclo de fontes (caminhada pelas arestas a partir do asset), erro; fonte `Derived` cuja fórmula depende deste atributo (`Formula.GetSourceDependencies`), erro; fonte `Attribute` sem atributo, erro; `Decimals < 0`, erro; `Hidden` sem categoria, aviso.
- `Editor/GAS/AttributeDefinitionEditor.cs` (sai de `GasEditors.cs`): `BuildAfterDefault` com um `FloatField` "valor de exemplo" e um `Label` com `Display.Format(exemplo, CultureInfo.CurrentCulture)` atualizado ao editar. Os campos de fonte usam `[ShowIf]` (fonte `Attribute` mostra o atributo).

### 7. Efeitos de partida e metadados (R21 a R24, R26)

- ASC: `[SerializeField] private List<GameplayEffect> startingEffects = new()`. No fim do `Awake`, depois de `attributeSet.EnsureInitialized()` (EA1) e das habilidades, aplica cada um com `TryApplyEffect(efeito, this, out _)` (RL1), na ordem da lista. Entrada nula é pulada. Resultado `BlockedByImmunity`, `MissingRequirements` ou `Invalid` gera `Debug.LogWarning` (com o nome do efeito e do ASC; só em desenvolvimento, `Debug.isDebugBuild`) e não interrompe os demais (CA28). Ouvintes externos ainda não existem nesse momento, e o manual diz isso.
- `GameplayEffect`: `[SerializeField] private bool isBuff, isDebuff, isHidden, isPurgable` com propriedades `IsBuff`, `IsDebuff`, `IsHidden`, `IsPurgable`, agrupados sob `[Header("Metadados")]`. `ActiveEffectInstance` repassa as quatro. `GameplayEffect : IValidatable`, com o aviso de `IsBuff` e `IsDebuff` juntos (CA30). O B-007 acrescenta regras a este `Validate`.
- **Nome e ícone do efeito (emenda de 2026-10-31, decisão do usuário no Lote E; R1 do PRD do B-018):** `GameplayEffect` ganha, sob `[Header("Identidade")]` e antes da duração, `[SerializeField] private string displayName` e `[SerializeField] private Sprite icon`, com as propriedades de leitura `DisplayName` (vazio por padrão, nunca nulo: `displayName ?? string.Empty`) e `Icon` (nulo por padrão). É a mesma forma do `DisplayName`/`Icon` do atributo e da habilidade: texto padrão, sem campo de chave. A chave de texto dos três nomes (atributo, habilidade, efeito) é do B-008 (P2 dele), que migra os três do mesmo jeito; por isso a chave **não** entra aqui. Sem `ActiveEffectInstance.DisplayName`: quem lê usa `instance.Effect.DisplayName`. O aviso "efeito visível sem ícone" é do B-018 (R14 dele), não daqui. Asset antigo abre com nome vazio e sem ícone (aditivo).
- O validador do ASC (`AbilitySystemComponent : IValidatable`): entrada vazia em `startingEffects`, erro.

### 8. O que não muda

- `GetValue`, `SetBaseValue` e `OnValueChanged` mantêm assinatura e semântica.
- Assets existentes abrem iguais: limites constantes, `KeepValue`, exibição `Flat` com 0 casas e visível, metadados falsos, `startingEffects` vazio.
- O GAS não referencia Events, Pooling nem UI; o `TextRoleTheme` está no Core (CA33).

## Quebras de compatibilidade

- **Ordem de cálculo em instantâneos e tiques (ADR 0029):** efeito instantâneo ou tique com dois ou mais modificadores no mesmo atributo usa a fórmula do R12 em vez da aplicação em sequência. Exemplos no CHANGELOG (os três da 0029).
- Adicionado: limites por atributo e `MaxChangePolicy`, `OnDepleted`/`OnReachedMax`, `MultiplyCompound` e `AddFinal`, `AttributeDisplay.Format`, `AttributeCategory`, `TextRoleTheme`, `startingEffects`, metadados de efeito, `GameplayEffect.DisplayName` e `GameplayEffect.Icon` (emenda de 2026-10-31).

## Tickets

Todos usam a Unity e seguem o WIP 1. **Não há par paralelo sem Editor nesta Spec.** Cada ticket escreve a sua parte de `gas.md` e do `CHANGELOG.md`. Testes com `ManualTimeSource`, `asc.Tick` e `GasFactory` (N5); os campos privados novos são preenchidos pelo `GasFactory.SetObjectField` do EA6 ou por `SerializedObject`. Tolerância numérica dos testes de composição: `Within(1e-4f)` (CA15). Teto: ~500 linhas e 10 arquivos.

**Conflitos com a fila:** AR1 a AR3 tocam `AttributeState.cs` e `AttributeSet.cs` (NC1, NC2, EA1) e `AbilitySystemComponent.cs` (NC*, EA*, RL1); entram depois deles. AR4 toca `CoreEditors.cs` e AR5/AR6 tocam `GasEditors.cs` (EA6 também): em série. AR6 e AR7 tocam `validador.md` (TG4, EA6, RL5). AR6 acrescenta `SpellBook.GAS.Editor` ao asmdef de testes EditMode do GAS só se o EA7 ainda não tiver acrescentado.

### AR1 — Limites dinâmicos e política ao mudar o máximo
- Escopo: `Runtime/GAS/Attributes/AttributeDefinition.cs`, `AttributeState.cs`, `AttributeSet.cs`, `Tests/GAS/PlayMode/DynamicLimitTests.cs` (novo), `gas.md` (`#attributedefinition`, "Limites"), `CHANGELOG.md`.
- Testes red: `MaxFollowsSource_HealStopsAt150` (CA1, C1); `KeepValue80_KeepRatio120_AddDelta130` (CA2, C2); `MaxDropsBelowValue_ClampedAllPolicies_SingleNotification` (CA3, C3); `MinFromAttribute_NeverBelow` (CA4, C4); `ConstantLimits_SameAsBefore` (CA5; guarda); `MissingSource_FallsBackToConstant_OneWarning` (CA6); `CycleAB_NoInfiniteLoop_OneWarning` (CA7, parte de runtime); `MinAboveMax_UsesMin` (CA8, parte de runtime); `SourceChange_UpdatesWithoutTick` (CA9).
- Critério de pronto: CA1 a CA6, CA9 e as partes de runtime de CA7 e CA8 verdes; suíte do GAS sem edição (CA35).
- Tamanho estimado: ~470 linhas; 6 arquivos. Depende de: EA1 (`EnsureInitialized`).

### AR2 — `OnDepleted` e `OnReachedMax`
- Escopo: `AttributeState.cs`, `Tests/GAS/EditMode/AttributeLimitEventsTests.cs` (novo; `AttributeState` com dono nulo), `Tests/GAS/PlayMode/LimitChangeEventsTests.cs` (novo; mudança de limite pelo `AttributeSet`), `gas.md`, `CHANGELOG.md`.
- Testes red: `Depleted_OnTransition_NotRepeated_AgainAfterHeal` (CA10, C5); `ReachedMax_OnTransition_NotRepeated` (CA11, C6); `BornAtLimit_DoesNotFire` (CA12); `MaxDropsToValue_FiresReachedMax_AlreadyAtMax_DoesNot` (CA13); `Depleted_AfterValueChanged_ListenerChangingValue_NoException` (CA14); `LimitEvents_DoNotAllocate` (N1).
- Critério de pronto: CA10 a CA14 verdes.
- Tamanho estimado: ~330 linhas; 5 arquivos. Depende de: AR1.

### AR3 — `MultiplyCompound`, `AddFinal` e ordem única
- Escopo: `Runtime/GAS/Attributes/AttributeModifier.cs`, `Runtime/GAS/Attributes/ModifierAccumulator.cs` (novo), `AttributeState.cs`, `AbilitySystemComponent.cs` (`ApplyModifiersToBase`), `Tests/GAS/EditMode/ModifierCompositionTests.cs` (novo), `Tests/GAS/PlayMode/InstantCompositionTests.cs` (novo), `gas.md` (a fórmula com o exemplo C7), `CHANGELOG.md` (quebra).
- Testes red: `ExampleC7_Is242_6` (CA15); `OverrideAfterAddFinal_ThenLimit` (CA16, C8); guardas do NC2 com os mesmos literais (CA17); `Enum_NewValuesAtEnd` e `SerializedOldOperation_SameValue` (CA18); `Instant_TwoMultiply_Sum_NotCompose`, `Instant_MultiplyThenAdd_UsesFormula`, `Tick_UsesFormula`, `TwoInstantApplications_Compose` (CA19, P5); `Recalculate_FourOperations_DoesNotAllocate_1000Times` (CA20).
- Critério de pronto: CA15 a CA20 verdes; a quebra no CHANGELOG com os exemplos da 0029.
- Tamanho estimado: ~450 linhas; 8 arquivos. Depende de: AR1 (`AttributeState.cs`). ADR 0029 aceita no gate.

### AR4 — `TextRole` e `TextRoleTheme`
- Escopo: `Runtime/Core/Text/TextRoleTheme.cs` (novo), `Editor/Core/Inspector/CoreEditors.cs` (uma linha), `Tests/Core/EditMode/TextRoleThemeTests.cs` (novo), `gas.md` (`#textroletheme`), `CHANGELOG.md`.
- Testes red: `GetColor_FourRoles_NoneUsesNeutral`; `ChangingAssetColor_ChangesReadColor` (parte automatizável do CA25); `Default_ExistsWithPackageColors`; `GetColor_DoesNotAllocate` (CA26, parte do tema); `Core_HasNoGasTypes` (CA33, guarda existente verde).
- Critério de pronto: testes verdes; CA25 (S) conferido no host pelo Examinador (criar um tema, mudar a cor, ler).
- Tamanho estimado: ~200 linhas; 5 arquivos. Depende de: nada no código; pela fila, depois do AR3.

### AR5 — `AttributeDisplay`, `Format`, `IsBeneficial` e `AttributeCategory`
- Escopo: `Runtime/GAS/Attributes/AttributeDisplay.cs` (novo), `Runtime/GAS/Attributes/AttributeCategory.cs` (novo), `AttributeDefinition.cs`, `Editor/GAS/GasEditors.cs` (uma linha para a categoria), `Tests/GAS/EditMode/AttributeDisplayTests.cs` (novo), `gas.md` (`#attributedisplay`, `#attributecategory`), `CHANGELOG.md`.
- Testes red: `Format_Percent0Dec_25pct`, `Format_Seconds1Dec_PtBr_EnUs`, `Format_Multiplier_x2`, `Format_Flat_PrefixSuffixDecimals`, `Format_PercentPoints` (CA21, C9); `Format_Op_AddSigned_MultiplyAsPercent`; `Format_NullCulture_Invariant` (CA22); `IsBeneficial_HigherAndLowerIsBetter_PosNegZero` (CA23, C10); `OldDefinition_FlatZeroDecimalsVisible_ReadOnlyApi` (CA24); `IsBeneficial_DoesNotAllocate` (CA26, parte do GAS).
- Critério de pronto: CA21 a CA24 e CA26 verdes.
- Tamanho estimado: ~430 linhas; 7 arquivos. Depende de: AR4 (`TextRole`), AR1 (`AttributeDefinition.cs`).

### AR6 — Validação e Inspector do `AttributeDefinition`
- Escopo: `AttributeDefinition.cs` (`IValidatable`), `Editor/GAS/AttributeDefinitionEditor.cs` (novo), `Editor/GAS/GasEditors.cs` (tira a linha do `AttributeDefinition`), `Tests/GAS/EditMode/AttributeDefinitionValidationTests.cs` (novo), `Tests/GAS/EditMode/AttributeDefinitionEditorTests.cs` (novo), `Tests/GAS/EditMode/SpellBook.GAS.Tests.EditMode.asmdef` (só se o EA7 não tiver entrado), `manual/ferramentas/validador.md`, `gas.md`, `CHANGELOG.md`.
- Testes red: `MinAboveMax_Error` (CA8, validador); `SelfSource_Error`; `CycleAB_Error` (CA7, validador); `DerivedSourceDependingOnThis_Error`; `NegativeDecimals_Error`; `HiddenWithoutCategory_Warning`; `ProjectValidator_ReportsDefinitionAsContext` (CA32, parte do atributo); `Editor_PreviewShowsFormattedSample`.
- Critério de pronto: CA7, CA8 e a parte de atributo do CA32 verdes; CA31 (S) no host para o `AttributeDefinition`.
- Tamanho estimado: ~420 linhas; 9 arquivos. Depende de: AR5, EA7 (asmdef de teste, se já entregue), EA6 (`GasEditors.cs`, `validador.md`).

### AR7 — `startingEffects`, metadados, nome e ícone de efeito
- Escopo: `AbilitySystemComponent.cs` (`startingEffects`, `IValidatable`), `Runtime/GAS/Effects/GameplayEffect.cs` (metadados, `displayName`, `icon`, `IValidatable`), `ActiveEffectInstance.cs` (repasse dos metadados), `Tests/GAS/PlayMode/StartingEffectsTests.cs` (novo), `Tests/GAS/EditMode/EffectMetadataTests.cs` (novo; inclui nome e ícone), `gas.md` (`#gameplayeffect`, metadados, nome e ícone, efeitos de partida), `manual/ferramentas/validador.md`, `CHANGELOG.md`.
- Testes red: `StartingRegen_AppliedAfterAttributeSet_RaisesHealth` (CA27, C11); `BlockedStartingEffect_Warns_OthersApplied`, `EmptyList_DoesNothing` (CA28); `Metadata_ReadFromAssetAndInstance_DefaultNeutral` (CA29, C12); `BuffAndDebuff_Warning` (CA30); `StartingEffectsNullEntry_Error` (CA32, parte do ASC); `NameAndIcon_ReadFromAsset_DefaultEmptyAndNull` e `NameAndIcon_ReadThroughInstanceEffect` (CA17 do PRD do B-018); `OldEffectYaml_WithoutNameAndIcon_OpensEmpty` (asset YAML sem os campos, como no VN2).
- Critério de pronto: CA27 a CA30 e CA32 completos; CA17 do B-018 verde; CA31 (S) para `GameplayEffect` (com nome e ícone sob "Identidade") e ASC; CA34 (CHANGELOG e manual com o R27, mais nome e ícone) conferido pelo Examinador.
- Tamanho estimado: ~430 linhas; 8 arquivos. Depende de: RL1 (`TryApplyEffect`), EA2 (`ActiveEffectInstance.cs`), AR6 (`validador.md`).
- Emenda de 2026-10-31 (Lote E): nome e ícone do efeito entram aqui (+~50 linhas, nenhum arquivo novo). A issue #146 é atualizada com o escopo e os três testes novos.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| AR1 | sim | EA1 | ~470 / 6 |
| AR2 | sim | AR1 | ~330 / 5 |
| AR3 | sim | AR1, ADR 0029 | ~450 / 8 |
| AR4 | sim | — | ~200 / 5 |
| AR5 | sim | AR4, AR1 | ~430 / 7 |
| AR6 | sim | AR5, EA6, EA7 | ~420 / 9 |
| AR7 | sim | RL1, EA2, AR6 | ~430 / 8 |

Fila proposta (P1 do PRD: limites e eventos primeiro, por causa do B-010; composição depois; exibição e tema por último, por causa do B-008): AR1 → AR2 → AR3 → AR4 → AR5 → AR6 → AR7. O B-007 precisa do AR3 (VN5); o AR4 pode adiantar se a fila do Unity ficar ociosa. CA33 e CA35 rodam em todos.

## Perguntas ao usuário

- **Q3. Cultura nula (P8).** O R16 fala em "cultura invariante configurável". Proposta: cultura nula usa `CultureInfo.InvariantCulture`, fixa, sem configuração global no pacote. Quem quer outra cultura a passa no parâmetro; no B-008, o `ITextProvider` é quem a fornece.
- A ADR 0029 (ordem única de composição, compartilhada com o B-007) passa pelo gate junto com esta Spec.
