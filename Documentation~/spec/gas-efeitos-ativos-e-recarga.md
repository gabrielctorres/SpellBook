# Spec: Efeitos ativos e recarga controláveis
PRD: [Documentation~/prd/gas-efeitos-ativos-e-recarga.md](../prd/gas-efeitos-ativos-e-recarga.md) · Backlog: B-003 · ADRs: nenhuma nova (usa 0003, 0004, 0007, 0012, 0014, 0016, 0026)

Status: Aprovada no gate de 2026-10-31 (EA1–EA8; Q1: sim) · Data: 2026-10-31 · Autor: Arquiteto

Emenda de 2026-10-31 (gate parcial do usuário, pedida pelo B-019d): o modelo do GAS Debugger vai para o runtime, em `Runtime/GAS/Diagnostics/`, público no asmdef `SpellBook.GAS`, para o overlay de debug do B-019d poder usá-lo no player. As linhas guardam a referência da fonte, não o nome em texto. Mudam o Design 8, o EA7 e a guarda do EA8. A issue #131 (EA7) e a do EA8 precisam ser atualizadas pelo Controlador.

Nota de fila: pela Spec do B-007 revisada, VN1–VN3 podem entrar antes do EA1. Nesse caso, `GameplayEffect.Duration` já é `ScalableFloat` e a constante do modificador é `ValueSource.Value`: o EA6 lê `CooldownEffect.Duration.Evaluate(1)` (o nível chega no VN5), e os testes usam a API nova.

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`. As linhas são as de hoje; o Lote A (NC1 a NC7, TG1 a TG5) muda parte delas antes deste item.

| Requisito | Onde está | O que existe |
|---|---|---|
| R1 lista | `Runtime/GAS/Core/AbilitySystemComponent.cs:31` | `_activeEffects` privada. Remoção por `List.Remove` (`:600`), que mantém a ordem de aplicação. |
| R2 instância | `Runtime/GAS/Effects/ActiveEffectInstance.cs:9-13` | `Effect`, `TimeRemaining`, `CurrentStacks`, `Target`, `Source` já públicos. `AppliedDuration` e `IsRemoved` chegam como `internal` no NC4 e no NC7. |
| R3, R4 busca | `AbilitySystemComponent.cs:422-444` | Só `HasTag` sobre as tags do ASC. `GameplayTagContainer` e `GameplayTagQuery` chegam no TG2 e no TG3, com sobrecargas `IReadOnlyList<GameplayTag>` pensadas para cá (Spec de tags, seção 6). |
| R5 remoção | `AbilitySystemComponent.cs:595-614` | O NC3 deixa a remoção idempotente e num ponto só (passo 3 da seção 2.1 daquela Spec). |
| R6, R7 mutação | `ActiveEffectInstance.cs:61-72` (`AddStack`, público) | Não há mutação de duração. Pilhas não mexem em modificadores nem em tags: tags e modificadores entram uma vez por instância (`AbilitySystemComponent.cs:529-539`). |
| R8 `Update` | `ActiveEffectInstance.cs:74` | Público. Só o `Tick` do ASC chama (`AbilitySystemComponent.cs:110`); nenhum teste chama (busca em `Tests/`). |
| R11 concessão | `AbilitySystemComponent.cs:123-145` | `GiveAbility` devolve o spec existente sem duplicar. O `AbilitySlotUpdateEvent` citado no PRD não existe mais no código (saiu na 0-M): não há o que substituir. |
| R12, R13 recarga | `AbilitySystemComponent.cs:206-211`, `:480-511`; `GameplayAbility.cs:75-86` | O NC5 cria `FindCooldownInstance` (maior tempo restante, casamento hierárquico). Piso de 0,1 s e `Clamp01` fixos no `GetCooldownDuration`, aplicados só com `CooldownReductionAttribute`. |
| R15 leitura | `Runtime/GAS/Attributes/AttributeSet.cs:56-81` | `TryGetState` e `GetValue` (avisa uma vez e devolve 0). O mapa é privado e não há como listar os atributos. `AttributeState._modifiers` é privado. |
| R16 a R18 Debugger | `Editor/Events/EventDebuggerWindow.cs`, `EventDebuggerModel.cs` | Padrão a seguir: janela + modelo testável, atualização a cada 250 ms, menu `Window/SpellBook/...`. |
| Testes | `Tests/GAS/EditMode` e `Tests/GAS/PlayMode`; `Tests/Testing/GasFactory.cs` | O GAS não tem `InternalsVisibleTo`: os testes só veem a API pública. |

## Respostas às perguntas técnicas do PRD (seção 8)

- **P3. `Replaced`.** Fica fora do enum. Reaplicar sem `CanStack` renova a mesma instância (NC4), então nenhuma instância é substituída. Se o B-009 ou outro item criar substituição, o valor entra no fim do enum.
- **P4. Evento de recarga.** Um evento só, com o motivo: `OnCooldownChanged(AbilitySpec spec, CooldownChange change)`, com `CooldownChange` em `Started`, `Changed` e `Ended`. A recarga **continua sendo um efeito ativo** (sem estado próprio); o evento é a leitura dela por habilidade, para a UI não ter de traduzir `OnEffectRemoved` em "habilidade pronta". Ver Design 6.
- **P5. Duração aplicada e mutação pela instância.** `AppliedDuration` (NC4) passa a ter `get` público. As mutações da instância (`ExtendDuration`, `ReduceDuration`, `RestartDuration`, `SetStacks`, `RemoveStacks`) são atalhos que chamam o ASC alvo (`Target`). Toda a regra fica no ASC, no mesmo ponto que dispara os eventos.
- **P6. Ordem entre eventos.** Regra única: **eventos disparam depois que o estado do ASC está final.** Em `RemoveStacks` ou `SetStacks` até zero: limpeza (tags e modificadores), depois `OnEffectStacksChanged(i, antes, 0)` e por último `OnEffectRemoved(i, StacksDepleted)`. Na aplicação que empilha: `OnEffectStacksChanged` depois do `AddStack`. Na aplicação que cria: só `OnEffectApplied`, depois de tags e modificadores.
- **P7. Modificadores por fonte no Debugger.** `AttributeState` ganha `Modifiers` (`IReadOnlyList<AttributeModifier>`, sem cópia). O `Source` de cada modificador de efeito é a `ActiveEffectInstance`, de onde o Debugger tira o nome do efeito. `AttributeSet` ganha `States` (`IReadOnlyList<AttributeState>`), para listar os atributos. Os dois servem também ao relay do B-004.

## Design

### 1. Leitura de atributos e tags (R15 e apoio ao R16)

`AttributeSet`:
- `bool TryGetValue(AttributeDefinition def, out float value)`, `float GetValueOrDefault(AttributeDefinition def, float fallback = 0f)`, `bool HasAttribute(AttributeDefinition def)` e `float GetBaseValue(AttributeDefinition def)` (0 para ausente). Nenhum avisa. `def` nulo: falso, `fallback` ou 0.
- `IReadOnlyList<AttributeState> States`: uma `List<AttributeState>` paralela ao mapa, na ordem de `startingAttributes` (a primeira entrada de uma definição repetida vence, como no mapa).
- A criação dos estados sai do `Awake` para um `internal void EnsureInitialized()` idempotente. O `Awake` chama; `States`, `TryGetState` e as leituras acima também chamam. Assim, um componente que lê o `AttributeSet` no próprio `Awake` ou `OnEnable` (relay do B-004, ASC com `startingEffects` do B-005) não depende da ordem dos componentes.

`AttributeState`: `IReadOnlyList<AttributeModifier> Modifiers => _modifiers`.

`AbilitySystemComponent`: atalhos `TryGetAttributeValue`, `GetAttributeValueOrDefault`, `HasAttribute` e `GetAttributeBaseValue`, que repassam ao `AttributeSet` (nulo: falso, padrão, falso, 0). `int GetTagCount(GameplayTag tag)` (contagem exata da tag, 0 se ausente ou inválida) e `IReadOnlyDictionary<GameplayTag, int> TagCounts` (o próprio dicionário). `GetAttributeValue` continua igual.

### 2. Lista, instância e busca (R1 a R4, R8)

- `IReadOnlyList<ActiveEffectInstance> ActiveEffects => _activeEffects`.
- `ActiveEffectInstance`: `public float AppliedDuration { get; private set; }` (era `internal get`); `public bool IsActive => !IsRemoved`; `Update` passa a `internal`; `AddStack` passa a `internal` (é mutação, e mutação agora passa pelo ASC). Construtores e `AddActionData`/`TryGetActionData` não mudam.
- **Casamento de um efeito com uma tag `t`:** alguma tag `g` de `Effect.GrantedTags` com `g.Matches(t)`. É a mesma direção de `HasTag` (a concedida é a filha). Lista nula conta como vazia. Tag inválida não casa (`GameplayTag.Matches`).
- Busca, todas preenchendo `results` (limpa antes) e devolvendo a quantidade, por laço de índice, sem alocar:
  - `int FindEffectsWithTag(GameplayTag tag, List<ActiveEffectInstance> results)`;
  - `int FindEffectsWithAnyTag(IReadOnlyList<GameplayTag> tags, List<ActiveEffectInstance> results)`, que aceita `GameplayTagContainer`, lista e array (R4, "alguma tag do conjunto");
  - `int FindEffectsMatching(GameplayTagQuery query, List<ActiveEffectInstance> results)`, com `query.Matches(effect.GrantedTags)` (R4);
  - `int FindEffects(GameplayEffect effect, List<ActiveEffectInstance> results)`;
  - `bool HasEffectWithTag(GameplayTag tag)`.
  `results` nulo lança `ArgumentNullException` (erro de programação, não de dados).

### 3. Remoção e eventos de efeito (R5, R9, R10)

- `public enum EffectRemovalReason { Expired, Removed, RemovedByTag, StacksDepleted }` (arquivo próprio em `Runtime/GAS/Effects/`). Valores novos só no fim.
- O ponto único de remoção do NC3 vira `private void RemoveEffectCore(ActiveEffectInstance instance, EffectRemovalReason reason)`. `RemoveEffect(instance)` público usa `Removed`; a expiração (`OnEffectExpired`) usa `Expired`. Depois do passo 3 do NC3 (tags e modificadores devolvidos), dispara `OnEffectRemoved(instance, reason)`. Instância já removida não passa do passo 2, então não dispara de novo.
- `int RemoveEffectsWithTag(GameplayTag tag)` (motivo `RemovedByTag`) e `int RemoveEffects(GameplayEffect effect)` (motivo `Removed`): laço de trás para frente com a guarda `if (i >= _activeEffects.Count) continue;`, conferindo o casamento de novo a cada índice, porque um ouvinte pode ter removido outras instâncias. Somam o `CurrentStacks` de cada instância **antes** de removê-la e devolvem o total. Nada ativo: 0.
- Eventos novos do ASC (C#, por instância, ADR 0016):
  - `event Action<ActiveEffectInstance> OnEffectApplied`, no fim da criação de uma instância (depois de tags, modificadores e assinaturas). Instantâneos não disparam (CA15). Renovação e empilhamento não disparam.
  - `event Action<ActiveEffectInstance, EffectRemovalReason> OnEffectRemoved`.
  - `event Action<ActiveEffectInstance, int, int> OnEffectStacksChanged` (antes, depois), só quando a contagem muda de fato. Na reaplicação que empilha, dispara depois do `AddStack`; no `MaxStacks`, a contagem não muda e não dispara.
- Disparo por `handler?.Invoke(...)` direto do campo do evento: delegate imutável durante a chamada, sem alocação sem ouvintes (N1). Um ouvinte que aplica ou remove efeitos durante o evento não corrompe a iteração, porque todo laço sobre `_activeEffects` tem a guarda de índice (NC3) e confere `IsActive` antes de agir.

### 4. Mutação de duração e de pilhas (R6, R7)

No ASC, todos devolvem `bool` (algo mudou). Instância nula ou fora deste ASC: `false`.

- `ExtendEffectDuration(instance, seconds)`: `seconds` negativo ou NaN é rejeitado com `Debug.LogWarning` (só em desenvolvimento: `Debug.isDebugBuild`) e devolve `false`. Soma ao `TimeRemaining`.
- `ReduceEffectDuration(instance, seconds)`: mesma validação. Subtrai; se o restante fica ≤ 0, expira pelo caminho normal (`RemoveEffectCore(..., Expired)`), no mesmo quadro.
- `RestartEffectDuration(instance)`: `TimeRemaining = AppliedDuration`. Não mexe no contador do período.
- As três ignoram efeitos que não são `HasDuration` (devolvem `false`). Nenhuma muda `AppliedDuration` (só a reaplicação muda, NC4).
- `SetEffectStacks(instance, count)`: limita `count` a `[0, MaxStacks]` (com `MaxStacks < 1` tratado como 1). Igual ao atual: `false`. Zero: remove com `StacksDepleted` na ordem da P6. Senão grava e dispara `OnEffectStacksChanged`.
- `RemoveEffectStacks(instance, count)`: `count` ≤ 0 devolve `false`; senão `SetEffectStacks(instance, CurrentStacks - count)`.
- `SetStacks` aqui muda **só a contagem** (PRD, seção 6): tags e modificadores continuam um por instância, como hoje. O B-007 (VN5) faz as pilhas multiplicarem a magnitude e passa por este mesmo ponto.
- `ActiveEffectInstance` ganha os atalhos públicos `ExtendDuration`, `ReduceDuration`, `RestartDuration`, `SetStacks` e `RemoveStacks`, que chamam `Target.<método>(this, ...)`. Instância sem alvo ou removida: `false`. A escrita de `TimeRemaining` e `CurrentStacks` fica em métodos `internal` da instância, chamados só pelo ASC.

### 5. Concessão de habilidade (R11)

`event Action<AbilitySpec> OnAbilityGranted` (no fim de `GiveAbility`, só quando cria o spec) e `event Action<AbilitySpec> OnAbilityRemoved` (no fim de `RemoveAbility`, depois do cancelamento e da retirada da lista). As habilidades de `startingAbilities` disparam no `Awake`, antes de qualquer ouvinte externo existir; isso fica escrito no manual.

### 6. Recarga (R12, R13)

- `public enum CooldownChange { Started, Changed, Ended }` e `event Action<AbilitySpec, CooldownChange> OnCooldownChanged`.
- `AbilitySpec` ganha `internal bool CooldownTracked`. `ActivateSpec`, depois de aplicar o efeito de recarga, marca `CooldownTracked = true` e dispara `Started`.
- `Ended`: depois de cada `RemoveEffectCore`, um laço pelos specs com `CooldownTracked` confere `!IsOnCooldown(spec.Ability.CooldownEffect)`; se a habilidade ficou pronta, desmarca e dispara `Ended`. Custo: um laço curto por remoção, sem alocação.
- Controle, por `GameplayAbility` (concedida ou não, porque a recarga é do ASC):
  - `bool ReduceCooldown(GameplayAbility ability, float seconds)`: valida `seconds` como no 4. Aplica `ReduceEffectDuration` a **toda** instância que `FindCooldownInstance` consideraria (mesmo efeito ou tag filha, NC5). Algo mudou e a recarga continua: `Changed`. Chegou a zero: as instâncias expiram, e o `Ended` sai pela regra acima.
  - `bool ResetCooldown(GameplayAbility ability)`: remove essas instâncias (motivo `Removed`). A habilidade volta a ser ativável, a menos que uma tag de recarga venha de fora de efeito (`AddTag` manual); o manual avisa.
  - `bool RestartCooldown(GameplayAbility ability)`: `RestartEffectDuration` em cada uma; `Changed`.
  - Habilidade nula, sem `CooldownEffect` ou pronta: `false`, sem exceção (CA18).
- Para os três, a lista de instâncias casadas é percorrida de trás para frente com a guarda de índice (uma remoção pode disparar ouvintes).

### 7. `CooldownReductionPolicy` (R14, R19)

`Runtime/GAS/Abilities/CooldownReductionPolicy.cs`, `ScriptableObject`, `[CreateAssetMenu("GAS/Cooldown Reduction Policy")]`, `[HelpURL(DocUrls.Gas + "#cooldownreductionpolicy")]`, `IValidatable`:

- campos privados serializados `minDuration = 0.1f`, `maxReduction = 1f`, `mode = CooldownReductionMode.Percent`, com propriedades de leitura;
- `public enum CooldownReductionMode { Percent, Haste }`;
- `float Apply(float baseDuration, float attributeValue)`:
  - `Percent`: redução = `Clamp(attributeValue, 0, maxReduction)`;
  - `Haste`: redução = `1 - 1 / (1 + max(attributeValue, 0))`, depois limitada a `maxReduction`;
  - duração = `max(minDuration, baseDuration × (1 - redução))`;
- `public static float ApplyDefault(float baseDuration, float attributeValue)` reproduz o código de hoje (piso 0,1 s, `Clamp01`), sem asset;
- `Validate`: `minDuration < 0` e `maxReduction` fora de `[0, 1]` são erros, com `propertyPath`.

`GameplayAbility` ganha `[SerializeField] private CooldownReductionPolicy cooldownReductionPolicy` e `CooldownReductionPolicy => ...`. `GetCooldownDuration` só aplica redução quando há `CooldownReductionAttribute` (como hoje): com política, `policy.Apply`; sem, `ApplyDefault`. O `Validate` da habilidade avisa quando há política sem atributo de redução. Campo nulo em asset antigo dá o comportamento de hoje (N5). O `SpellBookEditorCoverageTests` exige um Inspector derivado de `SpellBookEditor` para o tipo novo: entra uma linha em `GasEditors.cs`.

### 8. GAS Debugger v1 (R16 a R18)

- `Runtime/GAS/Diagnostics/GasDebuggerModel.cs` (`public sealed`, namespace `SpellBook.GAS.Diagnostics`, no asmdef `SpellBook.GAS`; emenda de 2026-10-31, era `Editor/GAS/Debugger/`, `internal`): monta linhas a partir da API pública, em listas reutilizadas, sem `UnityEditor` e sem alocar por atualização depois do aquecimento. As structs de linha (`AttributeRow`, `TagRow`, `EffectRow`, `AbilityRow`) e o `EventLog` são públicos. As linhas guardam a **referência** da fonte (a `ActiveEffectInstance`, o asset ou o `object` do modificador), e quem desenha (a janela do EA8, o overlay do B-019d) lê o nome:
  - `BuildAttributeRows(IReadOnlyList<AttributeState>, List<AttributeRow>)`: definição, base, atual, mínimo, máximo e, por modificador, operação, valor e fonte;
  - `BuildTagRows(IReadOnlyDictionary<GameplayTag, int>, List<TagRow>)`, em ordem alfabética do nome da definição;
  - `BuildEffectRows(IReadOnlyList<ActiveEffectInstance>, List<EffectRow>)`: asset, origem, tempo restante, duração aplicada, pilhas;
  - `BuildAbilityRows(AbilitySystemComponent, List<AbilityRow>)`: habilidade, nível, ativa, recarga restante e normalizada;
  - `EventLog` com limite fixo (50 itens, buffer circular), alimentado pelos eventos de 3, 5 e 6.
- `Editor/GAS/Debugger/GasDebuggerWindow.cs` (`internal sealed`, `EditorWindow`, UI Toolkit, menu `Window/SpellBook/GAS Debugger`): o ASC vem da seleção (`Selection.activeGameObject`) ou de um `ObjectField` na janela; fora do Play, mostra "Entre em Play para inspecionar um ASC". Quatro `Foldout` (Atributos, Tags, Efeitos, Habilidades) atualizados a cada 250 ms, como o Event Debugger. Ações: aplicar efeito (`ObjectField` de `GameplayEffect` + botão), remover o efeito selecionado, conceder e retirar habilidade (`ObjectField` de `GameplayAbility`), zerar a recarga da habilidade selecionada. Inscreve-se nos eventos do ASC escolhido e desinscreve ao trocar, ao sair do Play e no `OnDisable`.
- ~~`Editor/GAS/AssemblyInfo.cs`: `InternalsVisibleTo("SpellBook.GAS.Tests.EditMode")`.~~ Sai com a emenda de 2026-10-31: o modelo é público.
- Sem `System.Reflection` em `Editor/GAS/Debugger/` nem em `Runtime/GAS/Diagnostics/` (guarda textual, CA26). A janela fica no asmdef de Editor; o modelo entra no build de player (é pequeno e só lê).
- Página `Documentation~/manual/ferramentas/gas-debugger.md` e `DocUrls.GasDebugger = Base + "ferramentas/gas-debugger/"`. O `mkdocs.yml` não tem `nav` (ADR 0007, I26): a página entra sozinha.

### 9. O que não muda

- `ApplyEffect`, `GetAttributeValue`, `GetValue` (com aviso), `HasTag`, `IsOnCooldown` e as assinaturas do Lote A.
- Formato dos assets existentes: só o campo novo, nulo, em `GameplayAbility`.
- O GAS continua sem referência ao Events (N3, CA28).

## Quebras de compatibilidade

Para o CHANGELOG (pré-release 3.0, decisão I1, sem fachada):

- `ActiveEffectInstance.Update` e `ActiveEffectInstance.AddStack` passam a `internal`. O tempo avança por `AbilitySystemComponent.Tick`; pilhas mudam por reaplicação ou `SetEffectStacks`.
- Adicionado: `ActiveEffects`, busca e remoção por tag e por efeito, mutação de duração e pilhas, `OnEffectApplied`, `OnEffectRemoved`, `OnEffectStacksChanged`, `OnAbilityGranted`, `OnAbilityRemoved`, `OnCooldownChanged`, `ReduceCooldown`/`ResetCooldown`/`RestartCooldown`, `CooldownReductionPolicy`, leituras sem aviso, `GetTagCount`, `AttributeSet.States`, `AttributeState.Modifiers`, GAS Debugger.

## Tickets

Todos usam a Unity e seguem o WIP 1 (0003). **Não há par paralelo sem Editor nesta Spec.** Cada ticket escreve a sua parte de `Documentation~/manual/modulos/gas.md` e a sua linha no `CHANGELOG.md`; os dois contam só no limite de arquivos (0003, I35). Tempo por `asc.Tick` com `autoTick` desligado ou `ManualTimeSource` (N2); objetos pela `GasFactory` (N6); alocação por `Is.Not.AllocatingGCMemory()` com aquecimento. Teto: ~500 linhas e 10 arquivos (0003, emenda de 2026-10-30).

**Conflitos com a fila:** todos os tickets tocam `gas.md` e `CHANGELOG.md` (como NC*, RP-06 e SB-T6) e andam em série. EA1 depende de NC1 e NC2 (`AttributeState.cs`, `AttributeSet.cs`); EA2 a EA5 dependem de NC3, NC4, NC5 e NC7 (`AbilitySystemComponent.cs`, `ActiveEffectInstance.cs`); EA2 depende de TG2 e TG3. EA6 toca `GasFactory.cs`, do RP-04, e entra depois dele. EA6 e EA8 tocam `manual/ferramentas/validador.md` ou `DocUrls.cs`, como TG4 e SB-T1, e entram depois deles.

### EA1 — Leitura de atributos e tags sem aviso
- Escopo: `Runtime/GAS/Attributes/AttributeSet.cs`, `Runtime/GAS/Attributes/AttributeState.cs`, `Runtime/GAS/Core/AbilitySystemComponent.cs` (atalhos, `GetTagCount`, `TagCounts`), `Tests/GAS/PlayMode/AttributeReadTests.cs` (novo), `Tests/GAS/EditMode/AttributeStateModifiersTests.cs` (novo), `gas.md` (`#attributeset`), `CHANGELOG.md`.
- Testes red:
  - `TryGetValue_Present_TrueWithValue`, `TryGetValue_Missing_FalseNoWarning` (CA23; `LogAssert.NoUnexpectedReceived`);
  - `GetValueOrDefault_Missing_ReturnsFallback`, `HasAttribute_PresentAndMissing`, `GetBaseValue_PresentAndMissing` (CA23);
  - `AscShortcuts_MatchAttributeSet`;
  - `States_ListsStartingAttributesInOrder`, `States_BeforeAwakeOrder_IsInitialized` (lê `States` de um componente adicionado antes do `Awake` do `AttributeSet`);
  - `Modifiers_ReflectAddAndRemove` (EditMode, `AttributeState` com `owner` nulo);
  - `GetTagCount_CountsReferences`, `GetTagCount_InvalidTag_Zero`;
  - `TryGetValue_DoesNotAllocate_10000Times` (parte do CA27).
