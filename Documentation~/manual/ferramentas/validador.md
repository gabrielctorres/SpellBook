# Validador

O validador procura, de uma vez, os erros de configuração dos assets e componentes do projeto:
uma resposta de evento sem canal, um spawner com tamanho negativo, um tipo que sumiu do código.
As mesmas mensagens aparecem no topo do Inspector de cada objeto e numa janela com a lista do
projeto inteiro.

O validador fica no assembly `SpellBook.Core.Editor` e só existe no Editor.

## Abrir

Menu **SpellBook > Validar projeto**. A janela valida ao abrir, e o botão **Validar de novo**
refaz a varredura depois de uma correção.

A varredura cobre:

- os assets de `ScriptableObject` da pasta `Assets`;
- os prefabs da pasta `Assets`, com cada objeto da hierarquia;
- os objetos das cenas abertas, incluindo os desativados.

Em cada `GameObject`, o validador olha cada componente. Assets de outros pacotes, dentro de
`Packages`, ficam de fora.

## Ler a janela

Cada linha é um erro ou um aviso, com o nome do objeto e a mensagem, por exemplo
`Painel: Resposta Int32 sem canal: escolha o Channel.`. O topo da janela mostra quantos erros e avisos
a varredura achou.

Clique numa linha para selecionar o objeto e destacá-lo na Hierarchy ou no Project. Quando a
mensagem é de um componente, a seleção vai para o `GameObject` dele, e o Inspector mostra a mesma
mensagem no topo.

## Mensagens no Inspector

