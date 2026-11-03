# Spec: Correções do núcleo do GAS
PRD: [Documentation~/prd/gas-correcoes-nucleo.md](../prd/gas-correcoes-nucleo.md) · Backlog: B-002 · ADRs: nenhuma nova (usa 0003, 0004, 0007, 0009, 0014)

Status: Aprovada no gate de 2026-10-31 (NC1–NC6); NC7 acrescentado depois do gate, por decisão do usuário (Achado B2-1) · Data: 2026-10-31 · Autor: Arquiteto

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`.

| Requisito | Onde está hoje | O que o código faz |
|---|---|---|
| R1 base limitada | `Runtime/GAS/Attributes/AttributeState.cs:19-25` (construtor), `:27-35` (`SetBaseValue`) | Grava `BaseValue` sem limite; só o `CurrentValue` é limitado (`:88`). |
| R1 aviso único | `Runtime/GAS/Attributes/AttributeSet.cs:26-38` (`Awake`), `:76-81` (`WarnMissing`, padrão de aviso único) | Cria cada `AttributeState` com o `InitialValue` cru. |
| R1 instantâneo e tick | `Runtime/GAS/Core/AbilitySystemComponent.cs:563-586` (`ApplyModifiersToBase`) | Passa por `state.SetBaseValue`, então herda o limite do R1 sem mudança aqui. |
| R3 recálculo | `AttributeState.cs:55-94` (`Recalculate`), `:49-53` (`RemoveModifiersFromSource`) | Três consultas LINQ (`Where`, `Where`, `LastOrDefault`) e um `RemoveAll` com lambda que captura `source`: alocam a cada chamada. |
| R4 tick | `AbilitySystemComponent.cs:103-112` (`Tick`), `:553-557` (`HandleEffectTick`), `ActiveEffectInstance.cs:74-102` (`Update`) | O único ponto que aloca no tick periódico é o `Recalculate` (decisão I44). |
| R5 remoção | `AbilitySystemComponent.cs:595-614` (`RemoveEffect`) | Roda `Cleanup`, devolve tags e modificadores e tira da lista sem checar se a instância ainda está ativa. `RemoveEffect(null)` lança `NullReferenceException`. |
| R6 renovação | `ActiveEffectInstance.cs:61-72` (`AddStack`) | Renova com `Effect.Duration` (asset). A duração da aplicação (`:24-33`) não é guardada. |
| R6a reaplicação | `AbilitySystemComponent.cs:469-476` | Com `CanStack` desligado, cai em `ApplyDurationEffect` e cria uma segunda instância (Achado A1). |
| R7 normalizado | `AbilitySystemComponent.cs:506-511` (`GetCooldownNormalized`) | Divide pelo `cooldownEffect.Duration` do asset. A recarga reduzida vem de `GameplayAbility.cs:75-86` e entra como `durationOverride` em `AbilitySystemComponent.cs:253`. |
| R8 tags na recarga | `AbilitySystemComponent.cs:480-504` (`GetCooldownTimeRemaining`) | Usa `List.Contains` (igualdade exata). `IsOnCooldown` (`:206-211`) e `HasTag` (`:422-430`) já usam `Matches` (hierárquico). |
| R9 contexto derivado | `Runtime/GAS/Abilities/AbilityContext.cs:33-36` (`CreateDerived`) | Cria um contexto com `CustomData` vazio. |
| R10 spawn sem pool | `Tests/GAS/PlayMode/SpawnActionsTests.cs:36` e `:52`; `Tests/Core/EditMode/Structure/SourceRulesTests.cs:56` | **Já coberto** (ver P5 abaixo). |

Testes do GAS existentes que precisam continuar passando sem edição (N3, CA24): `Tests/GAS/PlayMode/AscTimeTests.cs`, `AbilityWaitTests.cs`, `SpawnActionsTests.cs`, `TagCountingTests.cs` e `Tests/GAS/EditMode/GasSourceScanTests.cs`. Nenhum deles aplica duas vezes o mesmo efeito sem `CanStack` nem depende da base passar do limite (lido em `AscTimeTests.cs:49-197`).

## Respostas às perguntas técnicas do PRD (seção 8)

- **P3. Onde guardar a duração aplicada.** Uma propriedade `internal float AppliedDuration { get; private set; }` em `ActiveEffectInstance`, gravada no construtor (`ActiveEffectInstance.cs:24-33`) e atualizada só pelo método interno de renovação (ver Design, 2.2). Não há `InternalsVisibleTo` no GAS, e o ASC está no mesmo assembly, então nada de API pública nova (N4). O B-003 decide o nome público (o PRD do B-003 fala em "duração aplicada", R2) e só troca a visibilidade.
- **P4. Direção do casamento na recarga.** Confirmada: a tag **concedida pelo outro efeito** é a filha. A condição é `concedida.Matches(tagDaRecarga)`, a mesma direção de `HasTag` (`AbilitySystemComponent.cs:427`: `activeTag.Matches(tag)`, a tag ativa casa com a perguntada se for igual ou descendente). No C5, `Cooldown.Fire.Matches(Cooldown)` é verdadeiro e `Cooldown.Matches(Cooldown.Fire)` é falso.
- **P6. Reaplicação sem `CanStack`.** Renova **sempre**, sem olhar `RefreshDurationOnStack` (o flag continua só para o empilhamento). `OnApplication` **não** roda de novo: ele roda uma vez por instância, no construtor (`ActiveEffectInstance.cs:35-42`), e a renovação não cria instância. O contador do período (`_timeSinceLastTick`) não é zerado, para a renovação não adiantar nem atrasar o próximo tique.
- **P5. O R10 já está coberto?** Sim. `SpawnActionsTests.SpawnProjectile_NoSpawner_NoPoolManager_NoException` (`:36`) e `PlayCue_NoSpawner_NoPoolManager_NoException` (`:52`) rodam o `Execute` das duas ações, que é o que o ASC roda na ativação (`AbilitySystemComponent.cs:282`), sem `PoolManager` na cena. A guarda estrutural `SourceRulesTests.NoPoolManagerInstanceOutsidePooling` (`:56`) impede a volta do acesso ao singleton. Nenhum teste novo; o CA21 é atendido por esses três. A Spec cita e não duplica (R10).

## Design

### 1. Atributos (R1 a R4)

**1.1 Limite único.** `AttributeState` ganha um método privado `ClampToLimits(float value)` que devolve `Mathf.Clamp(value, Definition.MinValue, Definition.MaxValue)`. Ele é usado em três lugares, e só neles:
- no construtor, para a base inicial;
- em `SetBaseValue`, antes de gravar (a base de um atributo `Derived` também é limitada; o aviso de sobrescrita continua igual, CA5);
- no fim do `Recalculate`, para o valor atual (substitui o `Mathf.Clamp` de `:88`, mesmo resultado).

Como instantâneos e tiques passam por `SetBaseValue` (`AbilitySystemComponent.cs:576-582`), o C1, o CA4 e o CA29 saem sem mudar o ASC.

**1.2 Aviso único.** No `AttributeSet.Awake`, depois de criar o estado, se o atributo é `Primary` e `state.BaseValue != attr.InitialValue` (o construtor limitou), registra um aviso **uma vez por definição**, num `HashSet<AttributeDefinition>` próprio (mesmo padrão de `_warnedMissing`, `:76-81`). Texto: `[GAS] Valor inicial {InitialValue} de '{def.name}' em '{name}' está fora de [{Min}, {Max}] e foi limitado para {BaseValue}.`, com `this` como contexto. Uma segunda entrada da mesma definição em `startingAttributes` não repete o aviso (CA30). O `AttributeState` em si não escreve no Console, para continuar usável em teste EditMode sem ruído.

**1.3 Recálculo sem alocação.** `Recalculate` vira um único `for` por índice sobre `_modifiers`:
- `Add`: soma no acumulador que **começa na base** (`finalValue += mod.Value`), na ordem da lista;
- `Multiply`: soma em `multiplierSum`, que começa em `1f`, na ordem da lista;
- `Override`: guarda `hasOverride = true` e `overrideValue = mod.Value` (o último vence).

Depois: `finalValue *= multiplierSum`; se `hasOverride`, `finalValue = overrideValue`; por fim `ClampToLimits`. A ordem das somas de ponto flutuante é a mesma de hoje (cada categoria em ordem de lista, a de `Add` começando na base), então os valores são idênticos bit a bit (CA6). O `using System.Linq` sai do arquivo.

`RemoveModifiersFromSource` vira um laço de trás para frente com `RemoveAt` quando `ReferenceEquals(_modifiers[i].Source, source)` (hoje `m.Source == source` entre `object`, que já é igualdade de referência). `AddModifier` e `RemoveModifier` não mudam: `List.Add` só aloca ao crescer, e `List.Remove` de uma classe sem `Equals` próprio não aloca.

**1.4 Tick sem alocação.** Com o 1.3, o caminho `Tick → ActiveEffectInstance.Update → OnTick → HandleEffectTick → ApplyModifiersToBase → SetBaseValue → Recalculate` fica sem alocação para modificadores de valor constante ou por atributo. `ValueSource` do tipo `Formula` continua fora (PRD, seção 6). Isso encerra a exceção da decisão I44.

**Gancho para o B-005:** os limites ficam só em `ClampToLimits`; o B-005 troca o corpo para os limites efetivos, sem caçar outros `Clamp`. As operações novas (`MultiplyCompound`, `AddFinal`) entram como mais casos do mesmo `for`.

### 2. Efeitos ativos (R5, R6, R6a)

**2.1 Remoção idempotente.** `RemoveEffect(ActiveEffectInstance instance)`:
1. `if (instance == null) return;`
2. `if (!_activeEffects.Remove(instance)) return;`: a instância não está ativa **neste** ASC (já removida, de outro ASC ou nunca aplicada); nada roda;
3. só então, na ordem de hoje: `instance.Cleanup()`, desassina `OnEffectExpired` e `OnTick`, devolve as tags e os modificadores.

A única mudança de ordem é tirar da lista **antes** do `Cleanup`. Uma ação de `OnRemoval` que chame `RemoveEffect` na mesma instância (reentrância) cai no passo 2 e não refaz nada. O CA11 sai do mesmo guarda: se a expiração e uma remoção explícita acontecem no mesmo quadro, quem chega primeiro remove e o segundo é ignorado.

`Tick` itera de trás para frente (`:108-111`). Uma remoção dentro do `Update` de uma instância pode tirar **outra** instância da lista e deslocar índices. Hoje isso já é possível; para não pular nem repetir instâncias, o `Tick` passa a checar `if (i >= _activeEffects.Count) continue;` antes de acessar o índice, o mesmo guarda de `CancelAbilitiesWithTags` (`:327`).

**2.2 Duração aplicada e renovação.** Em `ActiveEffectInstance`:
- `internal float AppliedDuration { get; private set; }`, gravada no construtor com `duration`;
- `internal void Renew(float appliedDuration)`: grava `AppliedDuration` e `TimeRemaining` com o valor; não mexe em pilhas, no período nem roda ações;
- `AddStack()` (público, assinatura igual) usa `AppliedDuration` no lugar de `Effect.Duration` quando `RefreshDurationOnStack` (R6, CA12, CA13).

No ASC, o par público `ApplyEffect(effect, source)` / `ApplyEffect(effect, durationOverride, source)` passa a delegar para um método privado único, `ApplyEffectCore(GameplayEffect effect, bool hasOverride, float durationOverride, AbilitySystemComponent source)`. A diferença entre os dois públicos importa só quando já existe instância:

| Situação (efeito com duração ou infinito, instância já ativa neste ASC) | Resultado |
|---|---|
| `CanStack` ligado, sem override | `existing.AddStack()`; renova (se `RefreshDurationOnStack`) com a `AppliedDuration` **da instância** |
| `CanStack` ligado, com override | se `RefreshDurationOnStack`: `existing.Renew(override)` e depois `AddStack()`; senão, só `AddStack()` |
| `CanStack` desligado, sem override | `existing.Renew(existing.AppliedDuration)` (R6a) |
| `CanStack` desligado, com override | `existing.Renew(override)` (R6a: "a duração aplicada na nova aplicação") |

Sem instância ativa, nada muda: cria com o override, ou com `effect.Duration` sem ele. Regra resumida para o manual: **quem passa duração redefine a duração aplicada; quem não passa mantém a da instância.** No C3, a reativação passa pela linha 253 com a duração reduzida (6 s) e volta a 6 s. No CA13, o efeito sem override volta à duração do asset.

Infinito sem `CanStack` reaplicado devolve a instância existente e não cria outra (CA27); `Renew` não tem efeito visível nele, porque `Update` só desconta tempo de `HasDuration` (`ActiveEffectInstance.cs:77`). Na reaplicação, os modificadores **não** são reavaliados nem reaplicados, e as tags não são somadas de novo (CA26). O retorno de `ApplyEffect` é a instância existente.

A busca da instância existente (`_activeEffects.Find(e => e.Effect == effect)`, `:469`) vira um laço por índice, para não alocar o delegate. Não é exigência do R4 (aplicar não é tick), mas evita lixo em reaplicações frequentes.

**Ganchos para o B-003 e o B-004:**
- toda remoção passa pelo passo 3 de 2.1, num ponto só; o evento "removido" do B-003 (R10 dele) entra depois da devolução de tags e modificadores, e a remoção repetida não dispara de novo;
- toda renovação passa por `Renew` ou `AddStack`; os eventos de pilhas e de renovação do B-003 entram ali;
- o `ApplyEffectCore` tem um ramo por desfecho (bloqueado por requisito, bloqueado por imunidade, instantâneo, empilhado, renovado sem pilha, criado). O B-004 troca o retorno por `EffectApplicationResult` sem duplicar a lógica. O ramo "renovado sem pilha" é distinto do "empilhado", o que deixa o B-004 decidir a P3 dele (`Refreshed` ou `Stacked`) sem mexer neste item;
- `AppliedDuration` é o dado que o B-003 expõe (R2 dele) e que o B-007 preenche com `%duracao` (R27 dele). A renovação não reavalia magnitude; ao vivo ou snapshot na renovação é decisão do B-007.

### 3. Recarga (R7, R8)

Um método privado `FindCooldownInstance(GameplayEffect cooldownEffect, out float remaining)` percorre `_activeEffects` por índice e devolve a instância de maior `TimeRemaining` entre:
- as instâncias cujo `Effect == cooldownEffect`;
- as instâncias que concedem alguma tag `g` (em `Effect.GrantedTags`) tal que `g.Matches(c)` para alguma tag `c` de `cooldownEffect.GrantedTags` (P4). Tag inválida não casa (`GameplayTag.Matches` já devolve falso).

Os laços sobre `GrantedTags` são por índice (são `List<GameplayTag>`), sem alocação. Empates ficam com a primeira encontrada.

- `GetCooldownTimeRemaining` devolve `remaining` (0 se nada casa). Mesma assinatura.
- `GetCooldownNormalized` devolve `Mathf.Clamp01(remaining / instance.AppliedDuration)` se há instância e `AppliedDuration > 0`; senão, 0 (CA15). Deixa de ler `cooldownEffect.Duration`.
- `IsOnCooldown` não muda: já usa `HasAnyTag` (hierárquico) e `GetCooldownTimeRemaining`, agora coerentes (C5).

Efeitos infinitos que casam entram como hoje: pelo `TimeRemaining` que têm (o do construtor). `IsOnCooldown` os pega pela tag.

### 4. Contexto derivado (R9)

`CreateDerived` passa a montar `CustomData = CustomData != null ? new Dictionary<string, object>(CustomData, CustomData.Comparer) : new Dictionary<string, object>()`. Cópia rasa: dicionário novo, mesmas referências de valor (P1 do PRD). O comparador é preservado, para um `CustomData` trocado pelo usuário por um dicionário com outro comparador continuar achando as mesmas chaves. `Ability`, `Level` e `Source` continuam herdados; `Spec`, `Targets` e `IsCancelled` continuam novos (CA20).

### 5. O que não muda

- Nenhuma assinatura pública muda e nenhum membro público é criado (N4). Os membros novos são `internal` ou `private`.
- Formato serializado de assets: nenhum campo novo.
- `GameplayAbility.GetCooldownDuration` (piso de 0,1 s e redução limitada a 0..1): fica, é do B-003 (`CooldownReductionPolicy`).
- `CanAffordCost`, `ExpressionFormula`, `Projectile`, `AnimationAction`, `WaitAction`: fora (PRD, seção 6).
- A ordem "expira e depois tique" dentro de `ActiveEffectInstance.Update` (`:76-101`) **muda** no NC7 (Achado B2-1, incluído pelo usuário no gate). Ver a seção 6.

### 6. Tique no quadro da expiração (Achado B2-1, NC7)

Hoje, quando o `Update` de um efeito com duração e período cobre ao mesmo tempo o fim da duração e um tique:
1. a duração expira primeiro, `OnEffectExpired` remove a instância e desassina o `OnTick`;
2. em seguida o bloco do período roda: o `OnTick` não tem mais assinante (os `PeriodicModifiers` **não** são aplicados), mas as `OnTickActions` **rodam** numa instância já limpa.

Com `Duration` múltiplo de `Period` (4 s e 0,5 s, por exemplo), o último tique cai nesse quadro: os modificadores periódicos são aplicados 7 vezes e as ações de tique 8 vezes, a última depois do `Cleanup` e das `OnRemoval`.

Regra nova: **o tique que cabe dentro da duração acontece antes da expiração.** No `Update` de um efeito `HasDuration`:
1. o tempo que conta para o período é `min(deltaTime, TimeRemaining)` (o que sobra do quadro depois do fim da duração não gera tique);
2. o bloco do período roda primeiro, com esse tempo (um tique por `Update`, como hoje);
3. só depois a duração é descontada e, se chegou a zero, o efeito expira.

Para nada rodar numa instância removida, `ActiveEffectInstance` ganha `internal bool IsRemoved`, marcado pelo ASC no passo 3 de 2.1 (antes do `Cleanup`). O `Update` sai logo no início se `IsRemoved`, e sai de novo depois do tique se uma ação ou um ouvinte do tique removeu a instância. Efeitos infinitos não mudam (sem duração, o passo 1 usa o `deltaTime` inteiro).

**A contagem de tiques muda:** um efeito de 4 s com período de 0,5 s passa a aplicar os modificadores periódicos 8 vezes (antes, 7) e roda as ações de tique 8 vezes (como antes), todas antes da remoção. Em geral, com `Duration` múltiplo de `Period`, o efeito ganha o último tique de modificador. Com `Duration` que não é múltiplo, nada muda. É o que a Spec do Sandbox já assume para a `GE_Queimadura` (8 × 5 de dano, `spec/sample-sandbox.md:97`).

## Quebras de compatibilidade

Mudanças de comportamento para o CHANGELOG (seção da pré-release 3.0, decisão I1, sem fachada). Cada ticket escreve a sua linha.

- **Base limitada:** `AttributeState.BaseValue` nunca sai de `[MinValue, MaxValue]`. Quem contava com a "dívida" de vida abaixo de 0 vê as curas valerem na hora. Valor inicial fora do intervalo nasce limitado e gera um aviso no Console.
- **Reaplicar sem `CanStack` renova** a instância existente em vez de criar outra. Quem empilhava "na marra" com `CanStack` desligado passa a ter uma instância só; para empilhar, ligue `CanStack`.
- **Renovação e recarga normalizada pela duração aplicada:** a renovação volta à duração com que o efeito foi aplicado; `GetCooldownNormalized` vai de 1 a 0 também com redução de recarga.
- **Recarga com tags hierárquicas:** `GetCooldownTimeRemaining` passa a contar efeitos que concedem tags filhas das tags da recarga.
- **`CreateDerived` copia o `CustomData`** (cópia rasa).
- **Último tique antes da expiração (NC7):** com `Duration` múltiplo de `Period`, os modificadores periódicos passam a ser aplicados uma vez a mais (4 s / 0,5 s: 8 em vez de 7). As `OnTickActions` deixam de rodar depois da remoção do efeito.
- **`RemoveEffect`** ignora `null` e instâncias que não estão ativas no ASC (antes, `null` lançava exceção e a segunda chamada refazia a limpeza). Entra em "Corrigido".

Migração: nenhuma ação obrigatória. Assets com valor inicial fora do intervalo devem ser revistos pelo aviso.

## Tickets

Todos usam a Unity (testes EditMode ou PlayMode no host) e seguem o WIP 1. Não há par paralelo sem Editor nesta Spec. Cada ticket atualiza a sua parte de `Documentation~/manual/modulos/gas.md` (0007, I15) e acrescenta a sua linha no `CHANGELOG.md`. Pela contagem da 0003, os dois contam no limite de 10 arquivos e não no de linhas (não são `.cs`, `.asmdef`, `.json` nem `.yml`).

Testes de alocação usam `Is.Not.AllocatingGCMemory()` (`UnityEngine.TestTools.Constraints`), como em `AscTimeTests.cs:173`, com aquecimento antes da asserção. Tempo sempre por `ManualTimeSource` ou `asc.Tick` com `autoTick` desligado (N2). Atributos e efeitos de teste saem da `GasFactory` (`Tests/Testing/GasFactory.cs`); os limites são os campos públicos `MinValue`/`MaxValue` do `AttributeDefinition`.

### NC1 — Valor base limitado e aviso único
- Escopo: `Runtime/GAS/Attributes/AttributeState.cs`, `Runtime/GAS/Attributes/AttributeSet.cs`, `Tests/GAS/PlayMode/AttributeLimitTests.cs` (novo), `Documentation~/manual/modulos/gas.md` (seção `#attributedefinition`), `CHANGELOG.md`.
- Testes red:
  - `InstantDamageAboveHealth_ClampsBaseAtMin_ThenHealCounts` (CA1, C1);
  - `SetBaseValue_AboveMax_ClampsToMax` (CA2);
  - `InitialValueOutOfRange_IsClamped` (CA3);
  - `PeriodicDamage_DoesNotTakeBaseBelowMin` (CA4);
  - `PeriodicRegen_StopsAtMax_BaseAndCurrent` (CA29, A2);
  - `InitialValueOutOfRange_LogsOneWarningWithAttributeName`, `DuplicateStartingAttribute_DoesNotRepeatWarning` (CA30, com `LogAssert.Expect` e `LogAssert.NoUnexpectedReceived`);
  - `DerivedAttribute_StillWarnsAndUsesFormula` (CA5; passa antes e depois, é guarda).