- Critério de pronto: CA23 verde e a parte de `TryGetValue` do CA27; suíte do GAS sem edição de teste antigo (CA30).
- Tamanho estimado: ~320 linhas; 7 arquivos. Depende de: NC1, NC2.

### EA2 — Lista pública, dados da instância e busca
- Escopo: `Runtime/GAS/Effects/ActiveEffectInstance.cs`, `AbilitySystemComponent.cs`, `Tests/GAS/PlayMode/EffectQueryTests.cs` (novo), `gas.md` (`#gameplayeffect`, "Efeitos ativos"), `CHANGELOG.md`.
- Testes red:
  - `ActiveEffects_InApplicationOrder` (CA1);
  - `FindWithTag_ParentReturnsChildren_ExactReturnsOne_UnknownReturnsZero` (CA1, C1);
  - `FindWithTag_InvalidTag_ZeroAndHasEffectFalse` (CA2);
  - `FindWithAnyTag_Container`, `FindMatching_QueryAllAnyNone` (CA3);
  - `FindEffects_ByAsset`;
  - `Instance_ExposesEffectSourceTargetRemainingAppliedDurationStacks`, `AppliedDuration_IsReducedCooldownDuration` (CA4);
  - `Instance_IsActiveFalseAfterRemoval`;
  - `Queries_DoNotAllocate_10000Times` (lista, busca em lista reutilizada, `GetCooldownTimeRemaining`; parte do CA27).
