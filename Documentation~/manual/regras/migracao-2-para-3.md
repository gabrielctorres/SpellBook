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
