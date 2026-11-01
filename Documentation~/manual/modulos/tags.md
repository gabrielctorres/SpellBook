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

## GameplayTagContainer {#gameplaytagcontainer}

Um `GameplayTagContainer` é um conjunto de tags sem repetição, que guarda as tags na ordem em que
foram adicionadas. Use-o quando um campo ou uma variável precisa de várias tags e de perguntas como
"tem alguma destas?".

```csharp
using SpellBook.Tags;

var tags = new GameplayTagContainer(fireFlame, stunned);   // repetidas e inválidas são ignoradas
tags.Add(slowed);      // true: entrou no fim
tags.Add(slowed);      // false: já estava
tags.Remove(stunned);  // true: saiu (só a tag exata; filhas dela ficam)

bool burning = tags.HasTag(fire);   // true se o conjunto tem Elemento.Fogo ou uma filha dele
```

Todas as perguntas usam a mesma regra do [Matches](#matches): uma tag do conjunto casa com a tag
perguntada se é ela ou descende dela. A exceção é `HasTagExact`, que compara sem hierarquia.

Exemplo com um conjunto que tem só `Elemento.Fogo.Chama`:

| Pergunta | Resposta |
|---|---|
| `HasTag(Elemento.Fogo.Chama)` | `true` |
| `HasTag(Elemento.Fogo)` | `true` (o pai casa) |
| `HasTag(Elemento)` | `true` |
| `HasTag(Elemento.Gelo)` | `false` |
| `HasTagExact(Elemento.Fogo)` | `false` (sem hierarquia) |

`HasAny(lista)` é `true` se o conjunto tem alguma tag da lista, e `HasAll(lista)` se tem todas.
Com um conjunto `{A, B}`: `HasAny({B, C})` é `true`, `HasAll({B, C})` é `false` e
`HasAll({A, B})` é `true`. Com a lista vazia (ou `null`), `HasAll` é `true` e `HasAny` é `false`.
A lista pode ser outro conjunto, uma `List<GameplayTag>` ou um array, como os campos de tag do GAS.

- **Tag inválida nunca casa**: `Add` a recusa, e uma tag inválida na pergunta dá `false`.
- **Sem alocação**: as perguntas e o `foreach` não geram lixo; pode chamar a cada quadro.
- **Igualdade**: `==` compara a referência do conjunto, não o conteúdo. Para comparar conteúdo,
  use `a.HasAll(b) && b.HasAll(a)`.
- Dados editados à mão podem ter linhas vazias ou repetidas. O conjunto não as corrige sozinho:
  a linha vazia nunca casa, e `Count` conta o que está guardado.

## Ferramentas próprias {#ferramentas-proprias}

Uma ferramenta sua, como um painel de debug, pode ler a hierarquia usando só a API pública:

- `tag.GetDefinition()` devolve o asset `GameplayTagDefinition` da tag, ou `null` se a tag é
  inválida.
- `definition.Parent` devolve a tag pai, ou `null` na raiz.

Exemplo: escrever no Console o nome de uma tag e a cadeia de pais dela.

```csharp
using System.Text;
using SpellBook.Tags;
using UnityEngine;

public static class TagDebug
{
    public static void LogChain(GameplayTag tag)
    {
        var text = new StringBuilder(tag.Name);
        for (GameplayTagDefinition parent = tag.GetDefinition()?.Parent; parent != null; parent = parent.Parent)
        {
            text.Append(" → ").Append(parent.name);
        }

        Debug.Log(text.ToString());   // State.Debuff.Slowed → State.Debuff → State
    }
}
```

## Armadilhas {#armadilhas}

- **Uma tag é um asset.** Duas definições com o mesmo nome são duas tags diferentes. Crie cada tag
  uma vez e reuse o mesmo asset em todos os campos.
- **A comparação é por referência.** `==`, `Equals` e `Matches` comparam os assets, não os nomes.
  Renomear o asset não quebra as referências, e uma cópia do asset é outra tag.
- **`==` não olha a hierarquia.** `State.Stunned == State` é `false`. Para perguntar se uma tag
  está dentro de outra, use `Matches`.
- **Tag inválida nunca casa**, nem com outra tag inválida. Um campo de tag vazio numa lista de
  **Required Tags** faz a habilidade nunca ativar.