- Critério de pronto: CA1 a CA4 e a parte de leitura do CA27 verdes; `Update` e `AddStack` internos sem quebrar a suíte.
- Tamanho estimado: ~380 linhas; 5 arquivos. Depende de: NC4, NC7, TG2, TG3, EA1.

### EA3 — Eventos de efeito e remoção por tag e por efeito
- Escopo: `Runtime/GAS/Effects/EffectRemovalReason.cs` (novo), `AbilitySystemComponent.cs` (`RemoveEffectCore`, eventos, `RemoveEffectsWithTag`, `RemoveEffects`), `Tests/GAS/PlayMode/EffectEventsTests.cs` (novo), `Tests/GAS/PlayMode/EffectRemoveByTagTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red:
  - `RemoveByTag_ReturnsStacks_ReturnsTagsAndModifiers_SecondCallZero` (CA5, C2);
  - `RemoveByEffect_OnlyThatAsset` (CA6);
  - `Applied_FiresOnce`, `Stacked_FiresStacksChanged_1To2`, `StackAtMax_DoesNotFire` (CA12, C6);
  - um teste por motivo de `OnEffectRemoved`: `Expired`, `Removed`, `RemovedByTag` (CA12);
  - `RemovedEvent_TagsAlreadyReturned` (CA13);
  - `ListenerRemovesOtherEffect_NoExceptionNoSkippedEvent` (CA14);
  - `InstantEffect_DoesNotFireApplied` (CA15);
  - `RemoveTwice_FiresRemovedOnce` (R10);
  - `Events_WithoutListeners_DoNotAllocate`.
- Critério de pronto: CA5, CA6, CA12 (menos `StacksDepleted`), CA13 a CA15 verdes.
- Tamanho estimado: ~460 linhas; 6 arquivos. Depende de: EA2.

### EA4 — Mutação de duração e de pilhas
- Escopo: `ActiveEffectInstance.cs` (atalhos e escritas internas), `AbilitySystemComponent.cs` (Extend/Reduce/Restart, SetStacks/RemoveStacks), `Tests/GAS/PlayMode/EffectMutationTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red:
  - `Extend2_From5_Is7`, `Reduce10_Expires_WithReasonExpired` (CA7, C3);
  - `NegativeOrNaN_ExtendReduce_NoChange` (CA8);
  - `Restart_ReturnsToAppliedDuration_NotAsset` (CA9, C4);
  - `Infinite_IgnoresExtendReduceRestart` (CA10);
  - `SetStacks3_Of5_Is3`, `SetStacks9_ClampsTo5`, `RemoveStacks_ToZero_RemovesWithStacksDepleted`, `Depletion_EventOrder_StacksChangedThenRemoved` (CA11, C5, P6);
  - `InstanceShortcuts_DelegateToAsc`, `ShortcutOnRemovedInstance_False`.
