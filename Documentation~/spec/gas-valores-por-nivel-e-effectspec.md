# Spec: Valores por nível e EffectSpec
PRD: [Documentation~/prd/gas-valores-por-nivel-e-effectspec.md](../prd/gas-valores-por-nivel-e-effectspec.md) · Backlog: B-007 · ADRs: 0030 (aceita, com a emenda de 2026-10-31), 0029 (aceita); usa 0003, 0004, 0007, 0009, 0012, 0014, 0016, 0018, 0020, 0021, 0026

Status: Aprovada no gate de 2026-10-31, com a revisão da Q4 pedida pelo usuário ("modular, sem remendos de código"), aprovada por ele · Data: 2026-10-31 · Autor: Arquiteto

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`.

- `Runtime/GAS/Abilities/AbilitySpec.cs:13`: `Level { get; set; }` público, sem limite e sem evento. `GiveAbility(ability, level = 1)` (`AbilitySystemComponent.cs:123`).
- `Runtime/GAS/Abilities/AbilityContext.cs:15-18`: `Ability`, `Spec`, `Level`. O NC6 faz o `CreateDerived` manter `Ability` e `Level`; o impacto do projétil usa o contexto derivado (`Projectile.cs:316-318`).
- `Runtime/GAS/Abilities/Actions/ApplyEffectAction.cs:15-33`: aplica com `ApplyEffect(effect, context.Source)`, sem nível nem habilidade.
- `Runtime/GAS/Attributes/ModifierValueSource.cs:9-43`: `struct` com `SourceType { Constant, Attribute, Formula }` e `float ConstantValue`. **`Attribute` já lê do instigador** (`:36`), não do alvo.
- `Runtime/GAS/Effects/GameplayEffect.cs:18-34`: `AttributeEffectModifier` é `struct`; `Duration` é campo público `float`.
- Valores de modificador: com duração, avaliados uma vez na aplicação (`AbilitySystemComponent.cs:536`); instantâneos e tiques, a cada aplicação (`:572`). Pilhas não mexem no valor.
- `GameplayAbility.GetCooldownDuration` (`:75-86`) lê `CooldownEffect.Duration`; `CanAffordCost` (`:216-231`) avalia o custo com `GetValue(this, this)`.
- O Lote B anterior entrega: `TryApplyEffect` e `Refreshed` (RL1); eventos de efeito, concessão e recarga, `SetEffectStacks` (EA3 a EA5); `CooldownReductionPolicy` (EA6); `ModifierAccumulator` e a ordem da 0029 (AR3); `startingEffects` (AR7); `GasFactory.SetObjectField` (EA6).
- `Runtime/GAS/Abilities/Actions/FlowActions.cs` e `SpawnProjectileAction.cs` (o RP-07 mexe neste) guardam listas de ações filhas.
- Assets do Sample com `Duration` e constantes de modificador: `GE_BolaDeFogo_Custo`, `GE_BolaDeFogo_Recarga`, `GE_BolaDeFogo_Dano` e `GE_Queimadura` (SB-T3, `Samples~/Sandbox/Data/`).

## Decisão da Q4 (revisão pedida no gate)

**Um caminho único para todo valor numérico que pode variar com o nível: o `ScalableFloat`.** Ele tem quatro modos, `Constant`, `Array`, `Curve` e `Table` (o `%nome`, um valor da tabela `AbilityValues` da habilidade de origem). Os campos que hoje são `float` e entram no escopo do PRD **mudam de tipo** para `ScalableFloat`:

- `GameplayEffect.Duration` (cobre recarga e duração, R27);
- a constante do modificador (`ModifierValueSource`), que cobre magnitude e custo (R6).

O que sai da proposta anterior, por ser mecanismo paralelo:
- não há `durationValueName` ao lado do `float Duration`;
- não há a origem `AbilityValue` no `ModifierValueSource`: o `%nome` é um modo do próprio `ScalableFloat`;
- não há a origem `Level`: "valor que depende do nível" é exatamente o que o `ScalableFloat` faz (curva ou array). Isso substitui o item "nível" do R14 do PRD.

Fora do escopo deste item, os campos `float` que o PRD não lista (`Period`, por exemplo) continuam `float`. Cargas (B-009) e alcance de cast (B-012) nascem como `ScalableFloat` nos itens deles.

**Migração (ADR 0009):** os dois campos ganham um campo legado privado, só de leitura do formato antigo (`[FormerlySerializedAs]` com o nome antigo, valor inicial `NaN`), e o dono converte em `OnAfterDeserialize` (`ISerializationCallbackReceiver`): valor legado diferente de `NaN` vira `ScalableFloat.Constant(valor)`, e o legado volta a `NaN`. Um menu de Editor regrava todos os assets do projeto no formato novo. Os campos legados existem só para a migração e saem antes da 3.0 final, em ticket próprio, depois que os projetos rodarem o migrador (pendência registrada na seção "Depois deste item").

## Respostas às perguntas técnicas do PRD (seção 8)

- **P4. Efeito sem habilidade.** Nível = `DefaultLevel` do efeito (campo novo, padrão 1). Sem habilidade não há tabela: um `ScalableFloat` em modo `Table` vale o seu `Fallback` (padrão 0), sem aviso. Isso responde o "em aberto" da 0020 sobre `startingEffects` (ADR 0030 e a emenda dela).
- **P5. Efeito compartilhado.** A validação parte da **habilidade**: cada `GameplayAbility` percorre os efeitos que referencia (recarga, custo e os de `ApplyEffectAction`, inclusive dentro de ações de fluxo e de `OnHitActions`) por um método virtual novo, `AbilityAction.CollectEffects(List<GameplayEffect>)`, e acusa cada `ScalableFloat` em modo `Table` cujo nome não está na própria tabela, com a habilidade como objeto clicável, citando o efeito e o nome (CA10). Custo proporcional às ações, sem varrer o projeto. A varredura de projeto existe só no Editor, para a lista de nomes do seletor (VN10).
- **P6. Forma do `EffectSpec`.** ADR 0030: structs passados por `in`; `SetByCallerValues` é classe reutilizável de quem chama; a instância guarda uma cópia do contexto. `TryApplyEffect(in EffectSpec, out instância)` entra ao lado do do RL1. Dois contextos (Q5, aprovada).
- **P7. SetByCaller.** Por nome. No Inspector, o nome é texto, e o validador exige formato de identificador; não há lista, porque o nome vem do código que aplica. No instantâneo, a busca é feita na aplicação; no efeito com duração, os valores são copiados uma vez para a instância. Nada é procurado por tique.
- **P8. Instigador destruído.** Último valor conhecido: nos persistentes, já está no `AttributeModifier.Value` (o recálculo ao vivo não roda com instigador nulo); nos tiques, a instância guarda o último valor de cada modificador periódico (`float[]`, uma alocação por instância com tique).
- **P9. Tiques periódicos.** Sempre avaliados no tique, com o contexto da instância (Q6, aprovada). O modo de magnitude vale só para os `Modifiers` persistentes.
- **P11. `OnLevelChanged`.** No ASC: `OnAbilityLevelChanged(AbilitySpec, int antes, int depois)`. O `set` de `AbilitySpec.Level` passa a `internal`; a mudança passa por `SetAbilityLevel`.
- **P12. Nível de um efeito ativo.** O da aplicação, guardado no `EffectContext`. Só um modificador ao vivo que depende do nível atualiza o nível guardado. A duração aplicada não muda (NC4).
- **P15. Assinatura ao vivo.** Uma `LiveModifierBinding` por modificador ao vivo, criada na aplicação, com o handler em campo. Ela assina o `OnValueChanged` do estado de onde o valor vem (instigador, mesmo em outro ASC, ou alvo) e o `OnAbilityLevelChanged` do ASC instigador quando o valor tem `ScalableFloat` que depende do nível (`Array`, `Curve` ou `Table`). O `RemoveEffectCore` (EA3) desfaz as ligações. Snapshot não cria ligação (CA27).
- **P16. Ciclos ao vivo.** ADR 0030: validador para o que um asset mostra; trava de reentrada por ligação em runtime, com aviso único.

## Design

### 1. `ScalableFloat` (R1 a R4, R6, R7)

`Runtime/GAS/Values/ScalableFloat.cs`, `[Serializable] public sealed class ScalableFloat`:
- `public enum ScalableMode { Constant, Array, Curve, Table }`; campos privados serializados `mode`, `constant`, `values` (`float[]`), `curve` (`AnimationCurve`), `tableName`, `fallback`;
- `float Evaluate(int level, AbilityValues table = null)`: nível < 1 vira 1; `Constant` devolve a constante; `Array` vazio devolve 0 e acima do tamanho repete o último; `Curve` nula devolve 0, senão `curve.Evaluate(level)` com nível inteiro; `Table` sem tabela devolve `fallback` sem aviso; com tabela, usa o índice em cache, e nome ausente devolve `fallback` com aviso único por instância de `ScalableFloat`. Sem alocação;
- **cache do `%nome` (R7):** campos `[NonSerialized]` com a última tabela, a versão dela (`AbilityValues.Version`) e o índice. Se a tabela e a versão são as mesmas, a avaliação é uma comparação de referência e de inteiro, sem texto. Um efeito usado por duas habilidades refaz a resolução ao alternar, e continua correto;
- fábricas: `Constant(float)`, `FromArray(params float[])`, `FromCurve(AnimationCurve)`, `FromTable(string name, float fallback = 0)`. **Sem conversão implícita de `float`**: o modo fica explícito no código;
- `static bool TryParse(string text, out float[] values, out string error)` (espaço, vírgula e ponto e vírgula; cultura invariante; só o Editor chama);
- `void Validate(ValidationResult result, string propertyPath, bool allowTable)`: array vazio, curva nula, `Table` sem nome, e `Table` onde não é permitido (dentro da própria `AbilityValues`).

### 2. `AbilityValues`, `MaxLevel` e nível (R5, R23, R24, R26, R28)

- `Runtime/GAS/Values/AbilityValues.cs`: `[Serializable] public sealed class AbilityValues` com `List<Entry>` (`Entry`: `string Name`, `ScalableFloat Value`); `int Version` (sobe em toda mudança por código e no `OnValidate` do dono); `int IndexOf(string name)` (ordinal, sem alocação); `float Evaluate(int index, int level)` (avalia a entrada sem tabela, então uma entrada nunca resolve outra); `bool TryGetValue(string name, int level, out float value)`; `Validate`: nome vazio, fora de `^[A-Za-z0-9_]+$` ou repetido; entrada em modo `Table` é erro.
- `GameplayAbility`: `[SerializeField] private AbilityValues values = new()` e `Values`; `[SerializeField, Min(1)] private int maxLevel = 1` e `MaxLevel`; `bool TryGetValue(string name, int level, out float value)` (R28); `OnValidate` incrementa `values.Version`.
- `AbilitySpec.Level`: `set` passa a `internal`.
- ASC: `GetAbilityLevel(GameplayAbility)` (0 se não concedida); `bool SetAbilityLevel(GameplayAbility, int)`: limita a `[1, MaxLevel]`, avisa uma vez por habilidade acima do máximo, dispara `event Action<AbilitySpec, int, int> OnAbilityLevelChanged` só em mudança real. `GiveAbility(ability, level)` limita igual.

### 3. Campos migrados

- `ModifierValueSource` e `AttributeEffectModifier` passam de `struct` a `[Serializable] public sealed class` (o formato serializado em linha é o mesmo; a classe evita cópias de struct que compartilhariam o `ScalableFloat`). O código do GAS confere entrada nula nas listas.
- `ModifierValueSource.SourceType`: o valor 0, `Constant`, passa a se chamar `Value` (o inteiro serializado não muda) e usa `public ScalableFloat Value = ScalableFloat.Constant(0)`. Legado: `[SerializeField, HideInInspector, FormerlySerializedAs("ConstantValue")] private float legacyConstant = float.NaN`, convertido no `OnAfterDeserialize` da própria classe.
- `GameplayEffect`: `[SerializeField, ShowIf(...)] private ScalableFloat duration = ScalableFloat.Constant(0)` com a propriedade `Duration`. Legado: `[SerializeField, HideInInspector, FormerlySerializedAs("Duration")] private float legacyDuration = float.NaN`, convertido no `OnAfterDeserialize` do efeito.
- Leitores atualizados no mesmo ticket: `ApplyEffect` (`effect.Duration.Evaluate(...)`), `GameplayAbility.GetCooldownDuration`, `CanAffordCost`, `GasFactory.CreateEffect` (usa `ScalableFloat.Constant(duration)`; a assinatura da factory não muda) e todo teste que escreve `Duration` ou `ConstantValue` (lista no PR).
- Até o VN5, a avaliação usa nível 1 e nenhuma tabela, então o comportamento observável é idêntico ao de antes (guarda: a suíte inteira).
- `Editor/GAS/GasAssetMigration.cs`: menu `SpellBook > Migrar assets do GAS (3.0)`. Carrega todo `GameplayEffect` do projeto (a conversão roda no carregamento), marca como sujo e salva. Idempotente. Fica documentado em `manual/regras/migracao-2-para-3.md`.

### 4. Contexto, spec e origens novas (R9 a R15, R27)

- `EffectContext` (`readonly struct`): `Instigator`, `Target`, `Ability`, `Level`, `HasImpactPoint`, `ImpactPoint`, `CallerValues` (`IReadOnlySetByCaller`); `Table => Ability != null ? Ability.Values : null`.
- `EffectSpec` (`struct`): `Effect`, `Context`, `HasDurationOverride`, `DurationOverride`; `Create(effect, instigator)` (sem habilidade, `DefaultLevel`); `FromAbility(effect, AbilityContext)` (instigador, habilidade, nível e `TargetPosition` como ponto de impacto); `WithCallerValues`, `WithImpactPoint`, `WithDurationOverride`.
- `SetByCallerValues` (classe reutilizável, `Set`, `TryGet`, `Clear`, `Count`); a instância copia só quando `Count > 0`.
- ASC: `TryApplyEffect(in EffectSpec, out instância)` e `ApplyEffect(in EffectSpec)`; as formas antigas montam `EffectSpec.Create` (CA17). `ActivateSpec` monta o spec da recarga e do custo com a habilidade e o nível do spec; a duração da recarga é `Duration.Evaluate(nível, tabela)` e depois passa pela `CooldownReductionPolicy` (EA6). `CanAffordCost` ganha a sobrecarga `internal` com nível e habilidade.
- `ActiveEffectInstance.Context`; `ApplyEffectAction` aplica `EffectSpec.FromAbility` (C6).
- `GameplayEffect`: `[SerializeField, Min(1)] private int defaultLevel = 1` e `DefaultLevel`.
- `ModifierValueSource.SourceType` ganha, no fim: `SetByCaller` (campos `CallerName`, `CallerDefault`, `CallerHasDefault`) e `TargetAttribute` (campo `TargetAttributeDefinition`). Avaliação: `internal float Evaluate(in EffectContext)`; `public float GetValue(in EffectContext)` igual, para quem está fora do caminho quente (a fonte `ModifierMagnitude` da `RaiseEventEffectAction`, RL6, passa a usar o `Context` da instância); `GetValue(source, target)` continua, com nível 1 e sem tabela.

### 5. Pilhas multiplicam a magnitude (R20 a R22)

- `GameplayEffect`: `[SerializeField] private bool stacksMultiplyMagnitude = true` e `StacksMultiplyMagnitude`. Asset antigo sem o campo fica com `true`: é a quebra do R21.
- A instância guarda o valor de uma pilha por modificador persistente e o `AttributeModifier` criado; `internal void RefreshMagnitudes()` grava `unidade × (StacksMultiplyMagnitude ? CurrentStacks : 1)` e recalcula cada atributo afetado (ADR 0029, item 2). O ASC chama depois de toda mudança de pilhas, antes de `OnEffectStacksChanged`. O tique também multiplica.
- Testes que mudam (N6): nenhum da suíte atual usa `CanStack`; se algum teste do Lote A ou do Lote B conferir valor de atributo com pilhas, o VN7 o lista no PR e no CHANGELOG. A `GE_Queimadura` do Sample tem `MaxStacks = 1` e não muda.

### 6. Magnitude ao vivo (R16 a R19, R25)

- `AttributeEffectModifier.Mode` (`MagnitudeMode { Snapshot, Live }`, padrão `Snapshot`).
- `LiveModifierBinding` (interna), com `OnSourceChanged`, `OnLevelChanged` e a trava `_evaluating`; não roda com instigador nulo.
- O ASC cria as ligações na aplicação e o `RemoveEffectCore` desfaz. `ActiveEffectInstance.LiveBindingCount` (público, só leitura) serve ao CA27 e ao Debugger.
- Dependências: `Attribute` (estado do instigador), `TargetAttribute` (do alvo), `Formula` (`GetSourceDependencies()` no instigador), `Value` com `ScalableFloat` que não é `Constant` (o nível do instigador).

### 7. Validação (R8, R30)

- `GameplayAbility.Validate`: `MaxLevel < 1`; `Values.Validate`; para cada efeito de `CollectEffects`, cada `ScalableFloat` em modo `Table` (duração, modificadores, periódicos) sem entrada na tabela é erro, citando efeito e nome; `MaxLevel` maior que um array da tabela é aviso.
- `GameplayEffect.Validate` (o do AR7, ampliado): cada `ScalableFloat` repassa o seu `Validate`; `SetByCaller` sem nome ou fora do formato de identificador, erro; sem padrão, aviso; `Live` em instantâneo ou periódico, aviso; ciclo ao vivo visível no asset, erro.
- `AbilityAction.CollectEffects` virtual, sobrescrito em `ApplyEffectAction`, nas ações de fluxo com filhas e no `SpawnProjectileAction`.

### 8. O que não muda

- `ApplyEffect(efeito, origem)` e `ApplyEffect(efeito, duração, origem)`: mesmo resultado para efeitos sem os recursos novos (CA17).
- `Attribute` continua lendo o instigador; snapshot continua o padrão (CA21).
- O GAS não referencia Events, Pooling, Cues, Teams nem UI (CA34).

## Quebras de compatibilidade

Para o CHANGELOG (pré-release 3.0, decisão I1):
- **`GameplayEffect.Duration` e a constante do modificador viram `ScalableFloat`.** Assets antigos são convertidos ao carregar; rode `SpellBook > Migrar assets do GAS (3.0)` para regravá-los. Código: `effect.Duration = 4f` vira `effect.Duration = ScalableFloat.Constant(4f)`; `ValueSource.ConstantValue` vira `ValueSource.Value`; `SourceType.Constant` passa a se chamar `SourceType.Value`.
- **`ModifierValueSource` e `AttributeEffectModifier` passam de struct a classe:** código que copiava o elemento da lista para alterá-lo passa a alterar o original.
- **Pilhas multiplicam a magnitude por padrão** (ADR 0021): para o comportamento antigo, desligue `StacksMultiplyMagnitude` no efeito.
- **`AbilitySpec.Level` sem `set` público:** use `SetAbilityLevel`. `MaxLevel` nasce 1, e o nível é limitado a `[1, MaxLevel]`, com aviso.
- **Formato serializado:** `SourceType` ganha valores no fim; campos novos em `ModifierValueSource`, `AttributeEffectModifier` e `GameplayEffect` (0009).
- Adicionado: `ScalableFloat`, `AbilityValues`, `EffectSpec`, `EffectContext`, `SetByCallerValues`, `MagnitudeMode`, `MaxLevel`, `OnAbilityLevelChanged`, `GetAbilityLevel`/`SetAbilityLevel`, `DefaultLevel`, migrador de assets.

## Tickets

Todos usam a Unity e seguem o WIP 1. **Não há par paralelo sem Editor nesta Spec.** Cada ticket escreve a sua parte de `gas.md` e do `CHANGELOG.md`. Curvas criadas por código (N7); tempo por `asc.Tick`; objetos pela `GasFactory`. Teto: ~500 linhas e 10 arquivos.

**Fila recomendada:** VN1, VN2a, VN2b e VN3 entram **logo depois do Lote A**, nessa ordem e antes do EA1 (o VN1 já está feito). Assim os testes do B-003, do B-004 e do B-005 já nascem com a API nova (`ScalableFloat` em `Duration` e na constante), e a migração edita só os testes do Lote A. O VN2a não muda nenhuma API usada pelos testes; o VN2b edita só os 11 testes listados no escopo dele (7 da Spec original e 4 do Lote B criados antes dele, por exceção única de 2026-11-01). EA6 e AR3 passam a ler `Duration.Evaluate(...)` e `ValueSource.Value` nos seus tickets. Se o Controlador preferir pôr algum ticket do Lote B antes do VN2b, o VN2b passa a editar também os testes novos que usam `ConstantValue` e sai do limite de 10 arquivos: nesse caso a Fábrica para e devolve (0003, item 4).

**Conflitos com a fila:** VN2a toca `GasFactory.cs` (RP-04), `GameplayEffect.cs`, `ActiveEffectInstance.cs`, `AbilitySystemComponent.cs` e `GameplayAbility.cs` (NC*). VN2b toca `ModifierValueSource.cs` (AR3 depois), `ShowIfRuleTests.cs` (testes do Core) e dez testes PlayMode do GAS (`AscTimeTests`, `AttributeAllocationTests`, `AttributeLimitTests`, `EffectPeriodTests`, `EffectRemovalTests`, `EffectRenewalTests`, `DynamicLimitTests`, `EffectApplicationResultTests`, `EffectRemoveByTagTests`, `InstantCompositionTests`; NC*, RP-* e Lote B), e reaproveita o `ScalableMigrationTests.cs` do VN2a: os dois em série, sem outro ticket do GAS no meio. VN3 toca os assets do Sample (SB-T3) e `migracao-2-para-3.md`. VN4 a VN8 tocam `AbilitySystemComponent.cs`, `ActiveEffectInstance.cs` e `GameplayEffect.cs` (EA*, RL1, AR7). VN9 toca `SpawnProjectileAction.cs` (RP-07), `FlowActions.cs` e `validador.md`. VN10 toca `GasEditors.cs` e o asmdef de testes EditMode do GAS (EA7).

### VN1 — `ScalableFloat` (constante, array, curva)
- Escopo: `Runtime/GAS/Values/ScalableFloat.cs` (novo, sem o modo `Table`, que entra no VN4), `Tests/GAS/EditMode/ScalableFloatTests.cs` (novo), `gas.md` (`#scalablefloat`), `CHANGELOG.md`.
- Testes red: `Array_10_8_6_4_Levels1_3_4` (CA1, C1); `AboveSize_RepeatsLast_ZeroOrNegative_IsLevel1` (CA2, C2); `Curve_EvaluatedAtIntegerLevel` (CA3, C3); `Constant_SameAtAnyLevel` (CA4); `TryParse_SpaceCommaSemicolon_Invariant`, `TryParse_Invalid_ReturnsError` (CA5, parte de runtime); `EmptyArray_Zero_ValidateError` (CA6); `Evaluate_DoesNotAllocate_10000Times` (parte do CA26).
- Critério de pronto: CA1 a CA6 verdes (o "mantém o último válido" do CA5 é do drawer, VN10).
- Tamanho estimado: ~280 linhas; 4 arquivos. Depende de: nada.

