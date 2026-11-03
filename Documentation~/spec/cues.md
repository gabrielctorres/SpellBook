# Spec: Módulo Cues
PRD: [Documentation~/prd/cues.md](../prd/cues.md) · Backlog: B-013 · ADRs: **0037 (nova, Proposta)**; usa 0003, 0004, 0007, 0009, 0012, 0013, 0014, 0015, 0026, 0034, 0036

Status: Aprovada no gate de 2026-10-31, Lote D (CU1–CU9; ADR 0037 aceita; Q1: cues no GAS só por ações; Q2: `SpawnVFXAction` sai com o `PlayCueAction`; Q3: sem roteador configurado, avisa e não toca) · Data: 2026-10-31 · Autor: Arquiteto

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`.

- `Runtime/GAS/Abilities/Actions/PlayCueAction.cs:9-46`: prefab e clipe; spawn sem devolução (`:40`); `AudioSource.PlayClipAtPoint` (`:43`). O B-012 (EV3) migra a classe para a base `GameplayAction`.
- `Runtime/GAS/Effects/Actions/CommonEffectActions.cs:21-83`: `SpawnVFXEffectAction`, que devolve na remoção pelos dados da instância (`:47-49`, `:58-71`). O B-012 (EV2) a migra como `SpawnVFXAction`.
- `Tests/GAS/PlayMode/SpawnActionsTests.cs:52-65` (`PlayCue_NoSpawner_NoPoolManager_NoException`) e `:68-87` (`SpawnVfx_ReleasesThroughConfiguredSpawner`): os únicos testes dessas ações.
- `Runtime/Core/Spawning/SpawnerAsset.cs:10-61`:
  - `Spawn(prefab, posição, rotação, parent)`, `Release`, `Default`, `OrDefault`;
  - `ReleaseWhenParticlesStop` (`:44-60`), que troca o `stopAction` por callback.
  - O RP-07 documenta o `null` de `Spawn`.
- `Runtime/Core/Sets/RuntimeSet.cs`: existe. `RuntimeAnchor<T>` está na tabela da 0012, mas **não existe** no código.
- `Runtime/Time/TimeSourceAsset.cs:10-67`:
  - `ElapsedTime`, `DeltaTime`, `Scaled` padrão;
  - registro estático `_live` para o reset de sessão (`:54-67`), que é o padrão a seguir para a âncora.
- `Runtime/GAS/Core/AbilitySystemComponent.cs:80-87`: consumo do tempo por diferença no `Update` (ADR 0014), o padrão a seguir no roteador.
- Tags:
  - `GameplayTag` é um `struct` com a definição (`Runtime/Tags/GameplayTag.cs:7-28`);
  - `GameplayTagDefinition.Matches` sobe pelo `parent` (`GameplayTagDefinition.cs:10-16`);
  - `GetDefinition()` público e `Parent` chegam no TG1.
- `IRandomSource` (Core) chega no EV4a do B-012.

## Respostas às perguntas técnicas do PRD (seção 8)

- **P7. Como o roteador é encontrado.** ADR 0037, item 2. `CueRouter` é um componente de cena. Ele se publica num `CueRouterAnchor` (`RuntimeAnchor<CueRouter>`) no `OnEnable` e sai no `OnDisable`. As ações do Bridges apontam a âncora. Não há roteador padrão global: âncora vazia avisa uma vez em desenvolvimento e não toca.
- **P8. Devolução do VFX.** O roteador devolve tudo o que um cue cria (ADR 0037, item 3). O `SpawnVFXAction` **sai do GAS** junto com o `PlayCueAction` (ADR 0037, item 6; pergunta Q2), então o GAS não guarda mais VFX nos dados da instância.
- **P9. `Cues` precisa de `Time`?** Sim (ADR 0037, item 1). Duração fixa e fim de clipe contam pelo `ITimeSource` do roteador, e pausa e câmera lenta valem. O "Em aberto" da 0012 fecha.
- **P10. `CueParameters` e `CueHandle`.**
  - `CueParameters` (`readonly struct`): `Position`, `Rotation`, `HasNormal`/`Normal`, `Target` (`Transform`), `Instigator` (`Transform`), `Magnitude`, `NormalizedMagnitude` (0 a 1; `HasMagnitude` falso usa o padrão da definição), `UserFloat` e `UserInt`.
  - Construtores `At(Vector3)` e `On(Transform)`, e `With…` que copiam.
  - Nada do GAS.
  - `CueHandle` (`readonly struct`): `(int slot, int generation)` e `IsValid`. Um slot reutilizado invalida os handles antigos.
- **P11. Prefab de áudio.** `Runtime/Cues/Prefabs/CueAudioSource.prefab` com `AudioSource` e o componente `CueAudioPlayer`. O roteador tem o campo `audioPrefab`, e o Inspector do roteador preenche o padrão do pacote com um botão. O fim do som é rastreado pelo roteador (duração do clipe ÷ |pitch|, pelo `ITimeSource`), sem corrotina. Com a fonte de tempo pausada, o objeto não volta antes de o tempo andar de novo.
- **P12. Momentos que o Bridges entrega.** Todos os momentos do R21 e do R23 viram abas de ação do B-012 (ADR 0037, item 5):
  - "Ao aplicar", "A cada tick" e "Ao remover" são `ExecuteCueAction` nas abas do efeito;
  - "Enquanto ativo" é `AddCueAction` em "Ao ser aplicado";
  - "Ao ativar", "Ao terminar" e "Ao ser cancelada" são as abas da habilidade.

  Efeito instantâneo: a aba "Ao ser aplicado" roda também nele (B-012, EV2), então o cue toca sem depender do relay do B-004.
- **P13. Cache por instância.** Não há: o handle do `AddCueAction` fica no `ActionFrame` da execução de vigia (ADR 0036), e o `OnCancel` da vigia o remove. Não há mapa por instância.

## Design

### 1. Core: `RuntimeAnchor<T>`

`Runtime/Core/Anchors/RuntimeAnchor.cs`:

```csharp
public abstract class RuntimeAnchor<T> : ScriptableObject where T : UnityEngine.Object
{
    public T Value { get; }                       // [NonSerialized]; nulo se o objeto foi destruído
    public bool HasValue { get; }
    public event Action<T> OnChanged;
    public void Provide(T value);                 // troca e avisa se mudou
    public void Unset(T value);                   // limpa só se for o mesmo
}
```

- A âncora volta a vazio ao entrar em Play, com ou sem domain reload, pelo mesmo registro de sessão do `TimeSourceAsset` (registro estático das instâncias vivas mais `SubsystemRegistration`).
- Os tipos concretos são não genéricos (`CueRouterAnchor`), por causa do `CreateAssetMenu` e do `[HelpURL]`. Servem também ao B-015 (`ActorResolver`).

### 2. Dados (`Runtime/Cues/`, namespace `SpellBook.Cues`)

- `CueKind { Burst, Looping }`, `CueLimitPolicy { DropNew, ReplaceOldest }` e `SoundSelection { RandomOne, All }`.
- `CueDefinition : ScriptableObject, IValidatable` (`Create > SpellBook > Cues > Cue`, `[HelpURL]`):
  - `GameplayTag Tag`, `CueKind Kind`;
  - `[SerializeReference, TypePicker] List<CueItem> Items`: todos tocam, VFX e itens próprios;
  - `[SerializeReference] List<SoundCueItem> Sounds` e `SoundSelection SoundSelection`;
  - `int MaxSimultaneous` (0 = sem limite), `CueLimitPolicy LimitPolicy`;
  - `float DefaultMagnitude` (normalizada, 0 a 1, padrão 1).
- `CueLibrary : ScriptableObject, IValidatable`: `List<CueDefinition> Cues`.
- `CueIndex` (classe interna do roteador): `Dictionary<GameplayTagDefinition, CueDefinition>` montado das bibliotecas uma vez (no `Awake` e ao trocar bibliotecas). `TryFind(tag)` procura a definição da tag, depois a do `Parent`, até a raiz, e devolve a primeira (a mais específica que casa, P4). Sem alocar.
- `CueItem` (`[Serializable] abstract`):
  - `public abstract void Play(in CueParameters p, CuePlayback playback)`;
  - `public virtual void Validate(ValidationResult r, string path, CueKind kind)`;
  - sem estado (ADR 0013).
- `CuePlayback` (classe do roteador, uma por slot, reutilizada):
  - `GameObject Spawn(GameObject prefab, SpawnerAsset spawner, in CueSpawnRequest req)`: cria pelo spawner (vazio: o padrão do roteador, senão `SpawnerAsset.Default`) e rastreia o objeto;
  - `IRandomSource Random`, `ITimeSource Time`;
  - `CueSpawnRequest` (`readonly struct`) traz posição, rotação, `CuePlacement`, alvo, deslocamento e `CueEnd`;
  - spawner que devolve `null`: pula com aviso uma vez em desenvolvimento e devolve `null` (R13).
- `CuePlacement { World, AttachToTarget, FollowTarget }`:
  - `AttachToTarget` copia posição e rotação do alvo, mais o deslocamento, a cada `Tick`;
  - `FollowTarget` copia só a posição;
  - nunca `SetParent` (ADR 0037).
- `CueEnd { ParticlesStop, FixedDuration, OnRemove, ClipEnd }`.
- `VfxCueItem`: prefab, spawner, placement, deslocamento, rotação, `bool scaleByMagnitude` com `Vector2 scaleRange`, `CueEnd end`, `float duration`.
  - `ParticlesStop`: o roteador devolve quando `ParticleSystem.IsAlive(true)` fica falso (sem `ReleaseWhenParticlesStop`, para um dono só).
  - Em `Remove` de `Looping`: `Stop()` nas partículas e devolve quando acabam. `OnRemove` e `FixedDuration` devolvem na hora.
- `SoundCueItem`: `AudioClip clip`, `Vector2 volume`, `Vector2 pitch` (sorteio uniforme pelo `IRandomSource`), `float spatialBlend`, `AudioMixerGroup mixer`, `bool loop` (só `Looping`), `bool volumeByMagnitude`.
  - Toca num objeto do `audioPrefab` do roteador, configurado pelo `CueAudioPlayer` (componente com `AudioSource` em cache), com o fim `ClipEnd`.
  - Com loop, o fim é `OnRemove`.

### 3. Roteador

`CueRouter : MonoBehaviour, IValidatable` (`[HelpURL]`):
- Campos: `List<CueLibrary> libraries`, `CueRouterAnchor anchor`, `TimeSourceAsset timeSource`, `SpawnerAsset defaultSpawner`, `GameObject audioPrefab`, `int capacity = 64` (slots pré-alocados; cresce dobrando, com aviso uma vez).
- API:
  - `bool Execute(GameplayTag tag, in CueParameters p)`: só `Burst`; `Looping` é rejeitado com aviso e devolve falso;
  - `CueHandle Add(GameplayTag tag, in CueParameters p)`: `Looping` ou `Burst`; o handle de um `Burst` deixa de valer quando ele acaba;
  - `bool Remove(CueHandle h)`, `void RemoveAll(GameplayTag tag)` (casamento hierárquico), `void RemoveAll()`;
  - `bool IsActive(CueHandle h)`, `int ActiveCount`;
  - `void Tick(float dt)`, `SetTimeSource(ITimeSource)` e `SetRandomSource(IRandomSource)`, como o ASC.
- O `Update` consome o tempo por diferença e chama `Tick`.
- Tag sem definição: devolve falso ou um handle inválido. Avisa uma vez por tag, só com `Debug.isDebugBuild` (C2).
- Limite: conta os slots ativos da mesma definição. `DropNew` não toca; `ReplaceOldest` remove o mais antigo e toca (C9).
- `Tick`, para cada slot ativo e cada objeto rastreado:
  1. posiciona;
  2. avança o temporizador;
  3. confere o fim e devolve.

  Em `Looping`, alvo destruído ou desativado (`AttachToTarget`/`FollowTarget`) remove o slot inteiro (C8). Em `Burst`, devolve só os objetos que seguiam aquele alvo. Slot sem objetos vivos volta ao livre e incrementa a geração.
- `OnDisable` faz `RemoveAll()` e `anchor.Unset(this)`.

### 4. Integração com o GAS (`SpellBook.GAS.Bridges`)

O asmdef do Bridges passa a referenciar `SpellBook.Cues`.

- `ExecuteCueAction : GameplayAction` (`[TypeCategory("Apresentação", Line = "Cue {Tag}")]`):
  - campos `CueRouterAnchor router`, `GameplayTag tag`, `CueMagnitude magnitude`;
  - `CueMagnitude` é `[Serializable]`, com o modo (`None`, `Value` com `ScalableFloat` e `%nome`, `SetByCaller` com nome) e `float max` para a normalizada.
  - Monta um `CueParameters` por alvo resolvido:
    - posição e `Target` = o alvo;
    - `Instigator` = transform do `context.Instigator`;
    - `Magnitude` avaliada no nível da execução, com o `EffectContext` quando há.
  - Sem alvo, usa `context.Point` (ou a posição do dono).
  - Devolve `Done` na hora.
- `AddCueAction : GameplayAction`: os mesmos campos, um cue `Looping` no primeiro alvo resolvido (ou no ponto).
  - Começa no ASC dono uma execução de vigia `ExecutionLink.FollowsOwner` (ADR 0036; ação interna `CueWatchAction`), com o handle em `ActionFrame.Index`/`Count`. A vigia espera até ser cancelada, e o `OnCancel` dela chama `Remove`.
  - A ação devolve `Done` na hora: a lista segue no mesmo quadro.
  - Sem roteador: nada, com aviso uma vez.
- Validação: tag inválida (erro); âncora vazia (aviso); `AddCueAction` fora de uma aba com dono (projétil `OnImpact`, sem dono lógico) gera aviso, porque o cue não terá quem o remova e vira `Burst` com aviso em runtime.
- **Saem do GAS:** `PlayCueAction` (remoção pura) e `SpawnVFXAction` (sai de `CommonEffectActions.cs`). Os dois testes de `SpawnActionsTests.cs` saem com elas (N8). Os casos equivalentes (spawner vazio e devolução pelo spawner) já são cobertos pelos testes do roteador (CU3, CU4).

### 5. Validação e Inspector

- `CueDefinition.Validate`:
  - sem tag (erro);
  - sem itens e sem sons (aviso);
  - item nulo (erro);
  - `VfxCueItem` sem prefab (erro), duração negativa (erro), `OnRemove` em `Burst` (erro);
  - `SoundCueItem` sem clipe (erro), faixas invertidas ou volume fora de 0 a 1 (erro), `loop` em `Burst` (erro);
  - `MaxSimultaneous` negativo (erro).
- `CueLibrary.Validate`: tag repetida (erro), definição nula (erro).
- `CueRouter.Validate`: sem biblioteca (aviso); sem `audioPrefab` com algum som nas bibliotecas (aviso); sem âncora (aviso).
- "Tag de cue sem definição" num efeito ou numa habilidade (R24) fica **fora** deste item: o validador precisaria ler bibliotecas de outro módulo a partir do GAS. Entra quando houver uma regra de projeto no `GAS.Bridges.Editor`.
- `Editor/Cues/` (`SpellBook.Cues.Editor`: Core, Tags, Time, Cues, Core.Editor), herdando de `SpellBookEditor`:
  - `CueDefinition`: listas com o resumo de uma linha pelo `SummaryFormatter` (AV1) e `Line` em cada item (por exemplo, `"VFX {Prefab} · {Placement} · {End}"`), e botão "Testar" só em Play (toca na câmera da cena ou na seleção, pelo primeiro `CueRouter` ativo de uma lista que o Editor monta com `FindObjectsByType`, só no Editor);
  - `CueLibrary` com busca por tag;
  - `CueRouter` com o botão "Usar prefab de áudio do pacote".

## Quebras de compatibilidade

Para o CHANGELOG e `manual/regras/migracao-2-para-3.md` (ADR 0009):
- **`PlayCueAction` sai.** Use `ExecuteCueAction` (Bridges) com uma `CueDefinition` por tag numa `CueLibrary` do `CueRouter`.
- **`SpawnVFXAction` (ex-`SpawnVFXEffectAction`) sai.** Use `AddCueAction` em "Ao ser aplicado" (VFX enquanto o efeito dura) ou `ExecuteCueAction` (explosão).
- `AudioSource.PlayClipAtPoint` não existe mais no pacote.
- Adicionado: `RuntimeAnchor<T>` (Core), módulo `SpellBook.Cues`, `ExecuteCueAction`, `AddCueAction`.

## Tickets

Todos usam a Unity e seguem o WIP 1. Tempo por `ManualTimeSource`, aleatoriedade por `SeededRandomSource` e objetos por factory (`CuesFactory`, `SpawnFactory`). Áudio real só no PlayMode. Cada ticket escreve a sua parte de `manual/modulos/cues.md` e do `CHANGELOG.md`. Teto: ~500 linhas e 10 arquivos.

**Conflitos com a fila:**
- `AsmdefRulesTests.cs`: CU2, CU4, CU7 e CU8, e também TM1, TM4 e TM5.
- `DocUrls.cs`: CU2, TM1 e TK4.
- `SpellBook.Testing.asmdef`: CU3 e TM2.
- `SpellBook.GAS.Bridges.asmdef`: CU8 e TM5.
- `SpellBookEditorCoverageTests.cs`: CU7 e TM4.
- `SpawnActionsTests.cs` e `CommonEffectActions.cs`: CU9, EV2 e EV3.
- `migracao-2-para-3.md`: CU9 e EV21.

### CU1 — `RuntimeAnchor<T>` no Core
- Escopo: `Runtime/Core/Anchors/RuntimeAnchor.cs` (novo), `Tests/Core/EditMode/RuntimeAnchorTests.cs` (novo; âncora de teste concreta), `Documentation~/manual/inicio/conceitos.md` (âncoras), `CHANGELOG.md`.
- Testes red: `Provide_SetsAndNotifiesOnce_SameValueNoEvent`; `Unset_OnlyClearsSameValue`; `DestroyedValue_ReadsAsNull`; `SessionStart_ClearsAllAnchors`; `Value_IsNotSerialized`.
- Critério de pronto: testes verdes; nenhum consumidor ainda.
- Tamanho estimado: ~220 linhas; 4 arquivos. Depende de: nada.

### Detalhe (2026-11-03)
- CA: nenhum CA do PRD diretamente; base do P7 e do Design 1 (ADR 0037, item 2; ADR 0012).
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/Core/Anchors/RuntimeAnchor.cs` (novo, namespace `SpellBook.Core`): `Value`, `HasValue`, `OnChanged`, `Provide`, `Unset`; registro das âncoras vivas numa classe interna não genérica `RuntimeAnchorRegistry` (a Unity não chama `RuntimeInitializeOnLoadMethod` em tipo genérico), com `ResetAllOnSessionStart` em `SubsystemRegistration`, como `Runtime/Time/TimeSourceAsset.cs:12,40-67`. O reset limpa o valor e os assinantes de `OnChanged`.
  - `Runtime/Core/AssemblyInfo.cs` (novo): `InternalsVisibleTo("SpellBook.Core.Tests.EditMode")`, consequência direta (o teste chama o reset interno, como `Runtime/Time/AssemblyInfo.cs:3`).
  - `Tests/Core/EditMode/RuntimeAnchorTests.cs` (novo, com `TestObjectAnchor : RuntimeAnchor<GameObject>`).
  - Sem `conceitos.md` nem `CHANGELOG.md`: manual só no passe de documentação (emenda de 2026-11-03 da 0007), e o ticket não quebra nada.