- Critério de pronto: CA7 a CA11 e o motivo `StacksDepleted` do CA12 verdes.
- Tamanho estimado: ~420 linhas; 5 arquivos. Depende de: EA3.

### EA5 — Concessão de habilidade e controle de recarga
- Escopo: `Runtime/GAS/Abilities/CooldownChange.cs` (novo), `Runtime/GAS/Abilities/AbilitySpec.cs` (`CooldownTracked`), `AbilitySystemComponent.cs`, `Tests/GAS/PlayMode/AbilityGrantEventsTests.cs` (novo), `Tests/GAS/PlayMode/CooldownControlTests.cs` (novo), `gas.md` (seção 4), `CHANGELOG.md`.
- Testes red:
  - `Give_FiresGrantedOnce_GiveAgainDoesNot`, `Remove_FiresRemovedOnce` (CA16, C10);
  - `Reduce3_From8_Is5`, `Reset_IsZero_AndActivatable`, `Restart_BackToAppliedFull` (CA17, C7);
  - `ControlsOnReadyAbility_ReturnFalse_NoException` (CA18);
  - `Controls_RespectChildTagCooldown` (CA19);
  - `CooldownChanged_StartedOnActivate_EndedOnExpiry`, `CooldownChanged_ChangedOnReduceAndRestart`, `CooldownChanged_EndedOnReset` (CA22).