### VN2a — Migração de `Duration` para `ScalableFloat` e modificador como classe
Dividido do VN2 em 2026-11-01: a contagem da Fábrica deu 16 arquivos. O VN2a não muda a API da constante do modificador, então nenhum teste existente deixa de compilar.
- Escopo: `Runtime/GAS/Effects/GameplayEffect.cs` (`duration`, propriedade `Duration`, `legacyDuration`, `ISerializationCallbackReceiver`; `AttributeEffectModifier` passa a classe, com `ValueSource = new ModifierValueSource()` como valor inicial, para o VN2b não precisar tocar este arquivo), `Runtime/GAS/Effects/ActiveEffectInstance.cs` (`:28`, construtor sem duração: `effect.Duration.Evaluate(1)`), `Runtime/GAS/Core/AbilitySystemComponent.cs` (`:481`, `Duration.Evaluate(1)`; entrada nula ignorada em `CanAffordCost` `:222`, `ApplyDurationEffect` `:580`, `ApplyModifiersToBase` `:614` e `RemoveEffect` `:661`), `Runtime/GAS/Abilities/GameplayAbility.cs` (`GetCooldownDuration`, `:79`), `Tests/Testing/GasFactory.cs` (`:81`, `ScalableFloat.Constant(duration)`; a assinatura não muda), `Tests/GAS/EditMode/ScalableMigrationTests.cs` (novo, com o ajudante que escreve o YAML 2.x), `gas.md` (`#gameplayeffect`: `Duration` é um `ScalableFloat`), `CHANGELOG.md` (quebra do `Duration` e de `AttributeEffectModifier` como classe).
- Testes red: `LegacyYaml_Duration4_LoadsAsConstant4` (asset YAML no formato 2.x escrito numa pasta temporária de `Assets/` com o GUID do script lido por `AssetDatabase`, importado, lido e apagado no `TearDown`); `NewEffect_SavesWithoutLegacyDuration` (`legacyDuration` `NaN` depois de salvar); `NullModifierEntry_Ignored` (lista de `Modifiers` e de `PeriodicModifiers` com `null`: aplicar, tique e remoção não lançam e os outros modificadores valem); a suíte inteira como guarda de comportamento (nível 1, sem tabela).
- Critério de pronto: testes verdes; suíte do GAS verde sem editar nenhum teste existente além da `GasFactory`; nenhuma conversão implícita de `float`.
- Tamanho estimado: ~230 linhas; 8 arquivos. Depende de: VN1, NC7, RP-04.

