# Migração das versões antigas para a 1.0

A 1.0 tem quebras de compatibilidade. Esta página lista o que muda e o que fazer em cada caso.
## URL de instalação

O pacote agora fica na pasta `Packages/com.spellbook` do repositório, e a URL do git passa a
apontar para ela com `?path=`. A chave `com.spellbook` no `manifest.json` não muda.

| Antes (versões antigas) | Depois (1.0) |
|---|---|
| `https://github.com/gabrielctorres/SpellBook.git` | `https://github.com/gabrielctorres/SpellBook.git?path=/Packages/com.spellbook` |

- Acrescente `#<tag>` ao final para fixar uma versão, por exemplo `#v1.0.0`.
- As tags das versões antigas continuam com a URL antiga, sem `?path=`.
- Veja [Instalação](../inicio/instalacao.md).

## Sem Odin Inspector

O pacote não depende mais do Odin Inspector. Ele compila e funciona num projeto sem o Odin.

- **Os assets continuam válidos.** Nenhum campo serializado mudou de nome nem de tipo, então
  habilidades, efeitos e atributos abrem sem perda de dados.
- **Há perdas no Inspector do GAS.** Os editores embutidos (custo, recarga, fórmula), a
  pré-visualização da `ExpressionFormula`, o destaque de campos obrigatórios e as tabelas e a
  busca de ações vinham do Odin e não existem mais. As abas e os grupos voltaram nos Inspectors
  próprios da habilidade e do efeito, em UI Toolkit, e os campos que não valem na configuração
  atual somem sem perder o valor. Veja [Inspector da habilidade](../modulos/gas.md#inspector-da-habilidade),
  [Inspector do efeito](../modulos/gas.md#inspector-do-efeito) e [Inspector](../inicio/inspector.md).

## Assembly do GAS

O GAS saiu do `SpellBook.Core` e agora é o assembly `SpellBook.GAS`. Um asmdef seu que usa
tipos do GAS precisa referenciar o `SpellBook.GAS`. Scripts fora de asmdef não precisam mudar.
Veja [Conceitos](../inicio/conceitos.md#modulos-e-assemblies).

## Tags

As tags agora são o módulo [Tags](../modulos/tags.md), no assembly `SpellBook.Tags`. O
namespace saiu do GAS (`SpellBook.GAS.Tags` virou `SpellBook.Tags`) e o assembly saiu do
`SpellBook.Core`.

| Antes (versões antigas) | Depois (1.0) |
|---|---|
| Namespace `SpellBook.GAS.Tags` | Namespace `SpellBook.Tags` |
| `GameplayTag` e `GameplayTagDefinition` no assembly `SpellBook.Core` | Os mesmos tipos no assembly `SpellBook.Tags` |

- Troque `using SpellBook.GAS.Tags;` por `using SpellBook.Tags;`.
- Um asmdef seu que usa tags precisa referenciar o `SpellBook.Tags`.
- Os assets de tag e os campos de tag continuam válidos; nada muda no Inspector.

## Recarga da habilidade

O campo **Cooldown Reduction Attribute** da `GameplayAbility` não aceita mais o nome antigo
`_cooldownReductionAttribute`. Nas versões antigas, um `[FormerlySerializedAs]` lia esse nome antigo e
migrava o valor; na 1.0 esse atributo saiu.

- Assets salvos nas versões antigas já usam o nome novo e não perdem nada.
- Assets salvos pela última vez nas versões antigas perdem o valor desse campo. Antes de atualizar, abra o
  projeto nas versões antigas e regrave essas habilidades, por exemplo com
  `AssetDatabase.ForceReserializeAssets()` num script de Editor. Se já atualizou, preencha o
  campo de novo no Inspector.

## Sistemas de eventos antigos

As versões antigas tinham três sistemas de eventos: os `GameEvent` de `Core/Events`, os canais de
`Core/NewEventSystem` e o `EventManager` de `Systems/Events`. Os três foram removidos, sem
`[Obsolete]`. O módulo `SpellBook.Events` os substitui.

- **Os assets de evento precisam ser recriados.** Um asset de `GameEvent`, `VoidEvent`,
  `IntEvent` etc. perde o script. Crie o canal novo pelo menu `SpellBook/Events/...` e refaça
  as referências nos componentes.
- **Não há canal de `Component`.** Use o `GameObjectEventChannel` ou declare um canal próprio
  (`XEventChannel : EventChannel<X>`).
- **Não há evento por string.** Troque o nome do evento por um canal ou por um tipo de evento
  próprio no `EventBus<T>`.

| Antes (versões antigas) | Depois (1.0) |
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

## `Void` e `RuntimeSet<T>`

Os dois mudaram de namespace e continuam no `SpellBook.Core`. Troque o `using` no seu código. O
nome da classe não mudou, então os assets de um `RuntimeSet<T>` seu continuam válidos. Veja
[Conceitos](../inicio/conceitos.md#core).

| Antes (versões antigas) | Depois (1.0) |
|---|---|
| `SpellBook.Core.NewEventSystem.Void` | `SpellBook.Core.Void` |
| `SpellBook.Core.RuntimeSets.RuntimeSet<T>` | `SpellBook.Core.RuntimeSet<T>` |

## `MathTools`

O componente `Math` não existe mais. A `MathTools` saiu de dentro dele e é uma classe estática do
`SpellBook.Core`. Remova o componente `Math` das cenas e chame os métodos direto. Veja
[Conceitos](../inicio/conceitos.md#mathtools).

| Antes (versões antigas) | Depois (1.0) |
|---|---|
| `SpellBook.MathTools.Math.MathTools` (aninhada no `MonoBehaviour` `Math`) | `SpellBook.Core.MathTools` |

## Pooling

O pooling saiu de `Systems/Pooling` e agora é o módulo [Pooling](../modulos/pooling.md), no
assembly `SpellBook.Pooling`. O namespace e o menu de criação mudaram.

| Antes (versões antigas) | Depois (1.0) |
|---|---|
| Namespace `SpellBook.Systems.Pooling` (`PoolManager`, `ComponentPooler`, `PoolConfiguration`, `PoolableObject`, `IPoolable`) | Namespace `SpellBook.Pooling`, com os mesmos nomes de tipo |
| Menu `PoolingSystem/...` | Menu `SpellBook/Pooling/...` |

- Troque `using SpellBook.Systems.Pooling;` por `using SpellBook.Pooling;`.
- Um asmdef seu que usa o pooling precisa referenciar o `SpellBook.Pooling`.
- Os assets de `PoolConfiguration` continuam válidos, porque o nome da classe não mudou.

## Eventos do GAS

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

## Tempo do ASC

O ASC não lê mais o `Time` da Unity. Durações de efeitos e recargas contam o tempo que o ASC
recebe da fonte de tempo. Veja [Tempo do ASC](../modulos/gas.md#id-4.6-tempo-do-asc).

- **Nada muda para quem não mexe nos campos novos.** O `autoTick` vem ligado e o `timeSource`
  vazio usa o tempo escalado da Unity, como nas versões antigas.
- **`ActiveEffectInstance.StartTime` foi removido.** Para saber há quanto tempo um efeito está
  ativo, guarde o `TickedTime` do ASC ao aplicar o efeito e compare com o valor atual.
- **As esperas das ações contam o tempo do ASC.** `WaitAction`, `RepeatAction.Interval` e
  `SpawnProjectileAction.DelayBetweenShots` esperam o `TickedTime` do ASC de origem, e não mais o
  `WaitForSeconds`. Com os padrões, nada muda. Com `autoTick` desligado e sem `Tick`, ou com o
  grupo pausado, a espera para. Veja [Ações que esperam](../modulos/gas.md#acoes-que-esperam).

| Antes (versões antigas) | Depois (1.0) |
|---|---|
| O ASC avançava sozinho por `Time.deltaTime` | `autoTick` (ligado por padrão) + `Tick(deltaTime)` + `timeSource` |
| `ActiveEffectInstance.StartTime` | removido |
| `WaitAction`, `RepeatAction.Interval` e `SpawnProjectileAction.DelayBetweenShots` esperavam o tempo escalado da Unity (`WaitForSeconds`) | esperam o `TickedTime` do ASC de origem; com `autoTick` desligado sem `Tick`, ou com o grupo pausado, a espera para. Com os padrões (`autoTick` ligado, fonte vazia), nada muda. `WaitAction` com `Duration <= 0` continua cedendo um quadro |

## Spawn e projéteis

As ações de spawn não usam mais o `PoolManager` da cena. Elas criam objetos pelo campo
`Spawner`, um `SpawnerAsset`. Vazio usa o spawner padrão, por `Instantiate`. Veja
[Ações de spawn](../modulos/gas.md#acoes-de-spawn).

- **Uma cena sem `PoolManager` não dá mais erro.** Nas versões antigas, `SpawnProjectileAction` e
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
  (vazio usa o tempo escalado, como nas versões antigas). O `protected virtual IEnumerator
  InitialInvulnerability()` saiu, e uma subclasse que o sobrescrevia deixa de compilar: ajuste a
  `InitialInvulnerabilityDuration` ou sobrescreva o `HandleCollision`. Uma subclasse que sobrescreve o
  `FixedUpdate` sem chamar `base.FixedUpdate()` perde a vida e a invulnerabilidade.

| Antes (versões antigas) | Depois (1.0) |
|---|---|
| `Projectile.Setup(ComponentPooler, ...)` | `Projectile.Setup(SpawnerAsset, ...)` |
| `Projectile : IPoolable` (`OnSpawnFromPool`, `OnReturnToPool`) | sem `IPoolable`; o estado reinicia no `OnEnable` |
| `SpawnProjectileAction` e `PlayCueAction` usavam `PoolManager.Instance` | campo `Spawner` (vazio → `Instantiate`) |
| `SpawnVFXEffectAction` usava `Instantiate` e `Destroy` | campo `Spawner`; o VFX volta pelo mesmo spawner. **Mudou o fim de vida sem `DestroyOnRemoval`:** nas versões antigas, `Destroy(vfx, duration + startLifetime)` destruía o VFX mesmo com `Looping` ligado; na 1.0 ele só volta pelo spawner quando a partícula para (`ReleaseWhenParticlesStop`), então uma partícula em loop nunca volta. Desligue o `Looping` ou marque `DestroyOnRemoval`. Veja [Armadilhas do pooling](../modulos/pooling.md#armadilhas). |
| `Projectile`: vida e invulnerabilidade por corrotina no tempo escalado; `protected virtual IEnumerator InitialInvulnerability()` | contadores no `FixedUpdate` pela fonte de tempo do projétil (vazio → escalado, como nas versões antigas); o método saiu, e subclasses que o sobrescrevem deixam de compilar: ajuste `InitialInvulnerabilityDuration` ou sobrescreva `HandleCollision` |

## Valores por nível no GAS

A `Duration` do `GameplayEffect`, o valor constante do modificador e o `Duration` da `WaitAction`
deixam de ser `float` e passam a ser um [`ScalableFloat`](../modulos/gas.md#valores-por-nivel-scalablefloat), que
pode variar com o nível.

- **Os assets continuam válidos.** Um efeito das versões antigas abre com a mesma duração e o mesmo valor, como
  constante, e uma `WaitAction` gravada numa habilidade abre com a mesma espera. A conversão roda quando o asset é carregado, então nada se perde mesmo sem migrar.
- **Regrave os assets com o menu.** Enquanto não forem regravados, os assets guardam os campos
  antigos no YAML, e o pacote vai deixar de ler esses campos em uma versão futura.

Passo a passo:

1. Atualize o pacote e espere a Unity compilar.
2. Faça um commit ou backup do projeto, para conferir a mudança no diff.
3. Rode **SpellBook > Migrar assets do GAS (3.0)**. O menu carrega todo `GameplayEffect` e todo
   `GameplayAbility` de `Assets/` e dos pacotes embutidos ou locais, regrava e escreve no Console
   quantos efeitos e quantas habilidades regravou. Os pacotes do registro são só leitura e ficam
   de fora.
   Nota: o nome do menu ainda traz o número antigo.
4. Confira no diff: `Duration: 4` vira um bloco `duration` com `constant: 4`, e
   `ConstantValue: -20` vira um bloco `Value` com `constant: -20`. Na `WaitAction`,
   `Duration: 1.5` vira um bloco `duration` com `constant: 1.5`. Os números não mudam.

Rodar o menu de novo não muda nada. Num script de Editor ou na CI, chame
`SpellBook.GAS.Editor.GasAssetMigration.MigrateAll()`, que devolve a soma dos efeitos e das
habilidades regravados.

No código:

| Antes (versões antigas) | Depois (1.0) |
|---|---|
| `efeito.Duration = 4f` | `efeito.Duration = ScalableFloat.Constant(4f)` |
| `float d = efeito.Duration` | `float d = efeito.Duration.Evaluate(1)` |
| `new WaitAction { Duration = 2f }` | `new WaitAction { Duration = ScalableFloat.Constant(2f) }` |
| `ValueSource.ConstantValue = 5f` | `ValueSource.Value = ScalableFloat.Constant(5f)` |
| `ModifierValueSource.SourceType.Constant` | `ModifierValueSource.SourceType.Value` (mesmo valor serializado) |
| `AttributeEffectModifier` e `ModifierValueSource` são `struct` | são classes: alterar o elemento da lista altera o original |
