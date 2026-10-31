# Inspector {#inspector}

O Inspector do SpellBook é feito em UI Toolkit, que já vem com a Unity. Esta página explica o
seletor de tipo, como renomear tipos sem perder dados, o aviso de tipo não encontrado, a
validação no topo do Inspector, como esconder campos com `[ShowIf]` e o
que acontece em Inspectors que não são UI Toolkit.

## Seletor de tipo {#seletor-de-tipo}

Alguns campos aceitam vários tipos concretos, por exemplo a lista de ações de uma habilidade.
No código, esses campos são `[SerializeReference]` com o atributo `[TypePicker]`:

```csharp
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using UnityEngine;

public class Exemplo : ScriptableObject
{
    [SerializeReference, TypePicker]
    private List<MinhaAcao> acoes = new();
}
```

No Inspector, cada campo mostra o rótulo e um botão com o nome do tipo atual, ou **(Nenhum)**
quando está vazio. Abaixo do botão aparecem os campos do tipo escolhido. Ao trocar o tipo,
os campos são refeitos.

Os tipos que aparecem na lista são os concretos, não genéricos, marcados com `[Serializable]`,
com construtor sem parâmetros e que não herdam de `UnityEngine.Object`. Tipos de outros
assemblies também entram.

## Popup, busca e categorias {#popup-busca-categorias}

Clicar no botão abre o popup do seletor, ancorado nele:

- **Busca:** o campo no topo recebe o foco ao abrir. A busca não diferencia maiúsculas de
  minúsculas e procura no nome exibido, no nome do tipo e no caminho da categoria. O resultado
  é uma lista plana.
- **Categorias:** sem busca, os tipos ficam em pastas. Clique numa categoria (marcada com seta)
  para entrar nela e use "voltar" para subir um nível. Categorias vêm antes dos tipos, em ordem
  alfabética, e **(Nenhum)** é o primeiro item.
- **Resumo:** o rodapé mostra o resumo do item marcado.
- **Teclado:** setas navegam, Enter escolhe, Esc fecha e Backspace com a busca vazia volta um
  nível. O popup também fecha ao perder o foco.

Para dar categoria e resumo a um tipo seu, use `[TypeCategory]`. Os níveis são separados por `/`.
Tipo sem o atributo aparece na raiz da lista.

```csharp
using System;
using SpellBook.Core.Inspector;

[Serializable]
[TypeCategory("Fluxo/Espera", Summary = "Espera um tempo antes de seguir.")]
public class MinhaEspera : MinhaAcao
{
}
```

Se o nome `TypeCategory` colidir com os de outro pacote, não escreva
`using SpellBook.Core.Inspector;`. Use um alias:

```csharp
using SB = SpellBook.Core.Inspector;

[SB.TypeCategory("Fluxo")]
public class MinhaAcaoComAlias : MinhaAcao
{
}
```

Em listas, duplicar um elemento no Inspector não deixa dois elementos apontando para a mesma
instância: o seletor troca a cópia por uma instância própria, e editar um elemento não muda o
outro.

## Renomear tipos sem perder dados {#movedfrom}

A Unity grava o nome do tipo (assembly, namespace e classe) nos assets de campos
`[SerializeReference]`. Se você renomear a classe, mudar o namespace ou movê-la de assembly, o
asset não encontra mais o tipo e o campo aparece como **(Nenhum)**.

Para renomear sem perder dados, marque o tipo com `[MovedFrom]`, que fica em
`UnityEngine.Scripting.APIUpdating`, informando o nome antigo:

```csharp
using System;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(autoUpdateAPI: false, sourceNamespace: "Jogo.Acoes", sourceAssembly: "Jogo", sourceClassName: "EsperaAntiga")]
public class MinhaEspera : MinhaAcao
{
}
```

Preencha só o que mudou: `sourceNamespace` para mudança de namespace, `sourceAssembly` para
mudança de assembly e `sourceClassName` para mudança de nome. Os dados são atualizados na
próxima vez que o asset for salvo. Mantenha o atributo enquanto existirem assets antigos.

## Tipo não encontrado {#tipo-nao-encontrado}

Se o tipo de um campo `[SerializeReference]` some (a classe foi apagada, renomeada sem
`[MovedFrom]` ou mudou de assembly), a Unity guarda os dados da referência no asset, mas o campo
aparece como **(Nenhum)**. A Unity não informa qual campo perdeu o tipo, então o aviso não fica
no campo: fica no **topo do Inspector** do objeto, um erro por tipo ausente:

> Tipo não encontrado: `<assembly>` `<namespace.Classe>`. Restaure o tipo para recuperar os
> dados, ou limpe a referência.