### VN2b — Constante do modificador como `ScalableFloat`
- Escopo: `Runtime/GAS/Attributes/ModifierValueSource.cs` (`struct` passa a classe; `SourceType.Constant` passa a `Value`, com o mesmo inteiro; `public ScalableFloat Value = ScalableFloat.Constant(0)` com o `[ShowIf]`; `legacyConstant` com `[FormerlySerializedAs("ConstantValue")]`; conversão no `OnAfterDeserialize` da própria classe), `Tests/GAS/EditMode/ScalableMigrationTests.cs` (casos novos, reaproveitando o ajudante do VN2a), os 11 testes que usam `ConstantValue` ou `SourceType.Constant`: `Tests/Core/EditMode/Inspector/ShowIfRuleTests.cs` (`:91-100`, campo e tipo renomeados), `Tests/GAS/PlayMode/AscTimeTests.cs` (`:115`), `AttributeAllocationTests.cs` (`:47`), `AttributeLimitTests.cs` (`:177`), `EffectPeriodTests.cs` (`:178`), `EffectRemovalTests.cs` (`:136`), `EffectRenewalTests.cs` (`:154`) e, criados depois da Spec, `DynamicLimitTests.cs` (`:229`), `EffectApplicationResultTests.cs` (`:404`), `EffectRemoveByTagTests.cs` (`:112`) e `InstantCompositionTests.cs` (`:138`), `CHANGELOG.md` (quebra: `ConstantValue` vira `Value`, `SourceType.Constant` vira `SourceType.Value`, `ModifierValueSource` vira classe).
- Fora do escopo, para caber em 10 arquivos: o texto do `gas.md` sobre o valor do modificador entra no VN3. Hoje o `gas.md` não cita `ConstantValue` nem `SourceType.Constant`, então nada no manual fica errado entre o VN2b e o VN3.
- Testes red: `LegacyYaml_ConstantValue_LoadsAsValueConstant`; `LegacyAttributeSource_KeepsTypeAndAttribute` (fonte `Attribute` do formato 2.x continua `Attribute`, com o mesmo atributo); `NewModifier_SavesWithoutLegacyConstant` (`legacyConstant` `NaN` depois de salvar); `ShowIf_ValueVisibleOnlyForValueType` (o teste do `ShowIfRuleTests`, com o campo novo); a suíte inteira como guarda.
- Critério de pronto: testes verdes; suíte do GAS e do Core verde, com os 11 testes editados listados no PR, cada um só com a troca de `ConstantValue = x` por `Value = ScalableFloat.Constant(x)` e de `Constant` por `Value`; nenhuma conversão implícita de `float`.
- Tamanho estimado: ~150 linhas; **14 arquivos**, acima do limite de 10 por exceção única do usuário (2026-11-01, emenda da ADR 0003). Em 2026-11-01, o grep por `ConstantValue` e `SourceType.Constant` não acha outro uso em código fora desta lista. Os três assets do Sample (`GE_BolaDeFogo_Custo`, `GE_BolaDeFogo_Dano`, `GE_Queimadura`) têm `ConstantValue` no YAML, carregam pelo `[FormerlySerializedAs]` e são regravados no VN3. `GetRawConstantValue` em `ManualLinksTests.cs` e `DocUrlsTests.cs` é API de reflexão e não tem relação com este ticket. Depende de: VN2a.