- Critério de pronto: CA1 a CA5, CA29 e CA30 verdes; suíte do GAS sem edição de teste antigo (CA24); seção do manual descreve o limite da base e o aviso.
- Tamanho estimado: ~260 linhas; 5 arquivos.
- Depende de: nada.

### NC2 — Recálculo e tick sem alocação
- Escopo: `Runtime/GAS/Attributes/AttributeState.cs`, `Tests/GAS/EditMode/AttributeRecalculateTests.cs` (novo; `AttributeState` com `owner` nulo, atributo `Primary`), `Tests/GAS/PlayMode/AttributeAllocationTests.cs` (novo, teste do tick), `Documentation~/manual/modulos/gas.md` (nota de desempenho em `#attributeset`), `CHANGELOG.md`.
- Testes red:
  - `Recalculate_DoesNotAllocate_AfterWarmup` e `AddRemoveModifier_DoesNotAllocate_AfterWarmup` (CA7; o `AttributeModifier` é criado **antes** da medição e reusado, porque `new` aloca);
  - `RemoveModifiersFromSource_DoesNotAllocate` (CA7);
  - `Tick_InfinitePeriodicEffect_DoesNotAllocate_300Frames` (CA8, C6; efeito infinito, período 0,1 s, `Add` constante, 300 `asc.Tick(1/60f)` dentro do `Assert.That(..., Is.Not.AllocatingGCMemory())`).
  - Guardas numéricas, verdes antes e depois (CA6): só `Add`; `Add` e `Multiply`; dois `Multiply` somados; dois `Override` (o último vence); resultado acima do máximo limitado. Os valores esperados são literais calculados à mão, não lidos do código.
