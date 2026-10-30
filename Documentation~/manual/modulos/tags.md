# Tags {#tags}

As tags de gameplay são rótulos hierárquicos, como `State.Stunned` ou `Ability.Fireball.Cooldown`.
O GAS usa as tags para bloquear, exigir e cancelar habilidades, e efeitos podem conceder tags
enquanto estão ativos. Esta página mostra como criar tags e como elas são comparadas.

As tags ficam no assembly `SpellBook.Tags`, no namespace `SpellBook.Tags`. Um asmdef seu que usa
tags precisa referenciar o `SpellBook.Tags`. Veja [Conceitos](../inicio/conceitos.md#modulos).

## GameplayTagDefinition {#gameplaytagdefinition}

Cada tag é um asset do tipo `GameplayTagDefinition`. O nome do asset é o nome da tag.

Para criar uma tag:

1. Clique com o botão direito no Project → **Create → GAS → Tag Definition**.
2. Dê ao asset o nome da tag, por exemplo `State.Stunned`.
3. Se a tag tem uma tag pai, arraste o asset do pai para o campo **Parent**.

### Hierarquia {#hierarquia}

A hierarquia vem só do campo **Parent**, não do nome. O ponto em `State.Stunned` é uma convenção
para quem lê; o que liga `State.Stunned` a `State` é o asset `State` estar no **Parent** de
`State.Stunned`.

Exemplo de hierarquia:

| Asset | Parent |
|---|---|
| `State` | (vazio) |
| `State.Stunned` | `State` |
| `State.Debuff` | `State` |
| `State.Debuff.Slowed` | `State.Debuff` |

## GameplayTag no Inspector {#gameplaytag}

Os campos de tag do GAS, como **Ability Tag**, **Blocked Tags** e **Granted Tags**, são do tipo
`GameplayTag`. No Inspector, um `GameplayTag` mostra o campo **Definition**: arraste para ele o
asset `GameplayTagDefinition` da tag. Um `GameplayTag` sem definição é uma tag **inválida**
(`IsValid` é `false` e `Name` é `"None"`).

Em código, crie um `GameplayTag` a partir da definição:

```csharp
using SpellBook.Tags;

var stunned = new GameplayTag(stunnedDefinition);
```

## Matches {#matches}

`a.Matches(b)` responde se a tag `a` é a tag `b` ou descende dela:

| `a` | `b` | `a.Matches(b)` |
|---|---|---|
| `State.Stunned` | `State.Stunned` | `true` (a mesma tag) |
| `State.Stunned` | `State` | `true` (filho casa com o pai) |
| `State.Debuff.Slowed` | `State` | `true` (vale para qualquer ancestral) |
| `State` | `State.Stunned` | `false` (pai não casa com o filho) |
| `State.Stunned` | `State.Debuff` | `false` (irmãos não casam) |
| inválida | qualquer | `false` |
| qualquer | inválida | `false` |

O `AbilitySystemComponent` usa a mesma regra: `HasTag(State)` é `true` quando o personagem tem
`State.Stunned`. Assim, uma habilidade com `State` em **Blocked Tags** fica bloqueada por
qualquer estado filho.

## Armadilhas {#armadilhas}

- **Uma tag é um asset.** Duas definições com o mesmo nome são duas tags diferentes. Crie cada tag
  uma vez e reuse o mesmo asset em todos os campos.
- **A comparação é por referência.** `==`, `Equals` e `Matches` comparam os assets, não os nomes.
  Renomear o asset não quebra as referências, e uma cópia do asset é outra tag.
- **`==` não olha a hierarquia.** `State.Stunned == State` é `false`. Para perguntar se uma tag
  está dentro de outra, use `Matches`.
- **Tag inválida nunca casa**, nem com outra tag inválida. Um campo de tag vazio numa lista de
  **Required Tags** faz a habilidade nunca ativar.