A janela e o Inspector usam a mesma validação: a mensagem do topo do Inspector de um objeto é a
mesma que a janela mostra para ele, e some quando o problema é corrigido. Veja
[Validação no Inspector](../inicio/inspector.md#validacao-no-inspector).

Um erro no `Validate` de um tipo não para a varredura. Ele aparece como um erro com o tipo da
exceção, por exemplo `A validação lançou NullReferenceException: ...`, e os outros objetos
continuam sendo validados.

## Regras da 3.0

O validador acusa erro em:

- uma resposta de `EventListener` sem canal, uma chamada do **Escape** sem objeto ou sem função e
  uma reação pronta com campo obrigatório vazio (veja [Validação](../modulos/eventos.md#validacao)
  em Eventos);
- um `EventEmitter` sem canal;
- um `AbilitySystemEventRelay` sem ASC no campo nem no objeto, com **Value Channel** de tipo fora
  da lista ou com chamada do **Response** sem objeto ou sem função (veja
  [Inspector e validação do relay](../modulos/eventos.md#inspector-e-validacao-do-relay) em Eventos);
- uma `RaiseEventAction` sem canal, acusada na habilidade que a contém (veja
  [RaiseEventAction](../modulos/gas.md#raiseeventaction));
- um `PooledSpawnerAsset` com **Initial Size** negativo;
- um `AttributeDefinition` com **Min Value** acima de **Max Value** (constantes), com fonte de limite
  `Attribute` sem atributo, apontando para o próprio atributo, fechando um ciclo de fontes ou
  derivada de uma fórmula que lê o atributo, ou com **Decimals** negativo no **Display** (veja
  [AttributeDefinition](../modulos/gas.md#attributedefinition));
- um `AbilitySystemComponent` com entrada vazia em **Starting Effects** (veja
  [Efeitos de partida](../modulos/gas.md#efeitos-de-partida));
- um `AttributeDefinition` com processador vazio em **Processors**, com `AbsorbWithAttribute` sem
  escudo, com o próprio atributo como escudo ou com **Fraction** fora de 0 a 1, com
  `ReduceByAttribute` sem atributo, ou com **State Tag While Depleted** apontando para uma tag que
  foi apagada. Num meta-atributo, o escudo igual ao próprio atributo aparece como ciclo. Um
  `GameplayEffect` com meta-atributo em **Modifiers** de efeito com duração ou infinito também é
  erro, porque o meta só muda pela base. A mensagem aponta o campo do processador (veja
  [Dano, escudo e morte](../modulos/dano-escudo-e-morte.md));
- uma habilidade com **Max Level** menor que 1, com nome vazio, inválido ou repetido na tabela
  **Values**, ou com uma entrada da tabela que lê outra (modo `Table`) (veja
  [Níveis e tabela de valores](../modulos/gas.md#niveis-e-tabela-de-valores));
- uma habilidade que usa um efeito cujo valor em modo `Table` tem um nome que não está na tabela
  dela. A mensagem cita o efeito e o nome, e selecionar a linha seleciona a habilidade. A habilidade
  confere a recarga, o custo e os efeitos de cada `ApplyEffectAction` das listas "Ao ativar",
  "Ao terminar" e "Ao ser cancelada", inclusive dentro de `RepeatAction`, `ParallelAction`,
  `ConditionalAction` e das **On Hit Actions** do `SpawnProjectileAction`. Uma ação sua que aplica efeitos entra na conferência ao sobrescrever
  `GameplayAction.CollectEffects`;
- um `GameplayEffect` com valor por nível inválido (array vazio, curva sem definir ou modo `Table`
  sem nome) na duração ou num modificador, com nome de **SetByCaller** vazio ou fora de letras,
  dígitos e `_`, ou com modificadores **Live** em ciclo, em que um atributo depende do outro (veja
  [Valores por nível](../modulos/gas.md#valores-por-nivel-scalablefloat) e
  [Snapshot ou ao vivo](../modulos/gas.md#snapshot-ou-ao-vivo));
- uma regra de **Activation Conditions** vazia ou sem condição, um `NotCondition` sem condição, uma
  `AttributeThresholdCondition` sem atributo ou com valor `SetByCaller` ou `TargetAttribute` (na
  ativação não há chamador nem alvo) e uma `CastRangeCondition`, mesmo dentro de outra condição, com
  **Cast Range** negativo em algum nível. A mensagem aponta o campo da regra ou o **Cast Range** da
  habilidade (veja [Condições](../modulos/gas.md#condicoes));
- uma habilidade com [cargas](../modulos/gas.md#cargas) ligadas e **Max Charges** constante menor que 1,
  **Recharge Time** constante menor ou igual a 0, ou um desses dois campos em modo `Table` com um nome que
  não está na tabela da habilidade;
- uma `CooldownReductionPolicy` com **Min Duration** negativo ou **Max Reduction** fora de 0 a 1
  (veja [CooldownReductionPolicy](../modulos/gas.md#cooldownreductionpolicy));
- uma linha vazia num `GameplayTagContainer` ou numa lista de um `GameplayTagQuery`, e a mesma tag
  em duas listas da consulta, nos campos cujo dono repassa a validação, como as **Damage Tags** do
  `GameplayEffect` (veja
  [Validação](../modulos/tags.md#validacao) em Tags);
- uma referência `[SerializeReference]` cujo tipo não existe mais (abaixo);
- um `MonoBehaviour` ou `ScriptableObject` público e concreto do SpellBook sem `[HelpURL]`, ou com
  `[HelpURL]` fora do manual (abaixo).

E acusa aviso numa tag repetida num conjunto ou numa lista da consulta, numa consulta com as três
listas vazias, num `AttributeDefinition` com **Hidden** sem **Category**, num meta-atributo sem processadores (a
alteração não afeta ninguém), num `AbilitySystemComponent` cujo **Attribute Set** tem um atributo com
processador que cita outro atributo fora do conjunto (em runtime, o processador não faz nada), num `GameplayEffect` com **Is Buff** e
**Is Debuff** ligados juntos (veja [Gameplay Effects](../modulos/gas.md#id-1.3-gameplay-effects-ge)), num `GameplayEffect`
com **SetByCaller** sem padrão (sem o valor do chamador, vale 0), com modo **Live** em efeito `Instant` ou
em modificador periódico, onde o modo não tem efeito, numa habilidade com **Cooldown Reduction Policy** sem
**Cooldown Reduction Attribute**, num `OrCondition` sem condições (sempre falso), numa `TagQueryCondition`
com a consulta vazia, numa `CastRangeCondition` com **Cast Range** 0 em todos os níveis (sem limite, ela
sempre passa) e numa habilidade com **Max Level** acima do tamanho de um array da tabela
(acima dele, o array repete o último valor).

## Tipo sem HelpURL

Cada componente e asset do SpellBook tem um `[HelpURL]` que leva à página dele neste manual (o
ícone de ajuda no topo do Inspector). A janela confere isso nos tipos de runtime do pacote, além
dos assets, e acusa um erro por tipo, por exemplo
`Tipo SpellBook.Pooling.PooledSpawnerAsset sem [HelpURL]: aponte o [HelpURL] para a página do manual (DocUrls.Base).`

Clique na linha para selecionar o script do tipo. Para corrigir, ponha no tipo o atributo
`[HelpURL]` com a constante de `DocUrls` do módulo e a âncora do tipo, por exemplo
`[HelpURL(DocUrls.Pooling + "#spawnerasset")]`. Esse erro só
aparece com o código do pacote alterado: numa versão publicada, todos os tipos já têm o atributo.
A regra não olha os seus tipos.

## Tipo não encontrado

Quando o tipo de um campo `[SerializeReference]` some do código, o validador acusa um erro por
tipo ausente, com o assembly e a classe que faltam, no mesmo texto do Inspector:

> Tipo não encontrado: `<assembly>` `<namespace.Classe>`. Restaure o tipo para recuperar os
> dados, ou limpe a referência.

O validador só relata: ele nunca limpa a referência, e os dados continuam no asset. Para
restaurar o tipo ou limpar a referência, veja
[Tipo não encontrado](../inicio/inspector.md#tipo-nao-encontrado) no Inspector.

Isso vale também para os seus tipos. Um tipo seu cujo Inspector não herda de `SpellBookEditor`
não mostra o aviso no Inspector: o tipo ausente dele só aparece aqui, no validador.

## Validar pelos testes

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

{% code title="Packages/com.spellbook/Tests/Core/EditMode/Examples/CoreManualExamples.cs:using-collections,using-linq,using-nunit,using-core-editor,using-validation,project-validation-test" %}
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
{% endcode %}

## Validar um tipo seu

Implemente `IValidatable` (`SpellBook.Core.Validation`) num `MonoBehaviour`, num
`ScriptableObject` ou numa classe `[Serializable]` usada por eles. O `Validate` só adiciona
mensagens: não muda o objeto, não escreve no console e não usa `UnityEditor`.

{% code title="Packages/com.spellbook/Tests/Core/EditMode/Examples/CoreManualExamples.cs:using-validation,using-unityengine,porta" %}
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
{% endcode %}

- `AddError` e `AddWarning` recebem uma mensagem curta, que diz o que corrigir, e o nome do campo
  (opcional). Mensagem vazia lança `ArgumentException`.
- Só os erros fazem o teste falhar; os avisos aparecem na janela e no Inspector.
- Um tipo com listas de objetos `[Serializable]` próprios repassa a chamada para cada item, como o
  `EventListener` faz com as respostas.

O validador acha o tipo sozinho. Para a mensagem aparecer também no topo do Inspector, o Inspector
do tipo precisa herdar de `SpellBookEditor`; veja
[Validação no Inspector](../inicio/inspector.md#validacao-no-inspector).
