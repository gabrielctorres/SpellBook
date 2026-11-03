# Spec: Modelo evento → ações → alvos
PRD: [Documentation~/prd/gas-evento-acoes-alvos.md](../prd/gas-evento-acoes-alvos.md) · DS: [Documentation~/ds/inspector-de-acoes.md](../ds/inspector-de-acoes.md) · Backlog: B-012 · ADRs: **0034 (nova, Proposta)**; usa 0003, 0004, 0007, 0009, 0012, 0013, 0014, 0016, 0018, 0019, 0022, 0026, 0030, 0032, 0033

Status: Aprovada no gate de 2026-10-31 (EV1–EV21, mais o EV22 acrescentado por decisão do gate; ADR 0034 aceita; DS aprovado; Q1: exceção única à 0003 para o EV3; Q2: sim) · Data: 2026-10-31 · Autor: Arquiteto

Emenda de 2026-10-31 (gate do Lote C, decisões do usuário):
- **EV3:** exceção única à ADR 0003 (emenda de 2026-10-31 na própria 0003), como a do 0M-54. Um ticket só, ~1100 linhas e ~22 arquivos, revisado pela lista de arquivos do escopo e pelos testes nomeados.
- **`Delayed` segura a habilidade:** a ativação só termina quando as execuções atrasadas dela terminam, e cancelar a habilidade cancela a atrasada (EV6, teste `AbilityStaysActiveUntilDelayedRuns`).
- **Indicador de "rodando" no Inspector (P-DS5):** entra como ticket pequeno, o **EV22**, depois do EV20. O EV21 passa a depender do EV22.

