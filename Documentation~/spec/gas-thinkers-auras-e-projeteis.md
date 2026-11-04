# Spec: Thinkers, auras e projéteis
PRD: [Documentation~/prd/gas-thinkers-auras-e-projeteis.md](../prd/gas-thinkers-auras-e-projeteis.md) · DS: [Documentation~/ds/inspector-de-acoes.md](../ds/inspector-de-acoes.md) · Backlog: B-014 · ADRs: **0035 e 0036 (novas, Propostas)**; usa 0003, 0004, 0007, 0012, 0013, 0014, 0015, 0018, 0026, 0030, 0032, 0034

Status: Aprovada no gate de 2026-10-31, Lote D (TK1–TK8; ADRs 0035 e 0036 aceitas; Q1: uma regra só para "termina com a execução"; Q2: thinker com ASC próprio; Q3: filtro único) · Data: 2026-10-31 · Autor: Arquiteto

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`. O B-012 muda parte destas linhas antes deste item.

- `Runtime/GAS/Abilities/Projectiles/Projectile.cs`:
  - campos públicos de movimento, `InitialInvulnerabilityDuration`, `DamageOwner`, `Lifetime` (`<= 0`: sem limite, `:124`), `IsHoming` e `timeSource` (`:20-56`);
  - `Setup(spawner, …)` (`:105-117`);
  - `OnEnable` reinicia o estado (`:119-128`);
  - o tempo é consumido por diferença no `FixedUpdate` (`:143-167`), e o fim do `Lifetime` chama `ReturnToPool` sem ação (`:156-160`);
  - `ReturnToPool` volta uma vez só (`_released`, `:278-290`);
  - `HandleCollision` usa `GetComponent<AbilitySystemComponent>` (`:306`, o item 14 do relatório) e trata colisor sem ASC como impacto (`:309`);
  - `HandleImpact` roda as ações e volta ao pool a cada impacto (`:312-329`).
  - O B-012 (EV3) troca `OnHitActions` por `OnImpact` (`[ActionEvent]`) e roda o impacto por `Source.RunActions` (P15).
- `Runtime/GAS/Core/AbilitySystemComponent.cs`:
  - `TimeSource`, `TickedTime` e `SetTimeSource` (`:46-97`);
  - `OnDisable` chama `CancelAllAbilities` (`:74-78`);
  - `Update` consome o tempo por diferença (`:80-87`).
  - O executor (`RunActions`, `CancelActions`, `HasRunningActions`, pools) chega no EV1, e o fim da ativação "depois das execuções dela" no EV6.
- `Runtime/GAS/Targeting/TargetingStrategy.cs:42-73`: overlap 2D/3D com buffers e `GetComponentInParent`. O B-012 substitui pelo `AreaSelector` (EV4b), com buffers por ASC e o filtro de candidato da ADR 0035 (EV8, emenda).
- `Runtime/Core/Spawning/SpawnerAsset.cs:36-38`: `Spawn` e `Release`. O RP-07 documenta o `null`.
- Testes do projétil: `Tests/GAS/PlayMode/AbilityWaitTests.cs` (`Projectile_*`, migrados no EV3) e `SpawnActionsTests.cs:90-` (`Projectile_ReturnsThroughSpawner`).
- Factories: `Tests/Testing/GasFactory.cs`, `SpawnFactory.cs` (`CreatePrefab`, `CreateParticlePrefab`) e `TimeFactory.cs`.

## Respostas às perguntas técnicas do PRD (seção 8)

- **P8. Detecção da aura.** Consulta de área por pulso (overlap), pelo `AreaSelector` do B-012 que a aura carrega. Não depende de gatilho de física e é determinística em teste. O conjunto "quem está dentro" fica no estado de execução do thinker: duas listas paralelas pré-alocadas (`alvo`, `instância aplicada`) e um buffer do pulso. Sem `HashSet` e sem alocar em regime (capacidade inicial 32, cresce dobrando).
- **P9. Onde moram.** `GameplayThinker`, `ThinkerBehaviour`, `AuraBehaviour` e `CreateThinkerAction` ficam no `SpellBook.GAS` (`Runtime/GAS/Thinkers/`). `AuraBehaviour` é uma configuração `[SerializeReference]` do thinker (campo `Behaviour`, um por thinker). A aura presa a uma unidade (R12) é receita, sem tipo novo.
- **P10. Eventos.**
  - Thinker: `OnCreated` (Ao criar), `OnPulse` (A cada pulso), `OnEnded` (Ao terminar).
  - Projétil: `OnImpact` (do B-012) e `OnEnded` (Ao terminar).
  - O motivo vai no contexto (`ActionContext.EndReason`, abaixo).
  - Alvo padrão: thinker, o próprio thinker (o ASC dele); projétil `OnEnded`, nenhum alvo, com o ponto de término em `context.Point`.
- **P11. Contexto do thinker.** O thinker guarda uma cópia do que importa do contexto de criação: instigador, habilidade, spec, nível e `EffectContext` (struct). Isso fica em campos do componente, preenchidos na criação, sem alocar por pulso. Cada pulso pega um `ActionContext` do pool do ASC do thinker (EV1).
- **P12. Gancho do filtro de impacto.** ADR 0035: as condições de impacto são o filtro de candidato `Condition<ConditionContext>`, com `Owner` = quem lançou e `Target` = `AtUnit(ASC atingido)`. A `TeamRelationCondition` (B-011) e a `TagQueryCondition` com sujeito `TargetUnit` se encaixam sem código novo.
- **P13. Efeito da aura e instância.** A aura guarda a `ActiveEffectInstance` que aplicou, na lista paralela do estado. Remove por `RemoveEffect(instância)` (EA3), e só essa. Instância já removida por outra via (`IsRemoved`, NC7) conta como "sem efeito" e é reaplicada no pulso seguinte se o alvo continua dentro (P3).
- **P14. Cancelamento no término.** `End(reason)` é idempotente (flag como o `_released`). Ele cancela as execuções do thinker no ASC dele (`CancelActions(thinker)`, `OnCancel` em ordem reversa, `Delayed` pendentes inclusive) antes de rodar `OnEnded`. Depois remove os efeitos da aura e devolve pelo spawner.
- **P15. Regra do B-030 nos pulsos.** Garantida pela ADR 0036, item 3: os pulsos são `ActionStatus.Wait` no cursor lógico da 0034. O teste `Pulse_TickInSameFrameAsPulseEnd_Counts` fixa (CA9).
- **P16. Ordem.** TK1 (vínculo de execução) primeiro, porque o B-013 também o usa. Depois o projétil (TK2, TK3), que não depende de B-011 nem de B-013. Depois thinker e aura (TK4 a TK6). O filtro de time não é ticket deste item: é a `TeamRelationCondition` (TM5) no filtro de candidato. TK8 tem o teste de ponta a ponta com o time.
- **Capacidade do "já acertou" (P6, número do Arquiteto).** 32 alvos por vida do projétil, num vetor pré-alocado. O validador avisa `Pierce` acima de 31. Os acertos além do limite contam como acerto repetido (P6).

## Design

### 1. Vínculo de execução (ADR 0036, item 4)

- `public enum ExecutionLink { Holds, FollowsOwner }`.
- `AbilitySystemComponent.RunActions(IReadOnlyList<GameplayAction> actions, ActionContext context, object owner, ExecutionLink link = ExecutionLink.Holds)`.
- `FollowsOwner`: a execução não conta para "a ativação termina quando as execuções dela terminam" (EV6). Ela é cancelada:
  - em `CancelActions(owner)`;
  - no fim da ativação (`EndAbility`, antes de `OnAbilityEnded`);
  - na remoção da instância de efeito, que já cancela tudo do dono.
- `ActionStatus.WaitUntilCancelled` (novo, sem tempo): a execução fica parada até ser cancelada. Ele é usado pelas vigias do thinker e do cue, e não lê o relógio.

### 2. Motivo de término no contexto

- `public enum ActionEndReason { None, Hit, Lifetime, Obstacle, Released, Expired, OwnerGone, ExecutionEnded }` (`Runtime/GAS/Actions/`).
- `ActionContext.EndReason` vale `None` fora de uma aba "Ao terminar".
- É um enum só para o projétil e o thinker. Uma `Condition` pode lê-lo pela execução em curso (`ConditionContext.Action`, EV7).

### 3. Projétil

Campos novos (padrões que reproduzem o comportamento de hoje, N7):
- `int Pierce = 0` e `bool UnlimitedPierce = false`;
- `bool HitEachTargetOnce = true`, que só vale com perfuração;
- `[SerializeReference, TypePicker] Condition<ConditionContext> ImpactFilter`, o filtro de candidato da ADR 0035;
- `[ActionEvent("Ao terminar", …)] List<GameplayAction> OnEnded`.

Fluxo:
- `HandleCollision(other)`:
  1. procura o ASC subindo a hierarquia com `TryGetComponent` (R18; sem a alocação do `GetComponentInParent` no Editor quando não acha);
  2. sem ASC: `End(Obstacle)` (P7);
  3. dono sem `DamageOwner`: ignora (como hoje);
  4. `HitEachTargetOnce` e o alvo já está no vetor "já acertou": ignora;
  5. o filtro de candidato falha: ignora, sem gastar perfuração e sem terminar (C14);
  6. senão, `HandleImpact`.
- `HandleImpact`:
  - roda `OnImpact` no ASC de quem lançou (EV3), registra o alvo e conta o impacto;
  - `impactos > Pierce`, sem `UnlimitedPierce`: `End(Hit)`.
- `Lifetime`: `End(Lifetime)`. `ReturnToPool()` público: `End(Released)`.
- `End(reason)`, uma vez:
  - com `OnEnded` não vazia e `Source` ativo, roda `OnEnded` no ASC de quem lançou, com `EndReason` e `Point` = posição atual;
  - depois devolve ao spawner (como o `ReturnToPool` de hoje);
  - sem ações, é o caminho de hoje (C16).
- `OnEnable` limpa o vetor e o contador (CA24).

### 4. Thinker

`[RequireComponent(typeof(AbilitySystemComponent))] public class GameplayThinker : MonoBehaviour, IValidatable` (`[HelpURL]`):
- Configuração:
  - `ScalableFloat duration` (0 = sem duração);
  - `ScalableFloat pulseInterval` (0 = sem pulsos);
  - `bool followOwner = false`, `bool endWithExecution = true`;
  - `bool followRotation = false`;
  - `[SerializeReference, TypePicker] ThinkerBehaviour behaviour`;
  - três listas `[ActionEvent]`.

  Valores por nível avaliados com o nível e a tabela da habilidade do contexto de criação (`%raio`, `%intervalo`, C9).
- Estado (campos do componente, limpos no `OnEnable`): criador (ASC), cópia do contexto de criação, `ended`, spawner de criação e o estado do comportamento (`ThinkerBehaviour.CreateState()` uma vez no `Awake`, reutilizado e limpo a cada ciclo de pool).
- **Ciclo** (ADR 0036, item 3): `Begin(...)` (chamado pela `CreateThinkerAction`, ou no `Start` quando o thinker foi colocado na cena sem criador) roda no ASC do thinker a ação interna `ThinkerLifeAction`. Ela:
  1. roda `OnCreated` (execução própria do thinker);
  2. em laço, espera `Wait(próximo marco)`, sendo o próximo marco o pulso ou o fim da duração. Em cada pulso, chama `behaviour.OnPulse(state, ctx)` e depois roda `OnPulse` como execução própria;
  3. quando a duração vence, chama `End(Expired)`. Pulso e fim no mesmo instante: o pulso primeiro (C1: pulsos em 1 a 5 com duração 5).
- Sem duração e sem pulso, a vida espera até ser cancelada (`WaitUntilCancelled`).
- **Criador:**
  - `followOwner` copia a posição (e a rotação, com `followRotation`) do criador a cada passo do ASC do thinker, num `LateUpdate` que só lê o transform;
  - com `followOwner` ou `endWithExecution`, criador nulo ou inativo leva a `End(OwnerGone)`, conferido no mesmo `LateUpdate`, sem alocar.
- **`End(reason)`**, uma vez:
  1. cancela as execuções do thinker no ASC dele;
  2. roda `OnEnded` com `EndReason`;
  3. chama `behaviour.OnEnd(state, ctx)` (a aura remove os efeitos);
  4. cancela a vigia do criador (se houver);
  5. volta pelo spawner de criação, ou `SpawnerAsset.OrDefault` para o thinker da cena.
- **Thinker na cena** (P2): sem criador, o `Instigator` do contexto é nulo, e o nível é 1. O validador avisa ações que leem o instigador.

`ThinkerBehaviour` (`[Serializable] abstract`):
- `object CreateState()`;
- `void OnBegin(object state, GameplayThinker t, ActionContext ctx)`;
- `void OnPulse(object state, GameplayThinker t, ActionContext ctx)`;
- `void OnEnd(object state, GameplayThinker t, ActionContext ctx)`;
- `Validate`.

A configuração não guarda estado (ADR 0013).

### 5. `CreateThinkerAction` (R8)

`[TypeCategory("Mundo", Line = "Criar {Prefab}")]`.
- Campos: `GameplayThinker Prefab`, `SpawnerAsset Spawner`, `ThinkerPosition position` (`Point`, `Caster`, `FirstTarget`; ou o seletor da ação, quando há), `RotationMode` (`Identity`, `Caster`, `TowardPoint`).
- Execução:
  1. `Spawn` pelo spawner (`null`: aviso uma vez, `Done`, C17);
  2. o ASC do thinker recebe `SetTimeSource(context.Owner.TimeSource)`;
  3. `thinker.Begin(criador: context.Owner, contexto, spawner)`.
- Com `endWithExecution` do thinker e um dono lógico na execução corrente, começa no ASC do criador uma vigia `FollowsOwner` (`ThinkerWatchAction`, com a referência do thinker em `ActionFrame.Data`) cujo `OnCancel` chama `thinker.End(ExecutionEnded)`.
- Grava o thinker em `context.LastCreatedThinker` e devolve `Done` na hora.

### 6. Aura (R9 a R13)

`AuraBehaviour : ThinkerBehaviour` (`[TypeCategory("Mundo", Line = "Aura {Effect}")]`):
- Campos: `GameplayEffect effect`, `AreaSelector area` (raio, origem = o thinker, filtro de candidato com tags e time, `includeOwner`, máximo, ordem; ADR 0035).
- Estado: `List<AbilitySystemComponent> inside`, `List<ActiveEffectInstance> applied` e `List<AbilitySystemComponent> scratch`.
- `OnBegin` faz um pulso imediato (C6: quem já estava dentro recebe o efeito ao nascer). Se o PRD preferir no primeiro pulso, só muda o teste: o texto do C6 diz "no primeiro pulso", e o pulso imediato é o primeiro.
- `OnPulse`:
  1. `area.Select(ctx, scratch)`, onde o filtro do seletor decide também "deixou de casar";
  2. para cada `inside[i]` que não está em `scratch`, ou cuja instância foi removida por outra via: remove a instância (se ainda viva) e tira da lista;
  3. para cada alvo de `scratch` que não está em `inside`: aplica `EffectSpec` com instigador = criador, habilidade e nível do contexto (B-007) e guarda a instância.

  Instância removida por outra via e alvo ainda dentro: reaplica neste pulso, que é "o pulso seguinte" à remoção (P3).
- `OnEnd`: remove todas as instâncias vivas e limpa as listas (C7).
- Efeito instantâneo (sem instância) só aplica, com aviso no validador.

### 7. Validação (R23) e Inspector (R22)

- **Thinker:**
  - sem duração, sem pulso e sem `endWithExecution` (aviso: nunca termina);
  - duração ou intervalo constantes negativos (erro);
  - ações em `OnPulse` sem intervalo (aviso);
  - prefab sem ASC (o `[RequireComponent]` acrescenta; o validador confere o prefab);
  - thinker na cena com ações que leem o instigador (aviso).
- **Aura:**
  - sem efeito (erro);
  - efeito instantâneo (aviso);
  - raio constante ≤ 0 (erro);
  - thinker sem intervalo (aviso: só o pulso inicial);
  - intervalo constante abaixo de 1/60 s (aviso).

  O aviso do PRD "filtro de time sem Teams" não se aplica: sem o módulo, o tipo não existe (ADR 0035).
- **Projétil:**
  - `Pierce` negativo (erro);
  - `Pierce` acima de 31 (aviso, P6);
  - `UnlimitedPierce` com `Lifetime ≤ 0` (aviso: nunca termina);
  - `HitEachTargetOnce` desligado com perfuração (aviso);
  - `OnEnded` com `SpawnProjectileAction` do mesmo prefab (aviso de laço).
- **`CreateThinkerAction`:**
  - sem prefab (erro);
  - `%nome` inexistente nos valores do thinker (regra do VN9);
  - `endWithExecution` com a ação numa habilidade `Instant` (aviso: termina quando a ativação termina, pergunta Q1).
- **Inspector:**
  - `GameplayThinkerEditor` (UI Toolkit, `SpellBookEditor`) com configuração, comportamento (cabeçalho da aura com efeito, raio e intervalo) e as abas pelo `ActionEventsView` (EV17), que descobre os campos `[ActionEvent]`;
  - o `ProjectileEditor` (EV17) ganha a aba "Ao terminar" sem código, pelo atributo;
  - Gizmo do raio pelo `OnDrawGizmosSelected` do thinker, com o raio avaliado no nível 1 da prévia. A cor do time fica fora: o GAS não referencia Teams.

## Quebras de compatibilidade

- **Projétil:** `HandleCollision` passa a achar o ASC no pai (correção, item 14 do relatório). Um colisor filho de um ASC antes era tratado como "sem ASC" e agora é impacto nesse ASC. Também deixa de existir o término sem ação no fim do `Lifetime` quando há `OnEnded`. Entra no CHANGELOG como **correção** e na página de migração.
- `RunActions` ganha um parâmetro opcional (sem quebra).
- Adicionado: `ExecutionLink`, `ActionStatus.WaitUntilCancelled`, `ActionEndReason`, `GameplayThinker`, `ThinkerBehaviour`, `AuraBehaviour`, `CreateThinkerAction`, perfuração, "um acerto por alvo", filtro de impacto e `OnEnded` do projétil.

## Tickets

Todos usam a Unity e seguem o WIP 1. Tempo por `asc.Tick` com `ManualTimeSource`, física por `Physics.SyncTransforms` e overlap nos testes PlayMode, e objetos pela `GasFactory` e pela `SpawnFactory`. Cada ticket escreve a sua parte de `gas.md` ou da página nova e do `CHANGELOG.md`. Teto: ~500 linhas e 10 arquivos.

**Conflitos com a fila:**
- O item inteiro toca `AbilitySystemComponent.cs`, `ActionRunner.cs`, `ActionContext.cs` e `Projectile.cs` (todo o B-012): entra depois do EV21.
- `DocUrls.cs`: TK4, TM1 e CU2.
- `GasFactory.cs`: TK4 e qualquer ticket do B-012 aberto.
- `validador.md`: TK7, TM3 e CU6.
- O CU8 depende do TK1.

### TK1 — Vínculo de execução `FollowsOwner` e espera até cancelar
- Escopo: `Runtime/GAS/Actions/ExecutionLink.cs` (novo), `ActionStatus.cs` (`WaitUntilCancelled`), `ActionRunner.cs`, `AbilitySystemComponent.cs` (`RunActions` com o vínculo; fim da ativação cancela as `FollowsOwner`), `Tests/GAS/PlayMode/ExecutionLinkTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red:
  - `FollowsOwner_CancelledWhenActivationEnds_DoesNotHoldIt`;
  - `FollowsOwner_CancelledWithEffectRemoval_AllReasons`;
  - `FollowsOwner_CancelledOnAbilityCancel`;
  - `WaitUntilCancelled_NeverEndsByTime`;
  - `Holds_StillDefault_DelayedRuleUnchanged`;
  - `FollowsOwner_DoesNotAllocate_AfterWarmup`.
