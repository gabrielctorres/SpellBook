# Spec: Fase 0-M, modularização do SpellBook

Status: **Aprovada** (gate 2026-10-29) · Data: 2026-10-29 · Backlog: B-001 (e B-020, B-021, B-022 pela via rápida)
PRD: [prd/modularizacao.md](../prd/modularizacao.md) · Plano: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção I (decisões 1 a 44)
ADRs: 0001, 0002, 0003, 0004, 0005, 0006 (Adiada), 0007, 0008, 0009, 0010, 0011, 0012, 0013, 0014, 0015, 0016, 0025

Legenda:
- **Desvio:** o código obriga a fugir do PRD ou de uma decisão do usuário. Precisa de aprovação no gate.
- **Inferido:** valor escolhido pelo Arquiteto que fica para validação de uso (seção 9 do PRD, `/ajustar`).
- **Guarda:** teste estrutural de uma regra que o código já cumpre; não tem red natural (ADR 0004).
- **Caracterização:** teste escrito e rodado verde **antes** de uma refatoração sem comportamento novo, que precisa continuar verde depois. Substitui o red nesses tickets.

---

## 1. Pesquisa no código afetado

### 1.1 Assemblies e pacote

- `Runtime/SpellBook.Core.asmdef:1-16`: assembly único, `references: ["UnityEngine"]`, `overrideReferences: false`, `defineConstraints: []`. Tudo em `Runtime/` (GAS, eventos, pooling, matemática) compila nele. O GUID do `.meta` é `7834e8f3d0902ad45bfb6fab58f7b919`.
- `Editor/SpellBook.Editor.asmdef:5`: referencia o Core pelo GUID. Contém só `Editor/FolderStructureCreator.cs` (menu "Tools/Create Default Project Folders", sem relação com o domínio).
- `package.json`: `"version": "2.0.0"`, `"unity": "2021.3"`, `"documentationUrl": "https://example.com/"`.
- Não existe `Tests/`, nem asmdef de teste, nem `.github/`.
- O Odin é usado só em arquivos do GAS: `GameplayAbility.cs`, `GameplayEffect.cs`, `AttributeDefinition.cs`, `ModifierValueSource.cs:19-25`, `AbilityDescription.cs`, `CommonEffectActions.cs`, `ExpressionFormula.cs`, `SimpleAttributeFormula.cs`. Só atributos (`Sirenix.OdinInspector`); nenhum `SerializedScriptableObject` ou `Sirenix.Serialization`.
- Odin verificado num projeto local com Odin 3.3.x (fora deste repositório): o `ProjectSettings.asset` tem `ODIN_INSPECTOR`, `ODIN_INSPECTOR_3`, `ODIN_INSPECTOR_3_1` a `_3_3`; `ISelfValidator` está documentado em `Sirenix.OdinInspector.Attributes.xml` (namespace `Sirenix.OdinInspector`, assembly de atributos).

### 1.2 Projeto host

- A raiz do repositório é **ao mesmo tempo** a pasta do pacote (`package.json` na raiz) e um projeto Unity (`Assets/`, `Library/`, `Packages/`, `ProjectSettings/`, todos no `.gitignore`), versão 6000.3.5f2 (`ProjectSettings/ProjectVersion.txt`).
- `Packages/manifest.json` da raiz não referencia `com.spellbook`, não tem `testables` nem `com.unity.test-framework`. O host não tem o Odin (`Assets/Plugins/` não existe). Hoje nenhum código do pacote é compilado pelo host.

### 1.3 Tempo

- `Runtime/GAS/Core/AbilitySystemComponent.cs:52-58`: `Update` avança cada efeito com `Time.deltaTime`.
- `Runtime/GAS/Effects/ActiveEffectInstance.cs:13,31`: propriedade pública `StartTime = Time.time` (ninguém no pacote a lê). A duração já é guardada como restante (`TimeRemaining`, `:82`).
- `Runtime/GAS/Abilities/Projectiles/Projectile.cs:156,172,187,200`: `Time.deltaTime` dentro de `FixedUpdate`; `:131,137`: `WaitForSeconds` para invulnerabilidade inicial e tempo de vida.
- `WaitForSeconds` também em `WaitAction.cs:13`, `FlowActions.cs:28` (`RepeatAction`) e `SpawnProjectileAction.cs:102`.
- Recarga: `ActivateSpec` aplica o `CooldownEffect` com `GetCooldownDuration` (`AbilitySystemComponent.cs:199`), e `IsOnCooldown` (`:152-157`) olha `GrantedTags` e `GetCooldownTimeRemaining`. Recarga é um efeito com duração: avançar os efeitos avança a recarga.

### 1.4 Eventos

- `Runtime/Core/Events/` (2 arquivos): `GameEvent` em SO com `Raise(Component, object)` e `GameEventListner` (grafia errada).
- `Runtime/Core/NewEventSystem/` (19 arquivos): `BaseGameEvent<T>` (`CustomEvents/BaseGameEvent.cs:6-31`, itera de trás para frente sem proteção contra desinscrição de outro ouvinte), canais Void/Int/Float/String/Component, listeners e `UnityEvent`s concretos, `Void.cs`. `BaseGameEventListener.cs:3` tem `using SpellBook.Core.Events`.
- `Runtime/Systems/Events/` (5 arquivos): `EventManager` estático por `Dictionary<Type, Delegate>` (`EventManager.cs:12`), sem reset; `GameEvent`/`GameEventListener` por string; `IEvent`; `GAS_Events.cs` com `AbilitySlotUpdateEvent` e `AbilityActivatedEvent`, que referenciam o GAS (`GAS_Events.cs:1-2`).
- `AbilitySystemComponent.cs:217`: `EventManager.Trigger(new AbilityActivatedEvent(...))`. É o único uso de eventos globais pelo GAS.
- O namespace `SpellBook.Events` já existe (em `Systems/Events`); o módulo novo usa o mesmo nome, então os tipos antigos precisam sumir **antes** do módulo novo nascer, para não haver dois `SpellBook.Events.IEvent`.

### 1.5 Spawn e pool

- `SpawnProjectileAction.cs:78,83`: `PoolManager.Instance.Get(...)` e `GetPooler(...)`; NRE sem `PoolManager` na cena.
- `PlayCueAction.cs:37`: `PoolManager.Instance.Get(...)`; `:41` `AudioSource.PlayClipAtPoint`. O VFX nunca volta ao pool.
- `CommonEffectActions.cs:42,58,71`: `SpawnVFXEffectAction` com `Instantiate`, `Destroy(obj, t)` e `Destroy` no `Cleanup`.
- `Projectile.cs:15,58,91-103,258-283`: implementa `IPoolable`, guarda `ComponentPooler` e devolve por `_originPool.ReturnToPool` ou `Destroy`.
- `Systems/Pooling/PoolManager.cs:12,19-31`: singleton com `DontDestroyOnLoad`; `:51-59` cria pool dinâmico (tamanho 5, expansível) quando o prefab não tem pool.
- `ComponentPooler.cs:19-83`: fila por prefab, chama `IPoolable.OnSpawnFromPool/OnReturnToPool`, adiciona `PoolableObject` às instâncias.

### 1.6 Tags

- `Runtime/GAS/Tags/GameplayTag.cs:27`: `internal GameplayTagDefinition GetDefinition()`. Usado em `AbilitySystemComponent.cs:336,345,374` (dicionário `_activeTags` com chave `GameplayTagDefinition`).
- Ao mover as tags para outro assembly, o ASC perde acesso a esse `internal`. Equivalência verificada: `GameplayTag.Equals`/`GetHashCode` usam a definição, e `GameplayTag.Matches` faz `definition.Matches(other.definition)`. Logo o ASC pode usar `GameplayTag` como chave e `activeTag.Matches(tag)` no `HasTag`, com o mesmo comportamento e sem API nova (R27 e B-006 preservados).
- Consumidores de `SpellBook.GAS.Tags`: `AbilitySystemComponent.cs:5`, `GameplayAbility.cs:5`, `GameplayEffect.cs:4`, `FlowActions.cs:5`, `TargetingStrategy.cs:4`.
- `GameplayAbility.cs:42` tem `[FormerlySerializedAs("_cooldownReductionAttribute")]` (removido pela 0009).

### 1.7 Outros

- `Runtime/MathTools/Math.cs:7-109`: `MonoBehaviour Math` com a classe estática aninhada `MathTools`.
- `Runtime/Core/Sets/RuntimeSet.cs:5`: namespace `SpellBook.Core.RuntimeSets`.
- `Runtime/GAS/GAS_Documentation.md` (+ `.meta`) dentro do runtime; a linha 98 cita o `AbilityActivatedEvent` global.
- **Conflito de nome do namespace `SpellBook.Time`:** dentro de qualquer `namespace SpellBook.*`, o nome `Time` resolve primeiro para o namespace `SpellBook.Time` (se o assembly estiver referenciado), antes de `UnityEngine.Time` vindo de `using`. Consequência: o assembly que passa a referenciar `SpellBook.Time` precisa, no mesmo ticket, eliminar todo `Time.xxx` não qualificado. É um efeito desejável (o compilador reforça a ADR 0014: fora do módulo Time ninguém lê `UnityEngine.Time`), mas ordena os tickets.
- Testes em EditMode: `AddComponent` não chama `Awake`, `OnEnable` nem `Update`. ASC (`Awake` monta o `AttributeSet`, `OnEnable` liga o cancelamento por tag) e `EventListener` (inscrição no `OnEnable`) só se comportam de verdade em PlayMode (ADR 0004).

---

## 2. ADRs respeitadas

| ADR | Como a Spec respeita |
|---|---|
| 0001 | Spec em `spec/`; manual em `Documentation~/manual/`; `GAS_Documentation.md` sai do runtime (0M-10) |
| 0002 | Tickets de processo pela via rápida (0M-01 a 0M-04) não criam ADR nem quebram API; issues em `gabrielctorres/SpellBook` com labels da 0002 |
| 0003 | Todo ticket ≤ ~400 linhas de código e ≤ 10 arquivos (páginas e fixtures contam só nos arquivos; arquivos apagados por inteiro, sem substituto, não contam em nenhum dos dois, decisão I42); WIP 1 |
| 0004 | Red antes do código; testes de guarda e de caracterização marcados; ciclo de vida em PlayMode; host em `HostProject~/` (decisão I41) |
| 0005 | Fábrica e Examinador criam e removem `.claude/editor.lock` antes de usar a Unity no host |
| 0006 | Adiada: uma branch por ticket no mesmo checkout |
| 0007 | Cada ticket que cria componente ou SO público traz `[HelpURL]` e a seção do manual; constantes de página em `DocUrls` |
| 0008 | Commits Conventional com escopo do módulo; o `version` (`3.0.0-pre.1`) e o CHANGELOG mudam só no release (DevOps); a 2.x está congelada, sem `release/2.x` (decisão I43) |
| 0009 | Sem `[MovedFrom]`, `[Obsolete]` nem `[FormerlySerializedAs]`; quebras listadas na seção 7 |
| 0010 | `"unity": "6000.0"`; CI na 6000.0 e na última 6000.x |
| 0011 | `defineConstraints: ["ODIN_INSPECTOR"]` em todo asmdef exceto Setup; runtime com `overrideReferences` e só o DLL de atributos |
| 0012 | Tabela de módulos com a emenda Events → Time; asmdefs auxiliares listados |
| 0013 | Reações não genéricas; `EventEmission<T>` genérica criada por código, verificada no 0M-32 |
| 0014 | `ITimeSource { ElapsedTime; DeltaTime }`, consumo por diferença; grupo pausável segue o escalado, só pausa; reset por `SubsystemRegistration` |
| 0015 | `SpawnerAsset` com `GameObject`; `PooledSpawnerAsset` sem `PoolManager.Instance`; `PlayCueAction` só troca o spawn |
| 0016 | Canais, bus, ouvinte, emissor e reações conforme as decisões incorporadas |
| 0025 | `Documentation~/mkdocs.yml` com `docs_dir: manual`; Action do Pages; Odin no CI por secret, bloqueado pela P21 |

---

## 3. Desvios e valores inferidos

### Desvios

- **D1. Host de testes em `HostProject~/`, não na raiz** (PRD R41, CA55). **Resolvido pela decisão I41**, que substitui a I32. Motivo técnico na ADR 0004: um `file:` apontando para a raiz faria a Unity importar `Assets/`, `Library/` e `ProjectSettings/` do próprio host como conteúdo do pacote. O host usa `HostProject~/Packages/manifest.json` com o pacote por `file:` e em `testables`, e o Odin em `HostProject~/Assets/Plugins/Sirenix`, no `.gitignore`. Leitura do CA55 com a I41: "`HostProject~/Packages/manifest.json`" e "`.gitignore` contém `HostProject~/Assets/Plugins/Sirenix/`" (o Criativo ajusta o PRD).
  - **Caminho relativo:** a I41 escreve `file:../`. A Unity resolve caminhos relativos do `manifest.json` a partir da pasta `Packages/` do projeto, não da raiz dele. Por isso a raiz do repositório, vista de `HostProject~/Packages/`, é `file:../..`. O 0M-06 confirma o caminho no red: o Package Manager precisa mostrar `com.spellbook` como pacote local. Se `file:../` resolver, fica `file:../`.