- Critério de pronto: CA16 a CA19 e CA22 verdes.
- Tamanho estimado: ~470 linhas; 7 arquivos. Depende de: EA4, NC5.

### EA6 — `CooldownReductionPolicy`
- Escopo: `Runtime/GAS/Abilities/CooldownReductionPolicy.cs` (novo), `Runtime/GAS/Abilities/GameplayAbility.cs`, `Editor/GAS/GasEditors.cs` (uma linha), `Tests/Testing/GasFactory.cs` (`SetObjectField(Object alvo, string campo, Object valor)`, público e estático, para campos privados serializados), `Tests/GAS/EditMode/CooldownReductionPolicyTests.cs` (novo), `Tests/GAS/PlayMode/CooldownPolicyAbilityTests.cs` (novo), `gas.md` (`#cooldownreductionpolicy`), `Documentation~/manual/ferramentas/validador.md`, `CHANGELOG.md`, `Tests/GAS/EditMode/SpellBook.GAS.Tests.EditMode.asmdef` (emenda de 2026-11-01: ganha a referência `SpellBook.Core.Editor`).
- Emenda de 2026-11-01 (escopo): o `ProjectValidator_ReportsPolicyAsContext` usa o `ProjectValidator` (`Editor/Core/ProjectValidator.cs:18`, assembly `SpellBook.Core.Editor`), que o asmdef de teste do GAS não referencia. O teste fica em `CooldownReductionPolicyTests.cs`, junto dos outros casos do CA21, e o asmdef ganha a referência. É o mesmo padrão dos testes de Tags (`SpellBook.Tags.Tests.EditMode.asmdef`, com `TagValidationTests` usando o `ProjectValidator`), e o `AsmdefRulesTests` não restringe referências de asmdef de teste (`AsmdefRules_TestAsmdefs_OnlyWithTestsAndTestRunnerReferences` só exige o TestRunner e o `UNITY_INCLUDE_TESTS`). Mover o teste para `Tests/Core/EditMode/` foi descartado: separaria o CA21 em dois módulos e poria um teste do GAS no Core. Nenhuma ADR muda.
- Testes red:
  - `Percent_Min1_Max50_8sAt90_Is4`, `Percent_Max100_At99_FloorIs1` (CA20, C8);
  - `Haste_DoublesSpeed_HalvesDuration`, `Haste_CappedByMaxReduction`;
  - `NoPolicy_SameAsToday_Floor01_Clamp01` (CA20; valores literais da fórmula atual);
  - `Validate_NegativeMin_Error`, `Validate_MaxOutOfRange_Error`, `ProjectValidator_ReportsPolicyAsContext` (CA21);
  - `Ability_WithPolicy_UsesPolicyDuration` (PlayMode, com atributo de redução).