Emenda de 2026-10-31 (gate do Lote D, ADR 0035 aceita: filtro de candidato único). Já aplicada no Design 3, no Design 4 e nos tickets:
- **EV4b:** o `AreaSelector` nasce sem o `GameplayTagQuery filter`. O teste de tags sai daqui, e o de alocação perde o filtro (~420 linhas).
- **EV8:** o `AreaSelector` ganha o filtro de candidato `Condition<ConditionContext>`, e a `TagQueryCondition` ganha `ConditionSubject` (`Owner`, padrão, ou `TargetUnit`). Entram o teste de tags no alvo (CA12) e o de alocação com filtro (CA34). Fica com ~450 linhas e 9 arquivos.
- **EV16:** aviso para a `TagQueryCondition` com sujeito `TargetUnit` usada fora de um filtro de candidato, emitido pelos donos que o EV16 já toca (`GameplayAbility.Validate` nas regras de ativação, `ConditionalAction.Validate`). Nenhum arquivo novo.
- **Design 4:** `PlayCueAction` e `SpawnVFXAction` continuam migrados no EV2 e no EV3, porque a troca é atômica, e saem do GAS no CU9 do B-013 (ADR 0037).
- As issues do EV4b (#178) e do EV8 (#182) são atualizadas pelo Controlador; a do EV16 também ganha a regra nova.

Emenda de 2026-11-02 (decisão do usuário): o EV1 foi dividido em EV1a (issue #174, renomeada) e EV1b (issue #307), depois de uma implementação com ~805 linhas em 8 arquivos. O design não muda. A ordem nova do lote 8 foi aprovada no gate parcial de 2026-11-02 (E1 e E2 como propostos). Detalhes em "EV1 — dividido em EV1a e EV1b" e na seção "Emenda de 2026-11-02: EV1 dividido e lote 8 revisto".

Emenda de 2026-11-02 (decisão do usuário, opção (c) e proposta WAIT): o EV3 faz só a troca atômica da base, com o mesmo comportamento, e a `WaitAction` continua com `float`. As listas `OnEnd` e `OnCancel` passam para o EV3c, e a `WaitAction` com `ScalableFloat` e a migração dos assets passam para o EV3d. Os dois entram no lote 9, depois do XR3 e antes do EV4a. A exceção de tamanho do EV3 foi ampliada na ADR 0003 (emenda da mesma data). Detalhes na seção "Emenda de 2026-11-02: EV3 só com a troca, EV3c e EV3d".

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`. As linhas são as de hoje; os Lotes A e B, o B-009 e o B-010 mudam parte delas antes deste item.

- `Runtime/GAS/Abilities/AbilityAction.cs:14-56`: `IEnumerator Execute(AbilityContext)`, `OnCancel`, `Validate`; `Run`/`RunSequence` registram em `context.RunningActions`. O VN9 acrescenta `CollectEffects`.
- `Runtime/GAS/Abilities/AbilityContext.cs:8-79`: `Source`, `TargetPosition`, `Targets`, `CustomData`, `Ability`, `Spec`, `Level`, `IsCancelled`, `RunningActions`, `CreateDerived`, `WaitSeconds` (`:52-67`, mede a partir do quadro em que a corrotina retoma) e `StartChildRoutine` (`:72-78`).
- `Runtime/GAS/Core/AbilitySystemComponent.cs:248-284`: `ActivateSpec` faz o commit e roda `StartCoroutine(RunAbility)`; `:291-314` `CancelAbility` para as corrotinas e chama `OnCancel` em ordem reversa; `:350-361` `EndAbility`.
- `Runtime/GAS/Abilities/AbilitySpec.cs:17-18`: `Routine` e `ChildRoutines` (corrotinas da Unity).
- `Runtime/GAS/Abilities/Actions/FlowActions.cs`: `RepeatAction` (`:14-33`, `Interval` por `WaitSeconds`), `ParallelAction` (`:39-70`, `StartChildRoutine` e um `int[]` por execução), `ConditionalAction` (`:76-116`, enum).
- `Runtime/GAS/Abilities/Actions/`: `WaitAction`, `AnimationAction`, `TargetingAction` (`:9-47`, usa `TargetingStrategy`), `ApplyEffectAction`, `PlayCueAction`, `SpawnProjectileAction` (`:40-110`, `DelayBetweenShots` por `WaitSeconds`).
- `Runtime/GAS/Abilities/Projectiles/Projectile.cs:312-329`: `HandleImpact` roda `OnHitActions` em `Source.StartCoroutine` com `CreateDerived`.
- `Runtime/GAS/Effects/EffectAction.cs:8-22`: síncrona; `ActiveEffectInstance.cs:35-42`, `:96-99`, `:104-122` chamam `Execute`/`Cleanup`; `:16` guarda dados por ação num `Dictionary`. `CommonEffectActions.cs`: `DebugLogEffectAction`, `SpawnVFXEffectAction`. `GameplayEffect.cs:48-59`: `OnApplication`, `OnTickActions`, `OnRemoval`.
- `Runtime/GAS/Targeting/`: `TargetingStrategy` (`ScriptableObject`, buffers estáticos, `HashSet` de vistos), `CircleTargeting`, `MouseTargetingStrategy` (aloca uma lista por chamada, `:22`).
- `Runtime/GAS.Bridges/RaiseEventAction.cs` e, do RL6, `GasPayloadSource.cs` (a `RaiseEventEffectAction.cs` não é criada: emenda de 2026-11-01); do RL7, `Editor/GAS.Bridges/RaiseEventDrawer.cs`.
- Editor: `Editor/GAS/GasEditors.cs` (Inspectors de uma linha); `GameplayAbilityEditor.cs` (VN10, AV9); `Editor/Core/Inspector/` (seletor de tipo, `SummaryFormatter` do AV1). `TabView` existe no `UnityEngine.UIElements` do host (6000.3.13, `UnityEngine.UIElementsModule.xml:20125`) e é da API desde a 2023.2, então vale na 6000.0 (ADR 0010).
- Testes que usam as classes de hoje: `Tests/GAS/PlayMode/AbilityWaitTests.cs`, `SpawnActionsTests.cs`, `Tests/GAS.Bridges/EditMode/RaiseEventActionTests.cs`, `Tests/GAS.Bridges/PlayMode/RaiseEventAbilityTests.cs`, `Tests/Core/EditMode/Inspector/TypePickerCatalogTests.cs:173` (`PickEffectAction : EffectAction`), `Tests/Testing/GasFactory.cs:106-145`, e os do Lote B que tocam ações (VN5 `ProjectileAtLevel3_EffectReadsLevel3`, VN9, RL6, RL7).
- Assets do pacote com ações: `Samples~/Sandbox/Data/Abilities/GA_BolaDeFogo.asset` (`SpawnProjectileAction`, `ApplyEffectAction`, `RaiseEventAction`) e `Samples~/Sandbox/Data/Effects/GE_Queimadura.asset` (`DebugLogEffectAction`).

## Respostas às perguntas técnicas do PRD (seção 8), ao critério do B-030 e ao DS (seção 10)

- **Critério do B-030 (R6, R7).** ADR 0034, item 3: cada execução tem um **cursor lógico** de tempo. Começa no `TickedTime` do ASC dono; passo instantâneo não o move; `Wait(s)` termina quando `TickedTime ≥ cursor + s` e passa o cursor a `cursor + s`. Uma espera conta do fim do passo anterior, um `Tick` no mesmo quadro conta, e o excesso de um `Tick` grande passa adiante. `Wait`, `Repeat.Interval` e `DelayBetweenShots` usam o mesmo `ActionStatus.Wait`, sem caso especial. Testes nomeados no EV3.
- **P6. Quadro entre ações sem espera.** Confirmado (R8): ações consecutivas sem espera rodam na mesma chamada. `Repeat` com `Count = 3` e `Interval = 0` roda as três repetições no mesmo quadro; `Wait(0)` não cede quadro. Testes que mudam: lista no Design 11.
- **P7. Contexto.** Dois contextos (ADR 0034, item 4; confirma a 0030): `ActionContext` (classe, em pool, mutável) substitui o `AbilityContext`; o `EffectContext` continua imutável e vai por cópia no `ActionContext` quando a execução vem de um efeito. O contexto da `Condition` é o `ConditionContext` do B-009 (ADR 0032), que ganha a execução em curso (EV7).
- **P8. Modelo de execução.** Executor próprio no ASC, avançado pelo `Tick`, com pilha de quadros por execução (ADR 0034, itens 1 a 3). Design 1.
- **P9. Seletor e aleatoriedade.** O `TargetSelector` **substitui** a `TargetingStrategy` (ADR 0034, item 7); a `TargetingAction` continua preenchendo os alvos do evento, agora com um seletor. A fonte de aleatoriedade é `SpellBook.Core.IRandomSource`, uma por ASC, trocável por `SetRandomSource` (ADR 0034, item 8).
- **P10. Eventos por dono.** Lista fechada no Design 2 (igual à tabela do DS 3.2, mais o "Ao terminar" do projétil na Fase 8).
- **P11. Gatilhos.** Disparo síncrono, na hora do evento, com duas travas: o spec que está sendo disparado ignora um novo disparo para ele mesmo até a tentativa acabar, e a profundidade de disparos aninhados no ASC tem teto 8 (aviso único). O alvo vem do `GameplayEventData` (unidade e ponto) como `AbilityTarget`.
- **P12. Reações do efeito.** `OnTakeDamage`: uma vez por alteração original com `Requested < 0` vinda de efeito (o `AttributeChangeReport` do B-010 com `RedirectedFrom` nulo), filtrada pela lista opcional `DamageAttributes` do efeito (vazia: qualquer atributo). `OnDeath`: quando o `DeathAttribute` do efeito dispara `OnAttributeDepleted` (B-010). `OnAbilityExecuted`: no `OnAbilityActivated` do dono. As reações rodam **depois** dos eventos públicos do ASC para a mesma causa (ordem fixa).
- **P13. Cancelamento.** O dono da execução é um **dono lógico** (ativação de habilidade ou instância de efeito). Cancelar o dono cancela todas as execuções dele, inclusive `Delayed` pendentes, com `OnCancel` em ordem reversa; a lista de execuções do ASC é percorrida de trás para frente com guarda de índice, então um `OnCancel` que inicia ou cancela outra execução não corrompe a iteração. `Delayed` com "continuar" vira execução sem dono.
- **P14. Quebra dos assets.** As classes concretas mantêm o nome, e as que mudam de namespace ou de nome ganham `[MovedFrom]` (`UnityEngine.Scripting.APIUpdating`), para referências `[SerializeReference]` antigas continuarem a abrir quando os campos batem. Mesmo assim, os dois assets do Sample são **refeitos** no EV21 (R1). Nenhum teste da suíte carrega YAML de ação (o Fábrica confirma por busca no EV3).
- **P15. Projétil.** `HandleImpact` monta um `ActionContext` derivado e chama `Source.RunActions(OnImpact, contexto, dono: null)`: a execução vive no ASC de quem lançou, sem vínculo de cancelamento com a habilidade (como hoje) e sem `StartCoroutine`.
- **P16. Autocast no `Tick`.** Ordem do `Tick`: tempo, cargas (B-009), efeitos, execuções, canalização, e por último a passada "quando pronta" (autocast e passiva que se reativa). Cada spec guarda o índice do último `Tick` em que tentou, então há no máximo uma tentativa por `Tick`. A passada usa a mesma trava por spec dos gatilhos (P11). Tentativas de autocast são silenciosas.
- **P-DS1. Resumo da linha.** Modelo declarativo `[TypeCategory(..., Line = "...")]` resolvido pelo `SummaryFormatter` (AV1) sobre o `SerializedProperty`, com valores ("Esperar {Duration}" mostra "Esperar 2 s" ou "Esperar %tempo"). Sem método de resumo no runtime (ADR 0034, item 9). Tipo sem `Line` mostra o nome do tipo, como o DS pede.
- **P-DS2. Abas.** `TabView`/`Tab` do UI Toolkit, confirmados acima.
- **P-DS3. Atributo novo.** Nenhum atributo novo no Core além da propriedade `Line` do `TypeCategory` (AV1). Os eventos são marcados com `[ActionEvent]` no `SpellBook.GAS` (é dado do GAS, não do Inspector).
- **P-DS4. Desempenho.** `ListView` virtualizado (`makeItem`/`bindItem`); o resumo de cada linha é calculado no `bind` e guardado até o `SerializedObject` mudar (`TrackSerializedObjectValue`). Verificação S com 50 ações (EV17).
- **P-DS5. Indicador de "rodando".** ~~Fica fora deste item~~ **Entra no EV22** (decisão do gate de 2026-10-31). Design 10a.
- **P-DS6. Reordenar entre listas e copiar/colar.** Entram, em tickets próprios (EV19, EV20); o DS continua a referência.

## Design

### 1. Ação, contexto e executor (R1 a R8; ADR 0034)

Em `Runtime/GAS/Actions/` (namespace `SpellBook.GAS.Actions`):

```csharp
[Serializable]
public abstract class GameplayAction : IValidatable
{
    [SerializeReference, TypePicker] private TargetSelector target;          // EV4a
    public TargetSelector Target => target;
    public virtual bool UsesTargets => true;                                  // chip do Inspector
    protected internal abstract ActionStatus Execute(ActionContext context, ref ActionFrame frame);
    protected internal virtual void OnCancel(ActionContext context, ref ActionFrame frame) { }
    public virtual void Validate(ValidationResult result) { }
    public virtual void CollectEffects(List<GameplayEffect> results) { }      // vem do VN9
}

public struct ActionFrame { public int Phase; public int Index; public int Count; public object Data; }

public readonly struct ActionStatus
{
    public static ActionStatus Done { get; }
    public static ActionStatus Wait(float seconds);                           // pelo cursor lógico
    public static ActionStatus Run(IReadOnlyList<GameplayAction> children);   // em sequência; volta a esta ação depois
    public static ActionStatus RunParallel(IReadOnlyList<GameplayAction> children); // todas; volta quando todas acabam
}
```

Emenda de 2026-11-02 (ADR 0034, emenda "ponto de extensão `protected`"): o `Execute` e o `OnCancel` passam a `protected` (`protected abstract` e `protected virtual`), e o executor os chama por dois métodos `internal` não virtuais da base. A assinatura acima vale com essa troca:

```csharp
protected abstract ActionStatus Execute(ActionContext context, ref ActionFrame frame);
protected virtual void OnCancel(ActionContext context, ref ActionFrame frame) { }
internal ActionStatus InvokeExecute(ActionContext context, ref ActionFrame frame) => Execute(context, ref frame);
internal void InvokeCancel(ActionContext context, ref ActionFrame frame) => OnCancel(context, ref frame);
```

- **Executor** (`ActionRunner`, interno): cada execução tem a pilha de quadros (lista reutilizada), o cursor lógico, o dono lógico e o `ActionContext`. `Advance(TickedTime)` chama `Execute` do quadro do topo enquanto o resultado não for uma espera ainda não vencida. `Run` empilha os filhos; `Done` desempilha e chama o pai de novo (o pai decide pela `Phase`); `RunParallel` cria execuções filhas ligadas à mãe. Entrada nula numa lista é pulada.
- **ASC:** `ActionExecutionHandle RunActions(IReadOnlyList<GameplayAction> actions, ActionContext context, object owner)`, `void CancelActions(object owner)`, `bool HasRunningActions(object owner)`. O `Tick` avança as execuções depois dos efeitos. Execuções e contextos vêm de pools do ASC; uma execução nova sem nada no pool aloca uma vez (N1).
- **`ActionContext`** (classe; substitui o `AbilityContext`): `Instigator` (quem lançou: o dono da habilidade, ou quem aplicou o efeito), `Owner` (ASC onde roda), `EventTargets` (lista do evento), `Targets` (os alvos resolvidos para a ação corrente), `Point`/`HasPoint`, `Ability`, `Spec`, `Level`, `EffectInstance`, `HasEffect`/`Effect` (`EffectContext` por cópia), `EventInstigator` (quem causou o evento de reação ou de gameplay), `Random`, `IsCancelled`, `CustomData` (criado sob demanda). `CreateDerived(Vector3 ponto)` mantém habilidade, nível e instigador, como o NC6.
  - **Emenda de 2026-11-01 (perguntas do RL6; pendente de gate parcial):** o `ActionContext` também leva `GameplayEffect EffectDefinition` (o asset do efeito, preenchido em toda execução vinda de um efeito, **inclusive no instantâneo**, em que `EffectInstance` é nulo) e `EffectEvent EffectEvent` (`enum EffectEvent { None, Applied, Tick, Removed, TakeDamage, Death, AbilityExecuted }`, declarado em `ActionContext.cs`; `None` fora de efeito). Quem dispara a lista preenche os dois (EV2; as reações, no EV11). Assim uma ação sabe o efeito, o nível (pelo `EffectContext`) e se roda num tique sem procurar a si mesma numa lista. Motivo: Design 4, emenda, da Spec `gas-resultado-de-aplicacao-e-relay.md`.
- `EffectSpec.FromAbility` (VN5) passa a receber `ActionContext`.
- Ordem reversa no cancelamento: `OnCancel` dos quadros do topo para a base, de cada execução, e das filhas antes da mãe (N7).

### 2. Eventos por dono (R2, R22; P10)

Cada lista é um campo `[SerializeReference, TypePicker] List<GameplayAction>` com `[ActionEvent(string label, DefaultTarget, bool hasEventTargets)]` (`Runtime/GAS/Actions/ActionEventAttribute.cs`).

| Dono | Campo (aba) | Alvos do evento | Alvo padrão (sem seletor) |
|---|---|---|---|
| `GameplayAbility` | `OnActivate` (Ao ativar; era `Actions`) | unidade do `AbilityTarget`, ou nenhum | alvos do evento |
| | `OnEnd` (Ao terminar), `OnCancel` (Ao ser cancelada) | os da ativação | alvos do evento |
| | `OnGranted` (Ao ser concedida), `OnRemoved` (Ao ser retirada) | nenhum | dono |
| | `OnChannelTick` (A cada passo da canalização; só `Channel`) | os da ativação | alvos do evento |
| `GameplayEffect` | `OnApplied`, `OnTick`, `OnRemoved` (eram `OnApplication`, `OnTickActions`, `OnRemoval`) | dono do efeito | dono do efeito |
| | `OnTakeDamage` (Ao receber dano), `OnDeath` (Ao morrer) | quem causou | dono do efeito |
| | `OnAbilityExecuted` (Ao executar habilidade) | alvo da ativação | dono do efeito |
| `Projectile` | `OnImpact` (No impacto; era `OnHitActions`) | quem foi atingido | alvos do evento |

Campos renomeados levam `[FormerlySerializedAs]` do nome antigo. Ao terminar normalmente roda `OnEnd`; ao ser cancelada roda `OnCancel` (lista), depois dos `OnCancel` das ações em curso. Emenda de 2026-11-02: `OnEnd` e `OnCancel` entram no EV3c, e não no EV3; a ordem exata está no EV3c.

### 3. Alvos por ação (R9 a R13)

- `SpellBook.Core.IRandomSource` (`float Value()` em [0, 1), `int Range(int maxExclusive)`) e `SeededRandomSource` (xorshift, semente no construtor). ASC: `IRandomSource Random`, `SetRandomSource(IRandomSource)`; padrão: `SeededRandomSource` com semente de `System.Environment.TickCount` no `Awake`.
- `Runtime/GAS/Targeting/TargetSelector.cs`: `[Serializable] abstract class TargetSelector { public abstract void Select(ActionContext context, List<AbilitySystemComponent> results); public virtual bool NeedsEventTargets => false; }`. Prontos: `CasterSelector`, `EventTargetsSelector`, `PointSelector` (não escolhe unidade; a ação usa `context.Point`) e `AreaSelector`.
- `AreaSelector`: origem (`Caster`, `Point`, `FirstEventTarget`), `PhysicsMode` (2D ou 3D), `ScalableFloat radius`, ~~`GameplayTagQuery filter` (exigidas e ignoradas)~~ `[SerializeReference, TypePicker] Condition<ConditionContext> filter` (filtro de candidato da ADR 0035, avaliado com `Owner` = quem procura e `Target` = `AtUnit(candidato)`; nulo aceita todos; entra no EV8, emenda de 2026-10-31), `includeOwner`, `int maxTargets` (0 ilimitado), `Order` (`Nearest`, `Random`), `LayerMask layers`. Buffers de física e de vistos **por ASC** (não estáticos), ordenação por inserção sem `Comparison` alocado, sorteio por Fisher-Yates parcial com o `IRandomSource`.
- O executor resolve o seletor quando a ação começa (`Phase == 0`) para um buffer do quadro e expõe em `context.Targets`; sem seletor, usa o alvo padrão do evento (tabela acima) ou o alvo corrente do `ForEachTarget`. `TargetingAction` ganha `[SerializeReference] TargetSelector selector` e o modo `Replace`/`Append` sobre os `EventTargets`.
- `TargetingStrategy`, `CircleTargeting` e `MouseTargetingStrategy` saem (o caso "mais perto do ponto" é `AreaSelector` com origem `Point`, `maxTargets = 1`, `Nearest`). A relação de time é a `TeamRelationCondition` do Bridges (B-011) posta no `filter` (ADR 0035, emenda de 2026-10-31). Tags do alvo usam a `TagQueryCondition` com sujeito `TargetUnit`.

### 4. Ações novas e migradas (R14 a R19)

- Migradas para a base nova, mesmo comportamento: `WaitAction` (`ScalableFloat Duration`; emenda de 2026-11-02: o EV3 a migra com `float`, e o `ScalableFloat` entra no EV3d), `AnimationAction`, `TargetingAction`, `ApplyEffectAction`, `PlayCueAction`, `SpawnProjectileAction` (`DelayBetweenShots` por `Wait`), `RepeatAction`, `ParallelAction`, `ConditionalAction`, `DebugLogAction` (era `DebugLogEffectAction`), `SpawnVFXAction` (era `SpawnVFXEffectAction`; o VFX para devolver na remoção fica nos dados da instância de efeito, como hoje), `RaiseEventAction` (Bridges; a `RaiseEventEffectAction` do RL6 é fundida nela no EV15: a fonte de payload lê a instância de efeito do contexto quando existe). **Emenda de 2026-11-01:** a `RaiseEventEffectAction` não chega a existir; o EV15 acrescenta à `RaiseEventAction` as fontes do lado do efeito, lidas de `EffectDefinition`, `EffectInstance`, `Effect` e `EffectEvent`. **Emenda de 2026-10-31 (ADR 0037):** `PlayCueAction` e `SpawnVFXAction` são migrados aqui só para a troca atômica compilar, e saem do GAS no CU9 do B-013.
- `ChanceAction`: `ScalableFloat probability`, roda as filhas se `Random.Value() < p` (C6).
- `DelayedAction`: `ScalableFloat delay`, `bool continueIfOwnerEnds`; agenda as filhas numa execução nova com o cursor em `cursor + delay` e devolve `Done` na hora (C7). A execução agendada pertence ao mesmo dono, e a **ativação só termina quando as execuções dela terminam** (a ação seguinte roda no mesmo quadro; a habilidade continua ativa até a atrasada rodar). Com "continuar", a execução fica sem dono.
- `ForEachTargetAction`: seletor próprio (ou os alvos do evento); copia o conjunto para o buffer do quadro **antes** de começar (reentrada segura, R16) e roda as filhas uma vez por alvo, com o alvo corrente como padrão (C8).
- `RemoveEffectAction`: `GameplayEffect effect` ou `GameplayTag tag`; usa `RemoveEffects`/`RemoveEffectsWithTag` (EA3) em cada alvo; grava o total em `context.LastRemovedStacks` (C9).
- `ConditionalAction`: `[SerializeReference] Condition<ConditionContext> condition`, `Then`, `Else`; o enum `ConditionType` sai (R18). `ConditionContext` ganha `Action` (o `ActionContext`) e `Targets` passa a ser `context.Targets`.
- `CommitAction` (Design 7) e `SendGameplayEventAction` (Design 5).
- Todas com `[TypeCategory(<categoria do DS 6.1>, Line = ...)]` e `[MovedFrom]` quando o nome ou o namespace muda.

### 5. Eventos de gameplay e gatilhos (R20, R21)

- `GameplayEventData` (`readonly struct`): `Instigator`, `Target` (ASC), `HasPoint`, `Point`, `Magnitude`, `Tags` (`GameplayTagContainer`, pode ser nulo), `HasEffectContext`, `EffectContext`.
- ASC: `void SendGameplayEvent(GameplayTag tag, in GameplayEventData data)` e `event GameplayEventHandler OnGameplayEvent` (`delegate void GameplayEventHandler(GameplayTag tag, in GameplayEventData data)`). Tag inválida: ignorada. Sem ouvinte nem gatilho: nada, sem alocar.
- `Runtime/GAS/Abilities/Triggers/AbilityTrigger.cs`: `[Serializable] abstract class AbilityTrigger { bool onlyWithAutocast; }`. Prontos: `GameplayEventTrigger(GameplayTag)`, `TagAddedTrigger(GameplayTag)`, `TagRemovedTrigger(GameplayTag)`, `WhenReadyTrigger(TargetSelector)` (EV14). `GameplayAbility.Triggers` (`[SerializeReference]`).
- O ASC indexa os gatilhos na concessão e desfaz na retirada (lista de pares spec–gatilho, sem alocar por evento). Disparo: monta o `AbilityTarget` (unidade `data.Target`, ponto `data.Point` ou a posição da unidade) e chama o caminho único do B-009 (`TryActivateInternal(spec, target, origem)`), com as travas da P11. Falha de gatilho comum dispara `OnAbilityFailed`; falha de gatilho "só com autocast" é silenciosa.
- `SendGameplayEventAction`: tag, alvo (seletor), magnitude (`ScalableFloat`); envia ao ASC de cada alvo.

### 6. Reações do efeito (R22)

`GameplayEffect` ganha `OnTakeDamage`, `OnDeath`, `OnAbilityExecuted` (Design 2), `List<AttributeDefinition> damageAttributes` e `AttributeDefinition deathAttribute`. O ASC mantém a lista das instâncias ativas com alguma reação (atualizada na aplicação e na remoção) e, nas causas da P12, roda a lista da instância com `EventInstigator` = instigador do relatório (dano, morte) e `context.Effect` = o contexto da instância. Reação que causa dano roda um pipeline aninhado (teto do B-010).

### 7. Comportamentos, alcance, unidade-alvo e commit (R23 a R30)

- `AbilityBehavior { Instant, Passive, Toggle, Channel }` (padrão `Instant`).
- **Passive:** ativada pelo caminho único (silencioso) logo depois de `OnAbilityGranted`; fica ativa até a retirada (roda `OnEnd`) ou o cancelamento. `bool reactivatePassive`: a passada "quando pronta" (EV14) tenta de novo enquanto inativa. Custo ou recarga configurados são cobrados (o validador avisa).
- **Toggle:** `TryActivateAbility` numa habilidade ligada **desliga** sem checagens: cancela as execuções da ativação (`OnCancel` reverso), roda a lista `OnCancel`, devolve as `ActivationOwnedTags` e dispara `OnToggleChanged(spec, false)`. Ligar passa pela checagem e pelo commit (custo só ao ligar, P3); a ativação não termina quando `OnActivate` acaba. `IsToggledOn(spec)`.
- **Channel:** `ScalableFloat channelDuration` (0: até cancelar, P4) e `float channelTickInterval` (0: sem passos). O ASC conta a canalização pelo cursor lógico; cada passo roda `OnChannelTick`; ao fim cancela o que ainda roda de `OnActivate`, roda `OnEnd` e termina (não cancelada). Leitura para a UI (P7 do B-016): `AbilitySpec.IsChanneling`, `ChannelDuration` (aplicada; 0 é indeterminada), `GetChannelElapsed(spec)`, `GetChannelRemaining(spec)`, `GetChannelNormalized(spec)`; início e fim pelos `OnAbilityActivated` e `OnAbilityEnded(spec, cancelado)`.
- Toggle e Channel concedem e devolvem `ActivationOwnedTags` pelo mesmo caminho da instantânea (R27).
- **Unidade-alvo (R28):** `AbilityTarget` ganha `Unit` (ASC) e `AtUnit(asc)`; sem ponto explícito, o ponto é a posição da unidade. A ativação põe a unidade em `EventTargets` de `OnActivate`.
- **Alcance (R29):** `CastRangeCondition` (B-009) ganha `RangeMeasure { Center, Edge }`; com unidade, mede até o centro dela ou até o ponto mais próximo do colisor dela (`Collider.ClosestPoint`/`Collider2D.ClosestPoint`; sem colisor, centro). O alcance continua o `GameplayAbility.CastRange` (B-009).
- **Commit (R30):** `CommitMode { OnStart, Manual }` (padrão `OnStart`). Com `Manual`, `ActivateSpec` não cobra; a `CommitAction` chama `CommitAbility(spec)`, que confere carga e custo de novo (sem tags e sem condições), cobra uma vez por ativação (`spec.IsCommitted`) e, se falhar, cancela a habilidade e dispara `OnAbilityFailed` com `NoCharges` ou `CannotAffordCost`. Cancelar antes do commit não cobra nada.

### 8. Autocast (R35 a R40)

- `GameplayAbility`: `AutocastConfig autocast` (`allowed = false`, `startsOn = false`).
- `AbilitySpec`: `AutocastEnabled` (nasce de `startsOn` quando `allowed`).
- ASC: `bool SetAutocast(AbilitySpec spec, bool on)` (falso sem `allowed`, em `Passive`/`Toggle`, ou sem mudança), `bool IsAutocastEnabled(AbilitySpec spec)`, `event Action<AbilitySpec, bool> OnAutocastChanged`.
- Gatilho "só com autocast" é ignorado com o autocast desligado. `WhenReadyTrigger`: na passada final do `Tick`, para cada spec inativo com o gatilho elegível e sem tentativa neste `Tick`: resolve o seletor a partir de quem lança (sem seletor: alvo nenhum; seletor sem resultado: pula, sem custo e sem evento) e chama o caminho único em modo silencioso. Habilidade sem `allowed` e sem `WhenReadyTrigger` não entra na lista da passada (CA42: nada por `Tick`).
- A ordem entre várias habilidades é a da concessão; prioridade e IA são do jogo.

### 9. Validação (R32)

`GameplayAbility.Validate`, `GameplayEffect.Validate` e o `Validate` de cada ação, seletor e gatilho cobrem a lista do R32 e o aviso de profundidade do DS (4.4): ação nula; lista de `OnChannelTick` com ações em habilidade que não é `Channel` (aviso, "N ações em 'A cada passo' não vão rodar"); `EventTargetsSelector` em evento sem alvos do evento (`hasEventTargets = false`, erro); `ForEachTarget` sem filhas (aviso); `Delayed` com tempo constante negativo (erro); `Chance` com constante fora de 0 a 1 (erro); `GameplayEventTrigger` com tag inválida (erro); gatilho por evento de gameplay cuja tag a própria habilidade envia por `SendGameplayEventAction` ao dono (aviso: a trava evita o laço, mas a habilidade nunca se redispara); Passive com custo ou recarga (aviso); Channel sem duração e sem `CancelledByTags` (aviso); `Manual` sem `CommitAction` (erro); `CommitAction` com `OnStart` (aviso); `RemoveEffect` sem efeito e sem tag (erro); autocast permitido em `Passive`/`Toggle` (erro); autocast permitido sem `WhenReadyTrigger` nem gatilho "só com autocast" (aviso); `WhenReadyTrigger` com seletor e `CastRange` ≤ 0 (aviso); `OnDeath` com ações e sem `deathAttribute` (erro); profundidade de ações aninhadas acima de 4 (aviso). Mensagens com `propertyPath`.

### 10. Inspector (R31; DS)

Em `Editor/GAS/Actions/` (UI Toolkit, herdando do `SpellBookEditor`), um componente para todo dono: `ActionEventsView` descobre os campos `[ActionEvent]` do alvo e monta um `TabView` (uma aba por evento; contador, ponto de validação, subtítulo do alvo padrão, aba lembrada por asset na sessão por `SessionState`, abas de reação agrupadas no efeito, aba de canalização só com `Channel` e aviso quando oculta com ações). `ActionListView` (`ListView` reordenável, virtualizado), `ActionRowView` (alça, ícone e barra de categoria, verbo e objeto pelo `SummaryFormatter`, chip de alvo, condição, ícone de validação, menu), `TargetChipView` (popup de seletor com "(Padrão do evento)", bloco "Alvo" para seletores com campos). Cores e ícones por tokens USS em `Editor/GAS/Actions/ActionInspector.uss`, com valores para tema claro e escuro (DS 6). `GameplayAbilityEditor` e um `GameplayEffectEditor` próprio (sai de `GasEditors.cs`) usam o componente na ordem do DS 8; o `ProjectileEditor` também. Gatilhos e regras de ativação usam a mesma linha.

### 10a. Indicador de "rodando" em Play (P-DS5; DS seção 9, linha "Modo Play")

- **Leitura no runtime:** `AbilitySystemComponent.CollectRunningActions(List<GameplayAction> into)` limpa a lista e acrescenta, para cada execução viva do ASC, as ações de todos os quadros da pilha (a ação folha e as de fluxo que a contêm, como `Repeat` ou `ForEachTarget`). Sem alocação depois do aquecimento; não expõe a pilha nem o `ActionFrame`. As ações são as instâncias `[SerializeReference]` do próprio asset (não há clonagem), então a identidade por referência basta para ligar execução e linha.
- **Editor:** `Editor/GAS/Actions/RunningActionsTracker.cs` (`internal sealed`). Só em Play: a cada 200 ms (`schedule.Every`, no padrão do Event Debugger), lê os ASCs vivos por `Object.FindObjectsByType<AbilitySystemComponent>(FindObjectsSortMode.None)`, junta as ações num `HashSet<GameplayAction>` reutilizado (comparação por referência) e avisa as linhas visíveis. Fora do Play, ou ao sair do Play, para o agendamento e limpa o conjunto. A fonte de ASCs é uma costura interna (`Func<IReadOnlyList<AbilitySystemComponent>>`) para os testes EditMode não dependerem de cena.
- **Linha:** `ActionRowView` ganha um ponto pequeno à direita do verbo, com a classe USS `action-row--running` (token de cor nos dois temas). Ele não muda o layout nem a altura da linha. A linha recolhida de uma ação de fluxo também acende quando uma filha roda. Nada muda nos campos: continuam editáveis em Play, como o DS pede.

### 11. Testes que mudam (N8) e quebras

- `AbilityWaitTests.cs`: `WaitAction_ZeroDuration_WaitsOneFrame` vira `WaitAction_ZeroOrNegative_CompletesInSameCall` (R8); `RepeatAction_Interval_CountsAscTicks` muda só o comentário do quadro; `WaitSeconds_CountsRemainingAcrossTicks` e `WaitSeconds_SourceDestroyed_Ends` são substituídos por testes do executor (o `WaitSeconds` some); `CountingAction` passa à base nova; `Projectile_*` trocam `List<AbilityAction>` por `List<GameplayAction>`. `SpawnProjectile_DelayBetweenShots_CountsAscTicks` **não muda** (CA8).
- `SpawnActionsTests.cs`, `RaiseEventActionTests.cs`, `RaiseEventAbilityTests.cs`, `TypePickerCatalogTests.cs` (`PickEffectAction`), `GasFactory.cs` (`CreateAbility(tag, params GameplayAction[])`, `CreateActionContext`), e os testes do Lote B que usam `AbilityAction`, `AbilityContext` ou `EffectAction` (lista no PR do EV2 e do EV3).

## Quebras de compatibilidade

Para o CHANGELOG e `manual/regras/migracao-2-para-3.md` (ADR 0009, I1):
- **`AbilityAction` e `EffectAction` deixam de existir;** a base é `GameplayAction` (`Execute(ActionContext, ref ActionFrame)` devolvendo `ActionStatus`). Ações próprias são reescritas: uma tabela "antiga → nova" por ação do pacote na página de migração.
- **`AbilityContext` vira `ActionContext`;** `WaitSeconds` e `StartChildRoutine` saem (use `ActionStatus.Wait`/`RunParallel`).
- **Campos renomeados:** `GameplayAbility.Actions` → `OnActivate`; `GameplayEffect.OnApplication`/`OnTickActions`/`OnRemoval` → `OnApplied`/`OnTick`/`OnRemoved`; `Projectile.OnHitActions` → `OnImpact`.
- **Fim da `TargetingStrategy`** (e `CircleTargeting`, `MouseTargetingStrategy`): use `AreaSelector`.
- **`ConditionalAction` sem enum:** use `Condition`.
- ~~**`RaiseEventEffectAction` fundida em `RaiseEventAction`.**~~ Emenda de 2026-11-01: não há quebra; a `RaiseEventEffectAction` nunca foi publicada. Adicionado: payload dinâmico da `RaiseEventAction` em listas de efeito.
- **Tempo:** ações sem espera não custam mais um quadro (R8); esperas contam do fim do passo anterior, inclusive um `Tick` no mesmo quadro (R6).
- **`WaitAction.Duration` vira `ScalableFloat`** (EV3d, emenda de 2026-11-02). O `float` salvo nos assets é convertido no carregamento, e o menu `SpellBook > Migrar assets do GAS (3.0)` passa a regravar também os `GameplayAbility`. Código que atribui `Duration = 2f` passa a usar `ScalableFloat.Constant(2f)`.
- Adicionado: seletores, `IRandomSource`, `Chance`, `Delayed`, `ForEachTarget`, `RemoveEffect`, `Commit`, `SendGameplayEvent`, gatilhos, reações, comportamentos, autocast, unidade-alvo, medida até a borda, `CommitMode`, leitura de canalização.

## Tickets

Todos usam a Unity e seguem o WIP 1. **Não há par paralelo sem Editor nesta Spec.** Cada ticket escreve a sua parte de `gas.md` e do `CHANGELOG.md` e anota as ações que cria com `[TypeCategory(..., Line)]`. Tempo por `asc.Tick` com `ManualTimeSource`; aleatoriedade por `SeededRandomSource` com semente fixa; objetos pela `GasFactory`. Teto: ~500 linhas e 10 arquivos, **salvo o EV3**: exceção única à ADR 0003, aprovada no gate de 2026-10-31 (emenda da 0003), como a do 0M-54.

**Conflitos com a fila:** o item inteiro toca `AbilitySystemComponent.cs`, `GameplayAbility.cs`, `GameplayEffect.cs` e `ActiveEffectInstance.cs` (Lote B, B-009, B-010): entra depois do DM8. EV2, EV3 e EV15 tocam o Bridges (RL6, RL7, AV5, DM6). EV17 a EV20 tocam `GameplayAbilityEditor.cs` (VN10, AV9) e `GasEditors.cs`. EV21 toca os assets do Sample (VN3) e `migracao-2-para-3.md` (VN3).

### EV1 — dividido em EV1a e EV1b (2026-11-02)

A Fábrica implementou o EV1 inteiro, e os 8 testes passam, mas o diff ficou com ~805 linhas em 8 arquivos (`ActionRunner.cs` com 279 e `ActionRunnerTests.cs` com 308), contra os ~490 estimados. Por decisão do usuário em 2026-11-02, ele se divide em dois tickets, como o DM1 e o AV1. O EV1a fica com a issue #174, renomeada, e o EV1b ganha a issue #307. O design da seção 1 não muda.

A fronteira separa uma execução que roda uma lista plana no tempo do ASC (EV1a) de tudo o que liga uma execução a outra ou a reaproveita (EV1b): filhas em sequência, filhas em paralelo, cancelamento e pools. As duas metades compilam sozinhas, e o EV1a não declara nenhum membro público que só o EV1b faria funcionar: `ActionStatus.Run`, `ActionStatus.RunParallel`, `GameplayAction.OnCancel`, `ActionContext.IsCancelled` e `AbilitySystemComponent.CancelActions` entram no EV1b, junto com o comportamento deles.

**Reaproveitamento do EV1 guardado.** O código está no working tree do checkout principal, sem commit, e tem cópia em `C:/Users/gabri/AppData/Local/Temp/claude/C--Users-gabri-MeusProjetos-SpellBook/a83a735c-1f71-45e0-a447-03cf4d1ad11a/scratchpad/ev1-guardado/`. A Fábrica corta os mesmos 8 arquivos entre os dois tickets, sem arquivo novo. No fim do EV1b, os arquivos são os do EV1 guardado, com duas diferenças: o `TwoExecutions_SameAction_NoSharedState` fica na forma do EV1a, e entra o teste do `CreateDerived`. As contagens abaixo foram feitas sobre o código guardado e seguem a ADR 0003: os `.meta` não contam, o `gas.md` conta só no arquivo e a saída gerada fica fora (emenda de 2026-11-02 do lote 8, abaixo).

Como cada arquivo se divide:

| Arquivo | EV1a | EV1b |
|---|---|---|
| `GameplayAction.cs` | `Execute`, `UsesTargets`, `Validate`, `CollectEffects` | `OnCancel` |
| `ActionStatus.cs` | `ActionFrame`; `ActionStatus` com `Done` e `Wait` | `Run`, `RunParallel` |
| `ActionContext.cs` | os dados da seção 1; contexto criado com `new` | `IsCancelled`, `CreateDerived`, `Reset` (contexto do pool) |
| `ActionRunner.cs` | `ActionExecutionHandle.IsRunning`; execução com a pilha de quadros só com a lista raiz; cursor lógico; `Advance` com `Done` e `Wait`; entrada nula pulada; `Tick`; `HasRunning`; execução e contexto alocados a cada `RunActions` | estados `Children` e `Parallel`; `Push` das filhas; execuções filhas (`Mother`, `Pending`, `StartParallel`, volta da mãe em `Finish`); `Cancel` e `CancelExecution`; conferência de versão depois do `Execute`; pools de execuções e de contextos (`RentContext`, `Release`), `Version` no handle |
| `AbilitySystemComponent.cs` | `Runner`, `RunActions`, `HasRunningActions`, avanço no `Tick` depois das cargas | `CancelActions` |
| `ActionRunnerTests.cs` | `LogAction`, `WaitTestAction`; os 4 testes do EV1a | `ParentAction`, `WaitThenLogAction`, `ReentrantCancelAction`, `CountAction`, `Counter`; os 5 testes do EV1b |
| `gas.md` (`#gameplayaction`) | `Done` e `Wait`, o `ActionFrame`, `RunActions` e `HasRunningActions`, a regra da espera, os campos do contexto | `Run` e `RunParallel`, `OnCancel`, `CancelActions` e a ordem do cancelamento, o pool e o contexto passado pelo chamador, `CreateDerived` |
| `CHANGELOG.md` | entrada "Base nova das ações e executor", sem filhas, paralelo, cancelamento e pool | completa a mesma entrada |

### EV1a — `GameplayAction`, `ActionContext` e execução em sequência
- Escopo: `Runtime/GAS/Actions/GameplayAction.cs`, `ActionStatus.cs` (com `ActionFrame`), `ActionContext.cs`, `ActionRunner.cs` (novos, com o `.meta` da pasta), `AbilitySystemComponent.cs` (`RunActions`, `HasRunningActions`, `Runner`, avanço no `Tick`), `Tests/GAS/PlayMode/ActionRunnerTests.cs` (novo), `gas.md` (`#gameplayaction`), `CHANGELOG.md`. O que cada arquivo leva está na tabela acima.
- Comportamento: `RunActions(acoes, contexto, dono)` roda a lista em sequência; as ações sem espera rodam na mesma chamada (R8), e `Wait(s)` segue o cursor lógico da ADR 0034, item 3. Contexto nulo cria um com este ASC como `Owner` e `Instigator`. O `dono` serve só ao `HasRunningActions` neste ticket. Execução e contexto são alocados a cada chamada; o pool entra no EV1b.
- Testes red: `InstantSequence_RunsInSameCall` (CA9); `Wait_CountsFromEndOfPreviousStep_TickInSameFrameCounts`; `Wait_BigTick_CarriesExcess`; `TwoExecutions_SameAction_NoSharedState` (CA5). No EV1a, o `TwoExecutions_SameAction_NoSharedState` roda duas vezes a mesma lista `{ WaitTestAction(1f), Log("done") }`, com as mesmas instâncias de ação, em vez do `WaitThenLogAction`, que vai para o EV1b. Os tempos e as asserções são os do teste guardado.
- Critério de pronto: os 4 testes verdes; nada do pacote usa ainda a base nova; nenhum membro público sem comportamento (`ActionStatus` só com `Done` e `Wait`, `GameplayAction` sem `OnCancel`, ASC sem `CancelActions`).
- Tamanho estimado: ~480 linhas; 8 arquivos (`ActionRunner.cs` ~175, testes ~135, `ActionContext.cs` ~65, `ActionStatus.cs` ~45, `GameplayAction.cs` ~30, ASC ~25). Depende de: DM8 (fila), AV9. Issue #174, renomeada.

### EV1b — Filhas, paralelo, cancelamento e pools do executor
- Escopo: `ActionStatus.cs` (`Run`, `RunParallel`), `GameplayAction.cs` (`OnCancel`), `ActionContext.cs` (`IsCancelled`, `CreateDerived`, `Reset`), `ActionRunner.cs`, `AbilitySystemComponent.cs` (`CancelActions`), `Tests/GAS/PlayMode/ActionRunnerTests.cs`, `gas.md` (`#gameplayaction`), `CHANGELOG.md`. O que cada arquivo leva está na tabela acima.
- Comportamento: o da seção 1 que ficou fora do EV1a. `Run` empilha as filhas e chama o pai de novo quando elas acabam. `RunParallel` cria execuções filhas ligadas à mãe, e a mãe volta quando todas acabam, com o cursor da filha que terminou por último. `CancelActions(dono)` cancela as execuções do dono com `OnCancel` das filhas antes da mãe e, em cada execução, do topo para a base (N7); a lista é percorrida de trás para frente com guarda de índice, e um `OnCancel` que cancela ou inicia outra execução não corrompe a iteração (P13). Execuções e contextos passam a vir dos pools do ASC; o handle compara a versão da execução, e um handle antigo de uma execução reaproveitada diz que ela não roda mais.
- Testes red: `RunChildren_ResumesParent`; `RunParallel_ResumesWhenAllDone`; `CancelOwner_OnCancelReverseOrder_NoException` (CA6); `Executions_10000_DoNotAllocate_AfterWarmup` (parte do CA34); `CreateDerived_KeepsInstigatorAbilityLevel_SetsPoint_CopiesCustomData` (novo: o derivado tem o ponto, herda instigador, habilidade e nível, recebe cópia rasa do `CustomData`, e uma chave acrescentada nele não aparece no original). Os 4 primeiros são os do EV1 guardado, sem edição.
- Critério de pronto: os 5 testes verdes, e os 4 do EV1a verdes sem edição; nada do pacote usa ainda a base nova.
- Tamanho estimado: ~360 linhas; 8 arquivos (`ActionRunner.cs` ~115, testes ~190, `ActionContext.cs` ~35, o resto ~20). Depende de: EV1a. Issue #307.

### EV2 — Lado do efeito na base nova
Emenda de 2026-11-01 (perguntas do RL6): sai a `RaiseEventEffectAction` (não existe mais) e entram `EffectDefinition` e `EffectEvent` no `ActionContext` (seção 1).
- Escopo: `Runtime/GAS/Actions/ActionEventAttribute.cs` (novo), `Runtime/GAS/Actions/ActionContext.cs` (`EffectDefinition`, `EffectEvent`), `GameplayEffect.cs` (listas `OnApplied`, `OnTick`, `OnRemoved` com `[ActionEvent]` e `[FormerlySerializedAs]`), `ActiveEffectInstance.cs` (dispara pelo executor, dono = instância, preenche o evento), `AbilitySystemComponent.cs` (instantâneo pelo executor com `EffectDefinition` e `EffectEvent.Applied`; cancelar as execuções da instância na remoção), `Runtime/GAS/Effects/Actions/CommonEffectActions.cs` (`DebugLogAction`, `SpawnVFXAction` na base nova, `[MovedFrom]`), `Tests/GAS/PlayMode/SpawnActionsTests.cs`, `Tests/GAS/PlayMode/EffectEventActionsTests.cs` (novo), `CHANGELOG.md`, mais três testes que usam as listas antigas com classes derivadas de `EffectAction` e quebram a compilação (emenda de 2026-11-02, exceção única da ADR 0003):
  - `Tests/GAS/PlayMode/EffectPeriodTests.cs`: `RemovalProbe` e `TickProbe` passam a derivar de `GameplayAction` (`Execute(source, target, instance)` vira a execução pelo `ActionContext`, com a instância lida do contexto); `OnTickActions` e a lista de remoção viram `OnTick` e `OnRemoved`.
  - `Tests/GAS/PlayMode/EffectRemovalTests.cs`: `CountingAction` e `RemoveSelfAction` na base nova (a `RemoveSelfAction` remove a instância lida do contexto); a lista de remoção vira `OnRemoved`.
  - `Tests/GAS/PlayMode/EffectRenewalTests.cs`: `CountingAction` na base nova; a lista de aplicação vira `OnApplied`.
  - Os asserts não mudam: a migração é mecânica e os testes continuam provando o mesmo comportamento de período, remoção e renovação.
- Testes red: `Effect_AppliedTickRemoved_EachOnceWithEffectContext` (CA3, C11); `EffectWaitThenApply_RemovedBefore_Cancelled` (CA4, C12); `SpawnVfx_ReleasedOnRemoval` (comportamento de hoje); `InstantEffect_OnApplied_HasDefinitionAndContext_NoInstance`; `EffectEvent_MatchesList_AppliedTickRemoved`.
- Critério de pronto: CA3 e CA4 verdes; `EffectAction` sem uso (é apagada no EV3).
- Tamanho estimado: ~490 linhas; 12 arquivos (era ~430 / 9; os três testes migrados somam ~60 linhas e passam o limite de arquivos em 20%, dentro da margem de ~30%: exceção única registrada na ADR 0003, emenda de 2026-11-02). Depende de: EV1b (era EV1 antes da emenda de 2026-11-02; a remoção da instância cancela as execuções dela por `CancelActions`, e a lista de efeito roda num contexto do pool).

### EV3 — Lado da habilidade na base nova (troca atômica)

Revisto em 2026-11-02 (opção (c) do usuário). O EV3 troca a base e mantém o comportamento. As listas `OnEnd` e `OnCancel` saíram para o EV3c, e a `WaitAction` com `ScalableFloat` saiu para o EV3d. A lista de arquivos abaixo é a recontagem feita no código depois do EV2 (`4f2b3b5`) e substitui a do texto aprovado em 2026-10-31.

- Comportamento: as ações, os campos e as funções de hoje continuam, agora na base `GameplayAction` e rodados pelo executor do ASC. A habilidade não ganha evento novo. As regras de tempo da base nova passam a valer para a habilidade, como a seção 11 já previa: ação sem espera não custa quadro (R8), e a espera conta do fim do passo anterior (R6). `GameplayAbility.Actions` vira `OnActivate` (`List<GameplayAction>`, `[ActionEvent("Ao ativar", ...)]`, `[FormerlySerializedAs("Actions")]`), e `Projectile.OnHitActions` vira `OnImpact` (`[FormerlySerializedAs("OnHitActions")]`). A `WaitAction` continua com `public float Duration`; zero ou menos devolve `Done` na mesma chamada (R8).
- Escopo, lista fechada de 40 arquivos (35 até a emenda "`ActionContext.cs` no EV3", 36 até a emenda "ponto de extensão `protected`" e 39 até a emenda "`ChargesTests.cs` no EV3", as três no fim do ticket). Os `.meta` e a saída gerada (catálogo, `api/reference/**`, `api/reference.sha256`) ficam fora da lista e da contagem.
  - Runtime (13):
    1. `Runtime/GAS/Abilities/GameplayAbility.cs`: `OnActivate`; `Validate` e a validação dos nomes da tabela percorrem `OnActivate`.
    2. `Runtime/GAS/Core/AbilitySystemComponent.cs`: ativação por `RunActions(OnActivate, contexto, dono = a ativação)`, fim quando a execução acaba, cancelamento por `CancelActions`; sai o `RunAbility` com `StartCoroutine`.
    3. `Runtime/GAS/Abilities/AbilitySpec.cs`: saem `Routine` e `ChildRoutines`; `ActiveContext` passa a `ActionContext`.
    4. `Runtime/GAS/Abilities/Actions/FlowActions.cs`: `RepeatAction` (`Interval` por `ActionStatus.Wait`), `ParallelAction` (`RunParallel`), `ConditionalAction` (o enum fica até o EV7).
    5. `Runtime/GAS/Abilities/Actions/WaitAction.cs`
    6. `Runtime/GAS/Abilities/Actions/AnimationAction.cs`
    7. `Runtime/GAS/Abilities/Actions/TargetingAction.cs` (continua com a `TargetingStrategy` até o EV4b)
    8. `Runtime/GAS/Abilities/Actions/ApplyEffectAction.cs`
    9. `Runtime/GAS/Abilities/Actions/PlayCueAction.cs`
    10. `Runtime/GAS/Abilities/Actions/SpawnProjectileAction.cs`: `DelayBetweenShots` por `ActionStatus.Wait`.
    11. `Runtime/GAS/Abilities/Projectiles/Projectile.cs`: `OnImpact`, `Setup` com `ActionContext` e `List<GameplayAction>`, impacto por `Source.RunActions` (P15).
    12. `Runtime/GAS/Effects/EffectSpec.cs`: `FromAbility(efeito, ActionContext)`.
    13. `Runtime/GAS.Bridges/RaiseEventAction.cs`
  - Editor (1):
    14. `Editor/GAS/ScalableFloatDrawer.cs`: `TableOwners` percorre `OnActivate` como `GameplayAction`. Sem essa troca o assembly do Editor não compila.
  - Testes (18):
    15. `Tests/Testing/GasFactory.cs`: `CreateAbility(tag, params GameplayAction[])`, `CreateActionContext` no lugar do `CreateAbilityContext`.
    16. `Tests/GAS/EditMode/GasFactoryTests.cs`
    17. `Tests/GAS/EditMode/LevelValuesValidationTests.cs`
    18. `Tests/GAS/EditMode/GasSourceScanTests.cs`: as guardas `NoCoroutineOrUnityClock_InActions` e `OldClassesGone`.
    19. `Tests/GAS/EditMode/ActionContextTests.cs`: substitui o `AbilityContextTests.cs`, apagado. Os seis casos do `CreateDerived` (chaves vistas, chave nova no derivado, chave nova no original, referências compartilhadas, comparador mantido, habilidade e nível mantidos) passam a testar o `ActionContext.CreateDerived`, com os mesmos asserts. O teste do EV1b não cobre o comparador nem a chave acrescentada no original. Só o preparo muda onde a API mudou: `new ActionContext()` no lugar de `new AbilityContext(null, Vector3.zero)`; no caso do comparador, `SetCustomDataComparer(StringComparer.OrdinalIgnoreCase)` e depois `CustomData["Crit"] = true`, no lugar de atribuir um dicionário; no da habilidade e do nível, `Instigator` e `Point` no lugar de `Source` e `TargetPosition`. Mais três casos novos, da emenda do item 36: `SetCustomDataComparer_WithKeys_Throws`; `SetCustomDataComparer_Null_UsesDefault`; `PooledContext_AfterCustomComparer_ComesBackWithDefault` (pelo `asc.RunActions` de um ASC da `GasFactory`: uma execução com contexto de comparador próprio termina, e a seguinte, com contexto `null` tirado do pool, vê `EqualityComparer<string>.Default`).
    20. `Tests/GAS/PlayMode/AbilityWaitTests.cs`: os testes da seção 11, os novos de tempo (CA7, CA9) e o `SameAction_InAbilityAndEffectLists` (CA2).
    21. `Tests/GAS/PlayMode/ActivationOrderTests.cs`: `WaitForeverAction` na base nova.
    22. `Tests/GAS/PlayMode/EffectContextTests.cs`
    23. `Tests/GAS/PlayMode/LiveMagnitudeTests.cs`
    24. `Tests/GAS/PlayMode/SpawnActionsTests.cs`
    25. `Tests/Core/EditMode/Inspector/TypePickerCatalogTests.cs`: `PickEffectAction` vira uma ação de teste derivada de `GameplayAction`, e o teste procura candidatos de `GameplayAction`.
    26. `Tests/Core/EditMode/Documentation/TypeCatalogTests.cs`: `Build_HasPackageBases` espera `SpellBook.GAS.Actions.GameplayAction`.
    27. `Tests/Core/EditMode/ProjectValidationTests.cs`
    28. `Tests/GAS.Bridges/EditMode/RaiseEventActionTests.cs`
    29. `Tests/GAS.Bridges/EditMode/RaiseEventDrawerTests.cs`
    30. `Tests/GAS.Bridges/EditMode/GasPayloadSourceTests.cs`
    31. `Tests/GAS.Bridges/PlayMode/RaiseEventPayloadTests.cs`
    32. `Tests/GAS.Bridges/PlayMode/RaiseEventAbilityTests.cs`
  - Documentação (3):
    33. `Documentation~/manual/modulos/gas.md`: as seções que citam `AbilityAction`, `AbilityContext`, `WaitSeconds`, `StartChildRoutine`, `OnHitActions` e `Setup` do projétil passam para a base nova; a nota "ainda usam `AbilityAction` e `EffectAction`" sai.
    34. `Documentation~/manual/ferramentas/validador.md`: a regra da tabela cita `GameplayAction.CollectEffects` no lugar de `AbilityAction.CollectEffects`.
    35. `Packages/com.spellbook/CHANGELOG.md`: quebra (`AbilityAction`, `AbilityContext` e `EffectAction` saem; campos renomeados; regra de tempo).
  - Runtime, acrescentado na emenda "`ActionContext.cs` no EV3" (1):
    36. `Runtime/GAS/Actions/ActionContext.cs`: `SetCustomDataComparer`, o `CreateDerived` com o comparador do original e o `Reset` que volta ao comparador padrão (design na emenda).
  - Runtime, acrescentado na emenda "ponto de extensão `protected`" (3):
    37. `Runtime/GAS/Actions/GameplayAction.cs`: `Execute` e `OnCancel` passam a `protected`; entram `InvokeExecute` e `InvokeCancel`, `internal` e não virtuais.
    38. `Runtime/GAS/Actions/ActionRunner.cs`: as duas chamadas (`:176` e `:254`) passam a `InvokeExecute` e `InvokeCancel`.
    39. `Runtime/GAS/Effects/Actions/CommonEffectActions.cs`: os dois overrides passam de `protected internal` a `protected`.
  - Testes, acrescentado na emenda "`ChargesTests.cs` no EV3" (1):
    40. `Tests/GAS/PlayMode/ChargesTests.cs`: no `BornFull_DisabledSameAsToday`, só o preparo da habilidade `plain` muda; os asserts ficam iguais (design na emenda).
  - Remoções puras (fora da contagem, I42): `Runtime/GAS/Abilities/AbilityAction.cs`, `Runtime/GAS/Abilities/AbilityContext.cs`, `Runtime/GAS/Effects/EffectAction.cs`. O `Tests/GAS/EditMode/AbilityContextTests.cs` também é apagado, mas tem substituto no mesmo ticket (item 19) e conta.
- Testes red: `TickInSameFrameAsStepEnd_CountsForWait` para `WaitAction`, `RepeatAction.Interval` e `SpawnProjectileAction.DelayBetweenShots` (CA7, C10, nomes do R7); `SameAction_InAbilityAndEffectLists` (CA2, C1); `RepeatCount3Interval0_SameCall` (CA9); `NoCoroutineOrUnityClock_InActions` (CA10, guarda textual sobre `Runtime/GAS/**` e `Runtime/GAS.Bridges/**` no padrão do `GasSourceScanTests`); `OldClassesGone` (CA1, guarda textual); `Projectile_ImpactRunsOnCasterAsc`; `SpawnProjectile_DelayBetweenShots_CountsAscTicks` sem edição (CA8); `NoProtectedInternal_InActions` (emenda "ponto de extensão `protected`": guarda textual no `GasSourceScanTests`, sobre `Runtime/GAS/**` e `Runtime/GAS.Bridges/**`, que acusa `protected internal` em qualquer arquivo).
- Critério de pronto: CA1, CA2, CA7 a CA10 verdes; suíte inteira verde com a lista de testes alterados no PR (CA36); a geração da referência da API roda sem erro e atualiza a saída gerada; nenhum arquivo fora da lista acima, além dos `.meta` e da saída gerada; a `WaitAction` com o mesmo campo `float Duration`.
- Tamanho estimado: ~1245 linhas; 40 arquivos, sem contar as três remoções puras (era ~1240 / 39 antes da emenda "`ChargesTests.cs` no EV3", ~1200 / 36 antes da emenda "ponto de extensão `protected`" e ~1150 / 35 antes da emenda "`ActionContext.cs` no EV3"). **Exceção única à ADR 0003**, ampliada na emenda de 2026-11-02 da 0003 (a de 2026-10-31 dava ~1100 linhas e ~22 arquivos). O Examinador revisa pela lista acima e pelos testes red; arquivo fora da lista faz a Fábrica parar e devolver. Depende de: EV2, VN9, RP-07, AV5. O `RunParallel` da `ParallelAction`, o `CancelActions` do cancelamento da habilidade e o `CreateDerived` do projétil vêm do EV1b, que entra antes do EV2.

#### Emenda de 2026-11-02: `ActionContext.cs` no EV3

Estado: decisão do usuário de 2026-11-02 (opção (a)), depois que a Fábrica devolveu o EV3 antes de editar. O item 19 pede o caso "comparador mantido" com os mesmos asserts, mas o `ActionContext` do EV1b (`Runtime/GAS/Actions/ActionContext.cs:65`) só tem getter no `CustomData`, e o `CreateDerived` (`:91-94`) copia chave por chave para um dicionário novo, com o comparador padrão. O `AbilityContext` antigo (`AbilityContext.cs:40-42`) mantinha o comparador. Tirar ou afrouxar o caso fere a regra de testes do `CLAUDE.md`, então o `ActionContext.cs` entra no escopo como item 36.

Design (sem mecanismo paralelo; o `CustomData` continua só com getter):
- `public void SetCustomDataComparer(IEqualityComparer<string> comparer)`: escolhe o comparador das chaves do `CustomData`. `null` é o `EqualityComparer<string>.Default`. Com o `CustomData` já com chaves, lança `InvalidOperationException` ("defina o comparador antes de gravar chaves"), porque trocar o comparador de chaves existentes pode juntar duas chaves numa. Com o mesmo comparador de hoje, não aloca; com outro, troca o dicionário vazio por um novo com esse comparador.
- Construtor com comparador não serve: os contextos vêm do pool do ASC (`ActionRunner.RentContext`), e o `CreateDerived` também aluga um.
- `CreateDerived`: se o original tem `CustomData`, chama `derived.SetCustomDataComparer(_customData.Comparer)` antes de copiar as chaves. Assim fica igual ao `AbilityContext`: dicionário novo, mesmo comparador, mesmas referências de valor. O derivado vem limpo do pool ou de `new`, então a chamada não lança.
- `Reset` (devolução ao pool): se o comparador do dicionário não é o padrão, solta o dicionário (`_customData = null`) e o próximo acesso cria um com o padrão; se é o padrão, só limpa, como hoje. Um contexto do pool nunca herda o comparador de outra execução, e o `Executions_10000_DoNotAllocate_AfterWarmup` continua sem alocar.
- O teste do EV1b (`ActionRunnerTests.CreateDerived_KeepsInstigatorAbilityLevel_SetsPoint_CopiesCustomData`) não muda: ele usa o comparador padrão, e o comportamento com o padrão é o mesmo. O `ActionRunnerTests.cs` fica fora da lista.
- `gas.md` (item 33) e `CHANGELOG.md` (item 35) citam o `SetCustomDataComparer` junto do `CustomData`, sem arquivo a mais.

Aviso da Fábrica (vale para o EV3 e os tickets seguintes): o `GameplayAction.Execute` é `protected internal`, e o GAS não expõe os internals aos testes. Os testes de fora do assembly do GAS (`Tests/**`, `Tests/GAS.Bridges/**`) não chamam o `Execute` direto: rodam as ações por `asc.RunActions(acoes, contexto, dono)` e, para o tempo, por `asc.Tick`. As ações de teste derivadas declaram `protected override ActionStatus Execute(...)`.

Tamanho: ~1200 linhas (era ~1150; ~25 no `ActionContext.cs`, ~25 nos três casos novos) e 36 arquivos. A exceção única da ADR 0003 foi ajustada para isso (emenda "`ActionContext.cs` no EV3, lista com 36 arquivos").

#### Emenda de 2026-11-02: ponto de extensão `protected`

Estado: aprovada pelo usuário no gate parcial de 2026-11-02 (`InvokeExecute`/`InvokeCancel`, 39 arquivos). A Fábrica devolveu o EV3 com o código migrado e os testes do ticket verdes (EditMode 77/77, PlayMode 74/74), mas a geração da referência da API falha. O docfx compila o pacote inteiro como um assembly só e, nele, o override de um membro `protected internal` tem de ser `protected internal`. A Unity compila o `SpellBook.GAS.Bridges` à parte, sem acesso aos internals do GAS, e nele o mesmo override tem de ser `protected`. Cada compilador dá CS0507 na forma que o outro aceita. O `RaiseEventAction.Execute` (`Runtime/GAS.Bridges/RaiseEventAction.cs:34`) é o primeiro override de fora do assembly do GAS no código do pacote, e qualquer ação de usuário fica no mesmo caso.

Decisão (ADR 0034, emenda de 2026-11-02): o `Execute` e o `OnCancel` passam a `protected`, e a base ganha `InvokeExecute` e `InvokeCancel`, `internal` e não virtuais, que só repassam a chamada. O `ActionRunner` chama esses dois. Os nomes `Execute` e `OnCancel` ficam, então quem escreve uma ação continua a declarar `protected override ActionStatus Execute(...)` e `protected override void OnCancel(...)`, em qualquer assembly, e o docfx aceita a mesma forma.

Por que não as outras saídas:
- `InternalsVisibleTo("SpellBook.GAS.Bridges")` resolve o Bridges e deixa a ação do usuário com a forma oposta à do Bridges, além de abrir todos os internals do GAS para um módulo opcional (ADR 0012).
- Mudar a geração para compilar um assembly por asmdef é o caminho mais caro, e o contrato continuaria dependendo de como cada ferramenta compila.
- Renomear o ponto de extensão para `OnExecute` resolve igual, mas obriga a editar todos os overrides de teste sem ganho nenhum. Manter o nome deixa os testes como estão.

Os outros membros da `GameplayAction` não mudam: `UsesTargets`, `Validate` e `CollectEffects` já são `public virtual`, e o override `public` vale nos dois compiladores. A busca no pacote (`Runtime/**`, `Editor/**`, `Tests/**`, `Samples~/**`) não acha outro `protected internal` além do `Execute` e do `OnCancel` da base e dos overrides das ações do GAS. Nenhuma ação chama o `Execute` de outra: as listas filhas rodam por `ActionStatus.Run` e `RunParallel`.

O que muda no código, todo dentro do EV3:
- `GameplayAction.cs` e `ActionRunner.cs` (itens 37 e 38).
- Os overrides das ações do GAS passam de `protected internal override` a `protected override`. São os itens 4 a 10 (`FlowActions.cs` com três, `WaitAction`, `AnimationAction`, `TargetingAction`, `ApplyEffectAction`, `PlayCueAction`, `SpawnProjectileAction`), que já estão na lista, e o `CommonEffectActions.cs` (item 39, dois overrides, vindo do EV2).
- `RaiseEventAction.cs` (item 13) fica com o `protected override` que já tem.
- `GasSourceScanTests.cs` (item 18) ganha a guarda `NoProtectedInternal_InActions`, que falha hoje por causa da base.
- `gas.md` (item 33), na seção de ação própria: o override é `protected`, e o executor é quem chama o `Execute`.
- `CHANGELOG.md` (item 35): a entrada "Base nova das ações e executor", ainda sem release, passa a dizer que o `Execute` e o `OnCancel` são `protected`.

Testes do EV1a e do EV1b: nenhum muda. As ações de teste do `ActionRunnerTests.cs` já declaram `protected override` no `Execute` e no `OnCancel`, e os testes rodam as ações por `asc.RunActions` e `asc.CancelActions`, sem chamar o `Execute` direto. Os outros testes com ações próprias (`ActionContextTests`, `EffectPeriodTests`, `EffectRemovalTests`, `EffectRenewalTests`, `EffectEventActionsTests`, `ActivationOrderTests`, `EffectContextTests`, `AbilityWaitTests`, `TypePickerCatalogTests`) também já usam `protected override`. O `ActionRunnerTests.cs` continua fora da lista.

O aviso da Fábrica, acima, continua certo para os testes. O que muda é o motivo: o `Execute` não é chamável de fora da hierarquia por ser `protected`, e não mais por ser `internal`.

Tamanho: ~1240 linhas (era ~1200; ~15 na base, ~2 no executor, ~15 na guarda, ~10 em modificadores e ~5 nas duas páginas) e 39 arquivos. A exceção única da ADR 0003 foi ajustada para isso (emenda "ponto de extensão `protected`, lista com 39 arquivos").

#### Emenda de 2026-11-02: `ChargesTests.cs` no EV3

Estado: aprovada pelo usuário no gate parcial de 2026-11-02 (40º arquivo, preparo por espera e frase de quebra no CHANGELOG e no `gas.md`). Com o EV3 no working tree, a suíte deu EditMode 648/648 e PlayMode 364/365. A falha é `ChargesTests.BornFull_DisabledSameAsToday` (`Tests/GAS/PlayMode/ChargesTests.cs:178`): o teste espera `AlreadyActive` e recebe `OnCooldown`.

Causa: a `plain` vem de `CreateAbilityWithCooldown(5f)` (`Tests/Testing/GasFactory.cs:130`), que não tem ações. Antes do EV3, a corrotina deixava essa habilidade ativa por um quadro, e a consulta logo depois da ativação caía na checagem `spec.IsActive` (`AbilitySystemComponent.cs:756`), que vem antes da recarga (`:775`). Pela R8, a lista vazia termina na mesma chamada, e a consulta já vê a recarga. O código está certo. O preparo do teste dependia do quadro da corrotina, sem dizer isso.

Intenção do teste (CA18, "cargas desligadas reproduzem o comportamento de hoje"): uma habilidade sem cargas, ainda ativa, é barrada pela checagem comum (`AlreadyActive`), e não por `NoCharges`, e a API de cargas fica inerte (0, 0, `false`, nenhum `OnChargesChanged`). O `AlreadyActive` é o assert que prova isso, então ele fica. Trocar o assert por `OnCooldown` mudaria o que o teste prova para a regra de tempo, que já tem testes próprios no `AbilityWaitTests` (R8).

O que muda, só no preparo:
- A `plain` passa a ter uma espera que não vence no teste: `_gas.CreateAbility(new WaitAction { Duration = 100f })` e depois `plain.CooldownEffect = _gas.CreateDurationEffect(5f)`, como faz o `CreateAbilityWithCooldown`. O ASC tem `autoTick` desligado e fonte manual, e o teste não chama `Tick`, então a habilidade fica ativa até o fim.
- Os asserts das linhas 171 a 183 não mudam. O `GasFactory.cs` não ganha sobrecarga nova.

CHANGELOG (item 35): a entrada de quebra "Habilidade e projétil na base nova das ações" já diz que a habilidade termina quando a execução acaba e que ação sem espera não custa quadro, mas não diz a consequência mais visível. A Fábrica acrescenta, sem arquivo a mais: "Uma habilidade sem ações, ou só com ações sem espera, termina dentro do `TryActivateAbility`: o `OnAbilityEnded` dispara e `AbilitySpec.IsActive` volta a `false` antes de a chamada retornar, e uma consulta logo depois vê a recarga (`OnCooldown`) em vez de `AlreadyActive`. Antes, a corrotina a deixava ativa por um quadro." O `gas.md` (item 33) diz o mesmo na seção de ativação, em uma frase.

Tamanho: ~1245 linhas (~3 no teste, ~2 no CHANGELOG) e 40 arquivos. A exceção única da ADR 0003 foi ajustada para isso (emenda "`ChargesTests.cs` no EV3, lista com 40 arquivos").

### EV3c — Listas "Ao terminar" e "Ao ser cancelada" da habilidade

Ticket novo (2026-11-02), tirado do EV3. Usa a Unity.

- Comportamento:
  - `GameplayAbility` ganha `[SerializeReference, TypePicker, ActionEvent("Ao terminar", ...)] List<GameplayAction> OnEnd` e `[SerializeReference, TypePicker, ActionEvent("Ao ser cancelada", ...)] List<GameplayAction> OnCancel`, com os alvos do evento da tabela do Design 2 (os da ativação). Os dois campos são novos e não precisam de `[FormerlySerializedAs]`.
  - Fim normal: quando a execução de `OnActivate` acaba, o ASC roda `OnEnd` e só depois devolve as `ActivationOwnedTags` e dispara `OnAbilityEnded(spec, false)`. `OnCancel` não roda.
  - Cancelamento: `CancelAbility` chama `CancelActions` da ativação (os `OnCancel` das ações em curso, em ordem reversa), depois roda a lista `OnCancel`, depois devolve as tags e dispara `OnAbilityEnded(spec, true)`. `OnEnd` não roda.
  - As duas listas rodam numa execução nova, com contexto do pool que copia da ativação a habilidade, o spec, o nível, o instigador, o ponto e os alvos do evento. A execução fica sem dono (`owner = null`), como a `OnRemoved` do efeito depois da remoção: as ações sem espera rodam na mesma chamada, antes do evento de fim, e uma espera dentro da lista continua depois de a habilidade terminar, sem segurar a habilidade ativa e sem ser cancelada por uma nova ativação.
  - Lista vazia ou nula não pega contexto do pool e não aloca.
  - **Emenda de 2026-11-02 (aprovada em 2026-11-02 pelo Controlador (modo autônomo)): o ASC é dono do contexto da ativação.** Hoje o `ActionRunner.Release` (`Runtime/GAS/Actions/ActionRunner.cs:259-277`) zera o contexto e o devolve ao pool assim que `OnActivate` termina ou é cancelada, antes do `EndAbility` (`AbilitySystemComponent.cs:980`), nos três caminhos: fim na mesma chamada (`:911` → `:922`), fim no `Tick` (`:243`) e `CancelAbility` (`:939` → `:943`). Copiar antes do `Run` não serve, porque a `TargetingAction` preenche `EventTargets` durante `OnActivate`. Decisão: usar a posse que o executor já tem (`Execution.OwnsContext`, hoje `false` nas filhas paralelas), sem mecanismo novo:
    - `ActionRunner` ganha a sobrecarga interna `Run(actions, context, owner, bool ownsContext)`; o `Run` de três parâmetros chama a nova com `true`, e o `RunActions` público não muda. Ganha também `internal void ReturnContext(ActionContext context)` (`Reset` e volta ao pool), que o `Release` passa a usar.
    - O `TryActivateAbility` roda `OnActivate` com `ownsContext: false`. O `EndAbility` monta o contexto de `OnEnd` ou `OnCancel` copiando de `spec.ActiveContext` (só se a lista não for vazia), zera `spec.ActiveContext` e só então chama `ReturnContext`, uma vez, antes de devolver as tags e disparar `OnAbilityEnded`.
    - Consequências: `AbilitySpec.ActiveContext` passa a valer, preenchido, durante toda a ativação, até o `EndAbility`; o XML doc muda de "é do executor" para "é do ASC". O caminho em que a tag concedida cancela a habilidade antes do `Run` (`:907-908`) passa a devolver o contexto ao pool, em vez de deixá-lo para o GC.
    - A ADR 0034 não decide quem é dono do contexto; não há emenda de ADR.
  - `GameplayAbility` ganha `public void CollectActionEffects(List<GameplayEffect> results)`, que percorre `OnActivate`, `OnEnd` e `OnCancel` pelo `CollectEffects` de cada ação. O `Validate` (ação nula e nomes da tabela) e o `ScalableFloatDrawer.TableOwners` passam a usar esse método, para um efeito aplicado em "Ao terminar" ter o nome da tabela conferido como os de "Ao ativar".
- Escopo:
  - `Runtime/GAS/Abilities/GameplayAbility.cs`: `OnEnd`, `OnCancel`, `CollectActionEffects`, `Validate` nas três listas.
  - `Runtime/GAS/Core/AbilitySystemComponent.cs`: as duas listas no fim e no cancelamento, na ordem acima; posse do contexto da ativação (emenda de 2026-11-02).
  - `Runtime/GAS/Actions/ActionRunner.cs` (emenda de 2026-11-02): `Run` com `ownsContext` e `ReturnContext`.
  - `Runtime/GAS/Abilities/AbilitySpec.cs` (emenda de 2026-11-02): XML doc de `ActiveContext`.
  - `Editor/GAS/ScalableFloatDrawer.cs`: `TableOwners` pelo `CollectActionEffects`.
  - `Tests/GAS/PlayMode/AbilityEndCancelListsTests.cs` (novo).
  - `Tests/GAS/EditMode/LevelValuesValidationTests.cs`: o teste de validação abaixo.
  - `Documentation~/manual/modulos/gas.md`: as duas listas e a ordem do fim e do cancelamento.
  - `Documentation~/manual/ferramentas/validador.md`: a regra da tabela vale para as três listas.
  - `Packages/com.spellbook/CHANGELOG.md`: "Adicionado".
  - Saída gerada fora da contagem.
- Testes red:
  - `OnEnd_RunsOnceAfterOnActivate_BeforeAbilityEnded`: o registro fica `activate`, `end`, `ended-event`, e `OnCancel` não aparece.
  - `Cancel_RunsOnCancelListAfterRunningActionsOnCancel_NotOnEnd`: com uma ação em espera em `OnActivate`, o registro fica `cancel:acao`, `cancel-list`, `ended-event(cancelado)`, e `OnEnd` não aparece.
  - `EndAndCancelLists_ContextCopiesActivation`: a ação de `OnEnd` vê a mesma habilidade, o spec, o nível 3, o instigador e os alvos do evento da ativação. Emenda de 2026-11-02: os alvos são postos por uma ação de `OnActivate` (não antes da ativação), e o teste cobre os três caminhos (`[TestCase]`): fim na mesma chamada, fim no `Tick` e `CancelAbility` (este com a lista `OnCancel`).
  - `ActiveContext_FilledUntilAbilityEnded` (emenda de 2026-11-02): com uma espera em `OnActivate`, depois do `Tick` que a vence, uma ação de `OnEnd` lê `spec.ActiveContext` com a habilidade e os alvos da ativação; dentro do `OnAbilityEnded`, `spec.ActiveContext` é nulo.
  - `OnEndWithWait_AbilityEndsNow_ListFinishesByTick`: a habilidade termina na mesma chamada, a ação depois da espera roda no `Tick` que vence a espera, e uma nova ativação no meio não a cancela.
  - `EmptyEndCancelLists_DoNotAllocate`: ativar e terminar 1000 vezes, depois do aquecimento, com as duas listas vazias, não aloca.
  - `Validate_TableNameInOnEndEffect_IsChecked` (EditMode): um `ApplyEffectAction` em `OnEnd` com efeito que lê um nome fora da tabela gera o mesmo erro que em `OnActivate`.
- Critério de pronto: os 7 testes verdes; a suíte inteira verde, com o `ActionRunnerTests` sem edição; nada muda para habilidade com as duas listas vazias, e os testes de ativação do EV3 passam sem edição.
- Tamanho estimado (emenda de 2026-11-02; era ~380 linhas e 8 arquivos): ~410 linhas; 10 arquivos (testes ~250, ASC ~70, `GameplayAbility.cs` ~50, `ActionRunner.cs` ~15, drawer ~10, `AbilitySpec.cs` ~2; as duas páginas contam só nos arquivos). Depende de: EV3, XR3 (fila do lote 9). Commit: `feat(gas): listas Ao terminar e Ao ser cancelada da habilidade`.

### EV3d — `WaitAction` com `ScalableFloat` e migração dos assets

Ticket novo (2026-11-02), tirado do EV3 pela proposta WAIT. Usa a Unity.

- Comportamento:
  - `WaitAction` troca `public float Duration` por `[SerializeField] private ScalableFloat duration = ScalableFloat.Constant(0f)` com a propriedade `public ScalableFloat Duration { get; set; }`, como o `GameplayEffect.Duration` do VN3. A espera vale `Duration.Evaluate(context.Level, context.Ability != null ? context.Ability.Values : null)`; sem habilidade (lista de efeito), o modo `Table` vale o `Fallback`, como em todo `ScalableFloat` sem tabela. Zero ou menos devolve `Done` na mesma chamada, como no EV3.
  - Campo legado: `[FormerlySerializedAs("Duration")] [SerializeField, HideInInspector] private float legacyDuration = float.NaN`. A `WaitAction` implementa `ISerializationCallbackReceiver`, e o `OnAfterDeserialize` faz o que o `GameplayEffect` já faz: se `legacyDuration` não é `NaN`, `duration = ScalableFloat.Constant(legacyDuration)` e `legacyDuration = NaN`. O teste de migração prova que o callback roda numa ação guardada por `[SerializeReference]`. Se não rodar, a Fábrica para e devolve, porque o desenho do ticket depende disso.
  - Menu `SpellBook > Migrar assets do GAS (3.0)`: o `GasAssetMigration.MigrateAll` passa a regravar também todo `GameplayAbility` gravável (`Assets/` e pacotes embutidos ou locais, a mesma regra de hoje) e devolve a soma dos dois tipos. A mensagem do Console conta efeitos e habilidades separados. Rodar de novo não muda o YAML.
- Escopo:
  - `Runtime/GAS/Abilities/Actions/WaitAction.cs`
  - `Editor/GAS/GasAssetMigration.cs`
  - `Tests/GAS/EditMode/GasAssetMigrationTests.cs`: o teste de migração abaixo, com um ajudante que grava um `GameplayAbility` em YAML 2.x na pasta temporária que o teste já usa.
  - `Tests/GAS/PlayMode/AbilityWaitTests.cs`: os testes de hoje passam a `Duration = ScalableFloat.Constant(x)`, mais o teste de nível.
  - `Documentation~/manual/modulos/gas.md`: a `WaitAction` com duração por nível.
  - `Documentation~/manual/regras/migracao-2-para-3.md`: a linha da `WaitAction` e o menu, que agora cobre as habilidades.
  - `Packages/com.spellbook/CHANGELOG.md`: quebra (`Duration` muda de tipo) e "Alterado" (o menu de migração).
  - Saída gerada fora da contagem.
- Testes red:
  - `WaitAction_TableDuration_UsesExecutionLevel` (PlayMode): habilidade com o valor `espera` = [1, 2, 3] na tabela e `WaitAction` em modo `Table`. No nível 3, a ação seguinte não roda com `Tick(2.9f)` e roda com mais `Tick(0.1f)`; no nível 1, roda depois de 1 s.
  - `Migrate_RewritesLegacyWaitAction_Idempotent` (EditMode): um `GameplayAbility` em YAML 2.x, com a lista no campo antigo `Actions` e uma `WaitAction` com `Duration: 1.5`, abre com `Duration.Evaluate(1) == 1.5` e `Mode == Constant`. Depois do menu, o YAML tem `OnActivate:`, `legacyDuration: NaN` e `constant: 1.5` e não tem mais o campo `Duration:` da ação, e a segunda execução do menu deixa o arquivo igual. O teste do VN3 (`Migrate_RewritesLegacyAsset_Idempotent`) continua passando sem edição.
  - Os testes de espera de hoje (`WaitAction_FinishesByAscTicks_NotRealTime` e o de zero ou menos) só trocam o `float` por `ScalableFloat.Constant(x)`, com os mesmos asserts.
- Critério de pronto: os 2 testes novos verdes; a suíte inteira verde; um asset com `WaitAction` gravado antes do EV3d abre com a mesma espera, sem passar pelo menu.
- Tamanho estimado: ~260 linhas; 7 arquivos (testes ~150, `WaitAction.cs` ~45, migração ~25; as duas páginas contam só nos arquivos). Depende de: EV3c (fila; os dois escrevem `gas.md`, o `CHANGELOG.md` e a saída gerada). Commit: `feat(gas)!: duração da WaitAction por nível, com migração dos assets`.

### EV4a — `IRandomSource` e seletores simples
- Escopo: `Runtime/Core/Random/IRandomSource.cs` (com `SeededRandomSource`), `Runtime/GAS/Targeting/TargetSelector.cs` (base, `CasterSelector`, `EventTargetsSelector`, `PointSelector`), `GameplayAction.cs` (campo `target`), `ActionRunner.cs` (resolução), `ActionContext.cs` (`Targets`, `Random`), `AbilitySystemComponent.cs` (`Random`, `SetRandomSource`), `Tests/Core/EditMode/SeededRandomSourceTests.cs` (novo), `Tests/GAS/PlayMode/TargetSelectorTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red: `CasterAndEventTargets_SameList_DifferentAscs` (CA11, C2); `NoSelector_UsesEventDefault_PerEventTable` (C3); `Seeded_SameSeedSameSequence`; `Selection_DoesNotAllocate`.
- Critério de pronto: CA11 verde.
- Tamanho estimado: ~420 linhas; 10 arquivos. Depende de: EV3d (emenda de 2026-11-02; era EV3). O EV4a toca `GameplayAction.cs`, `ActionContext.cs` e `gas.md`, e entra depois do EV3c e do EV3d na fila do lote 9.

### EV4b — `AreaSelector` e fim da `TargetingStrategy`
- Escopo: `Runtime/GAS/Targeting/AreaSelector.cs` (novo), `TargetingAction.cs` (seletor), `TargetingStrategy.cs`, `CircleTargeting.cs`, `MouseTargetingStrategy.cs` (apagados, com substituto), `Editor/GAS/GasEditors.cs` (tira a linha da estratégia), `Tests/GAS/PlayMode/AreaSelectorTests.cs` (novo), `gas.md`, `manual/referencia/catalogo.md` (linha do `AreaSelector`; entrou pela margem, ADR 0003, emenda de 2026-11-03), `CHANGELOG.md`.
- Testes red (emenda de 2026-10-31: sem filtro de tags neste ticket): `Area_Max2_ExcludesOwner_IncludeOwnerAddsIt`; `RandomN_Distinct_SameSeedSame` (CA13, C5); `Area2DAnd3D`; `Sequence3Actions_Area_10000_DoNotAllocate` (CA34, parte).
- Critério de pronto: CA13 e a parte sem filtro do CA34 verdes; CA15 pela guarda de assemblies. O CA12 passa para o EV8.
- Tamanho estimado: ~420 linhas; 9 arquivos. Real: 579 linhas; 10 arquivos, pela exceção única da ADR 0003 (emenda de 2026-11-03). Depende de: EV4a.

### EV5 — `Chance`, `ForEachTarget` e `RemoveEffect`
- Escopo: `Runtime/GAS/Actions/ChanceAction.cs`, `ForEachTargetAction.cs`, `RemoveEffectAction.cs` (novos), `ActionRunner.cs` (alvo corrente do `ForEach`), `ActionContext.cs` (`LastRemovedStacks`), `Tests/GAS/PlayMode/FlowAndRemoveActionsTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red: `Chance30_At02Runs_At05Not` (CA14, C6, runtime); `ForEach3_RunsThreeTimes_CurrentTargetDefault_ChildChangesSetSafe` (CA18, C8); `RemoveEffect_ByAssetAndTag_NoneDoesNothing` (CA19, C9).
- Critério de pronto: CA18, CA19 e a parte de runtime do CA14 verdes.
- Tamanho estimado: ~420 linhas; 8 arquivos. Depende de: EV4b.

### EV6 — `Delayed`
- Escopo: `Runtime/GAS/Actions/DelayedAction.cs` (novo), `ActionRunner.cs` (execução agendada com dono), `AbilitySystemComponent.cs` (ativação termina depois das execuções dela), `Tests/GAS/PlayMode/DelayedActionTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red: `Delayed_DoesNotBlock_NextSameCall_DelayedAt2s` (CA16, C7); `Delayed_CancelledWithOwner_ContinueRuns` (CA17); `AbilityStaysActiveUntilDelayedRuns`.
- Critério de pronto: CA16 e CA17 verdes.
- Tamanho estimado: ~360 linhas; 6 arquivos. Depende de: EV5.

### EV7 — `ConditionalAction` com `Condition`
- Escopo: `FlowActions.cs` (`ConditionalAction`), `Runtime/GAS/Conditions/ConditionContext.cs` (`Action`, `Targets` da execução), `ActionContext.cs` (monta o `ConditionContext`), `HasTargetsCondition.cs` (lê os alvos da execução), `Tests/GAS/PlayMode/ConditionalActionTests.cs` (novo), `gas.md`, `CHANGELOG.md` (quebra do enum).
- Testes red: `Conditional_AndOrNot_ThenElse` (CA20); `EnumGone` (guarda textual); `Conditional_ThresholdUsesExecutionLevel`.
- Critério de pronto: CA20 verde.
- Tamanho estimado: ~320 linhas; 7 arquivos. Depende de: EV6, AV2.

### EV8 — Unidade-alvo e alcance até a borda
- Escopo: `AbilityTarget.cs` (`Unit`, `AtUnit`), `CastRangeCondition.cs` (`RangeMeasure`, unidade), `AbilitySystemComponent.cs` (unidade nos alvos do evento e no `ConditionContext`), `Tests/GAS/PlayMode/UnitTargetTests.cs` (novo), `gas.md`, `CHANGELOG.md`. **Emenda de 2026-10-31 (ADR 0035):** também `Runtime/GAS/Targeting/AreaSelector.cs` (campo `filter`, avaliado por candidato), o arquivo da `TagQueryCondition` (AV2; `ConditionSubject subject`, padrão `Owner`) e `Tests/GAS/PlayMode/AreaSelectorTests.cs`.
- Testes red: `UnitTarget_InRangeActivates_OutFailsWithReasonNoCost` (CA28, C19); `EdgeMeasure_UsesColliderClosestPoint`; `PointOverload_StillWorks`. Da emenda: `Area_FilterTagOnTarget_Max2_ExcludesOwner` (CA12, C4); `TagQuery_SubjectOwnerDefault_TargetUnitReadsUnit_NoUnitFalse`; `Sequence3Actions_AreaWithFilter_10000_DoNotAllocate` (CA34, C22).
- Critério de pronto: CA28, CA12 e CA34 verdes.
- Tamanho estimado: ~450 linhas; 9 arquivos (era ~360 / 6). Depende de: EV7, AV4.

### EV9 — `CommitMode` e `CommitAction`
- Escopo: `GameplayAbility.cs` (`CommitMode`), `Runtime/GAS/Actions/CommitAction.cs` (novo), `AbilitySpec.cs` (`IsCommitted`), `AbilitySystemComponent.cs` (`CommitAbility`, commit adiado), `Tests/GAS/PlayMode/CommitModeTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red: `Manual_NoChargeBeforeCommit_CancelBeforeFree_CommitTwiceOnce_FailCancelsAndReports` (CA29, C20); `OnStart_ChargesBeforeAnyAction` (CA30); `Manual_SpendsChargeAtCommit` (cargas do B-009).
- Critério de pronto: CA29 e CA30 verdes.
- Tamanho estimado: ~420 linhas; 7 arquivos. Depende de: EV8, AV7.

### EV10 — `SendGameplayEvent` e gatilhos
- Escopo: `Runtime/GAS/Abilities/GameplayEventData.cs`, `Runtime/GAS/Abilities/Triggers/AbilityTrigger.cs` (base, `GameplayEventTrigger`, `TagAddedTrigger`, `TagRemovedTrigger`), `Runtime/GAS/Actions/SendGameplayEventAction.cs` (novos), `GameplayAbility.cs` (`Triggers`), `AbilitySystemComponent.cs` (envio, índice de gatilhos, travas), `AbilityTarget.cs` (sobrecarga `AtUnit(unit, point)`; acrescentado pela exceção de 2026-11-03 da ADR 0003), `Tests/GAS/PlayMode/GameplayEventTriggerTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red: `SendEvent_ReachesTrigger_NoListenerNoAlloc_InvalidTagIgnored` (CA21, C13); `Trigger_UsesCheckOrder_FailureReported_SelfTriggerNoLoop` (CA22, C14); `TagAddedRemoved_Triggers`; `Trigger_PassesEventTargetAsUnit`.
- Critério de pronto: CA21 e CA22 verdes.
- Tamanho estimado: 583 linhas; 9 arquivos (era ~480 / 8; exceção única de 2026-11-03 da ADR 0003, pela margem de ~30%). Depende de: EV9.

### EV11 — Reações do efeito
- Escopo: `GameplayEffect.cs` (`OnTakeDamage`, `OnDeath`, `OnAbilityExecuted`, `damageAttributes`, `deathAttribute`), `AbilitySystemComponent.cs` (lista de instâncias com reação, disparo), `ActiveEffectInstance.cs`, `Tests/GAS/PlayMode/EffectReactionsTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red: `Thorns_OnTakeDamage_InstigatorAndDelta_OncePerOriginal` (CA23, C15); `OnDeath_WhenDeathAttributeDepleted`; `OnAbilityExecuted_OnActivate`; `Reactions_AfterPublicEvents`; `ReactionDamage_NestedPipeline_NoLoop`.
- Critério de pronto: CA23 verde.
- Tamanho estimado: ~430 linhas; 6 arquivos. Depende de: EV10, DM5.

#### Detalhe (2026-11-03)

Aprovado pelo Controlador em modo autônomo em 2026-11-03. Detalhe pela emenda de 2026-11-03 da ADR 0002, lido no código da branch `feat/gas-lote-c`. Caminhos relativos a `Packages/com.spellbook/`.

**Divisão.** Recontado no código, o EV11 inteiro dá ~600 linhas (runtime ~270, testes ~330) em 8 arquivos e passa do teto da 0003. Ele se divide em:
- **EV11a — Reações a dano e morte** (detalhado abaixo): a base das reações, `OnTakeDamage` e `OnDeath`.
- **EV11b — Reação "Ao executar habilidade"**: `OnAbilityExecuted` sobre a base do EV11a. Objetivo: a lista roda depois do `OnAbilityActivated` do dono e antes do `OnActivate`, com a unidade-alvo da ativação nos alvos do evento e o ponto da ativação. CA: a parte de `OnAbilityExecuted` do CA23. Depende do EV11a. É detalhado na entrada dele (~150 linhas e ~5 arquivos, como rascunho).
- O EV12 passa a depender do EV11b. O CA23 fica completo no EV11b. Os objetivos, os CA e as ADRs não mudam, então a divisão não volta ao gate (emenda de 2026-11-03 da 0002, item 3).

##### EV11a — Reações a dano e morte

Contexto no código:
- `Runtime/GAS/Effects/GameplayEffect.cs:113-131`: as listas `OnApplied`, `OnTick` e `OnRemoved` (`[ActionEvent(..., DefaultTarget.Owner, true)]`).
- `Runtime/GAS/Effects/ActiveEffectInstance.cs:167-180`: o `RunActions` privado monta o contexto do efeito (`EventTargets` = dono) e roda com a instância como dono lógico, então remover o efeito cancela as execuções dela (`AbilitySystemComponent.cs:2090`).
- `Runtime/GAS/Actions/ActionContext.cs:13-23` já tem `EffectEvent.TakeDamage` e `Death`; `:67` já tem `EventInstigator`. `:141-164` é o `Reset`.
- `Runtime/GAS/Actions/ActionRunner.cs:255-270` (`ResolveTargets`): sem seletor, a ação usa sempre os `EventTargets`. O `DefaultTarget` do `[ActionEvent]` hoje é só metadado. Até o EV10 os dois coincidiam, porque toda lista de efeito punha o dono nos alvos do evento. Nas reações eles se separam (tabela do Design 2: alvos do evento = quem causou; alvo padrão = dono do efeito). `:360-373` (`CopyContext`) copia o contexto para a execução agendada.
- `Runtime/GAS/Core/AbilitySystemComponent.cs`: `:1614-1648` `ApplyDurationEffect`; `:2081-2111` `RemoveEffectCore`; `:1741-1810` `ProcessChange`, que dispara `OnAttributeChangeApplied` em `:1797`, depois da gravação da base; `:1841-1847` `HandleAttributeDepleted`, que dispara `OnAttributeDepleted` com a causa de `GetLimitCause` (`:1870-1874`) durante a gravação da base, portanto antes do `OnAttributeChangeApplied` da mesma alteração.
- `Runtime/GAS/Attributes/Processing/AttributeChange.cs:52-89`: `AttributeChangeReport` (`Requested`, `Final`, `Applied`, `Effect`, `Context`, `RedirectedFrom`, `IsValid`).

Comportamento:
- **Efeito.** `GameplayEffect` ganha, depois de `OnRemoved`, as listas `OnTakeDamage` (`[ActionEvent("Ao receber dano", DefaultTarget.Owner, true)]`) e `OnDeath` (`[ActionEvent("Ao morrer", DefaultTarget.Owner, true)]`), no mesmo formato das outras (`[SerializeReference, TypePicker]`, campo público, lista vazia por padrão). Ganha também `[SerializeField] private List<AttributeDefinition> damageAttributes` (propriedade `IReadOnlyList<AttributeDefinition> DamageAttributes`, nunca nula) e `[SerializeField] private AttributeDefinition deathAttribute` (propriedade `DeathAttribute`), com tooltip. São campos novos e não precisam de `[FormerlySerializedAs]`. `internal bool HasReactions` vale verdadeiro quando `OnTakeDamage` ou `OnDeath` tem pelo menos uma entrada.
- **Alvo padrão de verdade.** O `ActionContext` ganha `internal DefaultTarget DefaultTarget` (padrão `EventTargets`, zerado no `Reset` e copiado no `CopyContext`). No `ResolveTargets`, sem seletor e fora de `ForEachTarget`, a execução raiz sem alvos herdados usa `context.Owner` quando o padrão é `Owner`, e os `EventTargets` nos outros casos. Quem dispara uma lista de efeito põe `DefaultTarget.Owner`, em todas as listas de efeito (`OnApplied`, `OnTick` e `OnRemoved` também), como a tabela do Design 2 manda. Nessas três o resultado não muda, porque os alvos do evento já são o dono. Assim o `DefaultTarget` do atributo vale no runtime, sem caso especial por evento, e o EV12 (`OnGranted`, `OnRemoved` da habilidade) usa o mesmo campo.
- **Variação no contexto (C15).** O `ActionContext` ganha `public AttributeChangeReport Change { get; internal set; }`: a alteração que causou a reação. Nas outras execuções vale o relatório `default` (`IsValid` falso). É zerado no `Reset` e copiado no `CopyContext`. As tags de dano saem de `Change.Effect.DamageTags`, sem campo novo. É acréscimo de API, não quebra.
- **Instância.** O `RunActions` de `ActiveEffectInstance` passa a receber o `EventInstigator`, o relatório e os alvos do evento. Um `internal void RunReaction(List<GameplayAction> actions, EffectEvent effectEvent, AbilitySystemComponent eventInstigator, in AttributeChangeReport change)` usa o mesmo caminho: os alvos do evento são o `eventInstigator` (nenhum se nulo ou destruído), o padrão é o dono e o dono lógico é a instância. Não há segundo caminho de montagem de contexto.
- **Despacho.** Classe nova `internal sealed class EffectReactions` (`Runtime/GAS/Effects/EffectReactions.cs`), criada pelo ASC no primeiro efeito com reação. Ela guarda:
  - a lista das instâncias ativas com `HasReactions`. O `ApplyDurationEffect` a acrescenta depois de pôr a instância em `_activeEffects`, e o `RemoveEffectCore` a tira antes do `CancelActions`;
  - a trava de reentrada, uma lista das instâncias reagindo agora, como a `_triggering` dos gatilhos (`:1255-1275`). Uma instância que já está rodando uma reação de forma síncrona ignora uma nova reação dela mesma, e a profundidade tem teto `MaxReactionDepth = 8`, com aviso único por ASC (`[GAS] Reações de '<nome>' passaram de 8 ...`). É a regra da P11 aplicada às reações. O teto do B-010 (`MaxChangeDepth`) vale por ASC e só com processadores, então sozinho não corta o vai e volta entre dois ASCs com espinhos.
  - A iteração vai de trás para frente com guarda de índice e pula instância com `IsActive` falso, então uma reação que remove efeitos, inclusive o próprio, não corrompe a lista.
- **`OnTakeDamage`.** Logo depois de `OnAttributeChangeApplied?.Invoke` (`:1797`), o ASC chama `_reactions?.AfterChangeApplied(in report)`. Cada instância reage quando: `report.Effect != null`, `report.RedirectedFrom == null`, `report.Requested < 0` e `DamageAttributes` vazia ou contendo `report.Attribute`. O `EventInstigator` é `report.Context.Instigator`. Isso dá uma vez por alteração original, também com redirecionamento (escudo) e com meta-atributo. Cura, alteração por código sem efeito e alteração redirecionada não disparam.
- **`OnDeath`.** No `HandleAttributeDepleted`, depois de `OnAttributeDepleted(state, in cause)`, o ASC chama `_reactions?.AfterDepleted(state, in cause)`. Reage a instância cujo `DeathAttribute` é `state.Definition`. O `EventInstigator` é `cause.Context.Instigator` se `cause.IsValid`, senão nulo. A chamada também acontece sem ouvinte público: o `return` antecipado de `:1844` deixa de cortar as reações.
- **Ordem fixa (P12).** Para a mesma causa, o evento público vem antes da reação. No golpe que mata, a ordem é: `OnAttributeDepleted`, reação `OnDeath`, `OnAttributeChangeApplied`, reação `OnTakeDamage`. É a ordem dos eventos públicos do B-010, que a reação segue, sem fila nova.
- **Custo zero sem reação.** Sem nenhuma instância com reação, os ganchos param em `_reactions == null` ou na lista vazia. Com reação, depois do aquecimento, o despacho não aloca: os contextos vêm do pool e a trava é lista reutilizada.
- **Fora do EV11a:** `OnAbilityExecuted` (EV11b); validação de `OnDeath` sem `deathAttribute` e de reação em efeito `Instant`, que não tem instância e nunca reage (EV16); abas de reação do Inspector (EV17). As listas novas aparecem no Inspector de hoje pelo `TypePicker`, sem código de Editor.

Escopo (7 arquivos, 606 linhas; exceção única de linhas de 2026-11-03 na 0003, pela margem de ~30%; estimativa era ~490):
- `Runtime/GAS/Effects/GameplayEffect.cs`: as duas listas, `damageAttributes`, `deathAttribute`, propriedades e `HasReactions`.
- `Runtime/GAS/Effects/EffectReactions.cs` (novo; o `.meta` não conta).
- `Runtime/GAS/Effects/ActiveEffectInstance.cs`: `RunActions` com instigador do evento, relatório e padrão `Owner`; `RunReaction`.
- `Runtime/GAS/Core/AbilitySystemComponent.cs`: criação de `_reactions`, registro na aplicação e na remoção, os dois ganchos e o fim do `return` antecipado do `HandleAttributeDepleted`.
- `Runtime/GAS/Actions/ActionContext.cs`: `Change`, `DefaultTarget`, `Reset`.
- `Runtime/GAS/Actions/ActionRunner.cs`: padrão `Owner` no `ResolveTargets`; `CopyContext` com os dois campos.
- `Tests/GAS/PlayMode/EffectReactionsTests.cs` (novo; o `.meta` não conta).
- Sem manual, catálogo ou referência da API (emendas de 2026-11-03 da 0007 e da 0040). Sem CHANGELOG: o commit não tem `!`, e o "Adicionado" fica para o passe de documentação. Nenhum tipo novo de catálogo: as listas usam a base `GameplayAction`, que já está nele. O `GameplayEffect` já tem `[HelpURL]`.

Testes red (PlayMode, tempo por `asc.Tick` com `ManualTimeSource`, objetos pela `GasFactory`):
- `Thorns_OnTakeDamage_InstigatorAndChange_OncePerOriginal` (CA23, C15): B tem um efeito com uma ação de registro em `OnTakeDamage`, e A aplica em B um efeito instantâneo de -10 na vida, com um `AbsorbWithAttribute` (escudo) na definição. A reação roda uma vez, com `EventInstigator == A`, `EventTargets == [A]`, `EffectEvent == TakeDamage`, `Change.Requested == -10` e `Change.RedirectedFrom == null`. Uma cura (+5) e um `ApplyAttributeChange` por código sem efeito não disparam.
- `OnTakeDamage_DamageAttributesFilter_EmptyMeansAny`: com `DamageAttributes = [Vida]`, dano em mana não dispara e em vida dispara. Com a lista vazia, os dois disparam.
- `Reaction_NoSelector_TargetsEffectOwner_EventTargetsSelectorTargetsInstigator`: na reação, uma ação sem seletor recebe `Targets == [B]` (dono do efeito), e uma com `EventTargetsSelector` recebe `[A]`. Um efeito com `OnApplied` sem seletor continua recebendo o dono.
- `OnDeath_WhenDeathAttributeDepleted_WithKiller`: `DeathAttribute = Vida`. Zerar a mana não dispara; o golpe de A que zera a vida dispara uma vez, com `EventInstigator == A` e `Change.Applied` igual à variação aplicada. Sem nenhum ouvinte em `OnAttributeDepleted`, dispara do mesmo jeito.
- `Reactions_AfterPublicEvents_LethalHitOrder`: o registro do golpe que mata fica `depleted-event`, `death-reaction`, `change-applied-event`, `damage-reaction`.
- `ReactionDamage_TwoThorns_BounceOnceNoLoop`: A e B com espinhos que devolvem 1 de dano a quem causou (`ApplyEffectAction` com `EventTargetsSelector`). A aplica 10 em B: B devolve 1 a A, A devolve 1 a B, e a trava corta aí. Cada reação roda o número de vezes esperado, sem `StackOverflowException`, e as vidas finais batem.
- `ReactionDepth_CappedAt8_WarnsOnce`: dez ASCs em cadeia, cada um com uma reação que causa dano ao seguinte. Rodam 8 reações aninhadas, e `LogAssert` espera um aviso só.
- `Reaction_RemovesOwnEffect_IterationSafe_RemovedStopsReacting`: uma reação que remove o próprio efeito (`RemoveEffectAction`) não quebra a iteração e não roda outra vez no golpe seguinte. Um segundo efeito com reação no mesmo ASC continua reagindo nos dois golpes. A espera dentro de uma reação é cancelada quando o efeito sai.
- `Reactions_AfterWarmup_DoNotAllocate`: com dois efeitos com reação e 1000 golpes, depois do aquecimento, `GC.GetAllocatedBytesForCurrentThread` não muda. Sem efeito com reação, os mesmos golpes também não alocam.

Critério de pronto: os 9 testes verdes. A suíte inteira verde, com `ActionRunnerTests`, `TargetSelectorTests` e os testes de efeito do EV2 sem edição. A guarda `EveryCatalogTypeHasSummary` verde sem tipo novo. Um efeito sem `OnTakeDamage` e sem `OnDeath` se comporta como antes.

Tamanho estimado: ~490 linhas; 7 arquivos (testes ~270, `EffectReactions.cs` ~110, `GameplayEffect.cs` ~35, `ActiveEffectInstance.cs` ~25, ASC ~25, `ActionContext.cs` ~15, `ActionRunner.cs` ~10). Depende de: EV10, DM5, PD1 (fila). Commit: `feat(gas): reações do efeito a dano e morte`.

##### EV11b — Reação "Ao executar habilidade"

###### Detalhe (2026-11-03)

Aprovado pelo Controlador em modo autônomo em 2026-11-03. Detalhe pela emenda de 2026-11-03 da ADR 0002, lido no código da branch `feat/gas-lote-c` depois do EV11a (`a736434`). Caminhos relativos a `Packages/com.spellbook/`. Cabe no teto da 0003, sem divisão.

Contexto no código:
- `Runtime/GAS/Effects/GameplayEffect.cs:133-158`: `OnTakeDamage`, `OnDeath`, `damageAttributes`, `deathAttribute` e `HasReactions` (só as duas listas do EV11a).
- `Runtime/GAS/Effects/EffectReactions.cs:31-60`: `AfterChangeApplied` e `AfterDepleted` iteram de trás para frente e chamam `TryReact` (`:74-103`), que aplica a trava `Reacting` (estática, entre ASCs) e o teto `MaxReactionDepth`. `TryReact` e `RunReaction` recebem hoje um `AttributeChangeReport`, sem lugar para ponto nem para um alvo do evento diferente do instigador.
- `Runtime/GAS/Effects/ActiveEffectInstance.cs:167-202`: o `RunActions` privado é o caminho único de montagem do contexto do efeito; as listas de ciclo de vida passam `(null, Target, default)` e o `RunReaction` passa `(instigador, instigador, change)`.
- `Runtime/GAS/Core/AbilitySystemComponent.cs:902-947` (`ActivateSpec`): o contexto do `OnActivate` recebe o ponto (`:916`) e a unidade-alvo nos alvos do evento (`:918`); `OnAbilityActivated` em `:926`; a guarda `:929` encerra se a ativação foi cancelada ou trocada; o `OnActivate` roda em `:933`. Todas as origens de ativação (código, gatilho do EV10, `TryActivateInternal`) passam por aqui.
- `Runtime/GAS/Actions/ActionContext.cs:23`: `EffectEvent.AbilityExecuted` já existe.

Comportamento:
- **Efeito.** `GameplayEffect` ganha, depois de `OnDeath`, a lista `OnAbilityExecuted` (`[SerializeReference, TypePicker]`, `[ActionEvent("Ao executar habilidade", DefaultTarget.Owner, true)]`, tooltip "Roda quando o dono ativa uma habilidade. Os alvos do evento são a unidade-alvo da ativação.", lista vazia por padrão). `HasReactions` passa a contar também essa lista.
- **Causa da reação, um tipo só.** Para não abrir um segundo caminho nem multiplicar parâmetros, a causa vira `internal readonly struct EffectEventCause` (em `EffectReactions.cs`): `EventInstigator`, `EventTarget` (ASC ou nulo), `Change` (`AttributeChangeReport`), `HasPoint`, `Point`. Fábricas: `Lifecycle(owner)` (alvo do evento = dono; usada por `OnApplied`, `OnTick`, `OnRemoved`), `FromChange(in report, instigator)` (instigador e alvo do evento = quem causou; usada por dano e morte, sem mudança de comportamento) e `FromActivation(owner, in AbilityTarget target)` (instigador do evento = o dono que ativou; alvo do evento = `target.Unit`; ponto = `target.Point` quando `target.HasPoint`; `Change` = `default`). O `RunActions` privado de `ActiveEffectInstance` passa a receber `in EffectEventCause` e é o único lugar que preenche `EventInstigator`, `EventTargets`, `Change` e o ponto (`SetPoint`). `RunReaction(actions, effectEvent, in EffectEventCause)` substitui a assinatura do EV11a; `TryReact` repassa a causa. Tudo interno: sem quebra de API.
- **Contexto da reação.** `context.Ability` e `context.Spec` continuam nulos: a reação é do efeito, não da habilidade, e ações como a `CommitAction` leem o `Spec`. O efeito, o nível, a instância, o `EffectEvent == AbilityExecuted` e o padrão `Owner` seguem o caminho único do EV11a. Para filtrar por habilidade, a reação usa uma `Condition` sobre as tags do dono (as `ActivationOwnedTags` já foram concedidas em `:923`). Um campo com a habilidade executada fica fora (não pedido pelo PRD nem pelo CA23).
- **Despacho.** `EffectReactions.AfterAbilityActivated(in AbilityTarget target)`, no mesmo formato dos outros dois (sai cedo com a lista vazia, de trás para frente, guarda de índice, pula `IsActive` falso, `TryReact` com a trava e o teto). No `ActivateSpec`, o ASC chama `_reactions?.AfterAbilityActivated(in target)` logo depois de `OnAbilityActivated?.Invoke(spec)` e **antes** da guarda de `:929`, de modo que uma reação que cancela ou troca a ativação impede o `OnActivate`, sem guarda nova. Ordem fixa (P12): evento público, reação, `OnActivate`.
- **Sem laço.** Uma reação que ativa outra habilidade do dono (por `SendGameplayEventAction` e gatilho, por exemplo) dispara de novo o `AfterAbilityActivated`, mas a mesma instância está em `Reacting` e é ignorada; instâncias diferentes respeitam o teto de 8. Mesma regra da P11, sem mecanismo novo.
- **Falha não reage.** Ativação que falha nas checagens não chega ao `ActivateSpec`, então não há reação, como não há `OnAbilityActivated`.
- **Custo zero sem reação.** Sem efeito com reação, o gancho para em `_reactions == null` ou na lista vazia; com reação, depois do aquecimento, não aloca (`EffectEventCause` é struct passada por `in`).
- **Fora do EV11b:** validação de reação em efeito `Instant` (EV16); aba "Reações" do Inspector (EV17); o `ActivateSpec` continua chamando `SetPoint` sem olhar `HasPoint` para o `OnActivate` (fora do escopo; não muda aqui).

Escopo (5 arquivos):
- `Runtime/GAS/Effects/GameplayEffect.cs`: `OnAbilityExecuted` e `HasReactions`.
- `Runtime/GAS/Effects/EffectReactions.cs`: `EffectEventCause`, `AfterAbilityActivated`, `TryReact` e os dois despachos do EV11a com a causa.
- `Runtime/GAS/Effects/ActiveEffectInstance.cs`: `RunActions` e `RunReaction` com `EffectEventCause`.
- `Runtime/GAS/Core/AbilitySystemComponent.cs`: o gancho no `ActivateSpec`.
- `Tests/GAS/PlayMode/EffectReactionsTests.cs`: os testes abaixo, com os ajudantes atuais e habilidades da `GasFactory`.
- Sem manual, catálogo ou referência da API (emendas de 2026-11-03 da 0007 e da 0040). Sem CHANGELOG: não há quebra, e o "Adicionado" fica para o passe de documentação. Nenhum tipo novo de catálogo.

Testes red (PlayMode, tempo por `asc.Tick` com `ManualTimeSource`, objetos pela `GasFactory`):
- `OnAbilityExecuted_AfterActivatedEvent_BeforeOnActivate` (CA23, parte; P12): B tem um efeito com uma ação de registro em `OnAbilityExecuted`, e a habilidade de B tem uma ação de registro em `OnActivate`. Ativar dá o registro `activated-event`, `executed-reaction`, `on-activate`, com a reação uma vez por ativação.
- `OnAbilityExecuted_Context_UnitPointInstigatorAndDefaultOwner`: ativando com `AbilityTarget.AtUnit(C, p)`, a reação vê `EffectEvent == AbilityExecuted`, `EventInstigator == B`, `EventTargets == [C]`, `HasPoint` e `Point == p`, `Change.IsValid` falso, `Ability` e `Spec` nulos. Uma ação sem seletor recebe `[B]`, e uma com `EventTargetsSelector` recebe `[C]`. Com `AtPoint(p)`, os alvos do evento ficam vazios e o ponto é `p`; com `AbilityTarget.None`, sem ponto.
- `OnAbilityExecuted_FailedActivation_DoesNotRun_TriggerActivationRuns` (CA21, parte; C13): sem custo pago a ativação falha e a reação não roda; uma ativação por `GameplayEventTrigger` (`SendGameplayEvent`) roda a reação uma vez.
- `OnAbilityExecuted_ReactionCancelsActivation_OnActivateSkipped`: a reação aplica no dono um efeito que concede uma tag de cancelamento da habilidade; o `OnActivate` não roda, sem exceção, e a habilidade termina inativa.
- `OnAbilityExecuted_ReactionActivatesAbility_NoLoop`: a reação ativa uma segunda habilidade do dono por evento de gameplay; a reação roda uma vez por ativação externa, a segunda habilidade roda o `OnActivate` dela, e não há aviso de teto.
- `OnAbilityExecuted_RemovedEffect_StopsReacting_AfterWarmupNoAlloc`: depois de remover o efeito, a ativação não reage; com o efeito ativo e 1000 ativações (habilidade sem custo e sem recarga), depois do aquecimento `GC.GetAllocatedBytesForCurrentThread` não muda.

Critério de pronto: os 6 testes novos e os 9 do EV11a verdes, os do EV11a sem edição. A suíte inteira verde, com `GameplayEventTriggerTests`, `CommitModeTests` e `UnitTargetTests` sem edição. A guarda `EveryCatalogTypeHasSummary` verde sem tipo novo. Um efeito sem `OnAbilityExecuted` se comporta como antes. CA23 completo.

Tamanho estimado: ~220 linhas; 5 arquivos (testes ~150, `EffectReactions.cs` ~40, `ActiveEffectInstance.cs` ~15, `GameplayEffect.cs` ~10, ASC ~3). Depende de: EV11a. Commit: `feat(gas): reação do efeito ao executar habilidade`.

### EV12 — Passive e Toggle
- Escopo: `Runtime/GAS/Abilities/AbilityBehavior.cs` (novo), `GameplayAbility.cs` (comportamento, `OnGranted`, `OnRemoved`), `AbilitySpec.cs` (`IsToggledOn`), `AbilitySystemComponent.cs` (passiva na concessão, toggle, `OnToggleChanged`, listas de concessão), `Tests/GAS/PlayMode/PassiveTests.cs`, `Tests/GAS/PlayMode/ToggleTests.cs` (novos), `gas.md`, `CHANGELOG.md`.
- Testes red: `Passive_ActiveOnGrant_EndsOnRemoval_OnEndRuns` (CA24, C16); `Toggle_OnOff_CostOnlyOnOn_StateAndEvent` (CA25, C17); `Toggle_OwnedTags_GrantedAndReturned` (CA27, parte); `GrantedRemovedLists_Run`.
- Critério de pronto: CA24, CA25 e a parte de Toggle do CA27 verdes.
- Tamanho estimado: ~480 linhas; 8 arquivos. Depende de: EV11 (EV11b depois da divisão de 2026-11-03).

#### Detalhe (2026-11-03)

Aprovado pelo Controlador em modo autônomo em 2026-11-03. Detalhe pela emenda de 2026-11-03 da ADR 0002, lido no código da branch `feat/gas-lote-c` depois do EV11b (`31cdd33`). Caminhos relativos a `Packages/com.spellbook/`. Cabe no teto da 0003, sem divisão.

Contexto no código:
- `Runtime/GAS/Abilities/GameplayAbility.cs:93-114`: as listas `OnActivate`, `OnEnd` e `OnCancel` (`[ActionEvent(..., DefaultTarget.EventTargets, true)]`) e os `triggers`; `:224-262` `Validate` e `CollectActionEffects` percorrem as três listas.
- `Runtime/GAS/Abilities/AbilitySpec.cs:14-27`: `IsActive`, `IsCommitted`, `ActiveContext`, `Execution`, `Executing`, `ActivationId`.
- `Runtime/GAS/Core/AbilitySystemComponent.cs`: `:97-109` `Awake` concede `startingAbilities` antes de `ApplyStartingEffects`, que é quem chama `attributeSet.EnsureInitialized()` (`:115`); `:339-365` `GiveAbility` (lista, `IndexTriggers`, `OnAbilityGranted`) e `RemoveAbility` (`CancelAbility`, `UnindexTriggers`, saída da lista, `OnAbilityRemoved`); `:769-779` `TryActivate`, caminho único de código e gatilho, que sempre dispara `OnAbilityFailed`; `:782-828` `CheckActivation` (recusa `AlreadyActive` em `:786`); `:902-949` `ActivateSpec`, que termina a habilidade quando a execução de `OnActivate` acaba (`:940-948`); `:250-259` `EndFinishedAbilities`; `:1004-1017` `CancelAbility`; `:1053-1072` `EndAbility` (lista `OnEnd` ou `OnCancel`, `RemoveTags(ActivationOwnedTags)`, `OnAbilityEnded`); `:1074-1084` `CopyActivationContext`.
- `Runtime/GAS/Actions/ActionContext.cs:75`: `DefaultTarget` (do EV11a), que o `ResolveTargets` já respeita; `ActionRunner.cs:72-78` `RentContext` põe o ASC como `Instigator`.

Comportamento:
- **Tipo.** `Runtime/GAS/Abilities/AbilityBehavior.cs` (novo): `public enum AbilityBehavior { Instant, Passive, Toggle }`, com `<summary>` por valor. O `Channel` entra no EV13, no mesmo enum, sem quebra. `GameplayAbility` ganha `[SerializeField] private AbilityBehavior behavior = AbilityBehavior.Instant` no cabeçalho "Ativação", com tooltip, e a propriedade `Behavior` (get e set, como `CommitMode`). Campo novo, sem `[FormerlySerializedAs]`; assets antigos abrem como `Instant`, o comportamento de hoje.
- **Ativação sustentada.** `GameplayAbility` ganha `internal bool IsSustained => behavior == Passive || behavior == Toggle`. No `ActivateSpec`, depois do `Runner.Run` e da guarda de `:937`, uma habilidade sustentada **não termina** quando o `OnActivate` acaba: o ASC não marca `Executing` e não chama `EndAbility`. A ativação fica com `IsActive`, as `ActivationOwnedTags` e o `ActiveContext` até ser desligada, retirada ou cancelada. O `EndFinishedAbilities` não muda, porque só olha `Executing`. As execuções do spec (uma espera ou uma `DelayedAction` em `OnActivate`) continuam com o spec como dono e são canceladas no fim, como hoje. É o mesmo ponto que o `Channel` do EV13 usa para terminar pelo relógio, sem segundo caminho.
- **Ativação silenciosa.** `TryActivate` ganha `bool silent`: com ele, a falha não dispara `OnAbilityFailed`. Código e gatilhos continuam com `silent = false`; a passiva usa `true`, e o autocast do EV14 vai usar o mesmo parâmetro (P16: tentativas silenciosas). Interno, sem mudança de API.
- **Passive.** Na concessão, depois de `OnAbilityGranted` e da lista `OnGranted`, se o spec continua concedido e a habilidade é `Passive`, o ASC chama `TryActivate(spec, ability, AbilityTarget.None, silent: true)`: passa pelas checagens e pelo commit como qualquer ativação (custo e recarga configurados são cobrados; o validador do EV16 avisa). Na retirada, uma passiva ativa termina **normalmente**: o ASC cancela as execuções do spec (`CancelActions(spec)`, `OnCancel` das ações em curso em ordem reversa) e chama `EndAbility(spec, false)`, que roda `OnEnd`, devolve as tags e dispara `OnAbilityEnded(spec, false)` (C16, R24). Cancelar por código ou por `CancelledByTags` usa o `CancelAbility` de hoje (lista `OnCancel`, `OnAbilityEnded(spec, true)`) e a deixa inativa até uma ativação manual ou a reativação do EV14. Ativar à mão uma passiva já ativa dá `AlreadyActive`, como hoje.
- **`startingAbilities`.** O `Awake` passa a chamar `attributeSet.EnsureInitialized()` antes de conceder as habilidades de partida (a chamada sai de `ApplyStartingEffects`), para uma passiva de partida conferir custo e aplicar efeitos com o `AttributeSet` pronto. Os efeitos de partida continuam depois das habilidades.
- **Toggle, ligar.** Passa pelas checagens e pelo commit (custo só ao ligar, P3) e fica sustentada. `OnToggleChanged(spec, true)` sai logo depois de `OnAbilityActivated` e antes da reação `AfterAbilityActivated` do EV11b.
- **Toggle, desligar.** No `CheckActivation`, logo depois das checagens de habilidade e spec nulos, um spec `Toggle` com `IsActive` dá `Success` sem as outras checagens (`GetActivationResult` também, então a UI mostra o botão como acionável). No `TryActivate`, com sucesso e o spec ligado, o ASC chama `CancelAbility(spec)` em vez de `ActivateSpec`: cancela as execuções da ativação (`OnCancel` reverso), roda a lista `OnCancel`, devolve as `ActivationOwnedTags` e termina com `OnAbilityEnded(spec, true)`. Não cobra nada, e um gatilho de evento ou de tag numa habilidade ligada também desliga (é o mesmo caminho único).
- **Evento de toggle.** `public event Action<AbilitySpec, bool> OnToggleChanged` no ASC. Desligar sai no `EndAbility` de um `Toggle`, depois de `RemoveTags` e antes de `OnAbilityEnded`, então vale para todo fim: `TryActivateAbility`, `CancelAbility`, `CancelledByTags`, `CancelAbilitiesWithTag`, retirada e `OnDisable`. Ordem fixa: ligar = `OnAbilityActivated`, `OnToggleChanged(true)`; desligar = `OnToggleChanged(false)`, `OnAbilityEnded`.
- **Leitura.** `AbilitySpec.IsToggledOn` (`IsActive && Ability.Behavior == Toggle`), propriedade só de leitura, no mesmo lugar de `IsActive` e do `IsChanneling` do EV13. Decisão deste detalhe: o Design 7 escreve `IsToggledOn(spec)`, e o escopo do rascunho já punha a leitura no `AbilitySpec.cs`; um método no ASC duplicaria a leitura.
- **Tags (R27).** Toggle concede e devolve as `ActivationOwnedTags` pelo `AddTags` do `ActivateSpec` e pelo `RemoveTags` do `EndAbility` de hoje. Nenhum código novo; só o teste do CA27 (parte de Toggle).
- **Listas de concessão.** `GameplayAbility` ganha, depois de `OnCancel`, `OnGranted` (`[ActionEvent("Ao ser concedida", DefaultTarget.Owner, false)]`) e `OnRemoved` (`[ActionEvent("Ao ser retirada", DefaultTarget.Owner, false)]`), `[SerializeReference, TypePicker]`, campo público, lista vazia por padrão, com `<summary>`. `Validate` e `CollectActionEffects` passam a percorrer as cinco listas. O ASC roda cada uma por um ajudante privado único, `RunGrantList(spec, actions)`: contexto do pool com `Ability`, `Spec`, `Level`, `Instigator` = o ASC, `DefaultTarget.Owner`, sem alvos do evento e sem ponto, e **sem dono lógico** (como `OnEnd`): uma espera continua depois do fim e não segura a ativação. Lista vazia não aluga contexto.
- **Ordem da concessão e da retirada (espelhadas).** Conceder: entra na lista, `IndexTriggers`, `OnAbilityGranted`, `OnGranted`, ativação da passiva. Retirar: fim da ativação (passiva termina normal; as outras, `CancelAbility` como hoje), `UnindexTriggers`, sai da lista, `OnAbilityRemoved`, `OnRemoved`. O evento público vem antes da lista, como nas reações (P12). Uma ação de `OnGranted` que retira a habilidade impede a ativação da passiva (guarda por `_abilities.Contains(spec)`); uma ação de `OnRemoved` que concede a mesma habilidade cria um spec novo, porque o antigo já saiu da lista.
- **Fora do EV12:** `Channel` e leitura de canalização (EV13); reativação da passiva e `SetAutocast` recusando `Passive`/`Toggle` (EV14); avisos e erros do validador (passiva com custo, autocast em `Passive`/`Toggle`; EV16); abas novas do Inspector (EV17). As listas e o `behavior` aparecem no Inspector de hoje sem código de Editor.

Escopo (6 arquivos):
- `Runtime/GAS/Abilities/AbilityBehavior.cs` (novo; o `.meta` não conta).
- `Runtime/GAS/Abilities/GameplayAbility.cs`: `behavior`, `Behavior`, `IsSustained`, `OnGranted`, `OnRemoved`, `Validate` e `CollectActionEffects` com as cinco listas.
- `Runtime/GAS/Abilities/AbilitySpec.cs`: `IsToggledOn`.
- `Runtime/GAS/Core/AbilitySystemComponent.cs`: `OnToggleChanged`, `Awake` (`EnsureInitialized` antes das habilidades), `GiveAbility` e `RemoveAbility` (listas, passiva, ordem), `TryActivate` (`silent`, desligar), `CheckActivation` (toggle ligado), `ActivateSpec` (sustentada, evento de ligar), `EndAbility` (evento de desligar), `RunGrantList`.
- `Tests/GAS/PlayMode/PassiveTests.cs` (novo; o `.meta` não conta).
- `Tests/GAS/PlayMode/ToggleTests.cs` (novo; o `.meta` não conta).
- Sem manual, catálogo ou referência da API (emendas de 2026-11-03 da 0007 e da 0040). Sem CHANGELOG: não há quebra (o padrão `Instant` mantém os assets e o código de hoje), e o "Adicionado" fica para o passe de documentação. Nenhum tipo novo de catálogo: o enum não tem `[TypePicker]`, e as listas usam a base `GameplayAction`. O `GameplayAbility` já tem `[HelpURL]`.

Testes red (PlayMode, tempo por `asc.Tick` com `ManualTimeSource`, objetos pela `GasFactory`):
- `Passive_ActiveOnGrant_EndsOnRemoval_OnEndRuns` (CA24, C16): conceder uma `Passive` sem custo e sem recarga a ativa na hora, sem `OnAbilityFailed`; ela continua ativa depois de 10 `Tick` com o `OnActivate` já acabado. Retirar roda `OnEnd` (e não `OnCancel`) e dispara `OnAbilityEnded(spec, false)`; o registro fica `granted-event`, `activated-event`, `on-activate`, `on-end`, `ended-event`, `removed-event`.
- `Passive_WaitInOnActivate_CancelledOnRemoval_ReverseOnCancel`: uma espera em `OnActivate` recebe `OnCancel` na retirada, antes de `OnEnd`.
- `Passive_FailedChecks_Silent_CancelByTagEnds`: uma `Passive` com `RequiredTags` ausente não ativa na concessão e não dispara `OnAbilityFailed`; outra, ativa, recebe uma tag de `CancelledByTags`, roda `OnCancel`, termina com `cancelled == true` e fica inativa depois de outros `Tick`.
- `Passive_InStartingAbilities_ActiveAfterAwake`: uma `Passive` com custo de mana em `startingAbilities` está ativa depois do `Awake` e cobrou o custo.
- `Toggle_OnOff_CostOnlyOnOn_StateAndEvent` (CA25, C17): ligar cobra o custo, `IsToggledOn` vira verdadeiro e `OnToggleChanged(spec, true)` sai uma vez; a habilidade continua ligada depois de `Tick`. Ativar de novo, mesmo sem mana para o custo e com a recarga correndo, devolve `Success`, não cobra, roda a lista `OnCancel` (que remove o efeito infinito aplicado ao ligar), `IsToggledOn` vira falso e `OnToggleChanged(spec, false)` sai uma vez, antes de `OnAbilityEnded(spec, true)`. `GetActivationResult` com a habilidade ligada dá `Success`.
- `Toggle_OffByCancelTagOrRemoval_RaisesToggleChanged`: `CancelledByTags`, `CancelAbility` e `RemoveAbility` desligam e disparam `OnToggleChanged(spec, false)` uma vez cada.
- `Toggle_OwnedTags_GrantedAndReturned` (CA27, parte): ligada, o dono tem as `ActivationOwnedTags`; desligada, não tem, e a contagem de referência volta ao valor de antes.
- `GrantedRemovedLists_Run`: `OnGranted` roda uma vez na concessão e `OnRemoved` uma vez na retirada, com `Spec` e `Level` da habilidade e `Targets == [dono]` sem seletor; conceder de novo uma habilidade já concedida não roda `OnGranted`. Uma `Instant` com essas listas continua terminando ao fim de `OnActivate`.

Critério de pronto: os 8 testes verdes. A suíte inteira verde, com `AbilityGrantEventsTests`, `AbilityEndCancelListsTests`, `AbilityFailedEventTests`, `ActivationQueryTests`, `CommitModeTests`, `GameplayEventTriggerTests`, `StartingEffectsTests` e `EffectReactionsTests` sem edição. A guarda `EveryCatalogTypeHasSummary` verde sem tipo novo. Uma habilidade `Instant` se comporta como antes. CA24, CA25 e a parte de Toggle do CA27 verdes.

Tamanho estimado: ~470 linhas; 6 arquivos (testes ~330, ASC ~85, `GameplayAbility.cs` ~35, `AbilityBehavior.cs` ~15, `AbilitySpec.cs` ~5). Depende de: EV11b. Commit: `feat(gas): comportamentos Passive e Toggle`.

### EV13 — Channel e leitura para a barra de cast
- Escopo: `GameplayAbility.cs` (`channelDuration`, `channelTickInterval`, `OnChannelTick`), `AbilitySpec.cs` (`IsChanneling`, `ChannelDuration`), `AbilitySystemComponent.cs` (relógio da canalização, passos, fim, leitura), `Tests/GAS/PlayMode/ChannelTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red: `Channel3s_EndsCleanly_OnEndRuns`, `NoDuration_UntilCancelled`, `CancelByTagAndCode_ReverseOnCancel` (CA26, C18); `ChannelTicks_PerInterval_CountsSameFrameTick`; `Channel_OwnedTags` (CA27 completo); `ChannelReadApi_ElapsedRemainingNormalized_IndeterminateZero`.
- Critério de pronto: CA26 e CA27 verdes.
- Tamanho estimado: ~420 linhas; 6 arquivos. Depende de: EV12.

#### Detalhe (2026-11-03)

Detalhe pela emenda de 2026-11-03 da ADR 0002, lido no código da branch `feat/gas-lote-c` depois do EV12 (`6ca2344`). Caminhos relativos a `Packages/com.spellbook/`. Cabe no teto da 0042, sem exceção.

- CA: CA26 (C18) e CA27 completo (parte de Channel; a de Toggle veio no EV12). Leitura da barra de cast do Design 7 (P7 do B-016).
- Contexto no código: `Runtime/GAS/Abilities/AbilityBehavior.cs:62-70` (enum sem `Channel`); `GameplayAbility.cs:68-69` (`behavior`), `:104-125` (cinco listas), `:205-206` (`IsSustained`), `:249-291` (`Validate` e `CollectActionEffects`); `AbilitySpec.cs:14-16` (`IsActive`, `IsToggledOn`); `AbilitySystemComponent.cs:225-253` (`Tick`: executor e `EndFinishedAbilities`), `:383-390` (`EndPassive`: cancela as execuções do spec e termina normal), `:952-1007` (`ActivateSpec`; sustentada em `:994`), `:1061-1074` (`CancelAbility`), `:1110-1130` (`EndAbility`), `:1132-1142` (`CopyActivationContext`); `ActionRunner.cs:169-249` (cursor lógico pelo `TickedTime`).
- Comportamento:
  - **Tipo e dados.** `AbilityBehavior.Channel` no fim do enum (valor 3, sem quebra). `GameplayAbility` ganha, no cabeçalho "Ativação", `[SerializeField] ScalableFloat channelDuration = ScalableFloat.Constant(0f)` e `[SerializeField, Min(0f)] float channelTickInterval`, com tooltip, e as propriedades `ChannelDuration` (nulo volta a 0, como `CastRange`) e `ChannelTickInterval` (negativo vira 0). Lista nova `OnChannelTick` (`[ActionEvent("A cada passo", DefaultTarget.EventTargets, true)]`), depois de `OnCancel`; `Validate` e `CollectActionEffects` passam às seis listas. `IsSustained` inclui `Channel`: a ativação não termina quando `OnActivate` acaba (o mesmo ponto do EV12). Os avisos do validador (passo fora de `Channel`, `Channel` sem duração e sem `CancelledByTags`) ficam no EV16.
  - **Início.** No `ActivateSpec`, logo depois de `spec.ActiveContext = context` e antes de `OnAbilityActivated`, um `Channel` grava no spec a duração aplicada (`ChannelDuration.Evaluate(spec.Level, ability.Values)`, pelo menos 0), o início (`TickedTime`) e o próximo passo (início + intervalo; intervalo 0: sem passos). A UI lê a duração já no `OnAbilityActivated`.
  - **Relógio.** No `Tick`, depois de `_runner.Tick()` e antes de `EndFinishedAbilities`, o ASC avança cada spec que canaliza pelo cursor lógico: roda `OnChannelTick` para cada passo com horário ≤ `TickedTime` e, com duração, ≤ início + duração (um `Tick` grande roda todos os passos que cobriu; o passo no mesmo horário do fim roda antes do fim). Cada passo roda com uma cópia do contexto da ativação (`CopyActivationContext`, alvos do evento e ponto), com o spec como dono, então cancelar ou terminar cancela um passo em curso. Uma ação que cancele ou termine a habilidade interrompe a sequência (guarda por `ActivationId`). O intervalo é lido no início da canalização e guardado no spec; mudá-lo no meio só vale na próxima ativação (correção da revisão: intervalo 0 ao vivo travava o laço).
  - **Fim.** Com duração > 0 e `TickedTime` ≥ início + duração, o ASC cancela as execuções do spec (`OnCancel` reverso das ações em curso de `OnActivate` e dos passos) e chama `EndAbility(spec, false)`: lista `OnEnd`, devolve as `ActivationOwnedTags`, `OnAbilityEnded(spec, false)`. É o `EndPassive` de hoje, renomeado para `EndSustained` (mesmo corpo). Duração 0 canaliza até `CancelAbility`, `CancelledByTags`, `CancelAbilitiesWithTag`, retirada ou `OnDisable`, todos pelo `CancelAbility` de hoje (`OnCancel`, `OnAbilityEnded(spec, true)`).
  - **Leitura.** `AbilitySpec.IsChanneling` (`IsActive && Ability.Behavior == Channel`) e `AbilitySpec.ChannelDuration` (aplicada na ativação em curso; 0 é indeterminada, e volta a 0 no `EndAbility`, antes de rodar "Ao terminar"/"Ao ser cancelada", para que uma reativação nessas listas mantenha a duração nova). No ASC: `GetChannelElapsed(spec)` (segundos desde o início pelo `TickedTime`, limitado à duração quando há duração), `GetChannelRemaining(spec)` (duração − decorrido; indeterminada: 0) e `GetChannelNormalized(spec)` (decorrido / duração em [0, 1]; indeterminada: 0). Spec nulo ou que não canaliza: 0. Início e fim pelos `OnAbilityActivated` e `OnAbilityEnded`, sem evento novo.
  - **Tags (R27).** Pelo `AddTags` do `ActivateSpec` e o `RemoveTags` do `EndAbility` de hoje. Só o teste.
- Escopo (5 arquivos):
  - `Runtime/GAS/Abilities/AbilityBehavior.cs:62-70`: `Channel`. (O rascunho não o listava; o Detalhe do EV12 já o previa no mesmo enum.)
  - `Runtime/GAS/Abilities/GameplayAbility.cs:68-69`, `:104-125`, `:198-206`, `:249-291`: campos, propriedades, `OnChannelTick`, `IsSustained`, seis listas.
  - `Runtime/GAS/Abilities/AbilitySpec.cs:14-16`: `IsChanneling`, `ChannelDuration`, estado interno do relógio (início, próximo passo).
  - `Runtime/GAS/Core/AbilitySystemComponent.cs:225-253` (`Tick`), `:383-390` (`EndSustained`), `:952-1007` (início), `:1110-1130` (zera a duração), leitura nova: `AdvanceChannels`, `GetChannelElapsed`, `GetChannelRemaining`, `GetChannelNormalized`.
  - `Tests/GAS/PlayMode/ChannelTests.cs` (novo; o `.meta` não conta).
  - Sem manual, catálogo, referência da API nem CHANGELOG (emendas de 2026-11-03 da 0007 e da 0040; sem quebra: campos novos com padrão, enum acrescido no fim). Nenhum tipo novo de catálogo.
- Testes red (PlayMode, `ChannelTests`, `autoTick` desligado, `ManualTimeSource`, `GasFactory`):
  - `Channel3s_EndsCleanly_OnEndRuns` (CA26, C18): duração 3 e uma espera de 10 s em `OnActivate`; ativa depois de 2 `Tick(1)`; no 3º termina com `OnAbilityEnded(spec, false)`, a espera recebe `OnCancel` antes de `OnEnd`, e a lista `OnCancel` não roda.
  - `NoDuration_UntilCancelled` (CA26): duração 0 continua ativa depois de `Tick(100)`; `CancelAbility` termina com `cancelado == true` e roda `OnCancel`.
  - `CancelByTagAndCode_ReverseOnCancel` (CA26; casos tag e código): ações em curso de `OnActivate` e de um passo recebem `OnCancel` da mais interna para a mais externa, depois a lista `OnCancel`, e `OnAbilityEnded(spec, true)`.
  - `ChannelTicks_PerInterval_CountsSameFrameTick`: duração 3, intervalo 1: `Tick(0.5)` nenhum passo; `Tick(0.5)` um; `Tick(2)` mais dois, o do horário 3 antes de `OnEnd`, e termina.
  - `Channel_OwnedTags` (CA27): canalizando, o dono tem as `ActivationOwnedTags`; no fim, a contagem volta à de antes.
  - `ChannelReadApi_ElapsedRemainingNormalized_IndeterminateZero`: duração por nível (`FromArray(2, 4)`, nível 2) lida no `OnAbilityActivated` como 4; depois de `Tick(1.5)`, decorrido 1,5, restante 2,5, normalizado 0,375; no fim, `IsChanneling` falso e leituras 0. Indeterminada: decorrido conta, restante e normalizado 0. Spec nulo: 0.
- Critério de pronto: os 7 casos dos 6 testes verdes; `PassiveTests`, `ToggleTests`, `AbilityEndCancelListsTests`, `CommitModeTests` e `AbilityWaitTests` verdes sem edição; uma habilidade `Instant` se comporta como antes. CA26 e CA27 verdes.
- Tamanho: ~430 linhas e 5 arquivos (testes ~300, ASC ~80, `GameplayAbility.cs` ~30, `AbilitySpec.cs` ~15, `AbilityBehavior.cs` ~3). Exceção: nenhuma.
- Commit: `feat(gas): habilidade Channel e leitura da canalização`

### EV14 — Autocast e passiva que se reativa
- Escopo: `GameplayAbility.cs` (`AutocastConfig`, `reactivatePassive`), `Runtime/GAS/Abilities/Triggers/WhenReadyTrigger.cs` (novo), `AbilityTrigger.cs` (`onlyWithAutocast` na avaliação), `AbilitySpec.cs` (`AutocastEnabled`, última tentativa), `AbilitySystemComponent.cs` (`SetAutocast`, `IsAutocastEnabled`, `OnAutocastChanged`, passada "quando pronta"), `Tests/GAS/PlayMode/AutocastTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red: `AutocastOff_Nothing_On_ActivatesWithSelectorTarget_OffStops` (CA37, C23); `OnlyWithAutocast_IgnoredWhenOff_ManualWorksBoth` (CA38, C24); `FailureSilent_NoTargetSkips_OnePerTick_OrderPerPosition` (CA39, C25); `SetAutocast_FalseWithoutAllowedOrNoChange_EventOnce_StartsOn_ManualAndChargesCostSame` (CA40, C26); `NoAutocastNoTrigger_NoPerTickWork_NoAlloc` (CA42); `Passive_Reactivates_WhenConfigured`.
- Critério de pronto: CA37 a CA40 e CA42 verdes.
- Tamanho estimado: ~490 linhas; 7 arquivos. Depende de: EV13.

#### Detalhe (2026-11-03)

Detalhe pela emenda de 2026-11-03 da ADR 0002, lido no código da branch `feat/gas-lote-c` depois do EV13 (`5833ff7`). Caminhos relativos a `Packages/com.spellbook/`.

- CA: CA37 (C23), CA38 (C24), CA39 (C25), CA40 (C26) e CA42; reativação da passiva do Design 7 (`reactivatePassive`). O CA41 (validador) fica no EV16.
- Contexto no código: `Runtime/GAS/Abilities/Triggers/AbilityTrigger.cs:15-26` (o `onlyWithAutocast` já existe; `ListensTo` por causa); `GameplayAbility.cs:68-69` (`behavior`), `:139-141` (`triggers`), `:201` (`Triggers`); `AbilitySpec.cs:103-161`; `AbilitySystemComponent.cs:48-52` (índice `_triggers` e trava `_triggering`), `:225-253` (`Tick`, que hoje sai cedo sem `_runner`), `:371-407` (`GiveAbility`, `RemoveAbility`), `:837-849` (`TryActivate` com `silent`, do EV12), `:1381-1427` (`FireTriggers`, que hoje pula todo gatilho "só com autocast", e `IndexTriggers`/`UnindexTriggers`); `ActionRunner.cs:73-79` (`RentContext`) e `:409-413` (`ReturnContext`); `TargetSelector.Select(ActionContext, List<AbilitySystemComponent>)`.
- Comportamento:
  - **Dados.** `Runtime/GAS/Abilities/AutocastConfig.cs` (novo): `[Serializable] public sealed class AutocastConfig` com `allowed` e `startsOn` (`false` por padrão) e as propriedades `Allowed` e `StartsOn`, no molde do `ChargesConfig`. `GameplayAbility` ganha, no cabeçalho "Gatilhos", `[SerializeField] AutocastConfig autocast` (propriedade `Autocast`, nula volta ao padrão) e, no cabeçalho "Ativação", `[SerializeField] bool reactivatePassive` (propriedade `ReactivatePassive`). Campos novos com padrão: assets antigos abrem como hoje.
  - **Gatilho.** `Runtime/GAS/Abilities/Triggers/WhenReadyTrigger.cs` (novo): `WhenReadyTrigger : AbilityTrigger`, `[TypeCategory(..., Summary = ...)]`, com `[SerializeReference, TypePicker] TargetSelector selector` opcional (propriedade `Selector`). Não ouve causa nenhuma (`ListensTo` falso); só a passada "quando pronta" o lê.
  - **Estado.** `AbilitySpec.AutocastEnabled` (leitura pública, escrita interna) e `internal int WhenReadyTick` (último `Tick` em que a passada tentou o spec). Na concessão, `AutocastEnabled = Allowed && StartsOn` e o comportamento não é `Passive` nem `Toggle`; a concessão não dispara `OnAutocastChanged` (a UI lê `IsAutocastEnabled` no `OnAbilityGranted`).
  - **API do ASC.** `bool SetAutocast(AbilitySpec spec, bool on)`: falso com spec nulo ou de fora, sem `Allowed`, em `Passive`/`Toggle` ou sem mudança; senão grava e dispara `event Action<AbilitySpec, bool> OnAutocastChanged` uma vez. `bool IsAutocastEnabled(AbilitySpec spec)` (nulo: falso). Sem alocação.
  - **"Só com autocast" (R36).** `FireTriggers` passa a pular o gatilho marcado só com o autocast do spec desligado. Com ele ligado, a tentativa por evento é uma tentativa de autocast e vai em modo silencioso (R39, P16); gatilho sem a marca continua como hoje, com `OnAbilityFailed`.
  - **Passada "quando pronta" (P16).** `IndexTriggers` põe cada `WhenReadyTrigger` numa lista própria `_whenReady` (fora de `_triggers`), na ordem da concessão, e uma entrada sem gatilho para a `Passive` com `ReactivatePassive`; `UnindexTriggers` tira as duas. O `Tick` conta o índice do `Tick` e, por último, depois das canalizações e de `EndFinishedAbilities` (que deixam de depender de `_runner` para a passada rodar), percorre `_whenReady` só se ela não estiver vazia. Para cada entrada: pula spec ativo, spec já tentado neste `Tick` (`WhenReadyTick`), spec na trava `_triggering` e gatilho "só com autocast" com o autocast desligado. Com seletor, aluga um contexto do pool (ASC como dono e instigador, `Ability`, `Spec`, `Level`), roda `Select` numa lista reutilizada e devolve o contexto; sem resultado, pula, sem custo e sem evento; com resultado, o alvo é `AbilityTarget.AtUnit(primeiro)`. Sem seletor, `AbilityTarget.None`. Marca `WhenReadyTick`, entra na trava `_triggering` e chama `TryActivate(..., silent: true)`: mesmas checagens, mesma ordem (ADR 0022), mesmo commit, cargas e custo do uso manual (R38).
  - **Decisão deste detalhe.** O `WhenReadyTrigger` segue a regra uniforme do R36: com a marca "só com autocast", dispara só com o autocast ligado (o caso do C23); sem a marca, tenta a cada `Tick` mesmo sem autocast, como a lista da passada do Design 8 prevê ("sem `allowed` e sem `WhenReadyTrigger` não entra"). Registrada em `decisoes.md`.
  - **Fora do EV14:** validador (CA41, EV16), Inspector (EV17 a EV20), manual (passe de documentação).
- Escopo (7 arquivos):
  - `Runtime/GAS/Abilities/AutocastConfig.cs` (novo; o `.meta` não conta).
  - `Runtime/GAS/Abilities/Triggers/WhenReadyTrigger.cs` (novo; o `.meta` não conta).
  - `Runtime/GAS/Abilities/GameplayAbility.cs:68-69`, `:139-141`, `:198-201`: `reactivatePassive`, `autocast`, propriedades.
  - `Runtime/GAS/Abilities/AbilitySpec.cs:103-112`: `AutocastEnabled`, `WhenReadyTick`.
  - `Runtime/GAS/Core/AbilitySystemComponent.cs:48-52` (`_whenReady`, buffer, índice do `Tick`), `:225-253` (`Tick` e a passada), `:371-391` (estado inicial do autocast), `:1381-1427` (`FireTriggers`, índices), novos `SetAutocast`, `IsAutocastEnabled`, `OnAutocastChanged`.
  - `Tests/GAS/PlayMode/AutocastTests.cs` (novo; o `.meta` não conta).
  - `Documentation~/decisoes.md` (a linha da decisão; não conta).
  - Sem manual, catálogo, referência da API nem CHANGELOG (emendas de 2026-11-03 da 0007 e da 0040; sem quebra). O tipo novo do catálogo tem `Summary` (guarda `EveryCatalogTypeHasSummary`).
- Testes red (PlayMode, `AutocastTests`, `autoTick` desligado, `ManualTimeSource`, `GasFactory`):
  - `AutocastOff_Nothing_On_ActivatesWithSelectorTarget_OffStops` (CA37, C23): `WhenReadyTrigger` "só com autocast" com `AreaSelector` (3D, inimigo a 2 m); desligado, 3 `Tick` sem ativação; `SetAutocast(true)`, o `Tick` seguinte ativa com o inimigo nos alvos do evento; desligado de novo, não ativa mais.
  - `OnlyWithAutocast_IgnoredWhenOff_ManualWorksBoth` (CA38, C24): o evento ignora o gatilho marcado com o autocast desligado e o dispara ligado; um gatilho sem a marca dispara nos dois; `TryActivateAbility` ativa nos dois.
  - `FailureSilent_NoTargetSkips_OnePerTick` (CA39, C25): em recarga, nenhuma tentativa dispara `OnAbilityFailed`; seletor sem alvo pula sem custo, sem evento e sem exceção; dois `WhenReadyTrigger` no mesmo spec avaliam a regra de ativação uma vez por `Tick`.
  - `Autocast_CheckOrder_SameAsManual` (CA39, um caso por posição: tags, cargas, recarga, custo, condição; 5 casos): a falha na posição para a tentativa silenciosa antes das seguintes (a regra de contagem não é avaliada antes da posição dela) e, retirada a falha, o `Tick` seguinte ativa.
  - `SetAutocast_FalseWithoutAllowedOrNoChange_EventOnce_StartsOn` (CA40, C26): `SetAutocast` falso sem `Allowed`, em `Toggle` e sem mudança; o evento sai uma vez por mudança real; `StartsOn` liga na concessão sem evento.
  - `Autocast_ManualCommitAndCharges_ChargeLikeManual` (CA40): com `CommitMode.Manual`, cargas e custo, a ativação pelo autocast cobra a carga e o custo no `CommitAction`, igual à ativação manual de outro ASC.
  - `NoAutocastNoTrigger_NoPerTickWork_NoAlloc` (CA42): sem `Allowed` e sem gatilho, a regra de ativação não é avaliada em 100 `Tick`, e o `Tick` não aloca; com `WhenReadyTrigger` e seletor de área, a passada também não aloca depois do aquecimento (N1).
  - `Passive_Reactivates_WhenConfigured`: `Passive` com `ReactivatePassive` e `RequiredTags` ausente não ativa na concessão; com a tag, ativa no `Tick` seguinte; cancelada, volta no `Tick` seguinte; sem `ReactivatePassive`, fica inativa.
- Critério de pronto: os 12 casos dos 8 testes verdes; `GameplayEventTriggerTests`, `PassiveTests`, `ToggleTests`, `ChannelTests` e `CommitModeTests` verdes sem edição; `EveryCatalogTypeHasSummary` verde. CA37 a CA40 e CA42 verdes.
- Tamanho: ~647 linhas e 6 arquivos contados (testes ~450, ASC ~115, `AutocastConfig.cs` ~35, `WhenReadyTrigger.cs` ~30, `GameplayAbility.cs` ~20, `AbilitySpec.cs` ~6). Exceção: +29%, linha do EV14 de 2026-11-03 no `decisoes.md`.
- Commit: `feat(gas): autocast e passiva que se reativa`

### EV15 — `RaiseEventAction` única no Bridges
Emenda de 2026-11-01 (perguntas do RL6): este ticket passa a entregar o lado do efeito do payload dinâmico do B-004 (R15, R16; CA21 e CA22), que saiu do RL6. Não há `RaiseEventEffectAction` para apagar.
- Escopo: `Runtime/GAS.Bridges/RaiseEventAction.cs` (fonte de payload pelo `ActionContext`; campo `modifierIndex`), `Runtime/GAS.Bridges/GasPayloadSource.cs` (acrescenta `ModifierMagnitude`, `Stacks`, `EffectName` no fim; `EventData` com `EffectEventData` em lista de efeito; checagem "só em lista de efeito"), `Editor/GAS.Bridges/RaiseEventDrawer.cs` (campo de índice), testes do RL6 e do RL7 (lista no PR), `Tests/GAS.Bridges/PlayMode/RaiseEventEffectPayloadTests.cs` (novo), `eventos.md`, `gas.md`, `CHANGELOG.md`.
- Valores, pela tabela do Design 4 da Spec do B-004: `ModifierMagnitude` avalia o modificador `modifierIndex` de `PeriodicModifiers` com `EffectEvent.Tick` e de `Modifiers` nos outros, por `ModifierValueSource.Evaluate(in context.Effect)` (nível do `EffectContext`, ADR 0030); `Stacks` lê `EffectInstance.CurrentStacks` (instantâneo: 1); `EffectName` lê `EffectDefinition.name`; índice além da lista: não dispara e avisa uma vez.
- Testes red: `RaiseEvent_InEffectTick_UsesInstance_InAbility_UsesCaster`; `OnTick_RaisesOncePerTick` (CA21, C8); `Float_TickDamage_AtContextLevel` (CA22, efeito aplicado no nível 3 com valor diferente do nível 1), `String_EffectName`, `GameObject_Owner` (CA22); `InstantEffect_RaisesWithEffectNameAndStacks1`; `ModifierMagnitude_OnlyInEffectContext_ValidatorWhenNotSupported`; os testes do RL6 e do RL7 adaptados passam.
- Critério de pronto: um só tipo de ação de evento; CA21 e CA22 do B-004 verdes; CA30 do B-004 mantido para o comportamento.
- Tamanho estimado: ~480 linhas; 9 arquivos. Depende de: EV14, RL7.

#### Detalhe (2026-11-03)

Detalhe pela emenda de 2026-11-03 da ADR 0002, lido no código da branch `feat/gas-lote-c` depois do EV14 (`979be63`). Caminhos relativos a `Packages/com.spellbook/`.

- CA: CA21 e CA22 do B-004 (R15, R16; cenário C8); CA23 e CA30 do B-004 mantidos. Tabela do Design 4 da Spec `gas-resultado-de-aplicacao-e-relay.md` e validação do Design 5 de lá (`modifierIndex` negativo).
- Contexto no código: `Runtime/GAS.Bridges/RaiseEventAction.cs:25-120` (campos, `Execute`, `Validate`, `RaiseDynamic`, tudo pelo lado da habilidade); `GasPayloadSource.cs:9-23` (seis valores) e `:32-50` (`Supports`); `Editor/GAS.Bridges/RaiseEventDrawer.cs:20-22`, `:44`, `:66-75`, `:90-102`; `Runtime/GAS/Actions/ActionContext.cs:58-66` (`EffectInstance`, `EffectDefinition`, `EffectEvent`, `HasEffect`, `Effect`); `Runtime/GAS/Effects/ActiveEffectInstance.cs:185-198` e `AbilitySystemComponent.cs:1825-1841` (quem preenche o contexto do efeito, também no instantâneo); `ModifierValueSource.GetValue(in EffectContext)` (público; o `Evaluate` é `internal` e o Bridges não o vê); `GameplayAbility.cs:343-350` valida as ações com o `ValidationResult` da habilidade; o `GameplayEffect.Validate` não valida as listas de ações (isso é do EV16).
- Comportamento:
  - **Enum.** `ModifierMagnitude`, `Stacks` e `EffectName` entram no fim do `GasPayloadSource` (ordem da serialização; o `Fixed` continua 0).
  - **Tabela.** `Supports(fonte, tipo, bool inEffectList)` (novo) aplica o Design 4: `ModifierMagnitude` e `Stacks` com Float e `EffectName` com String só em lista de efeito; `AbilityName` só fora dela; `EventData` com `EffectEventData` em lista de efeito e com `AbilityEventData` fora dela. `Supports(fonte, tipo)` continua com o resultado de hoje e passa a ser o caso de fora da lista de efeito.
  - **Runtime.** A lista é de efeito quando `context.HasEffect`. Fonte que não serve não dispara, sem exceção. No efeito: `AttributeValue` lê o dono do efeito (`context.Owner`); `SourceObject`, quem aplicou (`Instigator`); `TargetObject`, o dono do efeito; `ModifierMagnitude` avalia `PeriodicModifiers[modifierIndex]` com `EffectEvent.Tick` e `Modifiers[modifierIndex]` nos outros eventos, por `ValueSource.GetValue(in context.Effect)` (nível e tabela do `EffectContext`, ADR 0030); `Stacks`, `EffectInstance.CurrentStacks` (instantâneo: 1); `EffectName`, `EffectDefinition.name`; `EventData`, um `EffectEventData` com `Owner`, `Source`, `Effect`, `Stacks` e `PreviousStacks` iguais às pilhas atuais. Índice além da lista (ou modificador sem fonte): não dispara e avisa uma vez por ação. Fora de efeito, nada muda.
  - **Validação.** Fonte que não serve para o canal no lugar da ação: a lista conta como de efeito quando o `result.Context` é um `GameplayEffect`, que é o asset validado (habilidade, projétil ou contexto nulo contam como fora). `modifierIndex` negativo com `ModifierMagnitude`: erro. O índice além da lista fica para o runtime, como o Design 5 do B-004 diz.
  - **Drawer.** O popup filtra pelo canal e pelo lugar (`serializedObject.targetObject is GameplayEffect`); o campo `modifier-index` aparece só com `ModifierMagnitude`.
- Escopo (6 arquivos contados):
  - `Runtime/GAS.Bridges/GasPayloadSource.cs:9-23`, `:32-50`: três valores e `Supports` com `inEffectList`.
  - `Runtime/GAS.Bridges/RaiseEventAction.cs:25-120`: campo `modifierIndex`, o lado do efeito em `Execute`/`RaiseDynamic`, `Validate`.
  - `Editor/GAS.Bridges/RaiseEventDrawer.cs:20-22`, `:44`, `:66-75`, `:90-102`: campo de índice e filtro pelo lugar.
  - `Tests/GAS.Bridges/PlayMode/RaiseEventEffectPayloadTests.cs` (novo; o `.meta` não conta).
  - `Tests/GAS.Bridges/EditMode/GasPayloadSourceTests.cs`: `Enum_Order` adaptado aos valores novos, tabela de efeito e validação.
  - `Tests/GAS.Bridges/EditMode/RaiseEventDrawerTests.cs`: teste do drawer em lista de efeito.
  - Sem manual, catálogo, referência da API nem CHANGELOG (emendas de 2026-11-03 da 0007 e da 0040; o enum cresce no fim e nada quebra).
- Testes red:
  - PlayMode, `RaiseEventEffectPayloadTests`: `RaiseEvent_InEffectTick_UsesInstance_InAbility_UsesCaster` (`AttributeValue` e `Stacks` no tique leem o dono e a instância com 2 pilhas; na habilidade, o conjurador); `OnTick_RaisesOncePerTick` (CA21, C8); `Float_TickDamage_AtContextLevel` (CA22; nível 3 diferente do nível 1); `String_EffectName` (CA22); `GameObject_Owner` (CA22; `TargetObject` dá o dono, `SourceObject` quem aplicou); `InstantEffect_RaisesWithEffectNameAndStacks1`; `EventData_InEffect_FillsEffectEventData`; `ModifierIndexOutOfRange_RaisesNothing_WarnsOnce`; `EffectSources_OutsideEffect_RaiseNothing`.
  - EditMode, `GasPayloadSourceTests`: `ModifierMagnitude_OnlyInEffectContext_ValidatorWhenNotSupported`; `Supports_EffectList_Table` (casos); `NegativeModifierIndex_ValidatorError`; `Enum_Order` (adaptado).
  - EditMode, `RaiseEventDrawerTests`: `Drawer_EffectList_ListsEffectSources_ShowsIndexOnlyForModifierMagnitude`.
- Critério de pronto: os testes acima verdes; `RaiseEventPayloadTests`, `RaiseEventAbilityTests`, `RaiseEventActionTests` e o resto de `GasPayloadSourceTests` e `RaiseEventDrawerTests` verdes sem edição (CA23, CA30); um só tipo de ação de evento.
- Tamanho: ~619 linhas e 6 arquivos contados (testes ~413, `RaiseEventAction.cs` ~128, `GasPayloadSource.cs` ~49, `RaiseEventDrawer.cs` ~29). Exceção: +24%, linha do EV15 de 2026-11-03 no `decisoes.md`.
- Commit: `feat(gas-bridges): payload do efeito na RaiseEventAction`

### EV16 — Validação do modelo
- Escopo: `GameplayAbility.cs`, `GameplayEffect.cs` (`Validate`), `FlowActions.cs`, `ChanceAction.cs`, `DelayedAction.cs`, `ForEachTargetAction.cs`, `RemoveEffectAction.cs`, `AbilityTrigger.cs` (`Validate`), `Tests/GAS/EditMode/ActionModelValidationTests.cs` (novo), `manual/ferramentas/validador.md`.
- Testes red: um por regra do Design 9 (CA32, CA41), com `propertyPath`; `Chance_OutOfRange_Error` (CA14 completo). Da emenda de 2026-10-31 (ADR 0035): `TagQueryTargetUnit_OutsideCandidateFilter_Warns` (em regra de ativação e em `ConditionalAction`).
- Critério de pronto: CA14, CA32 e CA41 verdes. A linha do CHANGELOG entra no EV21 (limite de arquivos).
- Tamanho estimado: ~460 linhas; 10 arquivos. Depende de: EV15, AV8.

### Detalhe (2026-11-03)
- CA: CA14 (parte do validador), CA32 e CA41; regras do Design 9, mais as que o EV11a e o EV11b deixaram para cá (`OnDeath` sem `deathAttribute`, reação em efeito `Instant`) e a da emenda de 2026-10-31 (ADR 0035).
- Escopo:
  - `Runtime/GAS/Actions/GameplayAction.cs:46-52`: gancho `internal virtual void CollectChildLists(List<ChildActionList>)` e o struct `internal readonly ChildActionList` (campo, lista, "evento próprio"), para a validação descer nas filhas. Consequência direta: sem ele a árvore só seria percorrida por troca de tipo.
  - `Runtime/GAS/Abilities/Actions/FlowActions.cs:17-135`: `CollectChildLists` em `RepeatAction`, `ParallelAction` e `ConditionalAction`; `ConditionalAction.Validate` avisa a `TagQueryCondition` `TargetUnit`; `ActionLists.Validate` percorre uma lista com o `ActionEventAttribute` do campo: ação nula (erro), profundidade acima de 4 (aviso, uma vez no 5.º nível), seletor com `NeedsEventTargets` em evento sem alvos (erro), a validação de cada ação com o caminho dela prefixado (`OnActivate.Array.data[0].actions.Array.data[1].delay`) e uma visita para as regras do dono.
  - `Runtime/GAS/Actions/ChanceAction.cs:16-55` (constante fora de 0 a 1, erro), `DelayedAction.cs:19-68` (constante negativa, erro), `ForEachTargetAction.cs:15-51` (sem filhas, aviso), `RemoveEffectAction.cs:15-50` (sem efeito e sem tag, erro): `Validate` e, nas três primeiras, `CollectChildLists`.
  - `Runtime/GAS/Abilities/Actions/SpawnProjectileAction.cs:26`: `CollectChildLists` do `OnHitActions` como evento próprio (o impacto tem alvos). Consequência direta: sem ele, as ações do impacto ficam fora da validação.
  - `Runtime/GAS/Abilities/Triggers/AbilityTrigger.cs:12-53`: `public virtual void Validate(ValidationResult, string propertyPath)` (aditivo), e o `GameplayEventTrigger` dá erro com tag inválida.
  - `Runtime/GAS/Abilities/GameplayAbility.cs:297-350` e `:391-468`: as seis listas por `ActionLists.Validate`; `OnChannelTick` fora de `Channel`; `Passive` com custo ou recarga; `Channel` sem duração e sem `CancelledByTags`; `Manual` sem `CommitAction`; `CommitAction` com `OnStart`; gatilhos (`Validate`, evento que a própria habilidade envia ao dono, `WhenReadyTrigger` com seletor e sem alcance); autocast em `Passive`/`Toggle` e sem gatilho que o use; `TagQueryCondition` `TargetUnit` nas regras de ativação. O `UsesCastRange` vira a busca genérica `ContainsCondition`, e a avaliação do alcance por nível é reaproveitada para a duração.
  - `Runtime/GAS/Effects/GameplayEffect.cs:204-228`: as seis listas por `ActionLists.Validate`; reação (`OnTakeDamage`, `OnDeath`, `OnAbilityExecuted`) em efeito `Instant` (aviso); `OnDeath` com ações e sem `deathAttribute` (erro).
  - `Tests/GAS/EditMode/ActionModelValidationTests.cs` (novo).
  - Fora (emenda de 2026-11-03): `manual/ferramentas/validador.md` vai para o passe de documentação; sem `!`, sem CHANGELOG.
- Testes red (EditMode, `ActionModelValidationTests`, cada um com uma mensagem só e o `propertyPath` achado pelo `SerializedObject`): `NullAction_Nested_Error`, `ChannelTick_OutsideChannel_Warns`, `EventTargetsSelector_InEventWithoutTargets_Error`, `ForEachTarget_Empty_Warns`, `Delayed_NegativeConstant_Error`, `Chance_OutOfRange_Error` (−0,1 e 1,5), `GameplayEventTrigger_InvalidTag_Error`, `Trigger_TagSentToOwnerBySelf_Warns`, `Passive_WithCostOrCooldown_Warns`, `Channel_WithoutDurationNorCancel_Warns`, `Manual_WithoutCommit_Error`, `Commit_WithOnStart_Warns`, `RemoveEffect_Empty_Error`, `Autocast_InPassiveOrToggle_Error`, `Autocast_WithoutTrigger_Warns`, `WhenReady_SelectorWithoutCastRange_Warns`, `OnDeath_WithoutDeathAttribute_Error`, `Reaction_InInstantEffect_Warns`, `Nesting_DeeperThanFour_Warns`, `TagQueryTargetUnit_OutsideCandidateFilter_Warns` (regra de ativação e `ConditionalAction`), `ConfiguredModel_HasNoMessages` (guarda contra falso positivo).
- Critério de pronto: `ActionModelValidationTests` verde; guardas verdes: `ActivationValidationTests`, `LevelValuesValidationTests`, `DamageValidationTests`, `EffectMetadataTests`, `ProjectValidationTests`, `RaiseEventActionTests` e `GasPayloadSourceTests`.
- Tamanho: estimado em ~610 linhas e 11 arquivos; medido na implementação: 710 linhas (421 de runtime, 289 de testes) e 12 arquivos, com o `Tests/GAS.Bridges/EditMode/RaiseEventActionTests.cs:77` (passava uma ação nula, agora erro). Fica acima de +30%: o ticket volta ao Controlador para ser dividido. Divisão proposta: **EV16a** (percurso das listas: ação nula, profundidade, seletor sem alvos do evento, caminho completo; regras das ações Chance, Delayed, ForEachTarget e RemoveEffect; filhas do projétil; listas e reações do efeito; ~400 linhas e 11 arquivos) e **EV16b** (regras da habilidade: comportamento, commit, gatilhos, autocast e `TagQueryCondition` `TargetUnit`; `GameplayAbility.cs`, `AbilityTrigger.cs`, `ConditionalAction.Validate` e os testes delas; ~330 linhas e 4 arquivos). Exceção: nenhuma.
- Commit: `feat(gas): validação do modelo evento → ações → alvos`

### Detalhe EV16a (2026-11-03)
- CA: CA14 (parte do validador) e CA32 (percurso das listas, regras das ações e do efeito); o CA41 e as regras da habilidade ficam no EV16b (#316).
- Escopo:
  - `Runtime/GAS/Actions/GameplayAction.cs:54-71`: `internal virtual CollectChildLists` e o struct `internal readonly ChildActionList`.
  - `Runtime/GAS/Abilities/Actions/FlowActions.cs`: `CollectChildLists` em `RepeatAction`, `ParallelAction` e `ConditionalAction`; `ActionLists.Validate` e `Walk` (ação nula, erro; nível 5, aviso; seletor com `NeedsEventTargets` em evento sem alvos, erro; caminho completo da ação).
  - `ChanceAction.cs`, `DelayedAction.cs`, `ForEachTargetAction.cs` (`Validate` e `CollectChildLists`), `RemoveEffectAction.cs` (`Validate`).
  - `SpawnProjectileAction.cs:80-85`: `OnHitActions` como evento próprio, com alvos.
  - `GameplayAbility.cs:322-327`: as seis listas por `ActionLists.Validate` (sai o `ValidateActions`).
  - `GameplayEffect.cs:231-262`: as seis listas, reação em `Instant` (aviso) e `OnDeath` sem `deathAttribute` (erro).
  - `Tests/GAS/EditMode/ActionModelValidationTests.cs` (novo) e `Tests/GAS.Bridges/EditMode/RaiseEventActionTests.cs:77` (ação nula agora é erro).
- Testes red (EditMode, `ActionModelValidationTests`): `NullAction_Nested_Error`, `EventTargetsSelector_InEventWithoutTargets_Error`, `ForEachTarget_Empty_Warns`, `Delayed_NegativeConstant_Error`, `Chance_OutOfRange_Error` (−0,1 e 1,5), `RemoveEffect_Empty_Error`, `OnDeath_WithoutDeathAttribute_Error`, `Reaction_InInstantEffect_Warns`, `Nesting_DeeperThanFour_Warns`, `ProjectileOnHit_ValidatedAsOwnEventWithTargets`, `ConfiguredModel_HasNoMessages` (guarda).
- Critério de pronto: os 12 casos verdes; guardas verdes: `ActivationValidationTests`, `LevelValuesValidationTests`, `DamageValidationTests`, `EffectMetadataTests`, `ProjectValidationTests`, `RaiseEventActionTests`, `GasPayloadSourceTests`.
- Tamanho: ~404 linhas (228 de runtime e do ajuste no teste do Bridges, 176 do teste novo) e 11 arquivos. Exceção: +10% em arquivos, linha do EV16a de 2026-11-03 no `decisoes.md`.
- Commit: `feat(gas): validação das listas de ações e do efeito`

### Detalhe EV16b (2026-11-03)
- CA: CA41 e a parte do CA32 que é regra da habilidade (comportamento, commit, gatilhos, autocast); a regra da emenda de 2026-10-31 (ADR 0035, `TagQueryCondition` `TargetUnit`).
- Escopo:
  - `Runtime/GAS/Abilities/Triggers/AbilityTrigger.cs:24-27` e `:50-53`: `public virtual void Validate(ValidationResult, string propertyPath)` (aditivo) e o erro do `GameplayEventTrigger` sem tag.
  - `Runtime/GAS/Abilities/Actions/FlowActions.cs:129-145`: `ConditionalAction.Validate` (aviso `TargetUnit`) e o `delegate ActionVisitor`; `:158-205`: `ActionLists.Validate` com o visitante opcional e o `Walk` propagando se a ação mira o dono.
  - `Runtime/GAS/Abilities/GameplayAbility.cs:296-327`: o visitante (ações `Commit` e eventos enviados ao dono) e as chamadas `ValidateBehavior`, `ValidateCommit` e `ValidateTriggers`; `:396-441`: `UsesCastRange` vira `ContainsCondition` genérica, o aviso `TargetUnit` nas regras de ativação, `PositiveAtSomeLevel` e `TableFor`.
  - `Tests/GAS/EditMode/ActionModelValidationTests.cs`: os testes abaixo e a versão completa do `ConfiguredModel_HasNoMessages`.
- Testes red (EditMode, `ActionModelValidationTests`): `ChannelTick_OutsideChannel_Warns`, `GameplayEventTrigger_InvalidTag_Error`, `Trigger_TagSentToOwnerBySelf_Warns`, `Passive_WithCostOrCooldown_Warns`, `Channel_WithoutDurationNorCancel_Warns`, `Manual_WithoutCommit_Error`, `Commit_WithOnStart_Warns`, `Autocast_InPassiveOrToggle_Error`, `Autocast_WithoutTrigger_Warns`, `WhenReady_SelectorWithoutCastRange_Warns`, `TagQueryTargetUnit_OutsideCandidateFilter_Warns`, `ConfiguredModel_HasNoMessages` (guarda, versão completa).
- Critério de pronto: `ActionModelValidationTests` inteiro verde; guardas verdes: `ActivationValidationTests`, `LevelValuesValidationTests`, `DamageValidationTests`, `EffectMetadataTests`, `ProjectValidationTests`, `RaiseEventActionTests`, `GasPayloadSourceTests`.
- Tamanho: ~362 linhas (~215 de runtime, ~147 de testes) e 4 arquivos. Exceção: nenhuma.
- Commit: `feat(gas): validação das regras da habilidade`

### EV17 — Inspector: abas, lista e linha recolhida
- Escopo: `Editor/GAS/Actions/ActionEventsView.cs`, `ActionListView.cs`, `ActionRowView.cs`, `ActionInspector.uss` (novos), `Editor/GAS/GameplayAbilityEditor.cs`, `Editor/GAS/GameplayEffectEditor.cs` (novo; sai de `GasEditors.cs`), `GasEditors.cs`, `Tests/GAS/EditMode/ActionInspectorTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red: `Tabs_OnePerActionEvent_CounterAndValidationDot`; `ChannelTab_OnlyWithChannel_HiddenWithWarning`; `Row_SummaryFromLine_NoLineShowsTypeName_NullShowsChoose`; `Row_ValidationIcon_OnlyWithMessage`; `TabRemembered_PerAssetInSession`; `MultiSelection_HidesTabs`.
- Critério de pronto: testes verdes; parte do CA31 (S) no host (abas, contador, linha legível, 50 ações sem engasgo).
- Tamanho estimado: ~500 linhas; 10 arquivos. Depende de: EV16, AV9.

#### EV17a — Inspector: lista e linha recolhida (divisão de 2026-11-03 do EV17)
O Detalhe do EV17 (#191) mediu 694 linhas (499 de editor, 195 de testes) e 9 arquivos na implementação, acima de +30% do teto da ADR 0042; o Controlador aprovou a divisão em EV17a e EV17b.

##### Detalhe (2026-11-03)
- CA: CA31 (S), parte da lista e da linha recolhida (DS 4.1 sem menu e teclado, 4.2 sem chip, 4.3 sem o aviso de troca de tipo). As abas, os editores, "Várias seleções" e "Dono sem nenhuma ação" ficam no EV17b; o chip no EV18, as filhas no EV19, o menu e o teclado no EV20.
- Escopo:
  - `Editor/GAS/Actions/ActionRowView.cs` (novo, `internal sealed`): seta de dobra, pastilha e barra de categoria pela raiz do `[TypeCategory]` (`:28-32`), resumo pelo `SummaryFormatter` com tooltip do texto inteiro, "(Escolha uma ação)" com o botão do seletor na ação nula (`:129-137`), ícone de validação só com mensagem cujo caminho fica sob o elemento (`IsUnder`, `:76-81`; `data[1]` não casa com `data[10]`), corpo expandido por `PropertyField` (`:117-127`; o `TypePickerDrawer` dá o botão de trocar tipo).
  - `Editor/GAS/Actions/ActionListView.cs` (novo): `ListView` ligado à lista (reordenável, virtualizado, altura dinâmica), "Expandir tudo"/"Recolher tudo", texto da lista vazia, "+ Adicionar ação" pelo `TypePickerPopup` (nova ação expandida) e o contador "N ações"; `Refresh(messages)` repassa as mensagens às linhas. Não depende do `ActionEventsView`: quem o monta e o atualiza é a aba do EV17b.
  - `Editor/GAS/Actions/ActionInspector.uss` (novo): tokens de categoria, aviso e erro nos temas claro e escuro (`.unity-theme-dark`/`.unity-theme-light` não existem no Inspector; o tema vem de `EditorGUIUtility.isProSkin`, que põe a classe `action-inspector--light` na raiz). Entra inteiro, com as classes das abas; quem carrega a folha é o `ActionEventsView` (EV17b).
  - `Editor/GAS/AssemblyInfo.cs` (novo): `InternalsVisibleTo("SpellBook.GAS.Tests.EditMode")`. Consequência direta: os testes da linha montam o `ActionRowView` sem painel.
  - `Tests/GAS/EditMode/ActionInspectorTests.cs` (novo): só os testes `Row_*` e o auxiliar `BindRows`.
  - Fora: nenhum Inspector muda; a lista e a linha só aparecem no host com o EV17b. `gas.md` vai para o passe de documentação; sem `!`, sem CHANGELOG. O `Line` das ações do pacote ("Esperar 2 s") não entra: sem ele, a linha mostra o nome do tipo, como o DS manda.
- Testes red (EditMode, `ActionInspectorTests`): `Row_SummaryFromLine_NoLineShowsTypeName_NullShowsChoose`; `Row_ValidationIcon_OnlyWithMessage`.
- Critério de pronto: os 2 testes verdes; guardas verdes: `ActivationInspectorTests` e `SpellBookEditorCoverageTests`. Sem conferência no host neste ticket.
- Tamanho: medido em 310 linhas (234 de editor, 76 de testes) e 5 arquivos (o `.uss` conta só no número de arquivos). Exceção: nenhuma.
- Commit: `feat(editor): lista e linha recolhida das ações`

#### EV17b — Inspector: abas de eventos e editores (divisão de 2026-11-03 do EV17)
Depende de: EV17a.

##### Detalhe (2026-11-03)
- CA: CA31 (S), parte das abas (DS 3, 9 "Várias seleções" e "Dono sem nenhuma ação"), com a lista e a linha do EV17a dentro de cada aba.
- Escopo:
  - `Editor/GAS/Actions/ActionEventsView.cs` (novo, `internal sealed`): descobre os campos `[ActionEvent]` do alvo e monta o `TabView` com um `ActionListView` por aba; rótulo com contador (`Ao ativar · 3`), ponto de validação pelas mensagens cujo `PropertyPath` começa no campo, subtítulo do alvo padrão, rótulo de grupo "Reações" no cabeçalho da primeira aba de reação, aba oculta pelo predicado do dono com aviso quando tem ações, aba lembrada por asset no `SessionState`, texto de dono sem ações, "Edição múltipla não mostra ações" em multi-seleção. Carrega o `ActionInspector.uss` e põe `action-inspector--light` pelo `isProSkin`. Esconde as `PropertyField` padrão das listas.
  - `Editor/GAS/GameplayAbilityEditor.cs:35-52`: a vista no fim do `BuildAfterDefault`, com a aba "A cada passo" só em `Channel`.
  - `Editor/GAS/GameplayEffectEditor.cs` (novo; sai de `GasEditors.cs:24-26`): a vista, com as três reações agrupadas.
  - `Editor/GAS/GasEditors.cs:6` e `:24-26`: sai o `GameplayEffectEditor` vazio e o `using` dele.
  - `Tests/GAS/EditMode/ActionInspectorTests.cs`: os testes abaixo, o `TearDown` que apaga a chave da sessão e destrói os editores, e os auxiliares `CreateInspector` e `CreateView`.
  - O código já escrito está guardado fora do repositório (`ev17b-guardado`, com os `.meta` da Unity).
  - Fora: `gas.md` vai para o passe de documentação; sem `!`, sem CHANGELOG. O `ProjectileEditor` não muda (o `OnImpact` é `HideInInspector`; o impacto aparece nas filhas do `SpawnProjectile`, EV19).
- Testes red (EditMode, `ActionInspectorTests`): `Tabs_OnePerActionEvent_CounterAndValidationDot`; `EffectTabs_ReactionsGrouped`; `ChannelTab_OnlyWithChannel_HiddenWithWarning`; `TabRemembered_PerAssetInSession`; `MultiSelection_HidesTabs`.
- Critério de pronto: os 5 testes verdes e os `Row_*` do EV17a seguem verdes; guardas verdes: `ActivationInspectorTests` e `SpellBookEditorCoverageTests`. No host (Examinador): abas, contador, linha legível e 50 ações sem engasgo.
- Tamanho: medido em 385 linhas (265 de editor, 120 de testes) e 5 arquivos. Exceção: nenhuma.
- Commit: `feat(editor): abas de eventos das ações nos editores`

### EV18 — Inspector: chip de alvo e bloco "Alvo"
- Escopo: `Editor/GAS/Actions/TargetChipView.cs` (novo), `ActionRowView.cs`, `ActionInspector.uss`, `Tests/GAS/EditMode/TargetChipTests.cs` (novo), `CHANGELOG.md`.
- Testes red: `Chip_TextPerSelector_AreaSummary`; `Chip_IncompatibleWithEvent_RedNotBlocked`; `AreaSelector_OpensTargetBlockBeforeActionFields`; `ForEachChild_ChipTooltipSaysCurrentTarget`.
- Critério de pronto: testes verdes; seletor inline conferido no host (CA31, parte).
- Tamanho estimado: ~380 linhas; 5 arquivos. Depende de: EV17b (era EV17, divisão de 2026-11-03).

#### Detalhe (2026-11-03)
- CA: CA31 (S), parte do seletor de alvo inline (DS 4.2 item 4, 5.1, 5.2 e 5.3). As filhas com a mesma linha ficam no EV19; o teclado do chip (`Enter`/`Esc`), no EV20.
- Escopo:
  - `Editor/GAS/Actions/TargetChipView.cs` (novo, `internal sealed`): chip `→ <texto>` pelo `target` da ação; oculto em ação nula ou com `UsesTargets` falso; sem seletor, `→ Padrão do evento` com tooltip do alvo padrão do evento (achado pelo `[ActionEvent]` do campo raiz do caminho) ou "o alvo corrente" sob um `ForEachTargetAction`; `AreaSelector` como `Área 5 m · 2 alvos · <resumo do filtro>`; outros seletores pelo `SummaryFormatter`. Mensagens sob `<ação>.target` pintam o chip de vermelho com ✖ e a mensagem no tooltip, sem desabilitar. O clique abre o `TypePickerPopup` de `TargetSelector` com "(Padrão do evento)"; seletor com campos expande a ação. Também monta o bloco "Alvo" (`BuildTargetBlock`): um `PropertyField` por campo visível do seletor.
  - `Editor/GAS/Actions/ActionRowView.cs:45-66` (chip no cabeçalho, entre o resumo e a validação), `:83-107` (`Bind` repassa ao chip), `:117-127` (bloco "Alvo" antes do `PropertyField` da ação).
  - `Editor/GAS/Actions/ActionInspector.uss`: classes do chip (padrão tracejado, cheio, erro) e do bloco, com a barra na cor da categoria "Alvo".
  - `Editor/Core/Inspector/TypePickerTree.cs:73-81` e `TypePickerPopup.cs:36-44`: sobrecarga aditiva com o rótulo do item nulo (`noneLabel`), padrão "(Nenhum)". Consequência direta: o DS 5.2 pede "(Padrão do evento)" no popup.
  - `Runtime/GAS/Actions/GameplayAction.cs:19`: `[HideInInspector]` no `target`, para o seletor não aparecer duas vezes (chip e bloco, e de novo nos campos da ação). Até o EV19, as filhas aninhadas, desenhadas pelo `TypePickerDrawer`, ficam sem o campo de alvo no Inspector; linha no `decisoes.md`.
  - `Runtime/GAS/Targeting/AreaSelector.cs:60-67`: a declaração do `filter` sobe para depois do `radius`, na ordem dos campos do DS 5.2 (o formato serializado é por nome e não muda).
  - `Tests/GAS/EditMode/TargetChipTests.cs` (novo).
  - Fora: sem `!`, sem CHANGELOG (nenhuma quebra); `gas.md` vai para o passe de documentação.
- Testes red (EditMode, `TargetChipTests`): `Chip_TextPerSelector_AreaSummary` (padrão, Quem lançou, Alvos do evento, Ponto, Área com raio, máximo e filtro; oculto em `Wait`); `Chip_IncompatibleWithEvent_RedNotBlocked` (`EventTargetsSelector` em "Ao ser concedida", pelo validador real); `AreaSelector_OpensTargetBlockBeforeActionFields`; `ForEachChild_ChipTooltipSaysCurrentTarget`; `Picker_NoneLabel_ReadsEventDefault`.
- Critério de pronto: os 5 testes verdes; guardas verdes: `ActionInspectorTests`, `TypePickerTreeTests`, `ActionModelValidationTests` e `SpellBookEditorCoverageTests`. No host (Examinador): chip clicável, troca na hora e bloco "Alvo" com os campos da Área.
- Tamanho: medido em 326 linhas (186 de editor e runtime, 140 de testes) e 8 arquivos (o `.uss` conta só no número de arquivos). Exceção: nenhuma.
- Commit: `feat(editor): chip de alvo e bloco Alvo nas ações`

### EV19 — Inspector: ações aninhadas e arrastar entre listas
- Escopo: `ActionListView.cs`, `ActionRowView.cs`, `ActionInspector.uss`, `Tests/GAS/EditMode/NestedActionListTests.cs` (novo), `CHANGELOG.md`.
- Testes red: `FlowChildren_IndentedUpTo3_Level4Indicator`; `CollapsedFlow_ShowsChildCount`; `DragBetweenListsSameTab_Moves_UndoRestores`.
- Critério de pronto: testes verdes; DS 4.4 conferido no host.
- Tamanho estimado: ~380 linhas; 5 arquivos. Depende de: EV18.

### EV20 — Inspector: menu, teclado, copiar e colar
- Escopo: `ActionRowView.cs`, `Editor/GAS/Actions/ActionClipboard.cs` (novo), `ActionListView.cs`, `Tests/GAS/EditMode/ActionClipboardTests.cs` (novo), `CHANGELOG.md`.
- Testes red: `Duplicate_CreatesNewInstanceBelow`; `CopyPaste_WithChildren_NewInstances_OtherOwnerValidatorFlags`; `ChangeType_NonDefault_WarnsAndUndo`; `Keyboard_DeleteCtrlDAltArrowsEnter`.
- Critério de pronto: testes verdes; CA31 (S) completo no host, com a verificação da seção 11 do DS.
- Tamanho estimado: ~420 linhas; 5 arquivos. Depende de: EV19.

### EV22 — Inspector: indicador de "rodando" em Play (novo, 2026-10-31)
- Escopo: `AbilitySystemComponent.cs` (`CollectRunningActions`), `Runtime/GAS/Actions/ActionRunner.cs` (percorre a pilha de quadros de cada execução), `Editor/GAS/Actions/RunningActionsTracker.cs` (novo), `ActionRowView.cs` (ponto e classe `action-row--running`), `ActionInspector.uss` (token nos dois temas), `Tests/GAS/PlayMode/RunningActionsQueryTests.cs` (novo), `Tests/GAS/EditMode/RunningIndicatorTests.cs` (novo; ASCs pela costura interna), `gas.md` (nota do Modo Play), `CHANGELOG.md`.
- Testes red:
  - PlayMode: `CollectRunningActions_WaitInsideRepeat_ListsLeafAndParent`; `CollectRunningActions_AfterExecutionEnds_Empty`; `CollectRunningActions_CancelledOwner_Empty`; `CollectRunningActions_AfterWarmup_DoesNotAllocate`.
  - EditMode: `Row_RunningClass_WhenActionInRunningSet_ByReference` (outra instância do mesmo tipo não acende); `CollapsedFlowRow_LitWhenChildRuns`; `Tracker_OutsidePlay_NoSchedule_SetEmpty`; `Tracker_ExitPlay_ClearsAllRows`.
- Critério de pronto: testes verdes; o Examinador confere no host, em Play, com a Bola de Fogo: a linha da ação em espera acende e apaga, o layout não pula e os campos seguem editáveis (DS 9, "Modo Play").
- Tamanho estimado: ~300 linhas; 9 arquivos. Depende de: EV20.

### EV21 — Sample refeito, migração e fechamento da documentação
- Escopo: `Samples~/Sandbox/Data/Abilities/GA_BolaDeFogo.asset`, `Samples~/Sandbox/Data/Effects/GE_Queimadura.asset` (refeitos no Inspector, com a pasta renomeada pela ADR 0028), `Documentation~/manual/regras/migracao-2-para-3.md` (tabela ação antiga → nova, campos renomeados, regra da espera), `gas.md` (modelo evento → ações → alvos, seletores, gatilhos, comportamentos, commit, autocast; troca a nota do RP-06 pela regra nova), `CHANGELOG.md` (R34 completo, validação do EV16).
- Testes red: as guardas do SB-T1 (`.meta`, GUID único) seguem verdes; nenhum código novo.
- Critério de pronto: o Sample abre sem "Missing" e a bola de fogo funciona como antes (o Examinador confere no host); CA35 (E).
- Tamanho estimado: ~0 linhas de código; 5 arquivos (2 fixtures). Depende de: EV22 (era EV20 antes da emenda de 2026-10-31), SB-T6.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| EV1a | sim | DM8 (fila), AV9 | ~480 / 8 (emenda de 2026-11-02) |
| EV1b | sim | EV1a | ~360 / 8 (emenda de 2026-11-02) |
| EV2 | sim | EV1b | ~490 / 12 (exceção única à 0003, emenda de 2026-11-02) |
| EV3 | sim | EV2, VN9, RP-07, AV5 | ~1240 / 39 (exceção única à 0003, ampliada em 2026-11-02; `ActionContext.cs` acrescentado; ponto de extensão `protected`) |
| EV3c | sim | EV3, XR3 (fila) | ~410 / 10 (novo, 2026-11-02; emenda da posse do contexto pendente) |
| EV3d | sim | EV3c | ~260 / 7 (novo, 2026-11-02) |
| EV4a | sim | EV3d (era EV3) | ~420 / 10 |
| EV4b | sim | EV4a | 579 / 10 (exceção, ADR 0003) |
| EV5 | sim | EV4b | ~420 / 8 |
| EV6 | sim | EV5 | ~360 / 6 |
| EV7 | sim | EV6, AV2 | ~320 / 7 |
| EV8 | sim | EV7, AV4 | ~450 / 9 |
| EV9 | sim | EV8, AV7 | ~420 / 7 |
| EV10 | sim | EV9 | 583 / 9 (exceção) |
| EV11a | sim | EV10, DM5, PD1 (fila) | 606 / 7 (exceção única de 2026-11-03 na 0003; estimativa ~490 / 7, da divisão de 2026-11-03; era EV11 ~430 / 6) |
| EV11b | sim | EV11a | ~220 / 5 (detalhe de 2026-11-03; rascunho era ~150 / 5) |
| EV12 | sim | EV11b (era EV11) | ~470 / 6 (detalhe de 2026-11-03; rascunho era ~480 / 8) |
| EV13 | sim | EV12 | ~420 / 6 |
| EV14 | sim | EV13 | ~490 / 7 |
| EV15 | sim | EV14, RL7 | ~480 / 9 (emenda de 2026-11-01) |
| EV16 | sim | EV15, AV8 | ~460 / 10 |
| EV17a | sim | EV16, AV9 | 310 / 5 (divisão de 2026-11-03 do EV17, que mediu 694 / 9; era ~500 / 10) |
| EV17b | sim | EV17a | ~385 / 5 (divisão de 2026-11-03 do EV17) |
| EV18 | sim | EV17b (era EV17) | ~380 / 5 |
| EV19 | sim | EV18 | ~380 / 5 |
| EV20 | sim | EV19 | ~420 / 5 |
| EV22 | sim | EV20 | ~300 / 9 |
| EV21 | sim | EV22, SB-T6 | ~0 / 5 |

Fila: EV1a → EV1b → EV2 → EV3 → EV3c → EV3d → EV4a → … → EV20 → EV22 → EV21 em série (o XR3 entra entre o EV3 e o EV3c, porque abre o lote 9) (o EV22 entra antes do EV21, que fecha a documentação). O B-016 pode começar a barra de cast depois do EV13, e o toggle e o autocast do slot depois do EV14. CA33 e CA36 rodam em todos.

### Emenda de 2026-11-02: lote 8, saída gerada e catálogo

Estado: aprovada no gate parcial de 2026-11-02, junto com a emenda de 2026-11-02 do lote 8 da `referencia-catalogo-e-api.md`, com L1, L2 e L3 como propostos (respostas lá).

- O EV1, o EV2, o EV3 e o EV4a entram no lote 8, depois do DM8. O EV4b abre o lote 9. A ordem do lote e os pares estão na `referencia-catalogo-e-api.md`, seção "Lote 8 e pares do WIP 2". (Revisto pela emenda seguinte, da divisão do EV1.)
- Pares com Unity no lote 8 (emenda de 2026-11-02 da 0003): EV1∥EX4 e EV2∥EX5. O EV3 e o EV4a seguem o WIP 1. (Revisto pela emenda seguinte.) Fora desses dois pares, continua valendo a frase do começo de "Tickets": todos usam a Unity e seguem o WIP 1.
- Saída gerada: todo EV muda `.cs` do pacote. Cada um regenera e commita `Documentation~/manual/referencia/catalogo.md`, `Documentation~/api/reference/**` e `Documentation~/api/reference.sha256` (D6 da `referencia-catalogo-e-api.md`). Esses caminhos ficam no escopo de todos os EV como saída gerada, fora da contagem (emenda "saída de gerador commitada" da 0003). A geração passou a existir depois desta Spec, e é por isso que os escopos acima não a citam.
- Catálogo: o EV2 e o EV3 trocam as seções `AbilityAction` e `EffectAction` do catálogo pela `GameplayAction`, e o EV4a cria a seção `TargetSelector` (o campo `target` tem `[TypePicker]`). Desde o RC8, a guarda `EveryCatalogTypeHasSummary` não tem lista de pendências: todo tipo de catálogo novo leva `[TypeCategory(caminho, Summary = ..., Line = ...)]` com `Summary` não vazio, no mesmo diff. As ações migradas mantêm o `Summary` que o RC7 e o RC8 puseram.
- EV3: o escopo ganha `Tests/Core/EditMode/Documentation/TypeCatalogTests.cs`. O `Build_HasPackageBases` (`:63-73`) procura a base `"SpellBook.GAS.Abilities.AbilityAction"` pelo nome, num texto, e o compilador não acusa a troca. Depois do EV3, o nome é `SpellBook.GAS.Actions.GameplayAction`. O arquivo já era um dos "testes que usam `AbilityAction`" do escopo; a emenda o põe na lista por nome, e a lista da exceção do EV3 fica com ~23 arquivos, sem contar as três remoções puras. Com o `AbilityAction.cs` apagado, o `Build_HasPackageBases` falha até o EV3 trocar o nome esperado; o teste continua cobrindo o mesmo comportamento.
- Exemplos do manual: o EX1 entra antes do EV1, e o `gas.md` fica na lista de pendências da guarda `NoHandWrittenCSharpBlocks` até o EX3, depois do EV7 (pergunta L1 da `referencia-catalogo-e-api.md`). Até lá, o EV1 a EV7 escrevem os blocos C# do `gas.md` à mão, e o EX3 os migra. Depois do EX3, o EV que escrever bloco C# no `gas.md`, ou numa página que saia dele na divisão do MN12 a MN17, põe `Tests/GAS/EditMode/Examples/GasManualExamples.cs` no escopo. O Arquiteto reconta o EV8 em diante quando montar os lotes deles.

### Emenda de 2026-11-02: EV1 dividido e lote 8 revisto

Estado: a divisão do EV1 é decisão do usuário (2026-11-02). A ordem nova do lote 8, o par novo e a saída do EV4a para o lote 9 foram aprovados no gate parcial de 2026-11-02, com E1 e E2 como propostos (respostas abaixo).

- **Tickets:** o EV1 vira o EV1a (issue #174, renomeada) e o EV1b (issue #307), na seção "EV1 — dividido em EV1a e EV1b". O EV2 passa a depender do EV1b. O EV3 não muda.
- **Lote 8:** DM7 → EX1 → DM8∥EX2 → EX4 → EV1a∥EX5 → EV1b → EV2 → EV3. São 10 tickets. O EX4 entra sozinho, e o par dele com o EV1 deixa de existir.
- **Par novo EV1a∥EX5.** Ele cabe nas duas condições da emenda de 2026-11-01 da 0003. O EX5 depende só do EX4, que entra antes, e nenhum dos dois depende do outro. Os escopos não têm arquivo em comum: o EV1a escreve `Runtime/GAS/Actions/` (com o `.meta` da pasta), `AbilitySystemComponent.cs`, `Tests/GAS/PlayMode/ActionRunnerTests.cs`, `gas.md`, o `CHANGELOG.md` e a saída gerada; o EX5 escreve `modulos/pooling.md`, `modulos/tempo.md`, `Tests/Pooling/EditMode/Examples/PoolingManualExamples.cs`, `Tests/Time/EditMode/Examples/TimeManualExamples.cs` (com o `.meta` das pastas) e a guarda dos exemplos. O EX5 tira da lista de pendências só as duas páginas dele, e o `gas.md` que o EV1a escreve continua na lista até o EX3. O EV2 perde o par: depois do EX5 não sobra EX no lote 8.
- **EV1b, EV2 e EV3** seguem o WIP 1. Os três mudam `.cs` do pacote e escrevem `api/reference.sha256`, então não formam par entre si.
- **EV4a:** sai do lote 8 e abre o lote 9, antes do EV4b. Com o EV1 dividido, o lote passaria a 11 tickets, e o teto do PR é de 10, como na L3 da `referencia-catalogo-e-api.md`, que tirou o EV4b pelo mesmo motivo. Nenhuma dependência muda: o EV4a continua depois do EV3.
- A `referencia-catalogo-e-api.md` (seção "Lote 8 e pares do WIP 2") e a ADR 0003 recebem emendas da mesma data com essa ordem.

### Emenda de 2026-11-02: EV3 só com a troca, EV3c e EV3d

Estado: decisão do usuário de 2026-11-02 (opção (c) para o EV3 e proposta WAIT). Vem da recontagem do EV3 no código depois do EV2: a lista aprovada em 2026-10-31 (~22 arquivos, ~23 com o `TypeCatalogTests.cs` da emenda anterior) deixava de fora o `ScalableFloatDrawer.cs` do Editor e boa parte dos testes que usam `AbilityAction`, `AbilityContext` ou o campo `GameplayAbility.Actions`.

- **EV3:** faz só a troca atômica da base, com o mesmo comportamento. A `WaitAction` fica com `float`, e o `Editor/GAS/ScalableFloatDrawer.cs` entra no escopo. A lista fechada tem 35 arquivos e ~1150 linhas, sem as três remoções puras e sem a saída gerada. A exceção única da 0003 foi ampliada para esse tamanho (emenda de 2026-11-02 da 0003).
- **EV3c (novo):** as listas `OnEnd` e `OnCancel` da habilidade, com testes próprios e o `CollectActionEffects` para a validação da tabela e o drawer.
- **EV3d (novo):** a `WaitAction` com `ScalableFloat`, o campo legado convertido no carregamento por `[FormerlySerializedAs]`, o menu de migração regravando os `GameplayAbility` e o teste de migração.
- **Lote 8:** não muda. Fecha com DM7, EX1, DM8, EX2, EX4, EV1a, EX5, EV1b, EV2 e EV3, com o EV3 como 10º ticket.
- **Lote 9:** XR3 → EV3c → EV3d → EV4a → EV4b, e o resto da fila como antes. O EV3c e o EV3d seguem o WIP 1, porque mudam `.cs` do pacote e escrevem `api/reference.sha256`, como os outros EV.
- **Dependências:** EV3c depende do EV3 (e vem depois do XR3 na fila); EV3d depende do EV3c; EV4a passa a depender do EV3d.
- **Issues:** o EV3c e o EV3d precisam de issue nova, com os títulos dos commits acima. A issue do EV3 recebe a lista nova de arquivos e perde as listas `OnEnd` e `OnCancel` e o `ScalableFloat` da `WaitAction`. Quem cria e edita as issues é o Controlador.

### Emenda de 2026-11-03: documentação no fim e detalhe na entrada

Estado: decisão do usuário, aprovada pelo usuário em 2026-11-03. Vem das emendas de 2026-11-03 da 0002, da 0003, da 0007 e da 0040, e da Spec `processo-doc-no-fim.md`.

- **Saída gerada.** A regra "todo EV regenera e commita" da emenda de 2026-11-02 ("lote 8, saída gerada e catálogo") deixa de valer. Ela é trocada pela regra provisória da `processo-doc-no-fim.md`: o EV10 regenera só se a guarda reprovar. Depois do PD1, os EV11 em diante não regeneram, e a geração roda no passe de documentação.
- **Manual.** O `gas.md`, o `eventos.md`, o `manual/ferramentas/validador.md` e as outras páginas saem do escopo dos EV11 em diante. O que cada ticket documentaria fica no objetivo e nos CA, para o passe de documentação. O EV21 deixa de "fechar a documentação": a parte de páginas vai para o passe, e o Sample refeito continua no EV21.
- **CHANGELOG.** Fica no escopo só dos EV com `!` no commit, com a entrada de quebra e a migração.
- **Catálogo.** O `[TypeCategory(..., Summary = ...)]` continua obrigatório em todo tipo novo do catálogo (guarda `EveryCatalogTypeHasSummary`, fora da categoria `Documentacao`).
- **Fila.** O PD1 entra depois do EV10 e antes do EV11.
- **Tickets ainda fora da fila** (EV11 a EV22 e EV21). O escopo, os testes e o tamanho escritos acima são rascunho. O Arquiteto os refaz lendo o código, na entrada de cada um na fila (emenda de 2026-11-03 da 0002). O objetivo, os CA e as dependências continuam valendo.

## Perguntas do gate parcial da divisão do EV1 (2026-11-02)

Respostas do usuário (2026-11-02), as duas como propostas: E1 — par EV1a∥EX5, com o EV2 sem par. E2 — o EV4a abre o lote 9, e o lote 8 fecha com DM7, EX1, DM8, EX2, EX4, EV1a, EX5, EV1b, EV2 e EV3. As perguntas, como foram ao gate:

- **E1. Par do lote 8.** Proposta: EV1a∥EX5, com o EV2 sem par. O EX5 não espera o EV1b nem o EV2, e o lote fecha antes. Alternativa: manter o EV2∥EX5 aprovado na L3, com o EV1a e o EV1b em série depois do EX4.
- **E2. EV4a.** Proposta: sai do lote 8 e abre o lote 9, para o PR continuar com 10 tickets. Alternativa: o lote 8 fica com 11 tickets e um PR maior.

## Perguntas ao usuário

- **Q1. Tamanho do EV3 (troca atômica da hierarquia).** Trocar a base das ações da habilidade obriga a mudar, no mesmo commit, todas as ações, o ASC, o projétil, a factory e os testes que as usam (~1100 linhas, ~22 arquivos). Proposta: **exceção única à ADR 0003 para o EV3**, como a do 0M-54, com o diff revisado por lista de arquivos e pelos testes nomeados. Alternativa: dividir em 4 tickets com uma **ponte temporária** (`AbilityAction` vira subclasse de `GameplayAction` e o executor roda o `IEnumerator` dela até a última ação migrar), apagada no fim do item; é um mecanismo paralelo por alguns tickets.
- **Q2. `Delayed` segura a habilidade ativa.** Proposta: a ativação só termina quando as execuções atrasadas dela terminam (a ação seguinte roda no mesmo quadro; a habilidade fica ativa até a atrasada rodar), e cancelar a habilidade cancela a atrasada. Alternativa: a habilidade termina ao fim da lista principal e a atrasada roda sem dono (o padrão "cancela com o dono" do PRD R15 perderia o sentido na habilidade instantânea).
- A ADR 0034 e o DS do Inspector de ações passam pelo gate junto com esta Spec.
- **Respostas do gate (2026-10-31):** Q1 — exceção única à 0003 para o EV3, como a do 0M-54 (sem ponte temporária). Q2 — sim, `Delayed` segura a habilidade. P-DS5 — entra como EV22. ADR 0034 aceita; DS aprovado.