- Critério de pronto: testes verdes; a suíte do B-012 sem edição.
- Tamanho estimado: ~320 linhas; 7 arquivos. Depende de: EV21.

### Detalhe (2026-11-03)
- CA: nenhum CA do PRD diretamente; base do CA5 (TK5) e do cue contínuo (CU8). Design 1 e ADR 0036, itens 4 e 5.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/GAS/Actions/ExecutionLink.cs` (novo): `public enum ExecutionLink { Holds, FollowsOwner }`.
  - `Runtime/GAS/Actions/ActionStatus.cs:24,44`: `StatusKind.WaitUntilCancelled` e `public static ActionStatus WaitUntilCancelled`.
  - `Runtime/GAS/Actions/ActionRunner.cs`: estado `EntryState.Suspended` (`:82`), que pára o avanço sem ler o relógio (`:246-255`, `:288-303`); campo `Execution.Link` (`:96-114`); `Run(..., link)` (`:133-152`); `HasRunning` ignora as `FollowsOwner` (`:154-161`); `Start` recebe o vínculo (`:208-223`), herdado pelas filhas em paralelo (`:403-417`) e pela execução agendada do `DelayedAction` com dono (`:357-377`).
  - `Runtime/GAS/Core/AbilitySystemComponent.cs`: `RunActions(actions, context, owner, ExecutionLink link = ExecutionLink.Holds)` (`:365-375`); doc de `HasRunningActions` (`:385-390`); `EndAbility` cancela as execuções que restam do spec antes das listas "Ao terminar"/"Ao ser cancelada" e do `OnAbilityEnded` (`:1275-1297`). A remoção da instância de efeito já cancela tudo do dono (`:2336`), sem mudança.
  - `Tests/GAS/PlayMode/ExecutionLinkTests.cs` (novo).
  - Sem `gas.md` nem `CHANGELOG.md`: manual só no passe de documentação (emenda de 2026-11-03 da 0007), e o ticket não quebra nada (parâmetro opcional).
- Testes red (PlayMode, `SpellBook.GAS.Tests.PlayMode`, classe `ExecutionLinkTests`): `FollowsOwner_CancelledWhenActivationEnds_DoesNotHoldIt`; `FollowsOwner_CancelledWithEffectRemoval_AllReasons` (Removed, RemovedByTag, Expired, StacksDepleted); `FollowsOwner_CancelledOnAbilityCancel`; `WaitUntilCancelled_NeverEndsByTime`; `Holds_StillDefault_DelayedRuleUnchanged`; `FollowsOwner_DoesNotAllocate_AfterWarmup`.
- Critério de pronto: os testes acima verdes pelo filtro `\.ExecutionLinkTests\.`, e `ActionRunnerTests`, `DelayedActionTests`, `AbilityEndCancelListsTests`, `EffectEventActionsTests` e `RunningActionsQueryTests` (suíte do B-012) verdes sem edição.
- Tamanho: ~360 linhas e 5 arquivos, pela ADR 0042 (medido no diff: 357 linhas). Exceção: nenhuma.
- Commit: `feat(gas): vínculo FollowsOwner e espera até cancelar`

### TK2 — Projétil: ASC no pai, perfuração e um acerto por alvo
- Escopo: `Runtime/GAS/Abilities/Projectiles/Projectile.cs`, `Tests/GAS/PlayMode/ProjectilePierceTests.cs` (novo), `Tests/Testing/GasFactory.cs` (`CreateProjectile`, alvo com colisor filho), `gas.md` (`#projectile`), `CHANGELOG.md` (correção do ASC no pai).
- Testes red:
  - `Pierce2_HitsThree_ImpactOncePerTarget_EndsOnThird` (CA17, C10);
  - `Unlimited_PassesAll_EndsByLifetime` (CA18, C11);
  - `ChildCollider_HitsParentAsc_TwoCollidersOneHit` (CA21, C15);
  - `NoNewFields_BehavesAsToday` (CA22, C16, com os casos do `Projectile_*` atual);
  - `DamageOwner_Invulnerability_Lifetime_Homing_Unchanged` (CA23);
  - `FromPool_HitSetCleared` (CA24);
  - `HitSetBeyond32_CountsAsRepeated`;
  - `Impacts_1000_WithPierce_DoNotAllocate` (CA27, parte).