- Critério de pronto: CA20 e CA21 verdes; `SpellBookEditorCoverageTests` e `HelpUrlRule` verdes.
- Tamanho estimado: ~380 linhas; 10 arquivos (era 9; +1 pelo asmdef, emenda de 2026-11-01; no limite da ADR 0003). Depende de: NC5, RP-04 (`GasFactory.cs`). Não depende de EA1 a EA5: pode entrar antes deles na fila, se o Controlador preferir.

> **Emenda APROVADA (2026-10-31, gate parcial do usuário; já aplicada no Design 8, no EA7 e no EA8).** Registro do motivo: o overlay de debug do B-019d precisa do mesmo modelo em runtime (build de desenvolvimento), e o `GasDebuggerModel` do EA7 ficaria no asmdef de Editor, inalcançável pelo player. O modelo só lê API pública do runtime e não usa `UnityEditor`, então mudá-lo agora custa quase nada; depois do código, seria um ticket de movimento e uma quebra de visibilidade. O que muda:
> - **Lugar:** `Runtime/GAS/Diagnostics/GasDebuggerModel.cs`, no asmdef `SpellBook.GAS` (sem asmdef novo; ADR 0012 não muda), namespace `SpellBook.GAS.Diagnostics`, `public sealed`, com as structs de linha (`AttributeRow`, `TagRow`, `EffectRow`, `AbilityRow`) e o `EventLog` públicos. A janela do EA8 continua em `Editor/GAS/Debugger/` e consome o modelo.
> - **Sem string por linha:** as linhas guardam a referência da fonte (`ActiveEffectInstance`, asset do efeito ou `object`), e quem desenha lê o nome. Assim o modelo não aloca por atualização, o que importa em runtime. O teste `AttributeRows_BaseCurrentMinMax_ModifiersWithSourceName` vira `AttributeRows_BaseCurrentMinMax_ModifiersWithSource`, e entra `Build_AfterWarmup_DoesNotAllocate`.
> - **Escopo do EA7:** `Runtime/GAS/Diagnostics/GasDebuggerModel.cs` (novo), `Tests/GAS/EditMode/GasDebuggerModelTests.cs` (novo), `CHANGELOG.md`. Saem o `Editor/GAS/AssemblyInfo.cs` e a mudança no asmdef de teste (o teste já referencia `SpellBook.GAS`). Fica com ~380 linhas e 3 arquivos.
> - **EA8:** a guarda `GasDebuggerSources_UseNoReflection` passa a cobrir também `Runtime/GAS/Diagnostics/`. O resto não muda.
> - **Fora:** o overlay em si, e se ele vem só em build de desenvolvimento, ficam com a Spec do B-019d.
> - **Issue:** a #131 (EA7) precisa ter o escopo e os testes atualizados. A issue do EA8 ganha a linha da guarda.

