# Migração 2.x → 3.0 {#migracao}

A 3.0 tem quebras de compatibilidade. Esta página lista o que muda e o que fazer em cada caso.
A 3.0 ainda está em desenvolvimento, e a lista cresce junto com ela.

## Sem Odin Inspector {#sem-odin}

O pacote não depende mais do Odin Inspector. Ele compila e funciona num projeto sem o Odin.

- **Os assets continuam válidos.** Nenhum campo serializado mudou de nome nem de tipo, então
  habilidades, efeitos e atributos abrem sem perda de dados.
- **Há perdas no Inspector do GAS.** As abas, os grupos, os editores embutidos (custo, recarga,
  fórmula), a pré-visualização da `ExpressionFormula`, o destaque de campos obrigatórios e as
  tabelas e a busca de ações vinham do Odin e não existem mais. O GAS usa o Inspector padrão da
  Unity com `[Header]` e `[Tooltip]`, o `[ShowIf]` e o seletor de tipo do próprio SpellBook. Veja
  [Inspector](../inicio/inspector.md).

## Assembly do GAS {#assembly-do-gas}

O GAS saiu do `SpellBook.Core` e agora é o assembly `SpellBook.GAS`. Um asmdef seu que usa
tipos do GAS precisa referenciar o `SpellBook.GAS`. Scripts fora de asmdef não precisam mudar.
Veja [Conceitos](../inicio/conceitos.md#modulos).

## Tags {#tags}

As tags agora são o módulo [Tags](../modulos/tags.md), no assembly `SpellBook.Tags`. O
namespace saiu do GAS (`SpellBook.GAS.Tags` virou `SpellBook.Tags`) e o assembly saiu do
`SpellBook.Core`.

| Antes (2.x) | Depois (3.0) |
|---|---|
| Namespace `SpellBook.GAS.Tags` | Namespace `SpellBook.Tags` |
| `GameplayTag` e `GameplayTagDefinition` no assembly `SpellBook.Core` | Os mesmos tipos no assembly `SpellBook.Tags` |

- Troque `using SpellBook.GAS.Tags;` por `using SpellBook.Tags;`.
- Um asmdef seu que usa tags precisa referenciar o `SpellBook.Tags`.
- Os assets de tag e os campos de tag continuam válidos; nada muda no Inspector.

## Recarga da habilidade {#recarga-da-habilidade}

O campo **Cooldown Reduction Attribute** da `GameplayAbility` não aceita mais o nome antigo
`_cooldownReductionAttribute`. Na 2.x, um `[FormerlySerializedAs]` lia esse nome antigo e
migrava o valor; na 3.0 esse atributo saiu.

- Assets salvos na 2.x já usam o nome novo e não perdem nada.
- Assets salvos pela última vez na 1.x perdem o valor desse campo. Antes de atualizar, abra o
  projeto na 2.x e regrave essas habilidades, por exemplo com
  `AssetDatabase.ForceReserializeAssets()` num script de Editor. Se já atualizou, preencha o
  campo de novo no Inspector.

## Sistemas de eventos antigos {#eventos-antigos}

A 2.x tinha três sistemas de eventos: os `GameEvent` de `Core/Events`, os canais de
`Core/NewEventSystem` e o `EventManager` de `Systems/Events`. Os três foram removidos, sem
`[Obsolete]`. O módulo `SpellBook.Events` os substitui e está sendo construído nesta
pré-release.

- **Os assets de evento precisam ser recriados.** Um asset de `GameEvent`, `VoidEvent`,
  `IntEvent` etc. perde o script. Crie o canal novo pelo menu `SpellBook/Events/...` e refaça
  as referências nos componentes.
- **Não há canal de `Component`.** Use o `GameObjectEventChannel` ou declare um canal próprio
  (`XEventChannel : EventChannel<X>`).
- **Não há evento por string.** Troque o nome do evento por um canal ou por um tipo de evento
  próprio no `EventBus<T>`.

| Antes (2.x) | Depois (3.0) |
|---|---|
| `SpellBook.Core.Events.GameEvent` (`Raise(Component, object)`) | um canal tipado: `VoidEventChannel`, `IntEventChannel`, `FloatEventChannel` etc. |
| `SpellBook.Core.Events.GameEventListner` | `EventListener` |
| `BaseGameEvent<T>` | `EventChannel<T>` |
| `VoidEvent`, `IntEvent`, `FloatEvent`, `StringEvent` | `VoidEventChannel`, `IntEventChannel`, `FloatEventChannel`, `StringEventChannel` |
| `ComponentEvent` | sem equivalente; `GameObjectEventChannel` ou canal próprio |
| `BaseGameEventListener`, `IGameEventListener<T>`, `VoidListener`, `IntListener`, `FloatListener`, `StringListener`, `ComponentListener` | `EventListener` com uma resposta por canal |
| `UnityVoidEvent`, `UnityIntEvent`, `UnityFloatEvent`, `UnityStringEvent`, `UnityComponentEvent` | removidos; o `UnityEvent<T>` fica no "Escape" da resposta do `EventListener` |
| `SpellBook.Events.EventManager` (`AddListener`, `RemoveListener`, `Trigger`) | `EventBus<T>` (`Subscribe`, `Unsubscribe`, `Raise`) |
| `SpellBook.Events.IEvent` | `SpellBook.Events.IEvent`, no assembly `SpellBook.Events` |
| `SpellBook.Events.GameEvent` e `GameEventListener` (por string) | sem equivalente; canal ou tipo de evento próprio |
| `SpellBook.Core.NewEventSystem.Void` | `SpellBook.Core.Void` (veja abaixo) |

## `Void` e `RuntimeSet<T>` {#void-runtime-set}

Os dois mudaram de namespace e continuam no `SpellBook.Core`. Troque o `using` no seu código. O
nome da classe não mudou, então os assets de um `RuntimeSet<T>` seu continuam válidos. Veja
[Conceitos](../inicio/conceitos.md#core).

| Antes (2.x) | Depois (3.0) |
|---|---|
| `SpellBook.Core.NewEventSystem.Void` | `SpellBook.Core.Void` |
| `SpellBook.Core.RuntimeSets.RuntimeSet<T>` | `SpellBook.Core.RuntimeSet<T>` |

## `MathTools` {#math-tools}

O componente `Math` não existe mais. A `MathTools` saiu de dentro dele e é uma classe estática do
`SpellBook.Core`. Remova o componente `Math` das cenas e chame os métodos direto. Veja
[Conceitos](../inicio/conceitos.md#math-tools).

| Antes (2.x) | Depois (3.0) |
|---|---|
| `SpellBook.MathTools.Math.MathTools` (aninhada no `MonoBehaviour` `Math`) | `SpellBook.Core.MathTools` |

## Eventos do GAS {#eventos-do-gas}

Os eventos globais `AbilityActivatedEvent` e `AbilitySlotUpdateEvent` foram removidos, e o ASC
não dispara mais evento no `EventManager`.

- No lugar do `AbilityActivatedEvent`, assine o evento C# `OnAbilityActivated` do
  `AbilitySystemComponent`. Ele recebe o `AbilitySpec`, e a habilidade está em `spec.Ability`:

  ```csharp
  asc.OnAbilityActivated += spec => Debug.Log($"{spec.Ability.DisplayName} ativada");
  ```

- O `AbilitySlotUpdateEvent` nunca foi disparado pelo pacote. Se o seu código disparava esse
  evento, declare um tipo de evento próprio com os mesmos campos.

Até uma versão futura, o pacote não tem canal global de "habilidade ativada" nem de "slot
atualizado". Quem precisa desses fatos usa os eventos C# do ASC.

## Tempo do ASC {#tempo-do-asc}

O ASC não lê mais o `Time` da Unity. Durações de efeitos e recargas contam o tempo que o ASC
recebe da fonte de tempo. Veja [Tempo do ASC](../modulos/gas.md#tempo-do-asc).

- **Nada muda para quem não mexe nos campos novos.** O `autoTick` vem ligado e o `timeSource`
  vazio usa o tempo escalado da Unity, como na 2.x.
- **`ActiveEffectInstance.StartTime` foi removido.** Para saber há quanto tempo um efeito está
  ativo, guarde o `TickedTime` do ASC ao aplicar o efeito e compare com o valor atual.
- **As esperas das ações contam o tempo do ASC.** `WaitAction`, `RepeatAction.Interval` e
  `SpawnProjectileAction.DelayBetweenShots` esperam o `TickedTime` do ASC de origem, e não mais o
  `WaitForSeconds`. Com os padrões, nada muda. Com `autoTick` desligado e sem `Tick`, ou com o
  grupo pausado, a espera para. Veja [Ações que esperam](../modulos/gas.md#acoes-que-esperam).

| Antes (2.x) | Depois (3.0) |
|---|---|
| O ASC avançava sozinho por `Time.deltaTime` | `autoTick` (ligado por padrão) + `Tick(deltaTime)` + `timeSource` |
| `ActiveEffectInstance.StartTime` | removido |
| `WaitAction`, `RepeatAction.Interval` e `SpawnProjectileAction.DelayBetweenShots` esperavam o tempo escalado da Unity (`WaitForSeconds`) | esperam o `TickedTime` do ASC de origem; com `autoTick` desligado sem `Tick`, ou com o grupo pausado, a espera para. Com os padrões (`autoTick` ligado, fonte vazia), nada muda. `WaitAction` com `Duration <= 0` continua cedendo um quadro |

## Spawn e projéteis {#spawn-e-projeteis}

As ações de spawn não usam mais o `PoolManager` da cena. Elas criam objetos pelo campo
`Spawner`, um `SpawnerAsset`. Vazio usa o spawner padrão, por `Instantiate`. Veja
[Ações de spawn](../modulos/gas.md#acoes-de-spawn).

- **Uma cena sem `PoolManager` não dá mais erro.** Na 2.x, `SpawnProjectileAction` e
  `PlayCueAction` lançavam `NullReferenceException` sem ele.
- **Quem usava o pool perde o pool até configurar um spawner.** Os projéteis e VFX passam a ser
  criados por `Instantiate`. O spawner com pool chega no módulo Pooling.
- **Código que chamava `Projectile.Setup` passa um `SpawnerAsset`** no lugar do
  `ComponentPooler`. Passe o mesmo spawner que criou o projétil, porque é por ele que o projétil
  volta.
- **O `Projectile` não implementa mais o `IPoolable`.** Uma subclasse que sobrescrevia
  `OnSpawnFromPool` ou `OnReturnToPool` passa o código para o `OnEnable` (reinício) ou para o
  `ReturnToPool` (saída).
- **Vida e invulnerabilidade do projétil seguem a fonte de tempo dele.** O `Lifetime` e a
  `InitialInvulnerabilityDuration` viraram contadores no `FixedUpdate`, pelo campo `timeSource`
  (vazio usa o tempo escalado, como na 2.x). O `protected virtual IEnumerator
  InitialInvulnerability()` saiu, e uma subclasse que o sobrescrevia deixa de compilar: ajuste a
  `InitialInvulnerabilityDuration` ou sobrescreva o `HandleCollision`. Uma subclasse que sobrescreve o
  `FixedUpdate` sem chamar `base.FixedUpdate()` perde a vida e a invulnerabilidade.

| Antes (2.x) | Depois (3.0) |
|---|---|
| `Projectile.Setup(ComponentPooler, ...)` | `Projectile.Setup(SpawnerAsset, ...)` |
| `Projectile : IPoolable` (`OnSpawnFromPool`, `OnReturnToPool`) | sem `IPoolable`; o estado reinicia no `OnEnable` |
| `SpawnProjectileAction` e `PlayCueAction` usavam `PoolManager.Instance` | campo `Spawner` (vazio → `Instantiate`) |
| `SpawnVFXEffectAction` usava `Instantiate` e `Destroy` | campo `Spawner`; o VFX volta pelo mesmo spawner. **Mudou o fim de vida sem `DestroyOnRemoval`:** na 2.x, `Destroy(vfx, duration + startLifetime)` destruía o VFX mesmo com `Looping` ligado; na 3.0 ele só volta pelo spawner quando a partícula para (`ReleaseWhenParticlesStop`), então uma partícula em loop nunca volta. Desligue o `Looping` ou marque `DestroyOnRemoval`. Veja [Armadilhas do pooling](../modulos/pooling.md#armadilhas). |
| `Projectile`: vida e invulnerabilidade por corrotina no tempo escalado; `protected virtual IEnumerator InitialInvulnerability()` | contadores no `FixedUpdate` pela fonte de tempo do projétil (vazio → escalado, como na 2.x); o método saiu, e subclasses que o sobrescrevem deixam de compilar: ajuste `InitialInvulnerabilityDuration` ou sobrescreva `HandleCollision` |
