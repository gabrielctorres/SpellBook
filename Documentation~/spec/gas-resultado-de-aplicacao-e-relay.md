# Spec: Resultado de aplicação e relay de eventos do GAS
PRD: [Documentation~/prd/gas-resultado-de-aplicacao-e-relay.md](../prd/gas-resultado-de-aplicacao-e-relay.md) · Backlog: B-004 · ADRs: **0031 (nova, Proposta)**; usa 0003, 0004, 0007, 0012, 0013, 0016, 0017, 0026

Status: Aprovada no gate de 2026-10-31 (RL1–RL7; ADR 0031 aceita; Q2: sim) · Data: 2026-10-31 · Autor: Arquiteto
Emenda de 2026-11-01 (perguntas do RL6), **pendente de gate parcial**: o lado do efeito do payload dinâmico sai do RL6 e vai para o B-012 (EV2 e EV15). Ver Design 4, "Emenda de 2026-11-01".
Emenda de 2026-11-01 (pergunta do RL7), aprovada no gate parcial de 2026-11-01 junto com a emenda de mesma data da ADR 0026 (RL7a, issue #280): o drawer de tipo não roda sob o `[TypePicker]`. Entra o ticket RL7a, antes do RL7. Ver Design 5 e RL7a.

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`.

- `Runtime/GAS/Core/AbilitySystemComponent.cs:448-478`: `ApplyEffect` devolve `null` no instantâneo e nos bloqueios (requisito em `:458`, imunidade em `:459`, nessa ordem). O NC4 troca o corpo por `ApplyEffectCore`, com um ramo por desfecho, inclusive "renovado sem pilha" separado de "empilhado" (Spec do B-002, seção 2.2, ganchos). O EA3 cria os eventos de efeito e o `EffectRemovalReason`; o EA5, os de concessão; o EA1, `AttributeSet.States` e `GetTagCount`.
- Eventos do ASC que já existem: `OnTagChanged(GameplayTag, bool)`, `OnAbilityActivated(AbilitySpec)`, `OnAbilityEnded(AbilitySpec, bool)`, `OnAbilityFailed(GameplayAbility, AbilityActivationResult)` (`:34-37`). `AttributeState.OnValueChanged(float antigo, float novo)` (`AttributeState.cs:17`) já entrega os dois valores (P4).
- `Runtime/GAS.Bridges/RaiseEventAction.cs`: guarda `[SerializeReference] EventEmission emission` e chama `emission.Raise(context.Source)`. O asmdef do Bridges referencia Core, Tags, Time, GAS e Events (`AsmdefRulesTests.cs:231-247`).
- `Runtime/Events/EventEmission.cs:52-80`: `EventEmission<T>` com canal e payload fixo; `Channel` e `PayloadType` públicos. `EventChannel<T>.Raise(T, Object)` (`EventChannel.cs:124`). `EventResponse<T>` com reações e `UnityEvent<T>` de escape (`EventResponse.cs:40-117`). Canais prontos: Void, Int, Float, Bool, String, Vector3, GameObject. O Inspector `EventChannelEditor` cobre toda subclasse de `EventChannel` (`[CustomEditor(typeof(EventChannel), true)]`).
- Não existe asmdef de Editor para o Bridges. Guardas: `SpellBookEditorCoverageTests` (todo `MonoBehaviour` público do Bridges precisa de Inspector `SpellBookEditor`), `AsmdefRulesTests` (lista de módulos de Editor em `:23`; Editor de módulo não referencia Editor de outro módulo, `:304-327`).
- Testes: `Tests/GAS.Bridges/EditMode` e `PlayMode` (referenciam GAS, Events, Bridges e Testing); `Tests/Testing/EventsFactory.cs`.
- `Documentation~/manual/modulos/eventos.md` e `gas.md` (`#raiseeventaction`).

## Respostas às perguntas técnicas do PRD (seção 8)

- **P3. Reaplicar sem `CanStack`.** Valor novo **`Refreshed`**, no fim do enum. `Stacked` vale para toda reaplicação de efeito com `CanStack` ligado (inclusive no `MaxStacks`, quando a contagem não sobe; a renovação segue o `RefreshDurationOnStack`, NC4). `Refreshed` vale para a reaplicação com `CanStack` desligado (renovação, NC4). A instância vem preenchida em `Applied`, `Stacked` e `Refreshed`. É o ramo que o NC4 já separa.
- **P4. Valor antigo do atributo.** `AttributeState.OnValueChanged` já entrega `(antigo, novo)`. O relay se inscreve em cada estado de `AttributeSet.States` (EA1) e calcula a variação sem guardar nada.
- **P5. Payload dinâmico.** Enum `GasPayloadSource` no Bridges, ao lado da `EventEmission` (ADR 0031, item 4). Tabela de fontes no Design 4.
- **P6. Estrutura dos pares.** Campos fixos, um `RelayPair<T>` por evento (ADR 0031, item 2). Custo: um objeto por par, criado com o componente; nenhum custo por evento sem canal e sem ouvinte além do teste de nulo.
- **P7. Ordem.** O relay dispara **dentro** do evento C# do ASC, no mesmo quadro (ADR 0031, item 3).

## Design

### 1. Resultado de aplicação e bloqueio (R1 a R7)

```csharp
public enum EffectApplicationResult   // Runtime/GAS/Effects/EffectApplicationResult.cs
{ Applied, Stacked, AppliedInstant, BlockedByImmunity, MissingRequirements, Invalid, Refreshed }
```

No ASC:
- `EffectApplicationResult TryApplyEffect(GameplayEffect effect, AbilitySystemComponent source, out ActiveEffectInstance instance)` e `TryApplyEffect(GameplayEffect effect, float durationOverride, AbilitySystemComponent source, out ActiveEffectInstance instance)`. São o `ApplyEffectCore` do NC4 com o desfecho exposto. Ordem das checagens igual à de hoje: efeito nulo (`Invalid`), requisitos (`MissingRequirements`), imunidade (`BlockedByImmunity`), instantâneo (`AppliedInstant`), existente com `CanStack` (`Stacked`), existente sem `CanStack` (`Refreshed`), novo (`Applied`).
- `ApplyEffect` (as duas sobrecargas) passa a chamar `TryApplyEffect` e devolver a instância. Mesma assinatura e mesmo retorno de antes (R3, CA4).
- `event Action<GameplayEffect, AbilitySystemComponent, EffectApplicationResult> OnEffectBlocked`: dispara em `BlockedByImmunity` e `MissingRequirements`, depois de decidir, com a origem já resolvida (`source ?? this`). Não dispara em `Invalid` (R6).
- `void ReportBlocked(GameplayTag reason, AbilitySystemComponent source = null)` e `event Action<GameplayTag, AbilitySystemComponent> OnBlocked`. Tag inválida é ignorada. Origem nula vira `this`.
- Sem alocação nova: o `out` e o enum não alocam (R5, CA8).

### 2. Payloads e canais tipados (R10)

Em `Runtime/GAS.Bridges/Payloads/GasEventPayloads.cs`, cinco `[Serializable] public struct` com campos públicos (o canal guarda `debugPayload` e `LastPayload`):

| Struct | Campos |
|---|---|
| `EffectEventData` | `Owner` (ASC alvo), `Source` (ASC), `Effect`, `Stacks`, `PreviousStacks`, `RemovalReason`, `Result` |
| `AbilityEventData` | `Owner`, `Ability`, `Level`, `Cancelled`, `FailureReason` (`AbilityActivationResult`) |
| `AttributeChangedData` | `Owner`, `Attribute`, `OldValue`, `NewValue`, `Delta` (gravado como `NewValue - OldValue`) |
| `TagChangedData` | `Owner`, `Tag`, `Added`, `Count` |
| `BlockedEventData` | `Owner`, `Reason` (`GameplayTag`), `Source` |

`BlockedEventData` não está na lista do R10. Ele entra para o `OnBlocked` genérico (R7), que não cabe em nenhum dos quatro (ver a pergunta Q2 ao usuário).

Para cada struct: um canal `XEventChannel : EventChannel<X>` em arquivo próprio (`Runtime/GAS.Bridges/Channels/`), com `[CreateAssetMenu(menuName = "SpellBook/GAS/Events/X Channel")]` e `[HelpURL(DocUrls.Events + "#canais-do-gas")]`, e uma resposta `XEventResponse : EventResponse<X>`, `[TypeCategory("Respostas")]`, todas em `Runtime/GAS.Bridges/Channels/GasEventResponses.cs`. O seletor do `EventListener` já lista respostas de qualquer assembly, filtradas pelo tipo (ADR 0016).

Emenda de 2026-11-01 (pergunta do RL2): o menu é `SpellBook/<Módulo>/…`, e o prefixo `SpellBook/Events/` pertence ao módulo Events, cujo conjunto fechado de sete canais o `Tests/Events/EditMode/ChannelMenuTests.cs:17` (`Menu_HasExactlySevenChannels_NoComponent`) fixa em todos os assemblies. Os canais do GAS ficam sob `SpellBook/GAS/Events/`, do módulo que os declara; o Events não passa a conhecer o GAS e o `ChannelMenuTests` não muda.

### 3. `AbilitySystemEventRelay` (R8, R9, R11 a R14)

`Runtime/GAS.Bridges/Relay/AbilitySystemEventRelay.cs`, `MonoBehaviour`, `[DefaultExecutionOrder(100)]`, `[HelpURL(DocUrls.Events + "#relay-do-gas")]`.

- `[SerializeField] private AbilitySystemComponent abilitySystem;` vazio usa `GetComponent` no `OnEnable`. Sem ASC: aviso único no Console e nada inscrito.
- `RelayPair<T>` (`Runtime/GAS.Bridges/Relay/RelayPair.cs`, `[Serializable] public sealed class`): `[SerializeField] EventChannel<T> channel`, `[SerializeField] EventChannel valueChannel`, `[SerializeField] UnityEvent<T> response`. `internal void Raise(in T data, Object sender, ValueSelector)`: canal tipado, depois canal de valor, depois `UnityEvent`, cada um só se configurado. O canal de valor é disparado por teste de tipo (`EventChannel<float>`, `EventChannel<string>`, `EventChannel<GameObject>`, `EventChannel<Void>`), sem alocação.
- Pares, agrupados por `[Header]`, com o valor simples de cada um:

| Grupo | Par (evento do ASC) | Float | String | GameObject |
|---|---|---|---|---|
| Efeitos | `effectApplied`, `effectRemoved`, `effectStacksChanged` (EA3), `effectBlocked` (1) | `Stacks` | nome do efeito | alvo |
| Habilidades | `abilityActivated`, `abilityEnded`, `abilityFailed`, `abilityGranted`, `abilityRemoved` (EA5) | `Level` | nome da habilidade | dono |
| Atributos | `attributeChanged` | `NewValue` | nome do atributo | dono |
| Atributos | `attributeDecreased`, com filtro opcional `decreasedAttribute` | `Delta` (negativo) | nome do atributo | dono |
| Tags | `tagAdded`, `tagRemoved` | `Count` (`GetTagCount`, EA1) | nome da tag | dono |
| Bloqueio | `blocked` (`OnBlocked`) | — | nome da tag | dono |

  O canal `Void` vale para todos (só o fato). Os textos usam `Object.name`, que aloca na Unity: o canal de texto aloca, e o manual diz isso (N2 fala do payload struct).
- `attributeDecreased` dispara só quando `NewValue < OldValue` e, com filtro, só para aquele atributo (R12, CA14). `attributeChanged` não tem filtro (o designer filtra na reação).
- Ciclo de vida (R13): inscreve em `OnEnable` e desinscreve em `OnDisable`; `OnDestroy` chama a mesma desinscrição, idempotente (guarda o ASC inscrito em `_subscribed`). Os handlers são delegates criados uma vez no `Awake`; os de atributo, uma lista criada no `OnEnable` (uma alocação por ativação). Desinscrever de um ASC destruído não lança, porque o objeto C# ainda existe. `OnValidate` em Play, com o componente ativo, refaz a inscrição se o campo mudou.
- Reentrada (R14): o relay não itera nada próprio durante o disparo. O evento C# do ASC usa um delegate imutável durante a chamada, e os canais usam `ListenerList` (0016).
- Só no Editor (`#if UNITY_EDITOR`): um buffer circular dos últimos 10 disparos (par, quadro) para o Inspector.

### 4. Ações com dados do GAS (R15 a R17)

- `public enum GasPayloadSource { Fixed, AttributeValue, ModifierMagnitude, Stacks, EffectName, AbilityName, SourceObject, TargetObject, EventData }` (`Runtime/GAS.Bridges/GasPayloadSource.cs`), com `public static bool Supports(GasPayloadSource, Type payloadType, bool isEffectAction)` e as funções de valor.
- `RaiseEventAction` ganha `[SerializeField] GasPayloadSource payloadSource` (padrão `Fixed`), `AttributeDefinition attribute` e `int modifierIndex`.
- `RaiseEventEffectAction : EffectAction` (`Runtime/GAS.Bridges/RaiseEventEffectAction.cs`, `[TypeCategory("Eventos")]`), com a mesma `EventEmission` e os mesmos três campos. Fica disponível no seletor das três listas de ações do efeito.

| Fonte | Canal | Ação de habilidade | Ação de efeito |
|---|---|---|---|
| `Fixed` | qualquer | `emission.Raise` (0-M) | `emission.Raise` |
| `AttributeValue` | Float | atributo do conjurador | atributo do alvo |
| `ModifierMagnitude` | Float | — | valor do modificador `modifierIndex`, de `PeriodicModifiers` na lista de tique e de `Modifiers` nas outras, avaliado como o ASC avalia (`ValueSource.GetValue(origem, alvo)`) |
| `Stacks` | Float | — | `instance.CurrentStacks` (instantâneo: 1) |
| `EffectName` | String | — | nome do efeito |
| `AbilityName` | String | nome da habilidade | — |
| `SourceObject` / `TargetObject` | GameObject | conjurador / primeiro alvo válido | origem / alvo |
| `EventData` | `EffectEventData` ou `AbilityEventData` | `AbilityEventData` | `EffectEventData` |

  "O dano do tique" do CA22 é o `ModifierMagnitude` do modificador periódico. O B-007 (VN4) muda a avaliação dos modificadores para usar o contexto; ele atualiza esta fonte no mesmo ticket.
- Fonte incompatível com o tipo do canal: a ação não dispara nada em runtime (sem exceção), e o validador acusa (R19). Sem lógica nem estado na ação (R17).

**Emenda de 2026-11-01 (perguntas do RL6; pendente de gate parcial).** O texto acima vale como destino; a entrega muda.

- **O problema.** A `EffectAction` de hoje (`Runtime/GAS/Effects/EffectAction.cs:16`) recebe só origem, alvo e instância. No instantâneo, o ASC a chama sem instância (`AbilitySystemComponent.cs:995`), então a ação não sabe qual é o efeito; ela também não recebe o `EffectContext` (o nível) nem o evento (aplicado, tique, removido). As saídas que a Fábrica levantou são adivinhação: não disparar no instantâneo, avaliar no nível 1 (proibido pela ADR 0030: o nível só vem do `EffectContext`; o `ModifierValueSource.Evaluate(in EffectContext)` é o caminho do ASC) e descobrir o tique procurando a ação em `OnTickActions`.
- **O caminho limpo já está aprovado.** O B-012 (ADR 0034, item 4; Spec `gas-evento-acoes-alvos.md`, seção 1) apaga a `EffectAction` no EV3 e passa a toda ação um `ActionContext` com `EffectInstance` e o `EffectContext` por cópia, e o EV15 funde a `RaiseEventEffectAction` na `RaiseEventAction`. Dar agora à `EffectAction` um contexto próprio criaria um tipo que o EV3 apaga, e escrever a `RaiseEventEffectAction` criaria uma classe que o EV15 apaga: dois mecanismos de transição.
- **Decisão.** A `RaiseEventEffectAction` não é criada. As fontes do lado do efeito (`ModifierMagnitude`, `Stacks`, `EffectName` e `EventData` com `EffectEventData`) e os CA21 e CA22 são entregues no EV15, sobre o `ActionContext`. O RL6 entrega o `GasPayloadSource` e o payload dinâmico da `RaiseEventAction` de habilidade, e o CA23. Lacuna do B-012 fechada na mesma emenda (lá, seção 1): o `ActionContext` ganha o asset do efeito (também no instantâneo) e o evento do efeito que disparou a lista (`Tick` escolhe `PeriodicModifiers`).
- **Respostas às três perguntas.** (1) Instantâneo: a ação dispara, com o asset e o `EffectContext` vindos do `ActionContext`; `Stacks` vale 1. (2) Nível: o do `EffectContext`, por `ModifierValueSource.Evaluate(in EffectContext)`, nunca o nível 1. (3) Tique: pelo evento no `ActionContext`, nunca procurando a ação na lista.
- **Enum por partes, ordem estável para a serialização:** o RL6 cria `GasPayloadSource { Fixed, AttributeValue, AbilityName, SourceObject, TargetObject, EventData }`; o EV15 acrescenta `ModifierMagnitude, Stacks, EffectName` no fim. `Supports(GasPayloadSource, Type payloadType)` no RL6; o EV15 acrescenta a checagem "só em lista de efeito" (`ModifierMagnitude_OnlyInEffectContext_ValidatorWhenNotSupported`, já previsto lá).
- A validação de `modifierIndex` (seção 5) vai com o `ModifierMagnitude` para o EV15. A frase "O B-007 (VN4) … atualiza esta fonte" acima perde o objeto: a fonte já nasce no contexto.

### 5. Editor (R18, R19)

- Asmdef `Editor/GAS.Bridges/SpellBook.GAS.Bridges.Editor.asmdef` (ADR 0031, item 1).
- `AbilitySystemEventRelayEditor : SpellBookEditor`: em `BuildBeforeDefault`, desenha os pares por grupo em `Foldout` fechados (e não usa o Inspector padrão para eles); em `BuildAfterDefault`, em Play, os últimos disparos. O canal tipado já é filtrado pelo tipo do campo; o canal de valor usa `ObjectField` de `EventChannel`, e o validador acusa tipo fora da lista.
- `RaiseEventDrawer` (`PropertyDrawer` para `RaiseEventAction` e `RaiseEventEffectAction`): `PropertyField` da emissão (o drawer do Events aplica-se sozinho), um `PopupField` de fontes com só as suportadas pelo `PayloadType` do canal, o campo de atributo ou de índice quando a fonte pede, e um `HelpBox` "o payload fixo é ignorado" quando a fonte não é `Fixed`.
- **Emenda de 2026-11-01 (pergunta do RL7).** A frase "aplica-se sozinho" fala só da emissão, que fica num campo sem `[TypePicker]`. O `RaiseEventDrawer`, ao contrário, nunca rodaria em `GameplayAbility.Actions` (`Runtime/GAS/Abilities/GameplayAbility.cs:68-70`, `[SerializeReference][TypePicker]`): o drawer de atributo vence o de tipo, e o `TypePickerDrawer` desenha os filhos com `PropertyField` próprios (`Editor/Core/Inspector/TypePickerDrawer.cs:70-78`). Tirar o `[TypePicker]` quebra o CA63 (`TypePickerUsageTests.cs:21-35`). Um drawer no enum que lesse os campos ao lado, ou um drawer que só funciona chamado direto, seria remendo. **Decisão:** o `TypePickerDrawer` usa o drawer do tipo concreto quando ele existir (emenda de 2026-11-01 da ADR 0026), no ticket RL7a. O `RaiseEventDrawer` continua um `[CustomPropertyDrawer(typeof(RaiseEventAction))]` comum no `SpellBook.GAS.Bridges.Editor`. Ele recebe a propriedade da ação, não usa `fieldInfo` nem `attribute`, e serve também para a lista de ações do efeito no EV15, sem mudança.
- `RaiseEventAction` ganha `[HelpURL(DocUrls.Gas + "#raiseeventaction")]` (CA28; a classe não tinha). O `GasPayloadSource` é enum e não leva `[HelpURL]`.
- Validação (`IValidatable`, 0026):
  - relay: sem ASC no campo nem no objeto; canal de valor de tipo fora da lista; `UnityEvent` com chamada sem alvo ou sem método (mesmo texto do `EventResponse`);
  - ações: fonte não suportada pelo tipo do canal; `AttributeValue` sem atributo; `modifierIndex` negativo.
  - `modifierIndex` além do tamanho da lista não é acusado pelo validador, porque a ação não conhece o efeito que a contém. Em runtime, a ação não dispara e avisa uma vez no Console.

### 6. O que não muda

- `ApplyEffect`: assinatura, retorno e comportamento (CA4). `RaiseEventAction` com payload fixo e assets existentes (campo novo com padrão `Fixed`, N5).
- O GAS não referencia o Events (N1, CA24). O módulo Events não muda.

## Quebras de compatibilidade

Nenhuma. Adicionado (CHANGELOG): `TryApplyEffect`, `EffectApplicationResult`, `OnEffectBlocked`, `ReportBlocked`/`OnBlocked`, payloads e canais do GAS, `AbilitySystemEventRelay`, payload dinâmico na `RaiseEventAction` (lado da habilidade; o lado do efeito entra no EV15, emenda de 2026-11-01), asmdef `SpellBook.GAS.Bridges.Editor`. A `RaiseEventEffectAction` não é publicada. Alterado (RL7a): o seletor de tipo passa a usar o drawer do tipo concreto, quando existe. Um drawer de tipo do usuário que antes era ignorado sob o `[TypePicker]` passa a ser usado. Isso não quebra API nem dados, e o CHANGELOG avisa.

## Tickets

Todos usam a Unity e seguem o WIP 1. **Não há par paralelo sem Editor nesta Spec.** Cada ticket escreve a sua parte do manual (`gas.md` ou `eventos.md`) e do `CHANGELOG.md`. Tempo por `ManualTimeSource` ou `asc.Tick`; objetos por `GasFactory` e `EventsFactory` (N3). Teto: ~500 linhas e 10 arquivos.

**Conflitos com a fila:** `RaiseEventAction.cs` e os testes do Bridges: o RP-04 edita `RaiseEventActionTests.cs` e `RaiseEventAbilityTests.cs`; nenhum ticket daqui edita esses dois arquivos (os testes novos vão em arquivos novos), mas o RL6 edita `RaiseEventAction.cs` e entra depois do RP-04 para não misturar revisões. O RL4 edita `manual/exemplos/sandbox.md`, do SB-T6, e entra depois dele. `AsmdefRulesTests.cs` (RL3) não está em nenhum ticket da fila. `validador.md` é tocado por TG4 e EA6: em série.

### RL1 — `TryApplyEffect`, `EffectApplicationResult` e bloqueios
- Escopo: `Runtime/GAS/Effects/EffectApplicationResult.cs` (novo), `Runtime/GAS/Core/AbilitySystemComponent.cs`, `Tests/GAS/PlayMode/EffectApplicationResultTests.cs` (novo), `gas.md` (`#gameplayeffect`, "Resultado da aplicação"), `CHANGELOG.md`.
- Testes red: um por valor do enum (`Applied`, `Stacked`, `AppliedInstant`, `BlockedByImmunity`, `MissingRequirements`, `Invalid`, `Refreshed`) (CA1, C1); `Instance_FilledOnlyInAppliedStackedRefreshed` (CA2); `Null_Invalid_NoException` (CA3); `ApplyEffect_SameReturnAsBefore_EachCase` (CA4); `InstantNeverHasInstance_DurationNeverInstant` (CA5); `ReapplyWithoutCanStack_Refreshed_SingleInstance` (CA6); `Enum_Order` (CA7); `TryApplyInstant_DoesNotAllocate_10000Times` (CA8); `Blocked_FiresOncePerReason_NotOnOthers` (CA9); `ReportBlocked_FiresWithTagAndSource_InvalidIgnored` (CA10).
- Critério de pronto: CA1 a CA10 verdes; suíte do GAS sem edição (CA30).
- Tamanho estimado: ~420 linhas; 5 arquivos. Depende de: EA3 (mesmo ponto do ASC), NC4.

### RL2 — Payloads e canais tipados do GAS
- Escopo: `Runtime/GAS.Bridges/Payloads/GasEventPayloads.cs`, os cinco `Runtime/GAS.Bridges/Channels/<X>EventChannel.cs`, `Runtime/GAS.Bridges/Channels/GasEventResponses.cs`, `Tests/GAS.Bridges/EditMode/GasChannelsTests.cs` (novo), `eventos.md` (`#canais-do-gas`), `CHANGELOG.md`.
- Testes red: `GasChannels_HaveCreateMenuAndHelpUrl` (CA20, no padrão do `ChannelMenuTests`: os cinco canais em `SpellBook/GAS/Events/X Channel`, nenhum sob `SpellBook/Events/`; o `Menu_HasExactlySevenChannels_NoComponent` continua verde sem mudança); `GasResponses_ListedForMatchingChannelType` (CA20); `EffectEventData_RaiseDoesNotAllocate_AfterWarmup` (N2); `AsmdefRules` verde (o Bridges continua sem Pooling).
- Critério de pronto: CA20 verde; tipos com `[HelpURL]` para âncora existente.
- Tamanho estimado: ~300 linhas; 10 arquivos. Depende de: RL1 (o `EffectRemovalReason` vem do EA3, anterior ao RL1).

### RL3 — Relay: núcleo, efeitos e bloqueios
- Escopo: `Runtime/GAS.Bridges/Relay/AbilitySystemEventRelay.cs` (novo; pares de efeito e `blocked`), `Runtime/GAS.Bridges/Relay/RelayPair.cs` (novo), `Editor/GAS.Bridges/SpellBook.GAS.Bridges.Editor.asmdef` (novo), `Editor/GAS.Bridges/GasBridgesEditors.cs` (novo; Inspector de uma linha para a cobertura), `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs` (módulo na lista de Editor e teste das referências exatas), `Tests/GAS.Bridges/PlayMode/EventRelayTests.cs` (novo), `eventos.md` (`#relay-do-gas`), `CHANGELOG.md`.
- Testes red: `Blocked_RaisesChannel_ListenerReacts` (CA11, C5); `UnityEventWithoutChannel_AndChannelWithoutUnityEvent` (CA12); `EffectAppliedRemovedStacks_Relayed` com o motivo (CA19, parte de efeito); `DisableAndDestroy_Unsubscribe_DestroyAscFirst_NoException` (CA17, C9); `ListenerAppliesEffectInsideCallback_NoExceptionNoRepeat` (CA18); `GasBridgesEditorAsmdef_ReferencesExactly` e `AllExpectedAssembliesExist` com o módulo novo (CA24).
- Critério de pronto: CA11, CA12, CA17, CA18, CA24 e a parte de efeito do CA19 verdes; `SpellBookEditorCoverageTests` verde.
- Tamanho estimado: ~480 linhas; 8 arquivos. Real: 578 linhas; 8 arquivos, pela exceção única da ADR 0003 (emenda de 2026-11-01, RL3, margem de ~30%); o escopo acima não muda. Depende de: RL2.

### RL4 — Relay: habilidades, atributos e tags
- Escopo: `AbilitySystemEventRelay.cs` (pares restantes), `Tests/GAS.Bridges/PlayMode/EventRelayTests.cs`, `eventos.md`, `manual/exemplos/sandbox.md` (nota: o Sample pode trocar o `DamageTakenRelay` pelo par `attributeDecreased`, CA29), `CHANGELOG.md`.
- Testes red: `AttributeChanged_RaisesFloatWithNewValue` (CA13, C6); `AttributeDecreased_OnlyOnDrop_NegativeDelta_FilterIgnoresOthers` (CA14, C7); `TagAddedRemoved_WithCount` (CA15); `AbilityActivatedEndedFailed_OnceEach_WithResult` (CA16); `AbilityGrantedRemoved_Relayed` (CA19, parte de habilidade).
- Critério de pronto: CA13 a CA16 e CA19 completos; CA29 (E).
- Tamanho estimado: ~380 linhas; 5 arquivos. Depende de: RL3, EA5 (concessão), EA1 (`States`, `GetTagCount`), SB-T6 (`sandbox.md`).

### RL5 — Inspector e validação do relay
- Escopo: `Editor/GAS.Bridges/AbilitySystemEventRelayEditor.cs` (novo; substitui `GasBridgesEditors.cs`), `Editor/GAS.Bridges/GasBridgesEditors.cs` (apagado; conta nos limites porque o editor novo o substitui), `AbilitySystemEventRelay.cs` (`IValidatable`), `Runtime/GAS.Bridges/Relay/RelayPair.cs` (validação de cada par), `Tests/GAS.Bridges/PlayMode/EventRelayTests.cs` (ajuste pelo comportamento novo), `Tests/GAS.Bridges/EditMode/SpellBook.GAS.Bridges.Tests.EditMode.asmdef` (acrescenta `SpellBook.GAS.Bridges.Editor` e `SpellBook.Core.Editor`), `Tests/GAS.Bridges/EditMode/RelayValidationTests.cs` (novo), `Tests/GAS.Bridges/EditMode/RelayInspectorTests.cs` (novo), `eventos.md` (seção "Ligar o GAS ao Events", passo a passo "som ao ser imune", R20), `manual/ferramentas/validador.md`, `CHANGELOG.md`.
- Testes red: `NoAsc_Error`, `ValueChannelOfUnsupportedType_Error`, `UnityEventWithoutTarget_Error` (CA26); `ProjectValidator_ReportsRelayAsContext`; `Inspector_GroupsPairsInFoldouts`.
- Critério de pronto: CA26 verde; CA25 (S) e CA27 (S) conferidos pelo Examinador no host, seguindo a página do manual, sem código.
- Tamanho estimado: ~440 linhas; 9 arquivos. Real: 512 linhas; 11 arquivos, pela exceção única da ADR 0003 (emenda de 2026-11-01, RL5, margem de ~30%); o escopo acima já inclui os dois arquivos acrescentados. Depende de: RL4, TG4, EA6 (`validador.md`).

### RL6 — Payload dinâmico na `RaiseEventAction`
Emenda de 2026-11-01: o lado do efeito (a `RaiseEventEffectAction`, CA21 e CA22) saiu deste ticket e vai para o EV15 do B-012 (Design 4, emenda). Título da issue: "RL6: feat(gas-bridges): payload dinâmico na RaiseEventAction".
- Escopo: `Runtime/GAS.Bridges/GasPayloadSource.cs` (novo; seis valores, `Supports(fonte, tipo)`), `Runtime/GAS.Bridges/RaiseEventAction.cs` (campos `payloadSource` e `attribute`, valor pelo `AbilityContext`, `IValidatable`), `Tests/GAS.Bridges/EditMode/GasPayloadSourceTests.cs` (novo; tabela `Supports` e validação), `Tests/GAS.Bridges/PlayMode/RaiseEventPayloadTests.cs` (novo), `gas.md` (`#raiseeventaction`), `CHANGELOG.md`.
- Testes red: `Float_AttributeValue_ReadsCaster`; `String_AbilityName`; `GameObject_SourceAndFirstValidTarget`; `AbilityAction_EventData_FillsAbilityEventData`; `UnsupportedSourceForChannel_ValidatorError`, `AttributeValueWithoutAttribute_ValidatorError`, `FixedPayload_StillWorks` (CA23); `UnsupportedSource_AtRuntime_RaisesNothing_NoException`.
- Critério de pronto: CA23 verde; suíte do Bridges sem edição de teste antigo (CA30); CA21 e CA22 ficam com o EV15.
- Tamanho estimado: ~300 linhas; 6 arquivos. Depende de: RL2, RP-04.

### RL7a — Seletor de tipo usa o drawer do tipo concreto
Emenda de 2026-11-01 (pergunta do RL7; ADR 0026, emenda de mesma data), aprovada no gate parcial de 2026-11-01. Issue: #280. Título da issue: "RL7a: feat(core-editor): seletor de tipo usa o drawer do tipo concreto".
- Escopo: `Editor/Core/Inspector/TypePickerCatalog.cs` (busca do drawer por tipo, com cache), `Editor/Core/Inspector/TypePickerDrawer.cs` (corpo pelo drawer do tipo, ou pelos filhos como hoje), `Tests/Core/EditMode/Inspector/TypePickerTypeDrawerTests.cs` (novo; tipos e drawers de teste no próprio arquivo), `manual/inicio/inspector.md` (`#inspectors-dos-seus-tipos`: drawer do seu tipo sob o seletor, contrato sem `fieldInfo`; `#um-drawer-por-campo`: o limite vale para atributos), `CHANGELOG.md`.
- Testes red: `ConcreteTypeWithDrawer_BodyComesFromTypeDrawer`; `ConcreteTypeWithoutDrawer_OnePropertyFieldPerChild` (regressão); `DerivedType_UsesBaseDrawer_OnlyWithUseForChildren`; `ExactTypeDrawer_WinsOverBaseDrawer`; `AttributeDrawers_NotUsedAsTypeDrawer_NoRecursion`; `PickingAnotherType_SwapsBody`; `TypeDrawerReturningNull_FallsBackToChildren`. Os testes criam o `ScriptableObject` e o `SerializedObject` no `SetUp`, os mantêm vivos durante o teste e os destroem no `TearDown`.
- Critério de pronto: os testes verdes; `TypePickerCatalogTests`, `TypePickerTreeTests` e `TypePickerUsageTests` (CA63) verdes sem edição; nenhum acesso a membro interno da Unity.
- Tamanho estimado: ~300 linhas; 5 arquivos. Depende de: nada desta Spec (só o seletor da 0-M). Usa a Unity, então entra na fila do WIP 1, antes do RL7.

### RL7 — Inspector do payload dinâmico
Emenda de 2026-11-01: o drawer cobre só a `RaiseEventAction` (a `RaiseEventEffectAction` não existe); o campo de índice do modificador entra no EV15.
Emenda de 2026-11-01 (pergunta do RL7): o drawer chega à lista de ações pelo RL7a; entram o `[HelpURL]` da `RaiseEventAction` e o teste dentro do seletor.
- Escopo: `Editor/GAS.Bridges/RaiseEventDrawer.cs` (novo), `Runtime/GAS.Bridges/RaiseEventAction.cs` (só o `[HelpURL]`), `Tests/GAS.Bridges/EditMode/RaiseEventDrawerTests.cs` (novo), `gas.md` (imagem opcional e texto do seletor), `CHANGELOG.md`.
- Testes red: `Drawer_ListsOnlySourcesSupportedByChannelType`; `Drawer_ShowsAttributeFieldOnlyForAttributeValue`; `Drawer_FixedHidesNotice`; `Drawer_UsedInsideTypePicker_OnAbilityActions` (`GameplayAbility` com uma `RaiseEventAction` em `Actions`; o corpo do seletor tem o popup de fontes); `RaiseEventAction_HasHelpUrl`. O `SerializedObject` fica vivo durante cada teste (ver RL7a).
- Critério de pronto: testes verdes; CA28 completo (CHANGELOG com o R21, `eventos.md` com a seção do R20, `[HelpURL]` nos tipos novos e na `RaiseEventAction`).
- Tamanho estimado: ~280 linhas; 5 arquivos. Depende de: RL5, RL6, RL7a.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| RL1 | sim | EA3, NC4 | ~420 / 5 |
| RL2 | sim | RL1 | ~300 / 10 |
| RL3 | sim | RL2 | ~480 / 8 |
| RL4 | sim | RL3, EA5, SB-T6 | ~380 / 5 |
| RL5 | sim | RL4, TG4, EA6 | ~440 / 9 |
| RL6 | sim | RL2, RP-04 | ~300 / 6 (emenda de 2026-11-01) |
| RL7a | sim | — | ~300 / 5 (emenda de 2026-11-01) |
| RL7 | sim | RL5, RL6, RL7a | ~280 / 5 (emenda de 2026-11-01) |

Fila proposta: RL1 → RL2 → RL3 → RL4 → RL5 → RL6 → RL7a → RL7. RL1 a RL3 podem entrar logo depois do EA3; o RL4, depois do EA5. CA24 (assemblies) e CA30 rodam em todos.

## Perguntas ao usuário

- **Q2. Quinto payload, `BlockedEventData`.** O R10 lista quatro structs; o `OnBlocked` genérico (R7) leva uma tag de motivo e uma origem, que não cabem em nenhum deles sem distorcer o sentido. Proposta: acrescentar `BlockedEventData` (com canal e resposta próprios) no RL2.
- A ADR 0031 (asmdef `SpellBook.GAS.Bridges.Editor`, pares fixos, disparo síncrono, payload dinâmico só no Bridges) passa pelo gate junto com esta Spec.