- Testes red (EditMode, `SpellBook.Core.Tests.EditMode`): `Provide_SetsAndNotifiesOnce_SameValueNoEvent`; `Unset_OnlyClearsSameValue`; `DestroyedValue_ReadsAsNull`; `SessionStart_ClearsAllAnchors`; `Value_IsNotSerialized`.
- Critério de pronto: os 5 testes verdes pelo filtro `\.RuntimeAnchorTests\.`; nenhum consumidor ainda.
- Tamanho: ~240 linhas e 3 arquivos, pela ADR 0042. Exceção: nenhuma.
- Commit: `feat(core): RuntimeAnchor<T> com reset de sessão`

### CU2 — Módulo, definição, biblioteca e índice por tag
- Escopo: `Runtime/Cues/SpellBook.Cues.asmdef` (novo; Core, Tags, Time), `CueDefinition.cs` (com os enums), `CueLibrary.cs` (com `CueIndex`), `CueItem.cs` (base, `CueParameters`, `CueHandle`, `CueSpawnRequest`, `CuePlacement`, `CueEnd`), `Tests/Cues/EditMode/SpellBook.Cues.Tests.EditMode.asmdef` (novo), `Tests/Cues/EditMode/CueIndexTests.cs` (novo), `AsmdefRulesTests.cs` (`Cues` em `RuntimeModules`; referências exatas: Core, Tags, Time), `DocUrls.cs` (`Cues = Base + "modulos/cues/"`), `Documentation~/manual/modulos/cues.md` (nova), `CHANGELOG.md`.
- Testes red: `Index_MostSpecificMatch_FallsBackToParent_NoneFalse` (CA3, C3); `Index_Lookup_DoesNotAllocate`; `CueParameters_DefaultsAndWith`; `Handle_DefaultIsInvalid`; `CuesAsmdef_ReferencesCoreTagsTime` (CA17, parte).
- Critério de pronto: testes verdes; `HelpUrlTests` e `ManualLinksTests` verdes.
- Tamanho estimado: ~460 linhas; 10 arquivos (1 página). Depende de: TG1.