- Critério de pronto: CA6 a CA8 verdes; `AttributeState.cs` sem `using System.Linq`; suíte do GAS sem edição.
- Tamanho estimado: ~280 linhas; 5 arquivos.
- Depende de: NC1 (mesmo arquivo).

### NC3 — Remoção idempotente
- Escopo: `Runtime/GAS/Core/AbilitySystemComponent.cs` (`RemoveEffect`, guarda de índice no `Tick`), `Tests/GAS/PlayMode/EffectRemovalTests.cs` (novo), `Documentation~/manual/modulos/gas.md` (`#gameplayeffect`), `CHANGELOG.md`.
- Testes red:
  - `RemoveTwice_RunsOnRemovalOnce_TagKeptBySecondEffect` (CA9, C2; uma `EffectAction` de teste que conta `Execute`);
  - `RemoveNull_DoesNothing` e `RemoveInstanceFromOtherAsc_DoesNothing` (CA10);
  - `RemoveInSameFrameAsExpiry_CleansUpOnce` (CA11; remoção explícita seguida de `Tick` que expiraria, e o inverso);
  - `OnRemovalThatRemovesSameInstance_DoesNotReenter`.
- Critério de pronto: CA9 a CA11 verdes; suíte do GAS sem edição.
- Tamanho estimado: ~220 linhas; 4 arquivos.
- Depende de: nada (pode vir antes do NC1; a ordem abaixo é só a fila).

