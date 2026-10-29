# SpellBook {#spellbook}

O SpellBook é um pacote de ferramentas reutilizáveis para a Unity. Ele junta, num só lugar,
sistemas que aparecem em quase todo jogo: habilidades e efeitos, eventos, tempo de jogo,
tags e reaproveitamento de objetos.

Para instalar, veja a página [Instalação](inicio/instalacao.md).

## Para quem é {#para-quem-e}

- **Programadores** que querem sistemas prontos, testados e separados em assemblies,
  para usar só o que o projeto precisa.
- **Designers** que montam habilidades, reações e eventos pelo Inspector, sem escrever código.
  Os campos com vários tipos possíveis têm um seletor com busca e categorias.

## Módulos {#modulos}

Cada módulo é um assembly próprio. Um módulo só depende do Core e dos módulos de que
realmente precisa.

| Módulo | Para que serve |
|---|---|
| **Core** | Base comum: o seletor de tipo do Inspector, a validação de assets e as utilidades usadas pelos outros módulos. |
| **Tempo** | Fontes de tempo trocáveis (escalada, não escalada, manual e grupos pausáveis), para pausar só parte do jogo. |
| **Eventos** | Eventos por código, canais em asset que o designer liga sem código, e ouvintes com reações prontas. |
| **Tags** | Tags hierárquicas de gameplay, como `Estado.Atordoado`. |
| **Pooling** | Reaproveitamento de objetos instanciados com frequência, como projéteis e partículas. |
| **GAS** | Sistema de habilidades: atributos, efeitos, habilidades, alvos e projéteis. |

!!! note "Versão 3.0 em desenvolvimento"
    A 3.0 reorganiza o pacote nos módulos acima e ainda está em desenvolvimento.
    As páginas de cada módulo entram no manual junto com o módulo.