### VN3 — Migrador de assets e Sample no formato novo
- Escopo: `Editor/GAS/GasAssetMigration.cs` (novo), `Tests/GAS/EditMode/GasAssetMigrationTests.cs` (novo; um asset legado temporário é regravado e o YAML não tem mais o valor legado), `Samples~/Sandbox/Data/GE_BolaDeFogo_Custo.asset`, `GE_BolaDeFogo_Recarga.asset`, `GE_BolaDeFogo_Dano.asset`, `GE_Queimadura.asset` (regravados pelo menu, com a pasta renomeada pela ADR 0028), `Documentation~/manual/regras/migracao-2-para-3.md`, `gas.md` (`#gameplayeffect`: o valor do modificador é um `ScalableFloat` na fonte `Value`; vem do VN2b), `CHANGELOG.md`.
- Testes red: `Migrate_RewritesLegacyAsset_Idempotent`; as guardas do SB-T1 (`.meta` e GUID único) continuam verdes.
- Critério de pronto: teste verde; os quatro assets abrem sem "Missing" e com os mesmos números (o Examinador confere no Inspector); página de migração com o passo a passo.
- Tamanho estimado: ~180 linhas; 9 arquivos (4 fixtures). Depende de: VN2b, SB-T3.

### VN4 — `AbilityValues`, modo `Table`, `MaxLevel` e API de nível
- Escopo: `Runtime/GAS/Values/AbilityValues.cs` (novo), `ScalableFloat.cs` (modo `Table`, cache, `Fallback`), `GameplayAbility.cs`, `Runtime/GAS/Abilities/AbilitySpec.cs`, `AbilitySystemComponent.cs`, `Tests/GAS/EditMode/AbilityValuesTests.cs` (novo), `Tests/GAS/PlayMode/AbilityLevelTests.cs` (novo), `Tests/GAS/EditMode/GasDebuggerModelTests.cs` (ajuste ao `MaxLevel` que começa em 1; acrescentado em 2026-11-01), `gas.md` (`#gameplayability`, níveis e tabela), `CHANGELOG.md`.
- Testes red: `GetValue_NameAndLevel_ReturnsAndExists`, `GetValue_UnknownName_FalseNoWarning` (CA11); `Validate_DuplicateOrInvalidNames_TableInsideTable` (CA9); `Table_SwitchingTables_ReResolves_EditingTableBumpsVersion` (trocar de tabela ou editar a tabela dá o valor novo; a ausência de comparação de texto por avaliação é conferida na revisão do código); `Table_NoTable_Fallback_NoWarning`, `Table_UnknownName_FallbackSingleWarning` (CA8, runtime); `MaxLevel_ClampsAndWarnsOnce`, `LevelChanged_OnlyOnRealChange` (CA28, C11); `SetGetAbilityLevel_GiveWithLevel` (CA29); `TableEvaluate_DoesNotAllocate_10000Times` (parte do CA26).
- Critério de pronto: CA8 (runtime), CA9, CA11, CA28 e CA29 verdes.
- Tamanho estimado: ~480 linhas; 9 arquivos. Real: 581 linhas (161 alteradas e 420 novas); 10 arquivos (exceção única, ADR 0003, emenda de 2026-11-01). Depende de: VN2b, EA5.