### NC4 — Duração aplicada: renovação e reaplicação sem `CanStack`
- Escopo: `Runtime/GAS/Effects/ActiveEffectInstance.cs`, `Runtime/GAS/Core/AbilitySystemComponent.cs` (`ApplyEffect` e `ApplyEffectCore`), `Tests/GAS/PlayMode/EffectRenewalTests.cs` (novo), `Documentation~/manual/modulos/gas.md` (`#gameplayeffect`, empilhamento e renovação, com a regra "quem passa duração redefine"), `CHANGELOG.md`.
- Testes red:
  - `StackRefresh_ReturnsToAppliedDuration_NotAsset` (CA12, C3; asset 8 s, aplicado com 6 s, 4 s depois reaplicado: restam 6 s);
  - `StackRefresh_WithoutOverride_ReturnsToAssetDuration` (CA13; guarda);
  - `ReapplyWithoutCanStack_KeepsSingleInstance_RenewsToAppliedDuration` (CA25);
  - `ReapplyWithoutCanStack_TagCountOne_ModifierOnce_RemoveRestoresAll` (CA26);
  - `ReapplyInfiniteWithoutCanStack_KeepsSingleInstance` (CA27);
  - `ReapplyWithCanStack_StillStacksUpToMax` (CA28; guarda);
  - `ReapplyWithoutCanStack_DoesNotRunOnApplicationAgain` (P6).
  - A contagem de instâncias é lida por `ApplyEffect` devolvendo a mesma referência e pela contagem de tags/modificadores, sem API nova.
