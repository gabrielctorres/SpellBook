# Conceitos {#conceitos}

Esta página explica como o SpellBook está dividido em módulos e quais dependências entre eles
são permitidas.

## Módulos e assemblies {#modulos}

Cada módulo do SpellBook é um assembly (`.asmdef`) próprio. Um asmdef do seu projeto que usa
um módulo precisa referenciar o assembly dele. Os assemblies de runtime têm
`autoReferenced` ligado, então scripts fora de asmdef (no `Assembly-CSharp`) já enxergam todos.

### Mapa atual {#mapa-atual}

A 3.0 está em desenvolvimento, e a separação em módulos acontece aos poucos. Hoje o pacote tem
estes assemblies:

| Assembly | O que tem hoje |
|---|---|
| `SpellBook.Core` | Seletor de tipo e `[ShowIf]` do Inspector, validação de assets, conjuntos em runtime, eventos, pooling e utilidades de matemática. |
| `SpellBook.GAS` | Sistema de habilidades: atributos, efeitos, habilidades, alvos, projéteis e as tags de gameplay. Referencia só o `SpellBook.Core`. |
| `SpellBook.Core.Editor` | Código de Editor do Core (Inspector do SpellBook). Só existe no Editor. |

### Mapa ao fim da reorganização {#mapa-alvo}

Ao fim da reorganização da 3.0, o que hoje está dentro do `SpellBook.Core` sai para módulos
próprios. Estes assemblies ainda **não existem**; a tabela mostra para onde o pacote vai:

| Assembly | Para que serve |
|---|---|
| `SpellBook.Core` | Base comum: Inspector, validação e utilidades. |
| `SpellBook.Time` | Fontes de tempo trocáveis. |
| `SpellBook.Events` | Eventos por código, canais em asset e ouvintes. |
| `SpellBook.Pooling` | Reaproveitamento de objetos. |
| `SpellBook.Tags` | Tags hierárquicas de gameplay (hoje dentro do `SpellBook.GAS`). |
| `SpellBook.GAS` | Sistema de habilidades. |
| `SpellBook.GAS.Bridges` | Ligações do GAS com outros módulos, como disparar um evento a partir de uma habilidade. |

## Core {#core}

Além do Inspector e da validação, o `SpellBook.Core` tem três utilidades que os outros módulos
e o seu projeto podem usar.

### `Void` {#void}

`Void` é uma struct vazia e serializável. Serve de payload para um evento que não carrega dado,
no lugar de um tipo inventado só para isso.

### `RuntimeSet<T>` {#runtime-set}

`RuntimeSet<T>` é um `ScriptableObject` abstrato que guarda uma lista de itens em runtime, como
os inimigos vivos na cena. Para usar, crie uma classe concreta que herda dele com o tipo do
item. `Add` ignora um item que já está na lista, e `Remove` ignora um item que não está.

```csharp
[CreateAssetMenu(menuName = "Game/Enemy Set")]
public class EnemySet : RuntimeSet<Enemy> { }
```

### `MathTools` {#math-tools}

`MathTools` é uma classe estática do namespace `SpellBook.Core`. Não precisa de componente na
cena: chame os métodos direto.

| Método | O que faz |
|---|---|
| `AngleBetweenVectors(from, to)` | Ângulo com sinal entre dois vetores 2D, em graus. |
| `Map(value, inMin, inMax, outMin, outMax)` | Converte um valor de um intervalo para outro, de forma linear. Valores fora do intervalo de entrada saem fora do intervalo de saída. |
| `ProjectToXZPlane(point, height)` | O mesmo ponto com o `y` trocado por `height`. |
| `RandomPointInCircle(center, radius)` | Ponto aleatório dentro de um círculo 2D, com distribuição uniforme. Usa o `UnityEngine.Random`. |
| `RandomPointInSphere(center, radius)` | Ponto aleatório dentro de uma esfera. Usa o `UnityEngine.Random`. |
| `MidPoint(a, b)` | Ponto médio entre dois pontos. |
| `IsInRange(value, min, max)` | `true` se `value` está entre `min` e `max`, com os dois limites incluídos. |
| `CenterOfMass(points)` | Média dos pontos de um array. |

```csharp
using SpellBook.Core;

float volume = MathTools.Map(distance, 0f, 20f, 1f, 0f);
```

Na 2.x, `MathTools` ficava aninhada no componente `Math` (`SpellBook.MathTools.Math.MathTools`).
Esse componente não existe mais na 3.0.

## Regra de dependência {#regra-de-dependencia}

A regra vale entre assemblies. Hoje ela é esta:

- O `SpellBook.Core` não depende de nenhum outro módulo do SpellBook, nem do GAS.
- O `SpellBook.GAS` depende só do `SpellBook.Core`.

Os testes do pacote conferem essa regra lendo os asmdefs.

Ao fim da reorganização, com os assemblies do [mapa alvo](#mapa-alvo), a regra vai ser esta:

- O `SpellBook.Core` não vai depender de nenhum outro módulo do SpellBook.
- Os módulos de infraestrutura (Core, Time, Events, Pooling e Tags) nunca vão depender do GAS.
- O `SpellBook.GAS` não vai depender de Events nem de Pooling. Quem vai ligar o GAS a esses
  módulos é o `SpellBook.GAS.Bridges`.
- O `SpellBook.GAS.Bridges` não vai depender de Pooling.

Assim, um projeto que só quer eventos ou pooling não vai carregar o GAS.

O pacote não depende do Odin Inspector nem de outro pacote de Inspector. Veja
[Inspector](inspector.md).
