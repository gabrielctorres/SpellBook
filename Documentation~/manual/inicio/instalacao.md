# Instalação

## Requisitos

- **Unity 6000.0 ou mais nova.** Versões anteriores (2021.3, 2022 e 2023) não instalam a 1.0.
- **Nenhuma dependência de terceiros.** O pacote usa só a Unity.

## Instalar pela URL do git

1. Na Unity, abra **Window > Package Manager**.
2. Clique em **+** e escolha **Install package from git URL...**.
3. Cole a URL `https://github.com/gabrielctorres/SpellBook.git?path=/Packages/com.spellbook` e clique em **Install**.

Também dá para adicionar o pacote direto no `Packages/manifest.json` do projeto:

```json
{
  "dependencies": {
    "com.spellbook": "https://github.com/gabrielctorres/SpellBook.git?path=/Packages/com.spellbook"
  }
}
```

Para fixar uma versão, acrescente `#<tag>` ao final da URL. Por exemplo:

```json
{
  "dependencies": {
    "com.spellbook": "https://github.com/gabrielctorres/SpellBook.git?path=/Packages/com.spellbook#v1.0.0"
  }
}
```

{% hint style="info" %}
**Versões antigas**

As versões antigas usam a URL sem `?path=`: `https://github.com/gabrielctorres/SpellBook.git`.
A URL com `?path=` vale a partir da 1.0.
{% endhint %}

Depois da instalação, o projeto deve compilar sem erros vindos do SpellBook.

## Importar o Sample

O pacote traz o Sample **Sandbox**, uma cena de teste manual dos módulos.

1. Em **Window > Package Manager**, selecione **Spell Book**.
2. Na aba **Samples**, clique em **Import** ao lado de **Sandbox**.
3. Abra `Assets/Samples/Spell Book/<versão>/Sandbox/Scenes/Sandbox.unity` e entre em Play.

O Sample usa o Input Manager legado (**Active Input Handling** em **Input Manager (Old)** ou **Both**). Os controles
e o passo a passo estão em [Sample Sandbox](../exemplos/sandbox.md).

## Inspector

O Inspector do pacote é feito em UI Toolkit, que já vem com a Unity. Não é preciso instalar
nenhum plugin de Inspector. Na 1.0, os campos que aceitam vários tipos (por exemplo, as ações
de uma habilidade) ganham um seletor com busca e categorias.

## Compatibilidade

A versão mínima declarada é a **Unity 6000.0** (campo `unity` do `package.json`).

Os testes do pacote (EditMode e PlayMode) rodam no projeto de desenvolvimento do repositório, na
**Unity 6000.3.13f1**. Essa é a única versão testada. Não há CI de testes.

Nas outras versões a partir da 6000.0 o pacote deve funcionar, mas elas não são testadas.