- **D2. Nome do canal sem payload** (PRD R12). `EventChannel` é a base abstrata não genérica (descrição, `logRaises`, tipo do payload, ouvintes vivos) e o canal sem payload concreto é `VoidEventChannel : EventChannel<Void>` com `Raise()`. Motivo: um `EventChannel` concreto e um `EventChannel<T>` com o mesmo nome confundem o seletor, o menu Create e o filtro por tipo, e o debugger precisa de uma base comum. O menu continua "Void Channel" (C2 e CA12 inalterados).
- **D3. Alocação do `ASC.Tick` com efeito periódico** (PRD N2). **Aceito pela decisão I44** até o B-002. `AttributeState.Recalculate` usa LINQ (`AttributeState.cs:69-82`) e roda a cada tick periódico (`ApplyModifiersToBase` → `SetBaseValue`). A correção é do B-002 (fora da 0-M). O teste de N2 da 0-M mede o `Tick` em regime com efeitos de duração **sem** tick periódico naquele quadro.
- **D4. `RaiseEventAction` saiu do B-004** (PRD seção 6 antiga). Já reconciliado pelo Criativo (R25a, decisões I22 e I39); listado só para rastreio.

### Inferidos (validar no uso)

| Id | Valor | Onde |
|---|---|---|
| I-a | Odin mínimo **3.3** (versão verificada) | README, instalação, 0011 |
| I-b | Grupo pausável segue o tempo escalado, só pausa, sem escala própria | 0014, 0M-18 |
| I-c | Pool criado sob demanda pelo `PooledSpawnerAsset`: tamanho inicial 5, expansível (os valores do fallback atual do `PoolManager.cs:56-57`), configuráveis no asset | 0M-23 |
| I-d | Histórico do Event Debugger: buffer circular de 256 disparos por sessão de Play | 0M-35 |
| I-e | Navegação do site gerada da árvore de pastas (sem `nav:` explícito), para nenhum ticket precisar editar a configuração ao criar página | 0M-08 |
| I-f | Âncoras de `[HelpURL]` explícitas com `attr_list` (`## EventListener {#eventlistener}`) | 0M-08, CA54 |
| I-g | `com.unity.test-framework` na versão que acompanha a 6000.0 escolhida no host, fixada no manifest | 0M-06 |
| I-h | Menus de criação dos assets do GAS continuam `GAS/...` (R27 pede tags sem mudança de comportamento); padronizar para `SpellBook/...` fica para a Fase 9 | 0M-14 |
| I-i | Uma espera pendente do Invoke com delay aloca uma entrada por disparo (fora do caminho sem alocação do N2) | 0M-31 |
| I-j | Patch exato da 6000.0 e da última 6000.x no CI e no `ProjectVersion.txt` do host: o mais recente disponível no GameCI na data do ticket | 0M-06, 0M-11 |

---

## 4. Desenho técnico por módulo

### 4.1 Mapa final de pastas e asmdefs

```
Runtime/
  Core/      SpellBook.Core.asmdef (o mesmo .meta de hoje, movido no 0M-40)
             DocUrls.cs, Void.cs, MathTools.cs, Sets/RuntimeSet.cs,
             Spawning/SpawnerAsset.cs, Spawning/InstantiateSpawnerAsset.cs, Spawning/ParticleStopReleaser.cs (internal)
  Time/      SpellBook.Time.asmdef: ITimeSource, TimeSourceAsset, ScaledTimeSource, UnscaledTimeSource,
             PausableTimeSource, ManualTimeSource
  Events/    SpellBook.Events.asmdef: IEvent, EventBus<T>, EventRegistry, ListenerList<T>,
             EventChannel(+<T>), 7 canais, EventResponse(+<T>), BuiltInResponses, EventListener,
             Reaction, Reactions/*, EventEmission(+<T>), EventEmitter
  Pooling/   SpellBook.Pooling.asmdef: PoolManager, ComponentPooler, PoolConfiguration, PoolableObject,
             IPoolable, PooledSpawnerAsset
  Tags/      SpellBook.Tags.asmdef: GameplayTag, GameplayTagDefinition
  GAS/       SpellBook.GAS.asmdef (subpastas atuais)
  GAS.Bridges/ SpellBook.GAS.Bridges.asmdef: RaiseEventAction
Editor/
  Core/      SpellBook.Core.Editor.asmdef (renomeado, mesmo .meta): ProjectValidator, ValidationWindow, FolderStructureCreator
  Events/    SpellBook.Events.Editor.asmdef: ChannelCatalog, ChannelPickerDrawer, EventDebuggerWindow,
             EventDebuggerModel, ChannelReferenceFinder
  Setup/     SpellBook.Setup.Editor.asmdef: OdinMissingNotice
Tests/
  Testing/   SpellBook.Testing.asmdef: GasFactory, TimeFactory, EventsFactory, SpawnFactory
  <Módulo>/EditMode, <Módulo>/PlayMode
HostProject~/ (decisão I41)
Documentation~/mkdocs.yml, requirements-docs.txt, manual/
.github/workflows/docs.yml, tests.yml
```

Regras de todos os asmdefs do pacote (exceto Setup): `defineConstraints: ["ODIN_INSPECTOR"]`. Runtime: `overrideReferences: true`, `precompiledReferences: ["Sirenix.OdinInspector.Attributes.dll"]`, `autoReferenced: true`. Editor: `overrideReferences: false`, `includePlatforms: ["Editor"]`, `autoReferenced: true`. Testes e `SpellBook.Testing`: `defineConstraints` também com `UNITY_INCLUDE_TESTS`, `autoReferenced: false`, `references` com `UnityEngine.TestRunner` e `UnityEditor.TestRunner`, `precompiledReferences: ["nunit.framework.dll", "Sirenix.OdinInspector.Attributes.dll"]`; EditMode com `includePlatforms: ["Editor"]`.

### 4.2 Core

```csharp
namespace SpellBook.Core
{
    public static class DocUrls
    {
        public const string Base = "https://gabrielctorres.github.io/SpellBook/";
        public const string Install = Base + "inicio/instalacao/";
        public const string Concepts = Base + "inicio/conceitos/";
        public const string Migration = Base + "regras/migracao-2-para-3/";
        public const string Time = Base + "modulos/tempo/";
        public const string Events = Base + "modulos/eventos/";
        public const string Pooling = Base + "modulos/pooling/";
        public const string Tags = Base + "modulos/tags/";
        public const string Gas = Base + "modulos/gas/";
        public const string Validator = Base + "ferramentas/validador/";
        public const string EventDebugger = Base + "ferramentas/event-debugger/";
    }
    [Serializable] public struct Void { }
    public static class MathTools { /* os 8 métodos atuais, sem mudança */ }
    public abstract class RuntimeSet<T> : ScriptableObject { /* sem mudança, só namespace */ }
}
```

- Todas as constantes de página nascem juntas no 0M-08, para nenhum ticket posterior editar `DocUrls.cs` só para ganhar uma página. O teste CA54 (0M-40) garante que cada uma tem página.
- `Void` mantém o nome do PRD. Risco: em arquivo com `using System;` o nome fica ambíguo com `System.Void`; o pacote escreve `SpellBook.Core.Void` onde houver `using System;` e o manual avisa em "Armadilhas".
- `SpawnerAsset`: ver 0015 (assinatura, `Default`, `OrDefault`, `ReleaseWhenParticlesStop`). `InstantiateSpawnerAsset` usa `Object.Instantiate(prefab, position, rotation, parent)`; `Release` usa `Destroy` em Play e `DestroyImmediate` fora dele. `ParticleStopReleaser` é `internal sealed` (fora do escopo do `[HelpURL]`), ligado por `ReleaseWhenParticlesStop`: define `main.stopAction = Callback` e, em `OnParticleSystemStopped`, chama `spawner.Release(gameObject)`.

### 4.3 Time

```csharp
namespace SpellBook.Time
{
    public interface ITimeSource { double ElapsedTime { get; } float DeltaTime { get; } }

    public abstract class TimeSourceAsset : ScriptableObject, ITimeSource
    {
        public abstract double ElapsedTime { get; }
        public abstract float DeltaTime { get; }
        protected virtual void ResetState() { }          // chamado ao entrar em Play
        protected virtual void OnEnable();                // registra a instância
        protected virtual void OnDisable();               // desregistra
        public static TimeSourceAsset Scaled { get; }     // instância padrão, HideFlags.DontSave
    }
    [CreateAssetMenu(menuName = "SpellBook/Time/Scaled Time Source")]    public sealed class ScaledTimeSource : TimeSourceAsset { }
    [CreateAssetMenu(menuName = "SpellBook/Time/Unscaled Time Source")]  public sealed class UnscaledTimeSource : TimeSourceAsset { }
    [CreateAssetMenu(menuName = "SpellBook/Time/Pausable Time Group")]   public sealed class PausableTimeSource : TimeSourceAsset
    {
        public bool IsPaused { get; }
        public void Pause(); public void Resume(); public void SetPaused(bool paused);
    }
    public sealed class ManualTimeSource : TimeSourceAsset   // sem menu
    {
        public void Advance(float seconds);  // seconds >= 0; ElapsedTime += seconds; DeltaTime = seconds
    }
}
```

- Leitura da Unity só aqui, sempre qualificada (`UnityEngine.Time.timeAsDouble`), por causa do conflito de nome (1.7).
- `PausableTimeSource`: `ElapsedTime = escalado - pausadoAcumulado - inícioDaSessão`, calculado sob demanda (sem `Update`); `DeltaTime = IsPaused ? 0 : UnityEngine.Time.deltaTime`.
- Reset: registro estático de instâncias vivas; `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` chama `ResetState()` em todas (P20).
- **Consumo por diferença** (0014): o consumidor guarda o último `ElapsedTime` lido; ao ligar (`OnEnable` ou ao trocar a fonte) o cursor recebe o valor atual, para não haver salto no primeiro quadro.

### 4.4 GAS (mudanças da 0-M)

- **ASC:**
  - `[SerializeField] TimeSourceAsset timeSource` (vazio → `TimeSourceAsset.Scaled`), `[SerializeField] bool autoTick = true`.
  - `public ITimeSource TimeSource { get; }`, `public void SetTimeSource(ITimeSource source)` (código e testes; reinicia o cursor).
  - `public void Tick(float deltaTime)`: ignora `deltaTime <= 0`, soma em `public double TickedTime { get; }` (relógio próprio do ASC) e avança os efeitos de trás para frente, como hoje.
  - `Update`: se `autoTick`, `Tick((float)(fonte.ElapsedTime - cursor))`.
  - Tags: `_activeTags` passa a `Dictionary<GameplayTag, int>`; `HasTag` itera as chaves com `Matches`.
  - Remove `EventManager.Trigger` (`:217`). Os eventos C# continuam.
- **`ActiveEffectInstance`:** remove `StartTime` (quebra; ninguém no pacote o lê) e o `using UnityEngine` deixa de ser preciso para tempo.
- **Esperas das ações** (0M-22): `AbilityContext.WaitSeconds(float seconds)` devolve um `IEnumerator` que espera, quadro a quadro, até o `Source.TickedTime` avançar `seconds` (conta pelo restante: `restante -= TickedTime - último`). `WaitAction`, `RepeatAction.Interval` e `SpawnProjectileAction.DelayBetweenShots` usam isso. Se o ASC for destruído, a espera termina.
- **`Projectile`:** `[SerializeField] TimeSourceAsset timeSource` (vazio → escalado); `FixedUpdate` lê o delta por diferença de `ElapsedTime`; tempo de vida e invulnerabilidade viram contadores de restante decrementados no `FixedUpdate`, sem corrotina. `Setup(SpawnerAsset spawner, AbilityContext parentContext, List<AbilityAction> onHitActions)` (e a sobrecarga com `AbilitySystemComponent`); `ReturnToPool()` chama `SpawnerAsset.OrDefault(spawner).Release(gameObject)`. Não implementa mais `IPoolable`; `OnEnable` reinicia o estado.
- **`SpawnProjectileAction`**, **`PlayCueAction`**, **`SpawnVFXEffectAction`:** campo `public SpawnerAsset Spawner` (vazio → `SpawnerAsset.Default`). `SpawnVFXEffectAction.Cleanup` devolve pelo mesmo spawner; o ramo sem `DestroyOnRemoval` usa `ReleaseWhenParticlesStop`. `PlayCueAction` mantém `PlayClipAtPoint` e não devolve o VFX (I37).
- `[HelpURL]` em todos os tipos públicos concretos que são `MonoBehaviour` ou `ScriptableObject`, com âncora na página GAS.

### 4.5 Tags

- `Runtime/Tags/`, namespace `SpellBook.Tags`, sem mudança de API (o `GetDefinition` continua `internal`). `[HelpURL(DocUrls.Tags + "#gameplaytagdefinition")]`.

### 4.6 Pooling

- Os cinco arquivos de `Runtime/Systems/Pooling/` vão para `Runtime/Pooling/`, namespace `SpellBook.Pooling`, com `[HelpURL]` nos públicos concretos. `PoolConfiguration` muda o menu de `PoolingSystem/Pool Configuration` para `SpellBook/Pooling/Pool Configuration`.
- `PooledSpawnerAsset : SpawnerAsset, ISelfValidator` (`Create > SpellBook > Pooling > Pooled Spawner`): campos `initialSize = 5`, `canExpand = true`; dicionário `prefab → ComponentPooler` em memória; raiz `[SpellBook Pools]` criada sob demanda (`DontDestroyOnLoad` só se `Application.isPlaying`); `Release` procura o `PoolableObject` e devolve ao pool de origem, ou destrói se a instância não for de pool. Validação: `initialSize < 0` é erro. Cache limpo ao entrar em Play (`SubsystemRegistration`).

### 4.7 Events