### EA7 — GAS Debugger: modelo e log de eventos
- Escopo (emenda de 2026-10-31): `Runtime/GAS/Diagnostics/GasDebuggerModel.cs` (novo, público), `Tests/GAS/EditMode/GasDebuggerModelTests.cs` (novo), `CHANGELOG.md`. Não entram mais o `Editor/GAS/AssemblyInfo.cs` nem a mudança no asmdef de teste: o teste já referencia `SpellBook.GAS`.
- Testes red (EditMode; `AttributeState` e `ActiveEffectInstance` criados pelos construtores públicos, ASC sem `Awake` só para habilidades e recarga, que não usam o `AttributeSet`):
  - `AttributeRows_BaseCurrentMinMax_ModifiersWithSource` (R16, P7; a linha guarda a referência da fonte, não o nome);
  - `TagRows_SortedWithCounts`;
  - `EffectRows_RemainingAppliedStacksSource`;
  - `AbilityRows_LevelActiveCooldown`;
  - `EventLog_KeepsLast50`;
  - `Build_AfterWarmup_DoesNotAllocate`.
- Critério de pronto: testes verdes; `AsmdefRulesTests` verde sem mudança (o modelo não usa `UnityEditor` e fica no asmdef `SpellBook.GAS`).
- Tamanho estimado: ~380 linhas; 3 arquivos. Depende de: EA5 (log de todos os eventos).
- Tamanho real (nota de 2026-11-01): **621 linhas**; 3 arquivos (361 no `GasDebuggerModel.cs`, 260 no `GasDebuggerModelTests.cs`). Acima do teto de ~500, aceito pelo usuário como exceção única, sem dividir o ticket: ADR 0003, emenda de 2026-11-01.