### VN5 — `EffectContext`, `EffectSpec` e nível na avaliação
- Escopo: `Runtime/GAS/Effects/EffectContext.cs`, `EffectSpec.cs`, `SetByCallerValues.cs` (novos), `ActiveEffectInstance.cs` (`Context`), `AbilitySystemComponent.cs` (spec, avaliação com nível e tabela em duração, modificadores e custo), `ModifierValueSource.cs` (`Evaluate(in EffectContext)`), `GameplayEffect.cs` (`DefaultLevel`), `ApplyEffectAction.cs`, `Runtime/GAS/Abilities/GameplayAbility.cs` (`GetCooldownDuration(asc)` público avalia a duração com o nível e a tabela), `Tests/GAS/PlayMode/EffectContextTests.cs` (novo), `CHANGELOG.md`.
- Recarga: a conta de redução da recarga (`CooldownReductionPolicy`, EA6) é **uma só**, chamada pelos dois caminhos: o `GetCooldownDuration(asc)` público da habilidade e o cálculo da recarga do spec no ASC. Nenhum dos dois repete a conta.
- Testes red: `Ability_Level3_Cooldown6_Damage100` (CA7, C4); `ProjectileAtLevel3_EffectReadsLevel3` (CA13, C6); `Context_InstigatorAbilityLevelImpactPoint` (CA14, parte do contexto); `NoAbility_DefaultLevel_TableFallback` (CA16); `OldApplyEffect_SameResult` (CA17); `DurationByLevel_4sAtL1_6sAtL3_RenewKeepsApplied` (CA31, C13); `TryApplyInstantSpec_DoesNotAllocate`.
- Critério de pronto: CA7, CA13, CA16, CA17, CA31 e a parte de contexto do CA14 verdes. O manual entra no VN6, para caber no limite de arquivos.
- Tamanho estimado: ~490 linhas; 10 arquivos. Real: ~600 linhas; 11 arquivos (exceção única pela margem de ~30%, ADR 0003, emenda de 2026-11-01). Depende de: VN4, RL1, AR7.

