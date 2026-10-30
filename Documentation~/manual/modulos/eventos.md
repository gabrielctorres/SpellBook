# Eventos {#eventos}

O módulo Eventos serve para um sistema avisar outros de que algo aconteceu sem que um conheça o
outro. Quem dispara não sabe quem ouve, e quem ouve não guarda referência para quem dispara.

O módulo fica no assembly `SpellBook.Events` (namespace `SpellBook.Events`). Um asmdef seu que usa
os eventos precisa referenciar o `SpellBook.Events`. Veja [Conceitos](../inicio/conceitos.md#modulos).

!!! note "Em desenvolvimento"
    Nesta versão de desenvolvimento da 3.0, o módulo tem só o barramento de código
    (`EventBus<T>`). Os canais em asset e o componente ouvinte chegam nas próximas etapas.

## Três usos e uma regra {#tres-usos}

Existem três jeitos de avisar que algo aconteceu. Cada um tem o seu caso:

| Uso | Quando usar | Forma |
|---|---|---|
| Canal em asset | Fato global que o designer liga no Inspector, sem código | `ScriptableObject` de canal (em breve) |
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

Ao entrar em Play, todo `EventBus<T>` fica sem ouvintes. Isso acontece com e sem domain reload
(opção **Enter Play Mode Options**), pelo
`RuntimeInitializeOnLoadMethod(SubsystemRegistration)`. Um ouvinte de uma sessão anterior que não
se desinscreveu não é chamado na sessão seguinte.
