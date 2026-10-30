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