- **`ListenerList<T>`** (interno, compartilhado por bus e canal): `List<Action<T>>` + profundidade de disparo + "sujo". `Raise` fixa `count` no início; desinscrever durante um disparo põe `null` na posição; ao fim do disparo mais externo, compacta. Inscrever duas vezes o mesmo delegate é ignorado. Em `UNITY_EDITOR || DEVELOPMENT_BUILD`, cada chamada fica em `try/catch` com `Debug.LogException`.
- **`EventBus<T> where T : struct, IEvent`**: `Subscribe(Action<T>)`, `Unsubscribe(Action<T>)`, `Raise(in T)`; construtor estático registra o `Clear` no `EventRegistry`.
- **`EventRegistry`** (interno): lista de ações de limpeza; `SubsystemRegistration` executa todas (CA19). Exposto aos testes por `InternalsVisibleTo("SpellBook.Events.Tests.EditMode")` e `.PlayMode`.
- **`EventChannel`** (abstrato): `[TextArea] description`, `logRaises`, `abstract Type PayloadType`, `int ListenerCount`, `void GetListeners(List<object> targets)` (para o debugger). **`EventChannel<T>`**: `event Action<T> Raised` (add/remove para o `ListenerList<T>`), `Raise(T payload)`, `Raise(T payload, UnityEngine.Object sender)`, `T LastPayload`, botão `[Button]` "Raise" com `[EnableIf("@UnityEngine.Application.isPlaying")]` e um payload de teste para o botão. Hook de observação estático (`internal static Action<EventChannel, object, UnityEngine.Object> Observed`), chamado só se não for nulo e só em `UNITY_EDITOR`; é por onde o debugger grava o histórico.
- **Canais:** `VoidEventChannel` (`Raise()`), `IntEventChannel`, `FloatEventChannel`, `BoolEventChannel`, `StringEventChannel`, `Vector3EventChannel`, `GameObjectEventChannel`, cada um num arquivo, menu `SpellBook/Events/<Tipo> Channel`.
- **`EventResponse`** (abstrato `[Serializable]`): `abstract EventChannel Channel`, `Subscribe(EventListener)`, `Unsubscribe()`, `Validate(SelfValidationResult)`. **`EventResponse<T>`**: `[SerializeField] EventChannel<T> channel`, `[SerializeReference] List<Reaction> reactions`, `[FoldoutGroup("Escape (UnityEvent)", expanded: false)] UnityEvent<T> escape`. Respostas prontas num arquivo (`VoidEventResponse` ... `GameObjectEventResponse`). Payload próprio: `XEventChannel : EventChannel<X>` + `XEventResponse : EventResponse<X>` (R14).
- **`EventListener : MonoBehaviour, ISelfValidator`**: `[SerializeReference] List<EventResponse> responses`; inscreve no `OnEnable`, desinscreve e cancela esperas no `OnDisable`; `Update` só processa esperas pendentes (sai cedo se não houver). Ordem: reações da lista, depois o `escape`.
- **`Reaction`** (abstrato `[Serializable]`): `virtual void Execute(EventListener listener)`, `virtual void Execute<T>(EventListener listener, in T payload)` (padrão: chama o primeiro), `virtual void Validate(SelfValidationResult result, Type payloadType)`.
- **Reações prontas:** `SetActiveReaction { GameObject target; bool active }`, `ToggleComponentReaction { Behaviour target; Mode { Enable, Disable, Toggle } }`, `LogReaction { string message; LogType type = Log }`, `RaiseChannelReaction { Mode { ForwardPayload, RaiseVoid }; EventChannel target }` (no modo `ForwardPayload`, só dispara se `target is EventChannel<T>`; o validador da resposta acusa tipo diferente), `DelayedReaction { float delay; TimeSourceAsset timeSource; [SerializeReference] List<Reaction> reactions }`.
- **`EventEmission`** (abstrato) e **`EventEmission<T>`** (`EventChannel<T> channel`, `T payload`, `Raise(sender)`), com `static EventEmission CreateFor(EventChannel channel)` por `MakeGenericType`.
- **`EventEmitter : MonoBehaviour, ISelfValidator`**: `[SerializeField] EventChannel channel`, `[SerializeReference, HideReferenceObjectPicker] EventEmission emission` recriada quando o tipo do canal muda (`[OnValueChanged]`), `public void Raise()` e botão no Play.
- **Editor:** `ChannelCatalog.Filter(IEnumerable<EventChannel>, Type payloadType)` (função pura, CA21 e CA58) e `ChannelCatalog.FindAll()` (AssetDatabase); drawer Odin para campos `EventChannel` dentro de respostas, emissões e `RaiseChannelReaction` (modo repassar), com a descrição do canal num `InfoBox`; `EventDebuggerWindow` + `EventDebuggerModel` (busca, histórico, limpar); `ChannelReferenceFinder.Find(EventChannel)` que varre prefabs, ScriptableObjects e cenas carregadas por `SerializedObject`, com papel decidido pelo tipo dono da referência (`EventEmission` e `RaiseChannelReaction` → emite; `EventResponse` → ouve).

### 4.8 GAS.Bridges

- `RaiseEventAction : AbilityAction` com `[SerializeField] EventChannel channel` e `[SerializeReference] EventEmission emission`, o mesmo mecanismo do emissor (canal sem payload ou `EventChannel<T>` com payload fixo, decisão I39). `Execute` dispara uma vez e termina no mesmo quadro.
- **Validação de ações** (R35, "`RaiseEventAction` sem canal"): `AbilityAction` não é `UnityEngine.Object` e não pode implementar `ISelfValidator` sozinha. `AbilityAction` ganha `public virtual void Validate(SelfValidationResult result)` (vazio por padrão), e o `GameplayAbility` implementa `ISelfValidator` repassando para cada ação da lista. O GAS continua sem conhecer o tipo concreto (0013).

### 4.9 Validação (Editor)

- `ProjectValidator.Validate(IEnumerable<UnityEngine.Object> targets, List<ValidationIssue> issues)`: para cada alvo e cada componente que implementa `ISelfValidator`, cria um `SelfValidationResult`, chama `Validate` e converte os itens em `ValidationIssue { Severity, Message, Object Context }`.
- `ProjectValidator.CollectProjectTargets()`: assets de `ScriptableObject` e prefabs do projeto (`AssetDatabase.FindAssets`) mais objetos das cenas abertas.
- Regra de `[HelpURL]` (R35, último item): função `HelpUrlRule.FindViolations(IEnumerable<Assembly>)`, usada pelo teste CA36 e pela janela.
- Menu `SpellBook/Validar projeto` abre `ValidationWindow` (Odin) com a lista; clicar seleciona e faz ping no objeto (CA34).
- **Plano B** se `SelfValidationResult` não puder ser criado e lido fora do Inspector (verificado no red do 0M-23): uma interface própria `SpellBook.Core.IValidatable { void Validate(List<ValidationMessage> messages); }` no Core, e os tipos implementam `ISelfValidator` repassando para ela. Muda só a implementação dos validadores, não o comportamento visível.

### 4.10 Setup sem Odin

- `OdinMissingNotice` (`[InitializeOnLoad]`, todo o corpo em `#if !ODIN_INSPECTOR`): lê `PackageInfo.FindForAssetPath("Packages/com.spellbook")?.documentationUrl`, monta `<url>inicio/instalacao/`, escreve `Debug.LogError` e, se `!Application.isBatchMode && !SessionState.GetBool(chave)`, mostra `EditorUtility.DisplayDialog` com botão "Abrir instalação" (`Application.OpenURL`).

### 4.11 Documentação

- `Documentation~/mkdocs.yml`: `site_url: https://gabrielctorres.github.io/SpellBook/`, `docs_dir: manual`, `use_directory_urls: true`, tema Material (ou Zensical, escolha no 0M-08), `markdown_extensions: [attr_list, admonition, toc]`, sem `nav:` (I-e). Build com `--strict`.
- Páginas da 0-M (todas com conteúdo real, decisão I26):

| Página | Arquivo | Criada em | Ampliada em |
|---|---|---|---|
| O que é | `manual/index.md` | 0M-08 | 0M-40 |
| Instalação | `manual/inicio/instalacao.md` | 0M-07 | 0M-08 |
| Conceitos (mapa de módulos, três usos de evento) | `manual/inicio/conceitos.md` | 0M-12 | 0M-24, 0M-26, 0M-40 |
| Migração 2.x → 3.0 | `manual/regras/migracao-2-para-3.md` | 0M-12 | 0M-15, 0M-19, 0M-21, 0M-23, 0M-25 |
| GAS | `manual/modulos/gas.md` | 0M-10 | 0M-12, 0M-19, 0M-21, 0M-22, 0M-37, 0M-39 |
| Tags | `manual/modulos/tags.md` | 0M-16 | — |
| Tempo | `manual/modulos/tempo.md` | 0M-17 | 0M-18 |
| Pooling (inclui `SpawnerAsset`) | `manual/modulos/pooling.md` | 0M-20 | 0M-23 |
| Eventos (inclui reações prontas e payload próprio) | `manual/modulos/eventos.md` | 0M-26 | 0M-27 a 0M-34, 0M-36 |
| Event Debugger | `manual/ferramentas/event-debugger.md` | 0M-35 | 0M-36 |
| Validador | `manual/ferramentas/validador.md` | 0M-38 | — |

---

## 5. Testes planejados

Cada teste diz por que falha antes (red). Nomes de arquivo relativos a `Tests/`.

| Teste | Arquivo | CA | Por que falha antes |
|---|---|---|---|
| Todo asmdef do pacote, exceto Setup, tem `ODIN_INSPECTOR` em `defineConstraints`; Setup não referencia nada | `Core/EditMode/Structure/AsmdefRulesTests.cs` | CA53 | Nenhum asmdef tem a constraint e o Setup não existe |
| Infra não referencia `SpellBook.GAS*`; GAS não referencia Events/Pooling; Bridges não referencia Pooling; Events só Core/Time | idem | CA2 | Guarda no 0M-07 (um assembly só); cada ticket que cria módulo acrescenta a asserção de existência do módulo, que falha antes de o asmdef existir |
| Runtime não referencia `UnityEditor`, `Sirenix.Serialization`, `Sirenix.OdinInspector.Editor` e não tem tipo derivado de `SerializedScriptableObject`/`SerializedMonoBehaviour` | `Core/EditMode/Structure/RuntimeReferencesTests.cs` | CA3 | Guarda |
| `DocUrls.Base` igual ao literal do site e ao `documentationUrl` do `package.json`; `unity == "6000.0"` | `Core/EditMode/DocUrlsTests.cs` | CA4, CA37 | `DocUrls` não existe; `package.json` tem 2021.3 e example.com |
| `SpellBook.GAS` existe e o Core não o referencia | `AsmdefRulesTests.cs` | CA1, CA2 | Não existe asmdef do GAS |
| Tags com contagem de referência e match hierárquico no ASC | `GAS/PlayMode/TagCountingTests.cs` | CA31 | Caracterização (verde antes e depois do 0M-14) |
| `GameplayTag` está no assembly e no namespace `SpellBook.Tags` | `AsmdefRulesTests.cs` | CA31 | Tipo ainda em `SpellBook.GAS.Tags` / no assembly do GAS |
| Match hierárquico de `GameplayTag` (igual, filho casa pai, pai não casa filho, inválida não casa) | `Tags/EditMode/GameplayTagTests.cs` | CA31, CA32 | O asmdef `SpellBook.Tags.Tests.EditMode` não existe (teste novo; a lógica já existe, por isso é caracterização da API movida) |
| `ManualTimeSource.Advance` acumula; escalado e não escalado leem a Unity; menu Create tem as variantes | `Time/EditMode/TimeSourceTests.cs` | CA44 | Módulo Time não existe |
| `timeScale = 0` por N quadros: escalado parado, não escalado anda | `Time/PlayMode/TimeScaleTests.cs` | CA45 | `ScaledTimeSource`/`UnscaledTimeSource` em PlayMode não existem no ticket do asmdef PlayMode |
| Grupo pausado: `DeltaTime` 0 e `ElapsedTime` parado; retomado volta do ponto; reset ao "entrar em Play" | `Time/PlayMode/PausableTimeSourceTests.cs` | CA46 (parte), P20 | `PausableTimeSource` não existe |
| Efeito de 2 s: ativo após `Tick(1.9)`, removido após `Tick(0.2)` | `GAS/PlayMode/AscTimeTests.cs` | CA7 | O ASC não tem `Tick` nem `SetTimeSource` |
| Recarga de 3 s: bloqueada após `Tick(2.9f)` num ASC e liberada após `Tick(3.0f)` em outro (risco 8) | idem | CA8 | Idem |
| Fonte parada + `autoTick` por N quadros: nada muda | idem | CA9 | O ASC lê `Time.deltaTime` |
| Duas execuções com a mesma sequência de `Tick` dão o mesmo estado | idem | CA11 | O ASC não tem `Tick` |
| Dois ASCs no mesmo grupo pausável e um no não escalado | `GAS/PlayMode/AscTimeTests.cs` | CA46 | ASC não aceita `TimeSourceAsset` |
| `Tick` em regime sem tick periódico não aloca | idem | N2 (D3) | `Tick` não existe |
| `InstantiateSpawnerAsset` cria na posição/rotação e destrói ao devolver | `Core/EditMode/SpawnerAssetTests.cs` | CA27 | `SpawnerAsset` não existe |
| `ReleaseWhenParticlesStop` devolve ao parar a partícula | `Core/PlayMode/ParticleReleaseTests.cs` | R25 | Método não existe |
| `SpawnProjectileAction` e `PlayCueAction` sem spawner e sem `PoolManager` não lançam NRE | `GAS/PlayMode/SpawnActionsTests.cs` | CA29, CA62 | Hoje usam `PoolManager.Instance` (NRE) |
| `SpawnVFXEffectAction` devolve o VFX pelo spawner configurado no `Cleanup` | idem | R25 | Usa `Destroy` direto |
| Nenhum arquivo fora do Pooling contém `PoolManager.Instance` | `Core/EditMode/Structure/SourceRulesTests.cs` | CA30 | `SpawnProjectileAction.cs:78,83` e `PlayCueAction.cs:37` contêm |
| Nenhum `.cs` de `Runtime/GAS` usa `Time.` não qualificado, `UnityEngine.Time` ou `WaitForSeconds` | `GAS/EditMode/GasSourceScanTests.cs` | CA10 | `WaitAction.cs:13`, `FlowActions.cs:28`, `SpawnProjectileAction.cs:102`, `Projectile.cs:131,137` |
| `WaitAction` de 1 s termina pelo `Tick` do ASC, não pelo relógio | `GAS/PlayMode/AbilityWaitTests.cs` | R10 | Usa `WaitForSeconds` |
| `PooledSpawnerAsset` sem `PoolManager`: cria sem exceção, reaproveita a mesma instância; `initialSize < 0` gera erro de validação | `Pooling/EditMode/PooledSpawnerTests.cs` | CA28, CA35 | `PooledSpawnerAsset` não existe |
| Não existe `MonoBehaviour` `Math`; `MathTools` no Core; `Map`, `IsInRange`, `MidPoint`, `CenterOfMass`, `RandomPointInCircle` dentro do raio | `Core/EditMode/MathToolsTests.cs` | CA43 | `MathTools` está aninhada em `Math : MonoBehaviour` |
| Pastas e tipos antigos de eventos não existem | `SourceRulesTests.cs` | CA25 | Existem |
| `EventBus<T>`: entrega, reentrada, exceção isolada, 0 byte em 1.000 disparos, limpo após reset | `Events/EditMode/EventBusTests.cs` | CA15, CA16, CA17, CA19 | `EventBus` não existe |
| `EventChannel<T>`: idem, `LastPayload`, `logRaises` | `Events/EditMode/EventChannelTests.cs` | CA15, CA16, CA18, CA19 | Canal não existe |
| Menu `SpellBook/Events` tem exatamente os 7 canais e nenhum de `Component` | `Events/EditMode/ChannelMenuTests.cs` | CA12 | Faltam Bool, String, Vector3, GameObject |
| `EventListener`: valor exato, ordem reações → `UnityEvent`; desabilitado não recebe | `Events/PlayMode/EventListenerTests.cs` | CA13, CA14 | `EventListener` não existe |
| Reações prontas | `Events/PlayMode/ReactionTests.cs` | CA47, CA48, CA49, CA50, CA58 | Reações não existem |
| Invoke com delay: 0,9 s não roda, +0,2 s roda uma vez; desabilitar cancela; sem asset usa escalado | `Events/PlayMode/DelayedReactionTests.cs` | CA51, CA59, CA60 | `DelayedReaction` não existe |
| `EventEmission<T>` genérica sobrevive a ida e volta de serialização | `Events/EditMode/EmissionSerializationTests.cs` | (0013) | `EventEmission` não existe; se a Unity não suportar, o teste falha **depois** da implementação e aciona o plano B |
| `EventEmitter.Raise()` dispara com o payload configurado; payload próprio de ponta a ponta | `Events/PlayMode/EventEmitterTests.cs` | CA20, CA22 | `EventEmitter` não existe |
| Validação do Events (resposta sem canal, `UnityEvent` sem alvo/método, reação com campo vazio, emissor sem canal, forma "repassar" com tipo errado) | `Events/EditMode/EventsValidationTests.cs` | CA35 | Validadores não existem |
| Filtro de canal por payload | `Events/EditMode/ChannelCatalogTests.cs` | CA21, CA58 | `ChannelCatalog` não existe |
| Modelo do Event Debugger: grava disparo com quadro, horário, payload e emissor; busca; limpar; buffer de 256 | `Events/EditMode/EventDebuggerModelTests.cs` | R20 | Modelo não existe |
| Find references: prefab emissor e SO ouvinte, papéis corretos; cena fechada fora | `Events/EditMode/FindReferencesTests.cs` | CA24 | Finder não existe |
| `RaiseEventAction` com Void dispara uma vez e um ouvinte reage; com Int 7 dispara 7; sem canal gera erro | `GAS.Bridges/EditMode/RaiseEventActionTests.cs`, `GAS.Bridges/PlayMode/RaiseEventAbilityTests.cs` | CA52, CA61, CA35 | Bridges não existe |
| Validador do projeto: cada erro do R35 aparece; corrigidos, nenhum; projeto do host sem erros | `Core/EditMode/ProjectValidationTests.cs` | CA35, R34 | `ProjectValidator` não existe |
| Todo MB/SO público concreto de runtime tem `[HelpURL]` iniciado por `DocUrls.Base` | `Core/EditMode/HelpUrlTests.cs` | CA36, CA37 | 8 tipos do GAS sem `[HelpURL]` |
| Cada `[HelpURL]` tem página e âncora `{#id}` no manual; nenhuma página é só título ou "em breve" | `Core/EditMode/ManualLinksTests.cs` | CA54 | Guarda (as páginas nascem com cada ticket); red mostrado removendo uma âncora localmente |
| Lista completa de asmdefs e um `Tests.EditMode` por módulo de runtime | `AsmdefRulesTests.cs` | CA1, CA32 | Guarda final; falha se algum ticket anterior faltar |

