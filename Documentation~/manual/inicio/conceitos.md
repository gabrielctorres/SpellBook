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