### EA8 — GAS Debugger: janela, ações e página
- Escopo: `Editor/GAS/Debugger/GasDebuggerWindow.cs` (novo), `Runtime/Core/DocUrls.cs` (`GasDebugger`), `Tests/GAS/EditMode/GasDebuggerSourceTests.cs` (novo: nenhum `.cs` de `Editor/GAS/Debugger/` nem de `Runtime/GAS/Diagnostics/` (emenda de 2026-10-31) contém `System.Reflection`, `BindingFlags` nem `GetField(`; red forçado como nas guardas do SB-T1), `Tests/Core/EditMode/DocUrlsTests.cs` (caso novo), `Documentation~/manual/ferramentas/gas-debugger.md` (nova, com conteúdo real), `gas.md` (link para a página), `CHANGELOG.md`.
- Testes red: `GasDebuggerSources_UseNoReflection` (CA26); `DocUrls_PageConstantPointsToPageUnderBase("GasDebugger", "ferramentas/gas-debugger/")`; `ManualLinksTests.EveryDocUrlHasPage` red só com a constante.
- Critério de pronto: CA24 e CA25 (S) conferidos pelo Examinador no host, em Play, com um ASC de teste (rascunho fora do diff): quatro blocos atualizando, as cinco ações e o log; CA26 (E); CA29 completo (manual descreve R1 a R12, a página do Debugger existe, tipos novos com `[HelpURL]`); `mkdocs build --strict`, se disponível.
- Tamanho estimado: ~460 linhas; 7 arquivos. Depende de: EA7.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| EA1 | sim | NC1, NC2 | ~320 / 7 |
| EA2 | sim | NC4, NC7, TG2, TG3, EA1 | ~380 / 5 |
| EA3 | sim | EA2 | ~460 / 6 |
| EA4 | sim | EA3 | ~420 / 5 |
| EA5 | sim | EA4, NC5 | ~470 / 7 |
| EA6 | sim | NC5, RP-04 | ~380 / 10 |
| EA7 | sim | EA5 | ~380 / 3 |
| EA8 | sim | EA7 | ~460 / 7 |

Fila proposta: EA1 → EA2 → EA3 → EA4 → EA5 → EA6 → EA7 → EA8. O Debugger (EA7, EA8) é o último, pela P1 do PRD: a UI-2 (B-016) já pode usar R1 a R15 depois do EA6. O B-004 depende de EA3 e EA5, não do Debugger, e pode entrar na fila antes de EA7 se o Controlador quiser.

CA28 (assemblies) e CA30 (suíte sem edição) rodam em todos os tickets. CA27 é a soma de EA1 e EA2.

## Perguntas ao usuário

- **Q1. Remoção só por tag e por efeito (P2 do PRD, ainda aberta).** Proposta: o ASC oferece só `RemoveEffectsWithTag` e `RemoveEffects(GameplayEffect)`; remover por categoria (buff, debuff, purgável) fica para depois dos metadados do B-005 (AR7), como item aditivo. O design acima já assume isso.