- Critério de pronto: CA12, CA13 e CA25 a CA28 verdes; suíte do GAS sem edição.
- Tamanho estimado: ~380 linhas; 5 arquivos.
- Depende de: NC3 (mesmo arquivo do ASC; o CA26 usa a remoção do NC3).

### NC5 — Recarga normalizada e hierárquica
- Escopo: `Runtime/GAS/Core/AbilitySystemComponent.cs` (`FindCooldownInstance`, `GetCooldownTimeRemaining`, `GetCooldownNormalized`), `Tests/GAS/PlayMode/CooldownTests.cs` (novo), `Documentation~/manual/modulos/gas.md` (seção 4, "Ciclo de vida, cooldown e custo"), `CHANGELOG.md`.
- Testes red:
  - `Normalized_WithReduction_StartsAtOne_HalfAtHalf_ZeroWhenReady` (CA14, C4; `CooldownReductionAttribute` com 0,25 sobre 8 s);
  - `Normalized_ZeroAppliedDuration_ReturnsZero` (CA15);
  - `Remaining_CountsChildTagOfCooldownTag_NotUnrelatedTag` (CA16, C5);
  - `Remaining_ParentTagGrantedByOther_DoesNotCount` (P4, direção);
  - `Remaining_TwoMatches_ReturnsLongest` (CA17).