### Detalhe (2026-11-03)
- CA: CA3 (C3, índice mais específico pelo `Parent`) e CA17 (parte: asmdef do Cues com Core, Tags e Time).
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/Cues/SpellBook.Cues.asmdef` (novo; referências Core, Tags e Time por GUID, como `Runtime/Teams/SpellBook.Teams.asmdef`).
  - `Runtime/Cues/AssemblyInfo.cs` (novo): `InternalsVisibleTo("SpellBook.Cues.Tests.EditMode")`, consequência direta (o `CueIndex` é interno, como `Runtime/Core/AssemblyInfo.cs`).
  - `Runtime/Cues/CueDefinition.cs` (novo): `CueKind`, `CueLimitPolicy`, `SoundSelection`; `CueDefinition` com `Tag`, `Kind`, `Items` (`[SerializeReference, TypePicker]`), `MaxSimultaneous`, `LimitPolicy`, `DefaultMagnitude` (0 a 1, padrão 1), `[HelpURL(DocUrls.Cues + "#cuedefinition")]`, menu `SpellBook/Cues/Cue`.
  - `Runtime/Cues/CueLibrary.cs` (novo): `CueLibrary` (`Cues`, `[HelpURL]`, menu `SpellBook/Cues/Cue Library`) e `internal sealed class CueIndex` (`Rebuild(IReadOnlyList<CueLibrary>)`, `TryFind(GameplayTag, out CueDefinition)` subindo por `GameplayTagDefinition.Parent` (`Runtime/Tags/GameplayTagDefinition.cs:13`), sem alocar; tag repetida: a primeira vence, e o erro é do CU6).
  - `Runtime/Cues/CueItem.cs` (novo): `CueItem` (base abstrata `[Serializable]`), `CueParameters` (`At`, `On`, `With…`), `CueHandle` (`IsValid`, construtor interno), `CueSpawnRequest`, `CuePlacement`, `CueEnd`.
  - `Tests/Cues/EditMode/SpellBook.Cues.Tests.EditMode.asmdef` e `Tests/Cues/EditMode/CueIndexTests.cs` (novos).
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:18-21` (`Cues` em `RuntimeModules`) e teste novo `CuesAsmdef_ReferencesCoreTagsTime`.
  - `Runtime/Core/DocUrls.cs:20` (`Cues = Base + "modulos/cues/"`).
  - Ficam para o ticket que traz o tipo de que dependem, sem mudar a API final do Design 2: `CueItem.Play` e o `CuePlayback` (CU3), `Sounds`/`SoundSelection` na definição (CU5), `IValidatable` e `Validate` (CU6). O `CueSpawnRequest` ganha `Duration`, que o fim `FixedDuration` pede.
  - Sem `cues.md` nem `CHANGELOG.md`: manual só no passe de documentação (emenda de 2026-11-03 da 0007), e o ticket não quebra nada. Sem a página, o `EveryDocUrlHasPage` e o `EveryHelpUrlHasPageAndAnchor` (categoria `Documentacao`) ficam vermelhos até o passe, como o `DocUrls.Teams`.