- Critério de pronto: CA17, CA18 e CA21 a CA24 verdes.
- Tamanho estimado: ~430 linhas; 5 arquivos. Depende de: TK1.

### Detalhe (2026-11-03)
- CA: CA17, CA18, CA21, CA22, CA23, CA24 e a parte do projétil do CA27 (C10, C11, C15, C16). Design 3, sem `ImpactFilter`, `OnEnded` e `End(reason)` (TK3).
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/GAS/Abilities/Projectiles/Projectile.cs`:
    - campos públicos `Pierce = 0`, `UnlimitedPierce = false` e `HitEachTargetOnce = true` (seção nova `[Header("Pierce")]` depois de `Lifetime`, `:48-49`);
    - estado: vetor "já acertou" de 32 posições pré-alocado, com contador, e o contador de impactos (`:80-86`); `OnEnable` limpa os dois (`:130-139`, CA24);
    - o contexto derivado no `Setup` (`:104-128`) passa a ser um modelo guardado: cada impacto deriva dele (`CreateDerived`), em vez de consumir na primeira vez. Com perfuração, o segundo impacto mantém instigador, habilidade e nível. O modelo volta ao pool do ASC dono no término (`ReturnToPool`, `:289-301`) ou num novo `Setup`;
    - `HandleCollision` (`:313-321`): ignora depois do término; acha o ASC subindo a hierarquia com `TryGetComponent` (C15); sem ASC, o caminho de hoje (impacto sem alvo e término, sem gastar perfuração; o `Obstacle` é do TK3); dono sem `DamageOwner` ignora (agora também por colisor filho do dono); com `HitEachTargetOnce` e perfuração, alvo já no vetor, ou o vetor cheio, conta como repetido e é ignorado (P6);
    - `HandleImpact` (`:323-338`): roda `OnImpact`, registra o alvo e conta o impacto; termina só com `impactos > Pierce` e sem `UnlimitedPierce`.
  - `Tests/Testing/GasFactory.cs`: `CreateProjectile<T>(TimeSourceAsset, Action<T> configure)` (projétil parado, sem limite de vida e sem invulnerabilidade, configurado antes do `OnEnable`) e `AddChildCollider(AbilitySystemComponent)` (filho com `BoxCollider`).
  - `Tests/GAS/PlayMode/ProjectilePierceTests.cs` (novo).
  - Sem `SpawnProjectileAction.cs`: os campos ficam no prefab. Sem `gas.md` nem `CHANGELOG.md`: manual só no passe de documentação (emenda de 2026-11-03 da 0007), e o ASC no pai é correção (seção "Quebras"), sem `!`.
  - O vínculo `FollowsOwner` (TK1) não entra: o impacto roda sem dono, como hoje.
- Testes red (PlayMode, `SpellBook.GAS.Tests.PlayMode`, classe `ProjectilePierceTests`): `Pierce2_HitsThree_ImpactOncePerTarget_EndsOnThird`; `Pierce_EveryImpact_KeepsAbilityContext`; `Pierce_Obstacle_EndsAtOnce`; `Unlimited_PassesAll_EndsByLifetime`; `ChildCollider_HitsParentAsc_TwoCollidersOneHit` (física real, gatilho); `NoNewFields_BehavesAsToday`; `DamageOwner_Invulnerability_Lifetime_Homing_Unchanged`; `FromPool_HitSetCleared`; `HitSetBeyond32_CountsAsRepeated`; `Impacts_1000_WithPierce_DoNotAllocate`.
- Critério de pronto: os testes acima verdes pelo filtro `\.ProjectilePierceTests\.`, e os `Projectile_*` de `AbilityWaitTests`, `SpawnActionsTests`, `EffectContextTests` e `TargetSelectorTests` verdes sem edição.
- Tamanho: ~450 linhas e 3 arquivos, pela ADR 0042 (medido no diff: 450 linhas). Exceção: nenhuma.
- Commit: `feat(gas): perfuração do projétil e ASC no pai`

### TK3 — Projétil: "Ao terminar" com motivo e filtro de impacto
- Escopo: `Projectile.cs` (`OnEnded`, `End(reason)`, `ImpactFilter`), `Runtime/GAS/Actions/ActionEndReason.cs` (novo), `ActionContext.cs` (`EndReason`), `Tests/GAS/PlayMode/ProjectileEndTests.cs`, `ProjectileImpactFilterTests.cs` (novos), `gas.md`, `CHANGELOG.md`.
- Testes red:
  - `End_RunsOnce_WithReason_HitLifetimeObstacleReleased` (CA19, C12, C13);
  - `End_WithoutActions_SameAsToday`;
  - `ImpactFilter_Fails_PassesThrough_NoImpactNoPierceSpentNoEnd` (CA20, C14, com `TagQueryCondition` sujeito `TargetUnit`);
  - `End_RunsOnCasterAsc_AtCurrentPosition`.
- Critério de pronto: CA19 e CA20 verdes.
- Tamanho estimado: ~420 linhas; 7 arquivos. Depende de: TK2, EV8 (com a emenda da ADR 0035).

### Detalhe (2026-11-03)
- CA: CA19 e CA20 (C12, C13, C14). Design 2 e 3: `OnEnded`, `End(reason)` e `ImpactFilter`. Validação do projétil fica no TK7.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/GAS/Actions/ActionEndReason.cs` (novo): `public enum ActionEndReason { None, Hit, Lifetime, Obstacle, Released, Expired, OwnerGone, ExecutionEnded }`.
  - `Runtime/GAS/Actions/ActionContext.cs`: `public ActionEndReason EndReason { get; internal set; }` (depois de `:81`), zerado no `Reset` (`:149-174`); o `CreateDerived` não o copia.
  - `Runtime/GAS/Abilities/Projectiles/Projectile.cs`:
    - campos `[SerializeReference, TypePicker] Condition<ConditionContext> ImpactFilter` e `[SerializeReference, TypePicker, ActionEvent("Ao terminar", DefaultTarget.EventTargets, false)] List<GameplayAction> OnEnded` (perto de `OnImpact`, `:68-70`);
    - `Setup(spawner, parentContext, …)` (`:135-144`) guarda o modelo de contexto sempre que há `parentContext`, não só com impacto: o término e o filtro também leem habilidade, nível e instigador;
    - `FixedUpdate` (`:194-199`): fim do `Lifetime` chama `End(Lifetime)`;
    - `ReturnToPool()` (`:316-329`) vira `End(Released)`; o corpo de hoje passa a `protected virtual void End(ActionEndReason reason)`, que antes de devolver roda `OnEnded` uma vez no ASC de quem lançou (com `Source` ativo), com `EndReason` e `Point` = posição atual, sem alvo e sem dono; sem ações, o caminho de hoje (C16);
    - `HandleCollision` (`:341-354`): depois do dono e do repetido, o filtro de candidato com `Owner` = `Source`, `Target` = `AtUnit(ASC)`, habilidade e nível do modelo; falha ignora o choque (C14);
    - `HandleImpact` (`:383-405`): termina com `End(Hit)` por perfuração e `End(Obstacle)` sem ASC. O colisor sem ASC continua rodando "No impacto" sem alvo antes de terminar, como hoje (C16, e o `NoNewFields_BehavesAsToday` do TK2 sem edição).
  - `Tests/GAS/PlayMode/ProjectileEndTests.cs` e `ProjectileImpactFilterTests.cs` (novos).
  - Sem `gas.md` nem `CHANGELOG.md`: manual só no passe de documentação (emenda de 2026-11-03 da 0007), e o ticket só acrescenta (a seção "Quebras" já lista o término por `Lifetime` com `OnEnded` como correção).