- Critério de pronto: CA14 a CA17 verdes; suíte do GAS sem edição (os casos de `AscTimeTests` que leem `GetCooldownTimeRemaining` continuam iguais).
- Tamanho estimado: ~260 linhas; 4 arquivos.
- Depende de: NC4 (`AppliedDuration`).

### NC6 — `CreateDerived` copia o `CustomData`
- Escopo: `Runtime/GAS/Abilities/AbilityContext.cs`, `Tests/GAS/EditMode/AbilityContextTests.cs` (novo; contexto com `Source` nulo, sem cena), `Documentation~/manual/modulos/gas.md` (`#projectile` ou seção 5, contexto), `CHANGELOG.md`.
- Testes red:
  - `CreateDerived_SeesOriginalKeysAndValues` (CA18, C7);
  - `CreateDerived_AddingKeyInDerived_DoesNotChangeOriginal` e o inverso (CA19);
  - `CreateDerived_SharesValueReferences` (cópia rasa);
  - `CreateDerived_KeepsComparer`;
  - `CreateDerived_KeepsSourceAbilityAndLevel` (CA20; guarda).
- Critério de pronto: CA18 a CA20 verdes; suíte do GAS sem edição.
- Tamanho estimado: ~140 linhas; 4 arquivos.
- Depende de: nada.