Cenários (S), verificados pelo Examinador no host e registrados na seção 9 do PRD: CA5 e CA41 (0M-07), CA6 (0M-40, também como compilação automática no host), CA23 (0M-35), CA34 (0M-38), CA38 e CA39 (0M-09 e a cada página nova), CA57 (0M-11).

---

## 6. Riscos de repetição, tempo e dados externos

| # | Categoria | Risco | Onde | Mitigação | Verificação |
|---|---|---|---|---|---|
| 1 | Repetição | Estado estático (bus, canais, fontes de tempo, pools) vaza entre testes e entre sessões de Play sem domain reload | `EventBus<T>`, `EventChannel`, `PausableTimeSource`, `ManualTimeSource`, `PooledSpawnerAsset` | Registros limpos em `SubsystemRegistration`; todo teste chama o reset no `TearDown` e destrói os objetos que criou (`Object.DestroyImmediate`) | CA19, P20, testes independentes em qualquer ordem (o Test Runner é rodado também com `--random-order` uma vez por épico) |
| 2 | Repetição | Alocação por quadro ou por disparo | `ASC.Tick`, `ListenerList.Raise`, `EventListener.Update` | Iteração por índice, sem LINQ, sem cópia da lista; `Update` do ouvinte sai cedo sem esperas; hook do debugger só em Editor e só se assinado; `logRaises` desligado por padrão | CA17, CA18, N2 com `Is.Not.AllocatingGCMemory()` do Test Framework (sem dependência nova) |
| 3 | Repetição | Alocação do `Recalculate` em tick periódico | `AttributeState.cs:69-82` | Fora da 0-M (B-002, aceito pela I44); medido sem tick periódico | D3 |
| 4 | Repetição | Inscrição dupla do mesmo ouvinte (habilitar duas vezes) chama duas vezes | `ListenerList` | Inscrição idempotente | CA15 |
| 5 | Repetição | Find references e validador do projeto lentos em projetos grandes | `ChannelReferenceFinder`, `ProjectValidator` | Rodam só sob demanda; o finder filtra antes por `AssetDatabase.GetDependencies` | N5 no host (projeto pequeno); medido no cenário CA34 |
| 6 | Repetição | Pool cresce sem limite se nada devolve (VFX do `PlayCueAction`) | `PlayCueAction`, `PooledSpawnerAsset` | Aceito até o Cues (I37); manual avisa em "Armadilhas" | Página Pooling |
| 7 | Tempo | Teste dependendo de relógio real | Todos | `ManualTimeSource` e `Tick`; PlayMode conta quadros, nunca segundos; nada de `WaitForSeconds` em teste | Revisão do Examinador (0004) |
| 8 | Tempo | Soma de floats na fronteira: `3f - 2.9f - 0.1f` dá cerca de `2,4e-8`, positivo, e a recarga **não** expiraria após "2,9 + 0,1" | CA8 | O CA8 usa dois arranjos independentes: um ASC com `Tick(2.9f)` (ainda em recarga) e outro com `Tick(3.0f)` (liberado); nunca a soma 2,9 + 0,1. Expiração continua "restante ≤ 0", como no código atual. A armadilha vai para a página GAS | CA8 |
| 9 | Tempo | `Time.timeScale` alterado por um teste afeta os seguintes | CA45, CA46, CA60 | `SetUp` guarda e `TearDown` restaura `UnityEngine.Time.timeScale` | Testes em ordem aleatória |
| 10 | Tempo | Salto de delta no primeiro quadro ou ao trocar de fonte | ASC, `Projectile`, `EventListener` | Cursor recebe o `ElapsedTime` atual no `OnEnable` e no `SetTimeSource` | CA9, CA46 |
| 11 | Tempo | Precisão do tempo acumulado em sessões longas | `ITimeSource.ElapsedTime` | `double` (`Time.timeAsDouble`) | — |
| 12 | Tempo | Conflito do namespace `SpellBook.Time` com `UnityEngine.Time` | Todo código em `namespace SpellBook.*` que referencia o asmdef Time | Ordem dos tickets (0M-19 troca todos os usos do GAS); testes escrevem `UnityEngine.Time` qualificado; manual avisa | Compilação; CA10 |
| 13 | Tempo | Ordem de reset em `SubsystemRegistration` (um registro limpo depois de alguém já ter se inscrito no Play) | Registros | Só limpar estado; nenhum código do pacote se inscreve antes de `AfterAssembliesLoaded` | CA19 |
| 14 | Dados externos | Odin ausente, versão diferente ou símbolo velho nos Player Settings | Todos os asmdefs | `defineConstraints`; Setup sem Odin; versão testada no README; instrução para limpar o símbolo | CA41, CA53 |
| 15 | Dados externos | API de `SelfValidationResult` não documentada | Validadores | Verificação no red do 0M-23; plano B (4.9) | 0M-23 |
| 16 | Dados externos | Suporte da 6000.0 a genérico fechado em `[SerializeReference]` | `EventEmission<T>` | Teste de ida e volta no 0M-32; plano B (0013) | 0M-32 |
| 17 | Dados externos | Licença do Odin no CI (P21) e disponibilidade das imagens GameCI | 0M-11 | Ticket bloqueado até a resposta; versões fixadas no workflow | CA56, CA57 |
| 18 | Dados externos | Primeira compilação no CI sem `ODIN_INSPECTOR` (o Odin grava o símbolo e força recompilação) | 0M-11 | Passo de importação antes dos testes (uma execução da Unity só para importar) e verificação de que a contagem de testes executados é > 0 | 0M-11 |
| 19 | Dados externos | Secrets da Unity (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`) e do Odin | 0M-11 | Só via `secrets.*`; nada escrito no workflow | CA56 |
| 20 | Dados externos | Pages precisa ser ligado nas configurações do repositório ("Source: GitHub Actions"), passo manual do dono | 0M-09 | Passo listado no ticket para o usuário | CA39 |
| 21 | Dados externos | Versões Python da ferramenta de doc mudam | 0M-08 | `requirements-docs.txt` com `==` | Build `--strict` |
| 22 | Dados externos | Testes que leem arquivos do pacote (asmdefs, `.cs`, manual) dependem do caminho real | CA2, CA10, CA25, CA30, CA53, CA54 | Caminho obtido por `PackageInfo.FindForAssembly(...).resolvedPath`; os testes rodam só no host com `file:` | 0M-07 |
| 23 | Dados externos | Testes que criam assets e cenas em disco (Find references) deixam lixo ou mudam as cenas abertas | 0M-36 | Pasta temporária `Assets/__SpellBookTestTemp` criada no `SetUp` e apagada no `TearDown`; cenas abertas salvas e restauradas com `EditorSceneManager` | CA24 |
| 24 | Dados externos | Setup lê `documentationUrl` do `package.json` instalado | 0M-07 | Se não achar o pacote, usa a URL da página de instalação escrita no próprio diálogo como texto, sem link | CA41 |

---

## 7. Quebras de compatibilidade (para o CHANGELOG da `3.0.0-pre.1`)

O DevOps leva esta lista ao CHANGELOG no release, com a tabela da página "Migração 2.x → 3.0" (CA26):

| Antes (2.x) | Depois (3.0) | Ticket |
|---|---|---|
| Assembly único `SpellBook.Core` | `SpellBook.Core`, `.Time`, `.Events`, `.Pooling`, `.Tags`, `.GAS`, `.GAS.Bridges` (asmdefs do usuário precisam referenciar os novos) | 0M-12 em diante |
| Sem Odin o pacote dava erros de compilação | Sem Odin o pacote fica desligado e mostra um diálogo | 0M-07 |
| `unity: 2021.3` | `unity: 6000.0` | 0M-08 |
| `SpellBook.GAS.Tags.*` | `SpellBook.Tags.*` (assembly `SpellBook.Tags`) | 0M-14, 0M-15 |
| `[FormerlySerializedAs("_cooldownReductionAttribute")]` | removido; assets da 1.x precisam ser reabertos na 2.x antes | 0M-15 |
| ASC avançava sozinho por `Time.deltaTime` | `autoTick` (padrão ligado) + `Tick(dt)` + fonte de tempo | 0M-19 |
| `ActiveEffectInstance.StartTime` | removido | 0M-19 |
| `Projectile.Setup(ComponentPooler, ...)`, `Projectile : IPoolable` | `Projectile.Setup(SpawnerAsset, ...)`, sem `IPoolable` | 0M-21 |
| Ações de spawn usavam `PoolManager.Instance` | campo `Spawner` (vazio → Instantiate) | 0M-21 |
| `SpellBook.Systems.Pooling.*`, menu `PoolingSystem/...` | `SpellBook.Pooling.*`, menu `SpellBook/Pooling/...` | 0M-23 |
| `SpellBook.MathTools.Math.MathTools` | `SpellBook.Core.MathTools` | 0M-24 |
| `SpellBook.Core.NewEventSystem.Void`, `SpellBook.Core.RuntimeSets.RuntimeSet<T>` | `SpellBook.Core.Void`, `SpellBook.Core.RuntimeSet<T>` | 0M-25 |
| `GameEvent`/`GameEventListner` (Core/Events), `VoidEvent`, `IntEvent`, `FloatEvent`, `StringEvent`, `ComponentEvent` e listeners, `EventManager`, `GameEvent`/`GameEventListener` por string | `VoidEventChannel` etc., `EventListener`, `EventEmitter`, `EventBus<T>`; não há canal de `Component`; assets de evento precisam ser recriados | 0M-25 a 0M-32 |
| `AbilityActivatedEvent`, `AbilitySlotUpdateEvent` globais | removidos; usar `ASC.OnAbilityActivated`; relay no B-004 (R22a) | 0M-12 |
| `Runtime/GAS/GAS_Documentation.md` | página GAS do manual | 0M-10 |

---

## 8. Tickets

Ordem por dependência. **WIP 1**: um ticket em implementação por vez; um ticket bloqueado (0M-11) não ocupa o WIP, e a fila segue para o próximo cujas dependências estejam prontas. "Linhas" é a estimativa de código (sem páginas e fixtures, ADR 0003). Todos os tickets de código usam a trava (0005) e terminam com a suíte inteira verde no host.

### Resumo

| Id | Título | Depende de | CAs do PRD |
|---|---|---|---|
| 0M-01 | `chore(process): apontar a skill /issue para gabrielctorres/SpellBook` | — | (B-020) |
| 0M-02 | `docs(process): alinhar TIME.md, CLAUDE.md, agentes e skills ao fluxo novo` | 0M-01 | (B-022) |
| 0M-03 | `chore(process): adaptar agentes de programação e QA ao TIME.md` | 0M-02 | (B-021) |
| 0M-04 | `chore(process): adaptar agentes de design, writer e Unity ao TIME.md` | 0M-02 | (B-021) |
| 0M-05 | `chore(host): ignorar artefatos do host e o Odin no git` | 0M-02 | CA55, CA56 (parte) |
| 0M-06 | `chore(host): criar projeto host de testes com o pacote em testables` | 0M-05 | CA55, CA5 (parte) |
| 0M-07 | `build(setup)!: desligar o pacote sem Odin e avisar com diálogo` | 0M-06 | CA53, CA3, CA2 (guarda), CA41, CA5 |
| 0M-08 | `docs(manual): criar o manual, DocUrls e declarar Unity 6000.0` | 0M-07 | CA4 (parte), CA37 (parte), CA42 |
| 0M-09 | `ci(manual): publicar o manual no GitHub Pages` | 0M-08 | CA39 |
| 0M-10 | `docs(gas): mover GAS_Documentation.md para o manual` | 0M-08 | CA40 |
| 0M-11 | `ci(test): rodar as suítes na 6000.0 e na última 6000.x com GameCI` | 0M-07 · **bloqueado pela P21** | CA56, CA57, N1 |
| 0M-12 | `refactor(gas)!: separar o GAS no assembly SpellBook.GAS` | 0M-10 | CA1 (parte), CA2 (parte), R22 (parte) |
| 0M-13 | `test(gas): caracterizar tags do ASC e criar as factories` | 0M-12 | CA31 (parte), CA33 (parte) |
| 0M-14 | `refactor(tags)!: mover as tags para o assembly SpellBook.Tags` | 0M-13 | CA2 (parte), CA31 (parte) |
| 0M-15 | `refactor(tags)!: renomear o namespace das tags para SpellBook.Tags` | 0M-14 | CA31 (parte) |
| 0M-16 | `test(tags): testes do módulo Tags, HelpURL e página` | 0M-15 | CA31, CA32 (parte) |
| 0M-17 | `feat(time): ITimeSource, fontes escalada, não escalada e manual` | 0M-12 | CA44 (parte), CA33 (parte) |
| 0M-18 | `feat(time): grupo pausável e reset sem domain reload` | 0M-17 | CA44, CA45, CA46 (parte) |
| 0M-19 | `feat(gas)!: Tick, autoTick e fonte de tempo no ASC` | 0M-18, 0M-15 | CA7, CA8, CA9, CA11, CA46, N2, N3 |
| 0M-20 | `feat(core): SpawnerAsset com implementação por Instantiate` | 0M-12 | CA27, CA33 (parte) |
| 0M-21 | `refactor(gas)!: ações de spawn e projétil usam SpawnerAsset` | 0M-20, 0M-19 | CA29, CA62, CA30 |
| 0M-22 | `refactor(gas): esperas do GAS pelo relógio do ASC` | 0M-21 | CA10, CA32 (parte) |
| 0M-23 | `refactor(pooling)!: módulo SpellBook.Pooling e PooledSpawnerAsset` | 0M-21 | CA28, CA35 (parte), CA6 (parte) |
| 0M-24 | `refactor(core)!: MathTools como classe estática no Core` | 0M-12 | CA43 |
| 0M-25 | `refactor(events)!: remover os três sistemas de eventos antigos` | 0M-12, 0M-24 | CA25, R22 |
| 0M-26 | `feat(events): EventBus<T> e registro limpo por sessão` | 0M-25, 0M-17 | CA15, CA16, CA17, CA19 (bus), CA2 (parte) |
| 0M-27 | `feat(events): EventChannel e canais Void, Int e Float` | 0M-26 | CA15, CA16, CA18, CA19, CA33 (parte) |
| 0M-28 | `feat(events): canais Bool, String, Vector3 e GameObject` | 0M-27 | CA12 |
| 0M-29 | `feat(events): EventListener com respostas e reações` | 0M-28 | CA13, CA14 |
| 0M-30 | `feat(events): reações SetActive, Log, Toggle e RaiseChannel` | 0M-29 | CA47, CA48, CA49, CA50, CA58 (runtime) |
| 0M-31 | `feat(events): reação Invoke com delay` | 0M-30, 0M-18 | CA51, CA59, CA60 |
| 0M-32 | `feat(events): EventEmitter e payload próprio de ponta a ponta` | 0M-31 | CA20, CA22 |
| 0M-33 | `feat(events): validação de ouvintes e respostas` | 0M-32, 0M-23 | CA35 (parte) |
| 0M-34 | `feat(events): seletor de canal filtrado por payload` | 0M-33 | CA21, CA58 (seletor) |
| 0M-35 | `feat(events): Event Debugger` | 0M-34 | CA23 |
| 0M-36 | `feat(events): Find references de canal` | 0M-35 | CA24 |
| 0M-37 | `feat(gas-bridges): RaiseEventAction` | 0M-32, 0M-22 | CA52, CA61, CA35 (parte), CA2 |
| 0M-38 | `feat(core): validar projeto por menu e por teste` | 0M-37, 0M-33 | CA34, CA35, R34 |
| 0M-39 | `docs(gas): HelpURL nos tipos restantes e teste de HelpURL` | 0M-38 | CA36, CA37 |
| 0M-40 | `refactor(core)!: Core em Runtime/Core e verificação final da estrutura` | 0M-39 | CA1, CA32, CA54, CA6, N4 |

Ordem de execução linear sugerida: 01 → 02 → 03 → 04 → 05 → 06 → 07 → 08 → 09 → 10 → (11 quando a P21 for respondida) → 12 → 13 → 14 → 15 → 16 → 17 → 18 → 19 → 20 → 21 → 22 → 23 → 24 → 25 → 26 → … → 40.

CAs que ficam com o DevOps no release (não são de ticket): CA4 (`version` = `3.0.0-pre.1`) e CA26 (CHANGELOG com a seção 7 e a nota do R22a).

---

### 0M-01 — `chore(process): apontar a skill /issue para gabrielctorres/SpellBook`
- Via: rápida (B-020). Executor: Fábrica (sem Unity).
- Objetivo: issues criadas no repositório do SpellBook, com as labels da 0002.
- Arquivos: `.claude/skills/issue/SKILL.md`.
- Depende de: —. Observação: a issue deste ticket é criada à mão pelo usuário no repositório do SpellBook, porque a skill ainda aponta para o lugar antigo.
- CAs: nenhum do PRD (processo).
- Testes: não há código. Critério verificável: a skill usa `--repo gabrielctorres/SpellBook`, aplica `epic:<slug>`, tipo e `via:rapida`, e não cita outro repositório nem o prefixo `[SpellBook]`.
- Manual: nenhuma página.
- Tamanho: ~40 linhas de texto, 1 arquivo.

### 0M-02 — `docs(process): alinhar TIME.md, CLAUDE.md, agentes e skills ao fluxo novo`
- Via: rápida (B-022). Executor: Fábrica (sem Unity).
- Objetivo: o time segue backlog → PRD → Spec (`spec/`) → gate único (parcial permitido) → issues no repo do SpellBook; o Examinador cria e remove a trava; uma branch por ticket (0006 Adiada); regra de contagem da 0003 (incluindo a de remoção pura, I42); host de testes em `HostProject~/` (I41); 2.x congelada (I43).
- Arquivos: `Documentation~/TIME.md`, `CLAUDE.md` (só a seção "Time de desenvolvimento"), `.claude/agents/arquiteto.md`, `.claude/agents/examinador.md`, `.claude/agents/fabrica.md`, `.claude/skills/implementar/SKILL.md`, `.claude/skills/revisao/SKILL.md`.
- Depende de: 0M-01.
- CAs: nenhum do PRD.
- Testes: critério verificável: nenhum dos 7 arquivos cita `specs/`, `PlaygroundRumble` ou `docs/`; `TIME.md` lista os gates, o gate parcial, a trava pelo Examinador e a seção "Issues" com `gabrielctorres/SpellBook`.
- Manual: nenhuma página.
- Tamanho: ~150 linhas de texto, 7 arquivos.

### 0M-03 — `chore(process): adaptar agentes de programação e QA ao TIME.md`
- Via: rápida (B-021, parte 1). Executor: Fábrica.
- Objetivo: tirar Write/Edit de quem só propõe e remover referências a `docs/engine-reference`, `docs/architecture` e `lead-programmer`.
- Arquivos: `.claude/agents/gameplay-programmer.md`, `engine-programmer.md`, `ai-programmer.md`, `ui-programmer.md`, `qa-tester.md`.
- Depende de: 0M-02. CAs: nenhum.
- Testes: critério verificável por busca textual nos 5 arquivos. **Inferido:** o escopo exato de ferramentas de cada agente é confirmado pelo Controlador na leitura dos arquivos (o Arquiteto não os leu).
- Manual: nenhuma. Tamanho: ~60 linhas de texto, 5 arquivos.

### 0M-04 — `chore(process): adaptar agentes de design, writer e Unity ao TIME.md`
- Via: rápida (B-021, parte 2). Executor: Fábrica.
- Arquivos: `.claude/agents/systems-designer.md`, `ux-designer.md`, `game-designer.md`, `writer.md`, `unity-asset-inspector.md`, `unity-console-reader.md`, `unity-test-reader.md`, `unity-explorer.md`.
- Depende de: 0M-02. CAs: nenhum. Testes: como o 0M-03. Manual: nenhuma. Tamanho: ~80 linhas de texto, 8 arquivos.

### 0M-05 — `chore(host): ignorar artefatos do host e o Odin no git`
- Objetivo: nenhum arquivo do Odin nem gerado pelo host entra no git, antes de o Odin ser importado.
- Arquivos: `.gitignore` (acrescenta `HostProject~/Library/`, `Logs/`, `Temp/`, `UserSettings/`, `obj/`, `HostProject~/ProjectSettings/*` com exceção `!HostProject~/ProjectSettings/ProjectVersion.txt`, `HostProject~/Assets/Plugins/Sirenix/` e `HostProject~/Assets/Plugins/Sirenix.meta`).
- Depende de: 0M-02. Liberado pela decisão I41 (host em `HostProject~/`).
- Host antigo na raiz: `/Assets/`, `/Packages/` e `/ProjectSettings/` já estão no `.gitignore` (commit c19b627), então não há arquivo versionado para mover ou apagar. As entradas da raiz continuam no `.gitignore`, e a cópia local do usuário não é tocada (`CLAUDE.md`: preservar o working tree). Critério extra: `git ls-files Assets Packages ProjectSettings` não lista nada.
- CAs: CA55 (`.gitignore` com o Odin), CA56 (parte: Odin nunca versionado).
- Testes: critério verificável: `git check-ignore` devolve ignorado para `HostProject~/Assets/Plugins/Sirenix/x.dll` e não ignorado para `HostProject~/Packages/manifest.json`.
- Manual: nenhuma. Tamanho: ~15 linhas, 1 arquivo.

### 0M-06 — `chore(host): criar projeto host de testes com o pacote em testables`
- Objetivo: um projeto Unity 6000.0 que compila o pacote por `file:` e lista os testes dele.
- Arquivos: `HostProject~/Packages/manifest.json` (`com.spellbook` por `file:` apontando para a raiz do repositório, `com.unity.test-framework` fixado, `testables: ["com.spellbook"]`, módulos mínimos da Unity), `HostProject~/ProjectSettings/ProjectVersion.txt` (6000.0, I-j), `Documentation~/README.md` (seção "Projeto host": abrir, importar o Odin em `HostProject~/Assets/Plugins/Sirenix`, criar a trava).
- Caminho do `file:` (decisão I41 escreve `file:../`): a Unity resolve o relativo a partir de `HostProject~/Packages/`, então o valor esperado é `file:../..`. O red do ticket é o Package Manager não mostrar `com.spellbook` com o caminho errado; fica o valor que resolver para a raiz (ver D1).
- Depende de: 0M-05. Liberado pela decisão I41.
- CAs: CA55; CA5 (parte: o pacote atual importa sem erro com Odin).
- Testes: sem teste de código (ainda não há asmdef de teste). Critério: com a trava, a Unity abre o host, o Package Manager mostra `com.spellbook` como local e o console não tem erro do SpellBook. **Inferido:** I-g, I-j.
- Manual: nenhuma (documento interno).
- Tamanho: ~40 linhas, 3 arquivos.

### 0M-07 — `build(setup)!: desligar o pacote sem Odin e avisar com diálogo`
- Objetivo: R40; regras de asmdef testadas; página de instalação.
- Arquivos: `Runtime/SpellBook.Core.asmdef` (constraint, `overrideReferences`, DLL de atributos), `Editor/SpellBook.Editor.asmdef` (constraint), `Editor/Setup/SpellBook.Setup.Editor.asmdef`, `Editor/Setup/OdinMissingNotice.cs`, `Tests/Core/EditMode/SpellBook.Core.Tests.EditMode.asmdef`, `Tests/Core/EditMode/Structure/PackageFiles.cs` (lê asmdefs e arquivos do pacote pelo `resolvedPath`), `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs`, `Tests/Core/EditMode/Structure/RuntimeReferencesTests.cs`, `Documentation~/manual/inicio/instalacao.md`.
- Depende de: 0M-06.
- CAs: CA53 (T), CA3 (T, guarda), CA2 (T, guarda), CA41 (S), CA5 (S).
- Testes red: `AsmdefRules_AllButSetupHaveOdinConstraint` falha (nenhum asmdef tem `ODIN_INSPECTOR`); `AsmdefRules_SetupHasNoReferences` falha (Setup não existe). Guardas: CA2 e CA3. Cenário CA41: abrir uma cópia do host sem o Odin e conferir console sem erro do SpellBook, diálogo uma vez e a mensagem no console.
- Manual: **Instalação** (conteúdo real: Unity 6000.0 ou mais nova; Odin Inspector obrigatório, da Asset Store, testado com 3.3.x; instalação por URL git; o que acontece sem Odin; como limpar o símbolo `ODIN_INSPECTOR` velho).
- Tamanho: ~260 linhas, 9 arquivos.

### 0M-08 — `docs(manual): criar o manual, DocUrls e declarar Unity 6000.0`
- Objetivo: esqueleto do site, constante única de URL, `package.json` e README.
- Arquivos: `Documentation~/mkdocs.yml`, `Documentation~/requirements-docs.txt`, `Documentation~/manual/index.md`, `Documentation~/manual/inicio/instalacao.md` (âncoras), `Runtime/Core/DocUrls.cs`, `package.json` (`unity`, `documentationUrl`; o `version` não muda), `README.md` (Odin obrigatório, versão testada, link do site), `Tests/Core/EditMode/DocUrlsTests.cs`.
- Depende de: 0M-07.
- CAs: CA4 (parte: `unity` e `documentationUrl`), CA37 (parte: constante única com o valor certo), CA42 (E).
- Testes red: `DocUrls_BaseMatchesSite` e `DocUrls_BaseMatchesPackageDocumentationUrl` falham (tipo não existe; `package.json` tem example.com); `Package_DeclaresUnity6000_0` falha (2021.3).
- Decisão registrada no ticket: Zensical ou MkDocs Material (0025), com a versão travada.
- Manual: **O que é** (o pacote, os módulos, para quem é); âncoras `{#...}` na Instalação. Verificação: `mkdocs build --strict` local passa.
- Tamanho: ~70 linhas de código, 8 arquivos.

### 0M-09 — `ci(manual): publicar o manual no GitHub Pages`
- Arquivos: `.github/workflows/docs.yml`.
- Depende de: 0M-08. **Passo do usuário:** ligar Pages com "Source: GitHub Actions" (risco 20).
- CAs: CA39 (S), CA38 (S, só depois do 0M-29 para o `EventListener`).
- Testes: critério: após o merge, a Action roda, `https://gabrielctorres.github.io/SpellBook/` abre sem login e não há nenhuma URL de `prd/`, `spec/`, `adr/`, `planos/`, `backlog`, `TIME`.
- Manual: nenhuma página nova. Tamanho: ~50 linhas, 1 arquivo.

### 0M-10 — `docs(gas): mover GAS_Documentation.md para o manual`
- Arquivos: `Runtime/GAS/GAS_Documentation.md` (apagado, com `.meta`), `Documentation~/manual/modulos/gas.md`.
- Depende de: 0M-08.
- CAs: CA40 (E).
- Testes: critério: o arquivo antigo e o `.meta` não existem; `mkdocs build --strict` passa.
- Manual: **GAS** (conteúdo atual revisado: visão geral, ASC, atributos, efeitos, habilidades, targeting, projéteis, descrições; com âncoras `{#abilitysystemcomponent}`, `{#gameplayability}`, `{#gameplayeffect}`, `{#attributeset}`, `{#attributedefinition}`, `{#formulas}`, `{#targeting}`, `{#projectile}`).
- Tamanho: 0 linhas de código, 2 arquivos.

### 0M-11 — `ci(test): rodar as suítes na 6000.0 e na última 6000.x com GameCI`
- **Bloqueado pela P21** (licença do Odin no CI).
- Arquivos: `.github/workflows/tests.yml`, `.github/scripts/fetch-odin.sh`, `Documentation~/manual/inicio/instalacao.md`.
- Depende de: 0M-07 (existe ao menos um asmdef de teste).
- Desenho: matriz `unityVersion: [6000.0.x, 6000.y.z]` (I-j); passo que baixa o Odin para `HostProject~/Assets/Plugins/Sirenix` com `secrets.ODIN_URL`/`secrets.ODIN_TOKEN`; passo de importação antes dos testes (risco 18); `game-ci/unity-test-runner` com `projectPath: HostProject~`, `testMode: all`; falha se o número de testes executados for 0; artefatos por versão.
- CAs: CA56 (E), CA57 (S), N1.
- Testes: critério: um PR dispara as duas versões, cada uma com resultado separado; `grep` no workflow não encontra URL nem credencial do Odin.
- Manual: seção "Compatibilidade" na Instalação (versões testadas no CI).
- Tamanho: ~130 linhas, 3 arquivos.

### 0M-12 — `refactor(gas)!: separar o GAS no assembly SpellBook.GAS`
- Objetivo: GAS fora do Core; o Core deixa de depender do GAS; fim do evento global do ASC (R22, R22a).
- Arquivos: `Runtime/GAS/SpellBook.GAS.asmdef` (refs Core; constraint; DLL de atributos), `Runtime/Systems/Events/GAS_Events.cs` (apagado), `Runtime/GAS/Core/AbilitySystemComponent.cs` (remove `:217`), `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs`, `Documentation~/manual/inicio/conceitos.md`, `Documentation~/manual/regras/migracao-2-para-3.md`, `Documentation~/manual/modulos/gas.md` (tira a frase do evento global).
- Depende de: 0M-10 (a página GAS precisa existir).
- CAs: CA1 (parte), CA2 (parte), R22 (parte), R22a.
- Testes red: `AsmdefRules_GasAssemblyExists` e `AsmdefRules_CoreDoesNotReferenceGas` falham (não existe asmdef do GAS).
- Manual: **Conceitos** (mapa de módulos atual e alvo da 0-M; regra de dependência); **Migração 2.x → 3.0** (primeiras linhas: assembly do GAS, fim do `AbilityActivatedEvent` e do `AbilitySlotUpdateEvent`, usar `OnAbilityActivated`).
- Tamanho: ~60 linhas, 7 arquivos.

### 0M-13 — `test(gas): caracterizar tags do ASC e criar as factories`
- Objetivo: proteger o comportamento de tags antes de mexer no ASC; criar o assembly de factories.
- Arquivos: `Tests/Testing/SpellBook.Testing.asmdef` (refs Core, GAS), `Tests/Testing/GasFactory.cs` (`CreateAsc(attributes...)`, `CreateTag(name, parent)`, `CreateEffect(...)`, `CreateAbility(...)`, com `SerializedObject` para campos privados serializados), `Tests/GAS/PlayMode/SpellBook.GAS.Tests.PlayMode.asmdef`, `Tests/GAS/PlayMode/TagCountingTests.cs`.
- Depende de: 0M-12.
- CAs: CA31 (parte), CA33 (parte).
- Testes: **caracterização** (verdes antes do 0M-14): `AddTag` duas vezes e `RemoveTag` uma mantém a tag; filho concede `HasTag(pai)`; pai não concede `HasTag(filho)`; `OnTagChanged` só na primeira adição e na última remoção.
- Manual: nenhuma página (só testes).
- Tamanho: ~220 linhas, 4 arquivos.

### 0M-14 — `refactor(tags)!: mover as tags para o assembly SpellBook.Tags`
- Objetivo: assembly próprio; o ASC deixa de usar o `internal GetDefinition`. O namespace ainda é o antigo (muda no 0M-15).
- Arquivos: `Runtime/Tags/SpellBook.Tags.asmdef`, `Runtime/Tags/GameplayTag.cs` (movido), `Runtime/Tags/GameplayTagDefinition.cs` (movido), `Runtime/GAS/SpellBook.GAS.asmdef` (+Tags), `Runtime/GAS/Core/AbilitySystemComponent.cs` (`Dictionary<GameplayTag,int>`, `HasTag` por `Matches`), `Tests/Testing/SpellBook.Testing.asmdef` (+Tags), `Tests/GAS/PlayMode/SpellBook.GAS.Tests.PlayMode.asmdef` (+Tags), `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs`.
- Depende de: 0M-13.
- CAs: CA2 (parte), CA31 (parte: assembly).
- Testes red: `AsmdefRules_TagsAssemblyExists` e `GameplayTag_LivesInTagsAssembly` falham. `TagCountingTests` continuam verdes. **Inferido:** I-h (menu das tags inalterado).
- Manual: nenhuma página nova (Tags no 0M-16).
- Tamanho: ~90 linhas, 8 arquivos.

### 0M-15 — `refactor(tags)!: renomear o namespace das tags para SpellBook.Tags`
- Arquivos: `Runtime/Tags/GameplayTag.cs`, `Runtime/Tags/GameplayTagDefinition.cs`, `Runtime/GAS/Core/AbilitySystemComponent.cs`, `Runtime/GAS/Abilities/GameplayAbility.cs` (também remove o `[FormerlySerializedAs]`, 0009), `Runtime/GAS/Effects/GameplayEffect.cs`, `Runtime/GAS/Abilities/Actions/FlowActions.cs`, `Runtime/GAS/Targeting/TargetingStrategy.cs`, `Tests/Testing/GasFactory.cs`, `Tests/GAS/PlayMode/TagCountingTests.cs`, `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs`.
- Depende de: 0M-14.
- CAs: CA31 (parte: namespace).
- Testes red: `GameplayTag_NamespaceIsSpellBookTags` falha (namespace `SpellBook.GAS.Tags`).
- Manual: nenhuma página neste ticket, porque ele já tem 10 arquivos. As linhas da Migração (namespace e `[FormerlySerializedAs]`) entram no 0M-16, que é o próximo ticket e edita a página. O gate de doc (0007) aceita, porque nenhum componente muda de comportamento aqui.
- Tamanho: ~40 linhas, 10 arquivos.

### 0M-16 — `test(tags): testes do módulo Tags, HelpURL e página`
- Arquivos: `Tests/Tags/EditMode/SpellBook.Tags.Tests.EditMode.asmdef`, `Tests/Tags/EditMode/GameplayTagTests.cs`, `Runtime/Tags/GameplayTagDefinition.cs` (`[HelpURL(DocUrls.Tags + "#gameplaytagdefinition")]`), `Documentation~/manual/modulos/tags.md`, `Documentation~/manual/regras/migracao-2-para-3.md`.
- Depende de: 0M-15.
- CAs: CA31 (T, E), CA32 (parte).
- Testes: `GameplayTagTests` (igual, filho casa pai, pai não casa filho, tag inválida nunca casa, `==` por definição). Red: o asmdef de teste do módulo não existe (o comportamento já existe; os testes são caracterização da API movida).
- Manual: **Tags** (o que é, criar uma tag e a hierarquia, `GameplayTag` no Inspector, `Matches`, armadilhas: tags por asset, comparação por referência).
- Tamanho: ~120 linhas, 5 arquivos.

### 0M-17 — `feat(time): ITimeSource, fontes escalada, não escalada e manual`
- Arquivos: `Runtime/Time/SpellBook.Time.asmdef`, `Runtime/Time/ITimeSource.cs`, `Runtime/Time/TimeSourceAsset.cs` (base, padrão escalado, registro e reset), `Runtime/Time/ScaledTimeSource.cs`, `Runtime/Time/UnscaledTimeSource.cs`, `Runtime/Time/ManualTimeSource.cs`, `Tests/Time/EditMode/SpellBook.Time.Tests.EditMode.asmdef`, `Tests/Time/EditMode/TimeSourceTests.cs`, `Documentation~/manual/modulos/tempo.md`.
- Depende de: 0M-12.
- CAs: CA44 (parte: duas variantes), CA33 (parte, a factory vem no 0M-19).
- Testes red: `Manual_AdvanceAccumulates`, `Manual_NegativeAdvanceThrows`, `Manual_ResetOnSessionStart`, `Menu_HasScaledAndUnscaled`, `Default_IsScaled` falham (módulo não existe).
- Manual: **Tempo** (o que é, `ITimeSource`, criar fontes pelo menu, `ManualTimeSource` em testes, consumo por diferença, armadilha do nome `Time` em `namespace SpellBook.*`). `[HelpURL]` nas três fontes.
- Tamanho: ~260 linhas, 9 arquivos.

### 0M-18 — `feat(time): grupo pausável e reset sem domain reload`
- Arquivos: `Runtime/Time/PausableTimeSource.cs`, `Tests/Time/PlayMode/SpellBook.Time.Tests.PlayMode.asmdef`, `Tests/Time/PlayMode/TimeScaleTests.cs`, `Tests/Time/PlayMode/PausableTimeSourceTests.cs`, `Tests/Time/EditMode/TimeSourceTests.cs` (menu com 3 variantes), `Documentation~/manual/modulos/tempo.md`.
- Depende de: 0M-17.
- CAs: CA44, CA45, CA46 (parte: o grupo em si).
- Testes red: `Pausable_PausedHasZeroDeltaAndFrozenElapsed`, `Pausable_ResumeContinuesFromSameValue`, `Pausable_ResetOnSessionStart`, `Scaled_DoesNotAccumulateWithTimeScaleZero`, `Unscaled_AccumulatesWithTimeScaleZero`, `Menu_HasThreeVariants` falham (tipo e asmdef não existem).
- Manual: seção **Grupo pausável** `{#pausabletimesource}` (pausar a gameplay e manter a UI, chamar `Pause` por `UnityEvent`, P19 e P20 explicadas). `[HelpURL]` no `PausableTimeSource`. **Inferido:** I-b.
- Tamanho: ~220 linhas, 6 arquivos.

### 0M-19 — `feat(gas)!: Tick, autoTick e fonte de tempo no ASC`
- Objetivo: R8 a R10 para efeitos e recargas; elimina todo `Time.` do GAS (risco 12).
- Arquivos: `Runtime/GAS/SpellBook.GAS.asmdef` (+Time), `Runtime/GAS/Core/AbilitySystemComponent.cs` (+`[HelpURL]`), `Runtime/GAS/Effects/ActiveEffectInstance.cs` (sem `StartTime`), `Runtime/GAS/Abilities/Projectiles/Projectile.cs` (delta pela fonte, +`[HelpURL]`), `Tests/Testing/SpellBook.Testing.asmdef` (+Time), `Tests/Testing/TimeFactory.cs`, `Tests/Testing/GasFactory.cs` (efeito com duração, habilidade com recarga), `Tests/GAS/PlayMode/AscTimeTests.cs`, `Documentation~/manual/modulos/gas.md`, `Documentation~/manual/regras/migracao-2-para-3.md`.
- Depende de: 0M-18, 0M-15.
- CAs: CA7, CA8, CA9, CA11, CA46, N2 (D3), N3.
- Testes red: `Effect2s_ActiveAfter1_9_RemovedAfter0_2`, `Cooldown3s_BlockedBefore_AllowedAt3`, `StoppedSource_AutoTick_NothingChangesOverNFrames`, `SameTicks_SameState`, `PausableGroup_TwoAscsFrozen_UnscaledAscRuns`, `Tick_InSteadyState_DoesNotAllocate` falham (`Tick`, `SetTimeSource` e o campo de fonte não existem).
- Manual: GAS, seção **Tempo do ASC** (`timeSource`, `autoTick`, `Tick`, `TickedTime`, receita do C6); Migração (`autoTick`, `StartTime`).
- Tamanho: ~300 linhas, 10 arquivos.

### 0M-20 — `feat(core): SpawnerAsset com implementação por Instantiate`
- Arquivos: `Runtime/Core/Spawning/SpawnerAsset.cs`, `Runtime/Core/Spawning/InstantiateSpawnerAsset.cs`, `Runtime/Core/Spawning/ParticleStopReleaser.cs`, `Tests/Core/EditMode/SpawnerAssetTests.cs`, `Tests/Core/PlayMode/SpellBook.Core.Tests.PlayMode.asmdef`, `Tests/Core/PlayMode/ParticleReleaseTests.cs`, `Tests/Testing/SpawnFactory.cs`, `Documentation~/manual/modulos/pooling.md`.
- Depende de: 0M-12.
- CAs: CA27, CA33 (parte).
- Testes red: `Instantiate_SpawnsAtPositionAndRotation`, `Instantiate_ReleaseDestroys`, `Default_IsInstantiateSpawner`, `ReleaseWhenParticlesStop_ReleasesOnStop` falham (tipos não existem).
- Manual: **Pooling**, seção **SpawnerAsset** (por que existe, padrão por Instantiate, criar o próprio spawner, quem devolve, `ReleaseWhenParticlesStop`). `[HelpURL]` em `InstantiateSpawnerAsset`.
- Tamanho: ~230 linhas, 8 arquivos.

### 0M-21 — `refactor(gas)!: ações de spawn e projétil usam SpawnerAsset`
- Arquivos: `Runtime/GAS/Abilities/Actions/SpawnProjectileAction.cs`, `Runtime/GAS/Abilities/Actions/PlayCueAction.cs`, `Runtime/GAS/Effects/Actions/CommonEffectActions.cs`, `Runtime/GAS/Abilities/Projectiles/Projectile.cs`, `Tests/GAS/PlayMode/SpawnActionsTests.cs`, `Tests/Testing/GasFactory.cs` (contexto de habilidade), `Tests/Core/EditMode/Structure/SourceRulesTests.cs`, `Documentation~/manual/modulos/gas.md`, `Documentation~/manual/regras/migracao-2-para-3.md`.
- Depende de: 0M-20, 0M-19.
- CAs: CA29, CA62, CA30.
- Testes red: `SpawnProjectile_NoSpawner_NoPoolManager_NoException`, `PlayCue_NoSpawner_NoPoolManager_NoException`, `SpawnVfx_ReleasesThroughConfiguredSpawner`, `Projectile_ReturnsThroughSpawner` falham (NRE do `PoolManager.Instance` / `Destroy` direto); `NoPoolManagerInstanceOutsidePooling` falha (3 ocorrências no GAS).
- Manual: GAS, seções **Projéteis** e **Ações de spawn** (campo `Spawner`, armadilha do VFX do `PlayCueAction` até o Cues); Migração (`Projectile.Setup`, `IPoolable`).
- Tamanho: ~220 linhas, 9 arquivos.

### 0M-22 — `refactor(gas): esperas do GAS pelo relógio do ASC`
- Arquivos: `Runtime/GAS/Abilities/AbilityContext.cs` (`WaitSeconds`), `Runtime/GAS/Abilities/Actions/WaitAction.cs`, `Runtime/GAS/Abilities/Actions/FlowActions.cs`, `Runtime/GAS/Abilities/Actions/SpawnProjectileAction.cs`, `Runtime/GAS/Abilities/Projectiles/Projectile.cs` (vida e invulnerabilidade como contadores), `Tests/GAS/EditMode/SpellBook.GAS.Tests.EditMode.asmdef`, `Tests/GAS/EditMode/GasSourceScanTests.cs`, `Tests/GAS/PlayMode/AbilityWaitTests.cs`, `Documentation~/manual/modulos/gas.md`.
- Depende de: 0M-21.
- CAs: CA10, CA32 (parte: EditMode do GAS).
- Testes red: `GasSources_DoNotReadUnityTimeOrWaitForSeconds` falha (5 ocorrências de `WaitForSeconds`); `WaitAction_FinishesByAscTicks_NotRealTime` falha (com `autoTick` desligado e sem `Tick`, a espera atual termina pelo relógio real; com o ASC, só termina após `Tick` somar a duração).
- Manual: GAS, seção **Ações que esperam** (a espera conta o tempo do ASC; pausa do grupo pausa a habilidade).
- Tamanho: ~180 linhas, 9 arquivos.

### 0M-23 — `refactor(pooling)!: módulo SpellBook.Pooling e PooledSpawnerAsset`
- Arquivos: `Runtime/Pooling/SpellBook.Pooling.asmdef`, `Runtime/Pooling/PoolManager.cs`, `ComponentPooler.cs`, `PoolConfiguration.cs`, `PoolableObject.cs`, `IPoolable.cs` (os 5 movidos de `Runtime/Systems/Pooling/`, namespace novo, `[HelpURL]`), `Runtime/Pooling/PooledSpawnerAsset.cs`, `Tests/Pooling/EditMode/SpellBook.Pooling.Tests.EditMode.asmdef`, `Tests/Pooling/EditMode/PooledSpawnerTests.cs`, `Documentation~/manual/modulos/pooling.md`.
- Depende de: 0M-21.
- CAs: CA28, CA35 (parte: regra do `PooledSpawnerAsset`), CA6 (parte), CA32 (parte).
- Testes red: `Pooled_NoPoolManager_SpawnsWithoutException`, `Pooled_ReleaseThenSpawn_ReusesSameInstance`, `Pooled_ReleaseNonPooled_Destroys`, `Pooled_NegativeInitialSize_IsValidationError` falham (tipo não existe). **Primeiro validador:** o red confirma que `new SelfValidationResult()` e a leitura dos itens funcionam fora do Inspector; senão, aplica o plano B de 4.9 neste ticket e avisa o Controlador.
- Manual: Pooling, seções **PooledSpawnerAsset**, **PoolManager (pré-aquecimento)** e **ComponentPooler/IPoolable**; Migração recebe a linha do namespace no próximo ticket que edita a página (0M-25). **Inferido:** I-c.
- Tamanho: ~200 linhas de código novo; os 5 arquivos movidos contam pela diferença real (namespace, menu e `[HelpURL]`, ~30 linhas). 10 arquivos.

### 0M-24 — `refactor(core)!: MathTools como classe estática no Core`
- Arquivos: `Runtime/MathTools/Math.cs` (apagado), `Runtime/Core/MathTools.cs`, `Tests/Core/EditMode/MathToolsTests.cs`, `Documentation~/manual/inicio/conceitos.md` (seção Core: `Void`, `RuntimeSet<T>`, `MathTools`).
- Depende de: 0M-12.
- CAs: CA43.
- Testes red: `MathTools_IsStaticInCoreNamespace` e `NoMonoBehaviourNamedMath` falham; caracterização de `Map`, `IsInRange`, `MidPoint`, `CenterOfMass`; `RandomPointInCircle`/`InSphere` testados só pela propriedade "dentro do raio" (sem depender da semente).
- Manual: Conceitos, seção **Core**.
- Tamanho: ~150 linhas, 4 arquivos.

### 0M-25 — `refactor(events)!: remover os três sistemas de eventos antigos`
- Arquivos (decisão I42: arquivos apagados por inteiro, sem substituto, não contam no limite de arquivos nem no de linhas): apagar `Runtime/Core/Events/**` (2), `Runtime/Core/NewEventSystem/**` (18, exceto `Void.cs`), `Runtime/Systems/Events/**` (4); mover `Runtime/Core/NewEventSystem/Void.cs` → `Runtime/Core/Void.cs` (namespace `SpellBook.Core`); `Runtime/Core/Sets/RuntimeSet.cs` (namespace `SpellBook.Core`); `Tests/Core/EditMode/Structure/SourceRulesTests.cs`; `Documentation~/manual/regras/migracao-2-para-3.md` (tabela de eventos, Pooling, MathTools, `Void`, `RuntimeSet`). Contam no limite: 4 arquivos (as 24 remoções puras ficam listadas no PR, fora da contagem).
- Depende de: 0M-12, 0M-24.
- CAs: CA25, R22.
- Testes red: `OldEventFoldersDoNotExist`, `NoTypeNamed_GameEventListner_EventManager_GameEvent` falham.
- Manual: Migração (tabela "tipo antigo → tipo novo" completa dos eventos; lacuna do R22a).
- Tamanho: ~60 linhas de código (fora as remoções), 4 arquivos.

### 0M-26 — `feat(events): EventBus<T> e registro limpo por sessão`
- Arquivos: `Runtime/Events/SpellBook.Events.asmdef` (Core, Time; `InternalsVisibleTo` em `AssemblyInfo.cs`), `Runtime/Events/AssemblyInfo.cs`, `Runtime/Events/IEvent.cs`, `Runtime/Events/EventBus.cs`, `Runtime/Events/EventRegistry.cs`, `Runtime/Events/ListenerList.cs`, `Tests/Events/EditMode/SpellBook.Events.Tests.EditMode.asmdef`, `Tests/Events/EditMode/EventBusTests.cs`, `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs` (Events só Core e Time), `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-25, 0M-17.
- CAs: CA15, CA16, CA17, CA19 (bus), CA2 (parte).
- Testes red: `Bus_DeliversToAll`, `Bus_UnsubscribeOtherDuringRaise_NoSkipNoDouble`, `Bus_SubscribeDuringRaise_NotCalledThisRaise`, `Bus_ExceptionInSecond_FirstAndThirdReceive_LogsException`, `Bus_ThousandRaises_ZeroAlloc`, `Bus_AfterSessionReset_NoListeners` falham (tipos não existem).
- Manual: **Eventos** (o que é; regra dos três usos; `EventBus<T>` com exemplo; segurança do disparo; release build sem `try/catch`).
- Tamanho: ~280 linhas, 10 arquivos.

### 0M-27 — `feat(events): EventChannel e canais Void, Int e Float`
- Arquivos: `Runtime/Events/EventChannel.cs` (base e `<T>`, hook do debugger, botão Raise), `Runtime/Events/Channels/VoidEventChannel.cs`, `IntEventChannel.cs`, `FloatEventChannel.cs`, `Tests/Events/EditMode/EventChannelTests.cs`, `Tests/Testing/SpellBook.Testing.asmdef` (+Events), `Tests/Testing/EventsFactory.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-26.
- CAs: CA15, CA16, CA18, CA19 (canal), CA33 (parte: canal).
- Testes red: `Channel_RaiseDeliversExactValue`, `Channel_LastPayload`, `Channel_ReentrancySafe`, `Channel_ExceptionIsolatedInEditor`, `Channel_ThousandRaises_ZeroAlloc`, `Channel_AfterSessionReset_NoListeners`, `Channel_LogRaises_WritesToConsole` falham.
- Manual: Eventos, seções **Canais** (`{#eventchannel}`, `{#voideventchannel}` …), criar pelo menu, descrição, botão Raise, `logRaises`, armadilhas (asset duplicado por Addressables; ordem não garantida; `Void` vs `System.Void`).
- Tamanho: ~260 linhas, 8 arquivos.

### 0M-28 — `feat(events): canais Bool, String, Vector3 e GameObject`
- Arquivos: `Runtime/Events/Channels/BoolEventChannel.cs`, `StringEventChannel.cs`, `Vector3EventChannel.cs`, `GameObjectEventChannel.cs`, `Tests/Events/EditMode/ChannelMenuTests.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-27.
- CAs: CA12.
- Testes red: `Menu_HasExactlySevenChannels_NoComponent` falha (só 3 canais).
- Manual: Eventos, âncoras dos 4 canais.
- Tamanho: ~90 linhas, 6 arquivos.

### 0M-29 — `feat(events): EventListener com respostas e reações`
- Arquivos: `Runtime/Events/EventListener.cs`, `Runtime/Events/EventResponse.cs`, `Runtime/Events/BuiltInResponses.cs`, `Runtime/Events/Reaction.cs`, `Tests/Events/PlayMode/SpellBook.Events.Tests.PlayMode.asmdef`, `Tests/Events/PlayMode/EventListenerTests.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-28.
- CAs: CA13, CA14; CA38 (S) passa a ser verificável após o deploy.
- Testes red: `Listener_ReceivesExactFloat_ReactionsInOrderThenUnityEvent`, `Listener_Disabled_DoesNotReceive_ReenabledReceives` falham.
- Manual: Eventos, seções **EventListener** `{#eventlistener}`, **Respostas** e **Payload próprio** (o struct e as duas classes; exemplo completo).
- Tamanho: ~280 linhas, 7 arquivos.

### 0M-30 — `feat(events): reações SetActive, Log, Toggle e RaiseChannel`
- Arquivos: `Runtime/Events/Reaction.cs` (+`Validate`), `Runtime/Events/Reactions/SetActiveReaction.cs`, `ToggleComponentReaction.cs`, `LogReaction.cs`, `RaiseChannelReaction.cs`, `Tests/Events/PlayMode/ReactionTests.cs`, `Tests/Events/EditMode/ReactionValidationTests.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-29.
- CAs: CA47, CA48, CA49, CA50, CA58 (parte runtime), CA35 (parte: campos obrigatórios das reações).
- Testes red: `SetActive_ActivatesInactive_AndDeactivates`, `RaiseVoid_RaisesTargetOnce_ListenerReceives`, `Forward_RaisesSameTypeWithPayload_3_5`, `Forward_DifferentType_DoesNotRaise`, `Log_WritesExactMessage`, `Toggle_SetsEnabled`, `EmptyRequiredField_IsValidationError` (cada reação) falham.
- Manual: Eventos, seções **Reações prontas** com uma âncora por reação.
- Tamanho: ~300 linhas, 8 arquivos.

### 0M-31 — `feat(events): reação Invoke com delay`
- Arquivos: `Runtime/Events/Reactions/DelayedReaction.cs`, `Runtime/Events/EventListener.cs` (esperas pendentes, `Update`, cancelamento no `OnDisable`), `Tests/Events/PlayMode/DelayedReactionTests.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-30, 0M-18.
- CAs: CA51, CA59, CA60.
- Testes red: `Delayed_1s_NotAt0_9_OnceAt1_1_InOrder` (com `ManualTimeSource`), `Delayed_ListenerDisabledDuringWait_NeverRuns`, `Delayed_NoAsset_TimeScaleZero_DoesNotRun`, `Delayed_NoReactions_IsValidationError` falham.
- Manual: Eventos, seção **Invoke com delay** `{#delayedreaction}` (fonte de tempo, cancelamento). **Inferido:** I-i.
- Tamanho: ~200 linhas, 4 arquivos.

### 0M-32 — `feat(events): EventEmitter e payload próprio de ponta a ponta`
- Arquivos: `Runtime/Events/EventEmission.cs`, `Runtime/Events/EventEmitter.cs`, `Tests/Events/EditMode/EmissionSerializationTests.cs`, `Tests/Events/PlayMode/EventEmitterTests.cs`, `Tests/Events/PlayMode/TestPayload/TestPayloadEventChannel.cs`, `Tests/Events/PlayMode/TestPayload/TestPayloadTypes.cs` (struct e `TestPayloadEventResponse`), `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-31.
- CAs: CA20, CA22, CA35 (parte: emissor sem canal).
- Testes red: `Emission_GenericRoundTrip_Float_AndCustomStruct` (risco 16; se falhar após a implementação, aplica o plano B da 0013 e volta ao usuário), `Emitter_Raise_RaisesConfiguredPayload`, `Emitter_ChannelTypeChange_RecreatesEmission`, `CustomPayload_EndToEnd_EmitterChannelListenerReaction`, `Emitter_NoChannel_IsValidationError` falham.
- Manual: Eventos, seção **EventEmitter** `{#eventemitter}` (disparo por `UnityEvent`, animação e botão).
- Tamanho: ~250 linhas, 7 arquivos.

### 0M-33 — `feat(events): validação de ouvintes e respostas`
- Arquivos: `Runtime/Events/EventListener.cs` (`ISelfValidator`), `Runtime/Events/EventResponse.cs` (`Validate`: sem canal, `UnityEvent` sem alvo ou método, reações com o tipo do payload), `Tests/Events/EditMode/EventsValidationTests.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-32, 0M-23 (API do validador confirmada).
- CAs: CA35 (parte).
- Testes red: `Response_NoChannel_IsError`, `Escape_CallWithoutTarget_IsError`, `Escape_CallWithoutMethod_IsError`, `Forward_ReactionWrongType_IsError`, `ValidListener_NoIssues` falham.
- Manual: Eventos, seção **Validação**.
- Tamanho: ~150 linhas, 4 arquivos.

### 0M-34 — `feat(events): seletor de canal filtrado por payload`
- Arquivos: `Editor/Events/SpellBook.Events.Editor.asmdef`, `Editor/Events/ChannelCatalog.cs`, `Editor/Events/ChannelPickerDrawer.cs`, `Tests/Events/EditMode/SpellBook.Events.Tests.EditMode.asmdef` (+Events.Editor), `Tests/Events/EditMode/ChannelCatalogTests.cs`, `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-33.
- CAs: CA21, CA58 (seletor), CA1 (parte).
- Testes red: `Filter_FloatResponse_OnlyFloatChannels`, `Filter_ForwardReaction_OnlySameType`, `Filter_Void_OnlyVoid` falham.
- Manual: Eventos, seção **Escolher o canal** (filtro e descrição ao lado).
- Tamanho: ~220 linhas, 7 arquivos.

### 0M-35 — `feat(events): Event Debugger`
- Arquivos: `Editor/Events/EventDebuggerModel.cs`, `Editor/Events/EventDebuggerWindow.cs`, `Runtime/Events/EventChannel.cs` (ajuste do hook com emissor), `Tests/Events/EditMode/EventDebuggerModelTests.cs`, `Documentation~/manual/ferramentas/event-debugger.md`.
- Depende de: 0M-34.
- CAs: R20, CA23 (S).
- Testes red: `Model_RecordsFrameTimePayloadSender`, `Model_SearchByName`, `Model_Clear`, `Model_RingBufferKeepsLast256`, `Model_NoRecordingWhenClosed` falham.
- Cenário CA23 no host: canal "PlayerDied", botão com `EventEmitter`, `EventListener` com SetActive num painel; o debugger mostra o disparo com horário e o ouvinte.
- Manual: **Event Debugger** (abrir, ler a lista, histórico, busca, limpar). **Inferido:** I-d.
- Tamanho: ~300 linhas, 5 arquivos.

### 0M-36 — `feat(events): Find references de canal`
- Arquivos: `Editor/Events/ChannelReferenceFinder.cs`, `Editor/Events/ChannelContextMenu.cs`, `Editor/Events/EventDebuggerWindow.cs` (botão), `Tests/Events/EditMode/FindReferencesTests.cs`, `Tests/Events/EditMode/TestListenerAsset.cs` (SO de teste com uma resposta), `Documentation~/manual/ferramentas/event-debugger.md`.
- Depende de: 0M-35.
- CAs: CA24.
- Testes red: `Find_PrefabEmitterAndSoListener_ExactlyTwo_WithRoles`, `Find_ClosedSceneListener_NotIncluded` falham (risco 23).
- Manual: Event Debugger, seção **Find references** (escopo: assets e cenas abertas).
- Tamanho: ~250 linhas, 6 arquivos.

### 0M-37 — `feat(gas-bridges): RaiseEventAction`
- Arquivos: `Runtime/GAS.Bridges/SpellBook.GAS.Bridges.asmdef`, `Runtime/GAS.Bridges/RaiseEventAction.cs`, `Runtime/GAS/Abilities/AbilityAction.cs` (`virtual Validate`), `Runtime/GAS/Abilities/GameplayAbility.cs` (`ISelfValidator` repassando às ações, `[HelpURL]`), `Tests/GAS.Bridges/EditMode/SpellBook.GAS.Bridges.Tests.EditMode.asmdef`, `Tests/GAS.Bridges/EditMode/RaiseEventActionTests.cs`, `Tests/GAS.Bridges/PlayMode/SpellBook.GAS.Bridges.Tests.PlayMode.asmdef`, `Tests/GAS.Bridges/PlayMode/RaiseEventAbilityTests.cs`, `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs`, `Documentation~/manual/modulos/gas.md`.
- Depende de: 0M-32, 0M-22.
- CAs: CA52, CA61, CA35 (parte: `RaiseEventAction` sem canal), CA2 (Bridges sem Pooling), CA32 (parte).
- Testes red: `Ability_RaiseVoid_OnceAndListenerReacts`, `Action_IntChannelFixed7_Raises7`, `Action_NoChannel_IsValidationError`, `AsmdefRules_BridgesDoesNotReferencePooling` falham.
- Manual: GAS, seção **RaiseEventAction** `{#raiseeventaction}` (canal sem payload ou com payload fixo; payload dinâmico no B-004).
- Tamanho: ~240 linhas, 10 arquivos.

### 0M-38 — `feat(core): validar projeto por menu e por teste`
- Arquivos: `Editor/SpellBook.Editor.asmdef` → `Editor/Core/SpellBook.Core.Editor.asmdef` (renomeado e movido com o `.meta`), `Editor/FolderStructureCreator.cs` → `Editor/Core/FolderStructureCreator.cs`, `Editor/Core/ProjectValidator.cs`, `Editor/Core/ValidationWindow.cs`, `Tests/Core/EditMode/SpellBook.Core.Tests.EditMode.asmdef` (+Core.Editor, Events, Pooling, GAS, GAS.Bridges), `Tests/Core/EditMode/ProjectValidationTests.cs`, `Documentation~/manual/ferramentas/validador.md`.
- Depende de: 0M-37, 0M-33.
- CAs: CA35, R33, R34, CA34 (S).
- Testes red: `Validator_ReportsEachR35Error` (um objeto em memória por regra), `Validator_FixedObjects_NoErrors`, `Project_HasNoValidationErrors` (projeto do host) falham (`ProjectValidator` não existe).
- Cenário CA34: no host, um `EventListener` sem canal aparece na janela e o clique seleciona o objeto.
- Manual: **Validador** (menu, ler a janela, regras da 0-M, o teste no CI).
- Tamanho: ~280 linhas, 7 arquivos.

### 0M-39 — `docs(gas): HelpURL nos tipos restantes e teste de HelpURL`
- Arquivos: `Runtime/GAS/Attributes/AttributeSet.cs`, `AttributeDefinition.cs`, `Formulas/SimpleAttributeFormula.cs`, `Formulas/ExpressionFormula.cs`, `Runtime/GAS/Targeting/CircleTargeting.cs`, `MouseTargetingStrategy.cs`, `Runtime/GAS/Effects/GameplayEffect.cs`, `Tests/Core/EditMode/HelpUrlTests.cs`, `Documentation~/manual/modulos/gas.md` (âncoras que faltarem). (`GameplayAbility` recebeu no 0M-37.)
- Depende de: 0M-38.
- CAs: CA36, CA37.
- Testes red: `AllPublicConcreteMbAndSo_HaveHelpUrl` falha (7 tipos sem atributo); `AllHelpUrls_StartWithBase` passa a cobrir todos.
- Manual: GAS (âncoras e uma linha por tipo, se faltar).
- Tamanho: ~80 linhas, 9 arquivos.

### 0M-40 — `refactor(core)!: Core em Runtime/Core e verificação final da estrutura`
- Arquivos: `Runtime/SpellBook.Core.asmdef` → `Runtime/Core/SpellBook.Core.asmdef` (com o `.meta`, GUID mantido; as pastas vazias `Runtime/Systems`, `Runtime/MathTools` e os `.meta` delas somem), `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs` (lista completa do CA1 e do CA32), `Tests/Core/EditMode/ManualLinksTests.cs`, `HostProject~/Assets/Scenarios/C1/C1.PoolingOnly.asmdef` (refs só Core e Pooling), `HostProject~/Assets/Scenarios/C1/C1PoolingUsage.cs`, `Documentation~/manual/inicio/conceitos.md` (mapa final), `Documentation~/manual/index.md`.
- Depende de: 0M-39.
- CAs: CA1, CA32, CA54, CA6, N4.
- Testes red: `AsmdefRules_CoreAsmdefInRuntimeCore` falha (asmdef ainda na raiz de `Runtime/`); `AllExpectedAssembliesExist` e `EachRuntimeModuleHasEditModeTests` são guardas finais; `EveryHelpUrlHasPageAndAnchor` e `NoStubPages` são guardas (red mostrado removendo uma âncora localmente). CA6: o host compila o cenário C1 (no CI, ao rodar a suíte).
- Manual: Conceitos (mapa final com as referências permitidas) e O que é (lista final de módulos).
- Tamanho: ~150 linhas, 7 arquivos.

---

## 9. Pendências para o gate

- Resolvidas pelas decisões I41 a I44: host (D1), remoção pura (0M-25 num ticket só), 2.x congelada, alocação do `Tick` com efeito periódico (D3).
- Abertas: **P21** (licença do Odin no CI; o 0M-11 continua bloqueado) e o passo manual do usuário de ligar o Pages com "Source: GitHub Actions" (0M-09).