### VN6 — SetByCaller e atributo do alvo
- Escopo: `ModifierValueSource.cs` (`SetByCaller`, `TargetAttribute`, `GetValue(in EffectContext)` público), `ActiveEffectInstance.cs` (cópia dos valores do chamador, último valor dos tiques), `AbilitySystemComponent.cs`, `Runtime/GAS.Bridges/GasPayloadSource.cs` (`ModifierMagnitude` com o contexto da instância), `Tests/GAS/PlayMode/ModifierSourcesTests.cs` (novo), `Tests/GAS/EditMode/ModifierValueSourceOrderTests.cs` (novo), `gas.md` (`#effectcontext`, SetByCaller), `CHANGELOG.md`.
- Testes red: `SetByCaller_120_DefaultWhenMissing_ZeroWithWarningWithoutDefault` (CA15, C7, runtime); `CallerValues_CopiedOnDurationInstance_LaterChangeIgnored` (R12); `Tick_UsesInstigatorAttribute`, `TargetAttribute_ReadsTarget` (CA14 completo, C8); `DestroyedInstigator_TickKeepsLastValue` (CA18); `Enum_NewValuesAtEnd_OldAssetSameValue` (CA19); `CallerLookup_DoesNotAllocate` (parte do CA26).
- Critério de pronto: CA14, CA15 (runtime), CA18 e CA19 verdes.
- Tamanho estimado: ~430 linhas; 8 arquivos. Depende de: VN5, RL6.

