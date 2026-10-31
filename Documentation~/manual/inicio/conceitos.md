# Conceitos {#conceitos}

Esta página explica como o SpellBook está dividido em módulos e quais dependências entre eles
são permitidas.

## Módulos e assemblies {#modulos}

Cada módulo do SpellBook é um assembly (`.asmdef`) próprio. Um asmdef do seu projeto que usa
um módulo precisa referenciar o assembly dele. Os assemblies de runtime têm
`autoReferenced` ligado, então scripts fora de asmdef (no `Assembly-CSharp`) já enxergam todos.

### Mapa de assemblies {#mapa}

O pacote tem estes assemblies. A coluna "Referencia" lista os assemblies do SpellBook que cada
um pode referenciar; nenhum outro é permitido.

| Assembly | O que tem | Referencia |
|---|---|---|
| `SpellBook.Core` | Seletor de tipo e `[ShowIf]` do Inspector, validação, `Void`, `RuntimeSet<T>`, `MathTools` e os assets de spawn (`SpawnerAsset`). | — |
| `SpellBook.Time` | [Fontes de tempo](../modulos/tempo.md) trocáveis. | Core |
| `SpellBook.Tags` | [Tags](../modulos/tags.md) hierárquicas de gameplay. | Core |
| `SpellBook.Pooling` | [Reaproveitamento de objetos](../modulos/pooling.md). | Core |
| `SpellBook.Events` | [Eventos](../modulos/eventos.md) por código, canais em asset, ouvintes e emissores. | Core e Time |
| `SpellBook.GAS` | [Sistema de habilidades](../modulos/gas.md): atributos, efeitos, habilidades, alvos e projéteis. | Core, Tags e Time |
| `SpellBook.GAS.Bridges` | Ligações do GAS com outros módulos, como a [`RaiseEventAction`](../modulos/gas.md#raiseeventaction), que dispara um evento a partir de uma habilidade. | Core, Tags, Time, GAS e Events |
| `SpellBook.Core.Editor` | Código de Editor do Core (Inspector do SpellBook). Só existe no Editor. | Core |
| `SpellBook.Events.Editor` | Inspectors do canal e do emissor, Event Debugger e Find references. Só existe no Editor. | Core, Time, Events e Core.Editor |
| `SpellBook.Time.Editor` | Inspectors das fontes de tempo. Só existe no Editor. | Core, Time e Core.Editor |
| `SpellBook.Tags.Editor` | Inspector da definição de tag. Só existe no Editor. | Core, Tags e Core.Editor |
| `SpellBook.Pooling.Editor` | Inspectors do pooling. Só existe no Editor. | Core, Pooling e Core.Editor |
| `SpellBook.GAS.Editor` | Inspectors do GAS. Só existe no Editor. | Core, Tags, Time, GAS e Core.Editor |

Um projeto que usa só o pooling, por exemplo, tem um asmdef que referencia o `SpellBook.Core` e o
`SpellBook.Pooling` e nada mais. Ele compila sem o GAS, o Events, o Time e o Tags.

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

A regra vale entre assemblies:

- O `SpellBook.Core` não depende de nenhum outro módulo do SpellBook.
- Os módulos de infraestrutura (Core, Time, Tags, Pooling e Events) nunca dependem do GAS.
- O `SpellBook.Events` depende só do `SpellBook.Core` e do `SpellBook.Time`.
- O `SpellBook.GAS` não depende de Events nem de Pooling.
- O `SpellBook.GAS.Bridges` é o único assembly que liga o GAS a outro módulo (o Events) e
  não depende de Pooling nem de nenhum assembly de Editor.
- Um assembly de Editor de módulo, como o `SpellBook.Events.Editor`, pode depender do
  `SpellBook.Core.Editor`, mas não do Editor de outro módulo.

Assim, um projeto que só quer eventos ou pooling não carrega o GAS. Os testes do pacote conferem
essa regra lendo os asmdefs.

O pacote não depende do Odin Inspector nem de outro pacote de Inspector. Veja
[Inspector](inspector.md).
