# SpellBook

O SpellBook é um pacote para Unity com ferramentas para habilidades e efeitos,
eventos e controle do tempo do jogo. Também traz tags e reaproveitamento de objetos.

Para instalar, veja a página [Instalação](inicio/instalacao.md).

## Para quem é

- **Programadores** que querem sistemas prontos, testados e separados em assemblies,
  para usar só o que o projeto precisa.
- **Designers** que montam habilidades, reações e eventos pelo Inspector, sem escrever código.
  Os campos com vários tipos possíveis têm um seletor com busca e categorias.

## Módulos

Cada módulo tem um assembly `SpellBook.<Módulo>` e outro para o código de Editor,
`SpellBook.<Módulo>.Editor`. Todos dependem do Core. O módulo de Eventos usa o Tempo,
e o GAS usa Tags e Tempo.

| Módulo | Para que serve |
|---|---|
| **Core** | Base comum: o seletor de tipo do Inspector, a validação de assets e as utilidades usadas pelos outros módulos. |
| **Tempo** | Fontes de tempo trocáveis (escalada, não escalada, manual e grupos pausáveis), para pausar só parte do jogo. |
| **Eventos** | Eventos por código, canais em asset que o designer liga sem código, e ouvintes com reações prontas. |
| **Tags** | Tags hierárquicas de gameplay, como `Estado.Atordoado`. |
| **Pooling** | Reaproveitamento de objetos instanciados com frequência, como projéteis e partículas. |
| **GAS** | Sistema de habilidades: atributos, efeitos, habilidades, alvos e projéteis. |
| **GAS.Bridges** | Ligações do GAS com outros módulos, como disparar um evento a partir de uma habilidade. |

Os assemblies de cada módulo e as dependências permitidas entre eles estão em
[Conceitos](inicio/conceitos.md#mapa-de-assemblies).

{% hint style="info" %}
**Versão 1.0**

A 1.0 reorganiza o pacote nos módulos acima.
Quem vem das versões antigas deve ler a [Migração das versões antigas para a 1.0](regras/migracao-2-para-3.md).
{% endhint %}