- Testes red (PlayMode, `SpellBook.GAS.Tests.PlayMode`):
  - `ProjectileEndTests`: `End_RunsOnce_WithReason_HitLifetimeObstacleReleased`; `End_WithoutActions_SameAsToday`; `End_RunsOnCasterAsc_AtCurrentPosition`; `EndReason_IsNone_InImpact`.
  - `ProjectileImpactFilterTests`: `ImpactFilter_Fails_PassesThrough_NoImpactNoPierceSpentNoEnd` (`TagQueryCondition` com sujeito `TargetUnit`); `ImpactFilter_OwnerIsCaster_WithAbilityLevel`; `ImpactFilter_Passing_OwnerStillIgnoredWithoutDamageOwner`.
- Critério de pronto: os testes acima verdes pelo filtro `\.ProjectileEndTests\.;\.ProjectileImpactFilterTests\.`, e `ProjectilePierceTests`, os `Projectile_*` de `AbilityWaitTests`, `SpawnActionsTests`, `EffectContextTests` e `TargetSelectorTests` verdes sem edição.
- Tamanho: ~510 linhas e 5 arquivos, pela ADR 0042 (medido no diff: 512 linhas, 403 delas nos dois testes). Exceção: até +30% (+2%), porque cada classe de teste traz os próprios auxiliares (projétil que expõe o choque, ações e condição de registro), como o `ProjectilePierceTests`, sem tocar no `GasFactory`.
- Commit: `feat(gas): término com motivo e filtro de impacto do projétil`

