# 0016 — Sistema de eventos unificado

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

O pacote tem três sistemas de eventos que se sobrepõem:

- `Runtime/Core/Events/` (`GameEvent`, `GameEventListner`);
- `Runtime/Core/NewEventSystem/` (eventos em asset tipados e listeners);
- `Runtime/Systems/Events/` (`EventManager` estático com structs, `GameEvent`/`GameEventListener` por string e `GAS_Events.cs`, que faz o barramento depender do GAS).

O `AbilitySystemComponent` chama o `EventManager` direto. O `EventManager` não é limpo com o domain reload desligado, e os ouvintes vazam entre sessões de Play.

## Decisão

1. Um único módulo, `SpellBook.Events`, com **três usos** e uma regra:

   | Uso | Quando | Forma |
   |---|---|---|
   | Canal em asset | Fato global que o designer liga no Inspector, sem código | `ScriptableObject` de canal, sem payload ou tipado |
   | Barramento de código | Evento global só entre sistemas de código | Barramento estático por tipo de struct, sem alocação |
   | Evento C# por instância | Estado de **uma** entidade | `event` C# no próprio componente (fora do módulo Events) |

   **Regra: estado por instância nunca passa por canal nem por barramento.** A vida de um personagem ou a recarga de um ASC são lidas do próprio componente.

2. **Canal:** disparo por código e por botão no Play, lista de ouvintes vivos, último payload e log opcional; iteração segura contra reentrada; em desenvolvimento, uma exceção num ouvinte não interrompe os outros.
3. **Ouvinte:** um único componente com várias respostas; cada resposta tem o canal, reações tipadas por `[SerializeReference]` (0013) e um `UnityEvent` opcional, marcado como escape.
4. **Emissor:** componente que dispara um canal a partir de `UnityEvent`, botão ou animação.
5. Registros estáticos são limpos em `RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)`.
6. O GAS só expõe eventos C# por instância (0012). A ponte para canais é um relay opcional por ator em `SpellBook.GAS.Bridges`.
7. O módulo nasce com o Event Debugger (canais, ouvintes, disparos) e "Find references" (quem emite e quem ouve).
8. Os três sistemas atuais são removidos sem compatibilidade (0009).

## Motivo

- Um jeito de fazer cada coisa, com regra escrita sobre qual usar.
- Separar global de por instância evita o acoplamento escondido de canais globais carregando dados de uma entidade.
- O debugger compensa a perda de rastreabilidade de ligações feitas no Inspector.

## Consequências

- Assets de eventos da 2.x precisam ser recriados (guia de migração).
- Criar um canal com payload próprio exige um struct e duas classes de uma linha (canal e resposta); um wizard que gera esse código fica para a Fase 9.
- A ordem dos ouvintes não é garantida, e isso é documentado.
- O `SendGameplayEvent` da Fase 7 é semântica do GAS, não deste módulo.

## O que os agentes NÃO podem fazer

- Publicar estado de uma entidade em canal ou barramento global.
- Fazer o GAS (ou qualquer módulo de infraestrutura) referenciar `SpellBook.Events`, exceto as pontes.
- Fazer a UI ler dados de um ator por canal ou relay; ela lê o componente pela fonte (0017).
- Criar outro sistema de eventos paralelo.
- Depender da ordem de chamada dos ouvintes.

## Decisões incorporadas (2026-10-29)

Decisões do usuário: I28 (reações prontas), I36 (padrões do Controlador), I38 (Events referencia Time; Invoke com delay), I39 (`RaiseEventAction`), I40 (duas formas da RaiseChannel). As demais são técnicas, do Arquiteto.

- **`try/catch` por ouvinte só em desenvolvimento** (I36): `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. Em release, a exceção sobe e interrompe o disparo.
- **Canais prontos** (I36): Void, Int, Float, Bool, String, Vector3 e GameObject, sem `Component`.
- **Find references** (I36): varre prefabs, ScriptableObjects e cenas abertas; não abre cenas fechadas.
- **Reações prontas** (I28, I38, I40): SetActive, RaiseChannel (forma "repassar o payload a um canal do mesmo tipo" ou "disparar um canal sem payload"), Log, Toggle de componente e Invoke com delay (atraso contado por um `TimeSourceAsset` do Inspector, padrão escalado; roda uma lista de reações; cancelado quando o ouvinte é desabilitado).
- **`SpellBook.Events` referencia Core e Time** (I38; emenda na 0012).
- **`RaiseEventAction`** (I39), em `SpellBook.GAS.Bridges`: canal sem payload ou `EventChannel<T>` com payload fixo no Inspector. Payload dinâmico fica no B-004.
- **`.meta` de `VoidEvent.cs` não é mantido** (técnica): o arquivo e o `.meta` são apagados e o canal novo nasce com GUID novo. Motivo: a 0009 dispensa compatibilidade, e reaproveitar o GUID faria assets antigos abrirem como o tipo novo com campos diferentes, um erro silencioso pior que a referência ausente.
- **Nomes** (técnica): `EventChannel` é a base abstrata **não genérica** (descrição, `logRaises`, tipo do payload, ouvintes vivos para o debugger) e `EventChannel<T> : EventChannel` a base tipada. O canal sem payload concreto é `VoidEventChannel : EventChannel<Void>`, com `Raise()` sem argumento. (Desvio de nome em relação ao R12 do PRD, que chama o canal sem payload de `EventChannel`; ver Spec.)
- **Reações** (técnica): base `[Serializable] abstract class Reaction` não genérica, com `public virtual void Execute<T>(EventListener listener, in T payload)` cujo padrão chama `Execute(EventListener listener)`. A resposta guarda `[SerializeReference] List<Reaction>`. Uma reação que precisa do payload (a RaiseChannel na forma "repassar") sobrescreve a versão genérica. Assim as reações prontas servem a qualquer tipo de payload sem uma classe por tipo, e a lista não precisa de genéricos em `[SerializeReference]` (0013).
- **Emissor** (técnica): `EventEmitter` guarda `[SerializeReference] EventEmission`, onde `EventEmission<T>` (canal + payload fixo) é criada pelo código a partir do tipo do canal escolhido (0013). O mesmo `EventEmission` é usado pela `RaiseEventAction`.
- **Iteração segura** (técnica): lista de ouvintes com contagem fixada no início do disparo, desinscrição durante o disparo marca a posição como vazia e a compactação acontece ao fim do disparo mais externo. Ouvinte inscrito durante um disparo só recebe o próximo. Sem cópia da lista, então sem alocação.
- **Registro limpo** (técnica): cada `EventBus<T>` fechado e cada canal se registram num registro não genérico do módulo; `SubsystemRegistration` limpa os ouvintes de todos (CA19). O registro em si não é apagado, só os ouvintes.
- **Seletores filtrados** (técnica): o filtro "só canais do payload desta resposta" (CA21) e "só canais do mesmo tipo" na RaiseChannel (CA58) ficam num drawer do `SpellBook.Events.Editor`, porque o runtime não pode referenciar `UnityEditor` (R3). A lógica do filtro é uma função pública e testável do Editor.

## Em aberto

Nada.

## Relacionadas

- 0009 (remoção sem compatibilidade), 0012 (módulos), 0013 (reações), 0017 (UI não usa canal para dado de ator).
