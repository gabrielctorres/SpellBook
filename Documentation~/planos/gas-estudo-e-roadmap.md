# Estudo do GAS do SpellBook e plano de evolução

> Status: estudo e planejamento, nada implementado. Data: 2026-10-29.
> Autor: Arquiteto (somente leitura). Próximo passo: o usuário responde as decisões da seção G e,
> depois, cada fase do roadmap vira issue (`/issue`) → PRD (Criativo) → Spec + ADR (Arquiteto).
>
> Referências: Unreal Gameplay Ability System e Dota 2 Workshop Tools, Abilities Data Driven
> (https://developer.valvesoftware.com/wiki/Dota_2_Workshop_Tools/Scripting/Abilities_Data_Driven).

## Três problemas que vêm antes de tudo

- **Não existe nenhum teste no pacote.** Não há pasta `Tests/` nem `.asmdef` de testes. Isso precisa vir antes de qualquer épico.
- **O pacote só compila com Odin Inspector instalado.** O runtime usa `Sirenix.OdinInspector` em 8 arquivos, mas nem `package.json` nem `Runtime/SpellBook.Core.asmdef` declaram essa dependência. Um jogo sem Odin não compila o SpellBook.
- Não existe `AGENTS.md` nem a pasta `Documentation~/ds`.

---

## A) Como o GAS funciona hoje

### Tipos principais

| Área | Tipo | Arquivo |
|---|---|---|
| Núcleo | `AbilitySystemComponent` (MonoBehaviour; tags com contagem de referência, efeitos ativos, specs) | `Runtime/GAS/Core/AbilitySystemComponent.cs` |
| Atributos | `AttributeDefinition` (SO: Icon, DisplayName, Type Primary/Derived, Formula, MinValue/MaxValue) | `Runtime/GAS/Attributes/AttributeDefinition.cs:13-44` |
| | `AttributeSet` (MonoBehaviour, `startingAttributes`, `TryGetState`, `GetValue`) | `Runtime/GAS/Attributes/AttributeSet.cs` |
| | `AttributeState` (Base, Current, lista de modificadores, `OnValueChanged`) | `Runtime/GAS/Attributes/AttributeState.cs` |
| | `AttributeModifier` + `ModifierOperation {Add, Multiply, Override}` | `Runtime/GAS/Attributes/AttributeModifier.cs` |
| | `ModifierValueSource {Constant, Attribute, Formula}` | `Runtime/GAS/Attributes/ModifierValueSource.cs` |
| | `Formula` (SO abstrato), `SimpleAttributeFormula`, `ExpressionFormula` (usa `DataTable.Compute`) | `Runtime/GAS/Attributes/Formulas/*` |
| Efeitos | `GameplayEffect` (SO: DurationPolicy, Duration, Period, Modifiers, PeriodicModifiers, OnApplication/OnTickActions/OnRemoval, tags, stacking) | `Runtime/GAS/Effects/GameplayEffect.cs` |
| | `ActiveEffectInstance` (TimeRemaining, CurrentStacks, `AddStack`, `Update(dt)` público, `Cleanup`) | `Runtime/GAS/Effects/ActiveEffectInstance.cs` |
| | `EffectAction` (síncrona: `Execute(source, target, instance)` e `Cleanup`), `DebugLogEffectAction`, `SpawnVFXEffectAction` | `Runtime/GAS/Effects/EffectAction.cs`, `Effects/Actions/CommonEffectActions.cs` |
| Habilidades | `GameplayAbility` (SO: identidade, Cooldown/Cost como GameplayEffect, `CooldownReductionAttribute`, 6 listas de tags, `Actions`) | `Runtime/GAS/Abilities/GameplayAbility.cs` |
| | `AbilitySpec` (Level, IsActive, ActiveContext, rotinas) | `Abilities/AbilitySpec.cs` |
| | `AbilityContext` (Source, TargetPosition, Targets, CustomData, Level, IsCancelled, `CreateDerived`, `StartChildRoutine`) | `Abilities/AbilityContext.cs` |
| | `AbilityAction` (corrotina `Execute`, `OnCancel`, `Run`/`RunSequence`) | `Abilities/AbilityAction.cs` |
| | Ações: `Wait`, `Animation`, `Targeting`, `ApplyEffect`, `PlayCue`, `SpawnProjectile`, `Repeat`, `Parallel`, `Conditional` | `Abilities/Actions/*` |
| | `AbilityActivationResult` (enum com 9 valores) | `Abilities/AbilityActivationResult.cs` |
| | `AbilityDescription` + `DescriptionElement` (`TextElement`, `AttributeElement`, `TemplateElement`; devolvem string com rich text) | `Abilities/AbilityDescription.cs` |
| | `Projectile` (pool, 2D/3D, homing, `OnHitActions`) | `Abilities/Projectiles/Projectile.cs` |
| Targeting | `TargetingStrategy` (SO; physicsMode, includeSelf, required/ignoredTargetTags), `CircleTargeting`, `MouseTargetingStrategy` | `Runtime/GAS/Targeting/*` |
| Tags | `GameplayTag` (struct; `GetDefinition()` é `internal`), `GameplayTagDefinition` (SO com `parent`) | `Runtime/GAS/Tags/*` |
| Infra | `EventManager` estático com struct events, `AbilityActivatedEvent`, `AbilitySlotUpdateEvent` | `Runtime/Systems/Events/*` |
| | `PoolManager` (singleton), `ComponentPooler`, `IPoolable` | `Runtime/Systems/Pooling/*` |

### Fluxo de ponta a ponta

1. **Concessão.** `GiveAbility` cria um `AbilitySpec` (`AbilitySystemComponent.cs:69-79`).
2. **Checagem.** `GetActivationResult` (`:111-126`) testa, nesta ordem: concedida, já ativa, BlockedTags, RequiredTags, bloqueio por habilidade ativa, cooldown, custo. O resultado é um enum fixo e não recebe a posição do alvo.
3. **Commit.** `ActivateSpec` (`:194-225`) aplica o `CooldownEffect` com a duração reduzida por `GetCooldownDuration` (`GameplayAbility.cs:95-106`, piso fixo de 0,1 s e `Clamp01`) e aplica o `CostEffect` antes de qualquer ação rodar. Depois cancela outras habilidades por tag, concede `ActivationOwnedTags`, dispara `OnAbilityActivated` e o `AbilityActivatedEvent` global, e roda `RunSequence(Actions)` numa corrotina do ASC.
4. **Custo.** `CanAffordCost` (`:162-177`) só olha modificadores `Add` negativos contra o `MinValue`.
5. **Ações.** O targeting preenche `context.Targets`, e `ApplyEffectAction` chama `target.ApplyEffect(effect, source)`. O nível do contexto **não chega** ao efeito.
6. **Efeito.** `ApplyEffect` (`:403-428`) verifica requisitos e imunidade e devolve `null` em silêncio quando bloqueia.
   - Instant: roda OnApplication e altera o valor base (`ApplyModifiersToBase`, `:513-536`).
   - Duration/Infinite: cria `ActiveEffectInstance`, concede tags, adiciona `AttributeModifier`s com o valor calculado uma única vez (snapshot), assina o tick e a expiração.
   - Stacking: incrementa `CurrentStacks`, mas **não muda a magnitude**.
7. **Atributo.** `AttributeState.Recalculate` (`:55-94`) faz `(base + ΣAdd) × (1 + ΣMultiply)`, aplica o último Override e limita entre Min/Max **só o CurrentValue**.
8. **Fim.** `EndAbility` e `OnAbilityEnded`. O cancelamento chama `OnCancel` em ordem reversa.

### Pontos de extensão que já existem

- Subclasses com `[SerializeReference]` de `AbilityAction`, `EffectAction` e `DescriptionElement`.
- Subclasses de SO: `Formula` e `TargetingStrategy`.
- Eventos C# no ASC: `OnTagChanged`, `OnAbilityActivated/Ended/Failed`.
- `AttributeState.OnValueChanged`, `ActiveEffectInstance.OnTick/OnEffectExpired`.
- `EventManager`.
- `Projectile` com métodos virtuais.

---

## B) Comparação com o Unreal GAS e o Dota data-driven

**O que já cobre.**

- Tags hierárquicas com contagem de referência.
- Atributos primários e derivados com fórmula.
- Efeitos Instant, Duration e Infinite, periódicos, com tags concedidas, requisitos e imunidade por tag.
- Stacking simples.
- Cooldown e custo como efeitos.
- Bloquear, cancelar e ser cancelado por tag; ativação com motivo de falha.
- Ações compostas em sequência, paralelo, repetição e condição. Isso equivale a "tasks" e ao OnSpellStart.
- Projétil linear ou teleguiado com payload no impacto (OnProjectileHitUnit).
- Busca de alvos em círculo com filtros e `maxTargets`.
- Escape para código: qualquer `AbilityAction` ou `EffectAction` própria, como o RunScript do Dota.
- OnCreated/OnDestroyed/OnIntervalThink equivalem a OnApplication, OnRemoval e OnTickActions.

**O que falta.**

- **Valores por nível.** Não há `AbilityValues`, "10 8 6 4", `%nome`, ScalableFloat, SetByCaller nem MaxLevel. `Spec.Level` existe, mas nada o consome.
- **EffectSpec/EffectContext.** Não há como levar habilidade, nível, ponto de impacto ou magnitudes do chamador até o efeito.
- **Grafo evento → ações → alvos unificado.**
  - Há duas hierarquias de ação incompatíveis: `AbilityAction` (corrotina, contexto) e `EffectAction` (síncrona). Um efeito não roda as ações de habilidade.
  - Não há bloco Target por ação: toda ação usa `context.Targets`.
  - Não há ActOnTargets/ForEach, Random/Chance, DelayedAction sem bloqueio nem RemoveModifier/RemoveEffect como ação.
- **Eventos.** Não existem OnChannel*, OnToggle, OnUpgrade, OnOwnerDied/Spawned, OnProjectileFinish nem gameplay events. No Unreal seriam `SendGameplayEvent` e AbilityTriggers; no Dota, os eventos de modifier OnTakeDamage, OnAttackLanded, OnDeath e OnAbilityExecuted.
- **Comportamentos de habilidade.** Faltam Passive, Toggle, Channelled, Autocast, alcance de cast (CastRange) e ativação com unidade-alvo: `TryActivateAbility` só recebe `Vector3`. Também não há commit separado: no Dota a mana sai depois do cast point; no Unreal existe `CommitAbility`.
- **Thinkers e auras.** O jogo reimplementou como `GroundZone`.
- **States.** Só existem tags; não há semântica padrão de stun, silence ou invulnerable nem consumidores prontos.
- **Times.** Não há relação aliado/inimigo (AbilityUnitTargetTeam); o jogo usa `FactionFilter`.
- **Stacking incompleto.** As cargas não escalam a magnitude; não há agregação por fonte ou por alvo, nem política de expiração por carga.
- **Gameplay Cues.** Só existem `PlayCueAction` e `SpawnVFXEffectAction`, com Instantiate e sem roteamento por tag.
- **Metadados de modifier para UI.** Faltam IsBuff, IsDebuff, IsHidden e IsPurgable, e não há purge por categoria.
- **Execution/damage pipeline.** Falta o equivalente ao ExecutionCalculation, a meta-atributo de dano e a ganchos Pre/PostAttributeChange.
- **Efeitos que concedem habilidades** (itens, buffs).
- **Ferramental.** Não há debugger em runtime nem validação de assets.

---

## C) Avaliação dos 14 itens

Tudo o que está marcado como **(proposta)** não existe no código.

### 1. Metadados de exibição do AttributeDefinition (Alto)

- **Status:** inexistente. Só há `Icon` e `DisplayName` (`AttributeDefinition.cs:18,22`).
- **(proposta)** Um struct serializável `AttributeDisplay` dentro do `AttributeDefinition`, com:
  - `ValueKind {Flat, Percent, PercentPoints, Multiplier, Seconds}`;
  - `Decimals` ou formato, `Prefix` e `Suffix`;
  - `HigherIsBetter`, `SortOrder`, `Hidden` e `Category` (SO `AttributeCategory`);
  - cores opcionais. O padrão bom/ruim viria de um SO global `GasTextTheme`, para não espalhar cores por atributo.
- **(proposta)** API `string AttributeDisplay.Format(float value, ModifierOperation op, IFormatProvider culture)` e `bool IsBeneficial(float delta, ModifierOperation op)`.
- **Risco:** baixo, porque só adiciona campos.
- **Dependências:** é a base dos itens 2, 13 e 14.

### 2. Formatador oficial de efeitos (Alto)

- **Status:** inexistente. O jogo reimplementou em `Rumble.Core.EffectDescriptionFormatter`, com cultura pt-BR fixa e regra própria de "quantidade".
- **(proposta)**
  - `EffectDescriber.Describe(GameplayEffect, in DescribeOptions, List<ModifierLine> results)`, sem alocar.
  - `ModifierLine {Attribute, Label, ValueText, float Value, ModifierOperation Op, bool IsBeneficial, bool IsVariable, Color}`.
  - `DescribeOptions {Stacks, Level, Source ASC, IFormatProvider Culture, IModifierTextFormatter Formatter}`.
  - Interface `IModifierTextFormatter` para o jogo sobrescrever.
  - `EffectModifiersElement : DescriptionElement`.
- **Risco:** `DescriptionElement.GetText(ability, source)` não tem cultura nem nível. Mudar a assinatura quebra elementos customizados. A saída é um overload virtual `GetText(DescriptionContext)` cujo padrão chama o antigo.
- **Dependências:** itens 1 e 8 (semântica da composição). A Fase 3 (valores por nível) também, para valores do tipo `%dano`.

### 3. Acesso e controle de efeitos ativos (Alto, é o mais urgente)

- **Status:** parcial.
  - Já existe: `ApplyEffect` devolve a instância, `RemoveEffect(instance)` é público, e a instância expõe `TimeRemaining` e `CurrentStacks`.
  - `_activeEffects` é privado (`AbilitySystemComponent.cs:20`). O jogo lê esse campo por reflexão em `CooldownTimer.cs` e avança o tempo chamando `instance.Update(step)`.
  - O jogo também mantém um registro paralelo em `StatusEffects.cs` para conseguir buscar e remover por tag.
- **(proposta) No ASC:**
  - `IReadOnlyList<ActiveEffectInstance> ActiveEffects`;
  - `int FindEffects(GameplayTag, List<ActiveEffectInstance>)` e `FindEffects(GameplayEffect, …)`;
  - `int RemoveEffectsWithTag(GameplayTag)`, que devolve as cargas removidas, e `RemoveEffect(GameplayEffect)`;
  - `ReduceCooldown(ability, s)`, `ResetCooldown(ability)` e `RestartCooldown(ability)`.
- **(proposta) Na instância, mutando pelo ASC para manter tags e modificadores consistentes:**
  - `Duration` (o total aplicado), `ExtendDuration`, `ReduceDuration`, `RestartDuration`;
  - `SetStacks` e `RemoveStacks(n)`.
- **(proposta) Eventos:** `OnEffectApplied`, `OnEffectRemoved(instance, EffectRemovalReason)` e `OnEffectStacksChanged(instance, old, new)`.
- **Correções que entram junto:**
  - `RemoveEffect` não é idempotente: remover duas vezes roda `Cleanup` e `OnRemoval` de novo.
  - `RefreshDurationOnStack` usa `Effect.Duration` e ignora o override com CDR (`ActiveEffectInstance.cs:71-74`).
  - `GetCooldownNormalized` divide pela duração do asset, não pela reduzida (`:456-461`).
- **Risco:** baixo. Manter `ActiveEffectInstance.Update` público, marcado como `[Obsolete]`, para não quebrar o jogo.

### 4. Pré-condições de ativação plugáveis (Alto)

- **Status:** inexistente no pacote. O jogo criou o próprio `IAbilityActivationCheck` em `Rumble.Ability.Actions` (`TargetingRequiredAction.cs:14`), que procura nas ações e é chamado pelo handler **antes** do `TryActivateAbility`, porque o pacote faz o commit antes das ações.
- **(proposta)**
  - Lista `[SerializeReference] List<AbilityActivationCondition> ActivationConditions` no `GameplayAbility`. É data-driven e preferível a procurar dentro das ações.
  - Interface `IAbilityActivationCheck { bool CanActivate(in ActivationQuery q, out ActivationFailure f); }`, com `ActivationQuery {ASC, Spec, TargetPosition}`.
  - `ActivationFailure {AbilityActivationResult Result; GameplayTag ReasonTag; string ReasonKey}`. Uso chave ou tag em vez de texto, por causa da localização.
  - Novo valor `BlockedByCondition`, adicionado **no fim** do enum.
  - Overload `GetActivationResult(ability, targetPos)`.
  - Condições prontas: `HasTargetsCondition`, `AttributeThresholdCondition`, `CastRangeCondition` e `TagQueryCondition`.
- **Risco:** mudar a assinatura de `OnAbilityFailed` quebra quem consome. O caminho é um evento novo `OnAbilityBlocked(ability, ActivationFailure)`, mantendo o antigo.
- **Dependências:** nenhuma forte. Há uma decisão de ordem/prioridade entre condições, cooldown e custo (pergunta 12).

### 5. Cargas de habilidade (Alto)

- **Status:** inexistente. O jogo tem `UseChargeAction` e `AbilityCharges`, com relógio paralelo por carga, e copia a conta de CDR com piso de 0,1.
- **(proposta)**
  - `ChargesConfig` no `GameplayAbility`: `Enabled`; `MaxCharges` como `ModifierValueSource` (constante ou atributo); `RechargeTime`; `RechargeMode {Sequential, Parallel}`.
  - Estado no `AbilitySpec`: `CurrentCharges` e timers, com tick pelo ASC.
  - `AbilityActivationResult.NoCharges` (no fim do enum), `OnChargesChanged(spec, old, new)` e `AddCharges` / `RefillCharges`.
  - O CDR usa a política do item 12.
- **Alternativa no estilo Unreal:** cooldown com stacks. É menos legível, e cabe uma ADR.
- **Risco:** baixo, só adiciona.
- **Dependências:** itens 12 e 4 (resultado/falha). É bom ter a Fase 3 (MaxCharges por nível) antes.

### 6. Limites dinâmicos e atributos de recurso (Médio)

- **Status:**
  - Min e Max são constantes.
  - **Bug:** o `BaseValue` não é limitado (`SetBaseValue`, `AttributeState.cs:27-35`; o clamp só existe no `CurrentValue`, `:88`). Um dano grande leva a base de Health abaixo de 0, e as curas seguintes "somem".
  - A regeneração já é possível com um efeito Infinite periódico usando `PeriodicModifiers` Add, mas o ASC não tem `startingEffects`.
  - O jogo sincroniza MaxHealth à mão em `GASHealthSync.cs`.
- **(proposta)**
  - `MaxSource` e `MinSource` (constante ou atributo) no `AttributeDefinition`, com política ao mudar o máximo `{KeepValue, KeepRatio, AddDelta}`.
  - Limitar também a base.
  - Eventos `OnReachedMin` / `OnDepleted` e `OnReachedMax`.
  - `startingEffects` no ASC para passivas e regeneração.
- **Risco:** limitar a base muda comportamento. É uma correção, mas precisa de nota no CHANGELOG.
- **Dependências:** é a base do item 7.

### 7. Absorção / escudo (Médio)

- **Status:** inexistente. O jogo usa `DamageResistance` do TopDown Engine (`Shield.cs`).
- **(proposta)**
  - Ganchos de pré-alteração no `AttributeSet`: uma lista de `AttributeChangeProcessor` (SO ou SerializeReference), com `PreBaseChange(state, ref delta, EffectContext)`.
  - Processador `AbsorbWithAttribute`: o dano em Health consome primeiro o atributo Shield, e `OnDepleted` do Shield vira o evento "quebrou".
  - Opcionalmente, o padrão de meta-atributo IncomingDamage do Unreal.
- **Risco:** médio. O desenho de quem é dono do escudo (base vs modificador de efeito com duração) precisa de ADR.
- **Dependências:** item 6, o EffectContext da Fase 3 e uma decisão sobre dano/Health (pergunta 6).

### 8. Política de composição do modificador (Médio)

- **Status:**
  - Em efeitos com duração, `Multiply` é aditivo, `(1 + Σv)` (`AttributeState.cs:74-79`).
  - Em Instant e tick, compõe sobre a base (`ASC.cs:528-529`). Essa inconsistência é o que o formatador do jogo precisou modelar.
  - `Override` usa o último aplicado, sem prioridade.
- **(proposta)** Acrescentar **no fim** de `ModifierOperation` os valores `MultiplyCompound` e `AddFinal`. Ordem: `(base + ΣAdd) × (1 + ΣMultiply) × Π(1 + MultiplyCompound) + ΣAddFinal`, depois Override.
- **Risco:** baixo, porque o enum é serializado como int e os valores entram no fim.
- **Dependências:** alimenta os itens 2 e 14.

### 9. Eventos globais no barramento (Médio)

- **Status:** parcial. Só existe `AbilityActivatedEvent`, e o GAS chama o `EventManager` direto (`ASC.cs:217`).
- **(proposta)** Structs `EffectAppliedEvent`, `EffectRemovedEvent`, `EffectStacksChangedEvent`, `EffectBlockedEvent`, `AttributeChangedEvent`, `AbilityFailedEvent`, `AbilityEndedEvent` e `TagChangedEvent`.
- Recomendação: o primário são os eventos do ASC por instância, com um componente opcional `GasEventBridge` que repassa ao barramento. Assim quem não quer o barramento global não paga o custo.
- **Dependências:** itens 3 e 10.

### 10. Feedback de imunidade (Médio)

- **Status:** inexistente. `ApplyEffect` devolve `null` tanto quando aplica um Instant quanto quando bloqueia (`ASC.cs:408-415`). O jogo checa a imunidade antes e dispara o próprio `StatusImmuneEvent`.
- **(proposta)**
  - `EffectApplicationResult {Applied, Stacked, AppliedInstant, BlockedByImmunity, MissingRequirements, Invalid}`.
  - `TryApplyEffect(effect, source, out ActiveEffectInstance)`, que devolve o resultado.
  - Evento `OnEffectBlocked(effect, source, result)`.
  - Para bloqueios que não são efeitos (o puxão do jogo): `ReportBlocked(GameplayTag reason)`.
- **Risco:** nulo, porque é API nova. `ApplyEffect` continua igual.

### 11. TryGetValue / GetValueOrDefault (Baixo)

- **Status:** parcial. Há `TryGetState` e `GetValue`, que avisa no console e devolve 0. O jogo criou o `GasStatReader`.
- **(proposta)** `TryGetValue`, `GetValueOrDefault(def, fallback)`, `HasAttribute` e `GetBaseValue` no `AttributeSet`, com atalhos no ASC.
- **Tamanho:** um ticket pequeno.

### 12. Piso e teto da redução de recarga (Baixo)

- **Status:** valores fixos de 0,1 e `Clamp01` (`GameplayAbility.cs:103`), duplicados no jogo.
- **(proposta)** SO compartilhado `CooldownReductionPolicy {MinDuration, MaxReduction, Mode: Percent | Haste}`, com padrão igual ao comportamento de hoje.
- **Dependências:** é usado pelos itens 3 e 5.

### 13. Formato e cultura por atributo (Baixo)

- **Recomendação:** juntar ao item 1. O formato fica por atributo; a cultura é **injetada** (opções ou configuração global), não por atributo.

### 14. Elementos de descrição para custo, recarga e duração (Baixo)

- **Status:** inexistentes.
- **(proposta)** `CostElement`, `CooldownElement` (com CDR do source), `DurationElement` e `ChargesElement`, todos usando o `DescriptionContext`.
- **Dependências:** itens 1, 2 e 5, e a Fase 3 (valores por nível).

---

## D) Lacunas extras fora da lista

### Fundação

1. Não há testes nem `.asmdef` de testes.
2. Não há como injetar o tempo: o ASC usa `Time.deltaTime` (`:56`) e a instância usa `Time.time`. É por isso que o jogo chama `Update` e `Step` à mão nos testes.
3. Odin é uma dependência obrigatória não declarada.
4. Há um único assembly, `SpellBook.Core`, com GAS, eventos e pooling juntos. `Systems/Events/GAS_Events.cs` faz o barramento depender do GAS.
5. Existem três sistemas de eventos duplicados: `Core/Events`, `Core/NewEventSystem` e `Systems/Events`. Há também o erro de grafia `GameEventListner` e um `Math.cs` que é MonoBehaviour embrulhando uma classe estática.

### Correção e desempenho

6. `AttributeState.Recalculate` usa LINQ (alocação) e roda a cada tick.
7. `ExpressionFormula` faz `string.Replace` e `DataTable.Compute` a cada avaliação. Isso aloca, não tem funções (min, max, clamp, level) e um valor negativo substituído gera `--`.
8. `GetCooldownTimeRemaining` usa `List.Contains` exato (`:446`), enquanto `IsOnCooldown` usa match hierárquico.
9. `CanAffordCost` ignora custos com `Multiply`, `Override` ou fórmula que depende do alvo.
10. Os modificadores de efeitos com duração são calculados uma vez (snapshot), e não há opção de valor ao vivo.
11. As cargas não escalam a magnitude dos modificadores. A `StatusStacksFormula` do jogo compensa.
12. Sem `CanStack`, reaplicar cria uma nova instância duplicada. Não há política "renova sem empilhar".
13. `AbilityContext.CreateDerived` não copia `CustomData`; o `ChanceAction` do jogo cita isso.
14. `Projectile.HandleCollision` usa `GetComponent` e não `GetComponentInParent` (o targeting usa o segundo). Também não tem filtro de time, perfuração (pierce) nem `OnProjectileFinish`.
15. `PoolManager.Instance` nulo gera NRE em `SpawnProjectileAction:78` e em `PlayCueAction`.
16. `PlayCueAction` usa `AudioSource.PlayClipAtPoint` e não devolve o VFX ao pool. `SpawnVFXEffectAction` usa Instantiate e Destroy.
17. `EventManager` não é resetado quando o domain reload está desligado (Enter Play Mode Options), então os listeners vazam.
18. `AnimationAction` faz `GetComponentInChildren` a cada execução e usa nome em string. `WaitAction` aloca `WaitForSeconds` e só usa tempo escalado.

### Modelo

19. `GameplayTag.GetDefinition()` é `internal`. Também faltam `GameplayTagContainer` e `GameplayTagQuery` (all/any/none).
20. Não há conceito de time ou facção.
21. Não há ativação com unidade-alvo nem verificação de alcance.
22. Não há gatilho de habilidade por evento nem passivas automáticas ao conceder.
23. Não há serialização de estado (save) do ASC: níveis, efeitos ativos, base dos atributos.
24. Não há atributos criados em runtime: se faltar em `startingAttributes`, o valor é 0 com warning.
25. Não há debugger em runtime para tags, efeitos e atributos, nem `OnValidate` nos assets.

---

## E) Roadmap proposto

Cada épico vira um PRD e uma Spec. Os tickets têm até ~400 linhas e a contagem é estimada.

**Critério de ordem:** primeiro o que o jogo hoje faz por reflexão, depois o que ele reimplementou, depois o modelo genérico Dota/Unreal.

| Fase | Épico | Itens | Tickets |
|---|---|---|---|
| 0 | **Fundação de testes e tempo:** asmdefs `Tests/Editor` e `Tests/Runtime`, factories, `ASC.Tick(dt)` ou `ITimeSource` | — | 2–3 |
| 0 | **Correções do núcleo:** limitar a base, `RemoveEffect` idempotente, refresh com a duração aplicada, cooldown normalizado, match de tag, Recalculate sem LINQ | D6, D8, item 6 (bug) | 2–3 |
| 0 | **ADR Odin opcional** (define `ODIN_INSPECTOR` em `versionDefines` + `#if`) e **ADR do split de assembly** `SpellBook.GAS` | D3, D4 | 2–4 |
| 1 | **Efeitos ativos e cooldown controláveis:** lista pública, Find/Remove por tag, Extend/Reduce/Restart, eventos, `ReduceCooldown`/`ResetCooldown`, política de CDR | 3, 12, 11 | 4–5 |
| 1 | **Resultado de aplicação e eventos:** `TryApplyEffect`, `OnEffectBlocked`, bridge para o barramento | 10, 9 | 2 |
| 2 | **Atributos ricos:** `AttributeDisplay` + Format, MultiplyCompound/AddFinal, Max/Min por atributo com política, OnDepleted/OnReachedMax, `startingEffects` | 1, 13, 8, 6 | 5–6 |
| 3 | **Valores por nível e EffectSpec:** `ScalableFloat` (array por nível ou curva), tabela `AbilityValues` nomeada, `ModifierValueSource` com AbilityValue/Level/SetByCaller, `EffectContext` (instigador, habilidade, nível, ponto), Duration como value source, magnitude por carga, MaxLevel e `OnLevelChanged` | lacuna Dota/Unreal | 5–7 |
| 4 | **Descrição e UI:** `EffectDescriber`, `IModifierTextFormatter`, `DescriptionContext` e cultura, `EffectModifiersElement`, elementos de custo, recarga, duração e cargas, templates `%nome` | 2, 14 | 4–5 |
| 5 | **Ativação avançada:** `ActivationConditions` + `ActivationFailure` + condições prontas, cargas (config, estado, tick, eventos) | 4, 5 | 4–5 |
| 6 | **Dano, escudo e morte:** processadores de pré-alteração, meta-atributo de dano opcional, absorção, OnDepleted como morte, tipos de dano por tag | 7 | 4–5 |
| 7 | **Modelo Dota evento → ações → alvos:** ADR de unificação `AbilityAction`/`EffectAction`; `TargetSelector` por ação (Caster/Targets/Point/Area com times, tags, máximo, aleatório); times e relação; ações Chance, Delayed, ForEachTarget e RemoveEffect; gameplay events (`SendGameplayEvent`) com gatilhos de habilidade e eventos de efeito (OnTakeDamage, OnDeath, OnAbilityExecuted); comportamentos Passive, Toggle e Channel; alcance de cast; ativação com unidade-alvo; commit explícito | lacuna central | 10–14 |
| 8 | **Thinkers, auras, cues e projéteis:** `GameplayThinker` em pool, `CreateThinkerAction`, aura; sistema de Gameplay Cues por tag com pool; projétil com pierce, finish, filtro e InParent | lacunas | 7–9 |
| 9 | **Ferramentas:** janela de debug em runtime, validadores de asset, documentação e exemplos (`Samples~`) | D25 | 3–4 |

**Dependências críticas:**

- A Fase 0 vem antes de todas.
- A Fase 1 vem antes das Fases 2 e 5.
- A Fase 2 vem antes das Fases 4 e 6.
- A Fase 3 vem antes das Fases 4, 5 e 7.
- A ADR de unificação de ações vem antes da Fase 7, que vem antes da Fase 8.

Isso dá 1 a 2 épicos de fundação e entre 55 e 70 tickets no total.

---

## F) Outros sistemas modulares possíveis

| Sistema | Usa o GAS? | Base que já existe no código |
|---|---|---|
| Inventário e itens (equipar concede efeitos e habilidades) | Sim (precisa de "efeito concede habilidade" e handles) | `GiveAbility`/`RemoveAbility`, `ApplyEffect` com instância |
| Progressão, XP e níveis (curvas de nível, pontos) | Sim (Fase 3) | `AbilitySpec.Level` sem uso |
| Loot tables (pesos, raridade, `IRandom` com seed) | Não | Nada. O jogo injeta `System.Random` via `CustomData` |
| Buffs e status (é o próprio GAS, mais metadados de UI IsBuff/IsDebuff/IsHidden e purge) | Sim | `GameplayEffect` e tags |
| Times e facções (relação aliado/inimigo) | Sim (targeting) | Nada. O jogo tem `FactionFilter` |
| Health e dano neutro de engine | Sim (Fase 6) | Nada. O jogo usa o TopDown Engine |
| Save/Load (`ISaveable`, estado do ASC) | Sim | Nada |
| IA (utility/BT usando `CanActivate` e as `TargetingStrategy`) | Sim | `GetActivationResult` e targeting |
| Spawn e waves (SOs de onda) | Indireto | Pooling (`PoolManager`, `PoolConfiguration`) |
| Gameplay Cues, VFX e áudio (cue por tag, AudioSource em pool) | Sim | `PlayCueAction`, `SpawnVFXEffectAction`, pooling |
| Feedbacks (hitstop, câmera, shake) | Opcional | Nada. O jogo tem `HitstopAction` e `CameraShakeAction` |
| Input → slots de habilidade | Sim | `AbilitySlotUpdateEvent` |
| Localização (`ITextProvider` ou pacote Unity Localization) | Usado pela descrição | Nada |
| Tempo e agendador (pausável, escalado, testável) | É base de tudo | Nada. É a Fase 0 |
| Diálogo e quests (condições por tag, eventos no barramento) | Indireto | `EventManager`, SO GameEvents, `RuntimeSet` |

---

## G) Decisões que precisam do usuário

1. ~~**Odin:** obrigatório ou opcional?~~ **Respondida:** Odin é dependência assumida (ver seção H). Falta só confirmar a regra "só atributos no runtime".
2. **Testes:** podemos criar `Tests/Editor` e `Tests/Runtime` dentro do pacote? Qual a versão mínima da Unity (o `package.json` diz 2021.3)?
3. **Versão 3.0.0:** aceita juntar quebras (split de assembly, limitar a base, assinaturas novas) numa major, ou tudo deve ser aditivo na 2.x?
4. **Split** de `SpellBook.Core` em `SpellBook.GAS` e outros: o jogo referencia o assembly e precisaria migrar.
5. **Cargas:** config explícita na habilidade ou cooldown com stacks (estilo Unreal)? A recarga é sequencial (Dota/LoL) ou paralela (como o jogo faz hoje)?
6. **Health e dano:** o SpellBook deve ter um pipeline próprio de dano, escudo e morte, ou ficar só em atributos com ganchos e deixar Health para a engine do jogo?
7. **Times:** o pacote ganha conceito de time e relação, ou continua só com tags?
8. **Ações:** unificar `EffectAction` e `AbilityAction` (o grafo do Dota) ou manter separadas com um adaptador?
9. **Texto:** saída estruturada (linhas) ou string com rich text? A localização usa o pacote Unity Localization (dependência nova) ou uma abstração própria?
10. **Barramento:** o primário são os eventos por ASC com bridge opcional para o `EventManager`, ou o barramento global? E consolidamos os três sistemas de eventos?
11. **Magnitude:** efeitos com duração usam snapshot (como hoje) ou valor ao vivo, configurável por modificador? As cargas devem multiplicar a magnitude? Hoje não multiplicam: isso é bug ou intenção?
12. **Ordem das checagens de ativação:** onde entram as condições plugáveis em relação a cooldown e custo, e qual motivo aparece primeiro na UI?
13. **Valores por nível:** array ("10 8 6 4"), `AnimationCurve` ou os dois?
14. **Item 13:** podemos trocar "cultura por atributo" por "formato por atributo + cultura injetada"?
15. **Multiplayer e predição:** confirma que estão fora de escopo?
16. **Prioridade:** confirma começar pela Fase 0 e depois a Fase 1, que elimina a reflexão em `CooldownTimer` e o registro paralelo em `StatusEffects` do PlaygroundRumble?

---

## H) Modularização, Eventos, UI, ferramentas e documentação

> Discussão em duas rodadas entre dois programadores de gameplay: o A cuidou de módulos e eventos, o B de UI, Inspector e ferramentas.
> Os requisitos novos do usuário foram:
> - o SpellBook também entrega ferramentas;
> - todo sistema de que o GAS precise vira módulo próprio;
> - um sistema de Eventos montado pelo Inspector;
> - UI modular;
> - Odin assumido, para acoplar classes `[SerializeReference]` pelo Inspector;
> - documentação no estilo TopDown Engine.
>
> Tudo nesta seção é **proposta**.

### H.1 Fatos novos encontrados

- **O jogo não usa nenhum dos 3 sistemas de eventos do pacote.** Ele usa o `MMEventManager` do TopDown Engine em 76 arquivos e duplicou os eventos do GAS em `Systems/Ability/AbilityTriggeredEvent.cs`. O único asset legado é `Resources/Events/OnUpdateWeapon.asset`, um `VoidEvent` órfão. Unificar custa quase nada.
- **Maior risco da modularização:** 22 assets do jogo guardam `[SerializeReference]` com `asm: SpellBook.Core`. Os tipos afetados são `TextElement`, `TemplateElement`, `WaitAction`, `TargetingAction`, `SpawnProjectileAction`, `PlayCueAction`, `RepeatAction`, `ConditionalAction`, `ApplyEffectAction` e `SpawnVFXEffectAction`.
  - Toda classe que mudar de assembly ou namespace precisa de `[MovedFrom(true, sourceNamespace, sourceAssembly: "SpellBook.Core")]`. Renomes de campo pedem `[FormerlySerializedAs]`.
  - Os testes carregam **fixtures YAML reais** copiados desses assets.
- **A versão da Unity declarada não bate com a usada.** O `package.json` diz 2021.3, mas o host usa 6000.3.5f2 e o jogo usa 6000.3.13f1.
- **O Odin vem da Asset Store** (`Assets/Plugins/Sirenix`), então não entra no `package.json`. Ele é declarado no README e na doc, e referenciado nos asmdefs.
- O `Editor/SpellBook.Editor.asmdef` referencia o Core pelo GUID `7834e8f3d0902ad45bfb6fab58f7b919`. Manter o `.meta` preserva essa referência.
- `Runtime/GAS/GAS_Documentation.md` está dentro de `Runtime/` e deve ir para `Documentation~/`.
- O pacote não tem nenhum código de UI. O jogo tem um bom padrão para generalizar: `UI/Hud/HudProvider.cs` (SO com `Bind(GameObject) → IDisposable` e `Preview`), `HudPiece`, `HudLayoutView` e as structs de view-model em `UI/Kit/UiViewModels.cs`.
- Contornos do jogo que a UI do pacote deve eliminar:
  - `AbilitySlotProvider.cs` (423 linhas, com `RunPlayer` estático);
  - `TooltipViews.Ability`, que procura `UseChargeAction` e fixa pt-BR;
  - `HudImmuneText` / `FloatingWorldText`, que usam Instantiate/Destroy;
  - `HudLayoutValidator`.

### H.2 Mapa de módulos (consenso)

**Regras de dependência:**
- A infraestrutura nunca depende do GAS.
- As integrações ficam em assemblies de ponte.
- O Core só depende da Unity e dos atributos do Odin.

| asmdef | Conteúdo | Referencia |
|---|---|---|
| `SpellBook.Core` (mantém o GUID) | `Void`, `RuntimeSet<T>`, `RuntimeAnchor<T>` (ex.: "PlayerASC"), `SpawnerAsset` abstrato (cai em Instantiate), base `Condition<TContext>` com And/Or/Not, `TextRoleTheme` (papéis Beneficial/Harmful/Neutral/Value), constante da URL da doc, matemática | Unity, Odin Attributes |
| `SpellBook.Time` | `ITimeSource`, `TimeSourceAsset` (escalado, não escalado, grupo pausável), `ManualTimeSource` para testes, Timer/Scheduler | Core |
| `SpellBook.Events` | `EventChannel` / `EventChannel<T>`, `EventBus<T>`, `EventListener`, `EventEmitter`, registry | Core |
| `SpellBook.Pooling` | `PoolManager`, `ComponentPooler`, `IPoolable`, `PooledSpawnerAsset : SpawnerAsset` | Core |
| `SpellBook.Tags` (namespace `SpellBook.Tags`) | `GameplayTag`, `GameplayTagDefinition`, depois `GameplayTagContainer` / `GameplayTagQuery` | Core |
| `SpellBook.Teams` | `TeamDefinition`, `TeamRelationMatrix`, `ITeamMember` / `TeamMember` | Core |
| `SpellBook.Cues` | `CueDefinition` por tag, roteador, `CueParameters`, spawn via `SpawnerAsset` | Core, Tags, Time |
| `SpellBook.GAS` | ASC, atributos, efeitos, habilidades, ações puras, condições do GAS, formatação e descrição. **Só expõe eventos C#** | Core, Tags, Time |
| `SpellBook.GAS.Bridges` | `AbilitySystemEventRelay`, canais com payload do GAS, `RaiseEventAction`, `PlayCueAction`, `SpawnProjectileAction`, `Projectile`, `SpawnVFXEffectAction`, condições e filtros de time | GAS, Events, Cues, Pooling, Teams |
| `SpellBook.UI` | Só contratos, **sem uGUI**: `IView<TModel>`, `UISource<TModel>` (Bind + `PreviewModel`), `IUIBinding` (`WantsTick`/`Tick`), `ActorResolver`, `UIUpdateDriver` (lê o `ITimeSource`), `UITheme` (referencia o `TextRoleTheme`), modelos `BarModel` / `SlotModel` / `TooltipModel` / `EffectIconModel` / `FloatingTextRequest` | Core, Time |
| `SpellBook.UI.UGUI` | Widgets padrão uGUI + TMP; listas e números flutuantes via `SpawnerAsset` | UI, UGUI/TMP |
| `SpellBook.GAS.UI` | Fontes do GAS: atributo, recarga, cargas, efeitos ativos, tags e descrição | GAS, UI, Tags, Time (**não** Bridges) |
| `SpellBook.UI.Toolkit` (depois) | Os mesmos `IView<T>` como `VisualElement` | UI |
| `*.Editor` por módulo | Inspectors e janelas Odin, validadores, debuggers | runtime correspondente |
| `SpellBook.Setup.Editor` | Avisa com erro claro se o Odin não estiver instalado | — |
| `SpellBook.<Módulo>.Tests` | EditMode / PlayMode | módulo + Time |

Por que `[SerializeReference]` é a costura entre módulos: uma `PlayCueAction` em `Bridges` aparece no seletor do Odin dentro de uma habilidade sem o `GameplayAbility` conhecer Cues. O núcleo só define os pontos de extensão.

**Regra do Odin:**
- No runtime, só atributos (`Sirenix.OdinInspector`). A serialização é nativa da Unity.
- É **proibido** usar `SerializedScriptableObject`, `SerializedMonoBehaviour` e `Sirenix.Serialization` no runtime.
- No Editor, drawers e `OdinEditorWindow` são livres.
- A validação usa `ISelfValidator`, que tem a versão instalada confirmada; ele substitui o `OnValidate`.

### H.3 Sistema de Eventos unificado (consenso)

**Um módulo com três usos e uma regra escrita:**

| Uso | Quando | Tipos |
|---|---|---|
| Canal em asset (Inspector) | Fato global que o designer liga sem código (PlayerDied, RunEnded) | `EventChannel`, `EventChannel<T>`, concretos Int/Float/Bool/String/Vector3/GameObject |
| Barramento de código | Evento global só entre sistemas de código | `EventBus<T> where T : struct, IEvent`, estático por tipo, sem alocação |
| Eventos C# por instância | Estado de UMA entidade (vida, recarga deste ASC) | Os eventos do ASC e do `AttributeState`. **Nunca passam por canal** |

**O canal:**
- Tem `Raise(T)`, `event Raised`, `[Button] Raise` no Play, lista de ouvintes vivos, último payload e `logRaises`.
- A iteração é segura contra reentrada, com `try/catch` por ouvinte em dev.
- Um registry limpo em `SubsystemRegistration` corrige o vazamento com domain reload desligado.

**O ouvinte:**
- Um único `EventListener` com `List<EventResponse>`.
- Cada resposta tem canal, `[SerializeReference] List<Reaction<T>>` tipadas e um `UnityEvent<T>` opcional, recolhido e marcado como "escape".
- O seletor de canal filtra pelo tipo do payload, e um `[InfoBox]` mostra a descrição do canal.
- O validador acusa UnityEvent sem alvo.

**Emissor:** `EventEmitter` (Raise por UnityEvent, botão ou animação), mais `RaiseEventAction` e `RaiseEventEffectAction` em Bridges.

**Como criar um evento:**
1. Tipo básico: Create > SpellBook > Events > Float Channel, depois arrastar no emissor e no ouvinte.
2. Payload próprio: o struct mais duas classes de uma linha (`XEventChannel : EventChannel<X>`, `XEventResponse : EventResponse<X>`). Depois vem um wizard que gera esse código.
3. Só código: `struct X : IEvent` e `EventBus<X>.Raise(x)`.

**Ponte do GAS:** `AbilitySystemEventRelay` em Bridges. É opcional e por ator, com canais e UnityEvents locais.
- Payloads: `AbilityEventData`, `EffectEventData`, `AttributeChangedData` e `TagChangedData`.
- A UI de dados por ator **não** passa pelo relay; ela lê o ASC pela fonte.
- O `SendGameplayEvent` da Fase 7 é semântica do GAS, não do barramento.

**Ferramenta:** Event Debugger (canais, ouvintes, disparos) e "Find references" (quem emite e quem ouve), entregues junto com o módulo.

**Migração:**

| Atual | Destino |
|---|---|
| `Runtime/Core/Events/GameEvent.cs`, `GameEventListner.cs` | Apagar |
| `Runtime/Core/NewEventSystem/**` | Absorvido. `VoidEvent.cs` vira `VoidEventChannel` **mantendo o `.meta`** |
| `Runtime/Systems/Events/EventManager.cs` | Vira `EventBus<T>`, ou fica `[Obsolete]` por uma minor |
| `Systems/Events/GameEvent.cs` / `GameEventListener.cs` (por string) | Apagar |
| `Systems/Events/GAS_Events.cs` | `AbilityActivatedEvent` → relay; `AbilitySlotUpdateEvent` → `OnAbilityGranted/Removed` no ASC |
| `AbilitySystemComponent.cs:217` (`EventManager.Trigger`) | Remover |

**Riscos:**
- Canais globais em excesso escondem o acoplamento. A mitigação é o debugger mais a regra por instância.
- Um asset duplicado por Addressables cria instâncias diferentes do canal.
- A ordem dos ouvintes não é garantida.
- O UnityEvent aloca na primeira chamada.

### H.4 Como o GAS se acopla

- **Tempo:** `TimeSourceAsset` no ASC (nulo cai em `Time.deltaTime`), mais `ASC.Tick(dt)` com `autoTick`. O `ActiveEffectInstance` deixa de ler `Time.time`.
- **Spawn:** projétil, cue e VFX usam `SpawnerAsset` em vez de `PoolManager.Instance`, o que corrige a NRE (D15).
- **Eventos:** só C# por instância. O barramento entra via relay.
- **Condições:** usam a base comum do Core. A ADR define o contexto, porque `AbilityContext` e o `EffectContext` da Fase 3 são diferentes.
- **Tags:** a única dependência de modelo do GAS fora do Core. O rename do namespace quebra 9 `using` no jogo.

### H.5 Sistema de UI modular (consenso)

**Três camadas:** a Fonte (dado), o Binder (escuta, formata e pede tick só enquanto há relógio) e a View (pixels, que recebe uma struct e não conhece o GAS).
- O kit visual do jogo (`UiSlot`, `UiBar`, `UiKitStyle`) implementa `IView<T>` e continua sendo dele.
- Os widgets do pacote servem para protótipo, samples e jogos sem kit.

**Binder:**
- Um por modelo (`BarBinder`, `SlotBinder`, `TooltipBinder`...), cada um com `[SerializeReference] UISource<T>`, `ActorResolver` e a view validada.
- A atualização é **push** sempre que possível. O tick acontece só durante recarga ou duração, num único `UIUpdateDriver`.
- **Dono:** `ActorResolver` do tipo `SelfOrParent`, `FromAnchor` (SO `RuntimeAnchor` mais `AnchorRegistrar` no prefab, que religa no respawn) ou `Explicit`. Nunca singleton nem canal global.
- **Slot:** identificado por tag, com um SO `AbilityLoadout {SlotTag, Ability}`. A ordem da lista é a ordem visual. O rótulo da tecla vem por `IKeyLabelSource`.

**Widgets e fontes:**

| Widget | Fonte | Disponível |
|---|---|---|
| Barra de atributo (rastro atrasado, camada de escudo, texto) | `AttributeSource {Current, Max, Secondary}` | Hoje; o texto bonito vem na Fase 2 |
| Texto de atributo | `AttributeSource` | Hoje / Fase 2 |
| Slot de habilidade (ícone, radial, segundos, cargas, estado, pulso de "pronto") | `AbilitySlotSource` | Recarga: Fase 1. Cargas e motivo de falha: Fase 5 |
| Barra de habilidades (gera slots pelo loadout) | `AbilityLoadoutSource` | Fase 1 (`OnAbilityGranted/Removed`) |
| Tooltip + `TooltipTrigger` | Fontes de habilidade, efeito e atributo → `TooltipModel` | Fase 4 |
| Lista de buffs e ícone de efeito | `ActiveEffectsSource {TagQuery, Category}` | Fase 1 + metadados (Fase 2) |
| Indicador de tag (ex.: atordoado) | `TagSource` | Hoje |
| Números flutuantes (em pool) + `FloatingTextStyle` | Regras de delta de atributo e de bloqueio | Delta: hoje. Imune: Fase 1. Crítico e tipo de dano: Fases 3 e 6 |
| Barra de cast | `ChannelSource` | Fase 7 |

**Dado ou código:**
- São dados: `UITheme`, `TextRoleTheme`, `FloatingTextStyle`, `AbilityLoadout`, `RuntimeAnchor`, prefabs de widget e a escolha de fonte, resolver e filtros.
- É código só o que estende o sistema: novas `UISource<T>`, novas `IView<T>`, `IKeyLabelSource` e `IModifierTextFormatter`.

**Prévia:** em edição, cada fonte tem um `PreviewModel` (o slot aparece com 50% de recarga sem Play). No Play, a prévia pode ser ligada ao ASC selecionado.

### H.6 Fluxo do designer: Fireball do zero até a HUD, sem código

Pressupõe as Fases 3, 4, 5 e 7.

1. **Criar.** `Create > SpellBook > GAS > Ability (template) > Projétil`, com o nome "Fireball". O wizard copia um asset modelo e gera `GA_`/`GE_` (recarga, custo, queimadura) e as tags `Ability.Fireball` / `Cooldown.Fireball`.
2. **Aba Identity.** Ícone, nome e tag, com um seletor em árvore que tem busca e "criar tag aqui".
3. **Aba Values.** Tabela `AbilityValues`, ex.: `damage "50 75 100 125"`, `cooldown "8 7 6 5"`. Um slider de "nível de prévia" alimenta o resto da tela.
4. **Aba Activation.** Recarga `%cooldown`, custo `-%mana` (escolhidos num dropdown de nomes, sem digitar), condições `CastRange` e `HasTarget`, e as cargas.
5. **Aba Events** (estilo Dota):
   - `OnSpellStart`: Animation → SpawnProjectile.
   - `OnProjectileHitUnit`: ApplyEffect → Damage `%damage`.
   - Cada ação recolhida mostra um resumo de uma linha. O alvo é um `TargetSelector` inline.
6. **Aba Description.** Template "Causa {damage}…", com prévia renderizada por nível.
7. **Validação.** Um cabeçalho verde ou vermelho com erros clicáveis.
8. **Conceder.** A habilidade entra no `AbilityLoadout` (`Slot.Q → GA_Fireball`).
9. **Ligar à HUD.**
   - Prefab `SB_AbilitySlot` com `Actor = FromAnchor(Player)` e `Source = AbilitySlotSource(Slot.Q)`.
   - `TooltipTrigger`.
   - Barra de mana com `AttributeSource(Mana, Max = MaxMana)`.
   - No inimigo, `FloatingTextSpawner` com a regra "Health delta < 0 → Dano".
10. **Testar.** No Play, o GAS Debugger mostra recarga, tags, efeitos e o último motivo de falha.

### H.7 Ferramentas de editor

| Prioridade | Ferramenta | Quando |
|---|---|---|
| P0 | `[HelpURL]` em todo componente e SO, apontando para a página da doc | Fase 0 |
| P0 | Validadores (`ISelfValidator`) + menu "SpellBook/Validar projeto" + **teste EditMode que roda todos os validadores** (vale no CI). Generaliza o `HudLayoutValidator` do jogo | Fase 0 |
| P0 | Event Debugger + "Find references" | Junto com o módulo Events (Fase 0-M) |
| P0 | **GAS Debugger v1** (atributos com modificadores por fonte, tags com contagem, efeitos, habilidades, recarga, cargas, último motivo de falha, botões de aplicar/conceder, log) | **Fase 1** (antes estava na 9) |
| P1 | Seletor de tag em árvore; tipos de ação agrupados por `[ActionCategory]` + `[TypeFilter]`; resumo por item | Fase 0 em diante |
| P1 | Wizard de templates de habilidade (Projétil, Área, Buff, Dash, Passiva, Aura) **como assets copiados**; substitui o `Editor/FolderStructureCreator.cs` | Fases 3 e 7 |
| P1 | Prévia de UI e prévia de descrição por nível | UI-1 / Fase 4 |
| P2 | Tabela de balanceamento (habilidades × valores × nível), simulador de efeito com timeline, overlay de debug em build, wizard "Create Event Type" (gera código) | Fase 3 / 9 |

Os mesmos metadados (`[ActionCategory]`, resumo, XML docs) alimentam o seletor do Inspector **e** o catálogo da doc, para os dois nunca divergirem.

### H.8 Documentação no estilo TopDown Engine

**Recomendação (A e B):** **MkDocs com o tema Material**, docs-as-code.
- O conteúdo fica em `Documentation~/manual/`. A Unity ignora a pasta.
- O site é publicado no GitHub Pages por uma Action.
- `package.json.documentationUrl` (hoje `https://example.com/`) e `[HelpURL]` apontam para o site.
- A referência da API em DocFX fica para a Fase 9.
- `prd/`, `specs/`, `adr/` e `TIME.md` continuam internos e não são publicados.
- O mantenedor anunciou o sucessor **Zensical**, compatível com o `mkdocs.yml`. A ADR da Fase 0 verifica o estado: adota o Zensical se ele já ler a estrutura; senão, fica no Material com versão travada.
- **Atenção:** é uma ferramenta Python, só para gerar a doc. Ela é dependência nova de ferramenta e precisa de aprovação.
- O GitBook foi descartado: hospeda fora do repo, usa Markdown proprietário e cria uma segunda verdade, mesmo com Git Sync.

**Árvore de páginas:**

```
Início: O que é · Instalação (UPM + Odin obrigatório + versão Unity) · Primeira habilidade em 10 min ·
        Conceitos (dados vs runtime, SerializeReference + Odin, mapa de módulos) · Glossário · Convenções de nome
Módulos (cada página: O que é / Conceitos / Componentes e assets campo a campo / Como criar X /
         Integração / Armadilhas / API essencial)
  Eventos · Tempo · Pooling · Tags · Times · Cues
  GAS: Visão geral · ASC · Atributos · Efeitos · Habilidades · Eventos e ações · Targeting · Projéteis ·
       Descrições · Receitas (Fireball, DoT, Dash, Aura, Passiva, Escudo)
  UI: Fonte → binder → view · Resolver o dono · um widget por página · Tema · Tooltip · Números flutuantes ·
      Criar sua fonte · Adaptar o kit do jogo · UI Toolkit
Ferramentas: GAS Debugger · Event Debugger · Validador · Templates · Tabela de balanceamento
Guias por gênero: ARPG top-down · MOBA (Dota) · Plataforma · Roguelike
Referência: Componentes · Menus de criação · Catálogo de ações/condições (gerado) · API (DocFX)
Regras: Design System do Inspector (abas Identity/Values/Activation/Events/Description) · Checklist
        "nova ação/efeito/widget" · Compatibilidade · Migração 2.x → 3.0 · Changelog · FAQ
```

**Gate no `/revisao`:**
- Componentes e SOs que o designer usa exigem página.
- Ações e condições aceitam página **ou** entrada no catálogo gerado.
- O validador cobra o `[HelpURL]`.
- As páginas de Regras são linkadas no `CLAUDE.md` e no `Documentation~/ds`.

### H.9 Roadmap revisado

| Fase | Mudança em relação à seção E |
|---|---|
| **0-M (nova, antes da 1)** | ADR "Odin obrigatório + serialização nativa"; ADR do mapa de asmdefs com `[MovedFrom]` e teste com fixtures reais; Unity mínima 6000.0; módulo **Time**; módulo **Events** unificado com Event Debugger (3–4 tickets); `SpawnerAsset`; asmdefs de teste; validadores + teste; `[HelpURL]`; esqueleto do MkDocs; mover `GAS_Documentation.md` |
| 1 | Relay e canais do GAS nascem em `Bridges`; `OnAbilityGranted/Removed`; **GAS Debugger v1** |
| 2 | Somam-se os campos IsBuff/IsDebuff/IsHidden/IsPurgable no `GameplayEffect` (os consumidores ficam na 4) e o `TextRoleTheme` |
| UI-1 (depois da 2) | `SpellBook.UI`, `UI.UGUI`, `GAS.UI`: barra, texto, indicador de tag, números flutuantes |
| UI-2 (depois das 1 e 5) | Slot, barra de habilidades, recarga, cargas, motivo de falha |
| UI-3 (depois da 4) | Tooltip e descrição estruturada |
| UI-4 (depois da 1 + metadados) | Lista de buffs |
| Teams | Módulo independente depois da 0-M; obrigatório antes da 7 |
| Cues | O módulo pode nascer antes; a integração com efeitos continua na 8 |
| 7 | A ADR de unificação de ações inclui o desenho do Inspector (abas, resumos, categorias, `TargetSelector` inline) e a hierarquia única de `Condition` (que substitui o enum de `FlowActions.cs:75`) |
| 9 | Só ferramentas P2, samples e API. A doc é escrita fase a fase |

### H.10 Decisões novas para o usuário

1. **Versão 3.0 com quebras:** Unity mínima 6000.0, split de asmdefs com `[MovedFrom]`, namespace `SpellBook.Tags` e remoção dos 3 sistemas de eventos antigos. Ou só mudanças aditivas na 2.x? Isso amplia a G3.
2. **Regra do Odin:** obrigatório, com "só atributos no runtime, serialização nativa"?
3. **Escopo do pacote:** Teams, Cues e UI viram módulos do SpellBook? O pacote entrega widgets uGUI padrão ou só contratos, fontes e binders? Qual a prioridade do UI Toolkit?
4. **Prioridade:** aceita a Fase 0-M antes da Fase 1? Ela atrasa a remoção da reflexão em `CooldownTimer`.
5. **Documentação:** MkDocs Material ou Zensical no GitHub Pages (aprovar Python como ferramenta)? Site público ou privado? Qual domínio? O gate reprova ou só avisa?
6. **Texto e localização:** descrição estruturada ou rich text? Pacote Unity Localization ou abstração própria? Isso amplia a G9.
7. **Migração do jogo:** o PlaygroundRumble troca o `MMEventManager` e os `HudProvider` pelos módulos do SpellBook? Em qual fase? Ou os dois convivem por uma ponte?

---

## I) Decisões do usuário (2026-10-29)

> A partir daqui, o SpellBook é planejado **sem foco em nenhum jogo**. Todo sistema serve só ao pacote. As menções ao PlaygroundRumble nas seções A a H são histórico e não guiam mais o desenho.

| # | Tema | Decisão |
|---|---|---|
| 1 | Compatibilidade | **3.0 com quebras livres.** Sem `[MovedFrom]` nem fachadas `[Obsolete]`; o CHANGELOG traz um guia de migração curto |
| 2 | Unity mínima | **6000.0** |
| 3 | Odin | Obrigatório. **No runtime, só atributos; a serialização é nativa** (`[SerializeReference]`). Proibidos `SerializedScriptableObject`, `SerializedMonoBehaviour` e `Sirenix.Serialization` no runtime. No Editor, Odin é livre |
| 4 | Issues | No **repositório do SpellBook**: uma issue por ticket, criada só depois do gate |
| 5 | Módulos da 3.0 | Events, Time, Pooling, **Tags** (módulo próprio), **Teams** e **Cues**, além de GAS, GAS.Bridges, UI, UI.UGUI e GAS.UI |
| 6 | UI | **Contratos (`SpellBook.UI`) + widgets uGUI (`SpellBook.UI.UGUI`)**. O UI Toolkit fica para depois |
| 7 | Dano | **Pipeline genérico próprio:** processadores de pré-alteração, meta-atributo de dano opcional, absorção, `OnDepleted` como morte, tipos de dano por tag |
| 8 | Ações | **Hierarquia única** (evento → ações → alvos), usada por habilidades, efeitos, projéteis e thinkers, com seletor de alvo por ação. Exige ADR |
| 9 | Valores por nível | `ScalableFloat` **array ("10 8 6 4") ou curva**, na tabela `AbilityValues` referenciada por `%nome` |
| 10 | Cargas | Configuração própria na habilidade (máximo fixo ou por atributo), **recarga sequencial** |
| 11 | Magnitude | **Configurável por modificador** (snapshot ou ao vivo). As pilhas multiplicam a magnitude por padrão, com opção de desligar |
| 12 | Ordem das checagens | **Tags → cargas/recarga → custo → condições plugáveis.** O primeiro motivo que falhar vai para a UI |
| 13 | Texto e localização | **Linhas estruturadas + `ITextProvider`.** Adaptador opcional para o Unity Localization via `versionDefines`, sem dependência obrigatória |
| 14 | Documentação | **MkDocs Material ou Zensical + GitHub Pages.** Python só como ferramenta da doc; Zensical se estiver estável, senão Material com versão travada |
| 15 | Gate da doc | O `/revisao` **reprova** um ticket sem página (ou sem entrada no catálogo, para ações e condições). O validador cobra o `[HelpURL]` |
| 16 | Pasta dos documentos | Tudo em **`Documentation~/`** (sem `.meta`, fora do pacote), com o índice em `Documentation~/README.md`. Manual público em `Documentation~/manual/` |
| 17 | Processo | Backlog (`Documentation~/backlog.md`, do Controlador) → PRD (`prd/`, Criativo) → Spec (`spec/`, Arquiteto, tickets de ≤ ~400 linhas e ≤ 10 arquivos) → ADRs (`adr/`) → **um único gate** → uma issue por ticket, com links |
| 18 | ADRs | 0001–0008: processo adaptado (Editor único com trava, testes primeiro, um ticket por PR, worktrees...). 0009 em diante: as decisões de arquitetura acima |
| 19 | PRD | Na visão de **quem usa o pacote** (dev e designer no Inspector). Os CAs viram testes ou cenários de Sample; o "playtest" vira validação de uso num projeto host/Sample (seção 9, `/ajustar`); o GDD vira o documento de visão do pacote |
| 20 | Ordem de trabalho | **Um épico por vez**, começando pela Fase 0-M |
| 21 | Time na 0-M | **Completo:** `ITimeSource`, `TimeSourceAsset` (escalado, não escalado, grupo pausável) e `ManualTimeSource` |
| 22 | Ações de spawn na 0-M | **Ficam no GAS** e usam só o `SpawnerAsset` do Core. O `PlayCueAction` fica como está até o módulo Cues. Na 0-M, o Bridges só ganha o `RaiseEventAction` |
| 23 | Lacuna de eventos | **Aceita:** os eventos C# do ASC continuam; o relay vem no B-004 |
| 24 | Versão | **3.0.0-pre.N** por épico; a 3.0.0 final sai quando GAS e UI estiverem completos |
| 25 | Odin ausente | `defineConstraints: ["ODIN_INSPECTOR"]` nos asmdefs que usam Odin, mais um asmdef de Setup sem Odin que mostra um diálogo com o link de instalação |
| 26 | Páginas da doc | **Conteúdo real** desde a 0-M, curto se preciso, sem stubs |
| 27 | Testes | **Assemblies por módulo:** `SpellBook.<Módulo>.Tests.EditMode` / `.PlayMode` |
| 28 | Reações prontas | **Conjunto mínimo:** SetActive, RaiseChannel, Log, Toggle de componente, Invoke com delay |
| 29 | Site | **Público**, em `gabrielctorres.github.io/SpellBook` |
| 30 | CI | Compila e testa na **6000.0 e na última 6000.x** (GameCI) |
| 31 | Math.cs | Vira **só a classe estática** no Core |
| 32 | Host de testes | **Raiz atual:** o manifest ganha `file:` + `testables`; o Odin é importado localmente e `Assets/Plugins/Sirenix` vai para o `.gitignore` |
| 33 | Repo e Odin | **Repo público.** O CI baixa o Odin de um local privado via secret (falta confirmar se a licença do Odin permite) |
| 34 | Worktrees | **Adiados** (ADR 0006 Adiada). Uma branch por ticket no mesmo checkout |
| 35 | Processo | Pasta **`spec/`** (corrigir `TIME.md`, o agente `arquiteto` e o `/implementar`); o **Examinador cria e remove a trava**; o **gate parcial** é permitido; páginas do manual e fixtures **não contam nas ~400 linhas**, mas contam nos 10 arquivos |
| 37 | PlayCueAction (P15) | Fica no GAS e instancia pelo `SpawnerAsset` do Core. O GAS não referencia o Pooling |
| 38 | Delay (P17) | **Events referencia Core + Time.** "Invoke com delay" usa um `TimeSourceAsset` escolhido no Inspector (padrão: escalado), roda uma lista de reações depois do atraso e é cancelado se o ouvinte for desabilitado |
| 39 | RaiseEventAction (P16) | Aceita canais sem payload e `EventChannel<T>` com payload fixo no Inspector. O payload dinâmico vindo do contexto fica para o B-004 |
| 40 | Reação RaiseChannel (P18) | **As duas formas:** repassa o payload recebido para um canal do mesmo tipo ou dispara um canal sem payload, escolhido no Inspector |
| 41 | Host (D1, substitui a 32) | O host de testes fica em **`HostProject~/`**, com `file:../` + `testables`. A raiz continua sendo só o pacote |
| 42 | Remoções | Arquivos apagados por inteiro, sem substituto no ticket, **não contam** no limite de 10 arquivos nem no de linhas |
| 43 | 2.x | **Congela**: não há branch `release/2.x` |
| 44 | Alocação (D3) | Aceita que o `ASC.Tick` com efeito periódico aloque até o B-002 tirar o LINQ do `Recalculate` |
| 36 | Padrões aplicados pelo Controlador (o usuário pode corrigir) | `try/catch` por ouvinte só em desenvolvimento (`DEVELOPMENT_BUILD`/Editor); canais Void, Int, Float, Bool, String, Vector3 e GameObject (sem `Component`); o Find references varre assets e cenas abertas, não cenas fechadas |

## Arquivos citados

- Pacote:
  - `Runtime/GAS/Core/AbilitySystemComponent.cs`
  - `Runtime/GAS/Effects/ActiveEffectInstance.cs`
  - `Runtime/GAS/Effects/GameplayEffect.cs`
  - `Runtime/GAS/Attributes/AttributeState.cs`
  - `Runtime/GAS/Attributes/AttributeDefinition.cs`
  - `Runtime/GAS/Abilities/GameplayAbility.cs`
  - `Runtime/GAS/Abilities/AbilityDescription.cs`
  - `Runtime/SpellBook.Core.asmdef`
- Contornos no jogo (`PlaygroundRumble/Assets/_Project/_Scripts/`):
  - `Systems/GameplayAbility/Runtime/CooldownTimer.cs`
  - `Systems/GameplayAbility/Runtime/StatusEffects.cs`
  - `Systems/GameplayAbility/Runtime/AbilityCharges.cs`
  - `Systems/GameplayAbility/Actions/TargetingRequiredAction.cs`
  - `Systems/GameplayAbility/Actions/UseChargeAction.cs`
  - `Systems/GameplayAbility/Runtime/Shield.cs`
  - `Core/EffectDescriptionFormatter.cs`
  - `Core/Attributes/GasStatReader.cs`
  - `Units/GASHealthSync.cs`