### TK4 — `GameplayThinker`: vida, pulsos e término
- Escopo: `Runtime/GAS/Thinkers/GameplayThinker.cs`, `ThinkerBehaviour.cs`, `ThinkerLifeAction.cs` (novos), `Tests/GAS/PlayMode/ThinkerTests.cs` (novo), `Tests/Testing/GasFactory.cs` (`CreateThinkerPrefab`), `Runtime/Core/DocUrls.cs` (`Thinkers = Base + "modulos/thinkers-e-auras/"`), `Documentation~/manual/modulos/thinkers-e-auras.md` (nova), `CHANGELOG.md`.
- Testes red:
  - `Created_PulsesAt1To5_EndedOnce_ThenReturnsToPool` (CA1, C1);
  - `Pulse_TickInSameFrameAsPulseEnd_Counts` (CA9);
  - `SameAction_InPulseAndAbilityActivate` (CA2, C2);
  - `End_CancelsRunningAndDelayed_TwiceDoesNotRepeat` (CA6);
  - `FromPool_StartsClean` (CA8);
  - `ScenePlaced_NoCreator_RunsWithOwnAsc` (P2);
  - `PulseOverlap_LongPulseDoesNotDelayNext`;
  - `ThinkerSources_NoCoroutineTimeOrInstantiate` (CA26, guarda textual).
