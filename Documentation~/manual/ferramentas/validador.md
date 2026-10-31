# Validador {#validador}

O validador procura, de uma vez, os erros de configuração dos assets e componentes do projeto:
uma resposta de evento sem canal, um spawner com tamanho negativo, um tipo que sumiu do código.
As mesmas mensagens aparecem no topo do Inspector de cada objeto e numa janela com a lista do
projeto inteiro.

O validador fica no assembly `SpellBook.Core.Editor` e só existe no Editor.

## Abrir {#abrir}

Menu **SpellBook > Validar projeto**. A janela valida ao abrir, e o botão **Validar de novo**
refaz a varredura depois de uma correção.

A varredura cobre:

- os assets de `ScriptableObject` da pasta `Assets`;
- os prefabs da pasta `Assets`, com cada objeto da hierarquia;
- os objetos das cenas abertas, incluindo os desativados.

Em cada `GameObject`, o validador olha cada componente. Assets de outros pacotes, dentro de
`Packages`, ficam de fora.

## Ler a janela {#janela}

Cada linha é um erro ou um aviso, com o nome do objeto e a mensagem, por exemplo
`Painel: Resposta Int32 sem canal: escolha o Channel.`. O topo da janela mostra quantos erros e avisos
a varredura achou.

Clique numa linha para selecionar o objeto e destacá-lo na Hierarchy ou no Project. Quando a
mensagem é de um componente, a seleção vai para o `GameObject` dele, e o Inspector mostra a mesma
mensagem no topo.

## Mensagens no Inspector {#inspector}

A janela e o Inspector usam a mesma validação: a mensagem do topo do Inspector de um objeto é a
mesma que a janela mostra para ele, e some quando o problema é corrigido. Veja
[Validação no Inspector](../inicio/inspector.md#validacao-no-inspector).

Um erro no `Validate` de um tipo não para a varredura. Ele aparece como um erro com o tipo da
exceção, por exemplo `A validação lançou NullReferenceException: ...`, e os outros objetos
continuam sendo validados.

## Regras da 3.0 {#regras}

O validador acusa erro em:

- uma resposta de `EventListener` sem canal, uma chamada do **Escape** sem objeto ou sem função e
  uma reação pronta com campo obrigatório vazio (veja [Validação](../modulos/eventos.md#validacao)
  em Eventos);
- um `EventEmitter` sem canal;
- uma `RaiseEventAction` sem canal, acusada na habilidade que a contém (veja
  [RaiseEventAction](../modulos/gas.md#raiseeventaction));
- um `PooledSpawnerAsset` com **Initial Size** negativo;
- uma referência `[SerializeReference]` cujo tipo não existe mais (abaixo).

## Tipo não encontrado {#tipo-nao-encontrado}

Quando o tipo de um campo `[SerializeReference]` some do código, o validador acusa um erro por
tipo ausente, com o assembly e a classe que faltam, no mesmo texto do Inspector:

> Tipo não encontrado: `<assembly>` `<namespace.Classe>`. Restaure o tipo para recuperar os
> dados, ou limpe a referência.

O validador só relata: ele nunca limpa a referência, e os dados continuam no asset. Para
restaurar o tipo ou limpar a referência, veja
[Tipo não encontrado](../inicio/inspector.md#tipo-nao-encontrado) no Inspector.

Isso vale também para os seus tipos. Um tipo seu cujo Inspector não herda de `SpellBookEditor`
não mostra o aviso no Inspector: o tipo ausente dele só aparece aqui, no validador.

## Validar pelos testes {#teste}

O pacote tem o teste EditMode `Project_HasNoValidationErrors`, que roda a mesma varredura e falha
se houver algum erro. O SpellBook não tem CI de testes: o teste roda na suíte do projeto, pelo
**Test Runner** da Unity.

Os testes de um pacote só aparecem no Test Runner se o pacote estiver em `testables` no
`Packages/manifest.json`:

```json
{
  "dependencies": { "...": "..." },
  "testables": [ "com.spellbook" ]
}
```

Para não rodar os testes do pacote inteiro, escreva um teste seu, num asmdef de testes de Editor
que referencie o `SpellBook.Core` e o `SpellBook.Core.Editor`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;

public class ValidacaoDoProjetoTests
{
    [Test]
    public void Projeto_SemErrosDeValidacao()
    {
        var mensagens = new List<ValidationMessage>();

        ProjectValidator.Validate(ProjectValidator.CollectProjectTargets(), mensagens);

        Assert.That(mensagens.Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.Message), Is.Empty);
    }
}
```

## Validar um tipo seu {#ivalidatable}

Implemente `IValidatable` (`SpellBook.Core.Validation`) num `MonoBehaviour`, num
`ScriptableObject` ou numa classe `[Serializable]` usada por eles. O `Validate` só adiciona
mensagens: não muda o objeto, não escreve no console e não usa `UnityEditor`.

```csharp
using SpellBook.Core.Validation;
using UnityEngine;

public class Porta : MonoBehaviour, IValidatable
{
    [SerializeField] private GameObject chave;
    [SerializeField] private float tempoAberta = 2f;

    public void Validate(ValidationResult result)
    {
        if (chave == null) result.AddError("Porta sem chave: escolha a Chave.", nameof(chave));
        if (tempoAberta <= 0f) result.AddWarning("Tempo aberta zero: a porta fecha na hora.", nameof(tempoAberta));
    }
}
```

- `AddError` e `AddWarning` recebem uma mensagem curta, que diz o que corrigir, e o nome do campo
  (opcional). Mensagem vazia lança `ArgumentException`.
- Só os erros fazem o teste falhar; os avisos aparecem na janela e no Inspector.
- Um tipo com listas de objetos `[Serializable]` próprios repassa a chamada para cada item, como o
  `EventListener` faz com as respostas.

O validador acha o tipo sozinho. Para a mensagem aparecer também no topo do Inspector, o Inspector
do tipo precisa herdar de `SpellBookEditor`; veja
[Validação no Inspector](../inicio/inspector.md#validacao-no-inspector).