### NC7 — Tique dentro da duração acontece antes da expiração
- Origem: Achado B2-1, incluído no B-002 pelo usuário no gate de 2026-10-31. Design na seção 6.
- Escopo: `Runtime/GAS/Effects/ActiveEffectInstance.cs` (`Update`, `IsRemoved`), `Runtime/GAS/Core/AbilitySystemComponent.cs` (marca `IsRemoved` no `RemoveEffect`), `Tests/GAS/PlayMode/EffectPeriodTests.cs` (novo), `Documentation~/manual/modulos/gas.md` (`#gameplayeffect`, parágrafo "Efeitos periódicos": quantos tiques um efeito dá e o tique no fim da duração), `CHANGELOG.md` (linha da seção "Quebras de compatibilidade" acima).
- Testes red (tempo por `asc.Tick` com `autoTick` desligado e valores exatos em binário, 0,5 e 0,25, para não depender de arredondamento):
  - `Duration4_Period0_5_AppliesPeriodicModifier8Times` (hoje 7);
  - `TickActionsAndPeriodicModifiers_RunSameNumberOfTimes` (8 e 8);
  - `TickActions_NeverRunAfterRemoval` (uma ação de tique de teste registra se a `OnRemoval` já rodou);
  - `TickActionRemovesOwnInstance_CleansUpOnce_NoFurtherTick`;
  - `DeltaBeyondExpiry_TicksOnceThenExpires` (um `Tick(10f)` num efeito de 4 s / 0,5 s: um tique e a expiração, como hoje "um tique por `Tick`").
  - Guardas, verdes antes e depois: `Duration4_Period1_5_TickCountUnchanged` (2 tiques, em 1,5 s e 3 s); `InfinitePeriodic_TickCountUnchanged`.
