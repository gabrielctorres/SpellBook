# Instalação {#instalacao}

## Requisitos {#requisitos}

- **Unity 6000.0 ou mais nova.** Versões anteriores (2021.3, 2022 e 2023) não instalam a 3.0.
- **Nenhuma dependência de terceiros.** O pacote usa só a Unity.

## Instalar pela URL do git {#instalar-pela-url-do-git}

1. Na Unity, abra **Window > Package Manager**.
2. Clique em **+** e escolha **Install package from git URL...**.
3. Cole a URL `https://github.com/gabrielctorres/SpellBook.git` e clique em **Install**.

Também dá para adicionar o pacote direto no `Packages/manifest.json` do projeto:

```json
{
  "dependencies": {
    "com.spellbook": "https://github.com/gabrielctorres/SpellBook.git"
  }
}
```

Depois da instalação, o projeto deve compilar sem erros vindos do SpellBook.

## Inspector {#inspector}

O Inspector do pacote é feito em UI Toolkit, que já vem com a Unity. Não é preciso instalar
nenhum plugin de Inspector. Na 3.0, os campos que aceitam vários tipos (por exemplo, as ações
de uma habilidade) ganham um seletor com busca e categorias.

## Compatibilidade {#compatibilidade}

Os testes do pacote (EditMode e PlayMode) rodam no CI a cada pull request, nestas versões:

| Versão | Papel |
|---|---|
| 6000.0.85f1 | Versão mínima suportada (6000.0) |
| 6000.6.7f1 | Última 6000.x testada |

O pacote vale para qualquer patch da 6000.0 em diante, mas só as versões da tabela são
testadas. As versões mudam quando o CI é atualizado.
