# Spec: Fase 0-M, modularização do SpellBook

Status: **Aprovada** (gate da revisão sem Odin, 2026-10-29). A versão anterior foi aprovada no gate de 2026-10-29 e volta a rascunho por causa das decisões I45 a I47.
Data: 2026-10-29 · Backlog: B-001 (e B-020, B-021, B-022 pela via rápida)
PRD: [prd/modularizacao.md](../prd/modularizacao.md) · Plano: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção I (decisões 1 a 47)
ADRs: 0001, 0002, 0003, 0004, 0005, 0006 (Adiada), 0007, 0008, 0009, 0010, 0011 (Substituída por 0026), 0012, 0013, 0014, 0015, 0016, 0025, **0026 (Proposta)**

> **Revisão sem Odin (2026-10-29).** O usuário decidiu que o SpellBook não depende do Odin (I45), que drawers e janelas são em UI Toolkit (I46) e que não há compatibilidade com o Odin (I47). A ADR 0026 substitui a 0011. A revisão:
> - remove o Odin dos 8 arquivos atuais logo depois do host (0M-44);
> - cria a infraestrutura de testes com a guarda "nenhum Sirenix" (0M-07);
> - cria o seletor de tipo para `[SerializeReference]` (catálogo no 0M-41, popup UI Toolkit no 0M-47, drawer no 0M-42), o `[ShowIf]` (0M-46), a interface de validação (0M-43), o Inspector base `SpellBookEditor` com validação no topo (0M-48), os Inspectors do Events (0M-45) e um `[CustomEditor]` de uma linha por tipo (0M-49, 0M-50);
> - desbloqueia o CI (0M-11).
>
> **Segunda rodada (2026-10-29, decisões I48 a I50):** nenhuma exceção IMGUI. Saem o `AdvancedDropdown` e o gancho `Editor.finishedDefaultHeaderGUI`, e entram o popup próprio e o `SpellBookEditor`. As perdas no Inspector do GAS foram aceitas (I49) e o canal dentro da `EventEmission` também (I50).
>
> Os ids 0M-01 a 0M-40 não mudam (issues #2 a #41). Os tickets novos são 0M-41 a 0M-50. A coluna "Mudança" da seção 8 mostra o que mudou em cada ticket. O PRD está sendo ajustado em paralelo pelo Criativo; os CAs novos CA63 a CA66 foram aceitos no PRD.

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
- O Odin é usado só em 8 arquivos do GAS: `GameplayAbility.cs:8`, `GameplayEffect.cs:6`, `AttributeDefinition.cs:2`, `ModifierValueSource.cs:19,22,25` (nome qualificado, sem `using`), `AbilityDescription.cs:5`, `CommonEffectActions.cs:4`, `ExpressionFormula.cs:5`, `SimpleAttributeFormula.cs:5`. São só atributos de `Sirenix.OdinInspector`; não há `SerializedScriptableObject` nem `Sirenix.Serialization`. O inventário por atributo está no Contexto da 0026. Os que importam para o comportamento:
  - `[ShowIf]` por campo irmão (6) e por expressão `@Period > 0` (2, `GameplayEffect.cs:55,66`);
  - `[InfoBox]` (3);
  - `[Required]` (4);
  - a pré-visualização `[ReadOnly][ShowInInspector]` de `ExpressionFormula.cs:48-53`, atualizada por `[OnValueChanged]` (`:40`).
- **Campos `[SerializeReference]` do GAS** (todos precisam do seletor de tipo, 0M-42):
  - `GameplayAbility.cs:76`;
  - `GameplayEffect.cs:59,64,70`;
  - `AbilityDescription.cs:97`;
  - `FlowActions.cs:18,39,86,89`;
  - `SpawnProjectileAction.cs:20`.
- `Runtime/SpellBook.Core.asmdef` tem `overrideReferences: false`. Num projeto com Odin, a DLL de atributos entra por referência automática. Num projeto sem Odin, os 8 arquivos não compilam.
- (Histórico, sem efeito após a 0026) O Odin 3.3.x foi verificado num projeto local: símbolos `ODIN_INSPECTOR*` e `ISelfValidator` no assembly de atributos.

### 1.2 Projeto host

- A raiz do repositório é **ao mesmo tempo** a pasta do pacote (`package.json` na raiz) e um projeto Unity (`Assets/`, `Library/`, `Packages/`, `ProjectSettings/`, todos no `.gitignore`), versão 6000.3.5f2 (`ProjectSettings/ProjectVersion.txt`).
  - Atualização (2026-10-29, decisão do usuário no 0M-06): o host antigo foi movido, sem ser apagado, para `C:/Users/gabri/MeusProjetos/SpellBook-host-antigo/`. A raiz agora só tem os arquivos do pacote (ADR 0004, emenda do host antigo).
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
| 0003 | Todo ticket ≤ ~400 linhas de código e ≤ 10 arquivos (páginas e fixtures contam só nos arquivos; arquivos apagados por inteiro, sem substituto, não contam em nenhum dos dois, decisão I42); WIP 1 para tickets com Unity; paralelo só sem Editor (emenda de 2026-10-29, seção 8) |
| 0004 | Red antes do código; testes de guarda e de caracterização marcados; ciclo de vida em PlayMode; host em `HostProject~/` (decisão I41), sem Odin (emenda 0026); o 0M-44 é o único ticket com red de compilação |
| 0005 | Fábrica e Examinador criam e removem `.claude/editor.lock` antes de usar a Unity no host |
| 0006 | Adiada: uma branch por ticket no mesmo checkout |
| 0007 | Cada ticket que cria componente ou SO público traz `[HelpURL]` e a seção do manual; constantes de página em `DocUrls` |
| 0008 | Commits Conventional com escopo do módulo; o `version` (`3.0.0-pre.1`) e o CHANGELOG mudam só no release (DevOps); a 2.x está congelada, sem `release/2.x` (decisão I43) |
| 0009 | Sem `[MovedFrom]`, `[Obsolete]` nem `[FormerlySerializedAs]`; quebras listadas na seção 7 |
| 0010 | `"unity": "6000.0"`; CI na 6000.0 e na última 6000.x. Desvio: host local na 6000.3.13f1, não na 6000.0 (D5) |
| 0011 | Substituída pela 0026: nada dela vale (sem constraint, sem Setup, sem DLL do Odin) |
| 0012 | Tabela de módulos com a emenda Events → Time; asmdefs auxiliares listados; emenda 0026 (Core sem Odin, `Core.Editor` no 0M-41, `Events.Editor` no 0M-45, `Time.Editor`/`Tags.Editor`/`Pooling.Editor` no 0M-49, `GAS.Editor` no 0M-50, `.Editor` de módulo pode referenciar `Core.Editor`, sem `Setup.Editor`, `overrideReferences: true`) |
| 0013 | Reações não genéricas; `EventEmission<T>` genérica criada por código, verificada no 0M-32; seletor `[TypePicker]` com o filtro da 0013 (0M-41) |
| 0014 | `ITimeSource { ElapsedTime; DeltaTime }`, consumo por diferença; grupo pausável segue o escalado, só pausa; reset por `SubsystemRegistration` |
| 0015 | `SpawnerAsset` com `GameObject`; `PooledSpawnerAsset` sem `PoolManager.Instance`; `PlayCueAction` só troca o spawn |
| 0016 | Canais, bus, ouvinte, emissor e reações conforme as decisões incorporadas e a emenda 0026 (Inspectors UI Toolkit, canal dentro da `EventEmission`) |
| 0025 | `Documentation~/mkdocs.yml` com `docs_dir: manual`; Action do Pages; CI sem Odin (emenda 0026) |
| 0026 (Proposta) | Nenhum `Sirenix`/`ODIN_INSPECTOR` no pacote (guarda no 0M-07); `overrideReferences: true`; `[TypePicker]`, `[TypeCategory]`, `[ShowIf]` no Core; `IValidatable`; drawers, Inspectors e janelas em UI Toolkit sem nenhuma exceção IMGUI (I48); popup próprio do seletor; `SpellBookEditor` com validação no topo e um `[CustomEditor]` de uma linha por tipo |

---

## 3. Desvios e valores inferidos

### Desvios

- **D1. Host de testes em `HostProject~/`, não na raiz** (PRD R41, CA55). **Resolvido pela decisão I41**, que substitui a I32. Motivo técnico na ADR 0004: um `file:` apontando para a raiz faria a Unity importar `Assets/`, `Library/` e `ProjectSettings/` do próprio host como conteúdo do pacote. O host usa `HostProject~/Packages/manifest.json` com o pacote por `file:` e em `testables`, **sem Odin** (0026). Leitura do CA55 com a I41 e a I45: "`HostProject~/Packages/manifest.json`" e "`.gitignore` contém os artefatos do host", sem nenhuma entrada do Sirenix (o Criativo ajusta o PRD).
  - **Raiz sem projeto Unity** (decisão do usuário, 2026-10-29): o mesmo motivo vale para o host antigo que estava na raiz. Ele foi movido para fora do repositório, e a raiz não pode ter `Assets/`, `Library/`, `Packages/` nem `ProjectSettings/` (risco 34).
  - **Caminho relativo** (confirmado em 2026-10-29 como valor esperado; o PRD deve escrever `file:../..`): a I41 escreve `file:../`. A Unity resolve caminhos relativos do `manifest.json` a partir da pasta `Packages/` do projeto, não da raiz dele. Por isso a raiz do repositório, vista de `HostProject~/Packages/`, é `file:../..`. O 0M-06 confirma o caminho no red: o Package Manager precisa mostrar `com.spellbook` como pacote local. Se `file:../` resolver, fica `file:../`.
- **D2. Nome do canal sem payload** (PRD R12). `EventChannel` é a base abstrata não genérica (descrição, `logRaises`, tipo do payload, ouvintes vivos) e o canal sem payload concreto é `VoidEventChannel : EventChannel<Void>` com `Raise()`. Motivo: um `EventChannel` concreto e um `EventChannel<T>` com o mesmo nome confundem o seletor, o menu Create e o filtro por tipo, e o debugger precisa de uma base comum. O menu continua "Void Channel" (C2 e CA12 inalterados).
- **D3. Alocação do `ASC.Tick` com efeito periódico** (PRD N2). **Aceito pela decisão I44** até o B-002. `AttributeState.Recalculate` usa LINQ (`AttributeState.cs:69-82`) e roda a cada tick periódico (`ApplyModifiersToBase` → `SetBaseValue`). A correção é do B-002 (fora da 0-M). O teste de N2 da 0-M mede o `Tick` em regime com efeitos de duração **sem** tick periódico naquele quadro.
- **D4. `RaiseEventAction` saiu do B-004** (PRD seção 6 antiga). Já reconciliado pelo Criativo (R25a, decisões I22 e I39); listado só para rastreio.
- **D5. Host local na 6000.3.13f1, não na 6000.0** (ADR 0010, consequência "o host fixa a 6000.0"; I-j na parte do host). Decisão do usuário em 2026-10-29. Motivo: a 6000.0 não está instalada na máquina (instaladas: 6000.3.1f1, 6000.3.5f2, 6000.3.13f1, 6000.4.10f1). O `HostProject~/ProjectSettings/ProjectVersion.txt` fica com 6000.3.13f1 (0M-06). Não mudam o mínimo declarado (`"unity": "6000.0"`: 0M-08, CA4, CA42) nem a matriz do CI (0M-11: 6000.0 e última 6000.x).
  - Risco: código que usa API ou comportamento que só existe a partir da 6000.1 passa no host. Os reds que dizem "confirma na 6000.0" (riscos 16, 25, 27 e 28; 0M-32, 0M-41, 0M-42, 0M-46, 0M-47, 0M-48) passam a confirmar na versão do host, e só o job 6000.0 do CI prova a 6000.0. Por isso o 0M-11 vem logo depois do 0M-07 (seção 8); antes dele, nenhum ticket depende de API da 6000.0.
  - Cobertura: o CI (0M-11) roda a suíte na 6000.0 com a versão explícita da matriz; o Examinador barra API nova sem a guarda de versão do item 3 da 0010; release só com as duas versões verdes (0010). O cenário CA5 é verificado no host 6000.3.13f1, e a compilação na 6000.0 fica com o CA57. A ADR 0010 foi emendada e o CA5 do PRD foi ajustado pelo Criativo (2026-10-29).
  - Quando a 6000.0 for instalada: um ticket `chore(host)` troca o `ProjectVersion.txt` para o patch da 6000.0 usado no CI (I-j), revê o `com.unity.test-framework` (I-g) e roda a suíte inteira no host. Com ela verde, este desvio sai e o I-j volta a valer para o host.

### Inferidos (validar no uso)

| Id | Valor | Onde |
|---|---|---|
| ~~I-a~~ | ~~Odin mínimo 3.3~~. **Cancelado** pela 0026 (sem Odin) | — |
| I-b | Grupo pausável segue o tempo escalado, só pausa, sem escala própria | 0014, 0M-18 |
| I-c | Pool criado sob demanda pelo `PooledSpawnerAsset`: tamanho inicial 5, expansível (os valores do fallback atual do `PoolManager.cs:56-57`), configuráveis no asset | 0M-23 |
| I-d | Histórico do Event Debugger: buffer circular de 256 disparos por sessão de Play | 0M-35 |
| I-e | Navegação do site gerada da árvore de pastas (sem `nav:` explícito), para nenhum ticket precisar editar a configuração ao criar página | 0M-08 |
| I-f | Âncoras de `[HelpURL]` explícitas com `attr_list` (`## EventListener {#eventlistener}`) | 0M-08, CA54 |
| I-g | `com.unity.test-framework` fixado no manifest numa versão que resolva no host (6000.3.13f1, D5) e na 6000.0 do CI: a que acompanha a 6000.0, se o host aceitar; senão, a do host, e o job 6000.0 do CI confirma | 0M-06, 0M-11 |
| I-h | Menus de criação dos assets do GAS continuam `GAS/...` (R27 pede tags sem mudança de comportamento); padronizar para `SpellBook/...` fica para a Fase 9 | 0M-14 |
| I-i | Uma espera pendente do Invoke com delay aloca uma entrada por disparo (fora do caminho sem alocação do N2) | 0M-31 |
| I-j | Patch exato da 6000.0 e da última 6000.x no CI: o mais recente disponível no GameCI na data do ticket. Não vale para o `ProjectVersion.txt` do host enquanto durar o D5 | 0M-11 |
| I-k | Categorias das reações prontas no seletor: `Objeto` (SetActive, Toggle de componente), `Eventos` (RaiseChannel), `Depuração` (Log), `Fluxo` (Invoke com delay) | 0M-30, 0M-31 |
| I-l | Tipos sem `[TypeCategory]` aparecem na raiz da lista do seletor. As ações e os elementos de descrição do GAS ficam sem categoria na 0-M; as categorias deles entram nas fases do GAS | 0M-41, 0M-42 |
| ~~I-m~~ | ~~Lista do seletor por `AdvancedDropdown`~~. **Resolvido pela I48:** popup próprio em UI Toolkit (0M-47) | — |
| ~~I-n~~ | ~~Validação no Inspector por `finishedDefaultHeaderGUI`~~. **Resolvido pela I48:** `SpellBookEditor` (0M-48) | — |
| I-o | Teclado do popup: setas, Enter, Esc, e Backspace com busca vazia volta um nível. Abre com o foco na busca | 0M-47 |
| I-p | O `SpellBookEditor` tem um botão "Revalidar" no contêiner de validação, além do recálculo automático por `TrackSerializedObjectValue` | 0M-48 |

---

## 4. Desenho técnico por módulo

### 4.1 Mapa final de pastas e asmdefs

```
Runtime/
  Core/      SpellBook.Core.asmdef (o mesmo .meta de hoje, movido no 0M-40)
             DocUrls.cs, Void.cs, MathTools.cs, Sets/RuntimeSet.cs,
             Spawning/SpawnerAsset.cs, Spawning/InstantiateSpawnerAsset.cs, Spawning/ParticleStopReleaser.cs (internal),
             Inspector/TypePickerAttribute.cs, Inspector/TypeCategoryAttribute.cs, Inspector/ShowIfAttribute.cs,
             Validation/IValidatable.cs, Validation/ValidationResult.cs, Validation/ValidationMessage.cs
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
  Core/      SpellBook.Core.Editor.asmdef (renomeado no 0M-41, mesmo .meta): FolderStructureCreator,
             Inspector/TypePickerCatalog, Inspector/TypePickerTree, Inspector/TypePickerPopup,
             Inspector/TypePickerDrawer, Inspector/ShowIfDrawer, Inspector/SpellBookEditor,
             Inspector/CoreEditors, ProjectValidator, ValidationWindow
  Events/    SpellBook.Events.Editor.asmdef (0M-45; refs Core, Time, Events, Core.Editor):
             EventChannelEditor, EventEmitterEditor, EventEmissionDrawer, EventsEditors,
             ChannelCatalog, ChannelPickerDrawer, EventDebuggerWindow, EventDebuggerModel,
             ChannelReferenceFinder, ChannelContextMenu
  Time/      SpellBook.Time.Editor.asmdef (0M-49; refs Core, Time, Core.Editor): TimeEditors
  Tags/      SpellBook.Tags.Editor.asmdef (0M-49; refs Core, Tags, Core.Editor): TagsEditors
  Pooling/   SpellBook.Pooling.Editor.asmdef (0M-49; refs Core, Pooling, Core.Editor): PoolingEditors
  GAS/       SpellBook.GAS.Editor.asmdef (0M-50; refs Core, Tags, Time, GAS, Core.Editor): GasEditors
  (não existe Editor/Setup: 0026)
Tests/
  Testing/   SpellBook.Testing.asmdef: GasFactory, TimeFactory, EventsFactory, SpawnFactory
  <Módulo>/EditMode, <Módulo>/PlayMode
HostProject~/ (decisão I41)
Documentation~/mkdocs.yml, requirements-docs.txt, manual/
.github/workflows/docs.yml, tests.yml
```

Regras de todos os asmdefs do pacote (0026, item 1). Nenhum tem constraint ou referência do Odin.

| Tipo de asmdef | `overrideReferences` | `precompiledReferences` | Outros |
|---|---|---|---|
| Runtime | `true` | `[]` | `autoReferenced: true`, `defineConstraints: []` |
| Editor | `true` | `[]` | `includePlatforms: ["Editor"]`, `autoReferenced: true` |
| Testes e `SpellBook.Testing` | `true` | `["nunit.framework.dll"]` | `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, `autoReferenced: false`, `references` com `UnityEngine.TestRunner` e `UnityEditor.TestRunner`; EditMode com `includePlatforms: ["Editor"]` |

O `AsmdefRulesTests` (0M-07) cobra essas regras, e o `NoOdinTests` (0M-07) cobra a ausência do Odin.

**Referências entre asmdefs de Editor** (0026, item 5; emenda 0012):
- um `SpellBook.<Módulo>.Editor` referencia o próprio runtime, as dependências dele e, se precisar, o `SpellBook.Core.Editor`;
- nunca referencia o `.Editor` de outro módulo;
- o `SpellBook.Core.Editor` referencia só o `SpellBook.Core`.

O `AsmdefRulesTests` cobra essa regra a partir do 0M-45.

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

```csharp
namespace SpellBook.Core.Inspector            // 0026, item 5; um atributo por arquivo
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TypePickerAttribute : PropertyAttribute { }                 // 0M-41

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TypeCategoryAttribute : Attribute                          // 0M-41
    {
        public TypeCategoryAttribute(string path);
        public string Path { get; }
        public string Summary { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ShowIfAttribute : PropertyAttribute                        // 0M-46
    {
        public ShowIfAttribute(string siblingField, object value);   // irmão bool ou enum; igualdade
        public string SiblingField { get; }
        public object Value { get; }
    }
}

namespace SpellBook.Core.Validation           // 0026, item 6; 0M-43
{
    public enum ValidationSeverity { Warning, Error }
    public readonly struct ValidationMessage { Severity; Message; Context; PropertyPath }
    public sealed class ValidationResult
    {
        public ValidationResult(UnityEngine.Object context);
        public UnityEngine.Object Context { get; }
        public IReadOnlyList<ValidationMessage> Messages { get; }
        public bool HasErrors { get; }
        public void AddError(string message, string propertyPath = null);
        public void AddWarning(string message, string propertyPath = null);
    }
    public interface IValidatable { void Validate(ValidationResult result); }
}
```

- `ValidationResult` guarda as mensagens numa `List<ValidationMessage>` criada no construtor. `Messages` devolve a lista como `IReadOnlyList`. `AddError` e `AddWarning` rejeitam mensagem nula ou vazia com `ArgumentException`.
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
- `PooledSpawnerAsset : SpawnerAsset, IValidatable` (`Create > SpellBook > Pooling > Pooled Spawner`): campos `initialSize = 5`, `canExpand = true`; dicionário `prefab → ComponentPooler` em memória; raiz `[SpellBook Pools]` criada sob demanda (`DontDestroyOnLoad` só se `Application.isPlaying`); `Release` procura o `PoolableObject` e devolve ao pool de origem, ou destrói se a instância não for de pool. Validação: `initialSize < 0` é erro. Cache limpo ao entrar em Play (`SubsystemRegistration`).

### 4.7 Events

- **`ListenerList<T>`** (interno, compartilhado por bus e canal): `List<Action<T>>` + profundidade de disparo + "sujo". `Raise` fixa `count` no início; desinscrever durante um disparo põe `null` na posição; ao fim do disparo mais externo, compacta. Inscrever duas vezes o mesmo delegate é ignorado. Em `UNITY_EDITOR || DEVELOPMENT_BUILD`, cada chamada fica em `try/catch` com `Debug.LogException`.
- **`EventBus<T> where T : struct, IEvent`**: `Subscribe(Action<T>)`, `Unsubscribe(Action<T>)`, `Raise(in T)`; construtor estático registra o `Clear` no `EventRegistry`.
- **`EventRegistry`** (interno): lista de ações de limpeza; `SubsystemRegistration` executa todas (CA19). Exposto aos testes por `InternalsVisibleTo("SpellBook.Events.Tests.EditMode")` e `.PlayMode`.
- **`EventChannel`** (abstrato): `[TextArea] description`, `logRaises`, `abstract Type PayloadType`, `int ListenerCount`, `void GetListeners(List<object> targets)` (para o debugger). **`EventChannel<T>`**: `event Action<T> Raised` (add/remove para o `ListenerList<T>`), `Raise(T payload)`, `Raise(T payload, UnityEngine.Object sender)`, `T LastPayload`, `[SerializeField] T debugPayload` (payload de teste usado pelo botão; `[Tooltip]`). O botão "Raise" (ativo só no Play), a lista de ouvintes vivos e o último payload são desenhados pelo `EventChannelEditor` (0M-45), não por atributo. Hook de observação estático (`internal static Action<EventChannel, object, UnityEngine.Object> Observed`), chamado só se não for nulo e só em `UNITY_EDITOR`; é por onde o debugger grava o histórico.
- **Canais:** `VoidEventChannel` (`Raise()`), `IntEventChannel`, `FloatEventChannel`, `BoolEventChannel`, `StringEventChannel`, `Vector3EventChannel`, `GameObjectEventChannel`, cada um num arquivo, menu `SpellBook/Events/<Tipo> Channel`.
- **`EventResponse`** (abstrato `[Serializable]`, `IValidatable`): `abstract EventChannel Channel`, `Subscribe(EventListener)`, `Unsubscribe()`, `Validate(ValidationResult)`. **`EventResponse<T>`**:
  - `[SerializeField] EventChannel<T> channel`;
  - `[SerializeReference, TypePicker] List<Reaction> reactions`;
  - `[SerializeField] EscapeEvent<T> escape`, em que `[Serializable] public sealed class EscapeEvent<T> { public UnityEvent<T> Event; }`.

  A classe aninhada aparece no Inspector nativo como foldout **fechado** por padrão, com o rótulo "Escape". Isso atende o R18 sem atributo (0016, emenda). Respostas prontas num arquivo (`VoidEventResponse` ... `GameObjectEventResponse`). Payload próprio: `XEventChannel : EventChannel<X>` + `XEventResponse : EventResponse<X>` (R14).
- **`EventListener : MonoBehaviour, IValidatable`**: `[SerializeReference, TypePicker] List<EventResponse> responses`; inscreve no `OnEnable`, desinscreve e cancela esperas no `OnDisable`; `Update` só processa esperas pendentes (sai cedo se não houver). Ordem: reações da lista, depois o `escape`.
- **`Reaction`** (abstrato `[Serializable]`, `IValidatable`): `virtual void Execute(EventListener listener)`, `virtual void Execute<T>(EventListener listener, in T payload)` (padrão: chama o primeiro), `virtual void Validate(ValidationResult result)` (campos obrigatórios) e `virtual void Validate(ValidationResult result, Type payloadType)` (padrão: chama o primeiro).
- **Reações prontas** (cada uma com `[TypeCategory]`, I-k):
  - `SetActiveReaction { GameObject target; bool active }`;
  - `ToggleComponentReaction { Behaviour target; Mode { Enable, Disable, Toggle } }`;
  - `LogReaction { string message; LogType type = Log }`;
  - `RaiseChannelReaction { Mode { ForwardPayload, RaiseVoid }; EventChannel target }`. No modo `ForwardPayload`, só dispara se `target is EventChannel<T>`, e o validador da resposta acusa tipo diferente;
  - `DelayedReaction { float delay; TimeSourceAsset timeSource; [SerializeReference, TypePicker] List<Reaction> reactions }`.
- **`EventEmission`** (abstrato, `IValidatable`: sem canal é erro) e **`EventEmission<T>`** (`EventChannel<T> channel`, `T payload`, `Raise(sender)`). Tem `abstract EventChannel Channel { get; }`, `abstract Type PayloadType { get; }` e `static EventEmission CreateFor(EventChannel channel)` por `MakeGenericType`; com canal nulo, devolve `null`.
- **`EventEmitter : MonoBehaviour, IValidatable`**: `[SerializeReference] EventEmission emission` (sem `[TypePicker]`; o canal fica **dentro** da emissão, 0016 emenda) e `public void Raise()`. O botão no Play vem do `EventEmitterEditor` (0M-45). O `EventEmissionDrawer` (0M-45) mostra o campo de canal e, quando o tipo de payload do canal escolhido difere do da emissão atual, troca `managedReferenceValue` por `EventEmission.CreateFor(canal)`. A lógica fica numa função estática testável, `EventEmissionEditing.SetChannel(SerializedProperty emission, EventChannel channel)`.
- **Editor** (`SpellBook.Events.Editor`, tudo em UI Toolkit):
  - `EventChannelEditor`: botão Raise no Play, ouvintes vivos e último payload;
  - `EventEmitterEditor`: botão Raise no Play;
  - `EventEmissionDrawer`;
  - `ChannelCatalog.Filter(IEnumerable<EventChannel>, Type payloadType)` (função pura, CA21 e CA58) e `ChannelCatalog.FindAll()` (AssetDatabase);
  - `ChannelPickerDrawer` para campos `EventChannel` dentro de respostas e de `RaiseChannelReaction` (modo repassar): um botão abre a lista filtrada, e a descrição do canal aparece num `HelpBox`;
  - `EventDebuggerWindow` (`EditorWindow.CreateGUI`) + `EventDebuggerModel` (busca, histórico, limpar);
  - `ChannelReferenceFinder.Find(EventChannel)`, que varre prefabs, ScriptableObjects e cenas carregadas por `SerializedObject`, com papel decidido pelo tipo dono da referência (`EventEmission` e `RaiseChannelReaction` → emite; `EventResponse` → ouve).

### 4.8 GAS.Bridges

- `RaiseEventAction : AbilityAction` com `[SerializeReference] EventEmission emission`, o mesmo campo e o mesmo drawer do emissor (canal sem payload ou `EventChannel<T>` com payload fixo, decisão I39). O canal fica dentro da emissão (0016, emenda). `Execute` dispara uma vez e termina no mesmo quadro.
- **Validação de ações** (R35, "`RaiseEventAction` sem canal"):
  - `AbilityAction` implementa `IValidatable`, com `public virtual void Validate(ValidationResult result)` vazio por padrão;
  - o `GameplayAbility` implementa `IValidatable` e repassa a chamada para cada ação da lista;
  - a `RaiseEventAction` repassa para a emissão: emissão nula ou sem canal é erro.

  O GAS continua sem conhecer o tipo concreto (0013).

### 4.9 Validação (Editor)

- `ProjectValidator.Validate(IEnumerable<UnityEngine.Object> targets, List<ValidationMessage> messages)` (0026, item 6):
  - cobre cada alvo e, nos `GameObject`, cada componente que implementa `IValidatable`;
  - para cada um, cria um `ValidationResult(alvo)`, chama `Validate` e junta as mensagens;
  - uma exceção dentro de `Validate` vira um erro com o tipo e a mensagem da exceção, e a varredura continua;
  - alvos com `SerializationUtility.HasManagedReferencesWithMissingTypes` recebem um erro por referência ausente, com o assembly e a classe (CA64).
- `ProjectValidator.CollectProjectTargets()`: assets de `ScriptableObject` e prefabs do projeto (`AssetDatabase.FindAssets`), mais os objetos das cenas abertas.
- Regra de `[HelpURL]` (R35, último item): função `HelpUrlRule.FindViolations(IEnumerable<Assembly>)`, usada pelo teste CA36 e pela janela.
- O menu `SpellBook/Validar projeto` abre a `ValidationWindow` (UI Toolkit, `EditorWindow.CreateGUI`, `ListView`) com a lista. Clicar num item seleciona o objeto e faz ping nele (CA34).
- **Inspector** (R32, I48): as mensagens aparecem no topo do Inspector do próprio objeto, pelo `SpellBookEditor` (0M-48, seção 4.10), que todo tipo do pacote usa (0M-49, 0M-50). O `ProjectValidator` não desenha nada no Inspector, e não existe gancho IMGUI.

### 4.10 Editor próprio (0026)

O Setup sem Odin foi **cancelado**: não existe `SpellBook.Setup.Editor` nem `OdinMissingNotice`.

**Seletor de tipo** (0M-41, em `Editor/Core/Inspector/`):
- **`TypePickerCatalog`** (estático, sem UI, testável):
  - `IReadOnlyList<TypePickerEntry> GetCandidates(Type baseType)`: aplica o filtro da 0026, item 4 (concreto, não genérico, `[Serializable]`, construtor sem parâmetros, fora de `UnityEngine.Object`), usa `TypeCache.GetTypesDerivedFrom` e guarda o resultado em cache por tipo base;
  - `TypePickerEntry { Type Type; string DisplayName; string CategoryPath; string Summary }`;
  - `IEnumerable<TypePickerEntry> Filter(IEnumerable<TypePickerEntry>, string query)`: busca sem diferenciar maiúsculas e minúsculas por nome exibido, nome do tipo e caminho;
  - `Type ResolveFieldBaseType(SerializedProperty)`: lê `managedReferenceFieldTypename`;
  - `void Assign(SerializedProperty, Type typeOrNull)`: instância nova ou nulo, com Undo;
  - `bool EnsureUniqueInArray(SerializedProperty element)`: troca por cópia o elemento cujo `managedReferenceId` já aparece antes na mesma lista; devolve `true` se trocou;
  - `bool TryGetMissingType(SerializedProperty, out string assembly, out string fullName)`, por `SerializationUtility.GetManagedReferencesWithMissingTypes(target)` e `managedReferenceId`.
- **`TypePickerDrawer`** (`[CustomPropertyDrawer(typeof(TypePickerAttribute))]`, `CreatePropertyGUI`):
  - linha de cabeçalho com o rótulo e um botão com o nome do tipo atual (ou "(Nenhum)"); o botão abre o `TypePickerPopup` (0M-47);
  - abaixo, um `PropertyField` por filho da instância. Isso não é recursivo, porque cada `PropertyField` é de um filho, não da própria propriedade;
  - `TrackPropertyValue` reconstrói os filhos quando o tipo muda;
  - tipo ausente: `HelpBox` de erro "Tipo não encontrado: `<assembly>` `<namespace.Classe>`" + botão "Limpar referência" (`SerializationUtility.ClearManagedReferenceWithMissingType`). Nunca limpa sozinho;
  - ao ligar, chama `EnsureUniqueInArray`;
  - `OnGUI` (IMGUI) desenha só o `HelpBox` "Este campo precisa do Inspector em UI Toolkit".

**`[ShowIf]`** (0M-46, `ShowIfDrawer`):
- o drawer não pode devolver `new PropertyField(property)` da própria propriedade, porque isso cai de novo no mesmo drawer (recursão);
- o red do 0M-46 decide entre duas formas, nesta ordem:
  - (a) devolver o campo padrão criado por `PropertyField` com o drawer contornado, se a 6000.0 permitir;
  - (b) construir o campo pelo tipo da propriedade (`PropertyField` de cada filho para struct e classe; `BindableElement` por `propertyType` para os primitivos usados: `float`, `int`, `bool`, `enum`, `Color`, `string`, referência de objeto).
- a visibilidade (`style.display`) é atualizada por `TrackPropertyValue` do irmão;
- a regra fica em `ShowIfRule.IsVisible(SerializedProperty field, ShowIfAttribute attr)`, testável: irmão `bool` compara com `bool`; irmão `enum` compara `intValue` com `Convert.ToInt32(value)`; irmão inexistente ou de outro tipo → visível e `Debug.LogWarning` uma vez por campo.

**Popup do seletor** (0M-47, I48; sem `AdvancedDropdown`):
- **`TypePickerTree`** (puro, testável):
  - `static TypePickerTree Build(IEnumerable<TypePickerEntry>)` monta nós de categoria e de tipo por `CategoryPath`. A ordem é alfabética, categorias antes de tipos, e "(Nenhum)" é o primeiro item da raiz;
  - `IReadOnlyList<TypePickerNode> Children(TypePickerNode level)`;
  - `TypePickerNode Parent(TypePickerNode)`;
  - `IReadOnlyList<TypePickerEntry> Search(string query)` devolve uma lista plana, por `TypePickerCatalog.Filter`.
- **`TypePickerPopup : EditorWindow`**:
  - `static void Show(Rect activatorWorldBound, Type baseType, Action<Type> onPicked)`, por `ShowAsDropDown`;
  - `CreateGUI` monta: `ToolbarSearchField` (com foco), cabeçalho do nível com "voltar", `ListView` (categorias com seta, tipos com o nome) e um rodapé com o `Summary` do item marcado;
  - teclado I-o;
  - fecha ao escolher, com Esc ou ao perder o foco;
  - `onPicked(null)` para "(Nenhum)".

**Inspector base** (0M-48, I48), em `Editor/Core/Inspector/SpellBookEditor.cs` (API na 0026, item 3a):
- `CreateInspectorGUI` monta:
  - o contêiner de validação (classe USS `spellbook-validation`), com um `HelpBox` por mensagem e um botão "Revalidar" (I-p);
  - `BuildBeforeDefault`;
  - `InspectorElement.FillDefaultInspector(root, serializedObject, this)`;
  - `BuildAfterDefault`.
- `RefreshValidation()` cria um `ValidationResult` por alvo em `targets`, chama `Validate` quando o alvo é `IValidatable` e reconstrói os `HelpBox`. Uma exceção vira um `HelpBox` de erro com o tipo da exceção, como no `ProjectValidator`.
- O recálculo é chamado no build e em `root.TrackSerializedObjectValue(serializedObject, _ => RefreshValidation())`.
- Função testável: `static IReadOnlyList<ValidationMessage> SpellBookEditor.CollectMessages(IEnumerable<UnityEngine.Object> targets)`.
- **Um editor por tipo** (0M-49, 0M-50):
  - um arquivo por módulo (`CoreEditors.cs`, `TimeEditors.cs`, `TagsEditors.cs`, `PoolingEditors.cs`, `GasEditors.cs`, `EventsEditors.cs`), com classes `[CustomEditor(typeof(X))] sealed class XEditor : SpellBookEditor { }`;
  - um tipo base com `editorForChildClasses: true` cobre as subclasses (por exemplo, `TimeSourceAsset`);
  - o canal e o emissor usam os editors com botão do 0M-45.
- **Teste de cobertura** (`SpellBookEditorCoverageTests`): para cada tipo público, concreto, `MonoBehaviour` ou `ScriptableObject` dos assemblies de runtime cobertos, cria uma instância em memória (`ScriptableObject.CreateInstance` ou `AddComponent` num `GameObject` temporário), faz `Editor.CreateEditor(instância)` e confere `is SpellBookEditor`. Só usa API pública e destrói tudo no `TearDown`.

**Inspectors do Events** (0M-45): ver 4.7. `EventChannelEditor` e `EventEmitterEditor` herdam de `SpellBookEditor` e sobrescrevem `BuildAfterDefault`.

### 4.11 Documentação

- `Documentation~/mkdocs.yml`: `site_url: https://gabrielctorres.github.io/SpellBook/`, `docs_dir: manual`, `use_directory_urls: true`, tema Material (ou Zensical, escolha no 0M-08), `markdown_extensions: [attr_list, admonition, toc]`, sem `nav:` (I-e). Build com `--strict`.
- Páginas da 0-M (todas com conteúdo real, decisão I26):

| Página | Arquivo | Criada em | Ampliada em |
|---|---|---|---|
| O que é | `manual/index.md` | 0M-08 | 0M-40 |
| Instalação | `manual/inicio/instalacao.md` | 0M-08 (com a seção Compatibilidade, D5) | — |
| Conceitos (mapa de módulos, três usos de evento, seletor de tipo e `[ShowIf]`) | `manual/inicio/conceitos.md` | 0M-12 | 0M-24, 0M-26, 0M-40 |
| Inspector do SpellBook (seletor de tipo, tipo ausente, `[MovedFrom]` para tipos do usuário, `[ShowIf]`, ambiguidade com outros pacotes) | `manual/inicio/inspector.md` | 0M-42 | 0M-46, 0M-48 (validação no topo, botão Revalidar), 0M-50 (como herdar de `SpellBookEditor` nos tipos do usuário) |
| Migração 2.x → 3.0 | `manual/regras/migracao-2-para-3.md` | 0M-12 (já com a linha "o pacote não depende mais do Odin; perdas no Inspector do GAS", do 0M-44) | 0M-15, 0M-16, 0M-19, 0M-21, 0M-23, 0M-25 |
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
| ~~Todo asmdef, exceto Setup, tem `ODIN_INSPECTOR`~~ | — | ~~CA53~~ | **Cancelado** (0026); substituído pela linha seguinte |
| Nenhum `.asmdef` (em `references`, `precompiledReferences`, `defineConstraints`, `versionDefines`) e nenhum `.cs`/`.asmdef`/`.asmref`/`.uxml`/`.uss` de `Runtime/`, `Editor/`, `Tests/`, nem o `package.json`, contém `Sirenix` ou `ODIN_INSPECTOR` | `Core/EditMode/Structure/NoOdinTests.cs` | CA65 (substitui CA53) | Guarda (o 0M-44 já tirou o Odin); red mostrado com um `.cs` temporário contendo `using Sirenix.OdinInspector;`, sem commit |
| Todo asmdef do pacote tem `overrideReferences: true`; runtime e Editor com `precompiledReferences: []`; testes só com `nunit.framework.dll` | `Core/EditMode/Structure/AsmdefRulesTests.cs` | CA65 (parte), 0026 | `Runtime/SpellBook.Core.asmdef` e `Editor/SpellBook.Editor.asmdef` têm `overrideReferences: false` |
| Infra não referencia `SpellBook.GAS*`; GAS não referencia Events/Pooling; Bridges não referencia Pooling; Events só Core/Time | idem | CA2 | Guarda no 0M-07 (um assembly só); cada ticket que cria módulo acrescenta a asserção de existência do módulo, que falha antes de o asmdef existir |
| Runtime não referencia `UnityEditor` | `Core/EditMode/Structure/RuntimeReferencesTests.cs` | CA3 (sem as partes do Odin) | Guarda |
| Seletor: candidatos de um tipo base de teste = só os concretos, não genéricos, `[Serializable]`, com construtor sem parâmetros, fora de `UnityEngine.Object`, inclusive de outro assembly; categoria e resumo vêm do `[TypeCategory]`; sem atributo vai para a raiz; busca por nome e categoria | `Core/EditMode/Inspector/TypePickerCatalogTests.cs` | CA63 | `TypePickerCatalog` não existe |
| Seletor: `Assign` cria instância do tipo escolhido e `null` limpa; elemento de lista com `managedReferenceId` repetido vira cópia independente | idem | CA63, 0013 | Idem |
| `TypePickerTree`: hierarquia por categoria, "(Nenhum)" primeiro, categorias antes de tipos, `Parent` volta um nível, `Search` plana com o caminho | `Core/EditMode/Inspector/TypePickerTreeTests.cs` | CA63 | `TypePickerTree` não existe |
| `SpellBookEditor`: alvo `IValidatable` inválido gera `HelpBox` de erro no topo; válido, nenhum; alvo não `IValidatable`, contêiner escondido; `BuildAfterDefault` aparece depois dos campos; exceção no `Validate` vira erro | `Core/EditMode/Inspector/SpellBookEditorTests.cs` | R32 | `SpellBookEditor` não existe |
| Todo MB/SO público concreto de runtime tem Inspector derivado de `SpellBookEditor` (Core, Time, Tags, Pooling no 0M-49; GAS, Events, GAS.Bridges no 0M-50) | `Core/EditMode/Inspector/SpellBookEditorCoverageTests.cs` | R32 (I48) | Não há `[CustomEditor]` para esses tipos |
| `ShowIfRule`: irmão enum igual → visível, diferente → oculto; irmão bool; irmão inexistente → visível | `Core/EditMode/Inspector/ShowIfRuleTests.cs` | (0026) | `ShowIfRule` não existe |
| GAS: todo campo `[SerializeReference]` de `Runtime/GAS` tem `[TypePicker]` | `Core/EditMode/Inspector/TypePickerUsageTests.cs` | CA63 | Os 10 campos do GAS não têm o atributo |
| `ValidationResult`: guarda severidade, mensagem, contexto e caminho; `HasErrors` só com erro; mensagem vazia lança | `Core/EditMode/Validation/ValidationResultTests.cs` | R32 | Tipos não existem |
| Inspector do canal tem botão "Raise" desabilitado fora do Play; Inspector do emissor idem | `Events/EditMode/EventsInspectorTests.cs` | R12, R19 | `Events.Editor` não existe |
| `EventEmissionEditing.SetChannel`: canal Float numa emissão Int recria como `EventEmission<float>`; mesmo tipo mantém o payload; canal nulo deixa a emissão sem canal | idem | CA22 (parte), 0016 | Função não existe |
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
| Validador do projeto: cada erro do R35 aparece; corrigidos, nenhum; projeto do host sem erros; exceção num `Validate` vira erro sem parar a varredura | `Core/EditMode/ProjectValidationTests.cs` | CA35, R34 | `ProjectValidator` não existe |
| Validador do projeto: asset com referência `[SerializeReference]` a tipo inexistente (YAML escrito na pasta temporária) gera erro com assembly e classe, e os dados continuam no arquivo | idem | CA64 | `ProjectValidator` não existe |
| Todo MB/SO público concreto de runtime tem `[HelpURL]` iniciado por `DocUrls.Base` | `Core/EditMode/HelpUrlTests.cs` | CA36, CA37 | 8 tipos do GAS sem `[HelpURL]` |
| Cada `[HelpURL]` tem página e âncora `{#id}` no manual; nenhuma página é só título ou "em breve" | `Core/EditMode/ManualLinksTests.cs` | CA54 | Guarda (as páginas nascem com cada ticket); red mostrado removendo uma âncora localmente |
| Lista completa de asmdefs e um `Tests.EditMode` por módulo de runtime | `AsmdefRulesTests.cs` | CA1, CA32 | Guarda final; falha se algum ticket anterior faltar |

Cenários (S), verificados pelo Examinador no host e registrados na seção 9 do PRD:
- CA5 (0M-44: host sem Odin compila sem erro nem aviso do SpellBook);
- CA6 (0M-40, também como compilação automática no host);
- CA23 (0M-35);
- CA34 (0M-38);
- CA38 e CA39 (0M-09 e a cada página nova);
- CA57 (0M-11);
- seletor de tipo no host (0M-42): adicionar uma `WaitAction` numa habilidade pelo seletor, salvar, reabrir e conferir o valor.

O CA41 foi **cancelado** (não existe cenário sem Odin).

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
| 14 | Dados externos | ~~Odin ausente ou símbolo velho~~. **Removido** (0026). Risco novo: um `using Sirenix` volta ao pacote por engano num projeto local que tem o Odin | Todos os asmdefs | `overrideReferences: true` com `precompiledReferences: []` (o compilador recusa); host e CI sem Odin | `NoOdinTests`, `AsmdefRulesTests` (0M-07) |
| 15 | Dados externos | ~~API de `SelfValidationResult`~~. **Removido**: a interface é própria (`IValidatable`, 0M-43) | — | — | — |
| 16 | Dados externos | Suporte da 6000.0 a genérico fechado em `[SerializeReference]` | `EventEmission<T>` | Teste de ida e volta no 0M-32; plano B (0013) | 0M-32 |
| 17 | Dados externos | Disponibilidade das imagens GameCI (a parte da licença do Odin, P21, foi **removida** pela 0026) | 0M-11 | Versões fixadas no workflow | CA57 |
| 18 | Dados externos | ~~Primeira compilação sem `ODIN_INSPECTOR`~~. **Removido** (0026). Continua valendo a verificação de que a contagem de testes executados é > 0 | 0M-11 | Falha se 0 testes | 0M-11 |
| 19 | Dados externos | Secrets da Unity (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`); não há secret do Odin | 0M-11 | Só via `secrets.*`; nada escrito no workflow | CA56 |
| 20 | Dados externos | Pages precisa ser ligado nas configurações do repositório ("Source: GitHub Actions"), passo manual do dono | 0M-09 | Passo listado no ticket para o usuário | CA39 |
| 21 | Dados externos | Versões Python da ferramenta de doc mudam | 0M-08 | `requirements-docs.txt` com `==` | Build `--strict` |
| 22 | Dados externos | Testes que leem arquivos do pacote (asmdefs, `.cs`, manual) dependem do caminho real | CA2, CA10, CA25, CA30, CA54, CA65 | Caminho obtido por `PackageInfo.FindForAssembly(...).resolvedPath`; os testes rodam só no host com `file:` | 0M-07 |
| 23 | Dados externos | Testes que criam assets e cenas em disco (Find references) deixam lixo ou mudam as cenas abertas | 0M-36 | Pasta temporária `Assets/__SpellBookTestTemp` criada no `SetUp` e apagada no `TearDown`; cenas abertas salvas e restauradas com `EditorSceneManager` | CA24 |
| 24 | Dados externos | ~~Setup lê `documentationUrl`~~. **Removido**: o Setup foi cancelado (0026) | — | — | — |
| 25 | Dados externos | **Compatibilidade do UI Toolkit, parte 1: recursão.** Um drawer que devolve `new PropertyField(property)` da própria propriedade cai de novo no mesmo drawer | `ShowIfDrawer` | Duas formas, (a) e (b), decididas no red do 0M-46 (4.10). O `TypePickerDrawer` só cria `PropertyField` de filhos | Cenário no host do 0M-42 e do 0M-46 (o Inspector abre sem estouro de pilha) |
| 26 | Dados externos | **Compatibilidade do UI Toolkit, parte 2: Inspector IMGUI.** Um Inspector IMGUI do usuário, ou um Inspector de terceiros (o Odin, por exemplo), desenha campos do SpellBook sem os drawers UI Toolkit | Drawers do Core e do Events | O `OnGUI` dos drawers mostra só o `HelpBox` de uma linha "precisa do Inspector em UI Toolkit" (único `OnGUI` permitido, 0026 item 3); sem suporte a outros Inspectors (I47); o manual explica na página Inspector | Página Inspector (0M-42) |
| 27 | Dados externos | **Compatibilidade do UI Toolkit, parte 3: APIs na 6000.0.** `CreatePropertyGUI`, `TrackPropertyValue`, `TrackSerializedObjectValue`, `CreateInspectorGUI`, `InspectorElement.FillDefaultInspector`, `EditorWindow.ShowAsDropDown`, `CreateGUI`, `ToolbarSearchField`, `ListView`, `TypeCache` e `SerializationUtility.*MissingTypes` precisam existir e se comportar igual na 6000.0 e na última 6000.x (0010, emenda) | 0M-41, 0M-47, 0M-42, 0M-46, 0M-48, 0M-38, 0M-45 | O red de cada ticket confirma a API no host (6000.3.13f1, D5); a 6000.0 e a última 6000.x são confirmadas pelo CI | CI (0M-11) |
| 28 | Dados externos | **Tipo serializado não encontrado.** Um tipo `[SerializeReference]` removido ou renomeado (do pacote na 3.0, de uma ponte ou do usuário) deixa a referência com "tipo ausente"; um clique errado ou uma limpeza automática apagaria os dados | Assets com `[SerializeReference]` | Drawer mostra o erro e só limpa por botão (0026, item 4); validador acusa erro (CA64); tipos do pacote renomeados na 3.0 vão para a tabela de migração (seção 7); depois da `3.0.0`, `[MovedFrom]`; o manual ensina `[MovedFrom]` ao usuário | 0M-41 (red confirma que a Unity guarda os dados do tipo ausente na 6000.0), 0M-38 (CA64) |
| 29 | Repetição | **Referência compartilhada.** Duplicar um elemento de lista `[SerializeReference]` no Inspector deixa dois elementos com o mesmo `managedReferenceId`; editar um muda o outro (proibido pela 0013) | Listas com `[TypePicker]` | `EnsureUniqueInArray` no bind do drawer | `TypePickerCatalogTests` (0M-41) |
| 30 | Repetição | Custo do seletor e da validação no Inspector | `TypePickerCatalog`, `SpellBookEditor` | Candidatos em cache por tipo base (o `TypeCache` já é indexado); validação só ao montar o Inspector e em `TrackSerializedObjectValue`, nunca por repaint | N5; revisão do Examinador |
| 32 | Dados externos | **Inspector do usuário concorre com o do pacote.** Um `[CustomEditor]` do usuário para um tipo do SpellBook (ou de terceiros, com `editorForChildClasses`) pode ser o escolhido pela Unity, e a validação no topo some | Tipos do pacote | O manual (página Inspector, 0M-50) manda herdar de `SpellBookEditor`; a janela do validador continua mostrando as mensagens | Página Inspector |
| 33 | Dados externos | **Bind do UI Toolkit em teste.** `CreateInspectorGUI` fora de um painel não liga os campos (`Bind`), e o teste não deve depender disso | `SpellBookEditorTests`, `EventsInspectorTests` | Os testes conferem só a estrutura (contêiner, `HelpBox`, botões) e as funções puras (`CollectMessages`, `SetChannel`); o comportamento ligado fica nos cenários no host | 0M-48, 0M-45 |
| 34 | Dados externos | **Projeto Unity na raiz do pacote.** A raiz do repositório é a pasta do pacote; um `Assets/`, `Library/`, `Packages/` ou `ProjectSettings/` ali, mesmo ignorado pelo git, entra como conteúdo do `com.spellbook` (assemblies duplicados, `.meta` gravados, importação lenta) | Raiz do repositório, `HostProject~/` | Host antigo movido para fora (ADR 0004, emenda de 2026-10-29); único host em `HostProject~/` | Critério do 0M-06: as quatro pastas não existem na raiz quando o host abre |
| 31 | Dados externos | Ambiguidade de nome (`ShowIf`, `TypeCategory`) com outros pacotes no código do usuário | Projetos do usuário | Namespace `SpellBook.Core.Inspector` (só quem escreve `using` dele vê os nomes); o manual mostra o alias | Página Inspector |

---

## 7. Quebras de compatibilidade (para o CHANGELOG da `3.0.0-pre.1`)

O DevOps leva esta lista ao CHANGELOG no release, com a tabela da página "Migração 2.x → 3.0" (CA26):

| Antes (2.x) | Depois (3.0) | Ticket |
|---|---|---|
| Assembly único `SpellBook.Core` | `SpellBook.Core`, `.Time`, `.Events`, `.Pooling`, `.Tags`, `.GAS`, `.GAS.Bridges` (asmdefs do usuário precisam referenciar os novos) | 0M-12 em diante |
| O pacote exigia o Odin Inspector (sem ele, erros de compilação) | O pacote não depende do Odin; a serialização e os assets não mudam | 0M-44 |
| Inspector do GAS com abas, grupos, `InlineEditor` (custo, recarga, fórmula), pré-visualização da `ExpressionFormula`, destaque de `[Required]`, tabelas e busca de ações (Odin) | Inspector padrão da Unity com `[Header]`/`[Tooltip]`, `[ShowIf]` próprio onde havia `ShowIf` por campo e seletor de tipo próprio; o resto se perde até os épicos do GAS (perdas aceitas pela I49). Nenhum dado é perdido | 0M-44, 0M-42, 0M-46 |
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

Ordem por dependência. **WIP 1**: um ticket com Unity em implementação por vez (exceção para tickets sem Editor logo abaixo). Nenhum ticket está bloqueado depois da revisão sem Odin. "Linhas" é a estimativa de código, sem páginas e fixtures (ADR 0003). Todos os tickets de código usam a trava (0005) e terminam com a suíte inteira verde no host. A exceção é o 0M-44, que termina com o host compilando, porque ainda não existe suíte.

**Paralelo sem Editor** (emenda da 0003, 2026-10-29): o WIP 1 vale para os tickets que usam a Unity e a trava. Um ticket de docs, processo ou CI, sem trava, pode rodar em paralelo, na própria branch e numa cópia de trabalho própria (0006 adiada), desde que não toque arquivo do outro ticket. Par marcado nesta Spec: **0M-11 em paralelo com o 0M-41** (o 0M-11 só cria `.github/workflows/tests.yml`). Outros pares só valem se forem marcados aqui.

### Resumo

Mudança: **igual** (sem mudança), **alterado** (escopo, arquivos, dependência ou critério), **novo** (id novo), **cancelado** (fica registrado com o motivo e não é implementado).

| Id | Título | Depende de | CAs do PRD | Mudança | Motivo da mudança |
|---|---|---|---|---|---|
| 0M-01 | `chore(process): apontar a skill /issue para gabrielctorres/SpellBook` | — | (B-020) | igual | — |
| 0M-02 | `docs(process): alinhar TIME.md, CLAUDE.md, agentes e skills ao fluxo novo` | 0M-01 | (B-022) | igual | Verificado: `TIME.md`, `CLAUDE.md` e os 5 arquivos de agente e skill do ticket não citam o Odin (busca em 2026-10-29); nada a mudar |
| 0M-03 | `chore(process): adaptar agentes de programação e QA ao TIME.md` | 0M-02 | (B-021) | igual | — |
| 0M-04 | `chore(process): adaptar agentes de design, writer e Unity ao TIME.md` | 0M-02 | (B-021) | alterado | `unity-explorer.md:13` cita o Odin e `Assets/_Project` (contexto de jogo); o critério passa a cobrar a remoção |
| 0M-05 | `chore(host): ignorar artefatos do host no git` | 0M-02 | CA55 (parte) | alterado | Sai o `.gitignore` do Sirenix (I45) |
| 0M-06 | `chore(host): criar projeto host de testes com o pacote em testables` | 0M-05 | CA55 | alterado | Host sem Odin; o critério registra os erros `Sirenix` como red do 0M-44; host na 6000.3.13f1 (D5) |
| 0M-44 | `refactor(gas)!: remover o Odin dos 8 arquivos do runtime` | 0M-06 | CA5 | **novo** | I45: o pacote precisa compilar no host sem Odin antes de qualquer teste |
| 0M-07 | `test(core): asmdef de testes do Core e regras de estrutura sem Odin` | 0M-44 | CA65, CA3, CA2 (guarda) | alterado | Era "desligar sem Odin e avisar" (Setup, `defineConstraints`), que a I47 elimina. Fica a infraestrutura de teste e entra a guarda "nenhum Sirenix" + `overrideReferences` |
| 0M-11 | `ci(test): rodar as suítes na 6000.0 e na última 6000.x com GameCI` | 0M-07 | CA56, CA57, N1 | alterado | Desbloqueado (P21 sem objeto); sem secret nem script do Odin; sem passo de importação extra. Adiantado para logo depois do 0M-07 (D5); a seção "Compatibilidade" do manual passou para o 0M-08 |
| 0M-41 | `feat(core): catálogo do seletor de tipo e atributos TypePicker/TypeCategory` | 0M-07 | CA63 (parte) | **novo** | I45: parte lógica do seletor que substitui o do Odin (filtro, categorias, busca, tipo ausente, referência compartilhada); renomeia o asmdef do Editor para `Core.Editor` |
| 0M-47 | `feat(core): popup de seleção de tipo em UI Toolkit` | 0M-41 | CA63 (parte: busca e categorias) | **novo** (2ª rodada) | I48: substitui o `AdvancedDropdown`; o popup próprio não cabia no 0M-42 |
| 0M-42 | `feat(core): drawer do seletor de tipo e uso no GAS` | 0M-47 | CA63 | **novo** | I45/I46: drawer UI Toolkit do seletor; devolve ao GAS a escolha de tipo nos 10 campos `[SerializeReference]`. 2ª rodada: abre o popup do 0M-47 |
| 0M-46 | `feat(core): atributo [ShowIf] e uso no GAS` | 0M-42 | — | **novo** | I45: único atributo de layout da lista inicial; devolve ao GAS o `ShowIf` por campo irmão |
| 0M-43 | `feat(core): interface de validação IValidatable` | 0M-07 | R32 (parte) | **novo** | I45: substitui `ISelfValidator`; precisa existir antes do primeiro validador (0M-23) |
| 0M-48 | `feat(core): Inspector base SpellBookEditor com validação no topo` | 0M-43, 0M-41 | R32, CA66 | **novo** (2ª rodada) | I48: substitui o gancho IMGUI; base dos Inspectors com botão e dos editors de uma linha |
| 0M-08 | `docs(manual): criar o manual, DocUrls, Instalação e declarar Unity 6000.0` | 0M-07 | CA4 (parte), CA37 (parte), CA42 | alterado | Cria a página Instalação (vinha do 0M-07) sem Odin; README sem Odin |
| 0M-09 | `ci(manual): publicar o manual no GitHub Pages` | 0M-08 | CA39 | igual | — |
| 0M-10 | `docs(gas): mover GAS_Documentation.md para o manual` | 0M-08 | CA40 | igual | — |
| 0M-12 | `refactor(gas)!: separar o GAS no assembly SpellBook.GAS` | 0M-10 | CA1 (parte), CA2 (parte), R22 (parte) | alterado | Asmdef do GAS sem constraint e sem DLL do Odin (`overrideReferences: true`, `[]`); a Migração ganha a linha "sem Odin" |
| 0M-13 | `test(gas): caracterizar tags do ASC e criar as factories` | 0M-12 | CA31 (parte), CA33 (parte) | igual | (asmdefs seguem a 4.1 revisada) |
| 0M-14 | `refactor(tags)!: mover as tags para o assembly SpellBook.Tags` | 0M-13 | CA2 (parte), CA31 (parte) | igual | (idem) |
| 0M-15 | `refactor(tags)!: renomear o namespace das tags para SpellBook.Tags` | 0M-14 | CA31 (parte) | igual | — |
| 0M-16 | `test(tags): testes do módulo Tags, HelpURL e página` | 0M-15 | CA31, CA32 (parte) | igual | — |
| 0M-17 | `feat(time): ITimeSource, fontes escalada, não escalada e manual` | 0M-12 | CA44 (parte), CA33 (parte) | igual | (idem) |
| 0M-18 | `feat(time): grupo pausável e reset sem domain reload` | 0M-17 | CA44, CA45, CA46 (parte) | igual | — |
| 0M-19 | `feat(gas)!: Tick, autoTick e fonte de tempo no ASC` | 0M-18, 0M-15 | CA7, CA8, CA9, CA11, CA46, N2, N3 | igual | — |
| 0M-20 | `feat(core): SpawnerAsset com implementação por Instantiate` | 0M-12 | CA27, CA33 (parte) | igual | — |
| 0M-21 | `refactor(gas)!: ações de spawn e projétil usam SpawnerAsset` | 0M-20, 0M-19 | CA29, CA62, CA30 | igual | — |
| 0M-22 | `refactor(gas): esperas do GAS pelo relógio do ASC` | 0M-21 | CA10, CA32 (parte) | igual | — |
| 0M-23 | `refactor(pooling)!: módulo SpellBook.Pooling e PooledSpawnerAsset` | 0M-21, 0M-43 | CA28, CA35 (parte), CA6 (parte) | alterado | `IValidatable` no lugar de `ISelfValidator`; sai a verificação do `SelfValidationResult` e o plano B |
| 0M-24 | `refactor(core)!: MathTools como classe estática no Core` | 0M-12 | CA43 | igual | — |
| 0M-25 | `refactor(events)!: remover os três sistemas de eventos antigos` | 0M-12, 0M-24 | CA25, R22 | igual | — |
| 0M-26 | `feat(events): EventBus<T> e registro limpo por sessão` | 0M-25, 0M-17 | CA15, CA16, CA17, CA19 (bus), CA2 (parte) | igual | (idem) |
| 0M-27 | `feat(events): EventChannel e canais Void, Int e Float` | 0M-26 | CA15, CA16, CA18, CA19, CA33 (parte) | alterado | Sai o `[Button]`/`[EnableIf]` do runtime; o botão Raise vai para o 0M-45 |
| 0M-28 | `feat(events): canais Bool, String, Vector3 e GameObject` | 0M-27 | CA12 | igual | — |
| 0M-29 | `feat(events): EventListener com respostas e reações` | 0M-28, 0M-41 | CA13, CA14 | alterado | `[TypePicker]` nas listas; escape em classe aninhada (foldout nativo) no lugar do `[FoldoutGroup]` |
| 0M-30 | `feat(events): reações SetActive, Log, Toggle e RaiseChannel` | 0M-29, 0M-43 | CA47, CA48, CA49, CA50, CA58 (runtime) | alterado | `Validate(ValidationResult)`; `[TypeCategory]` nas reações (I-k) |
| 0M-31 | `feat(events): reação Invoke com delay` | 0M-30, 0M-18 | CA51, CA59, CA60 | alterado | `[TypePicker]` na lista interna; `[TypeCategory]`; validação por `ValidationResult` |
| 0M-32 | `feat(events): EventEmitter e payload próprio de ponta a ponta` | 0M-31 | CA20, CA22 | alterado | Canal dentro da emissão; sem `[HideReferenceObjectPicker]`/`[OnValueChanged]`/botão (vão para o 0M-45); `IValidatable` |
| 0M-45 | `feat(events): Inspectors do canal e do emissor e drawer de emissão em UI Toolkit` | 0M-32, 0M-48 | R12, R19, CA22 (parte) | **novo** | I45/I46: substitui `[Button]`, `[EnableIf]` e `[OnValueChanged]` do Odin; cria o `SpellBook.Events.Editor`. 2ª rodada: os dois Inspectors herdam de `SpellBookEditor`; o asmdef referencia `Core.Editor` |
| 0M-33 | `feat(events): validação de ouvintes e respostas` | 0M-32, 0M-43 | CA35 (parte) | alterado | `IValidatable`; passa a depender do 0M-43, não do 0M-23 |
| 0M-34 | `feat(events): seletor de canal filtrado por payload` | 0M-33, 0M-45 | CA21, CA58 (seletor) | alterado | Drawer em UI Toolkit com `HelpBox`; o asmdef `Events.Editor` já existe (0M-45) |
| 0M-35 | `feat(events): Event Debugger` | 0M-34 | CA23 | alterado | `EditorWindow.CreateGUI` (UI Toolkit), não `OdinEditorWindow` |
| 0M-36 | `feat(events): Find references de canal` | 0M-35 | CA24 | alterado | Botão do debugger e menu de contexto em UI Toolkit; papel "emite" lido da `EventEmission` (canal dentro dela) |
| 0M-37 | `feat(gas-bridges): RaiseEventAction` | 0M-32, 0M-22, 0M-43 | CA52, CA61, CA35 (parte), CA2 | alterado | `IValidatable` em `AbilityAction` e `GameplayAbility`; emissão com canal dentro (drawer do 0M-45) |
| 0M-38 | `feat(core): validar projeto por menu e por teste` | 0M-37, 0M-33 | CA34, CA35, R32, R33, R34, CA64 | alterado | `IValidatable`; janela UI Toolkit; erro de tipo ausente; o rename do asmdef sai (foi para o 0M-41). 2ª rodada: sai o gancho do Inspector (as mensagens no Inspector vêm do `SpellBookEditor`) |
| 0M-39 | `docs(gas): HelpURL nos tipos restantes e teste de HelpURL` | 0M-38 | CA36, CA37 | igual | — |
| 0M-49 | `feat(editor): Inspectors de uma linha para Core, Time, Tags e Pooling` | 0M-39, 0M-48 | R32 | **novo** (2ª rodada) | I48: todo tipo do pacote usa o `SpellBookEditor`; cria `Time.Editor`, `Tags.Editor` e `Pooling.Editor` |
| 0M-50 | `feat(editor): Inspectors de uma linha para GAS e Events e cobertura completa` | 0M-49 | R32 | **novo** (2ª rodada) | I48: completa a cobertura; cria `GAS.Editor` |
| 0M-40 | `refactor(core)!: Core em Runtime/Core e verificação final da estrutura` | 0M-50 | CA1, CA32, CA54, CA6, N4 | alterado | Lista final de asmdefs sem `Setup.Editor` e com `Core.Editor`, `Events.Editor`, `Time.Editor`, `Tags.Editor`, `Pooling.Editor` e `GAS.Editor`; depende do 0M-50 |

Ordem de execução linear: 01 → 02 → 03 → 04 → 05 → 06 → **44** → 07 → 11 → **41** → **47** → **42** → **46** → **43** → **48** → 08 → 09 → 10 → 12 → 13 → … → 31 → 32 → **45** → 33 → 34 → 35 → 36 → 37 → 38 → 39 → **49** → **50** → 40.

Os ids novos não seguem a ordem de execução (0M-47 vem antes do 0M-42), porque o 0M-42 já tinha sido divulgado na primeira rodada.

Por que essa ordem no começo:
- o Core só compila no host sem Odin depois do 0M-44, e nenhum teste roda antes disso (com erro de compilação, a Unity não recarrega o domínio). Por isso o 0M-44 é o primeiro ticket depois do host;
- a guarda "nenhum Sirenix" vem logo depois, no 0M-07, junto com o primeiro asmdef de teste;
- o CI (0M-11) vem logo depois do 0M-07, primeiro ticket com teste: o host está na 6000.3.13f1 (D5), e só o job 6000.0 do CI prova a versão mínima para os tickets com risco de API (0M-41, 0M-47, 0M-42, 0M-46, 0M-48);
- o seletor (0M-41, 0M-42) e o `[ShowIf]` (0M-46) entram antes de qualquer ticket que crie ou mexa em `[SerializeReference]`, e antes do 0M-12 mover o GAS de assembly;
- a validação (0M-43) entra antes do primeiro validador (0M-23);
- o `SpellBookEditor` (0M-48) entra logo depois, para os Inspectors do Events (0M-45) herdarem dele;
- os editors de uma linha (0M-49, 0M-50) ficam para o fim, quando todos os tipos existem, e o teste de cobertura fecha a regra antes da verificação final (0M-40).

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
- Depende de: 0M-02. CAs: nenhum. Testes: como o 0M-03, e mais um critério (revisão sem Odin): nenhum dos 8 arquivos cita `Odin`, `Sirenix` ou `Assets/_Project`. Hoje `unity-explorer.md:13` cita os três. Manual: nenhuma. Tamanho: ~80 linhas de texto, 8 arquivos.

### 0M-05 — `chore(host): ignorar artefatos do host no git`
- **Alterado (revisão sem Odin):** saem as entradas do Sirenix (I45). O título perde "e o Odin".
- Objetivo: nenhum arquivo gerado pelo host entra no git.
- Arquivos: `.gitignore` (acrescenta `HostProject~/Library/`, `Logs/`, `Temp/`, `UserSettings/`, `obj/`, `HostProject~/ProjectSettings/*` com exceção `!HostProject~/ProjectSettings/ProjectVersion.txt`). Nenhuma entrada cita `Sirenix`.
- Depende de: 0M-02. Liberado pela decisão I41 (host em `HostProject~/`).
- Host antigo na raiz: `/Assets/`, `/Packages/` e `/ProjectSettings/` já estão no `.gitignore` (commit c19b627), então não há arquivo versionado para mover ou apagar. As entradas da raiz continuam no `.gitignore`. A cópia local do usuário já foi movida por ele para fora do repositório (`SpellBook-host-antigo/`, emenda de 2026-10-29 da ADR 0004), e nenhum ticket a toca. Critério extra: `git ls-files Assets Packages ProjectSettings` não lista nada.
- CAs: CA55 (parte: `.gitignore` do host).
- Testes: critério verificável: `git check-ignore` devolve ignorado para `HostProject~/Library/x` e `HostProject~/ProjectSettings/EditorSettings.asset`, e não ignorado para `HostProject~/Packages/manifest.json` e `HostProject~/ProjectSettings/ProjectVersion.txt`; `.gitignore` não contém `Sirenix`.
- Manual: nenhuma. Tamanho: ~15 linhas, 1 arquivo.

### 0M-06 — `chore(host): criar projeto host de testes com o pacote em testables`
- Objetivo: um projeto Unity 6000.3.13f1 (D5) que compila o pacote por `file:` e lista os testes dele.
- **Alterado (revisão sem Odin):** o host não tem o Odin. O critério muda, porque o pacote ainda tem os 8 arquivos com `Sirenix` e não compila nesse host até o 0M-44.
- Arquivos: `HostProject~/Packages/manifest.json` (`com.spellbook` por `file:` apontando para a raiz do repositório, `com.unity.test-framework` fixado, `testables: ["com.spellbook"]`, módulos mínimos da Unity), `HostProject~/ProjectSettings/ProjectVersion.txt` (6000.3.13f1, D5), `HostProject~/Assets/.gitkeep` (vazio; a Unity não abre projeto sem `Assets/` — "Couldn't set project path" — e o git não versiona pasta vazia; arquivo oculto, a Unity não gera `.meta`), `Documentation~/README.md` (seção "Projeto host": abrir e criar a trava; nenhuma instrução de Odin).
- Caminho do `file:` (decisão I41 escreve `file:../`): a Unity resolve o relativo a partir de `HostProject~/Packages/`, então o valor esperado é `file:../..`. O red do ticket é o Package Manager não mostrar `com.spellbook` com o caminho errado; fica o valor que resolver para a raiz (ver D1).
- Depende de: 0M-05. Liberado pela decisão I41.
- CAs: CA55.
- Testes: sem teste de código (ainda não há asmdef de teste). Critérios:
  - antes de abrir o host, a raiz do repositório não contém `Assets/`, `Library/`, `Packages/` nem `ProjectSettings/` (risco 34). Se contiver, o ticket para e avisa o Controlador; a Fábrica não move nem apaga;
  - com a trava, a Unity abre o host (em batchmode, ou ignorando o Safe Mode);
  - o Package Manager mostra `com.spellbook` como local;
  - os **únicos** erros de compilação são de símbolos do Odin (`CS0246`/`CS0234`/`CS0103`: tipos e namespace `Sirenix`, e enums e constantes do Odin como `ObjectFieldAlignment`) nos 8 arquivos da seção 1.1. Na 6000.3.13f1 saem 813: `CS0246` nos 8 e 15 `CS0103` em `GameplayAbility`, `AttributeDefinition`, `ExpressionFormula` e `GameplayEffect`. A lista exata vai na descrição do PR e é o red do 0M-44;
  - `ProjectVersion.txt` com `m_EditorVersion: 6000.3.13f1`.

  **Inferido:** I-g. **Desvio:** D5.
- Manual: nenhuma (documento interno).
- Tamanho: ~40 linhas, 4 arquivos.

### 0M-44 — `refactor(gas)!: remover o Odin dos 8 arquivos do runtime` (novo)
- **Novo (revisão sem Odin, I45).** Vem logo depois do 0M-06, porque nenhum teste roda enquanto o Core não compilar no host sem Odin.
- Objetivo: o pacote compila numa Unity sem Odin, sem mudar dado serializado. O ticket verifica no host (6000.3.13f1, D5); a 6000.0 fica com o CI (0M-11).
- Arquivos (8):
  - `Runtime/GAS/Abilities/GameplayAbility.cs`:
    - sai o `using Sirenix.OdinInspector`;
    - `TabGroup`/`BoxGroup`/`HorizontalGroup`/`VerticalGroup` viram `[Header("Identidade")]`, `[Header("Ativação")]`, `[Header("Tags")]` e `[Header("Execução")]`;
    - o texto do `InfoBox` do custo vai para `[Tooltip]`;
    - saem `PreviewField`, `HideLabel`, `InlineEditor`, `ListDrawerSettings` e `Searchable`;
    - o `[FormerlySerializedAs]` fica (sai no 0M-15).
  - `Runtime/GAS/Effects/GameplayEffect.cs`:
    - abas e grupos viram `[Header]`;
    - `Title` vira `[Header]`;
    - o `InfoBox` do período vai para `[Tooltip]`;
    - os dois `ShowIf("@Period > 0")` saem, e os campos ganham o `[Tooltip]` "Usado só quando Period > 0";
    - o `ShowIf` da duração sai (volta no 0M-46).
  - `Runtime/GAS/Attributes/AttributeDefinition.cs`: grupos, `PreviewField`, `LabelWidth`, `EnumPaging`, `InlineEditor` e `Required` saem; o `[Tooltip]` diz "Obrigatório quando Type = Derived"; o `ShowIf` volta no 0M-46.
  - `Runtime/GAS/Attributes/ModifierValueSource.cs`: os 3 `Sirenix.OdinInspector.ShowIf` saem (voltam no 0M-46).
  - `Runtime/GAS/Abilities/AbilityDescription.cs`: `HideLabel`, `HorizontalGroup`, `FoldoutGroup`, `TableList` e `ListDrawerSettings` saem; o `ShowIf("UseColor")` volta no 0M-46.
  - `Runtime/GAS/Effects/Actions/CommonEffectActions.cs`: sai o `[Required]`.
  - `Runtime/GAS/Attributes/Formulas/ExpressionFormula.cs`: o `InfoBox` vai para `[Tooltip]`; saem o campo `_sanitizedPreview`, o `UpdatePreview` e os atributos de layout (perda aceita na 0026, pendente do usuário).
  - `Runtime/GAS/Attributes/Formulas/SimpleAttributeFormula.cs`: `BoxGroup` vira `[Header]`; o resto do layout sai.
- Depende de: 0M-06.
- CAs: CA5 (S, com o texto novo: host **sem** Odin).
- Testes: **red de compilação**, a única exceção da 0-M (0004, emenda). Ainda não há asmdef de teste, e com erro de compilação a Unity não recarrega o domínio.
  - Red: a lista de erros de símbolos do Odin (`CS0246`/`CS0234`/`CS0103`) registrada no PR do 0M-06.
  - Green, todos estes:
    - o host compila sem erro e sem aviso vindo do SpellBook;
    - busca textual: nenhum arquivo de `Runtime/` e `Editor/` contém `Sirenix` (a guarda automática vem no 0M-07);
    - nenhum campo foi renomeado nem teve o tipo trocado (o diff mostra só atributos e `using`), então o formato serializado não muda.
- Manual: ainda não existe (nasce no 0M-08). A linha da Migração ("o pacote não depende mais do Odin; perdas no Inspector do GAS") entra no 0M-12, que cria a página. A quebra fica registrada na seção 7.
- Tamanho: ~150 linhas de diff (quase tudo remoção), 8 arquivos.

### 0M-07 — `test(core): asmdef de testes do Core e regras de estrutura sem Odin`
- **Alterado (revisão sem Odin).** Antes era "desligar o pacote sem Odin e avisar com diálogo". O `SpellBook.Setup.Editor`, o `OdinMissingNotice`, o `defineConstraints` e o CA41 foram cancelados pela I47. A página Instalação passa para o 0M-08. Ficam a infraestrutura de teste e as guardas de estrutura, e entra a guarda "nenhum Sirenix", que é o teste de estrutura da remoção do 0M-44.
- Objetivo: primeiro asmdef de teste; regras de asmdef da 0026 cobradas por teste.
- Arquivos:
  - `Runtime/SpellBook.Core.asmdef` (`overrideReferences: true`, `precompiledReferences: []`);
  - `Editor/SpellBook.Editor.asmdef` (idem);
  - `Tests/Core/EditMode/SpellBook.Core.Tests.EditMode.asmdef`;
  - `Tests/Core/EditMode/Structure/PackageFiles.cs` (lê asmdefs e arquivos do pacote pelo `resolvedPath` de `PackageInfo.FindForAssembly`);
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs`;
  - `Tests/Core/EditMode/Structure/RuntimeReferencesTests.cs`;
  - `Tests/Core/EditMode/Structure/NoOdinTests.cs`.
- Depende de: 0M-44.
- CAs: CA65 (T, substitui o CA53), CA3 (T, guarda, sem as partes do Odin), CA2 (T, guarda).
- Testes:
  - Red: `AsmdefRules_AllOverrideReferences_PrecompiledInAllowList` falha (os dois asmdefs têm `overrideReferences: false`).
  - Guardas: `NoOdin_NoAsmdefReferencesSirenixOrOdinSymbol` e `NoOdin_NoSourceOrPackageFileMentionsSirenix`. O red é mostrado com um `.cs` temporário com `using Sirenix.OdinInspector;` e com um asmdef temporário com `ODIN_INSPECTOR`, sem commit.
  - Guardas: CA2 e CA3.
- Manual: nenhuma (sem mudança visível ao usuário).
- Tamanho: ~220 linhas, 7 arquivos.

### 0M-11 — `ci(test): rodar as suítes na 6000.0 e na última 6000.x com GameCI`
- **Alterado (revisão sem Odin):** **desbloqueado**, porque a P21 perdeu o objeto (I45). Saem o script e os secrets do Odin e o passo de importação extra.
- **Adiantado (D5, aprovado em 2026-10-29):** vem logo depois do 0M-07, para que os tickets com risco de API da 6000.0 (0M-41, 0M-47, 0M-42, 0M-46, 0M-48) já tenham o job 6000.0 no PR. A seção "Compatibilidade" da Instalação passou para o 0M-08, porque a página ainda não existe neste ponto.
- Arquivos: `.github/workflows/tests.yml`.
- Depende de: 0M-07 (existe ao menos um asmdef de teste).
- Desenho:
  - matriz `unityVersion: [6000.0.x, 6000.y.z]` (I-j), passada explicitamente ao runner. Nunca `auto`, que leria o `ProjectVersion.txt` do host (6000.3.13f1, D5) e deixaria a 6000.0 sem teste;
  - `game-ci/unity-test-runner` com `projectPath: HostProject~` e `testMode: all`;
  - secrets só da Unity (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`);
  - falha se o número de testes executados for 0;
  - artefatos por versão.
- CAs: CA56 (E, com o texto novo: nenhum arquivo do Odin e nenhuma referência ao Odin nos workflows), CA57 (S), N1.
- Testes: critérios:
  - um PR dispara as duas versões, cada uma com resultado separado, e o log de cada job mostra o Editor da versão da matriz (o da 6000.0 não é o 6000.3.13f1 do host);
  - `grep -i "odin\|sirenix"` em `.github/` não encontra nada.
- Manual: nenhuma (a seção "Compatibilidade" é do 0M-08).
- Tamanho: ~85 linhas, 1 arquivo.

### 0M-41 — `feat(core): catálogo do seletor de tipo e atributos TypePicker/TypeCategory` (novo)
- **Novo (revisão sem Odin, I45).** É a parte lógica do seletor, sem UI. O drawer vem no 0M-42.
- Objetivo: decidir, por função pura e testada, quais tipos o seletor oferece para um campo `[SerializeReference]`, como agrupá-los e como tratar tipo ausente e referência compartilhada (0026, item 4; seção 4.10).
- Arquivos:
  - `Runtime/Core/Inspector/TypePickerAttribute.cs`;
  - `Runtime/Core/Inspector/TypeCategoryAttribute.cs`;
  - `Editor/SpellBook.Editor.asmdef` → `Editor/Core/SpellBook.Core.Editor.asmdef` (renomeado e movido com o `.meta`, GUID mantido; o rename saiu do 0M-38);
  - `Editor/FolderStructureCreator.cs` → `Editor/Core/FolderStructureCreator.cs` (movido com o `.meta`);
  - `Editor/Core/Inspector/TypePickerCatalog.cs`;
  - `Tests/Core/EditMode/SpellBook.Core.Tests.EditMode.asmdef` (+`SpellBook.Core.Editor`);
  - `Tests/Core/EditMode/Inspector/TypePickerCatalogTests.cs`, com os tipos de teste no mesmo arquivo: base abstrata, concreto, concreto com `[TypeCategory]`, genérico, abstrato, sem `[Serializable]`, sem construtor sem parâmetros e um `ScriptableObject`.
- Depende de: 0M-07.
- CAs: CA63 (T, parte: filtro, categorias e busca).
- Testes red (o tipo `TypePickerCatalog` não existe):
  - `Candidates_ExcludeAbstractGenericUnityObjectNonSerializableAndNoDefaultCtor`;
  - `Candidates_IncludeConcreteBaseItself`;
  - `Category_FromTypeCategory_RootWhenMissing`;
  - `Summary_FromTypeCategory`;
  - `Filter_ByDisplayNameTypeNameAndCategory_CaseInsensitive`;
  - `Assign_CreatesInstanceOfChosenType_NullClears` (SO de teste em memória + `SerializedObject`);
  - `EnsureUnique_SharedReferenceInList_BecomesIndependentCopy` (a mesma instância posta duas vezes na lista por código; depois da chamada, mudar um elemento não muda o outro);
  - `MissingType_IsReported_DataKeptUntilClear`. Um `.asset` em YAML com uma referência a uma classe inexistente é escrito em `Assets/__SpellBookTestTemp` (risco 23). O teste confirma que a 6000.0 guarda os dados (risco 28), que `TryGetMissingType` devolve o assembly e a classe, e que os dados só somem depois de `ClearManagedReferenceWithMissingType`.
- Manual: nenhuma página neste ticket (nenhum campo usa o seletor ainda). A página **Inspector** nasce no 0M-42 com o drawer.
- Tamanho: ~380 linhas (atributos ~30, catálogo ~150, testes ~200), 7 arquivos.

### 0M-47 — `feat(core): popup de seleção de tipo em UI Toolkit` (novo, 2ª rodada)
- **Novo (I48).** Substitui o `AdvancedDropdown`. Saiu do 0M-42, porque o drawer, o popup e o uso no GAS juntos dariam ~540 linhas.
- Objetivo: lista de tipos com busca e categorias, em UI Toolkit, sem IMGUI (seção 4.10, "Popup do seletor").
- Arquivos:
  - `Editor/Core/Inspector/TypePickerTree.cs` (modelo puro);
  - `Editor/Core/Inspector/TypePickerPopup.cs` (`EditorWindow`, `ShowAsDropDown`, `CreateGUI`);
  - `Tests/Core/EditMode/Inspector/TypePickerTreeTests.cs`.
- Depende de: 0M-41.
- CAs: CA63 (parte: busca e categorias).
- Testes red (o tipo `TypePickerTree` não existe):
  - `Build_GroupsByCategoryPath_NoneFirst_CategoriesBeforeTypes`;
  - `Build_TypeWithoutCategory_AtRoot`;
  - `Parent_ReturnsUpperLevel_RootHasNoParent`;
  - `Search_ReturnsFlatMatchesWithPath`;
  - `Search_EmptyQuery_ReturnsEmpty`.

  A janela não tem teste automatizado (risco 33); ela é verificada no cenário do host do 0M-42. O red confirma `ShowAsDropDown`, `ToolbarSearchField` e `ListView` na 6000.0 (risco 27).
- Manual: nenhuma página neste ticket (o popup só aparece pelo drawer do 0M-42, que traz a página).
- Tamanho: ~320 linhas (modelo ~100, janela ~140, testes ~80), 3 arquivos.

### 0M-42 — `feat(core): drawer do seletor de tipo e uso no GAS` (novo)
- **Novo (revisão sem Odin, I45/I46).** Na 2ª rodada (I48), o botão abre o `TypePickerPopup` do 0M-47 em vez do `AdvancedDropdown`. Depois de tirar a lista do ticket, o tamanho continua dentro do limite.
- Objetivo: os campos `[SerializeReference]` voltam a ser editáveis pelo Inspector: escolher, trocar, limpar e ver tipo ausente.
- Arquivos:
  - `Editor/Core/Inspector/TypePickerDrawer.cs` (UI Toolkit, seção 4.10; abre o `TypePickerPopup`);
  - `Tests/Core/EditMode/Inspector/TypePickerUsageTests.cs`;
  - `Runtime/GAS/Abilities/GameplayAbility.cs`, `Runtime/GAS/Effects/GameplayEffect.cs`, `Runtime/GAS/Abilities/AbilityDescription.cs`, `Runtime/GAS/Abilities/Actions/FlowActions.cs` e `Runtime/GAS/Abilities/Actions/SpawnProjectileAction.cs` (`[TypePicker]` nos 10 campos da seção 1.1);
  - `Documentation~/manual/inicio/inspector.md`.
- Depende de: 0M-47.
- CAs: CA63.
- Testes:
  - Red: `EverySerializeReferenceFieldInRuntime_HasTypePicker_ExceptEventEmission` falha. O teste percorre por reflexão os assemblies `SpellBook.*` carregados que não são de teste, sem referência de compilação ao GAS. Os 10 campos do GAS não têm o atributo. A regra vale para o pacote inteiro daqui em diante.
  - A UI não tem teste automatizado, porque o bind do UI Toolkit precisa de painel. O cenário no host (seção 5) cobre: adicionar uma `WaitAction` numa habilidade pelo seletor, buscar "wait", salvar, reabrir e conferir; duplicar o elemento e editar a cópia sem mudar o original; o `OnGUI` mostra o `HelpBox`.
  - O red também confirma que `CreatePropertyGUI` com `TrackPropertyValue` funciona na 6000.0 (risco 27).
  - No cenário do host entram também: o popup abre ancorado no botão, a busca filtra, a navegação por categoria funciona e o teclado segue I-o.
- Manual: **Inspector** (conteúdo real):
  - o seletor, o popup, a busca e as categorias;
  - "Tipo não encontrado" e como recuperar;
  - `[MovedFrom]` para tipos próprios;
  - limite de um drawer por campo;
  - Inspectors IMGUI e de terceiros (risco 26).
- Tamanho: ~220 linhas (drawer ~150, teste ~50, GAS ~20), 8 arquivos.

### 0M-46 — `feat(core): atributo [ShowIf] e uso no GAS` (novo)
- **Novo (revisão sem Odin, I45).** É o único atributo de layout da lista inicial da 0026.
- Objetivo: campos do GAS que só valem para um modo ficam escondidos nos outros modos, como antes com o Odin.
- Arquivos:
  - `Runtime/Core/Inspector/ShowIfAttribute.cs`;
  - `Editor/Core/Inspector/ShowIfDrawer.cs` (inclui `ShowIfRule`);
  - `Tests/Core/EditMode/Inspector/ShowIfRuleTests.cs`;
  - `Runtime/GAS/Attributes/ModifierValueSource.cs` (3 campos);
  - `Runtime/GAS/Attributes/AttributeDefinition.cs` (`Formula` quando `Derived`);
  - `Runtime/GAS/Effects/GameplayEffect.cs` (`Duration` quando `HasDuration`);
  - `Runtime/GAS/Abilities/AbilityDescription.cs` (`Color` quando `UseColor`);
  - `Documentation~/manual/inicio/inspector.md`.
- Depende de: 0M-42.
- CAs: nenhum do PRD (comportamento de Inspector da 0026).
- Testes red (o tipo `ShowIfRule` não existe):
  - `EnumSibling_Equal_Visible_Different_Hidden`;
  - `BoolSibling_True_Visible_False_Hidden`;
  - `MissingSibling_Visible_AndWarnsOnce`;
  - `NestedStructInList_UsesSiblingInSameElement` (o `ModifierValueSource` dentro de uma lista).

  O red também decide entre as formas (a) e (b) do drawer (4.10, risco 25). Cenário no host: trocar `Type` do `ModifierValueSource` mostra só o campo do modo.
- Manual: Inspector, seção **ShowIf** (campo irmão `bool`/`enum`, sem expressão, não esconde listas).
- Tamanho: ~220 linhas, 8 arquivos.

### 0M-43 — `feat(core): interface de validação IValidatable` (novo)
- **Novo (revisão sem Odin, I45).** Substitui o `ISelfValidator` e o plano B da versão anterior.
- Arquivos:
  - `Runtime/Core/Validation/ValidationMessage.cs` (com `ValidationSeverity`);
  - `Runtime/Core/Validation/ValidationResult.cs`;
  - `Runtime/Core/Validation/IValidatable.cs`;
  - `Tests/Core/EditMode/Validation/ValidationResultTests.cs`.
- Depende de: 0M-07.
- CAs: R32 (parte: a interface). As regras do R35 vêm nos tickets dos tipos.
- Testes red (os tipos não existem):
  - `AddError_StoresSeverityMessageContextAndPath`;
  - `HasErrors_FalseWithOnlyWarnings_TrueWithError`;
  - `AddError_EmptyMessage_Throws`;
  - `Messages_KeepInsertionOrder`.
- Manual: nenhuma página neste ticket. A página **Validador** (0M-38) documenta o `IValidatable` para quem estende o pacote.
- Tamanho: ~130 linhas, 4 arquivos.

### 0M-48 — `feat(core): Inspector base SpellBookEditor com validação no topo` (novo, 2ª rodada)
- **Novo (I48).** Substitui o gancho IMGUI `Editor.finishedDefaultHeaderGUI`, que estava previsto no 0M-38.
- Objetivo: um Inspector base em UI Toolkit que mostra o Inspector padrão, as mensagens de `IValidatable` no topo e pontos de extensão para os botões dos módulos (0026, item 3a; seção 4.10).
- Arquivos:
  - `Editor/Core/Inspector/SpellBookEditor.cs`;
  - `Tests/Core/EditMode/Inspector/SpellBookEditorTests.cs`. No mesmo arquivo ficam um SO de teste `IValidatable` com o editor de uma linha dele, um SO não validável e um editor de teste que sobrescreve `BuildAfterDefault`;
  - `Documentation~/manual/inicio/inspector.md` (seção **Validação no Inspector**).
- Depende de: 0M-43, 0M-41.
- CAs: R32 (o texto novo do PRD), CA66 (S: mensagem no topo do Inspector igual à da janela).
- Testes red (o tipo `SpellBookEditor` não existe):
  - `CollectMessages_InvalidTarget_ReturnsError`;
  - `CollectMessages_ValidTarget_ReturnsNone`;
  - `CollectMessages_NonValidatable_ReturnsNone`;
  - `CollectMessages_ExceptionInValidate_BecomesError`;
  - `CreateInspectorGUI_ValidationContainerFirst_WithOneHelpBoxPerMessage`;
  - `CreateInspectorGUI_NonValidatable_ContainerHidden`;
  - `CreateInspectorGUI_AfterDefaultExtensionIsLast`.

  Os testes conferem a estrutura, sem bind (risco 33). Cenário no host: depois do 0M-49, um `PooledSpawnerAsset` com `initialSize` negativo mostra o erro no topo, e o erro some ao corrigir o valor.
- Manual: Inspector, seção **Validação no Inspector** (onde aparece, botão Revalidar, como um Inspector próprio herda de `SpellBookEditor`).
- Tamanho: ~250 linhas (editor ~110, testes ~140), 3 arquivos.

### 0M-08 — `docs(manual): criar o manual, DocUrls, Instalação e declarar Unity 6000.0`
- **Alterado (revisão sem Odin):** passa a **criar** a página Instalação, que vinha do 0M-07, já sem Odin. O README perde o "Odin obrigatório".
- Objetivo: esqueleto do site, constante única de URL, `package.json` e README.
- Arquivos: `Documentation~/mkdocs.yml`, `Documentation~/requirements-docs.txt`, `Documentation~/manual/index.md`, `Documentation~/manual/inicio/instalacao.md` (criada, com âncoras), `Runtime/Core/DocUrls.cs`, `package.json` (`unity`, `documentationUrl`; o `version` não muda), `README.md` (requisito: só Unity 6000.0 ou mais nova, sem dependência de terceiros; link do site), `Tests/Core/EditMode/DocUrlsTests.cs`.
- Depende de: 0M-07.
- CAs: CA4 (parte: `unity` e `documentationUrl`), CA37 (parte: constante única com o valor certo), CA42 (E).
- Testes red: `DocUrls_BaseMatchesSite` e `DocUrls_BaseMatchesPackageDocumentationUrl` falham (tipo não existe; `package.json` tem example.com); `Package_DeclaresUnity6000_0` falha (2021.3).
- Decisão registrada no ticket: Zensical ou MkDocs Material (0025), com a versão travada.
- Manual:
  - **O que é**: o pacote, os módulos e para quem é;
  - **Instalação**, com conteúdo real: Unity 6000.0 ou mais nova; instalação por URL git; nenhuma dependência de terceiros; o Inspector do pacote é UI Toolkit (link para a página Inspector quando ela existir, 0M-42); seção **Compatibilidade** com as versões testadas no CI, copiadas da matriz do `tests.yml` (vinha do 0M-11, adiantado pelo D5). Tem âncoras `{#...}`. **Não cita o Odin** (a guarda `NoOdinTests` não lê o manual; o Examinador confere).

  Verificação: `mkdocs build --strict` local passa.
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

### 0M-12 — `refactor(gas)!: separar o GAS no assembly SpellBook.GAS`
- Objetivo: GAS fora do Core; o Core deixa de depender do GAS; fim do evento global do ASC (R22, R22a).
- **Alterado (revisão sem Odin):** o asmdef do GAS segue a 4.1 revisada, sem constraint e sem DLL do Odin. A Migração ganha a linha "sem Odin" do 0M-44.
- Arquivos: `Runtime/GAS/SpellBook.GAS.asmdef` (refs Core; `overrideReferences: true`, `precompiledReferences: []`), `Runtime/Systems/Events/GAS_Events.cs` (apagado), `Runtime/GAS/Core/AbilitySystemComponent.cs` (remove `:217`), `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs`, `Documentation~/manual/inicio/conceitos.md`, `Documentation~/manual/regras/migracao-2-para-3.md`, `Documentation~/manual/modulos/gas.md` (tira a frase do evento global).
- Depende de: 0M-10 (a página GAS precisa existir).
- CAs: CA1 (parte), CA2 (parte), R22 (parte), R22a.
- Testes red: `AsmdefRules_GasAssemblyExists` e `AsmdefRules_CoreDoesNotReferenceGas` falham (não existe asmdef do GAS).
- Manual: **Conceitos** (mapa de módulos atual e alvo da 0-M; regra de dependência); **Migração 2.x → 3.0** (primeiras linhas: o pacote não depende mais do Odin, os assets continuam válidos e há perdas no Inspector do GAS (seção 7); assembly do GAS; fim do `AbilityActivatedEvent` e do `AbilitySlotUpdateEvent`, usar `OnAbilityActivated`).
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
- **Alterado (revisão sem Odin):** o `PooledSpawnerAsset` implementa `IValidatable` (0M-43). Saem a verificação do `SelfValidationResult` e o plano B.
- Depende de: 0M-21, 0M-43.
- CAs: CA28, CA35 (parte: regra do `PooledSpawnerAsset`), CA6 (parte), CA32 (parte).
- Testes red: `Pooled_NoPoolManager_SpawnsWithoutException`, `Pooled_ReleaseThenSpawn_ReusesSameInstance`, `Pooled_ReleaseNonPooled_Destroys` e `Pooled_NegativeInitialSize_IsValidationError` falham (o tipo não existe). O último chama `Validate(new ValidationResult(asset))` e confere um erro com `PropertyPath == "initialSize"`.
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
- **Alterado (revisão sem Odin):** o runtime não tem mais botão (`[Button]`/`[EnableIf]`). Fica só o campo `debugPayload`; o botão Raise é desenhado pelo `EventChannelEditor` do 0M-45.
- Arquivos: `Runtime/Events/EventChannel.cs` (base e `<T>`, hook do debugger, `debugPayload`), `Runtime/Events/Channels/VoidEventChannel.cs`, `IntEventChannel.cs`, `FloatEventChannel.cs`, `Tests/Events/EditMode/EventChannelTests.cs`, `Tests/Testing/SpellBook.Testing.asmdef` (+Events), `Tests/Testing/EventsFactory.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-26.
- CAs: CA15, CA16, CA18, CA19 (canal), CA33 (parte: canal).
- Testes red: `Channel_RaiseDeliversExactValue`, `Channel_LastPayload`, `Channel_ReentrancySafe`, `Channel_ExceptionIsolatedInEditor`, `Channel_ThousandRaises_ZeroAlloc`, `Channel_AfterSessionReset_NoListeners`, `Channel_LogRaises_WritesToConsole` falham.
- Manual: Eventos, seções **Canais** (`{#eventchannel}`, `{#voideventchannel}` …), criar pelo menu, descrição, `logRaises` (o botão Raise entra na página no 0M-45), armadilhas (asset duplicado por Addressables; ordem não garantida; `Void` vs `System.Void`).
- Tamanho: ~260 linhas, 8 arquivos.

### 0M-28 — `feat(events): canais Bool, String, Vector3 e GameObject`
- Arquivos: `Runtime/Events/Channels/BoolEventChannel.cs`, `StringEventChannel.cs`, `Vector3EventChannel.cs`, `GameObjectEventChannel.cs`, `Tests/Events/EditMode/ChannelMenuTests.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-27.
- CAs: CA12.
- Testes red: `Menu_HasExactlySevenChannels_NoComponent` falha (só 3 canais).
- Manual: Eventos, âncoras dos 4 canais.
- Tamanho: ~90 linhas, 6 arquivos.

### 0M-29 — `feat(events): EventListener com respostas e reações`
- **Alterado (revisão sem Odin):**
  - `responses` e `reactions` levam `[TypePicker]`, e a guarda do 0M-42 cobra isso;
  - o escape vira `EscapeEvent<T>` (classe serializável aninhada, foldout nativo fechado) no lugar do `[FoldoutGroup]` (4.7);
  - `BuiltInResponses` ganha `[TypeCategory("Respostas")]`.
- Arquivos: `Runtime/Events/EventListener.cs`, `Runtime/Events/EventResponse.cs` (inclui `EscapeEvent<T>`), `Runtime/Events/BuiltInResponses.cs`, `Runtime/Events/Reaction.cs`, `Tests/Events/PlayMode/SpellBook.Events.Tests.PlayMode.asmdef`, `Tests/Events/PlayMode/EventListenerTests.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-28, 0M-41.
- CAs: CA13, CA14; CA38 (S) passa a ser verificável após o deploy.
- Testes red: `Listener_ReceivesExactFloat_ReactionsInOrderThenUnityEvent`, `Listener_Disabled_DoesNotReceive_ReenabledReceives` falham.
- Manual: Eventos, seções **EventListener** `{#eventlistener}`, **Respostas** e **Payload próprio** (o struct e as duas classes; exemplo completo).
- Tamanho: ~280 linhas, 7 arquivos.

### 0M-30 — `feat(events): reações SetActive, Log, Toggle e RaiseChannel`
- **Alterado (revisão sem Odin):** `Reaction` implementa `IValidatable` (`Validate(ValidationResult)` e a sobrecarga com `payloadType`), e cada reação pronta tem `[TypeCategory]` (I-k).
- Arquivos: `Runtime/Events/Reaction.cs` (+`Validate`), `Runtime/Events/Reactions/SetActiveReaction.cs`, `ToggleComponentReaction.cs`, `LogReaction.cs`, `RaiseChannelReaction.cs`, `Tests/Events/PlayMode/ReactionTests.cs`, `Tests/Events/EditMode/ReactionValidationTests.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-29, 0M-43.
- CAs: CA47, CA48, CA49, CA50, CA58 (parte runtime), CA35 (parte: campos obrigatórios das reações).
- Testes red: `SetActive_ActivatesInactive_AndDeactivates`, `RaiseVoid_RaisesTargetOnce_ListenerReceives`, `Forward_RaisesSameTypeWithPayload_3_5`, `Forward_DifferentType_DoesNotRaise`, `Log_WritesExactMessage`, `Toggle_SetsEnabled`, `EmptyRequiredField_IsValidationError` (cada reação) falham.
- Manual: Eventos, seções **Reações prontas** com uma âncora por reação.
- Tamanho: ~300 linhas, 8 arquivos.

### 0M-31 — `feat(events): reação Invoke com delay`
- **Alterado (revisão sem Odin):** a lista interna leva `[SerializeReference, TypePicker]`; `[TypeCategory("Fluxo")]` (I-k); o teste de validação usa `ValidationResult`.
- Arquivos: `Runtime/Events/Reactions/DelayedReaction.cs`, `Runtime/Events/EventListener.cs` (esperas pendentes, `Update`, cancelamento no `OnDisable`), `Tests/Events/PlayMode/DelayedReactionTests.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-30, 0M-18.
- CAs: CA51, CA59, CA60.
- Testes red: `Delayed_1s_NotAt0_9_OnceAt1_1_InOrder` (com `ManualTimeSource`), `Delayed_ListenerDisabledDuringWait_NeverRuns`, `Delayed_NoAsset_TimeScaleZero_DoesNotRun`, `Delayed_NoReactions_IsValidationError` falham.
- Manual: Eventos, seção **Invoke com delay** `{#delayedreaction}` (fonte de tempo, cancelamento). **Inferido:** I-i.
- Tamanho: ~200 linhas, 4 arquivos.

### 0M-32 — `feat(events): EventEmitter e payload próprio de ponta a ponta`
- **Alterado (revisão sem Odin):**
  - o `EventEmitter` guarda só `[SerializeReference] EventEmission emission`, com o canal dentro da emissão (0016, emenda);
  - saem `[HideReferenceObjectPicker]`, `[OnValueChanged]` e o botão;
  - a recriação da emissão quando o canal muda e o botão Raise vão para o 0M-45;
  - `EventEmitter` e `EventEmission` implementam `IValidatable`;
  - `EventEmission` entra na lista de exceções do teste de `[TypePicker]` do 0M-42 (é criada por código, 0013).
- Arquivos: `Runtime/Events/EventEmission.cs`, `Runtime/Events/EventEmitter.cs`, `Tests/Events/EditMode/EmissionSerializationTests.cs`, `Tests/Events/PlayMode/EventEmitterTests.cs`, `Tests/Events/PlayMode/TestPayload/TestPayloadEventChannel.cs`, `Tests/Events/PlayMode/TestPayload/TestPayloadTypes.cs` (struct e `TestPayloadEventResponse`), `Tests/Core/EditMode/Inspector/TypePickerUsageTests.cs` (exceção `EventEmission`), `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-31, 0M-43.
- CAs: CA20, CA22, CA35 (parte: emissor sem canal).
- Testes red (falham):
  - `Emission_GenericRoundTrip_Float_AndCustomStruct` (risco 16; se falhar depois da implementação, aplica o plano B da 0013 e volta ao usuário);
  - `Emission_CreateFor_NullChannel_ReturnsNull`;
  - `Emitter_Raise_RaisesConfiguredPayload`;
  - `CustomPayload_EndToEnd_EmitterChannelListenerReaction`;
  - `Emitter_NoEmissionOrNoChannel_IsValidationError`.
- Manual: Eventos, seção **EventEmitter** `{#eventemitter}` (disparo por `UnityEvent` e por animação; o botão entra no 0M-45).
- Tamanho: ~250 linhas, 8 arquivos.

### 0M-45 — `feat(events): Inspectors do canal e do emissor e drawer de emissão em UI Toolkit` (novo)
- **Novo (revisão sem Odin, I45/I46).** Entrega o que o Odin dava por `[Button]`, `[EnableIf]` e `[OnValueChanged]`. Cria o asmdef `SpellBook.Events.Editor`, que antes nascia no 0M-34.
- Objetivo: R12 (botão Raise no Play, ouvintes vivos, último payload), R19 (botão do emissor) e a troca de canal na emissão, usada pelo emissor e pela `RaiseEventAction` (0016, emenda).
- Arquivos:
  - `Editor/Events/SpellBook.Events.Editor.asmdef` (Core, Time, Events, **Core.Editor**; regras da 4.1);
  - `Editor/Events/EventChannelEditor.cs` (`[CustomEditor(typeof(EventChannel), true)] : SpellBookEditor`; sobrescreve `BuildAfterDefault` com o botão "Raise" que usa o `debugPayload`, habilitado só em Play e atualizado por `EditorApplication.playModeStateChanged`, os ouvintes vivos e o último payload; a validação no topo e o Inspector padrão vêm da base);
  - `Editor/Events/EventEmitterEditor.cs` (`: SpellBookEditor`; botão "Raise" no Play em `BuildAfterDefault`);
  - `Editor/Events/EventEmissionDrawer.cs` (`[CustomPropertyDrawer(typeof(EventEmission), true)]`, com `EventEmissionEditing.SetChannel`);
  - `Tests/Events/EditMode/SpellBook.Events.Tests.EditMode.asmdef` (+Events.Editor);
  - `Tests/Events/EditMode/EventsInspectorTests.cs`;
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs` (Events.Editor só Core, Time, Events e Core.Editor; e a regra geral "um `.Editor` de módulo não referencia o `.Editor` de outro módulo, exceto o `Core.Editor`");
  - `Documentation~/manual/modulos/eventos.md`.
- **2ª rodada (I48):** os dois Inspectors herdam de `SpellBookEditor`, e o asmdef referencia o `Core.Editor`. O tamanho cai um pouco, porque o Inspector padrão e a validação vêm da base.
- Depende de: 0M-32, 0M-48.
- CAs: R12, R19, CA22 (parte: escolha do canal e do payload pelo Inspector).
- Testes red (falham, porque o asmdef e os tipos não existem):
  - `ChannelInspector_HasRaiseButton_DisabledOutsidePlay` (`Editor.CreateEditor(canal).CreateInspectorGUI()` e busca do `Button` pelo nome);
  - `EmitterInspector_HasRaiseButton_DisabledOutsidePlay`;
  - `SetChannel_DifferentPayloadType_RecreatesAsTypedEmission`;
  - `SetChannel_SamePayloadType_KeepsPayload`;
  - `SetChannel_Null_KeepsEmissionWithoutChannel`;
  - `ChannelInspector_IsSpellBookEditor`;
  - `AsmdefRules_EventsEditorReferencesOnlyCoreTimeEventsCoreEditor`;
  - `AsmdefRules_ModuleEditorsDoNotReferenceOtherModuleEditors`.
- Risco 27: o red confirma que um drawer de tipo para um campo `[SerializeReference]` de tipo abstrato é usado pela Unity na 6000.0, também com o campo nulo. Se não for, o plano B é um atributo de marcação **no módulo Events** (`[EmissionField]`), não no Core, porque a necessidade é de um módulo só (0026, item 5); isso volta ao Controlador.
- Manual: Eventos, seções **Canais** (botão Raise, ouvintes, último payload) e **EventEmitter** (escolher o canal recria o payload; botão no Play).
- Tamanho: ~280 linhas, 8 arquivos.

### 0M-33 — `feat(events): validação de ouvintes e respostas`
- **Alterado (revisão sem Odin):** `IValidatable` no lugar de `ISelfValidator`; depende do 0M-43 em vez do 0M-23. A posição na fila não muda.
- Arquivos: `Runtime/Events/EventListener.cs` (`IValidatable`, repassa a cada resposta), `Runtime/Events/EventResponse.cs` (`Validate(ValidationResult)`: sem canal, `UnityEvent` do escape sem alvo ou método, reações com o tipo do payload), `Tests/Events/EditMode/EventsValidationTests.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-32, 0M-43.
- CAs: CA35 (parte).
- Testes red: `Response_NoChannel_IsError`, `Escape_CallWithoutTarget_IsError`, `Escape_CallWithoutMethod_IsError`, `Forward_ReactionWrongType_IsError`, `ValidListener_NoIssues` falham.
- Manual: Eventos, seção **Validação**.
- Tamanho: ~150 linhas, 4 arquivos.

### 0M-34 — `feat(events): seletor de canal filtrado por payload`
- **Alterado (revisão sem Odin):** o drawer é UI Toolkit e a descrição do canal fica num `HelpBox`, não num `InfoBox` do Odin. O asmdef `Events.Editor`, a referência de teste e a regra de asmdef já vêm do 0M-45 e saem deste ticket.
- Arquivos: `Editor/Events/ChannelCatalog.cs`, `Editor/Events/ChannelPickerDrawer.cs` (`[CustomPropertyDrawer(typeof(EventChannel), true)]`; o tipo de payload vem da resposta ou da reação que contém o campo; botão que abre a lista filtrada; `HelpBox` com a descrição), `Tests/Events/EditMode/ChannelCatalogTests.cs`, `Documentation~/manual/modulos/eventos.md`.
- Depende de: 0M-33, 0M-45.
- CAs: CA21, CA58 (seletor).
- Testes red: `Filter_FloatResponse_OnlyFloatChannels`, `Filter_ForwardReaction_OnlySameType`, `Filter_Void_OnlyVoid` falham.
- Manual: Eventos, seção **Escolher o canal** (filtro e descrição ao lado).
- Tamanho: ~200 linhas, 4 arquivos.

### 0M-35 — `feat(events): Event Debugger`
- **Alterado (revisão sem Odin):** a janela é UI Toolkit (`EditorWindow.CreateGUI`, `ListView`, `ToolbarSearchField`), não `OdinEditorWindow`. O modelo não muda.
- Arquivos: `Editor/Events/EventDebuggerModel.cs`, `Editor/Events/EventDebuggerWindow.cs`, `Runtime/Events/EventChannel.cs` (ajuste do hook com emissor), `Tests/Events/EditMode/EventDebuggerModelTests.cs`, `Documentation~/manual/ferramentas/event-debugger.md`.
- Depende de: 0M-34.
- CAs: R20, CA23 (S).
- Testes red: `Model_RecordsFrameTimePayloadSender`, `Model_SearchByName`, `Model_Clear`, `Model_RingBufferKeepsLast256`, `Model_NoRecordingWhenClosed` falham.
- Cenário CA23 no host: canal "PlayerDied", botão com `EventEmitter`, `EventListener` com SetActive num painel; o debugger mostra o disparo com horário e o ouvinte.
- Manual: **Event Debugger** (abrir, ler a lista, histórico, busca, limpar). **Inferido:** I-d.
- Tamanho: ~300 linhas, 5 arquivos.

### 0M-36 — `feat(events): Find references de canal`
- **Alterado (revisão sem Odin):** o botão e a lista de resultados são UI Toolkit. O papel "emite" é lido do canal **dentro** da `EventEmission` (0016, emenda).
- Arquivos: `Editor/Events/ChannelReferenceFinder.cs`, `Editor/Events/ChannelContextMenu.cs`, `Editor/Events/EventDebuggerWindow.cs` (botão), `Tests/Events/EditMode/FindReferencesTests.cs`, `Tests/Events/EditMode/TestListenerAsset.cs` (SO de teste com uma resposta), `Documentation~/manual/ferramentas/event-debugger.md`.
- Depende de: 0M-35.
- CAs: CA24.
- Testes red: `Find_PrefabEmitterAndSoListener_ExactlyTwo_WithRoles`, `Find_ClosedSceneListener_NotIncluded` falham (risco 23).
- Manual: Event Debugger, seção **Find references** (escopo: assets e cenas abertas).
- Tamanho: ~250 linhas, 6 arquivos.

### 0M-37 — `feat(gas-bridges): RaiseEventAction`
- **Alterado (revisão sem Odin):**
  - `AbilityAction` e `GameplayAbility` implementam `IValidatable`;
  - a `RaiseEventAction` guarda só `[SerializeReference] EventEmission emission`, com o canal dentro dela, e usa o drawer do 0M-45;
  - a ação ganha `[TypeCategory("Eventos")]`.
- Arquivos: `Runtime/GAS.Bridges/SpellBook.GAS.Bridges.asmdef`, `Runtime/GAS.Bridges/RaiseEventAction.cs`, `Runtime/GAS/Abilities/AbilityAction.cs` (`IValidatable`, `virtual Validate(ValidationResult)`), `Runtime/GAS/Abilities/GameplayAbility.cs` (`IValidatable` repassando às ações, `[HelpURL]`), `Tests/GAS.Bridges/EditMode/SpellBook.GAS.Bridges.Tests.EditMode.asmdef`, `Tests/GAS.Bridges/EditMode/RaiseEventActionTests.cs`, `Tests/GAS.Bridges/PlayMode/SpellBook.GAS.Bridges.Tests.PlayMode.asmdef`, `Tests/GAS.Bridges/PlayMode/RaiseEventAbilityTests.cs`, `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs`, `Documentation~/manual/modulos/gas.md`.
- Depende de: 0M-32, 0M-22, 0M-43.
- CAs: CA52, CA61, CA35 (parte: `RaiseEventAction` sem canal), CA2 (Bridges sem Pooling), CA32 (parte).
- Testes red: `Ability_RaiseVoid_OnceAndListenerReacts`, `Action_IntChannelFixed7_Raises7`, `Action_NoChannel_IsValidationError`, `AsmdefRules_BridgesDoesNotReferencePooling` falham.
- Manual: GAS, seção **RaiseEventAction** `{#raiseeventaction}` (canal sem payload ou com payload fixo; payload dinâmico no B-004).
- Tamanho: ~240 linhas, 10 arquivos.

### 0M-38 — `feat(core): validar projeto por menu e por teste`
- **Alterado (revisão sem Odin):**
  - o validador usa `IValidatable` (4.9);
  - a janela é UI Toolkit;
  - entra o erro de tipo `[SerializeReference]` ausente (CA64, risco 28);
  - o rename do asmdef do Editor e a mudança do `FolderStructureCreator` saem, porque foram feitos no 0M-41;
  - **2ª rodada (I48):** sai o `ValidationInspectorHook`. As mensagens no Inspector vêm do `SpellBookEditor` (0M-48). A janela e o Inspector usam as mesmas mensagens: o `ProjectValidator` e o `SpellBookEditor.CollectMessages` chamam o mesmo `IValidatable`.
- Arquivos: `Editor/Core/ProjectValidator.cs`, `Editor/Core/ValidationWindow.cs`, `Tests/Core/EditMode/SpellBook.Core.Tests.EditMode.asmdef` (+Events, Pooling, GAS, GAS.Bridges), `Tests/Core/EditMode/ProjectValidationTests.cs`, `Documentation~/manual/ferramentas/validador.md`.
- Depende de: 0M-37, 0M-33.
- CAs: CA35, R32, R33, R34, CA64, CA34 (S).
- Testes red (falham, porque `ProjectValidator` não existe):
  - `Validator_ReportsEachR35Error` (um objeto em memória por regra);
  - `Validator_FixedObjects_NoErrors`;
  - `Validator_ExceptionInValidate_BecomesErrorAndContinues`;
  - `Validator_MissingSerializeReferenceType_IsError_DataKept` (YAML na pasta temporária, risco 23);
  - `Project_HasNoValidationErrors` (projeto do host).
- Cenários no host:
  - CA34: um `EventListener` sem canal aparece na janela, e o clique seleciona o objeto;
  - R32: o mesmo erro aparece no topo do Inspector do `EventListener` e some ao ligar o canal. Isso só é verificável depois do 0M-50, que dá ao `EventListener` o editor de uma linha; o Examinador registra o cenário no 0M-50.
- Manual: **Validador** (menu, como ler a janela e o Inspector, regras da 0-M, tipo ausente, o teste no CI, como implementar `IValidatable` num tipo próprio).
- Tamanho: ~280 linhas, 5 arquivos.

### 0M-39 — `docs(gas): HelpURL nos tipos restantes e teste de HelpURL`
- Arquivos: `Runtime/GAS/Attributes/AttributeSet.cs`, `AttributeDefinition.cs`, `Formulas/SimpleAttributeFormula.cs`, `Formulas/ExpressionFormula.cs`, `Runtime/GAS/Targeting/CircleTargeting.cs`, `MouseTargetingStrategy.cs`, `Runtime/GAS/Effects/GameplayEffect.cs`, `Tests/Core/EditMode/HelpUrlTests.cs`, `Documentation~/manual/modulos/gas.md` (âncoras que faltarem). (`GameplayAbility` recebeu no 0M-37.)
- Depende de: 0M-38.
- CAs: CA36, CA37.
- Testes red: `AllPublicConcreteMbAndSo_HaveHelpUrl` falha (7 tipos sem atributo); `AllHelpUrls_StartWithBase` passa a cobrir todos.
- Manual: GAS (âncoras e uma linha por tipo, se faltar).
- Tamanho: ~80 linhas, 9 arquivos.

### 0M-49 — `feat(editor): Inspectors de uma linha para Core, Time, Tags e Pooling` (novo, 2ª rodada)
- **Novo (I48).** Todo tipo do pacote usa o `SpellBookEditor`. Este ticket cobre os módulos de infraestrutura e cria o teste de cobertura.
- Arquivos:
  - `Editor/Core/Inspector/CoreEditors.cs` (`InstantiateSpawnerAsset`);
  - `Editor/Time/SpellBook.Time.Editor.asmdef` (Core, Time, Core.Editor) e `Editor/Time/TimeEditors.cs` (`TimeSourceAsset` com `editorForChildClasses`);
  - `Editor/Tags/SpellBook.Tags.Editor.asmdef` (Core, Tags, Core.Editor) e `Editor/Tags/TagsEditors.cs` (`GameplayTagDefinition`);
  - `Editor/Pooling/SpellBook.Pooling.Editor.asmdef` (Core, Pooling, Core.Editor) e `Editor/Pooling/PoolingEditors.cs` (`PooledSpawnerAsset`, `PoolManager`, `PoolConfiguration`, `PoolableObject`);
  - `Tests/Core/EditMode/Inspector/SpellBookEditorCoverageTests.cs` (assemblies cobertos: Core, Time, Tags, Pooling; seção 4.10);
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs` (os 3 asmdefs novos e as referências deles).
- Depende de: 0M-39 (todos os tipos e `[HelpURL]` existem), 0M-48.
- CAs: R32.
- Testes red:
  - `EveryPublicConcreteMbAndSo_InCoreTimeTagsPooling_UsesSpellBookEditor` falha (nenhum desses tipos tem `[CustomEditor]`);
  - `AsmdefRules_TimeTagsPoolingEditorsExist_WithAllowedReferences` falha.

  Cenário no host: o `PooledSpawnerAsset` com `initialSize` negativo mostra o erro no topo do Inspector (o cenário do 0M-48).
- Manual: nenhuma página nova (a página Inspector já explica o comportamento, 0M-48).
- Tamanho: ~180 linhas, 9 arquivos.

### 0M-50 — `feat(editor): Inspectors de uma linha para GAS e Events e cobertura completa` (novo, 2ª rodada)
- **Novo (I48).** Fecha a regra "todo tipo do pacote usa o `SpellBookEditor`".
- Arquivos:
  - `Editor/GAS/SpellBook.GAS.Editor.asmdef` (Core, Tags, Time, GAS, Core.Editor);
  - `Editor/GAS/GasEditors.cs` (`AbilitySystemComponent`, `AttributeSet`, `GameplayAbility`, `GameplayEffect`, `AttributeDefinition`, `Formula` com `editorForChildClasses`, `TargetingStrategy` com `editorForChildClasses`, `Projectile`);
  - `Editor/Events/EventsEditors.cs` (`EventListener`; canal e emissor já têm editor, 0M-45);
  - `Tests/Core/EditMode/Inspector/SpellBookEditorCoverageTests.cs` (+GAS, Events, GAS.Bridges);
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs` (`GAS.Editor`);
  - `Documentation~/manual/inicio/inspector.md` (seção **Inspectors dos seus tipos**: herdar de `SpellBookEditor`, conflito com o Inspector do usuário, risco 32).
- Depende de: 0M-49.
- CAs: R32.
- Testes red:
  - a cobertura de GAS, Events e GAS.Bridges falha (sem editor);
  - `AsmdefRules_GasEditorExists_WithAllowedReferences` falha.

  Cenário no host (R32 do 0M-38): um `EventListener` sem canal mostra o erro no topo do Inspector, e o erro some ao ligar o canal.
- Manual: Inspector, seção **Inspectors dos seus tipos**.
- Tamanho: ~150 linhas, 6 arquivos.

### 0M-40 — `refactor(core)!: Core em Runtime/Core e verificação final da estrutura`
- Arquivos: `Runtime/SpellBook.Core.asmdef` → `Runtime/Core/SpellBook.Core.asmdef` (com o `.meta`, GUID mantido; as pastas vazias `Runtime/Systems`, `Runtime/MathTools` e os `.meta` delas somem), `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs` (lista completa do CA1 e do CA32), `Tests/Core/EditMode/ManualLinksTests.cs`, `HostProject~/Assets/Scenarios/C1/C1.PoolingOnly.asmdef` (refs só Core e Pooling), `HostProject~/Assets/Scenarios/C1/C1PoolingUsage.cs`, `Documentation~/manual/inicio/conceitos.md` (mapa final), `Documentation~/manual/index.md`.
- Depende de: 0M-50.
- CAs: CA1, CA32, CA54, CA6, N4.
- **Alterado (revisão sem Odin):** a lista final do CA1 não tem `SpellBook.Setup.Editor`. Tem `SpellBook.Core.Editor`, `.Events.Editor`, `.Time.Editor`, `.Tags.Editor`, `.Pooling.Editor` e `.GAS.Editor`. Depende do 0M-50, não mais do 0M-39. O `NoOdinTests` e o `SpellBookEditorCoverageTests` continuam verdes depois do movimento.
- Testes red: `AsmdefRules_CoreAsmdefInRuntimeCore` falha (asmdef ainda na raiz de `Runtime/`); `AllExpectedAssembliesExist` e `EachRuntimeModuleHasEditModeTests` são guardas finais; `EveryHelpUrlHasPageAndAnchor` e `NoStubPages` são guardas (red mostrado removendo uma âncora localmente). CA6: o host compila o cenário C1 (no CI, ao rodar a suíte).
- Manual: Conceitos (mapa final com as referências permitidas) e O que é (lista final de módulos).
- Tamanho: ~150 linhas, 7 arquivos.

---

## 9. Pendências para o gate

- Resolvidas pelas decisões I41 a I44: host (D1), remoção pura (0M-25 num ticket só), 2.x congelada, alocação do `Tick` com efeito periódico (D3).
- ~~P21 (licença do Odin no CI)~~: sem objeto depois da I45. O 0M-11 foi desbloqueado.
- Abertas para este gate (revisão sem Odin):
  - aceitar a **ADR 0026** (Proposta);
  - ~~I-m, I-n, perdas no Inspector do GAS e canal dentro da emissão~~: resolvidos pelas decisões I48, I49 e I50;
  - os ajustes do PRD feitos pelo Criativo:
    - CA53 e CA41 cancelados; CA63 a CA65 novos;
    - R1 (regra dos `.Editor`) e R32 (validação no Inspector e na janela), com o texto do Arquiteto;
    - R28, R29, R40, R41 (`file:../..`), R42 e outros revistos.
- Continua aberto o passo manual do usuário de ligar o Pages com "Source: GitHub Actions" (0M-09).
- Abertas pelo D5 (host na 6000.3.13f1, decisão do usuário de 2026-10-29):
  - ~~emenda da ADR 0010~~: **resolvida**, aprovada pelo usuário em 2026-10-29 (consequência do host trocada e seção "Emenda: host local fora da 6000.0");
  - ~~ajuste do CA5 do PRD pelo Criativo~~: **resolvido**. O Criativo ajustou o CA5 (PRD, linha 242) e registrou o D5 no diário;
  - ~~adiantar o 0M-11 para logo depois do 0M-07~~: **resolvida**, aprovada pelo usuário em 2026-10-29 (ordem e Resumo da seção 8; a seção "Compatibilidade" do manual passou para o 0M-08).