- Critério de pronto: testes verdes; suíte do GAS sem edição de teste antigo (CA24); o manual diz quantos tiques um efeito dá; o CHANGELOG traz a mudança na contagem.
- Tamanho estimado: ~240 linhas; 5 arquivos.
- Depende de: NC4 (mesmo `ActiveEffectInstance.cs`; usa o `RemoveEffect` do NC3).

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| NC1 | sim | — | ~260 / 5 |
| NC2 | sim | NC1 | ~280 / 5 |
| NC3 | sim | — | ~220 / 4 |
| NC4 | sim | NC3 | ~380 / 5 |
| NC5 | sim | NC4 | ~260 / 4 |
| NC6 | sim | — | ~140 / 4 |
| NC7 | sim | NC4 | ~240 / 5 |

Fila proposta: NC1 → NC2 → NC3 → NC4 → NC5 → NC6 → NC7. Todos tocam `gas.md` e `CHANGELOG.md`, então nenhum anda em paralelo com outro ticket que mexa nesses arquivos (RP-06, issue #110, mexe em `gas.md`; o primeiro NC entra depois do merge dele).

CA22 (CHANGELOG) e CA23 (manual) são cumpridos pela soma dos tickets; o Examinador do NC5 confere a página inteira contra R1, R5 a R8.

## Achados

- **B2-1.** O tique no quadro da expiração rodava as `OnTickActions` numa instância já limpa. **Decisão do usuário (gate de 2026-10-31): corrigir neste item**, no NC7 (seção 6).
