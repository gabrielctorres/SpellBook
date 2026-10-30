# Eventos {#eventos}

O módulo Eventos serve para um sistema avisar outros de que algo aconteceu sem que um conheça o
outro. Quem dispara não sabe quem ouve, e quem ouve não guarda referência para quem dispara.

O módulo fica no assembly `SpellBook.Events` (namespace `SpellBook.Events`). Um asmdef seu que usa
os eventos precisa referenciar o `SpellBook.Events`. Veja [Conceitos](../inicio/conceitos.md#modulos).

!!! note "Em desenvolvimento"
    Nesta versão de desenvolvimento da 3.0, o módulo tem o barramento de código (`EventBus<T>`),
    os canais Void, Int, Float, Bool, String, Vector3 e GameObject e o componente ouvinte
    (`EventListener`) com as reações prontas SetActive, Toggle de componente, Log e RaiseChannel.
    A reação Invoke com delay e o emissor chegam nas próximas etapas.

## Três usos e uma regra {#tres-usos}

Existem três jeitos de avisar que algo aconteceu. Cada um tem o seu caso:

| Uso | Quando usar | Forma |
|---|---|---|
| Canal em asset | Fato global que o designer liga no Inspector, sem código | [Canal](#canais) (`ScriptableObject`) |
| Barramento de código | Fato global só entre sistemas de código | [`EventBus<T>`](#eventbus) com um struct |
| Evento C# por instância | Estado de **uma** entidade | `event` C# no próprio componente, fora deste módulo |

**Regra: estado de uma entidade nunca passa por canal nem por barramento.** A vida de um
personagem ou a recarga de uma habilidade são lidas do próprio componente, por um `event` C# dele.
Canal e barramento são globais: qualquer um ouve qualquer disparo, e um dado de uma entidade
misturado com os das outras vira acoplamento escondido.

## EventBus\<T\> {#eventbus}

O `EventBus<T>` é um barramento estático por tipo. Cada struct de evento tem o seu barramento,
sem asset e sem objeto para criar. Serve para avisos entre sistemas de código, como "a partida
começou" ou "o jogador subiu de nível".

### IEvent {#ievent}

O tipo do evento é um `struct` que implementa `IEvent`. A interface é vazia: ela só marca o struct
como evento.

```csharp
using SpellBook.Events;

public struct PartidaComecou : IEvent
{
    public int Fase;
}
```

### Inscrever, desinscrever e disparar {#eventbus-uso}

```csharp
using SpellBook.Events;
using UnityEngine;

public sealed class Placar : MonoBehaviour
{
    private void OnEnable()  { EventBus<PartidaComecou>.Subscribe(AoComecar); }
    private void OnDisable() { EventBus<PartidaComecou>.Unsubscribe(AoComecar); }

    private void AoComecar(PartidaComecou evento)
    {
        Debug.Log($"Fase {evento.Fase}");
    }
}

// Em qualquer outro sistema:
EventBus<PartidaComecou>.Raise(new PartidaComecou { Fase = 2 });
```

- `Subscribe(Action<T>)` inscreve o ouvinte. Inscrever duas vezes o mesmo método não faz ele ser
  chamado duas vezes.
- `Unsubscribe(Action<T>)` desinscreve. Desinscrever quem não está inscrito não faz nada.
- `Raise(in T)` entrega o evento a todos os inscritos.
- Inscreva no `OnEnable` e desinscreva no `OnDisable`. Um ouvinte que não se desinscreve continua
  sendo chamado depois de destruído.
- Para desinscrever, passe o **mesmo** delegate da inscrição. Um método do componente, como no
  exemplo, funciona. Uma lambda escrita de novo no `OnDisable` é outro delegate e não desinscreve.

O `Raise` não aloca memória depois que os ouvintes estão inscritos, então pode ser chamado todo
quadro. A **ordem** de chamada dos ouvintes não é garantida: não escreva código que dependa dela.

## Canais {#canais}

Um canal é um asset (`ScriptableObject`) que representa um fato global, como "o jogador morreu"
ou "a porta abriu". Quem dispara e quem ouve recebem o **mesmo asset** num campo do Inspector e
não se conhecem. É o jeito de ligar eventos sem escrever código de ligação.

### Criar um canal {#criar-canal}

No Project, **Create > SpellBook > Events** e escolha o tipo:

| Menu | Classe | Payload |
|---|---|---|
| Void Channel | [`VoidEventChannel`](#voideventchannel) | nenhum |
| Int Channel | [`IntEventChannel`](#inteventchannel) | `int` |
| Float Channel | [`FloatEventChannel`](#floateventchannel) | `float` |
| Bool Channel | [`BoolEventChannel`](#booleventchannel) | `bool` |
| String Channel | [`StringEventChannel`](#stringeventchannel) | `string` |
| Vector3 Channel | [`Vector3EventChannel`](#vector3eventchannel) | `Vector3` |
| GameObject Channel | [`GameObjectEventChannel`](#gameobjecteventchannel) | `GameObject` |

Não há canal de `Component`. Para avisar sobre um componente, use o
[`GameObjectEventChannel`](#gameobjecteventchannel) e leia o componente no objeto recebido.

No Inspector do canal:

- **Description**: para que serve o canal e quando ele é disparado. Escreva para quem vai ligar o
  canal depois.
- **Log Raises**: escreve cada disparo no console, com o nome do canal e o payload. Ligue para
  depurar e desligue depois, porque o log aloca memória.
- **Debug Payload**: payload de teste do canal. Não muda nada no jogo.

### EventChannel\<T\> {#eventchannel}

Todo canal herda de `EventChannel<T>`, em que `T` é o tipo do dado que o disparo leva.

```csharp
using SpellBook.Events;
using UnityEngine;

public sealed class BarraDeVida : MonoBehaviour
{
    [SerializeField] private IntEventChannel vidaMudou;

    private void OnEnable()  { vidaMudou.Raised += AoMudar; }
    private void OnDisable() { vidaMudou.Raised -= AoMudar; }

    private void AoMudar(int vida)
    {
        Debug.Log($"Vida: {vida}");
    }
}

// Em quem dispara, com o mesmo asset no campo:
vidaMudou.Raise(80);
```

- `Raised` é um `event` C#: `+=` inscreve e `-=` desinscreve. Inscrever duas vezes o mesmo método
  não faz ele ser chamado duas vezes.
- `Raise(T payload)` entrega o payload a todos os inscritos. `Raise(T payload, Object sender)` faz
  o mesmo e informa quem disparou, que aparece no log.
- `LastPayload` guarda o último payload disparado.
- `ListenerCount` diz quantos ouvintes estão inscritos, e `GetListeners(List<object>)` preenche a
  lista com o objeto de cada ouvinte (ou o próprio delegate, num método estático).
- As regras do barramento valem aqui: inscreva no `OnEnable` e desinscreva no `OnDisable`, o
  `Raise` não aloca memória com os ouvintes inscritos e o Log Raises desligado, e a
  [segurança do disparo](#seguranca) é a mesma.

Para um payload seu, crie uma classe `XEventChannel : EventChannel<X>` com
`[CreateAssetMenu]`.

### VoidEventChannel {#voideventchannel}

Canal sem dado: só avisa que algo aconteceu. Dispare com `Raise()`. O ouvinte recebe um
`SpellBook.Core.Void`, que não carrega nada.

### IntEventChannel {#inteventchannel}

Canal com payload `int`, como uma quantidade ou um índice.

### FloatEventChannel {#floateventchannel}

Canal com payload `float`, como uma porcentagem ou uma duração.

### BoolEventChannel {#booleventchannel}

Canal com payload `bool`, como ligado e desligado ou pausado e em jogo.

### StringEventChannel {#stringeventchannel}

Canal com payload `string`, como um identificador ou uma mensagem. Montar a string a cada disparo
aloca memória; prefira strings fixas num disparo que acontece todo quadro.

### Vector3EventChannel {#vector3eventchannel}

Canal com payload `Vector3`, como uma posição ou uma direção.

### GameObjectEventChannel {#gameobjecteventchannel}

Canal com payload `GameObject`, como o objeto que morreu ou que foi coletado. O ouvinte lê o
componente de que precisa no objeto recebido. Um objeto destruído antes do disparo chega como
`null` para o Unity: confira antes de usar.

### Armadilhas {#canais-armadilhas}

- **Asset duplicado pelo Addressables.** Se o canal estiver num grupo do Addressables e também for
  referenciado por uma cena ou asset fora dele, o build pode ter duas cópias do canal. Quem
  dispara numa cópia não chega a quem ouve na outra. Deixe o canal e quem o usa no mesmo esquema
  de carregamento.
- **A ordem não é garantida.** Não escreva um ouvinte que dependa de outro ter sido chamado antes.
- **`Void` não é `System.Void`.** O payload do `VoidEventChannel` é o `Void` do namespace
  `SpellBook.Core`. Num arquivo com `using System;`, escreva `SpellBook.Core.Void` no método do
  ouvinte.

## EventListener {#eventlistener}

O `EventListener` é o componente que ouve canais sem código. Adicione-o a um objeto
(**Add Component > EventListener**) e, na lista **Responses**, escolha uma resposta para cada
canal que o objeto deve ouvir.

- O componente se inscreve nos canais no `OnEnable` e se desinscreve no `OnDisable`. Desabilitado
  (o componente ou o objeto), ele não recebe disparos; habilitado de novo, volta a receber.
  Desabilitar também cancela as esperas de um [Invoke com delay](#delayedreaction).
- Um disparo do canal roda as **reações** da resposta na ordem da lista e, depois delas, o
  **Escape**.
- Uma resposta sem canal não faz nada.

### Respostas {#respostas}

Cada resposta liga **um** canal ao que acontece quando ele dispara. Há uma resposta pronta para
cada canal, na categoria **Respostas** do seletor:

| Resposta | Canal |
|---|---|
| `VoidEventResponse` | [`VoidEventChannel`](#voideventchannel) |
| `IntEventResponse` | [`IntEventChannel`](#inteventchannel) |
| `FloatEventResponse` | [`FloatEventChannel`](#floateventchannel) |
| `BoolEventResponse` | [`BoolEventChannel`](#booleventchannel) |
| `StringEventResponse` | [`StringEventChannel`](#stringeventchannel) |
| `Vector3EventResponse` | [`Vector3EventChannel`](#vector3eventchannel) |
| `GameObjectEventResponse` | [`GameObjectEventChannel`](#gameobjecteventchannel) |

No Inspector de uma resposta:

- **Channel**: o canal ouvido. Só aceita canais do mesmo payload da resposta.
- **Reactions**: lista de reações, escolhidas pelo seletor de tipo. Cada uma recebe o payload do
  disparo e o `EventListener` que a executa.
- **Escape**: um `UnityEvent` com o payload, num foldout fechado. Chamado depois das reações. Use
  só quando nenhuma reação serve: a ligação pelo `UnityEvent` quebra se o método for
  renomeado.

Uma reação sua herda de `Reaction` e sobrescreve `Execute(EventListener)`, ou
`Execute<T>(EventListener, in T)` quando precisa do payload:

```csharp
using System;
using SpellBook.Events;
using UnityEngine;

[Serializable]
public sealed class TocarSom : Reaction
{
    [SerializeField] private AudioSource fonte;

    public override void Execute(EventListener listener)
    {
        fonte.Play();
    }
}
```

### Reações prontas {#reacoes-prontas}

O pacote traz reações para os casos comuns. No seletor da lista **Reactions**, elas ficam
agrupadas por categoria. Cada uma tem validação: um campo obrigatório vazio é erro de validação.

| Reação | Categoria | Faz |
|---|---|---|
| [`SetActiveReaction`](#setactivereaction) | Objeto | Ativa ou desativa um GameObject |
| [`ToggleComponentReaction`](#togglecomponentreaction) | Objeto | Liga, desliga ou inverte um componente |
| [`RaiseChannelReaction`](#raisechannelreaction) | Eventos | Dispara outro canal |
| [`LogReaction`](#logreaction) | Depuração | Escreve uma mensagem no console |
| [`DelayedReaction`](#delayedreaction) | Fluxo | Roda uma lista de reações depois de um atraso |

#### SetActive {#setactivereaction}

Chama `SetActive` no GameObject escolhido.

- **Target**: o GameObject. Obrigatório.
- **Active**: marcado, ativa o objeto; desmarcado, desativa. Começa desmarcado.

#### Toggle de componente {#togglecomponentreaction}

Muda o `enabled` de um componente que é `Behaviour`, como um script seu, uma `Light` ou um
`Animator`. Um `Collider` 3D ou um `Renderer` não entram no campo, porque não são `Behaviour`.

- **Target**: o componente. Obrigatório.
- **Mode**: `Enable` liga, `Disable` desliga e `Toggle` inverte o estado atual.

#### RaiseChannel {#raisechannelreaction}

Dispara outro canal quando a resposta recebe um disparo. O `EventListener` aparece como quem
disparou no log do canal. Há duas formas, escolhidas em **Mode**:

- `ForwardPayload`: repassa o payload recebido para o **Target**, que precisa ter o mesmo tipo de
  payload da resposta. Com um canal de outro tipo, a reação não dispara nada e a validação acusa
  erro. Exemplo: numa `FloatEventResponse`, ao receber `3.5`, dispara outro `FloatEventChannel`
  com `3.5`.
- `RaiseVoid`: dispara um [`VoidEventChannel`](#voideventchannel), seja qual for o payload da
  resposta. Um **Target** que não é Void é erro de validação.

**Target** é obrigatório nas duas formas.

#### Log {#logreaction}

Escreve no console exatamente o texto de **Message**, sem prefixo. Clicar na entrada do console
seleciona o `EventListener`.

- **Message**: o texto. Obrigatório.
- **Type**: tipo da entrada (`Log`, `Warning`, `Error`...). Começa em `Log`.

#### Invoke com delay {#delayedreaction}

Espera um atraso e então roda a sua própria lista de reações, na ordem, com o payload do disparo
que a agendou.

- **Delay**: o atraso em segundos. Começa em `1`.
- **Time Source**: a [fonte de tempo](tempo.md) que conta o atraso. Vazio usa o tempo escalado:
  com `Time.timeScale = 0`, a espera não anda, e volta a andar quando o jogo retoma. Escolha uma
  [fonte não escalada](tempo.md#unscaledtimesource) para contar o atraso no menu de pausa, ou
  um [grupo pausável](tempo.md#pausabletimesource) para pausar só uma parte do jogo.
- **Reactions**: as reações rodadas depois do atraso, escolhidas pelo seletor. Obrigatória: uma
  lista vazia é erro de validação.

Cada disparo agenda uma espera própria: dois disparos seguidos rodam a lista duas vezes, cada
uma com o payload dela. Cada espera pendente ocupa um pouco de memória, então evite um Invoke
com delay num canal que dispara todo quadro.

**Cancelamento.** Se o `EventListener` for desabilitado (o componente ou o objeto) durante a
espera, a lista não roda, nem quando o atraso acaba nem quando o ouvinte é habilitado de novo.
Um disparo recebido depois de reabilitar agenda uma espera nova normalmente.

### Payload próprio {#payload-proprio}

Para um canal com um dado seu, crie o struct, o canal e a resposta. As classes do canal e da
resposta não precisam de corpo.

```csharp
using System;
using SpellBook.Events;
using UnityEngine;

[Serializable]
public struct Dano
{
    public int Valor;
    public Vector3 Ponto;
}

[CreateAssetMenu(menuName = "Jogo/Events/Dano Channel")]
public sealed class DanoEventChannel : EventChannel<Dano>
{
}

[Serializable]
public sealed class DanoEventResponse : EventResponse<Dano>
{
}
```

- O canal (`DanoEventChannel.cs`) fica num arquivo com o nome da classe, porque é um asset.
- A `DanoEventResponse` aparece no seletor da lista **Responses**; o **Escape** dela é um
  `UnityEvent<Dano>`.
- Quem dispara usa `danoRecebido.Raise(new Dano { Valor = 10, Ponto = transform.position })`.
- Marque o struct com `[Serializable]` para o payload aparecer no Inspector do canal e do Escape.

## Segurança do disparo {#seguranca}

Um ouvinte pode inscrever ou desinscrever ouvintes durante um disparo sem quebrar o disparo:

- quem é **desinscrito** durante o disparo e ainda não foi chamado não é chamado;
- quem é **inscrito** durante o disparo só recebe a partir do próximo disparo;
- nenhum ouvinte é pulado nem chamado duas vezes no mesmo disparo.

### Exceção num ouvinte {#excecao}

No Editor e em development build (`DEVELOPMENT_BUILD`), se um ouvinte lança exceção, ela vai para o
console e os outros ouvintes recebem o evento normalmente.

Em **release build** não há essa proteção. A exceção sobe pelo `Raise` como qualquer outra, e os
ouvintes que viriam depois não são chamados. Isso evita o custo de um `try/catch` por ouvinte no
jogo publicado. Trate a exceção dentro do próprio ouvinte se ele puder falhar em release.

## Estado entre sessões de Play {#reset}

Ao entrar em Play, todo `EventBus<T>` e todo canal ficam sem ouvintes. Isso acontece com e sem domain reload
(opção **Enter Play Mode Options**), pelo
`RuntimeInitializeOnLoadMethod(SubsystemRegistration)`. Um ouvinte de uma sessão anterior que não
se desinscreveu não é chamado na sessão seguinte. O `LastPayload` dos canais não é limpo.