- Critério de pronto: CA1, CA2, CA6, CA8 e CA9 verdes; `HelpUrlTests` e `ManualLinksTests` verdes.
- Tamanho estimado: ~480 linhas; 8 arquivos (1 página). Depende de: TK1.

### Detalhe (2026-11-03)
- CA: CA1, CA2, CA6, CA8, CA9 e a parte do thinker do CA26 (C1, C2, P2). Design 4 (ciclo, `End`, thinker na cena) e ADR 0036, itens 1 a 3. Criador, `followOwner`, `endWithExecution` e a vigia ficam no TK5; a aura no TK6; o `Validate` no TK7.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/GAS/Thinkers/GameplayThinker.cs` (novo): `[RequireComponent(typeof(AbilitySystemComponent))]`, `[HelpURL(DocUrls.Thinkers + "#gameplaythinker")]`; `ScalableFloat duration` e `pulseInterval`, `[SerializeReference, TypePicker] ThinkerBehaviour behaviour` e as listas `onCreated`, `onPulse` e `onEnded` com `[ActionEvent(..., DefaultTarget.Owner, true)]` (o ASC do thinker entra nos alvos do evento, como o dono nas listas de efeito, porque `ApplyEffectAction` e `PlayCueAction` leem `EventTargets`); `Begin(AbilitySystemComponent creator, ActionContext creationContext, SpawnerAsset spawner)` copia instigador (o criador), habilidade, spec, nível e `EffectContext`, avalia duração e intervalo no nível e na tabela da habilidade e roda a vida no ASC do thinker com dono lógico = o thinker; `Start` chama `Begin(null, null, null)` se ninguém chamou; `End(ActionEndReason)` uma vez (cancela as execuções do thinker, roda `onEnded` com `EndReason`, chama `behaviour.OnEnd`, cancela o que `onEnded` deixou pendente e volta por `SpawnerAsset.OrDefault(spawner)`); `OnEnable` zera o estado do ciclo; `OnDisable` sem `End` cancela as execuções do thinker.
  - `Runtime/GAS/Thinkers/ThinkerBehaviour.cs` (novo): `[Serializable] abstract`, com `CreateState`, `OnBegin`, `OnPulse`, `OnEnd` e `Validate` virtuais. O estado é criado uma vez no `Awake` do thinker.
  - `Runtime/GAS/Thinkers/ThinkerLifeAction.cs` (novo, `internal sealed`, fora do catálogo): fase 0 chama `behaviour.OnBegin` e roda `onCreated`; depois espera com `ActionStatus.Wait` até o próximo marco (pulso ou fim). No pulso chama `behaviour.OnPulse` e roda `onPulse` como execução própria; no mesmo instante, o pulso vem antes do fim (`End(Expired)`). Sem duração e sem intervalo, `WaitUntilCancelled`.
  - `Runtime/Core/DocUrls.cs:23`: `Thinkers = Base + "modulos/thinkers-e-auras/"`.
  - `Tests/Testing/GasFactory.cs`: `CreateThinkerPrefab(duration, pulseInterval, configure)` cria um thinker inativo (modelo de prefab) com `autoTick` desligado.
  - `Tests/GAS/PlayMode/ThinkerTests.cs` (novo).
  - `Tests/GAS/EditMode/GasSourceScanTests.cs`: guarda textual do CA26 em `Runtime/GAS/Thinkers`.
  - `Editor/GAS/GasEditors.cs:35`: `GameplayThinkerEditor : SpellBookEditor`, como o `ProjectileEditor` (consequência direta, ADR 0042 item 6: a guarda `SpellBookEditorCoverageTests` cobre todo `MonoBehaviour` público concreto).
  - Sem página `thinkers-e-auras.md` nem `CHANGELOG.md`: manual só no passe de documentação (emenda de 2026-11-03 da 0007), e o ticket só acrescenta. As guardas `Documentacao` da URL nova ficam para o passe.
  - O vínculo `FollowsOwner` não entra: as execuções do thinker são `Holds` com dono = o thinker. A lacuna do TK1 (herança do vínculo pelas filhas em paralelo e pelo `DelayedAction`) fica para quem usar esse caminho.
- Testes red:
  - PlayMode, `SpellBook.GAS.Tests.PlayMode`, classe `ThinkerTests`: `Created_PulsesAt1To5_EndedOnce_ThenReturnsToPool`; `Pulse_TickInSameFrameAsPulseEnd_Counts`; `SameAction_InPulseAndAbilityActivate`; `End_CancelsRunningAndDelayed_TwiceDoesNotRepeat`; `FromPool_StartsClean`; `ScenePlaced_NoCreator_RunsWithOwnAsc`; `PulseOverlap_LongPulseDoesNotDelayNext`.
  - EditMode, `SpellBook.GAS.Tests.EditMode`, classe `GasSourceScanTests`: `ThinkerSources_NoCoroutineTimeOrInstantiate`.
- Critério de pronto: os testes acima verdes pelo filtro `\.ThinkerTests\.` (PlayMode) e `\.GasSourceScanTests\.` (EditMode), e `HelpUrlTests`, `SpellBookEditorCoverageTests` e `SourceRulesTests` verdes (EditMode). `ManualLinksTests` é da categoria `Documentacao` e roda no passe.
- Tamanho: ~645 linhas e 8 arquivos, pela ADR 0042 (medido no diff: 644 linhas, 309 delas no `ThinkerTests`, e 5 no `GasEditors.cs`, que entrou na revisão). Exceção: até +30% (+29%), porque os sete testes PlayMode trazem os próprios auxiliares (spawner de uma vaga, comportamento e ações de registro), e o código novo do thinker (~290 linhas) é o mínimo do Design 4 sem criador nem aura.
- Commit: `feat(gas): GameplayThinker com vida, pulsos e término`

### TK5 — `CreateThinkerAction`, criador, seguir e terminar com a execução
- Escopo: `Runtime/GAS/Thinkers/CreateThinkerAction.cs`, `ThinkerWatchAction.cs` (novos), `GameplayThinker.cs` (`Begin` com criador, `followOwner`, `OwnerGone`), `ActionContext.cs` (`LastCreatedThinker`), `Tests/GAS/PlayMode/CreateThinkerActionTests.cs` (novo), `thinkers-e-auras.md`, `CHANGELOG.md`.
- Testes red:
  - `Create_BySpawner_WithCreatorAbilityLevelContext_ReturnsThinker` (CA3, C3);
  - `TimeSource_InheritedFromCreator`;
  - `FollowOwner_Tracks_CreatorDisabledOrDestroyed_EndsOwnerGone` (CA4, C4);
  - `EndWithExecution_EffectRemoved_AbilityCancelled_EndsExecutionEnded` (CA5, C5);
  - `NoFlags_OutlivesCreator`;
  - `SpawnerReturnsNull_SkipsWithWarning` (CA7, C17).
- Critério de pronto: CA3, CA4, CA5 e CA7 verdes.
- Tamanho estimado: ~430 linhas; 7 arquivos. Depende de: TK4.

### TK6 — Aura
- Escopo: `Runtime/GAS/Thinkers/AuraBehaviour.cs` (novo), `GameplayThinker.cs` (ganchos do comportamento), `Tests/GAS/PlayMode/AuraTests.cs` (novo), `thinkers-e-auras.md`, `CHANGELOG.md`.
- Testes red:
  - `Enter_Applies_InsideAtBirth_Applies_Exit_RemovesOwnInstance` (CA10, C6);
  - `End_RemovesFromAll_OtherSourceSameEffectKept` (CA11, C7);
  - `Purged_ReappliedNextPulseIfInside`;
  - `Filter_TagOnTarget_IncludeOwner_Max_Order_StopsMatchingLoses` (CA12, C8);
  - `RadiusAndInterval_ByLevel_NoRecreate` (CA13, C9);
  - `TwoAurasSameAsset_NoSharedState` (CA15);
  - `PulseActionCreatesDestroysThinkers_RemovesAuraEffect_NoCorruption` (CA28).
- Critério de pronto: CA10 a CA13, CA15 e CA28 verdes.
- Tamanho estimado: ~480 linhas; 5 arquivos. Depende de: TK5, EV8 (emenda 0035).

### TK7 — Validação e Gizmo
- Escopo: `GameplayThinker.cs`, `AuraBehaviour.cs`, `Projectile.cs`, `CreateThinkerAction.cs` (`Validate`; Gizmo no thinker), `Tests/GAS/EditMode/ThinkerValidationTests.cs` (novo), `Documentation~/manual/ferramentas/validador.md`, `CHANGELOG.md`.
- Testes red: um por regra do Design 7 (CA30), com `propertyPath`; `CreateThinker_InInstantAbility_WithEndWithExecution_Warns`.
- Critério de pronto: CA30 verde.
- Tamanho estimado: ~400 linhas; 7 arquivos. Depende de: TK6.

### TK8 — Inspector do thinker, receitas, desempenho e documentação
- Escopo: `Editor/GAS/Thinkers/GameplayThinkerEditor.cs` (novo), `Tests/GAS/PlayMode/ThinkerRecipesTests.cs`, `ThinkerAllocationTests.cs` (novos), `Tests/GAS.Bridges/PlayMode/AuraTeamFilterTests.cs` (novo; aura e projétil com `TeamRelationCondition`), `thinkers-e-auras.md` (três receitas), `gas.md` (projétil: perfuração, término, filtro), `migracao-2-para-3.md` (projétil), `CHANGELOG.md`.
- Testes red:
  - `Recipe_AuraOnUnit_EffectCreatesFollowingThinker` (CA16, R12);
  - `Recipe_PulseRenewal_ForEachApplyRenews` (CA16, R13);
  - `Aura1000Pulses20Targets_Projectile1000Impacts_DoNotAllocate` (CA27, C18);
  - `AuraAndProjectile_TeamFilter_OnlyAllies_OnlyHostiles` (CA14, com Teams);
  - `GasSuite_PassesWithoutEdits` (CA32, CA25 pela guarda de assemblies).
- Critério de pronto: os testes verdes; CA29 (S) conferido pelo Examinador no host (abas, resumo, seletor inline, cabeçalho da aura e Gizmo); CA31 (E): páginas com as receitas, `[HelpURL]`, CHANGELOG com o R25.
- Tamanho estimado: ~460 linhas; 8 arquivos. Depende de: TK7, TM5, EV17.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| TK1 | sim | EV21 | ~320 / 7 |
| TK2 | sim | TK1 | ~430 / 5 |
| TK3 | sim | TK2, EV8 (emenda 0035) | ~420 / 7 |
| TK4 | sim | TK1 | ~480 / 8 |
| TK5 | sim | TK4 | ~430 / 7 |
| TK6 | sim | TK5, EV8 (emenda 0035) | ~480 / 5 |
| TK7 | sim | TK6 | ~400 / 7 |
| TK8 | sim | TK7, TM5, EV17 | ~460 / 8 |

Fila: TK1 → TK2 → TK3 → TK4 → … → TK8 em série. TK1 vem antes do CU8. CA25, CA26 e CA32 rodam em todos.

## Perguntas ao usuário

- **Q1. `EndWithExecution` em habilidade instantânea.** Proposta: "termina com a execução" quer dizer que o thinker termina quando o dono termina ou é cancelado, numa regra única (ADR 0036). Com isso, numa habilidade `Instant` o thinker termina quando a ativação termina. Para a zona que sobrevive ao lançamento, o designer desliga `EndWithExecution`, e o validador avisa. Alternativa: só o cancelamento da habilidade termina o thinker (como o C5 descreve). O fim normal de uma canalização ou de um toggle não terminaria o thinker, e o vínculo teria duas regras.
- **Q2. Todo thinker tem um ASC próprio (ADR 0036).** Proposta: o prefab do thinker leva um ASC, que roda as ações e o tempo dele. Assim o thinker colocado na cena e o thinker que sobrevive a quem o criou funcionam com o mesmo executor. Quem o criou vira o instigador. Alternativa: rodar as ações no ASC de quem o criou. Seria mais leve, mas o thinker morre com o criador, e o thinker da cena precisa de um relógio e de um executor à parte.
- **Q3. Filtro de candidato único (ADR 0035)**, a mesma pergunta Q2 da Spec do B-011: o filtro de impacto e o da aura são a `Condition<ConditionContext>` do seletor de área.
- As ADRs 0035 e 0036 passam pelo gate junto com esta Spec.