- **Restaurar o tipo** (voltar a classe com o mesmo nome, namespace e assembly, ou marcar a
  classe nova com [`[MovedFrom]`](#movedfrom)) recupera os dados.
- **Limpar referência**, o botão do aviso, apaga os dados guardados. Ele pede confirmação, e o
  SpellBook nunca limpa sozinho. Trate a limpeza como definitiva.
- Antes de escolher outro tipo num campo que aparece como **(Nenhum)** por causa desse aviso,
  restaure o tipo ou limpe a referência.

O aviso aparece nos Inspectors que herdam de `SpellBookEditor` (veja
[Validação no Inspector](#validacao-no-inspector)). Um tipo seu com um Inspector que não herda
dele não mostra o aviso.

## Mostrar campos por condição {#showif}

`[ShowIf]` mostra um campo só quando um campo **irmão** tem um valor. O irmão é outro campo
serializado do mesmo objeto ou da mesma struct, do tipo `bool` ou `enum`, e a comparação é por
igualdade:

```csharp
using SpellBook.Core.Inspector;
using UnityEngine;

public class MinhaConfig : ScriptableObject
{
    public enum Modo { Fixo, Aleatorio }

    public Modo modo;

    [ShowIf(nameof(modo), Modo.Fixo)]
    public float valor;

    public bool usarCor;

    [ShowIf(nameof(usarCor), true)]
    public Color cor;
}
```

Quando o irmão muda, o campo aparece ou some na hora. Dentro de uma lista de structs, cada
elemento usa o irmão do próprio elemento.

Limites:

- **Sem expressão.** O irmão é só o nome de um campo. Propriedades, métodos e expressões como
  `Period > 0` não são aceitos.
- **Não esconde listas.** Em um campo de lista, a Unity aplica o atributo a cada elemento, e não à
  lista inteira.
- **Irmão inválido.** Se o irmão não existe, não é `bool` nem `enum`, ou o valor não combina com o
  tipo dele, o campo fica sempre visível e o Console mostra um aviso uma vez.
- **Um drawer por campo.** `[ShowIf]` não combina com `[TypePicker]` no mesmo campo (veja abaixo).

O valor guardado no asset não muda: um campo escondido mantém o valor que tinha.

Se o nome `ShowIf` colidir com o de outro pacote, use um alias, como em `[TypeCategory]`:

```csharp
using SB = SpellBook.Core.Inspector;

public class MinhaConfigComAlias : UnityEngine.ScriptableObject
{
    public bool usarCor;

    [SB.ShowIf(nameof(usarCor), true)]
    public UnityEngine.Color cor;
}
```

## Limite de um drawer por campo {#um-drawer-por-campo}

A Unity usa um único drawer por campo. Um campo com `[TypePicker]` já tem o drawer do
seletor, então não combine esse atributo com outro atributo de drawer no mesmo campo, como
`[ShowIf]`: só um deles vale, e o outro é ignorado.

`[TypePicker]` vale só para campos `[SerializeReference]`, simples ou em lista.

## Validação no Inspector {#validacao-no-inspector}

Um tipo que implementa `IValidatable` (`SpellBook.Core.Validation`) mostra as mensagens de
validação no **topo do Inspector**, um aviso ou erro por mensagem, logo abaixo dos avisos de
[tipo não encontrado](#tipo-nao-encontrado). Quando não há mensagem, nada aparece.

As mensagens são recalculadas ao abrir o Inspector e sempre que um valor do objeto muda. O botão
**Revalidar** recalcula na hora, incluindo os avisos de tipo não encontrado. Com vários objetos
selecionados, cada mensagem começa com o nome do objeto. Se o `Validate` lançar uma exceção, ela
aparece como um erro com o tipo da exceção.

Isso vale para os Inspectors que herdam de `SpellBookEditor` (`SpellBook.Core.Editor`). Para usar
no seu tipo, crie um editor de uma linha numa pasta `Editor`:

```csharp
using SpellBook.Core.Editor;
using UnityEditor;

[CustomEditor(typeof(MinhaConfig))]
public sealed class MinhaConfigEditor : SpellBookEditor { }
```

Para pôr botões ou outros elementos, sobrescreva `BuildBeforeDefault` (antes dos campos) ou
`BuildAfterDefault` (depois dos campos), que recebem o elemento raiz do Inspector.

## Inspectors dos seus tipos {#inspectors-dos-seus-tipos}

Todo `MonoBehaviour` e `ScriptableObject` público do SpellBook já tem um Inspector que herda de
`SpellBookEditor`. Nos seus tipos, faça o mesmo: crie o editor de uma linha mostrado acima. Se o
seu tipo herda de um tipo do SpellBook, ele pode já usar o Inspector do pacote por herança, como
as subclasses de `Formula`, `TargetingStrategy`, `TimeSourceAsset` e `EventChannel`.

Para cobrir um tipo base e todas as subclasses dele, passe `true` no segundo argumento:

```csharp
using SpellBook.Core.Editor;
using UnityEditor;

[CustomEditor(typeof(MinhaBase), true)]
public sealed class MinhaBaseEditor : SpellBookEditor { }
```

**Conflito com um Inspector seu.** Se você escrever um `[CustomEditor]` para um tipo do
SpellBook, ou um com `editorForChildClasses` para uma base da qual um tipo do SpellBook herda,
a Unity pode escolher o seu Inspector em vez do do pacote. Se ele não herdar de
`SpellBookEditor`, a validação no topo e os avisos de tipo não encontrado somem desse Inspector.
O mesmo vale para Inspectors de outros pacotes. Para não perder os avisos:

- herde de `SpellBookEditor` e use `BuildBeforeDefault` e `BuildAfterDefault` para o que for
  seu;
- se não puder herdar, use o validador do projeto, que continua mostrando as mesmas mensagens
  (veja [Validador](../ferramentas/validador.md)).

## Inspectors IMGUI e de terceiros {#imgui-e-terceiros}

Os drawers do SpellBook só existem em UI Toolkit. Se um Inspector IMGUI seu, ou um Inspector
de outro pacote, desenhar um campo do SpellBook pelo caminho IMGUI, o campo não mostra o
seletor. Em vez disso, aparece uma única linha de aviso: **"Este campo precisa do Inspector em
UI Toolkit"**.

O SpellBook não dá suporte a esses Inspectors. O que fazer:

1. Se o Inspector é seu, migre-o para UI Toolkit (`CreateInspectorGUI`) ou remova o
   `[CustomEditor]`, para a Unity usar o Inspector padrão do tipo.
2. Se o Inspector é de outro pacote, desligue-o para os tipos do SpellBook, ou deixe que a
   Unity use o Inspector padrão neles.

Os dados não são afetados: o aviso é só de exibição, e nada é gravado ou perdido.