- Testes red (EditMode): `CueIndexTests` (`SpellBook.Cues.Tests.EditMode`): `Index_MostSpecificMatch_FallsBackToParent_NoneFalse`, `Index_Lookup_DoesNotAllocate`, `CueParameters_DefaultsAndWith`, `Handle_DefaultIsInvalid`; `AsmdefRulesTests` (`SpellBook.Core.Tests.EditMode`): `CuesAsmdef_ReferencesCoreTagsTime`, mais `AllExpectedAssembliesExist` e `EachRuntimeModuleHasEditModeTests` com o `Cues` na lista.
- Critério de pronto: os testes acima verdes pelo filtro `\.CueIndexTests\.;\.AsmdefRulesTests\.;\.HelpUrlTests\.`; `HelpUrlTests` verde. O `ManualLinksTests` fica para o passe de documentação.
- Tamanho: ~620 linhas e 9 arquivos, pela ADR 0042 (medido no diff). Exceção: +24% em linhas, linha do CU2 (#204) em `Documentation~/decisoes.md`.
- Commit: `feat(cues): módulo, definição, biblioteca e índice por tag`

### CU3 — Roteador: tocar, ligar, desligar, limite e tempo
- Escopo: `Runtime/Cues/CueRouter.cs`, `CueRouterAnchor.cs`, `CuePlayback.cs` (novos), `Tests/Cues/EditMode/CueRouterTests.cs` (novo; item de teste que cria um objeto vazio com fim fixo), `Tests/Testing/CuesFactory.cs` (novo: biblioteca, definição, roteador com `ManualTimeSource` e spawner que registra), `Tests/Testing/SpellBook.Testing.asmdef` (+Cues), `cues.md`, `CHANGELOG.md`.
- Testes red:
  - `Execute_PlaysAllItems_ReturnsTrue` (CA1, parte);
  - `UnknownTag_NoException_ReturnsFalse_WarnsOnce` (CA2, C2);
  - `Add_Remove_RepeatedAndStaleHandle_NoOp_RemoveAll` (CA7, C7);
  - `FixedDuration_ReleasesByTimeSource_PausedDoesNot` (CA5, parte);
  - `MaxSimultaneous_DropNew_ReplaceOldest` (CA9, C9);
  - `SpawnerReturnsNull_SkipsItem_PlaysRest` (CA10);
  - `Follow_CopiesPosition_TargetDestroyed_RemovesLooping` (CA8, C8);
  - `RouterPublishesInAnchor_UnsetOnDisable`;
  - `Execute_10000_WarmPool_DoesNotAllocate` (CA18, C10).
- Critério de pronto: os testes acima verdes.
- Tamanho estimado: ~490 linhas; 8 arquivos. Depende de: CU1, CU2.

### CU4 — VFX: partículas, duração e magnitude
- Escopo: `Runtime/Cues/VfxCueItem.cs` (novo), `CueRouter.cs` (fim por partículas), `Tests/Cues/PlayMode/SpellBook.Cues.Tests.PlayMode.asmdef` (novo), `Tests/Cues/PlayMode/VfxCueTests.cs` (novo), `AsmdefRulesTests.cs` (`Cues` em `PlayModeModules`), `cues.md`, `CHANGELOG.md`.
- Testes red: `Vfx_ParticlesStop_ReturnsToSpawner` (CA5, C5); `Vfx_FixedDuration_Returns_PausedDoesNot` (CA5); `Vfx_ScaleByMagnitude_DefaultWhenNone` (CA4, parte); `LoopingVfx_Remove_StopsThenReturns`; `AttachToTarget_CopiesRotation_NoParenting`.
- Critério de pronto: CA5 verde; CA4 com a parte de escala.
- Tamanho estimado: ~380 linhas; 7 arquivos. Depende de: CU3.

### CU5 — Som em pool
- Escopo: `Runtime/Cues/SoundCueItem.cs`, `CueAudioPlayer.cs` (novos), `Runtime/Cues/Prefabs/CueAudioSource.prefab` (novo, fixture), `CueRouter.cs` (`audioPrefab`, `SetRandomSource`, sorteio de um som ou de todos), `Tests/Cues/EditMode/SoundCueTests.cs` (novo; clipe por `AudioClip.Create`), `Tests/Cues/PlayMode/SoundPlaybackTests.cs` (novo), `cues.md`, `CHANGELOG.md`.
- Testes red:
  - `RandomOne_PicksBySeed_All_PlaysAll` (CA1 completo, CA12);
  - `VolumePitch_FromRangeBySeed_MagnitudeScalesVolume` (CA4, CA12);
  - `Sound_ReturnsAtClipEndOverPitch_ByTimeSource` (CA6, parte);
  - `TwoSounds_TwoObjects_LiveCountStable` (CA6, C6);
  - `LoopingSound_UntilRemove`;
  - `RealAudio_PlaysAndReturns` (PlayMode, N6).
- Critério de pronto: CA1, CA4, CA6 e CA12 verdes.
- Tamanho estimado: ~470 linhas; 8 arquivos (1 fixture). Depende de: CU4, EV4a (`IRandomSource`).

### CU6 — Validação
- Escopo: `CueDefinition.cs`, `CueLibrary.cs`, `CueRouter.cs`, `VfxCueItem.cs`, `SoundCueItem.cs` (`Validate`), `Tests/Cues/EditMode/CuesValidationTests.cs` (novo), `Documentation~/manual/ferramentas/validador.md`, `CHANGELOG.md`.
- Testes red: um por regra do Design 5 (CA20, parte do R15), com `propertyPath`; `ProjectValidator_ReportsCueAssets`.
- Critério de pronto: a parte do R15 no CA20 verde.
- Tamanho estimado: ~380 linhas; 8 arquivos. Depende de: CU5.

### CU7 — Inspector e "Testar"
- Escopo: `Editor/Cues/SpellBook.Cues.Editor.asmdef` (novo), `Editor/Cues/CuesEditors.cs` (biblioteca, roteador, âncora), `CueDefinitionEditor.cs` (novo), os `[TypeCategory(..., Line)]` em `VfxCueItem.cs` e `SoundCueItem.cs`, `AsmdefRulesTests.cs` (`Cues` em `EditorModules`), `SpellBookEditorCoverageTests.cs` (+`SpellBook.Cues`), `cues.md`, `CHANGELOG.md`.
- Testes red: `CuesEditorAsmdef_References`; coverage com o Cues; `ItemLine_SummaryFromTypeCategory` (pelo `SummaryFormatter`).
- Critério de pronto: CA19 (S) conferido pelo Examinador no host: listas com resumo e "Testar" em Play, com som real que volta ao pool.
- Tamanho estimado: ~420 linhas; 9 arquivos. Depende de: CU6, AV1.

### CU8 — Cues no GAS: `ExecuteCueAction` e `AddCueAction`
- Escopo: `Runtime/GAS.Bridges/SpellBook.GAS.Bridges.asmdef` (+Cues), `Runtime/GAS.Bridges/Cues/ExecuteCueAction.cs`, `AddCueAction.cs` (com `CueWatchAction` interna), `CueMagnitude.cs` (novos), `Tests/GAS.Bridges/PlayMode/SpellBook.GAS.Bridges.Tests.PlayMode.asmdef` (+Cues), `Tests/GAS.Bridges/PlayMode/CueActionsTests.cs` (novo), `AsmdefRulesTests.cs` (referências do Bridges), `cues.md` (seção "Cues no GAS", com a tabela dos momentos), `CHANGELOG.md`.
- Testes red:
  - `ExecuteCue_InAbility_PlaysWithContextParameters` (CA13, parte);
  - `ExecuteCue_InEffectTick_UsesEffectMagnitudeBySetByCaller`;
  - `AddCue_InEffectApplied_RemovedOnExpireRemoveTagAndStacksZero` (CA13, C12, os quatro motivos);
  - `AddCue_StackingDoesNotPlayAgain` (C12);
  - `AddCue_InAbility_RemovedWhenActivationEnds_DoesNotHoldActivation` (ADR 0036);
  - `EmptyAnchor_WarnsOnce_NoException`;
  - `SpawnerEmpty_InEffectCue_SkipsWithWarning` (CA15, C13);
  - `BridgesReferencesCues_GasDoesNot` (CA17).
- Critério de pronto: CA13, CA15 e CA17 verdes. CA14 do PRD é substituído pelo teste de vigia (ADR 0037, item 5).
- Tamanho estimado: ~470 linhas; 9 arquivos. Depende de: CU3, EV21, TK1 (`ExecutionLink`).

### CU9 — Fim do `PlayCueAction` e do `SpawnVFXAction`
- Escopo: `Runtime/GAS/Abilities/Actions/PlayCueAction.cs` (remoção pura), `Runtime/GAS/Effects/Actions/CommonEffectActions.cs` (sai `SpawnVFXAction`), `Tests/GAS/PlayMode/SpawnActionsTests.cs` (saem os dois testes, N8), `Tests/GAS/EditMode/GasSourceScanTests.cs` (guarda: `PlayClipAtPoint` em nenhum `.cs` do pacote; nenhuma ação do GAS chama `Instantiate`/`Destroy`), `Documentation~/manual/regras/migracao-2-para-3.md` (ação antiga → nova), `Documentation~/manual/modulos/gas.md` (tira as ações), `CHANGELOG.md` (quebra).
- Testes red: `NoPlayClipAtPoint_InPackage` (CA11, CA16); `GasHasNoVfxOrCueActions` (guarda textual). A suíte do GAS passa com só os dois testes removidos, listados no PR (CA22).
- Critério de pronto: CA11, CA16 e CA22 verdes; CA21 (E) conferido: CHANGELOG, página e `[HelpURL]`.
- Tamanho estimado: ~200 linhas; 6 arquivos (+1 remoção pura). Depende de: CU8.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| CU1 | sim | — | ~220 / 4 |
| CU2 | sim | TG1 | ~460 / 10 |
| CU3 | sim | CU1, CU2 | ~490 / 8 |
| CU4 | sim | CU3 | ~380 / 7 |
| CU5 | sim | CU4, EV4a | ~470 / 8 |
| CU6 | sim | CU5 | ~380 / 8 |
| CU7 | sim | CU6, AV1 | ~420 / 9 |
| CU8 | sim | CU3, EV21, TK1 | ~470 / 9 |
| CU9 | sim | CU8 | ~200 / 6 |

Fila: CU1 → CU7 podem entrar antes do fim do B-012, mas o CU5 espera o EV4a e o CU7 espera o AV1. CU8 e CU9 entram depois do EV21 e do TK1. CA11 e CA17 rodam em todos.

## Perguntas ao usuário

- **Q1. Cues no GAS só por ações (ADR 0037, item 5).** Proposta: no lugar da lista de tags de cue no efeito e na habilidade e do componente `AbilitySystemCueBridge` (R21 a R23, P6 aceita), entram duas ações no Bridges.
  - `ExecuteCueAction` cobre os momentos de explosão nas abas que já existem: ao aplicar, a cada tick, ao remover, ao ativar, ao terminar, ao ser cancelada.
  - `AddCueAction` cobre "enquanto ativo": o cue sai sozinho quando o efeito sai ou a ativação termina.

  Não há componente para lembrar de pôr no ASC, e o GAS não ganha campo de cue. Alternativa: o R21 a R23 como escritos. Seriam uma lista de "quando" no efeito ao lado das abas de ação e um componente de ponte por ASC.
- **Q2. `SpawnVFXAction` sai do GAS junto com o `PlayCueAction` (ADR 0037, item 6).** Proposta: feedback visual e sonoro é só do Cues, e quem usa o GAS sem o Cues cria uma ação própria. Alternativa: manter o `SpawnVFXAction` no GAS (com devolução, R20 do PRD) como VFX sem Cues, o que deixa dois jeitos de pôr VFX numa habilidade.
- **Q3. Sem roteador padrão global (R8).** Proposta: o roteador é achado só pela âncora configurada, e âncora vazia avisa e não toca. Alternativa: um roteador padrão do projeto, que pede um ponto global de configuração (asset em `Resources` ou Project Settings) que o pacote ainda não tem.
- A ADR 0037 passa pelo gate junto com esta Spec.
