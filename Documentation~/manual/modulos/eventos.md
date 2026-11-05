# Eventos {#eventos}

O módulo Eventos serve para um sistema avisar outros de que algo aconteceu sem que um conheça o
outro. Quem dispara não sabe quem ouve, e quem ouve não guarda referência para quem dispara.

O módulo fica no assembly `SpellBook.Events` (namespace `SpellBook.Events`). Um asmdef seu que usa
os eventos precisa referenciar o `SpellBook.Events`. Veja [Conceitos](../inicio/conceitos.md#modulos).

!!! note "Em desenvolvimento"
    Nesta versão de desenvolvimento da 3.0, o módulo tem o barramento de código (`EventBus<T>`),
    os canais Void, Int, Float, Bool, String, Vector3 e GameObject e o componente ouvinte
    (`EventListener`) com as reações prontas SetActive, Toggle de componente, Log, RaiseChannel e
    Invoke com delay, e o emissor (`EventEmitter`), com o botão Raise no Inspector do canal e do
    emissor, e o seletor de canal filtrado por payload. O depurador de eventos chega numa próxima
    etapa.

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

{% code title="Packages/com.spellbook/Tests/Events/EditMode/Examples/EventsManualExamples.cs:using-events,partida-comecou" %}
```csharp
using SpellBook.Events;

public struct PartidaComecou : IEvent
{
    public int Fase;
}
```
{% endcode %}

### Inscrever, desinscrever e disparar {#eventbus-uso}

{% code title="Packages/com.spellbook/Tests/Events/EditMode/Examples/EventsManualExamples.cs:using-events,using-unityengine,placar,placar-raise" %}
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
{% endcode %}

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
- **Debug Payload**: payload de teste do canal, enviado pelo botão **Raise**. Não muda nada no
  jogo.

Abaixo dos campos, o Inspector do canal mostra:

- **Raise**: dispara o canal com o **Debug Payload**, como um `Raise(T)` sem emissor. Só fica
  ativo no Play. Com vários canais selecionados, dispara cada um com o seu payload de teste.
- **Último payload**: o `LastPayload` do canal. Não é limpo ao entrar em Play.
- **Ouvintes**: quantos estão inscritos agora e quem são (o nome e o tipo do objeto de cada
  ouvinte). A lista se atualiza sozinha enquanto o Inspector está aberto.

### EventChannel\<T\> {#eventchannel}

Todo canal herda de `EventChannel<T>`, em que `T` é o tipo do dado que o disparo leva.

{% code title="Packages/com.spellbook/Tests/Events/EditMode/Examples/EventsManualExamples.cs:using-events,using-unityengine,barra-de-vida,barra-de-vida-raise" %}
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
{% endcode %}

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

## Canais do GAS {#canais-do-gas}

O assembly `SpellBook.GAS.Bridges` traz canais com os dados dos eventos do GAS. Crie-os em
**Create > SpellBook > GAS > Events**. Eles ficam no GAS, e não no módulo Events: o Events não
conhece o GAS. Cada canal tem a sua resposta no seletor da lista **Responses** do
[`EventListener`](#eventlistener).

| Canal | Payload | Campos | Resposta |
|---|---|---|---|
| `EffectEventChannel` | `EffectEventData` | `Owner`, `Source`, `Effect`, `Stacks`, `PreviousStacks`, `RemovalReason`, `Result` | `EffectEventResponse` |
| `AbilityEventChannel` | `AbilityEventData` | `Owner`, `Ability`, `Level`, `Cancelled`, `FailureReason`, `FailureTag`, `FailureKey` | `AbilityEventResponse` |
| `AttributeChangedEventChannel` | `AttributeChangedData` | `Owner`, `Attribute`, `OldValue`, `NewValue`, `Delta`, `Instigator`, `Effect` | `AttributeChangedEventResponse` |
| `AttributeLimitEventChannel` | `AttributeLimitEventData` | `Owner`, `Attribute`, `Value`, `Instigator`, `Effect` | `AttributeLimitEventResponse` |
| `TagChangedEventChannel` | `TagChangedData` | `Owner`, `Tag`, `Added`, `Count` | `TagChangedEventResponse` |
| `BlockedEventChannel` | `BlockedEventData` | `Owner`, `Reason`, `Source` | `BlockedEventResponse` |

- `Owner` é o ASC do evento. Em efeito, `Source` é quem aplicou; em bloqueio, a origem da ação
  bloqueada.
- `RemovalReason` só tem sentido na remoção de efeito; `Result`, na aplicação e no bloqueio.
  `Cancelled` só tem sentido no fim da habilidade; `FailureReason`, `FailureTag` e `FailureKey`, na falha.
  `FailureTag` e `FailureKey` são a `ReasonTag` e a `ReasonKey` da `ActivationFailure`: inválida e nula
  quando a falha não tem esse motivo.
- `Delta` é `NewValue - OldValue`: negativo quando o valor caiu. O construtor
  `new AttributeChangedData(dono, atributo, antigo, novo)` calcula esse valor.
- `Instigator` e `Effect` dizem quem causou a mudança e com que efeito, quando ela vem do
  [pipeline de dano](dano-escudo-e-morte.md): um efeito instantâneo, um tique ou
  `ApplyAttributeChange`. O tipo do dano está nas `DamageTags` do `Effect`. Uma mudança fora do
  pipeline, como um modificador com duração ou `SetBaseValue`, chega com os dois nulos. O
  instigador destruído também chega nulo.
- `AttributeLimitEventData` é o atributo no mínimo ou no máximo: `Value` é o valor atual nesse
  momento, e `Instigator` e `Effect` vêm da alteração que levou ao limite, com as mesmas regras.
- Os payloads são structs: disparar não aloca.

## Relay do GAS {#relay-do-gas}

O `AbilitySystemEventRelay` (assembly `SpellBook.GAS.Bridges`) repassa os eventos C# de um ASC
para [canais do GAS](#canais-do-gas) e para `UnityEvent`s locais. Ponha um por ator, no mesmo
objeto do `AbilitySystemComponent` ou em outro, com o ASC no campo **Ability System**. Com o campo
vazio, o relay usa o ASC do próprio objeto; sem ASC, ele avisa uma vez no Console e não repassa
nada.

Cada evento é um **par** com três campos, todos opcionais. Ligue só o que precisa:

- **Channel:** o canal tipado com os dados completos do evento;
- **Value Channel:** um canal de valor simples (`VoidEventChannel`, `FloatEventChannel`,
  `StringEventChannel` ou `GameObjectEventChannel`), com o valor da tabela abaixo;
- **Response:** um `UnityEvent` deste objeto, chamado depois dos canais.

| Par | Evento do ASC | Float | String | GameObject |
|---|---|---|---|---|
| `Effect Applied` | `OnEffectApplied` | pilhas | nome do efeito | alvo |
| `Effect Removed` | `OnEffectRemoved` (com o `RemovalReason`) | pilhas (0) | nome do efeito | alvo |
| `Effect Stacks Changed` | `OnEffectStacksChanged` | pilhas | nome do efeito | alvo |
| `Effect Blocked` | `OnEffectBlocked` (com o `Result`) | pilhas (0) | nome do efeito | alvo |
| `Ability Activated` | `OnAbilityActivated` | nível | nome da habilidade | dono |
| `Ability Ended` | `OnAbilityEnded` (com o `Cancelled`) | nível | nome da habilidade | dono |
| `Ability Failed` | `OnAbilityFailed` (com o `FailureReason`, a `FailureTag` e a `FailureKey`) | nível (0 se não concedida) | nome da habilidade | dono |
| `Ability Granted` | `OnAbilityGranted` | nível | nome da habilidade | dono |
| `Ability Removed` | `OnAbilityRemoved` | nível | nome da habilidade | dono |
| `Attribute Changed` | `OnValueChanged` de cada atributo | valor novo | nome do atributo | dono |
| `Attribute Decreased` | `OnValueChanged`, só quando o valor cai | variação (negativa) | nome do atributo | dono |
| `Attribute Depleted` | `OnAttributeDepleted` | valor (o mínimo) | nome do atributo | dono |
| `Attribute Reached Max` | `OnAttributeReachedMax` | valor (o máximo) | nome do atributo | dono |
| `Tag Added` | `OnTagChanged`, ao entrar | contagem (1) | nome da tag | dono |
| `Tag Removed` | `OnTagChanged`, ao sair | contagem (0) | nome da tag | dono |
| `Blocked` | `OnBlocked` (`ReportBlocked`) | — | nome da tag | dono |

- O `VoidEventChannel` vale para todos os pares: só avisa que o evento aconteceu. Um canal de valor
  de outro tipo, ou o `FloatEventChannel` no par `Blocked`, não dispara.
- Os atributos usam o `AttributeChangedEventChannel`, com o valor antigo, o novo e o `Delta`
  (novo − antigo). `Attribute Changed` dispara em toda mudança; filtre o atributo na reação.
  `Attribute Decreased` dispara só quando o valor cai e, com o campo **Decreased Attribute**
  preenchido, só para aquele atributo. Ele serve para "dano recebido": o `Delta` é negativo.
- `Attribute Depleted` e `Attribute Reached Max` usam o `AttributeLimitEventChannel` e disparam
  uma vez ao chegar ao limite, de novo só depois de sair dele. Use `Attribute Depleted` para a
  morte, o escudo quebrado ou a mana vazia; filtre o atributo na reação.
- As tags usam o `TagChangedEventChannel`. A tag entra na primeira concessão e sai na última
  remoção; concessões repetidas da mesma tag não disparam de novo.
- O relay acompanha os atributos que o `AttributeSet` tem ao ser habilitado.
- O relay dispara dentro do evento do ASC, no mesmo quadro. Um ouvinte que aplica um efeito no
  mesmo ator dentro do callback é seguro.
- O relay se inscreve ao ser habilitado e se desinscreve ao ser desabilitado ou destruído, em
  qualquer ordem com o ASC. Ele não guarda estado de gameplay.
- Os payloads não alocam; o canal de texto aloca, porque lê o `name` do asset.

### Inspector e validação do relay {#relay-inspector}

O Inspector mostra o campo **Ability System** e, abaixo, os pares em cinco grupos recolhidos:
**Efeitos**, **Habilidades**, **Atributos** (com o **Decreased Attribute** e os pares de mínimo e
máximo), **Tags** e **Bloqueio**. Abra só o grupo do evento que vai ligar.

O validador acusa erro, no topo do Inspector e na janela do
[validador](../ferramentas/validador.md), em:

- um relay sem ASC no campo **Ability System** nem no próprio objeto;
- um **Value Channel** de tipo fora da lista (`Void`, `Float`, `String` ou `GameObject`), por
  exemplo um `IntEventChannel`;
- uma chamada do **Response** sem objeto, ou com objeto e sem função (**No Function**).

## Ligar o GAS ao Events {#ligar-gas-events}

Para que um evento do GAS chegue a outro objeto sem código, ponha um
[relay](#relay-do-gas) no ator e ligue o par do evento a um canal. Qualquer objeto ouve o canal
com um [`EventListener`](#eventlistener).

### Exemplo: som ao ser imune {#exemplo-som-imune}

O efeito de veneno tem a tag `Imune.Veneno` em **Application Immunity Tags**, e o personagem tem
essa tag. Quando alguém aplica o veneno nele, a aplicação é bloqueada (`BlockedByImmunity`) e um
som toca em outro objeto.

1. Crie o canal: **Create > SpellBook > Events > Void Channel**, com o nome `Imune`.
2. No personagem, o objeto com o `AbilitySystemComponent`, use **Add Component >
   AbilitySystemEventRelay**. Deixe o **Ability System** vazio: o relay usa o ASC do objeto.
3. Abra o grupo **Efeitos** e, no par **Effect Blocked**, arraste o canal `Imune` para o
   **Value Channel**.
4. No objeto do som, com um `AudioSource` que tem o clipe, use **Add Component >
   EventListener**. Em **Responses**, adicione uma resposta `Void` e escolha o canal `Imune`.
5. No **Escape** da resposta, adicione uma chamada, arraste o objeto do som e escolha
   **AudioSource > Play ()**.
6. Entre em Play e aplique o veneno no personagem: o som toca.

O par **Effect Blocked** dispara também quando faltam as tags exigidas pelo efeito
(`MissingRequirements`). Para separar os dois casos, use o `EffectEventChannel` no **Channel**:
o campo `Result` diz `BlockedByImmunity` ou `MissingRequirements`.

## EventListener {#eventlistener}

O `EventListener` é o componente que ouve canais sem código. Adicione-o a um objeto
(**Add Component > EventListener**) e, na lista **Responses**, escolha uma resposta para cada
canal que o objeto deve ouvir.

- O componente se inscreve nos canais no `OnEnable` e se desinscreve no `OnDisable`. Desabilitado
  (o componente ou o objeto), ele não recebe disparos; habilitado de novo, volta a receber.
  Desabilitar também cancela as esperas de um [Invoke com delay](#delayedreaction).
- Um disparo do canal roda as **reações** da resposta na ordem da lista e, depois delas, o
  **Escape**.
- Uma resposta sem canal não faz nada e é erro de [validação](#validacao).

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

- **Channel**: o canal ouvido. Só aceita canais do mesmo payload da resposta (veja
  [Escolher o canal](#escolher-canal)).
- **Reactions**: lista de reações, escolhidas pelo seletor de tipo. Cada uma recebe o payload do
  disparo e o `EventListener` que a executa.
- **Escape**: um `UnityEvent` com o payload, num foldout fechado. Chamado depois das reações. Use
  só quando nenhuma reação serve: a ligação pelo `UnityEvent` quebra se o método for
  renomeado.

Uma reação sua herda de `Reaction` e sobrescreve `Execute(EventListener)`, ou
`Execute<T>(EventListener, in T)` quando precisa do payload:

{% code title="Packages/com.spellbook/Tests/Events/EditMode/Examples/EventsManualExamples.cs:using-system,using-events,using-unityengine,tocar-som" %}
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
{% endcode %}

### Escolher o canal {#escolher-canal}

Todo campo de canal no Inspector, nas respostas, nas reações e nos seus scripts, tem ao lado o
botão **Escolher**. Ele abre a lista dos canais do projeto que o campo aceita, por nome, com
**(Nenhum)** no topo para limpar o campo. Abaixo do campo, a **Description** do canal escolhido
aparece num quadro de informação; sem descrição, o quadro some.

A lista segue o tipo de payload do campo:

| Campo | Canais listados |
|---|---|
| **Channel** de uma resposta (`EventChannel<T>`) | só os de payload `T`; numa `FloatEventResponse`, só canais Float |
| **Target** de um [RaiseChannel](#raisechannelreaction) em `ForwardPayload` | só os do payload da resposta que contém a reação, inclusive dentro de um [Invoke com delay](#delayedreaction) |
| **Target** de um RaiseChannel em `RaiseVoid` | só canais Void |
| campo seu do tipo base `EventChannel` | todos os canais |

Arrastar para o campo um canal que a lista não mostraria não tem efeito: o campo continua com o
canal anterior. Trocar o **Mode** de um RaiseChannel não limpa o **Target**; se ele deixar de
servir, a [validação](#validacao) acusa erro. O canal de um [`EventEmitter`](#eventemitter) não usa
este seletor, porque escolher um canal de outro tipo ali troca o tipo do payload.

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

{% code title="Packages/com.spellbook/Tests/Events/EditMode/Examples/EventsManualExamples.cs:using-system,using-events,using-unityengine,dano" %}
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
{% endcode %}

- O canal (`DanoEventChannel.cs`) fica num arquivo com o nome da classe, porque é um asset.
- A `DanoEventResponse` aparece no seletor da lista **Responses**; o **Escape** dela é um
  `UnityEvent<Dano>`.
- Quem dispara usa `danoRecebido.Raise(new Dano { Valor = 10, Ponto = transform.position })`.
- Um [`EventEmitter`](#eventemitter) com um `DanoEventChannel` dispara o `Dano` sem código.
- Marque o struct com `[Serializable]` para o payload aparecer no Inspector do canal e do Escape.

### Validação {#validacao}

O `EventListener` valida cada resposta da lista **Responses**. O validador acusa erro em:

- uma resposta sem **Channel**;
- uma chamada do **Escape** sem objeto (o campo do alvo vazio ou com um objeto que foi apagado);
- uma chamada do **Escape** com objeto, mas sem função escolhida (**No Function**);
- uma reação com campo obrigatório vazio (veja cada [reação pronta](#reacoes-prontas));
- um [RaiseChannel](#raisechannelreaction) em `ForwardPayload` com um canal de outro tipo de
  payload, inclusive dentro de um [Invoke com delay](#delayedreaction).

Uma chamada do Escape sem objeto e sem função gera um erro só, o do objeto. Escolhido o objeto,
o erro da função aparece se ela ainda faltar. Uma resposta vazia na lista (sem tipo escolhido)
não é validada.

## EventEmitter {#eventemitter}

O `EventEmitter` é o componente que dispara um canal sem código. Ele guarda uma **emissão**: o
canal e o payload fixo enviado a cada disparo. O tipo do payload vem do canal: um canal Float
pede um número, um canal de payload próprio pede os campos do seu struct, e um canal Void não
pede nada.

Para disparar, chame o método público `Raise()` do componente:

- **Por um `UnityEvent`**: no `OnClick` de um botão, por exemplo, arraste o objeto com o
  `EventEmitter` e escolha **EventEmitter > Raise ()**.
- **Por animação**: num evento de animação de um clipe do objeto, escolha a função `Raise`.

Cada chamada dispara o canal uma vez, e o emissor aparece como quem disparou no log do canal.
Sem emissão ou sem canal, `Raise()` não faz nada, e o validador acusa o
erro.

No Inspector do `EventEmitter`:

- **Channel**: o canal disparado. Aceita um canal de qualquer payload.
  - Um canal com o **mesmo** tipo de payload do anterior troca só o canal e mantém o payload.
  - Um canal de **outro** tipo recria a emissão: o campo do payload passa a ser do tipo novo, com
    o valor padrão, e o payload anterior se perde.
  - Esvaziar o campo mantém a emissão e o payload, sem canal; o validador acusa o erro.
- **Payload**: o valor enviado a cada disparo, no tipo do canal. Um canal Void não mostra o campo.
- **Raise**: chama `Raise()` do emissor. Só fica ativo no Play.

Por código de Editor, a emissão de um canal é criada com `EventEmission.CreateFor(canal)`.

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