### VN7 — Pilhas multiplicam a magnitude (quebra isolada)
- Escopo: `GameplayEffect.cs`, `ActiveEffectInstance.cs`, `AbilitySystemComponent.cs`, `Tests/GAS/PlayMode/StackMagnitudeTests.cs` (novo), `gas.md` (pilhas e aviso de migração), `CHANGELOG.md` (quebra).
- Testes red: `Plus5_3Stacks_Is15`, `MultiplyOff_Is5`, `RemoveStacks_ReducesValue_FiresAttributeEvents` (CA24, C10); `StacksBeforeComposition_AddAndMultiply` (CA25); `Tick_MultipliedByStacks`; `OldAssetWithoutField_MultipliesByDefault` (R21).
- Critério de pronto: CA24 e CA25 verdes; testes alterados listados no PR (esperado: nenhum).
- Tamanho estimado: ~330 linhas; 6 arquivos. Depende de: VN6, AR3, EA4.

### VN8 — Magnitude ao vivo e ciclos em runtime
- Escopo: `GameplayEffect.cs` (`MagnitudeMode`), `Runtime/GAS/Effects/LiveModifierBinding.cs` (novo), `ActiveEffectInstance.cs`, `AbilitySystemComponent.cs`, `Tests/GAS/PlayMode/LiveMagnitudeTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red: `Snapshot_SourceChange_NoEffect`, `Live_SourceChange_UpdatesWithoutTick` (CA20, C9); `DefaultIsSnapshot_OldValuesSame` (CA21); `LiveCycle_CutWithSingleWarning` (CA22, runtime); `Tick_EvaluatesWithInstanceContext` (CA23); `Live_1000Recalcs_DoNotAllocate` (CA26 completo); `Snapshot_HasZeroBindings` (CA27); `LiveLevel_RecalculatesOnLevelUp_SnapshotDoesNot` (CA30); `RemoveEffect_DisposesBindings`.
- Critério de pronto: CA20 a CA23, CA26, CA27 e CA30 verdes.
- Tamanho estimado: ~480 linhas; 7 arquivos. Depende de: VN7.

### VN9 — Validação dos valores por nível
- Escopo: `GameplayAbility.cs`, `GameplayEffect.cs`, `Runtime/GAS/Abilities/AbilityAction.cs` (`CollectEffects`), `ApplyEffectAction.cs`, `FlowActions.cs`, `SpawnProjectileAction.cs`, `Runtime/GAS/Values/AbilityValues.cs` (acesso `internal` mínimo aos nomes das entradas da tabela e ao tamanho de cada array; `IsIdentifier` passa de `private` a `internal`, reaproveitado na checagem do `SetByCaller`), `Runtime/GAS/Values/ScalableFloat.cs` (acesso `internal` mínimo ao tamanho do array), `Tests/GAS/EditMode/LevelValuesValidationTests.cs` (novo), `manual/ferramentas/validador.md`, `CHANGELOG.md`. Os dois arquivos de `Values/` entraram pela exceção de 2026-11-01 da ADR 0003; a API pública não muda.
- Testes red: `UnknownTableName_ErrorOnAbility_Clickable` (CA8, validador); `EffectUsedByAbilityWithoutName_ErrorNamesEffect` (CA10, inclusive em `RepeatAction` e `OnHitActions`); `CallerWithoutDefault_Warning`, `CallerNameNotIdentifier_Error` (CA15, validador); `LiveSelfDependency_Error`, `LiveCrossPair_Error` (CA22, validador); `MaxLevelBelow1_Error`, `LiveOnInstant_Warning`, `LiveOnPeriodic_Warning`, `MaxLevelAboveArray_Warning`, `EmptyArrayInDuration_Error` (CA33).
- Critério de pronto: CA8, CA10, CA15, CA22 e CA33 completos.
- Tamanho estimado: ~420 linhas; 9 arquivos. Corrigido para ~400 linhas e 11 arquivos (exceção, 0003). Real: 559 linhas; 11 arquivos (exceção única pela margem de ~30%, ADR 0003, emenda de 2026-11-01 de linhas do VN9). Depende de: VN8, RP-07.

### VN10 — Inspector do `ScalableFloat`, da tabela e nível de prévia
- Escopo: `Editor/GAS/ScalableFloatDrawer.cs` (novo; seletor de modo, texto do array que mantém o último valor válido, lista, curva, e no modo `Table` uma lista com os nomes das tabelas das habilidades que usam o efeito, por varredura do `AssetDatabase` com cache, sem texto livre), `Editor/GAS/GameplayAbilityEditor.cs` (novo; "nível de prévia" e prévia por nível; sai de `GasEditors.cs`), `Editor/GAS/GasEditors.cs`, `Tests/GAS/EditMode/LevelValuesDrawerTests.cs` (novo), `gas.md` (Inspector, com o exemplo C4 da Fireball), `CHANGELOG.md`.
- Testes red: `ScalableDrawer_InvalidText_KeepsLastValid_ShowsError` (CA5, Editor); `ScalableDrawer_TableMode_OffersOnlyTableNames_NoFreeText` (CA12, parte automatizável); `AbilityEditor_PreviewLevelUpdatesPreview`.
- Critério de pronto: testes verdes; CA12 e CA32 (S) conferidos no host; CA35 completo (CHANGELOG com o R32, `gas.md` com o R31).
- Tamanho estimado: ~460 linhas; 6 arquivos. Real: 523 linhas e 7 arquivos, exceção única da ADR 0003 (emenda de 2026-11-01). Depende de: VN9, EA7.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| VN1 | sim | — | ~280 / 4 |
| VN2a | sim | VN1, NC7, RP-04 | ~230 / 8 |
| VN2b | sim | VN2a | ~150 / 14 (exceção, 0003) |
| VN3 | sim | VN2b, SB-T3 | ~180 / 9 |
| VN4 | sim | VN2b, EA5 | 581 / 10 (exceção, 0003) |
| VN5 | sim | VN4, RL1, AR7 | ~490 / 10 |
| VN6 | sim | VN5, RL6 | ~430 / 8 |
| VN7 | sim | VN6, AR3, EA4 | ~330 / 6 |
| VN8 | sim | VN7 | ~480 / 7 |
| VN9 | sim | VN8, RP-07 | ~400 / 11 (exceção, 0003) |
| VN10 | sim | VN9, EA7 | 523 / 7 (exceção 0003) |

Fila: VN1 → VN2a → VN2b → VN3 logo depois do Lote A; VN4 a VN10 depois do B-003, do RL1/RL6 e do AR3/AR7, com a quebra das pilhas isolada no VN7. CA34 e CA36 rodam em todos.

## Depois deste item

- **Remover os campos legados** (`legacyDuration`, `legacyConstant`) e a conversão no `OnAfterDeserialize` antes da 3.0 final, em ticket próprio (via rápida), quando o usuário confirmar que os projetos dele e o Sample rodaram o migrador. O Controlador registra no backlog.

## Respostas do gate (2026-10-31)

- Q4: proposta anterior (`%nome` ao lado do `float`) **recusada**; o usuário pediu "a opção que deixe o sistema modular sem remendos de código". Esta versão migra os campos para `ScalableFloat` (seção "Decisão da Q4"), aprovada pelo usuário.
- Q5 e Q6: aprovadas como propostas.

## Divisão do VN2 (aprovada em 2026-11-01)

- O usuário aprovou a divisão do VN2 em VN2a e VN2b, com as duas mudanças propostas: (1) o `AttributeEffectModifier` passa a classe já no VN2a, com `ValueSource = new ModifierValueSource()` como valor inicial, para o VN2b não tocar o `GameplayEffect.cs`; (2) o texto do `gas.md` sobre o valor do modificador sai do VN2b e entra no VN3, para o VN2b caber em 10 arquivos.
- Issues: o VN2a fica com a issue #148 (antes do VN2), com o título e o corpo trocados pelos do VN2a; o VN2b ganha uma issue nova, criada pelo Controlador, no épico `epic:gas-valores-por-nivel-e-effectspec`. O VN3 passa a depender do VN2b.
