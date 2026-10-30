# Spec: Fase 0-M, modularização do SpellBook

Status: **Aprovada** (gate da revisão sem Odin, 2026-10-29). A versão anterior foi aprovada no gate de 2026-10-29 e volta a rascunho por causa das decisões I45 a I47.
Data: 2026-10-29 · Backlog: B-001 (e B-020, B-021, B-022 pela via rápida)
PRD: [prd/modularizacao.md](../prd/modularizacao.md) · Plano: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção I (decisões 1 a 47)
ADRs: 0001, 0002, 0003, 0004, 0005, 0006 (Adiada), 0007, 0008, 0009, 0010, 0011 (Substituída por 0026), 0012, 0013, 0014, 0015, 0016, 0025, **0026 (Proposta)**

> **Revisão sem Odin (2026-10-29).** O usuário decidiu que o SpellBook não depende do Odin (I45), que drawers e janelas são em UI Toolkit (I46) e que não há compatibilidade com o Odin (I47). A ADR 0026 substitui a 0011. A revisão:
> - remove o Odin dos 8 arquivos atuais logo depois do host (0M-44);
> - cria a infraestrutura de testes com a guarda "nenhum Sirenix" (0M-07);
> - cria o seletor de tipo para `[SerializeReference]` (catálogo no 0M-41, popup UI Toolkit no 0M-47, drawer no 0M-42), o `[ShowIf]` (0M-46), a interface de validação (0M-43), o Inspector base `SpellBookEditor` com validação no topo (0M-48) e o tipo ausente no topo dele (0M-53; relatado por objeto, D6), os Inspectors do Events (0M-45) e um `[CustomEditor]` de uma linha por tipo (0M-49, 0M-50);
> - desbloqueia o CI (0M-11; cancelado depois, em 2026-10-29, ver abaixo).
>
> **Segunda rodada (2026-10-29, decisões I48 a I50):** nenhuma exceção IMGUI. Saem o `AdvancedDropdown` e o gancho `Editor.finishedDefaultHeaderGUI`, e entram o popup próprio e o `SpellBookEditor`. As perdas no Inspector do GAS foram aceitas (I49) e o canal dentro da `EventEmission` também (I50).
>
> Os ids 0M-01 a 0M-40 não mudam (issues #2 a #41). Os tickets novos são 0M-41 a 0M-50. Os ids 0M-51 (plano de divisão do 0M-07) e 0M-52 (divisão do 0M-41) não são usados: as duas divisões foram descartadas e os dois tickets têm exceção de tamanho aprovada. A coluna "Mudança" da seção 8 mostra o que mudou em cada ticket. O PRD está sendo ajustado em paralelo pelo Criativo; os CAs novos CA63 a CA66 foram aceitos no PRD.
>
> **Sem CI de testes (decisão do usuário, 2026-10-29):** "tira essa regra. A ideia é que o sistema funciona a partir do 6000, o que já vai funcionar". Os secrets da Unity não serão cadastrados. O **0M-11 foi cancelado** (seção 8), o D5 passa de desvio a decisão, o mínimo declarado continua `"unity": "6000.0"` e a verificação é só no host 6000.3.13f1. ADRs 0010 e 0004 emendadas. O PRD já foi ajustado pelo Criativo nesta revisão (R34, R42, R43, N1, CA56, CA57, P14, I30, objetivo 5 e persona Mantenedor).

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
| 0010 | `"unity": "6000.0"` declarado; API mais nova só com guarda de versão; verificação só no host 6000.3.13f1 (D5), sem CI de testes (emenda de 2026-10-29) |
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
- **D5. Host local na 6000.3.13f1, não na 6000.0** (ADR 0010, consequência "o host fixa a 6000.0"; I-j na parte do host). Decisão do usuário em 2026-10-29. Motivo: a 6000.0 não está instalada na máquina (instaladas: 6000.3.1f1, 6000.3.5f2, 6000.3.13f1, 6000.4.10f1). O `HostProject~/ProjectSettings/ProjectVersion.txt` fica com 6000.3.13f1 (0M-06). O mínimo declarado não muda (`"unity": "6000.0"`: 0M-08, CA4, CA42).
  - **Atualização (2026-10-29, decisão do usuário): o D5 deixa de ser desvio e vira decisão.** Não há CI de testes (0M-11 cancelado; emenda "sem CI de testes" da 0010). A única verificação é a suíte no host 6000.3.13f1. O texto abaixo substitui o que dizia que o job 6000.0 do CI provava a versão mínima.
  - Risco aceito pelo usuário: código que usa API ou comportamento que só existe a partir da 6000.1 passa no host e ninguém o roda na 6000.0. Os reds que dizem "confirma na 6000.0" (riscos 16, 25, 27 e 28; 0M-32, 0M-41, 0M-42, 0M-46, 0M-47, 0M-48, 0M-53) confirmam na versão do host.
  - Cobertura: o Examinador barra API nova sem a guarda de versão do item 3 da 0010 (conferindo na documentação da Unity 6000.0 as APIs do risco 27 e as que o ticket usa pela primeira vez); release com a suíte inteira verde no host. O cenário CA5 é verificado no host 6000.3.13f1. O manual cita como testada só a versão do host (0010, "não afirmar compatibilidade com versão em que os testes não rodaram").
  - Se a 6000.0 for instalada um dia: trocar o host para ela é opcional, por ticket `chore(host)` próprio, que revê o `com.unity.test-framework` (I-g) e roda a suíte inteira.
- **D6. Tipo ausente relatado por objeto, não por campo** (ADR 0026, item 4, "Drawer: num campo com tipo ausente"). Proposto pelo Arquiteto em 2026-10-29 e **aprovado pelo usuário em 2026-10-29**. Achado da Fábrica no red do 0M-41, com log de diagnóstico na 6000.3.13f1:
  - quando o tipo de uma referência `[SerializeReference]` some, o campo fica com `managedReferenceId = -2` (`ManagedReferenceUtility.RefIdNull`, o mesmo valor de um campo nulo) e `managedReferenceFullTypename` vazio;
  - `SerializationUtility.GetManagedReferencesWithMissingTypes(obj)` devolve o tipo ausente com outro `referenceId` (no log, `3572287566287470595: SpellBook.Core.Tests.Inspector.PickRemovido`);
  - nenhuma API pública liga o campo ao `referenceId` do tipo ausente.

  Por isso o `TryGetMissingType(SerializedProperty)` não é viável e o drawer não sabe que o campo dele perdeu o tipo: o campo aparece como "(Nenhum)". Das três saídas da Fábrica, a Spec adota a **(A)**:
  - **(A) adotada:** relatar por objeto. `MissingReferenceTypes.Get(UnityEngine.Object)` lista os tipos ausentes; o `SpellBookEditor` mostra um `HelpBox` de erro por tipo ausente no topo do Inspector, cada um com o botão "Limpar referência" pelo `referenceId` (seção 4.10, 0M-53; o `SpellBookEditor` base e o ponto de extensão vêm no 0M-48). Usa só API pública que já existe e que a Fábrica viu funcionar.
  - (B) recusada: tratar todo campo com `-2` num objeto com tipo ausente como "possível tipo ausente" acusa campos que estão nulos de propósito e não diz qual campo é quando o objeto tem mais de um. O aviso perde a confiança do designer.
  - (C) recusada: ler o YAML do asset é frágil (formato interno da Unity) e não funciona para objeto só em memória nem para objeto de cena não salva.

  O que muda em relação à 0026: o aviso sai do drawer e vai para o topo do Inspector do objeto; a mensagem, o botão e a regra "nunca limpa sozinho" continuam. O R45 do PRD continua atendido ("o Inspector mostra um erro com o assembly e a classe que faltam e um botão para limpar a referência"), e o CA64 (validador) não muda. Limite aceito: um tipo do usuário cujo Inspector não herda de `SpellBookEditor` só vê o tipo ausente no validador do projeto (R33, CA64); o manual explica isso (0M-53). Precisa de uma **emenda datada na ADR 0026, item 4**, escrita pelo Arquiteto depois da aprovação (texto proposto na seção 9).

### Inferidos (validar no uso)

| Id | Valor | Onde |
|---|---|---|
| ~~I-a~~ | ~~Odin mínimo 3.3~~. **Cancelado** pela 0026 (sem Odin) | — |
| I-b | Grupo pausável segue o tempo escalado, só pausa, sem escala própria | 0014, 0M-18 |
| I-c | Pool criado sob demanda pelo `PooledSpawnerAsset`: tamanho inicial 5, expansível (os valores do fallback atual do `PoolManager.cs:56-57`), configuráveis no asset | 0M-23 |
| I-d | Histórico do Event Debugger: buffer circular de 256 disparos por sessão de Play | 0M-35 |
| I-e | Navegação do site gerada da árvore de pastas (sem `nav:` explícito), para nenhum ticket precisar editar a configuração ao criar página | 0M-08 |
| I-f | Âncoras de `[HelpURL]` explícitas com `attr_list` (`## EventListener {#eventlistener}`) | 0M-08, CA54 |
| I-g | `com.unity.test-framework` fixado no manifest numa versão que resolva no host (6000.3.13f1, D5). Sem CI (2026-10-29), a parte "e na 6000.0 do CI" sai | 0M-06 |
| I-h | Menus de criação dos assets do GAS continuam `GAS/...` (R27 pede tags sem mudança de comportamento); padronizar para `SpellBook/...` fica para a Fase 9 | 0M-14 |
| I-i | Uma espera pendente do Invoke com delay aloca uma entrada por disparo (fora do caminho sem alocação do N2) | 0M-31 |
| ~~I-j~~ | ~~Patch exato da 6000.0 e da última 6000.x no CI~~. **Cancelado** com o 0M-11 (sem CI de testes, 2026-10-29) | — |
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
.github/workflows/docs.yml   (tests.yml saiu com o 0M-11, cancelado em 2026-10-29)
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

**Referências dos asmdefs de teste** (esclarecimento de 2026-10-29, desvio do 0M-12):
- a regra de dependência entre módulos (o Core e a infraestrutura não referenciam o GAS) vale para os asmdefs de runtime e de Editor. Os asmdefs de teste ficam fora do grafo: têm `autoReferenced: false` e ninguém os referencia, então não criam ciclo nem acoplamento no pacote;
- um `SpellBook.<Módulo>.Tests.*` referencia o módulo testado, as dependências dele, o `SpellBook.Time` e o `SpellBook.Testing` (ADR 0004, item 4);
- o `SpellBook.Core.Tests.EditMode` também é o assembly dos testes de estrutura e de integração do pacote (`AsmdefRulesTests`, validador do 0M-38, uso do seletor e do `[ShowIf]` nos tipos do GAS). Por isso pode referenciar qualquer runtime do pacote: o GAS desde o 0M-12 e os outros módulos no 0M-38;
- o `AsmdefRulesTests` não aplica a regra do Core aos asmdefs de teste: a lista `Infra` tem só os runtimes.

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
  - O gancho é `internal static void ResetAllOnSessionStart()`, exposto aos testes por `InternalsVisibleTo("SpellBook.Time.Tests.EditMode")` e `.PlayMode` em `Runtime/Time/AssemblyInfo.cs`, como o `EventRegistry` (4.7). Os testes o chamam direto, sem reflection, e no `TearDown` (risco 1).
  - O registro **não é esvaziado**: o gancho remove só as instâncias destruídas e reseta as outras. Sem domain reload, um asset já carregado não roda `OnEnable` de novo ao entrar em Play; se a lista fosse esvaziada, ele sairia do registro e não seria resetado na sessão seguinte. "Registro limpo" na 0014 quer dizer estado de sessão limpo, não lista vazia.
  - (Emenda do Arquiteto, 2026-10-30, dúvida da Fábrica no 0M-17.)
  - **Início da sessão do grupo pausável é gravado na primeira leitura, não no reset.** A documentação da Unity não garante o valor de `Time.timeAsDouble` em `SubsystemRegistration` (sem domain reload ele pode ainda ser o da sessão anterior). Por isso `ResetState()` não lê o relógio: zera pausa e acumulado e marca o início como pendente. O primeiro uso depois do reset (leitura de `ElapsedTime` ou `SetPaused`/`Pause`/`Resume`) grava `inícioDaSessão = UnityEngine.Time.timeAsDouble` e desmarca a pendência; esse uso já devolve ou conta a partir de 0. Assim o `ElapsedTime` do grupo nunca começa negativo e "recomeça do 0" vale para a primeira leitura da sessão, qualquer que seja a ordem de inicialização da Unity. Fontes escalada e não escalada não mudam (devolvem o relógio da Unity; o consumo é por diferença).
  - (Emenda do Arquiteto, 2026-10-30, dúvida do Examinador no 0M-18.)
- **Consumo por diferença** (0014): o consumidor guarda o último `ElapsedTime` lido; ao ligar (`OnEnable` ou ao trocar a fonte) o cursor recebe o valor atual, para não haver salto no primeiro quadro.

### 4.4 GAS (mudanças da 0-M)

- **ASC:**
  - `[SerializeField] TimeSourceAsset timeSource` (vazio → `TimeSourceAsset.Scaled`), `[SerializeField] bool autoTick = true`.
  - `public ITimeSource TimeSource { get; }`, `public void SetTimeSource(ITimeSource source)` (código e testes; reinicia o cursor).
  - `public void Tick(float deltaTime)`: ignora `deltaTime <= 0`, soma em `public double TickedTime { get; }` (relógio próprio do ASC) e avança os efeitos de trás para frente, como hoje.
  - `Update`: se `autoTick`, `Tick((float)(fonte.ElapsedTime - cursor))`. Em todo quadro, com `autoTick` ligado ou não, o cursor recebe o `ElapsedTime` atual da fonte. Assim, ao religar o `autoTick`, o primeiro `Tick` recebe só o delta do último quadro, e não o tempo acumulado enquanto esteve desligado (0014, risco 10). Com `autoTick` desligado o ASC só avança por `Tick` explícito.
  - (Emenda do Arquiteto, 2026-10-30, comportamento aceito pelo Examinador na revisão do 0M-19.)
  - Tags: `_activeTags` passa a `Dictionary<GameplayTag, int>`; `HasTag` itera as chaves com `Matches`.
  - Remove `EventManager.Trigger` (`:217`). Os eventos C# continuam.
- **`ActiveEffectInstance`:** remove `StartTime` (quebra; ninguém no pacote o lê) e o `using UnityEngine` deixa de ser preciso para tempo.
- **Esperas das ações** (0M-22): `AbilityContext.WaitSeconds(float seconds)` devolve um `IEnumerator` que espera, quadro a quadro, até o `Source.TickedTime` avançar `seconds` (conta pelo restante: `restante -= TickedTime - último`). `WaitAction`, `RepeatAction.Interval` e `SpawnProjectileAction.DelayBetweenShots` usam isso. Se o ASC for destruído, a espera termina.
  - **`WaitSeconds` com `seconds <= 0` termina na hora**, sem ceder quadro. `RepeatAction` e `SpawnProjectileAction` só esperam com intervalo `> 0`, como na 2.x.
  - **`WaitAction` com `Duration <= 0` espera exatamente um quadro** (`yield return null`) e não lê o tempo, nem o do ASC. É o comportamento da 2.x (`WaitForSeconds(0)` e valores negativos cedem um quadro), e há listas de ações que usam "Wait 0" para passar ao quadro seguinte. A regra fica no `WaitAction`, não no `WaitSeconds`. Com o ASC parado (`autoTick` desligado ou grupo pausado), "Wait 0" ainda termina no quadro seguinte, porque espera quadro, não tempo.
  - (Emenda do Arquiteto, 2026-10-30, dúvida da Fábrica no 0M-22.)
- **`Projectile`:** `[SerializeField] TimeSourceAsset timeSource` (vazio → escalado); `FixedUpdate` lê o delta por diferença de `ElapsedTime`; tempo de vida e invulnerabilidade viram contadores de restante decrementados no `FixedUpdate`, sem corrotina. `Setup(SpawnerAsset spawner, AbilityContext parentContext, List<AbilityAction> onHitActions)` (e a sobrecarga com `AbilitySystemComponent`); `ReturnToPool()` chama `SpawnerAsset.OrDefault(spawner).Release(gameObject)`. Não implementa mais `IPoolable`; `OnEnable` reinicia o estado.
  - Com o contador, sai o `protected virtual IEnumerator InitialInvulnerability()` (quebra para subclasses que o sobrescrevem; seção 7). A invulnerabilidade continua no campo `InitialInvulnerabilityDuration` e no `HandleCollision`, que ignora o choque enquanto o contador for positivo. (Emenda do Arquiteto, 2026-10-30, 0M-22.)
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
  - cada alvo (e cada componente, nos `GameObject`) recebe um erro por entrada de `MissingReferenceTypes.Get(alvo)`, com o texto de `MissingReferenceTypes.FormatMessage` (assembly e classe, CA64). É a mesma função e o mesmo texto do topo do Inspector (0M-53, seção 4.10, D6). O validador nunca chama `Clear`.
- `ProjectValidator.CollectProjectTargets()`: assets de `ScriptableObject` e prefabs do projeto (`AssetDatabase.FindAssets`), mais os objetos das cenas abertas.
- Regra de `[HelpURL]` (R35, último item): função `HelpUrlRule.FindViolations(IEnumerable<Assembly>)`, usada pelo teste CA36 e pela janela.
- O menu `SpellBook/Validar projeto` abre a `ValidationWindow` (UI Toolkit, `EditorWindow.CreateGUI`, `ListView`) com a lista. Clicar num item seleciona o objeto e faz ping nele (CA34).
- **Inspector** (R32, I48): as mensagens aparecem no topo do Inspector do próprio objeto, pelo `SpellBookEditor` (0M-48, seção 4.10), que todo tipo do pacote usa (0M-49, 0M-50). O `ProjectValidator` não desenha nada no Inspector, e não existe gancho IMGUI.

### 4.10 Editor próprio (0026)

O Setup sem Odin foi **cancelado**: não existe `SpellBook.Setup.Editor` nem `OdinMissingNotice`.

**Seletor de tipo** (0M-41, em `Editor/Core/Inspector/`, namespace `SpellBook.Core.Editor.Inspector`; os atributos ficam em `SpellBook.Core.Inspector`):
- **`TypePickerCatalog`** (estático, sem UI, testável; não trata tipo ausente, ver "Tipo ausente" abaixo e D6):
  - `IReadOnlyList<TypePickerEntry> GetCandidates(Type baseType)`: aplica o filtro da 0026, item 4 (concreto, não genérico, `[Serializable]`, construtor sem parâmetros, fora de `UnityEngine.Object`), usa `TypeCache.GetTypesDerivedFrom` e guarda o resultado em cache por tipo base;
  - `TypePickerEntry { Type Type; string DisplayName; string CategoryPath; string Summary }`;
  - `IEnumerable<TypePickerEntry> Filter(IEnumerable<TypePickerEntry>, string query)`: busca sem diferenciar maiúsculas e minúsculas por nome exibido, nome do tipo e caminho;
  - `Type ResolveFieldBaseType(SerializedProperty)`: lê `managedReferenceFieldTypename`;
  - `void Assign(SerializedProperty, Type typeOrNull)`: instância nova ou nulo, com Undo;
  - `bool EnsureUniqueInArray(SerializedProperty element)`: troca por cópia o elemento cujo `managedReferenceId` já aparece antes na mesma lista; devolve `true` se trocou.
  - ~~`bool TryGetMissingType(SerializedProperty, ...)`~~: **removido** (D6). A Unity não liga o campo ao tipo ausente.
- **`TypePickerDrawer`** (`[CustomPropertyDrawer(typeof(TypePickerAttribute))]`, `CreatePropertyGUI`):
  - linha de cabeçalho com o rótulo e um botão com o nome do tipo atual (ou "(Nenhum)"); o botão abre o `TypePickerPopup` (0M-47);
  - abaixo, um `PropertyField` por filho da instância. Isso não é recursivo, porque cada `PropertyField` é de um filho, não da própria propriedade;
  - `TrackPropertyValue` reconstrói os filhos quando o tipo muda;
  - tipo ausente: o drawer **não** trata (D6). O campo cujo tipo sumiu tem `managedReferenceId = -2`, como um campo nulo, e aparece como "(Nenhum)"; o aviso fica no topo do Inspector (ver "Tipo ausente" abaixo);
  - ao ligar, chama `EnsureUniqueInArray`;
  - `OnGUI` (IMGUI) desenha só o `HelpBox` "Este campo precisa do Inspector em UI Toolkit".

**`[ShowIf]`** (0M-46, `ShowIfDrawer`):
- o drawer não pode devolver `new PropertyField(property)` da própria propriedade, porque isso cai de novo no mesmo drawer (recursão);
- o red do 0M-46 decidiu entre duas formas, e **ficou a (b)** (decisão de 2026-10-29):
  - (a) devolver o campo padrão criado por `PropertyField` com o drawer contornado. **Descartada:** o `PropertyField` público da 6000.3 só tem os construtores `(property)` e `(property, label)` e não consegue pular o drawer;
  - (b) **adotada:** construir o campo pelo tipo da propriedade (`PropertyField` de cada filho para struct e classe; `BindableElement` por `propertyType` para os primitivos usados: `float`, `int`, `bool`, `enum`, `Color`, `string`, referência de objeto). O `ShowIfDrawer` nunca cria um `PropertyField` da própria propriedade, o que evita a recursão (risco 25).
- consequência da (b): um campo tem um drawer só, então **nenhum campo pode juntar `[ShowIf]` e `[TypePicker]`**. Nenhum campo do GAS junta os dois;
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
  - **ponto de extensão do topo (0M-48):** o contêiner de avisos do topo (classe USS `spellbook-top-notices`), sempre o primeiro filho, escondido quando não tem filho. Quem o preenche é o método privado `RefreshTopNotices()`, que no 0M-48 só limpa o contêiner e o esconde (sem avisos). É chamado no build, no "Revalidar" e depois de cada ação de um aviso. O **0M-53** pluga o tipo ausente só dentro desse método: um `HelpBox` de erro por entrada de `MissingReferenceTypes.Get(alvo)`, com o texto de `FormatMessage` (e o nome do objeto quando há mais de um alvo), e um botão "Limpar referência" em cada um (ver "Tipo ausente" abaixo). A ordem do `CreateInspectorGUI`, o contêiner de validação e os métodos de extensão não mudam no 0M-53;
  - o contêiner de validação (classe USS `spellbook-validation`), com um `HelpBox` por mensagem e um botão "Revalidar" (I-p);
  - `BuildBeforeDefault`;
  - `InspectorElement.FillDefaultInspector(root, serializedObject, this)`;
  - `BuildAfterDefault`.
- `RefreshValidation()` cria um `ValidationResult` por alvo em `targets`, chama `Validate` quando o alvo é `IValidatable` e reconstrói os `HelpBox`. Uma exceção vira um `HelpBox` de erro com o tipo da exceção, como no `ProjectValidator`.
- O recálculo é chamado no build e em `root.TrackSerializedObjectValue(serializedObject, _ => RefreshValidation())`. O contêiner do topo é reconstruído por `RefreshTopNotices()` no build, no "Revalidar" e (0M-53) depois de cada "Limpar referência".

**Tipo ausente** (0M-53, D6), em `Editor/Core/Inspector/MissingReferenceTypes.cs`, namespace `SpellBook.Core.Editor.Inspector`. Relata por **objeto**, porque a Unity não liga o campo ao tipo ausente (o campo fica com `managedReferenceId = -2`, igual a nulo):
- `readonly struct MissingTypeEntry { long ReferenceId; string AssemblyName; string FullTypeName; }`. `FullTypeName` é `namespace.Classe`, ou só `Classe` quando o namespace é vazio;
- `static IReadOnlyList<MissingTypeEntry> Get(UnityEngine.Object target)`: lista vazia para alvo nulo ou sem tipo ausente (`SerializationUtility.HasManagedReferencesWithMissingTypes` antes, por custo); senão, uma entrada por item de `SerializationUtility.GetManagedReferencesWithMissingTypes(target)`, na ordem devolvida pela Unity;
- `static bool Clear(UnityEngine.Object target, long referenceId)`: chama `SerializationUtility.ClearManagedReferenceWithMissingType(target, referenceId)`, marca o alvo como sujo (`EditorUtility.SetDirty`) e devolve o resultado da Unity. Só é chamada pelo botão; nada no pacote chama sozinho;
- `static string FormatMessage(MissingTypeEntry)`: "Tipo não encontrado: `<assembly>` `<namespace.Classe>`. Restaure o tipo para recuperar os dados, ou limpe a referência.";
- o botão "Limpar referência" do `SpellBookEditor` pede confirmação (`EditorUtility.DisplayDialog`), porque a limpeza apaga os dados guardados e a Spec não promete Undo para ela. O cenário do host do 0M-53 registra se o Undo da Unity recupera os dados; o manual descreve o que for observado;
- o drawer não mostra nada (o campo aparece como "(Nenhum)"). O manual orienta a restaurar o tipo ou limpar a referência antes de escolher outro tipo nesse campo. O efeito de escolher um tipo num campo nessa situação, sem limpar antes, é verificado no cenário do host do 0M-53 (risco 28);
- um tipo do usuário cujo Inspector não herda de `SpellBookEditor` não vê o aviso no Inspector, só no validador do projeto (0M-38, CA64). O manual explica (0M-53).
- Função testável: `static IReadOnlyList<ValidationMessage> SpellBookEditor.CollectMessages(IEnumerable<UnityEngine.Object> targets)`.
- **Um editor por tipo** (0M-49, 0M-50):
  - um arquivo por módulo (`CoreEditors.cs`, `TimeEditors.cs`, `TagsEditors.cs`, `PoolingEditors.cs`, `GasEditors.cs`, `EventsEditors.cs`), com classes `[CustomEditor(typeof(X))] sealed class XEditor : SpellBookEditor { }`;
  - um tipo base com `editorForChildClasses: true` cobre as subclasses (por exemplo, `TimeSourceAsset`);
  - o canal e o emissor usam os editors com botão do 0M-45.
- **Teste de cobertura** (`SpellBookEditorCoverageTests`): para cada tipo público, concreto, `MonoBehaviour` ou `ScriptableObject` dos assemblies de runtime cobertos, cria uma instância em memória (`ScriptableObject.CreateInstance` ou `AddComponent` num `GameObject` temporário), faz `Editor.CreateEditor(instância)` e confere `is SpellBookEditor`. Só usa API pública e destrói tudo no `TearDown`.

**Inspectors do Events** (0M-45): ver 4.7. `EventChannelEditor` e `EventEmitterEditor` herdam de `SpellBookEditor` e sobrescrevem `BuildAfterDefault`.

### 4.11 Documentação

- `Documentation~/mkdocs.yml`: `site_url: https://gabrielctorres.github.io/SpellBook/`, `docs_dir: manual`, `use_directory_urls: true`, tema Material, `markdown_extensions: [attr_list, admonition, toc, fenced_code]`, sem `nav:` (I-e). Build com `--strict`.
  - **Ferramenta (decidida no 0M-08, ADR 0025 item 2):** MkDocs 1.6.1 com Material 9.8.0, travados com `==` em `Documentation~/requirements-docs.txt`. O Zensical está na 0.0.73, pré-1.0, então não atende "estável" da 0025 e fica de fora. A 0025 **não precisa de emenda**: o item 2 já manda cair no MkDocs com Material em versão travada e registrar a escolha na Spec, que é este parágrafo. Uma troca futura para o Zensical aí sim exige emenda (consequências da 0025).
  - **Extensões (decidido no 0M-08):** `fenced_code` entra. Sem ela, os blocos cercados com três crases não renderizam, e o manual tem muito C#. É extensão do próprio Python-Markdown, que o MkDocs já instala, então não há dependência nova nem plugin (a 0025 só barra plugin). Não entram `pymdownx.superfences` nem `pymdownx.highlight` (vêm do `pymdown-extensions`, dependência transitiva do Material, fora do `requirements-docs.txt`): colorir o C# fica para quando alguém pedir, com ticket próprio e o `pymdown-extensions` travado.
  - **Verificação local disponível (aprovada pelo usuário, 2026-10-29):** venv fora do repositório em `C:/Users/gabri/MeusProjetos/SpellBook-venv-docs/`, com `pip install -r Documentation~/requirements-docs.txt`. Todo ticket de docs roda `mkdocs build --strict -f Documentation~/mkdocs.yml -d <pasta fora do repositório>`. O `-d` evita deixar `Documentation~/site/` na árvore antes de o `.gitignore` cobri-lo (0M-09). O venv nunca entra no repositório.
- Páginas da 0-M (todas com conteúdo real, decisão I26):

| Página | Arquivo | Criada em | Ampliada em |
|---|---|---|---|
| O que é | `manual/index.md` | 0M-08 | 0M-40 |
| Instalação | `manual/inicio/instalacao.md` | 0M-08 (com a seção Compatibilidade, D5) | — |
| Conceitos (mapa de módulos, três usos de evento, seletor de tipo e `[ShowIf]`) | `manual/inicio/conceitos.md` | 0M-12 | 0M-24, 0M-26, 0M-40 |
| Inspector do SpellBook (seletor de tipo, tipo ausente, `[MovedFrom]` para tipos do usuário, `[ShowIf]`, ambiguidade com outros pacotes) | `manual/inicio/inspector.md` | 0M-42 | 0M-46, 0M-48 (validação no topo, botão Revalidar), 0M-53 (tipo ausente no topo, seção **Tipo não encontrado**), 0M-50 (como herdar de `SpellBookEditor` nos tipos do usuário) |
| Migração 2.x → 3.0 | `manual/regras/migracao-2-para-3.md` | 0M-12 (já com a linha "o pacote não depende mais do Odin; perdas no Inspector do GAS", do 0M-44) | 0M-15, 0M-16, 0M-21 (com as linhas do 0M-19, emenda de 2026-10-30), 0M-25, 0M-40 (linha de Pooling, emenda de 2026-10-30) |
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
| Nenhum `.asmdef` (em `references`, `precompiledReferences`, `defineConstraints`, `versionDefines`) e nenhum `.cs`/`.asmdef`/`.asmref`/`.uxml`/`.uss` de `Runtime/`, `Editor/`, `Tests/`, nem o `package.json`, contém `Sirenix` ou `ODIN_INSPECTOR` no **início de um identificador** (regra de busca no 0M-07: borda de identificador só à esquerda, sensível a maiúsculas; pega `using Sirenix.*`, `Sirenix.OdinInspector.dll`, `ODIN_INSPECTOR` e `ODIN_INSPECTOR_3_1`, e não pega o nome de teste `...MentionsSirenix`) | `Core/EditMode/Structure/NoOdinTests.cs` | CA65 (substitui CA53) | Guarda (o 0M-44 já tirou o Odin); red mostrado com um `.cs` temporário contendo `using Sirenix.OdinInspector;`, sem commit |
| Todo asmdef do pacote tem `overrideReferences: true`; runtime e Editor com `precompiledReferences: []`; testes só com `nunit.framework.dll` | `Core/EditMode/Structure/AsmdefRulesTests.cs` | CA65 (parte), 0026 | `Runtime/SpellBook.Core.asmdef` e `Editor/SpellBook.Editor.asmdef` têm `overrideReferences: false` |
| Infra não referencia `SpellBook.GAS*`; GAS não referencia Events/Pooling; Bridges não referencia Pooling; Events só Core/Time | idem | CA2 | Guarda no 0M-07 (um assembly só); cada ticket que cria módulo acrescenta a asserção de existência do módulo, que falha antes de o asmdef existir |
| Runtime não referencia `UnityEditor` | `Core/EditMode/Structure/RuntimeReferencesTests.cs` | CA3 (sem as partes do Odin) | Guarda |
| Seletor: candidatos de um tipo base de teste = só os concretos, não genéricos, `[Serializable]`, com construtor sem parâmetros, fora de `UnityEngine.Object`, inclusive de outro assembly; categoria e resumo vêm do `[TypeCategory]`; sem atributo vai para a raiz; busca por nome e categoria | `Core/EditMode/Inspector/TypePickerCatalogTests.cs` | CA63 | `TypePickerCatalog` não existe |
| Seletor: `Assign` cria instância do tipo escolhido e `null` limpa; elemento de lista com `managedReferenceId` repetido vira cópia independente | idem | CA63, 0013 | Idem |
| `TypePickerTree`: hierarquia por categoria, "(Nenhum)" primeiro, categorias antes de tipos, `Parent` volta um nível, `Search` plana com o caminho | `Core/EditMode/Inspector/TypePickerTreeTests.cs` | CA63 | `TypePickerTree` não existe |
| `SpellBookEditor`: alvo `IValidatable` inválido gera `HelpBox` de erro no topo; válido, nenhum; alvo não `IValidatable`, contêiner escondido; `BuildAfterDefault` aparece depois dos campos; exceção no `Validate` vira erro; o contêiner `spellbook-top-notices` é o primeiro filho e fica escondido sem avisos | `Core/EditMode/Inspector/SpellBookEditorTests.cs` | R32 | `SpellBookEditor` não existe |
| Tipo ausente por objeto (D6): asset em YAML com referência a classe inexistente → `MissingReferenceTypes.Get` devolve uma entrada com assembly e classe; os dados continuam depois de reimportar; somem só depois de `Clear`; objeto sem tipo ausente → lista vazia; `SpellBookEditor` de um alvo com tipo ausente tem um `HelpBox` com "Limpar referência" no `spellbook-top-notices` (0M-53) | `Core/EditMode/Inspector/MissingReferenceTypesTests.cs` e `SpellBookEditorMissingTypeTests.cs` | R45 (CA64 é do validador) | `MissingReferenceTypes` não existe |
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
- ~~CA57 (0M-11)~~: **cancelado** com o 0M-11 (sem CI de testes, 2026-10-29); o CA57 também está cancelado no PRD;
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
| 8 | Tempo | Soma de floats na fronteira: o sinal do resto depende dos valores. Em float32, `3f - 2.9f - 0.1f` dá cerca de `-9,7e-8` (a recarga expira por acaso), mas `2f - 1.9f - 0.1f` dá cerca de `+2,2e-8`, positivo, e a recarga de 2 s **não** expiraria após "1,9 + 0,1". (Emenda do Arquiteto, 2026-10-30, revisão do 0M-19: o exemplo anterior dava `+2,4e-8` para `3f - 2.9f - 0.1f`, o que estava errado.) | CA8 | O CA8 usa dois arranjos independentes: um ASC com `Tick(2.9f)` (ainda em recarga) e outro com `Tick(3.0f)` (liberado); nunca a soma 2,9 + 0,1. Expiração continua "restante ≤ 0", como no código atual. A armadilha vai para a página GAS | CA8 |
| 9 | Tempo | `Time.timeScale` alterado por um teste afeta os seguintes | CA45, CA46, CA60 | `SetUp` guarda e `TearDown` restaura `UnityEngine.Time.timeScale` | Testes em ordem aleatória |
| 10 | Tempo | Salto de delta no primeiro quadro ou ao trocar de fonte | ASC, `Projectile`, `EventListener` | Cursor recebe o `ElapsedTime` atual no `OnEnable` e no `SetTimeSource` | CA9, CA46 |
| 11 | Tempo | Precisão do tempo acumulado em sessões longas | `ITimeSource.ElapsedTime` | `double` (`Time.timeAsDouble`) | — |
| 12 | Tempo | Conflito do namespace `SpellBook.Time` com `UnityEngine.Time` | Todo código em `namespace SpellBook.*` que referencia o asmdef Time | Ordem dos tickets (0M-19 troca todos os usos do GAS); testes escrevem `UnityEngine.Time` qualificado; manual avisa | Compilação; CA10 |
| 13 | Tempo | Ordem de reset em `SubsystemRegistration` (um registro limpo depois de alguém já ter se inscrito no Play) | Registros | Só limpar estado; nenhum código do pacote se inscreve antes de `AfterAssembliesLoaded` | CA19 |
| 14 | Dados externos | ~~Odin ausente ou símbolo velho~~. **Removido** (0026). Risco novo: um `using Sirenix` volta ao pacote por engano num projeto local que tem o Odin | Todos os asmdefs | `overrideReferences: true` com `precompiledReferences: []` (o compilador recusa); host e CI sem Odin | `NoOdinTests`, `AsmdefRulesTests` (0M-07) |
| 15 | Dados externos | ~~API de `SelfValidationResult`~~. **Removido**: a interface é própria (`IValidatable`, 0M-43) | — | — | — |
| 16 | Dados externos | Suporte da 6000.0 a genérico fechado em `[SerializeReference]` | `EventEmission<T>` | Teste de ida e volta no 0M-32; plano B (0013) | 0M-32 |
| 17 | Dados externos | ~~Disponibilidade das imagens GameCI~~. **Removido** com o 0M-11 (sem CI de testes, 2026-10-29) | — | — | — |
| 18 | Dados externos | ~~Primeira compilação sem `ODIN_INSPECTOR`~~. **Removido** (0026). ~~Contagem de testes executados > 0 no CI~~ **removida** com o 0M-11 (2026-10-29); no host, o Examinador confere que a saída lista os testes do ticket | — | — | — |
| 19 | Dados externos | ~~Secrets da Unity no CI~~. **Removido** com o 0M-11 (2026-10-29): o usuário não cadastra os secrets. Continua valendo que nenhum workflow traz credencial escrita (o `docs.yml` do 0M-09 não usa secret) | `.github/workflows/docs.yml` (0M-09) | Critério CA56 no 0M-09 | CA56 (0M-09) |
| 20 | Dados externos | Pages precisa ser ligado nas configurações do repositório ("Source: GitHub Actions"), passo manual do dono | 0M-09 | Passo listado no ticket para o usuário | CA39 |
| 21 | Dados externos | Versões Python da ferramenta de doc mudam | 0M-08 | `requirements-docs.txt` com `==` | Build `--strict` |
| 22 | Dados externos | Testes que leem arquivos do pacote (asmdefs, `.cs`, manual) dependem do caminho real | CA2, CA10, CA25, CA30, CA54, CA65 | Caminho obtido por `PackageInfo.FindForAssembly(...).resolvedPath`; os testes rodam só no host com `file:` | 0M-07 |
| 23 | Dados externos | Testes que criam assets e cenas em disco (Find references) deixam lixo ou mudam as cenas abertas | 0M-36 | Pasta temporária `Assets/__SpellBookTestTemp` criada no `SetUp` e apagada no `TearDown`; cenas abertas salvas e restauradas com `EditorSceneManager` | CA24 |
| 24 | Dados externos | ~~Setup lê `documentationUrl`~~. **Removido**: o Setup foi cancelado (0026) | — | — | — |
| 25 | Dados externos | **Compatibilidade do UI Toolkit, parte 1: recursão.** Um drawer que devolve `new PropertyField(property)` da própria propriedade cai de novo no mesmo drawer | `ShowIfDrawer` | Duas formas, (a) e (b), decididas no red do 0M-46 (4.10). O `TypePickerDrawer` só cria `PropertyField` de filhos | Cenário no host do 0M-42 e do 0M-46 (o Inspector abre sem estouro de pilha) |
| 26 | Dados externos | **Compatibilidade do UI Toolkit, parte 2: Inspector IMGUI.** Um Inspector IMGUI do usuário, ou um Inspector de terceiros (o Odin, por exemplo), desenha campos do SpellBook sem os drawers UI Toolkit | Drawers do Core e do Events | O `OnGUI` dos drawers mostra só o `HelpBox` de uma linha "precisa do Inspector em UI Toolkit" (único `OnGUI` permitido, 0026 item 3); sem suporte a outros Inspectors (I47); o manual explica na página Inspector | Página Inspector (0M-42) |
| 27 | Dados externos | **Compatibilidade do UI Toolkit, parte 3: APIs na 6000.0.** `CreatePropertyGUI`, `TrackPropertyValue`, `TrackSerializedObjectValue`, `CreateInspectorGUI`, `InspectorElement.FillDefaultInspector`, `EditorWindow.ShowAsDropDown`, `CreateGUI`, `ToolbarSearchField`, `ListView`, `TypeCache` e `SerializationUtility.*MissingTypes` precisam existir e se comportar igual na 6000.0 e na última 6000.x (0010, emenda) | 0M-41, 0M-47, 0M-42, 0M-46, 0M-48, 0M-53, 0M-38, 0M-45 | O red de cada ticket confirma a API no host (6000.3.13f1, D5). Sem CI (2026-10-29): o Examinador confere na documentação da Unity 6000.0 que cada API existe e, se não existir, cobra a guarda do item 3 da 0010. Risco aceito pelo usuário: comportamento diferente na 6000.0 não é testado | Red no host; revisão do Examinador |
| 28 | Dados externos | **Tipo serializado não encontrado.** Um tipo `[SerializeReference]` removido ou renomeado (do pacote na 3.0, de uma ponte ou do usuário) deixa a referência com "tipo ausente"; um clique errado ou uma limpeza automática apagaria os dados | Assets com `[SerializeReference]` | **Por objeto, não por campo (D6):** a Unity deixa o campo com `managedReferenceId = -2`, igual a nulo, e não liga o campo ao tipo ausente (medido pela Fábrica na 6000.3.13f1, 2026-10-29). O topo do Inspector (`SpellBookEditor`) mostra um erro por tipo ausente e só limpa por botão com confirmação (0026, item 4, com a emenda da D6); validador acusa erro com o mesmo texto (CA64); tipos do pacote renomeados na 3.0 vão para a tabela de migração (seção 7); depois da `3.0.0`, `[MovedFrom]`; o manual ensina `[MovedFrom]` ao usuário e avisa que o campo aparece como "(Nenhum)". Limite: tipo do usuário sem `SpellBookEditor` só vê o erro no validador. Em aberto até o cenário do host do 0M-53: o que acontece com os dados ausentes se o designer escolher um tipo no campo "(Nenhum)" sem limpar antes, e se o Undo recupera uma limpeza | 0M-53 (`MissingReferenceTypesTests`: a Unity guarda os dados do tipo ausente até o `Clear`, no host; sem CI, D5), 0M-38 (CA64) |
| 29 | Repetição | **Referência compartilhada.** Duplicar um elemento de lista `[SerializeReference]` no Inspector deixa dois elementos com o mesmo `managedReferenceId`; editar um muda o outro (proibido pela 0013) | Listas com `[TypePicker]` | `EnsureUniqueInArray` no bind do drawer | `TypePickerCatalogTests` (0M-41) |
| 30 | Repetição | Custo do seletor e da validação no Inspector | `TypePickerCatalog`, `SpellBookEditor` | Candidatos em cache por tipo base (o `TypeCache` já é indexado); validação só ao montar o Inspector e em `TrackSerializedObjectValue`, nunca por repaint | N5; revisão do Examinador |
| 32 | Dados externos | **Inspector do usuário concorre com o do pacote.** Um `[CustomEditor]` do usuário para um tipo do SpellBook (ou de terceiros, com `editorForChildClasses`) pode ser o escolhido pela Unity, e a validação no topo some | Tipos do pacote | O manual (página Inspector, 0M-50) manda herdar de `SpellBookEditor`; a janela do validador continua mostrando as mensagens | Página Inspector |
| 33 | Dados externos | **Bind do UI Toolkit em teste.** `CreateInspectorGUI` fora de um painel não liga os campos (`Bind`), e o teste não deve depender disso | `SpellBookEditorTests`, `SpellBookEditorMissingTypeTests`, `EventsInspectorTests` | Os testes conferem só a estrutura (contêiner, `HelpBox`, botões) e as funções puras (`CollectMessages`, `SetChannel`); o comportamento ligado fica nos cenários no host | 0M-48, 0M-53, 0M-45 |
| 34 | Dados externos | **Projeto Unity na raiz do pacote.** A raiz do repositório é a pasta do pacote; um `Assets/`, `Library/`, `Packages/` ou `ProjectSettings/` ali, mesmo ignorado pelo git, entra como conteúdo do `com.spellbook` (assemblies duplicados, `.meta` gravados, importação lenta) | Raiz do repositório, `HostProject~/` | Host antigo movido para fora (ADR 0004, emenda de 2026-10-29); único host em `HostProject~/` | Critério do 0M-06: as quatro pastas não existem na raiz quando o host abre |
| 35 | Dados externos | **Caminhos relativos no batchmode** (B-025, medido no 0M-07 rodando da raiz com `-projectPath HostProject~`). O `-logFile` relativo resolve a partir da pasta de onde o comando roda: o log foi para `Temp/` na raiz do pacote, a Unity importou e gerou `.meta` e erro (mesma causa do risco 34). O `-testResults` relativo resolve a partir do projeto: foi para `HostProject~/Temp/`, que a Unity apaga ao fechar, e o relatório se perdeu | `/implementar`, `unity-test-reader`, `unity-console-reader`, toda chamada local da Unity em batchmode | Caminhos **absolutos** dentro de `HostProject~/Logs/` (o `~` impede a importação, e a pasta já está no `.gitignore`): `-testResults "$PWD/HostProject~/Logs/TestResults-<Plataforma>.xml"` (`EditMode` ou `PlayMode`) e `-logFile "$PWD/HostProject~/Logs/unity-tests.log"`, com o comando rodando da raiz do repositório. Nenhum caminho relativo e nada em `Temp/`. A pasta é criada antes da chamada se não existir. (Não há CI de testes: o 0M-11 foi cancelado em 2026-10-29) | Depois da rodada: o XML e o log existem em `HostProject~/Logs/`; não existe `Temp/` na raiz do pacote; `git status` sem arquivo novo fora do `HostProject~/`. A atualização da skill e dos dois agentes é um ticket rápido à parte (B-025) |
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
| `SpawnVFXEffectAction` sem `DestroyOnRemoval` destruía o VFX em `duration + startLifetime`, mesmo com `Looping` ligado | devolve o VFX pelo spawner quando a partícula para (`ReleaseWhenParticlesStop`, ADR 0015); um `ParticleSystem` com `Looping` nunca para e o VFX não volta: ligar `DestroyOnRemoval` ou desligar `Looping` | 0M-21 |
| `WaitAction`, `RepeatAction.Interval` e `SpawnProjectileAction.DelayBetweenShots` esperavam o tempo escalado da Unity (`WaitForSeconds`) | esperam o `TickedTime` do ASC de origem; com `autoTick` desligado sem `Tick`, ou com o grupo pausado, a espera para. Com os padrões (`autoTick` ligado, fonte vazia), nada muda. `WaitAction` com `Duration <= 0` continua cedendo um quadro | 0M-22 |
| `Projectile`: vida e invulnerabilidade por corrotina no tempo escalado; `protected virtual IEnumerator InitialInvulnerability()` | contadores no `FixedUpdate` pela fonte de tempo do projétil (vazio → escalado, como na 2.x); o método saiu, e subclasses que o sobrescrevem deixam de compilar: ajuste `InitialInvulnerabilityDuration` ou sobrescreva `HandleCollision`; subclasses que sobrescrevem o `FixedUpdate` sem chamar `base.FixedUpdate()` perdem a vida e a invulnerabilidade | 0M-22 |
| `SpellBook.Systems.Pooling.*`, menu `PoolingSystem/...` | `SpellBook.Pooling.*`, menu `SpellBook/Pooling/...` | 0M-23 |
| `SpellBook.MathTools.Math.MathTools` | `SpellBook.Core.MathTools` | 0M-24 |
| `SpellBook.Core.NewEventSystem.Void`, `SpellBook.Core.RuntimeSets.RuntimeSet<T>` | `SpellBook.Core.Void`, `SpellBook.Core.RuntimeSet<T>` | 0M-25 |
| `GameEvent`/`GameEventListner` (Core/Events), `VoidEvent`, `IntEvent`, `FloatEvent`, `StringEvent`, `ComponentEvent` e listeners, `EventManager`, `GameEvent`/`GameEventListener` por string | `VoidEventChannel` etc., `EventListener`, `EventEmitter`, `EventBus<T>`; não há canal de `Component`; assets de evento precisam ser recriados | 0M-25 a 0M-32 |
| `AbilityActivatedEvent`, `AbilitySlotUpdateEvent` globais | removidos; usar `ASC.OnAbilityActivated`; relay no B-004 (R22a) | 0M-12 |
| `Runtime/GAS/GAS_Documentation.md` | página GAS do manual | 0M-10 |

---

## 8. Tickets

Ordem por dependência. **WIP 1**: um ticket com Unity em implementação por vez (exceção para tickets sem Editor logo abaixo). Nenhum ticket está bloqueado depois da revisão sem Odin. "Linhas" é a estimativa de código, sem páginas e fixtures (ADR 0003). Todos os tickets de código usam a trava (0005) e terminam com a suíte inteira verde no host. A exceção é o 0M-44, que termina com o host compilando, porque ainda não existe suíte.

**Paralelo sem Editor** (emenda da 0003, 2026-10-29): o WIP 1 vale para os tickets que usam a Unity e a trava. Um ticket de docs, processo ou CI, sem trava, pode rodar em paralelo, na própria branch e numa cópia de trabalho própria (0006 adiada), desde que não toque arquivo do outro ticket. Pares marcados nesta Spec (revisão de 2026-10-29). Outros pares só valem se forem marcados aqui.

- ~~**0M-11 em paralelo com o 0M-41.**~~ Sem objeto: o 0M-11 foi cancelado (2026-10-29).
- **0M-08 em paralelo com o 0M-42, o 0M-46, o 0M-43, o 0M-48 ou o 0M-53** (decisão do usuário de 2026-10-29; o 0M-08 entra logo depois do 0M-47). Nenhum desses tickets toca os arquivos do 0M-08. O 0M-08 usa a Unity e a trava só no passo de `DocUrls.cs`, `DocUrlsTests.cs` e `package.json`, que roda na vez da trava, sem coincidir com a suíte do outro ticket. O resto (`mkdocs.yml`, páginas, `README.md`) não usa a Unity. As partes que precisam da Unity são `Runtime/Core/DocUrls.cs` e `Tests/Core/EditMode/DocUrlsTests.cs`, porque compilam no pacote e rodam na suíte com a trava, e o campo `unity` do `package.json`, que o host resolve e que o `Package_DeclaresUnity6000_0` testa. O resto (`mkdocs.yml`, `requirements-docs.txt`, `index.md`, `instalacao.md` e `README.md`) não precisa da Unity, mas fica no mesmo ticket.
- **0M-09 em paralelo com o 0M-42, o 0M-46, o 0M-43, o 0M-48, o 0M-53 ou o 0M-12**, depois do merge do 0M-08. O 0M-09 só cria `.github/workflows/docs.yml`, e nenhum desses tickets toca `.github/`. O passo do usuário (ligar o Pages) não usa a Unity.
- **0M-10 em paralelo com o 0M-42, o 0M-46, o 0M-43, o 0M-48 ou o 0M-53**, depois do merge do 0M-08. O 0M-10 apaga `Runtime/GAS/GAS_Documentation.md` e o `.meta` dele sem o Editor e cria `Documentation~/manual/modulos/gas.md`. O 0M-42 e o 0M-46 mexem em outros arquivos de `Runtime/GAS/` e em `manual/inicio/inspector.md`, mas nenhum desses arquivos é do 0M-10. **O 0M-10 nunca roda junto com o 0M-12**: o 0M-12 depende dele e edita o `gas.md` que o 0M-10 cria, então o 0M-10 precisa estar no `main` antes de o 0M-12 começar.
- **0M-09 em paralelo com o 0M-10.** Os dois não usam a Unity e não têm arquivo em comum.
- Um par cai se um dos tickets precisar de um arquivo fora da lista dele. O caso provável é a navegação do `mkdocs.yml`. Nesse caso, quem roda em paralelo volta para a fila.

Na ordem atual, o 0M-08 vem logo depois do 0M-47 (decisão do usuário, 2026-10-29; ele depende só do 0M-07 e nenhuma dependência muda). Depois do merge do 0M-08, o 0M-09 e o 0M-10 ficam livres e rodam em paralelo com a trilha do Inspector (0M-42, 0M-46, 0M-43, 0M-48, 0M-53). Regra fixa: **o 0M-10 nunca roda junto com o 0M-12**; o 0M-09 pode rodar com o 0M-12.

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
| 0M-11 | `ci(test): rodar as suítes na 6000.0 e na última 6000.x com GameCI` | 0M-07 | ~~CA56, CA57, N1~~ | **cancelado** | Decisão do usuário (2026-10-29): sem CI de testes e sem secrets da Unity; verificação só no host (D5, emenda da 0010). Nenhum ticket dependia dele na ordem de dependências; os CAs que ele levava foram redistribuídos: CA56 para o 0M-09, N1 para o DevOps no release, CA57 cancelado no PRD. Issue #12 e PR #64 fecham sem merge |
| 0M-41 | `feat(core): catálogo do seletor de tipo e atributos TypePicker/TypeCategory` | 0M-07 | CA63 (parte) | **novo** | I45: parte lógica do seletor que substitui o do Odin (filtro, categorias, busca, referência compartilhada); renomeia o asmdef do Editor para `Core.Editor`. Exceção de tamanho (~440 linhas) aprovada em 2026-10-29. O tipo ausente saiu para o 0M-53 (D6, aprovado em 2026-10-29; passou do 0M-48 para o 0M-53 na divisão por tamanho) |
| 0M-47 | `feat(core): popup de seleção de tipo em UI Toolkit` | 0M-41 | CA63 (parte: busca e categorias) | **novo** (2ª rodada) | I48: substitui o `AdvancedDropdown`; o popup próprio não cabia no 0M-42 |
| 0M-42 | `feat(core): drawer do seletor de tipo e uso no GAS` | 0M-47, 0M-41 | CA63 | **novo** | I45/I46: drawer UI Toolkit do seletor; devolve ao GAS a escolha de tipo nos 10 campos `[SerializeReference]`. 2ª rodada: abre o popup do 0M-47 |
| 0M-46 | `feat(core): atributo [ShowIf] e uso no GAS` | 0M-42 | — | **novo** | I45: único atributo de layout da lista inicial; devolve ao GAS o `ShowIf` por campo irmão |
| 0M-43 | `feat(core): interface de validação IValidatable` | 0M-07 | R32 (parte) | **novo** | I45: substitui `ISelfValidator`; precisa existir antes do primeiro validador (0M-23) |
| 0M-48 | `feat(core): Inspector base SpellBookEditor com validação no topo` | 0M-43, 0M-41 | R32, CA66 | **novo** (2ª rodada) | I48: substitui o gancho IMGUI; base dos Inspectors com botão e dos editors de uma linha. Divisão por tamanho (2026-10-29): o tipo ausente (D6) saiu para o 0M-53; o 0M-48 deixa o contêiner `spellbook-top-notices` e `RefreshTopNotices()` como ponto de extensão |
| 0M-53 | `feat(core): tipo ausente por objeto no Inspector do SpellBook` | 0M-48 | R45 (Inspector) | **novo** (divisão do 0M-48) | D6: `MissingReferenceTypes`, o `HelpBox` com "Limpar referência" no topo do `SpellBookEditor` e a seção **Tipo não encontrado**; confirma o risco 28. Issue #74 |
| 0M-08 | `docs(manual): criar o manual, DocUrls, Instalação e declarar Unity 6000.0` | 0M-07 | CA4 (parte), CA37 (parte), CA42 | alterado | Cria a página Instalação (vinha do 0M-07) sem Odin; README sem Odin. 2026-10-29: a seção "Compatibilidade" cita só a versão do host (0M-11 cancelado) |
| 0M-09 | `ci(manual): publicar o manual no GitHub Pages` | 0M-08 | CA39, CA56 | alterado | Recebe o CA56 do 0M-11 cancelado (2026-10-29): o `docs.yml` passa a ser o único workflow, e o critério cobra nenhum Odin/Sirenix e nenhuma credencial escrita |
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
| 0M-22 | `refactor(gas)!: esperas do GAS pelo relógio do ASC` | 0M-21 | CA10, CA32 (parte) | alterado | Emenda de 2026-10-30: quebras na Migração (+1 arquivo, 10), `Duration <= 0` espera um quadro, teste da invulnerabilidade; exceção de tamanho (~480 linhas, teto 500) |
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
| 0M-38 | `feat(core): validar projeto por menu e por teste` | 0M-37, 0M-33, 0M-48, 0M-53 | CA34, CA35, R32, R33, R34, CA64 | alterado | `IValidatable`; janela UI Toolkit; erro de tipo ausente; o rename do asmdef sai (foi para o 0M-41). 2ª rodada: sai o gancho do Inspector (as mensagens no Inspector vêm do `SpellBookEditor`, 0M-48). D6: o erro de tipo ausente usa `MissingReferenceTypes` do 0M-53 |
| 0M-39 | `docs(gas): HelpURL nos tipos restantes e teste de HelpURL` | 0M-38 | CA36, CA37 | igual | — |
| 0M-49 | `feat(editor): Inspectors de uma linha para Core, Time, Tags e Pooling` | 0M-39, 0M-48 | R32 | **novo** (2ª rodada) | I48: todo tipo do pacote usa o `SpellBookEditor`; cria `Time.Editor`, `Tags.Editor` e `Pooling.Editor` |
| 0M-50 | `feat(editor): Inspectors de uma linha para GAS e Events e cobertura completa` | 0M-49 | R32 | **novo** (2ª rodada) | I48: completa a cobertura; cria `GAS.Editor` |
| 0M-40 | `refactor(core)!: Core em Runtime/Core e verificação final da estrutura` | 0M-50 | CA1, CA32, CA54, CA6, N4 | alterado | Lista final de asmdefs sem `Setup.Editor` e com `Core.Editor`, `Events.Editor`, `Time.Editor`, `Tags.Editor`, `Pooling.Editor` e `GAS.Editor`; depende do 0M-50 |

Ordem de execução linear: 01 → 02 → 03 → 04 → 05 → 06 → **44** → 07 → ~~11~~ (cancelado) → **41** → **47** → 08 → **42** → **46** → **43** → **48** → **53** → 09 → 10 → 12 → 13 → … → 31 → 32 → **45** → 33 → 34 → 35 → 36 → 37 → 38 → 39 → **49** → **50** → 40.

Os ids novos não seguem a ordem de execução (0M-47 vem antes do 0M-42), porque o 0M-42 já tinha sido divulgado na primeira rodada.

Por que essa ordem no começo:
- o Core só compila no host sem Odin depois do 0M-44, e nenhum teste roda antes disso (com erro de compilação, a Unity não recarrega o domínio). Por isso o 0M-44 é o primeiro ticket depois do host;
- a guarda "nenhum Sirenix" vem logo depois, no 0M-07, junto com o primeiro asmdef de teste;
- ~~o CI (0M-11) vem logo depois do 0M-07~~: o 0M-11 foi cancelado (2026-10-29). Os tickets com risco de API (0M-41, 0M-47, 0M-42, 0M-46, 0M-48, 0M-53) são verificados no host 6000.3.13f1 e na revisão do item 3 da 0010 (D5, risco 27);
- o manual (0M-08) vem logo depois do 0M-47, por decisão do usuário de 2026-10-29. Ele depende só do 0M-07 e nenhuma dependência muda. Assim, o 0M-09 e o 0M-10 (que dependem dele) ficam livres durante a trilha do Inspector e podem rodar em paralelo com ela (seção "Paralelo sem Editor");
- o seletor (0M-41, 0M-42) e o `[ShowIf]` (0M-46) entram antes de qualquer ticket que crie ou mexa em `[SerializeReference]`, e antes do 0M-12 mover o GAS de assembly;
- a validação (0M-43) entra antes do primeiro validador (0M-23);
- o `SpellBookEditor` (0M-48) entra logo depois, para os Inspectors do Events (0M-45) herdarem dele, e o tipo ausente (0M-53) logo depois dele, porque pluga no ponto de extensão do 0M-48 e o validador (0M-38) reusa o `MissingReferenceTypes`;
- os editors de uma linha (0M-49, 0M-50) ficam para o fim, quando todos os tipos existem, e o teste de cobertura fecha a regra antes da verificação final (0M-40).

CAs que ficam com o DevOps no release (não são de ticket): CA4 (`version` = `3.0.0-pre.1`), CA26 (CHANGELOG com a seção 7 e a nota do R22a) e N1 (veio do 0M-11 cancelado, 2026-10-29). O N1 é uma propriedade do pacote inteiro na hora do release, não de um ticket: no commit do release, o `package.json` declara `"unity": "6000.0"` (campo trocado no 0M-08), o host `HostProject~/` na versão da Spec compila o pacote sem avisos novos do SpellBook e roda a suíte inteira verde (emenda "sem CI de testes" da 0010: release exige a suíte inteira verde no host). A saída da suíte vai na descrição do PR do release. Cada ticket já termina com a suíte verde no host; o release só confere de novo no commit final.

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
- Objetivo: o pacote compila numa Unity sem Odin, sem mudar dado serializado. O ticket verifica no host (6000.3.13f1, D5). (A frase "a 6000.0 fica com o CI (0M-11)" deixou de valer: 0M-11 cancelado em 2026-10-29.)
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
  - **Regra de busca do CA65 (decidida em 2026-10-29, achado da Fábrica):** os nomes dos testes acima contêm `Sirenix`, e uma busca por substring faria a guarda falhar no próprio arquivo. A regra aceita é a da Fábrica, com um ajuste:
    - cada termo (`Sirenix`, `ODIN_INSPECTOR`) só conta no **início de um identificador**: `(?<![A-Za-z0-9_])` antes do termo, sem borda à direita, sensível a maiúsculas. Sem a borda à direita, a busca pega também `ODIN_INSPECTOR_3`, `ODIN_INSPECTOR_3_1` e um eventual `SirenixX`, que a borda dos dois lados deixaria passar;
    - o `NoOdinTests.cs` monta os dois termos por partes (por exemplo `"Sire" + "nix"`), para que nenhum literal do arquivo case com a regra;
    - os testes **não** são renomeados (o nome continua dizendo o que guardam) e o arquivo de teste **não** é excluído da busca;
    - a regra fica numa função pura do `NoOdinTests` (ou do `PackageFiles`), coberta por um teste de casos: casa `using Sirenix.OdinInspector;`, `Sirenix.Serialization.dll`, `"Assets/Plugins/Sirenix/"`, `#if ODIN_INSPECTOR` e `ODIN_INSPECTOR_3_1`; não casa `NoOdin_NoSourceOrPackageFileMentionsSirenix` nem `NoOdin_NoAsmdefReferencesSirenixOrOdinSymbol`.
  - Guardas: CA2 e CA3.
- Testes no batchmode: os caminhos de `-testResults` e `-logFile` seguem o risco 35. Um `Temp/` (com `.meta`) que uma rodada anterior tenha deixado na raiz do pacote é apagado, não entra no commit, e a Fábrica confirma que a raiz fica sem ele depois da rodada.
- Manual: nenhuma (sem mudança visível ao usuário).
- Tamanho: ~250 linhas (mais ~30 do teste de casos da regra de busca), 7 arquivos.
  - **Tamanho real e decisão (2026-10-29, achado do Examinador, ADR 0003 item 4):** o diff tem **440 linhas** e 7 arquivos (`PackageFiles.cs` 150, `AsmdefRulesTests.cs` 140, `NoOdinTests.cs` 87, `RuntimeReferencesTests.cs` 36, asmdef de testes 23, os dois asmdefs alterados 4). O Arquiteto aceitou o excesso só neste ticket, e o **usuário aprovou a exceção em 2026-10-29**. O limite de ~400 da 0003 não muda para os outros.
    - Motivo: o excesso é de cerca de 10% e todo ele é código de teste. Vem do que esta Spec pediu e a estimativa não contou: as regras por tipo de asmdef da seção 3, o teste de varredura e o teste de casos da regra de busca. O ticket entrega um comportamento só (as guardas de estrutura sobre a mesma leitura de asmdefs), com 16/16 testes verdes.
    - Dividir não ajuda a revisão. Seria preciso tirar do diff as regras por tipo e o `RuntimeReferencesTests` (~110 linhas) e reabrir o `AsmdefRulesTests.cs` e o `PackageFiles.cs` num ticket novo. Isso custa mais um ciclo com Unity em WIP 1 e reescreve código que já cumpre a Spec.
    - **Plano de divisão descartado (0M-51 não existe):** o usuário aprovou a exceção em 2026-10-29, então o 0M-07 segue inteiro e não se cria o 0M-51. O texto abaixo fica só como registro do plano que não foi usado: o 0M-07 mantém o asmdef de testes, os dois asmdefs, o `PackageFiles.cs` sem o `AsmdefKind`, o `NoOdinTests.cs`, o teste de varredura e `AsmdefRules_AllOverrideReferences_PrecompiledInAllowList` (com a lista permitida do asmdef de testes lida pelo caminho em `Tests/`), mais `AsmdefRules_ModuleReferences_FollowDependencyMap`. Os testes `AsmdefRules_RuntimeAsmdefs_*`, `AsmdefRules_EditorAsmdefs_*` e `AsmdefRules_TestAsmdefs_*`, o `AsmdefKind` e o `RuntimeReferencesTests.cs` (CA3) vão para o **0M-51** (`test(core): regras por tipo de asmdef e runtime sem UnityEditor`), que depende do 0M-07, vem antes do 0M-11 e mede ~120 linhas em 3 arquivos.

### 0M-11 — `ci(test): rodar as suítes na 6000.0 e na última 6000.x com GameCI` (cancelado)
- **Cancelado (decisão do usuário, 2026-10-29):** "tira essa regra. A ideia é que o sistema funciona a partir do 6000, o que já vai funcionar". O usuário não cadastra `UNITY_LICENSE`, `UNITY_EMAIL` e `UNITY_PASSWORD`, então o workflow não roda. A verificação é só no host 6000.3.13f1 (D5, emenda "sem CI de testes" da 0010). Nenhum ticket dependia dele na ordem de dependências. A issue #12 e o PR #64 fecham sem merge; o `.github/workflows/tests.yml` não entra no `main`. Os CAs dele: o CA57 foi cancelado no PRD; o CA56 (texto novo do PRD) passou para o **0M-09**, cujo `docs.yml` fica como único workflow; o N1 passou para os **CAs do DevOps no release** (seção 8, depois de "Por que essa ordem"). O texto abaixo fica só como registro.
- **Alterado (revisão sem Odin):** **desbloqueado**, porque a P21 perdeu o objeto (I45). Saem o script e os secrets do Odin e o passo de importação extra.
- **Adiantado (D5, aprovado em 2026-10-29):** vem logo depois do 0M-07, para que os tickets com risco de API da 6000.0 (0M-41, 0M-47, 0M-42, 0M-46, 0M-48, 0M-53) já tenham o job 6000.0 no PR. A seção "Compatibilidade" da Instalação passou para o 0M-08, porque a página ainda não existe neste ponto.
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
- Objetivo: decidir, por função pura e testada, quais tipos o seletor oferece para um campo `[SerializeReference]`, como agrupá-los e como tratar referência compartilhada (0026, item 4; seção 4.10; risco 29). O tipo ausente saiu deste ticket e foi para o 0M-53 (D6; passou pelo 0M-48 antes da divisão por tamanho).
- Arquivos:
  - `Runtime/Core/Inspector/TypePickerAttribute.cs`;
  - `Runtime/Core/Inspector/TypeCategoryAttribute.cs`;
  - `Editor/SpellBook.Editor.asmdef` → `Editor/Core/SpellBook.Core.Editor.asmdef` (renomeado e movido com o `.meta`, GUID mantido; o rename saiu do 0M-38);
  - `Editor/FolderStructureCreator.cs` → `Editor/Core/FolderStructureCreator.cs` (movido com o `.meta`);
  - `Editor/Core/Inspector/TypePickerCatalog.cs` (namespace `SpellBook.Core.Editor.Inspector`), sem `TryGetMissingType` (D6);
  - `Tests/Core/EditMode/SpellBook.Core.Tests.EditMode.asmdef` (+`SpellBook.Core`, +`SpellBook.Core.Editor`);
  - `Tests/Core/EditMode/Inspector/TypePickerCatalogTests.cs`, com os tipos de teste no mesmo arquivo: base abstrata, concreto, concreto com `[TypeCategory]`, genérico, abstrato, sem `[Serializable]` e sem construtor sem parâmetros. Sem pasta temporária: todos os testes deste ticket usam objetos em memória;
  - `Tests/Core/EditMode/Inspector/PickHolder.cs`: o `ScriptableObject` de teste, num arquivo com o nome da classe. Fica neste ticket porque o `Assign` e o `EnsureUnique` usam ele, e o 0M-53 reaproveita ele no asset em disco do tipo ausente.
- Depende de: 0M-07.
- CAs: CA63 (T, parte: filtro, categorias e busca). O tipo ausente no Inspector é do 0M-53 (R45, D6), e o erro do validador continua no 0M-38 (CA64).
- Testes red (o tipo `TypePickerCatalog` não existe):
  - ~~`MissingType_IsReported_DataKeptUntilClear`~~: **saiu** (D6). O `TryGetMissingType` por campo não é viável (a Unity deixa o campo com `managedReferenceId = -2` e não o liga ao tipo ausente). O teste volta no 0M-53 como `MissingReferenceTypesTests`, por objeto. A Fábrica tira deste ticket o teste, o `TryGetMissingType`, a escrita do YAML e a pasta temporária;
  - `Candidates_ExcludeAbstractGenericUnityObjectNonSerializableAndNoDefaultCtor`;
  - `Candidates_IncludeConcreteBaseItself`;
  - `Category_FromTypeCategory_RootWhenMissing`;
  - `Summary_FromTypeCategory`;
  - `Filter_ByDisplayNameTypeNameAndCategory_CaseInsensitive`;
  - `Assign_CreatesInstanceOfChosenType_NullClears` (SO de teste em memória + `SerializedObject`);
  - `EnsureUnique_SharedReferenceInList_BecomesIndependentCopy` (a mesma instância posta duas vezes na lista por código; depois da chamada, mudar um elemento não muda o outro).
- Manual: nenhuma página neste ticket (nenhum campo usa o seletor ainda). A página **Inspector** nasce no 0M-42 com o drawer.
- Critério de pronto: os testes do catálogo passam no host (sem CI, 2026-10-29); `TypePickerCatalog` não tem API de tipo ausente.
- Tamanho: **~390 linhas** estimadas depois da D6 (a Fábrica mediu 463 linhas físicas em 6 arquivos com o tipo ausente; saem ~70: `TryGetMissingType` ~20, o teste com o YAML ~40, a pasta temporária no `SetUp`/`TearDown` ~10), 8 arquivos na lista do ticket (os 2 movidos com `.meta` não somam linhas). A Fábrica confirma a medição. A **exceção de ~440 linhas aprovada em 2026-10-29, só para este ticket**, continua cobrindo o ticket se a medição passar de 400; nenhuma exceção nova é pedida. O limite de ~400 da 0003 não muda para os outros.
- **Decisões da implementação (2026-10-29, achados da Fábrica):**
  - **`PickHolder` em arquivo próprio.** A Unity só liga um `ScriptableObject` salvo em disco ao script quando o arquivo tem o nome da classe. Com o `PickHolder` dentro do `TypePickerCatalogTests.cs`, o `LoadAssetAtPath<PickHolder>` devolve `null` depois do reimport, e o risco 28 fica sem verificação. Aceita a proposta da Fábrica: o `PickHolder` vai para `Tests/Core/EditMode/Inspector/PickHolder.cs` já neste ticket. Os outros tipos de teste continuam no arquivo de testes, porque não são `UnityEngine.Object`.
  - **Tamanho: exceção aprovada.** A Fábrica mediu ~430 linhas em 7 arquivos, e o `PickHolder.cs` leva o total a ~440. Não há corte que preserve a cobertura (só sobrariam os comentários XML da API pública, usados pelo drawer e pelo popup, e juntar testes violaria "um comportamento por teste"). A divisão proposta (ticket 0M-52 só com o tipo ausente) foi **desfeita**: o usuário decidiu em 2026-10-29 não dividir e aceitar a exceção de ~440 linhas só para o 0M-41. O 0M-52 não existe. (A frase "o tipo ausente fica neste ticket" foi substituída pela D6 abaixo.)
  - **Tipo ausente por objeto e fora do ticket (D6, 2026-10-29, aguarda aprovação do usuário).** No red, a Fábrica mediu na 6000.3.13f1 que o campo com tipo ausente fica com `managedReferenceId = -2` e `managedReferenceFullTypename` vazio, que `GetManagedReferencesWithMissingTypes` devolve o tipo com outro `referenceId`, e que nenhuma API pública liga os dois; o teste `MissingType_IsReported_DataKeptUntilClear` falhou (23/24). A Spec adota o relato por objeto (alternativa A da Fábrica; B e C recusadas, ver D6). Como quem mostra o aviso é o topo do Inspector, a API (`MissingReferenceTypes`) e o teste foram para o 0M-48, que já desenha esse topo; depois a divisão do 0M-48 por tamanho (2026-10-29) levou o tipo ausente para o 0M-53. Se o usuário preferir manter o tipo ausente no 0M-41, o ticket recebe `MissingReferenceTypes.cs` (API da 4.10), `MissingReferenceTypesTests.cs` e a pasta temporária de volta, fica com ~470 linhas em 10 arquivos e precisa de uma **exceção nova** (a aprovada é de ~440).
  - **Asmdef de testes** referencia `SpellBook.Core`, além de `SpellBook.Core.Editor`. Os tipos de teste usam `[TypeCategory]`, e com `overrideReferences: true` a referência precisa ser explícita.
  - **Namespace** do catálogo e do `TypePickerEntry`: `SpellBook.Core.Editor.Inspector`, espelhando o `SpellBook.Core.Inspector` dos atributos. Os testes ficam em `SpellBook.Core.Tests.Inspector`.

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
- Tamanho: ~500 linhas (`TypePickerTree` 163, `TypePickerPopup` 223, testes 108), 3 arquivos. **Exceção de tamanho aprovada pelo usuário em 2026-10-29, só neste ticket** (a estimativa anterior, ~320, ficou curta depois do red). Nenhum outro ticket herda a exceção.

### 0M-42 — `feat(core): drawer do seletor de tipo e uso no GAS` (novo)
- **Novo (revisão sem Odin, I45/I46).** Na 2ª rodada (I48), o botão abre o `TypePickerPopup` do 0M-47 em vez do `AdvancedDropdown`. Depois de tirar a lista do ticket, o tamanho continua dentro do limite.
- Objetivo: os campos `[SerializeReference]` voltam a ser editáveis pelo Inspector: escolher, trocar e limpar. O tipo ausente não aparece no drawer (D6): vem no topo do Inspector, no 0M-53.
- Arquivos:
  - `Editor/Core/Inspector/TypePickerDrawer.cs` (UI Toolkit, seção 4.10; abre o `TypePickerPopup`);
  - `Tests/Core/EditMode/Inspector/TypePickerUsageTests.cs`;
  - `Runtime/GAS/Abilities/GameplayAbility.cs`, `Runtime/GAS/Effects/GameplayEffect.cs`, `Runtime/GAS/Abilities/AbilityDescription.cs`, `Runtime/GAS/Abilities/Actions/FlowActions.cs` e `Runtime/GAS/Abilities/Actions/SpawnProjectileAction.cs` (`[TypePicker]` nos 10 campos da seção 1.1);
  - `Documentation~/manual/inicio/inspector.md`.
- Depende de: 0M-47, 0M-41 (catálogo: `ResolveFieldBaseType`, `Assign`, `EnsureUniqueInArray`).
- CAs: CA63.
- Testes:
  - Red: `EverySerializeReferenceFieldInRuntime_HasTypePicker_ExceptEventEmission` falha. O teste percorre por reflexão os assemblies `SpellBook.*` carregados que não são de teste, sem referência de compilação ao GAS. Os 10 campos do GAS não têm o atributo. A regra vale para o pacote inteiro daqui em diante.
  - A UI não tem teste automatizado, porque o bind do UI Toolkit precisa de painel. O cenário no host (seção 5) cobre: adicionar uma `WaitAction` numa habilidade pelo seletor, buscar "wait", salvar, reabrir e conferir; duplicar o elemento e editar a cópia sem mudar o original; o `OnGUI` mostra o `HelpBox`.
  - O red também confirma que `CreatePropertyGUI` com `TrackPropertyValue` funciona na 6000.0 (risco 27).
  - No cenário do host entram também: o popup abre ancorado no botão, a busca filtra, a navegação por categoria funciona e o teclado segue I-o.
- Manual: **Inspector** (conteúdo real):
  - o seletor, o popup, a busca e as categorias;
  - `[MovedFrom]` para tipos próprios (renomear sem perder dados). A seção "Tipo não encontrado" vem no 0M-53 (D6);
  - limite de um drawer por campo;
  - Inspectors IMGUI e de terceiros (risco 26).
- Tamanho: ~200 linhas (drawer ~130 sem o `HelpBox` de tipo ausente, teste ~50, GAS ~20), 8 arquivos.

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

  O red decidiu pela forma (b) do drawer (4.10, risco 25): o `PropertyField` público da 6000.3 só tem os construtores `(property)` e `(property, label)` e não pula o drawer. O `ShowIfDrawer` monta o campo pelo tipo da propriedade e nunca cria `PropertyField` da própria propriedade. Consequência: nenhum campo junta `[ShowIf]` e `[TypePicker]` (um drawer por campo), e nenhum campo do GAS junta os dois.

  Cenário no host: trocar `Type` do `ModifierValueSource` mostra só o campo do modo. Observação do Examinador: o "avisa uma vez" do irmão ausente é contado **por instância do atributo** (`ShowIfDrawer.cs:18`), não por campo serializado nem por objeto. No host, o aviso pode repetir em outro campo com o mesmo atributo; conferir o log com isso em mente.
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
- **Dividido por tamanho (decisão do usuário, 2026-10-29):** o ticket original media ~582 linhas (código e teste). O tipo ausente (D6) foi para o **0M-53**; este ticket fica com o `SpellBookEditor` e os testes dele (~371 linhas).
- Objetivo: um Inspector base em UI Toolkit que mostra o Inspector padrão e as mensagens de `IValidatable` no topo, com pontos de extensão para os botões dos módulos e **um ponto de extensão no topo para o tipo ausente do 0M-53** (0026, item 3a; seção 4.10).
- **Ponto de extensão para o 0M-53 (sem reescrever o editor):** o `CreateInspectorGUI` já cria, como primeiro filho, o contêiner `spellbook-top-notices` (antes do contêiner de validação), e o método privado `RefreshTopNotices()` o limpa e o esconde quando não tem filho. Aqui ele não coloca aviso nenhum. O 0M-53 só acrescenta o corpo desse método (um `HelpBox` por tipo ausente) e a chamada dele depois de "Limpar referência"; não muda a ordem do build, o contêiner de validação nem a API de extensão.
- Arquivos:
  - `Editor/Core/Inspector/SpellBookEditor.cs`;
  - `Tests/Core/EditMode/Inspector/SpellBookEditorTests.cs`. No mesmo arquivo ficam um SO de teste `IValidatable` com o editor de uma linha dele, um SO não validável e um editor de teste que sobrescreve `BuildAfterDefault`;
  - `Documentation~/manual/inicio/inspector.md` (seção **Validação no Inspector**; a **Tipo não encontrado** é do 0M-53).
- Depende de: 0M-43, 0M-41 (o asmdef de testes com `Core.Editor`).
- CAs: R32 (o texto novo do PRD), CA66 (S: mensagem no topo do Inspector igual à da janela). O R45 (Inspector) é do 0M-53.
- Testes red do Inspector (o tipo `SpellBookEditor` não existe):
  - `CreateInspectorGUI_TopNoticesContainerFirstAndHidden_WhenNoNotices`;
  - `CollectMessages_InvalidTarget_ReturnsError`;
  - `CollectMessages_ValidTarget_ReturnsNone`;
  - `CollectMessages_NonValidatable_ReturnsNone`;
  - `CollectMessages_ExceptionInValidate_BecomesError`;
  - `CreateInspectorGUI_ValidationContainerFirst_WithOneHelpBoxPerMessage`;
  - `CreateInspectorGUI_NonValidatable_ContainerHidden`;
  - `CreateInspectorGUI_AfterDefaultExtensionIsLast`.

  Os testes conferem a estrutura, sem bind (risco 33). Cenários no host:
  - depois do 0M-49, um `PooledSpawnerAsset` com `initialSize` negativo mostra o erro no topo, e o erro some ao corrigir o valor;
  - o cenário do tipo ausente (risco 28) é do 0M-53.
- Critério de pronto: os testes passam no host (sem CI, 2026-10-29); o `SpellBookEditor` não referencia `MissingReferenceTypes`; `spellbook-top-notices` existe e fica escondido.
- Manual: Inspector, seção **Validação no Inspector** (onde aparece, botão Revalidar, como um Inspector próprio herda de `SpellBookEditor`). A seção **Tipo não encontrado** é do 0M-53.
- Tamanho: ~371 linhas (`SpellBookEditor.cs` 154, `SpellBookEditorTests.cs` 217; a medição do usuário, que a Fábrica confirma), 3 arquivos. Dentro do limite da 0003.

### 0M-53 — `feat(core): tipo ausente por objeto no Inspector do SpellBook` (novo, divisão do 0M-48)
- **Novo (2026-10-29, decisão do usuário, issue #74).** Vem do 0M-48, que passava de 400 linhas. É a D6 inteira que sai do 0M-48.
- Objetivo: o designer vê no topo do Inspector de um objeto com `[SerializeReference]` de tipo removido ou renomeado um `HelpBox` de erro por tipo ausente, com assembly e classe, e só limpa a referência por botão com confirmação (0026, item 4 com a emenda da D6; seção 4.10, "Tipo ausente"; risco 28).
- Arquivos:
  - `Editor/Core/Inspector/MissingReferenceTypes.cs` (`MissingTypeEntry`, `Get`, `Clear`, `FormatMessage`; seção 4.10);
  - `Editor/Core/Inspector/SpellBookEditor.cs` (editado): o corpo de `RefreshTopNotices()` e a chamada dele depois de "Limpar referência"; nenhuma outra parte muda;
  - `Tests/Core/EditMode/Inspector/MissingReferenceTypesTests.cs` (pasta temporária `Assets/__SpellBookTestTemp` e limpeza no `TearDown`, risco 23; usa o `PickHolder` do 0M-41 para o asset em disco);
  - `Tests/Core/EditMode/Inspector/SpellBookEditorMissingTypeTests.cs` (novo, para não reabrir o arquivo de testes do 0M-48; o editor de teste que só herda de `SpellBookEditor` fica nele);
  - `Documentation~/manual/inicio/inspector.md` (seção **Tipo não encontrado**).
- Depende de: 0M-48 (`SpellBookEditor` e o `spellbook-top-notices`), 0M-41 (`PickHolder`).
- CAs: R45 (parte do Inspector: erro com assembly e classe, botão para limpar, nunca limpa sozinho). O CA64 continua no 0M-38.
- Testes red de tipo ausente (o tipo `MissingReferenceTypes` não existe), em `MissingReferenceTypesTests`, com o asset em YAML gravado na pasta temporária, apontando para uma classe que não existe:
  - `Get_AssetWithMissingType_ReturnsAssemblyAndFullName` (uma entrada, com o assembly e o `namespace.Classe` do YAML);
  - `Get_AfterReimport_DataStillThere` (a Unity guarda os dados do tipo ausente: depois de `ImportAsset` com `ForceUpdate`, `Get` ainda devolve a entrada e o arquivo ainda tem os dados; risco 28);
  - `Clear_RemovesOnlyThatReference_AndReturnsTrue` (depois de `Clear` e de salvar, `Get` fica vazio e o arquivo não tem mais a classe);
  - `Get_ObjectWithoutMissingType_ReturnsEmpty` (o `PickHolder` em memória, e alvo nulo);
  - `FormatMessage_HasAssemblyAndFullName_NamespaceOptional`.

  Esses testes confirmam na versão do host (6000.3.13f1) o comportamento medido pela Fábrica; a 6000.0 não é testada (D5, sem CI desde 2026-10-29).
- Testes red do Inspector, em `SpellBookEditorMissingTypeTests` (sem o plug, `spellbook-top-notices` fica vazio e só o `MissingType_HelpBox...` falha; o `NoMissingType...` passa sem o plug e fica como guarda):
  - `CreateInspectorGUI_MissingType_HelpBoxWithClearButtonInTopNotices` (o asset em disco; confere a estrutura, sem bind, risco 33; o botão não é clicado, porque abre o diálogo de confirmação; o contêiner vem antes do de validação);
  - `CreateInspectorGUI_NoMissingType_TopNoticesHidden`.
- Cenário no host (risco 28), com scripts temporários no host, sem commit (como o red do 0M-07): um SO com um campo `[SerializeReference, TypePicker]`, o editor de uma linha dele (`: SpellBookEditor`) e uma classe de dados escolhida no campo. Apagar a classe de dados: o topo mostra "Tipo não encontrado" e o campo mostra "(Nenhum)"; voltar a classe recupera os dados; apagar de novo e usar "Limpar referência": pede confirmação e remove o aviso. O Examinador registra duas observações para o manual: se `Ctrl+Z` recupera a limpeza, e o que acontece com os dados ausentes se um tipo for escolhido no campo "(Nenhum)" sem limpar antes.
- Manual: Inspector, seção **Tipo não encontrado** (o aviso fica no topo do Inspector, não no campo, que aparece como "(Nenhum)"; restaurar o tipo recupera os dados; limpar apaga; o que o cenário observou sobre Undo e sobre escolher um tipo antes de limpar; link para `[MovedFrom]`). A frase de que o tipo próprio sem `SpellBookEditor` só aparece no validador não entra aqui: o validador nasce no 0M-38 e o manual não cita o que ainda não existe (CA54).
- Critério de pronto: os testes passam no host (sem CI, 2026-10-29); o `HelpBox` só aparece para tipo ausente; nada no pacote chama `Clear` fora do botão.
- Tamanho: ~240 linhas (`MissingReferenceTypes.cs` 69 e `MissingReferenceTypesTests.cs` 142, que são os ~211 medidos pelo usuário, mais ~30 do plug no `SpellBookEditor` e do teste de estrutura; a Fábrica confirma), 5 arquivos. Dentro do limite da 0003.

### 0M-08 — `docs(manual): criar o manual, DocUrls, Instalação e declarar Unity 6000.0`
- **Alterado (revisão sem Odin):** passa a **criar** a página Instalação, que vinha do 0M-07, já sem Odin. O README perde o "Odin obrigatório".
- Objetivo: esqueleto do site, constante única de URL, `package.json` e README.
- Arquivos: `Documentation~/mkdocs.yml`, `Documentation~/requirements-docs.txt`, `Documentation~/manual/index.md`, `Documentation~/manual/inicio/instalacao.md` (criada, com âncoras), `Runtime/Core/DocUrls.cs`, `package.json` (`unity`, `documentationUrl`; o `version` não muda), `README.md` (requisito: só Unity 6000.0 ou mais nova, sem dependência de terceiros; link do site), `Tests/Core/EditMode/DocUrlsTests.cs`.
- Depende de: 0M-07.
- CAs: CA4 (parte: `unity` e `documentationUrl`), CA37 (parte: constante única com o valor certo), CA42 (E).
- Testes red: `DocUrls_BaseMatchesSite` e `DocUrls_BaseMatchesPackageDocumentationUrl` falham (tipo não existe; `package.json` tem example.com); `Package_DeclaresUnity6000_0` falha (2021.3).
- Decisão registrada (seção 4.11): MkDocs 1.6.1 + Material 9.8.0 travados em `requirements-docs.txt` (Zensical 0.0.73 é pré-1.0; sem emenda na 0025). Extensões: `attr_list`, `admonition`, `toc` e `fenced_code`.
- Achado da Fábrica fora do escopo deste ticket: o build gera `Documentation~/site/`, que não está no `.gitignore`. Fica com o 0M-09.
- Manual:
  - **O que é**: o pacote, os módulos e para quem é;
  - **Instalação**, com conteúdo real: Unity 6000.0 ou mais nova; instalação por URL git; nenhuma dependência de terceiros; o Inspector do pacote é UI Toolkit (link para a página Inspector quando ela existir, 0M-42); seção **Compatibilidade** (texto revisto em 2026-10-29, 0M-11 cancelado): mínimo declarado 6000.0 (`package.json`); a suíte EditMode e PlayMode roda no projeto host do repositório na 6000.3.13f1 (a de `HostProject~/ProjectSettings/ProjectVersion.txt`), que é a única versão testada; não há CI de testes. Não cita `tests.yml`, GameCI nem outras versões como testadas (0010, CA54). Tem âncoras `{#...}`. **Não cita o Odin** (a guarda `NoOdinTests` não lê o manual; o Examinador confere).

  Verificação: `mkdocs build --strict` local passa, pelo venv fora do repositório (seção 4.11), com `-d` fora da árvore.
- Tamanho: ~70 linhas de código, 8 arquivos.

### 0M-09 — `ci(manual): publicar o manual no GitHub Pages`
- Arquivos: `.github/workflows/docs.yml`, `.gitignore` (acrescenta `Documentation~/site/`, a saída do `mkdocs build`, que não deve ser commitada).
- Depende de: 0M-08. **Passo do usuário:** ligar Pages com "Source: GitHub Actions" (risco 20).
- CAs: CA39 (S), CA38 (S, só depois do 0M-29 para o `EventListener`), CA56 (E; veio do 0M-11 cancelado, 2026-10-29).
- Testes: critérios:
  - após o merge, a Action roda, `https://gabrielctorres.github.io/SpellBook/` abre sem login e não há nenhuma URL de `prd/`, `spec/`, `adr/`, `planos/`, `backlog`, `TIME`;
  - CA56: `grep -ri "odin\|sirenix" .github/` não encontra nada; o `docs.yml` não baixa nada de local privado e não traz credencial escrita (não usa `secrets.*` além do `GITHUB_TOKEN` implícito do Pages, nem token, senha ou URL com credencial no arquivo); `git ls-files | grep -i "sirenix\|odin" | grep -v "^Documentation~/adr/" | grep -v "^Tests/Core/EditMode/Structure/NoOdinTests\.cs"` tem **saída vazia** (texto revisto em 2026-10-29, achado do Examinador: sem os dois `grep -v`, o comando lista 4 arquivos legítimos, as ADRs 0011 e 0026 e a guarda `NoOdinTests.cs` com o `.meta`).
- Manual: nenhuma página nova. Tamanho: ~50 linhas, 2 arquivos.

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
- Arquivos: `Runtime/GAS/SpellBook.GAS.asmdef` (refs Core; `overrideReferences: true`, `precompiledReferences: []`), `Runtime/Systems/Events/GAS_Events.cs` (apagado), `Runtime/GAS/Core/AbilitySystemComponent.cs` (remove `:217`), `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs`, `Tests/Core/EditMode/SpellBook.Core.Tests.EditMode.asmdef` (+GAS), `Documentation~/manual/inicio/conceitos.md`, `Documentation~/manual/regras/migracao-2-para-3.md`, `Documentation~/manual/modulos/gas.md` (tira a frase do evento global).
- **Desvio aceito (2026-10-29, Arquiteto):** a referência `SpellBook.GAS` no asmdef de testes do Core entra aqui, e não no 0M-38. O `ShowIfRuleTests` (0M-46) e o `TypePickerCatalogTests` (0M-41) usam tipos do GAS (`SpellBook.GAS.Attributes`, `.Effects`, `.Core`) e deixam de compilar quando o GAS vira assembly próprio. Esses testes **não** vão para o `SpellBook.GAS.Tests.EditMode` (0M-22): eles testam o `ShowIfRule` e o `TypePickerCatalog`, que são do `Core.Editor`, aplicados aos campos reais do GAS; levá-los obrigaria o asmdef de testes do GAS a referenciar o `Core.Editor`. Nenhum ticket posterior move esses testes. Regra na 4.1 ("Referências dos asmdefs de teste").
- Depende de: 0M-10 (a página GAS precisa existir).
- CAs: CA1 (parte), CA2 (parte), R22 (parte), R22a.
- Testes red: `AsmdefRules_GasAssemblyExists` e `AsmdefRules_CoreDoesNotReferenceGas` falham (não existe asmdef do GAS).
- Manual: **Conceitos** (mapa de módulos atual e alvo da 0-M; regra de dependência); **Migração 2.x → 3.0** (primeiras linhas: o pacote não depende mais do Odin, os assets continuam válidos e há perdas no Inspector do GAS (seção 7); assembly do GAS; fim do `AbilityActivatedEvent` e do `AbilitySlotUpdateEvent`, usar `OnAbilityActivated`).
- Tamanho: ~60 linhas, 8 arquivos (o asmdef de testes entrou pelo desvio aceito; o `.meta` apagado do `GAS_Events.cs` não conta, ADR 0003).

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
- Arquivos: `Runtime/Time/SpellBook.Time.asmdef`, `Runtime/Time/AssemblyInfo.cs` (`InternalsVisibleTo` dos testes EditMode e PlayMode), `Runtime/Time/ITimeSource.cs`, `Runtime/Time/TimeSourceAsset.cs` (base, padrão escalado, registro e reset), `Runtime/Time/ScaledTimeSource.cs`, `Runtime/Time/UnscaledTimeSource.cs`, `Runtime/Time/ManualTimeSource.cs`, `Tests/Time/EditMode/SpellBook.Time.Tests.EditMode.asmdef`, `Tests/Time/EditMode/TimeSourceTests.cs`, `Documentation~/manual/modulos/tempo.md`.
- Depende de: 0M-12.
- CAs: CA44 (parte: duas variantes), CA33 (parte, a factory vem no 0M-19).
- Testes red: `Manual_AdvanceAccumulates`, `Manual_NegativeAdvanceThrows`, `Manual_ResetOnSessionStart`, `Manual_ResetTwice_StillRegistered` (avança, reseta, avança, reseta: zera nas duas; pega o registro esvaziado), `SessionHook_IsSubsystemRegistration` (só confere o atributo do gancho), `Menu_HasScaledAndUnscaled`, `Default_IsScaled` falham (módulo não existe). Os testes chamam `TimeSourceAsset.ResetAllOnSessionStart()` direto e no `TearDown` (4.3, risco 1).
- Manual: **Tempo** (o que é, `ITimeSource`, criar fontes pelo menu, `ManualTimeSource` em testes, consumo por diferença, armadilha do nome `Time` em `namespace SpellBook.*`). `[HelpURL]` nas três fontes.
- Tamanho: ~280 linhas, 10 arquivos.

### 0M-18 — `feat(time): grupo pausável e reset sem domain reload`
- Arquivos: `Runtime/Time/PausableTimeSource.cs`, `Tests/Time/PlayMode/SpellBook.Time.Tests.PlayMode.asmdef`, `Tests/Time/PlayMode/TimeScaleTests.cs`, `Tests/Time/PlayMode/PausableTimeSourceTests.cs`, `Tests/Time/EditMode/TimeSourceTests.cs` (menu com 3 variantes), `Documentation~/manual/modulos/tempo.md`.
- Depende de: 0M-17.
- CAs: CA44, CA45, CA46 (parte: o grupo em si).
- Testes red: `Pausable_PausedHasZeroDeltaAndFrozenElapsed`, `Pausable_ResumeContinuesFromSameValue`, `Pausable_ResetOnSessionStart`, `Pausable_AfterReset_FirstReadIsZeroEvenIfFramesPassed` (reseta, espera quadros sem ler, a primeira leitura é 0), `Pausable_AfterReset_PauseBeforeAnyRead_ElapsedIsZero` (reseta, espera quadros, pausa sem ler antes, espera, `ElapsedTime` é 0), `Scaled_DoesNotAccumulateWithTimeScaleZero`, `Unscaled_AccumulatesWithTimeScaleZero`, `Menu_HasThreeVariants` falham (tipo e asmdef não existem).
- Manual: seção **Grupo pausável** `{#pausabletimesource}` (pausar a gameplay e manter a UI, chamar `Pause` por `UnityEvent`, P19 e P20 explicadas). `[HelpURL]` no `PausableTimeSource`. **Inferido:** I-b.
- Tamanho: ~220 linhas, 6 arquivos.

### 0M-19 — `feat(gas)!: Tick, autoTick e fonte de tempo no ASC`
- Objetivo: R8 a R10 para efeitos e recargas; elimina todo `Time.` do GAS (risco 12).
- Arquivos: `Runtime/GAS/SpellBook.GAS.asmdef` (+Time), `Runtime/GAS/Core/AbilitySystemComponent.cs` (+`[HelpURL]`), `Runtime/GAS/Effects/ActiveEffectInstance.cs` (sem `StartTime`), `Runtime/GAS/Abilities/Projectiles/Projectile.cs` (delta pela fonte, +`[HelpURL]`), `Tests/Testing/SpellBook.Testing.asmdef` (+Time), `Tests/Testing/TimeFactory.cs`, `Tests/Testing/GasFactory.cs` (efeito com duração, habilidade com recarga), `Tests/GAS/PlayMode/AscTimeTests.cs`, `Tests/GAS/PlayMode/SpellBook.GAS.Tests.PlayMode.asmdef` (+Time), `Documentation~/manual/modulos/gas.md`.
- **Emenda (2026-10-30, Arquiteto):** o asmdef de testes PlayMode do GAS ganha a referência `SpellBook.Time` aqui. O `AscTimeTests` usa `ManualTimeSource`, `PausableTimeSource` e `SetTimeSource(ITimeSource)`, e sem a referência não compila (CS0012). A regra já estava na 4.1 ("Referências dos asmdefs de teste"), mas nenhum ticket a aplicava. Para ficar em 10 arquivos (ADR 0003), a página Migração sai deste ticket: as linhas `autoTick` e `StartTime` passam para o 0M-21, que é o próximo ticket que edita a página e depende deste. É o mesmo caminho do 0M-15. O gate de doc (0007) continua atendido: a página do comportamento (GAS, seção **Tempo do ASC**) muda aqui, e as quebras seguem no 0M-19 na tabela de "Quebras de compatibilidade" (CHANGELOG).
- Depende de: 0M-18, 0M-15.
- CAs: CA7, CA8, CA9, CA11, CA46, N2 (D3), N3.
- Testes red: `Effect2s_ActiveAfter1_9_RemovedAfter0_2`, `Cooldown3s_BlockedBefore_AllowedAt3`, `StoppedSource_AutoTick_NothingChangesOverNFrames`, `SameTicks_SameState`, `PausableGroup_TwoAscsFrozen_UnscaledAscRuns`, `Tick_InSteadyState_DoesNotAllocate` falham (`Tick`, `SetTimeSource` e o campo de fonte não existem).
- Manual: GAS, seção **Tempo do ASC** (`timeSource`, `autoTick`, `Tick`, `TickedTime`, receita do C6). A Migração (`autoTick`, `StartTime`) vai para o 0M-21 (emenda acima).
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
- Manual: GAS, seções **Projéteis** e **Ações de spawn** (campo `Spawner`, armadilha do VFX do `PlayCueAction` até o Cues); Migração (`Projectile.Setup`, `IPoolable` e, pela emenda de 2026-10-30 do 0M-19, `autoTick` e `StartTime`).
- Tamanho: ~220 linhas, 9 arquivos.

### 0M-22 — `refactor(gas)!: esperas do GAS pelo relógio do ASC`
- Arquivos: `Runtime/GAS/Abilities/AbilityContext.cs` (`WaitSeconds`), `Runtime/GAS/Abilities/Actions/WaitAction.cs`, `Runtime/GAS/Abilities/Actions/FlowActions.cs`, `Runtime/GAS/Abilities/Actions/SpawnProjectileAction.cs`, `Runtime/GAS/Abilities/Projectiles/Projectile.cs` (vida e invulnerabilidade como contadores), `Tests/GAS/EditMode/SpellBook.GAS.Tests.EditMode.asmdef`, `Tests/GAS/EditMode/GasSourceScanTests.cs`, `Tests/GAS/PlayMode/AbilityWaitTests.cs`, `Documentation~/manual/modulos/gas.md`, `Documentation~/manual/regras/migracao-2-para-3.md`.
- **Emenda (2026-10-30, Arquiteto, dúvidas da Fábrica na implementação):**
  - **Quebras.** O ticket quebra compatibilidade em três pontos: as esperas contam o `TickedTime` do ASC (param com `autoTick` desligado sem `Tick` ou com o grupo pausado); o `Projectile` perde o `protected virtual IEnumerator InitialInvulnerability()`; vida e invulnerabilidade do projétil seguem a fonte de tempo dele. A Spec já pedia "sem corrotina" (4.4), mas o ticket não listava as quebras. Por isso o título ganha `!`, a seção 7 ganha duas linhas e a página Migração entra no ticket, que vai a 10 arquivos (limite da ADR 0003; página do manual conta só em arquivos).
  - **`Duration <= 0`.** O `WaitAction` espera exatamente um quadro, como na 2.x (4.4). O `WaitSeconds` continua terminando na hora com `seconds <= 0`.
  - **Invulnerabilidade com teste.** O teste não usa física: uma subclasse de teste do `Projectile` chama o `HandleCollision` (`protected virtual`, ponto de extensão público para herança) com um alvo que tem ASC, e o efeito observável é o projétil voltar pelo spawner. A fonte é uma `ManualTimeSource` posta no campo `timeSource` por `SerializedObject`, como em `Projectile_Lifetime_CountsItsTimeSource`.
- Depende de: 0M-21.
- CAs: CA10, CA32 (parte: EditMode do GAS).
- Testes red: `GasSources_DoNotReadUnityTimeOrWaitForSeconds` falha (5 ocorrências de `WaitForSeconds`); `WaitAction_FinishesByAscTicks_NotRealTime` falha (com `autoTick` desligado e sem `Tick`, a espera atual termina pelo relógio real; com o ASC, só termina após `Tick` somar a duração).
  - Pela emenda: `WaitAction_ZeroDuration_WaitsOneFrame` (ASC com `autoTick` desligado e sem `Tick`; logo depois do `StartCoroutine` a ação ainda não terminou; depois de no máximo dois `yield return null` do teste, terminou; o mesmo com `Duration = -1`). Com o código de antes da emenda, a primeira asserção falha (termina na hora). `Projectile_Invulnerability_CountsItsTimeSource` (`InitialInvulnerabilityDuration = 1`, `ManualTimeSource` parada: o choque é ignorado e o projétil continua ativo; avançar a fonte 1 s e esperar um `WaitForFixedUpdate`: o choque devolve o projétil pelo spawner). Ele passa com o contador e serve de caracterização: a Fábrica confirma que ele falha se o `FixedUpdate` não decrementar o contador (mutação manual, não commitada).
- Manual: GAS, seção **Ações que esperam** (a espera conta o tempo do ASC; pausa do grupo pausa a habilidade; "Wait 0" passa ao quadro seguinte). Migração, seção **Tempo do ASC** `{#tempo-do-asc}`: as esperas das ações e, em **Spawn e projéteis**, a vida, a invulnerabilidade e o `InitialInvulnerability()` removido, com as linhas da tabela iguais às da seção 7.
- Tamanho: ~220 linhas, 10 arquivos. **Revisto pela emenda abaixo: ~480 linhas, 10 arquivos.**
- **Emenda (2026-10-30, Arquiteto, limite da ADR 0003 na revisão):**
  - **Medida.** O Examinador contou 465 linhas (82 em `Runtime/`, 383 nos testes novos) e 10 arquivos. Com os achados da revisão (teste de regressão do `_isInvulnerable`, ~15 linhas, e 1 linha de tooltip), o ticket fecha em ~480 linhas. A estimativa de ~220 subestimou os testes PlayMode que as emendas de hoje pediram (`Duration <= 0`, invulnerabilidade, `ReleasesOnce`).
  - **Decisão: o 0M-22 fica inteiro, com ~480 linhas (teto de 500), como exceção pontual ao ~400 da ADR 0003.** A ADR não muda.
  - **Motivo para não dividir.** (1) O código de produção tem 82 linhas; o excesso é só de testes, que são o que a ADR 0004 pede, e o diff continua fácil de revisar. (2) Separar o `Projectile` deixa o `GasSources_DoNotReadUnityTimeOrWaitForSeconds` vermelho no primeiro ticket, porque o `Projectile` tem 2 dos 5 `WaitForSeconds`. Para ele passar, seria preciso uma exceção temporária na varredura, ou seja, contornar o sintoma. (3) As duas partes mudam o mesmo contrato (o tempo do GAS segue o ASC ou a fonte do projétil) e a mesma seção da Migração. Dividir criaria dois PRs com quebras parciais do mesmo assunto. (4) O diff já foi revisado inteiro. Dividir agora custa uma issue nova, duas revisões e mover testes entre arquivos, sem reduzir o risco.
  - **Para o Examinador:** neste ticket, o limite de linhas é 500, contado como na ADR 0003. Acima disso, ou com um 11º arquivo, o ticket volta ao Arquiteto para ser dividido.
  - **Testes red adicionais (achados da revisão):** o teste de regressão do `_isInvulnerable` (a Fábrica escolhe o nome, seguindo o padrão `Projectile_<caso>_<resultado>`) entra em `Tests/GAS/PlayMode/AbilityWaitTests.cs`. Não entra arquivo novo.

### 0M-23 — `refactor(pooling)!: módulo SpellBook.Pooling e PooledSpawnerAsset`
- Arquivos: `Runtime/Pooling/SpellBook.Pooling.asmdef`, `Runtime/Pooling/PoolManager.cs`, `ComponentPooler.cs`, `PoolConfiguration.cs`, `PoolableObject.cs`, `IPoolable.cs` (os 5 movidos de `Runtime/Systems/Pooling/`, namespace novo, `[HelpURL]`), `Runtime/Pooling/PooledSpawnerAsset.cs`, `Tests/Pooling/EditMode/SpellBook.Pooling.Tests.EditMode.asmdef`, `Tests/Pooling/EditMode/PooledSpawnerTests.cs`, `Documentation~/manual/modulos/pooling.md`.
- **Alterado (revisão sem Odin):** o `PooledSpawnerAsset` implementa `IValidatable` (0M-43). Saem a verificação do `SelfValidationResult` e o plano B.
- Depende de: 0M-21, 0M-43.
- CAs: CA28, CA35 (parte: regra do `PooledSpawnerAsset`), CA6 (parte), CA32 (parte).
- Testes red: `Pooled_NoPoolManager_SpawnsWithoutException`, `Pooled_ReleaseThenSpawn_ReusesSameInstance`, `Pooled_ReleaseNonPooled_Destroys` e `Pooled_NegativeInitialSize_IsValidationError` falham (o tipo não existe). O último chama `Validate(new ValidationResult(asset))` e confere um erro com `PropertyPath == "initialSize"`.
- Manual: Pooling, seções **PooledSpawnerAsset**, **PoolManager (pré-aquecimento)** e **ComponentPooler/IPoolable**. **Inferido:** I-c.
- **Emenda (2026-10-30):** a linha da Migração (`SpellBook.Systems.Pooling.*` → `SpellBook.Pooling.*`, menu `PoolingSystem/...` → `SpellBook/Pooling/...`) não entra aqui, porque o ticket já tem 10 arquivos e a ADR 0003 não tem exceção. Também não entra no 0M-25: ele é feito antes deste ticket, quando o namespace novo ainda não existe. A linha vai para o 0M-40, o primeiro ticket depois deste com folga no limite e garantidamente posterior (depende do 0M-50). Até lá a Migração fica sem a linha de Pooling; a quebra continua registrada na seção 7.
- Tamanho: ~200 linhas de código novo; os 5 arquivos movidos contam pela diferença real (namespace, menu e `[HelpURL]`, ~30 linhas). 10 arquivos.

### 0M-24 — `refactor(core)!: MathTools como classe estática no Core`
- Arquivos: `Runtime/MathTools/Math.cs` (apagado), `Runtime/Core/MathTools.cs`, `Tests/Core/EditMode/MathToolsTests.cs`, `Documentation~/manual/inicio/conceitos.md` (seção Core: `Void`, `RuntimeSet<T>`, `MathTools`).
- Depende de: 0M-12.
- CAs: CA43.
- Testes red: `MathTools_IsStaticInCoreNamespace` e `NoMonoBehaviourNamedMath` falham; caracterização de `Map`, `IsInRange`, `MidPoint`, `CenterOfMass`; `RandomPointInCircle`/`InSphere` testados só pela propriedade "dentro do raio" (sem depender da semente).
- Manual: Conceitos, seção **Core**.
- Tamanho: ~150 linhas, 4 arquivos.

### 0M-25 — `refactor(events)!: remover os três sistemas de eventos antigos`
- Arquivos (decisão I42: arquivos apagados por inteiro, sem substituto, não contam no limite de arquivos nem no de linhas): apagar `Runtime/Core/Events/**` (2), `Runtime/Core/NewEventSystem/**` (18, exceto `Void.cs`), `Runtime/Systems/Events/**` (4); mover `Runtime/Core/NewEventSystem/Void.cs` → `Runtime/Core/Void.cs` (namespace `SpellBook.Core`); `Runtime/Core/Sets/RuntimeSet.cs` (namespace `SpellBook.Core`); `Tests/Core/EditMode/Structure/SourceRulesTests.cs`; `Documentation~/manual/regras/migracao-2-para-3.md` (tabela de eventos, MathTools, `Void`, `RuntimeSet`; a linha de Pooling saiu na emenda de 2026-10-30 e foi para o 0M-40). Contam no limite: 4 arquivos (as 24 remoções puras ficam listadas no PR, fora da contagem).
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
  - **2ª rodada (I48):** sai o `ValidationInspectorHook`. As mensagens no Inspector vêm do `SpellBookEditor` (0M-48). A janela e o Inspector usam as mesmas mensagens: o `ProjectValidator` e o `SpellBookEditor.CollectMessages` chamam o mesmo `IValidatable`;
  - **D6 (2026-10-29, aprovada pelo usuário em 2026-10-29):** o erro de tipo ausente usa `MissingReferenceTypes.Get` e `FormatMessage` do 0M-53, por objeto, com o mesmo texto do topo do Inspector. O CA64 não muda: o validador já relatava por objeto.
- Arquivos: `Editor/Core/ProjectValidator.cs`, `Editor/Core/ValidationWindow.cs`, `Tests/Core/EditMode/SpellBook.Core.Tests.EditMode.asmdef` (+Events, Pooling, GAS.Bridges; o GAS já entrou no 0M-12), `Tests/Core/EditMode/ProjectValidationTests.cs`, `Documentation~/manual/ferramentas/validador.md`.
- Depende de: 0M-37, 0M-33, 0M-48 (`SpellBookEditor`, CA66), 0M-53 (`MissingReferenceTypes`, CA64; o 0M-53 já vem antes na ordem linear).
- CAs: CA35, R32, R33, R34, CA64, CA34 (S).
- Testes red (falham, porque `ProjectValidator` não existe):
  - `Validator_ReportsEachR35Error` (um objeto em memória por regra);
  - `Validator_FixedObjects_NoErrors`;
  - `Validator_ExceptionInValidate_BecomesErrorAndContinues`;
  - `Validator_MissingSerializeReferenceType_IsError_DataKept` (YAML na pasta temporária, risco 23; a mensagem é igual a `MissingReferenceTypes.FormatMessage` da entrada, e o arquivo ainda tem os dados depois da validação);
  - `Project_HasNoValidationErrors` (projeto do host).
- Cenários no host:
  - CA34: um `EventListener` sem canal aparece na janela, e o clique seleciona o objeto;
  - R32: o mesmo erro aparece no topo do Inspector do `EventListener` e some ao ligar o canal. Isso só é verificável depois do 0M-50, que dá ao `EventListener` o editor de uma linha; o Examinador registra o cenário no 0M-50.
- Manual: **Validador** (menu, como ler a janela e o Inspector, regras da 0-M, tipo ausente com link para a seção **Tipo não encontrado** da página Inspector, e a frase de que um tipo próprio sem `SpellBookEditor` só aparece no validador (passou do 0M-53, porque o validador nasce aqui), o teste `Project_HasNoValidationErrors` na suíte do projeto (não há CI de testes, 2026-10-29), como implementar `IValidatable` num tipo próprio).
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
- Arquivos: `Runtime/SpellBook.Core.asmdef` → `Runtime/Core/SpellBook.Core.asmdef` (com o `.meta`, GUID mantido; as pastas vazias `Runtime/Systems`, `Runtime/MathTools` e os `.meta` delas somem), `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs` (lista completa do CA1 e do CA32), `Tests/Core/EditMode/ManualLinksTests.cs`, `HostProject~/Assets/Scenarios/C1/C1.PoolingOnly.asmdef` (refs só Core e Pooling), `HostProject~/Assets/Scenarios/C1/C1PoolingUsage.cs`, `Documentation~/manual/inicio/conceitos.md` (mapa final), `Documentation~/manual/index.md`, `Documentation~/manual/regras/migracao-2-para-3.md` (linha de Pooling, emenda de 2026-10-30).
- Depende de: 0M-50.
- CAs: CA1, CA32, CA54, CA6, N4.
- **Alterado (revisão sem Odin):** a lista final do CA1 não tem `SpellBook.Setup.Editor`. Tem `SpellBook.Core.Editor`, `.Events.Editor`, `.Time.Editor`, `.Tags.Editor`, `.Pooling.Editor` e `.GAS.Editor`. Depende do 0M-50, não mais do 0M-39. O `NoOdinTests` e o `SpellBookEditorCoverageTests` continuam verdes depois do movimento.
- Testes red: `AsmdefRules_CoreAsmdefInRuntimeCore` falha (asmdef ainda na raiz de `Runtime/`); `AllExpectedAssembliesExist` e `EachRuntimeModuleHasEditModeTests` são guardas finais; `EveryHelpUrlHasPageAndAnchor` e `NoStubPages` são guardas (red mostrado removendo uma âncora localmente). CA6: o host compila o cenário C1 (ao rodar a suíte no host; sem CI desde 2026-10-29).
- Manual: Conceitos (mapa final com as referências permitidas), O que é (lista final de módulos) e Migração (linha `SpellBook.Systems.Pooling.*` → `SpellBook.Pooling.*` e menu `PoolingSystem/...` → `SpellBook/Pooling/...`, vinda do 0M-23/0M-25 pela emenda de 2026-10-30).
- Tamanho: ~150 linhas, 8 arquivos.

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
- Decididas pelo usuário em 2026-10-29 (0M-07):
  - ~~exceção de tamanho do 0M-07 (440 linhas)~~: **aprovada**, só neste ticket; o plano do 0M-51 foi descartado;
  - ~~regra de busca do CA65~~: **aprovada**. O CA65 fica como está no PRD, e a regra exata (termo só no início de identificador, sem borda à direita, termos montados por partes) mora na Spec, no 0M-07. Sem pendência.
- Abertas pelo 0M-41 (2026-10-29), para o usuário:
  - ~~divisão do 0M-41 com o ticket novo 0M-52~~: **recusada pelo usuário em 2026-10-29**. Vale a **exceção de tamanho de ~440 linhas só para o 0M-41**, como a do 0M-07; o 0M-52 foi desfeito e não pede issue;
  - `PickHolder.cs` em arquivo próprio no 0M-41 (8 arquivos), a referência a `SpellBook.Core` no asmdef de testes e o namespace `SpellBook.Core.Editor.Inspector` foram registrados no ticket como decisões técnicas.
- Abertas pelo red do 0M-41 (2026-10-29, achado da Fábrica sobre tipo ausente), para o usuário (gate parcial: só Spec e emenda da ADR 0026):
  - ~~**D6, tipo ausente relatado por objeto**~~: **aprovada pelo usuário em 2026-10-29** (alternativa A da Fábrica; B e C recusadas, seção 3). O aviso sai do drawer e vai para o topo do Inspector do `SpellBookEditor`; o validador já era por objeto;
  - ~~**mover o tipo ausente do 0M-41 para o 0M-48**~~: **aprovado pelo usuário em 2026-10-29** (sem ticket novo). O 0M-41 fica com ~390 linhas, coberto pela exceção de ~440 já aprovada; o 0M-48 vai de ~250 para ~370 linhas e de 3 para 5 arquivos, dentro da 0003. O 0M-38 passa a depender também do 0M-48 (já vem antes na ordem). Alternativa, se o usuário recusar: o tipo ausente por objeto fica no 0M-41, com ~470 linhas e 10 arquivos, e precisa de **exceção nova** de ~470;
  - ~~**emenda da ADR 0026, item 4**~~: **aprovada pelo usuário em 2026-10-29 e escrita** na ADR 0026 (seção "Emenda (2026-10-29): tipo ausente relatado por objeto, D6 da Spec"). Texto proposto: "Emenda (2026-10-29, D6 da Spec de modularização): a Unity deixa o campo com tipo ausente com `managedReferenceId = -2`, igual a nulo, e nenhuma API pública liga o campo ao `referenceId` do tipo ausente. Por isso o aviso não fica no drawer: o `SpellBookEditor` mostra no topo do Inspector um `HelpBox` de erro por tipo ausente do objeto, 'Tipo não encontrado: `<assembly>` `<namespace.Classe>`', com o botão 'Limpar referência' (com confirmação) pelo `referenceId`. Nunca limpa sozinho. O campo aparece como '(Nenhum)'. Um tipo do usuário sem `SpellBookEditor` só vê o erro no validador.";
  - **Divisão do 0M-48 por tamanho (decisão do usuário, 2026-10-29):** o 0M-48 media ~582 linhas (`SpellBookEditor.cs` 154, `MissingReferenceTypes.cs` 69, testes 217 e 142). Ficou assim: 0M-48 com o `SpellBookEditor` e os testes (~371 linhas, 3 arquivos, e o ponto de extensão `spellbook-top-notices` + `RefreshTopNotices()`); **0M-53 novo (issue #74)** com `MissingReferenceTypes`, os testes, o `HelpBox` de tipo ausente e a seção **Tipo não encontrado** (~240 linhas, 5 arquivos), logo depois do 0M-48. O 0M-38 (CA64) passa a depender também do 0M-53; D6, riscos 27, 28 e 33 e o manual apontam para o 0M-53. Os tickets que só herdam do `SpellBookEditor` (0M-45, 0M-49, 0M-50, 0M-40) continuam no 0M-48. Sem exceção de tamanho. A emenda da ADR 0026 não muda (ela não cita ticket). Nada para o Criativo;
  - **PRD:** nenhum CA muda. O CA63 não fala de tipo ausente, o CA64 é do validador (por objeto, igual), e o R45 ("O Inspector mostra um erro com o assembly e a classe que faltam e um botão para limpar a referência") continua atendido pelo topo do Inspector. Nada para o Criativo.
  - Também corrigido: a ordem linear da seção 8 ainda citava o 0M-52, que foi desfeito.
- Decididas pelo usuário em 2026-10-29 (0M-47):
  - ~~exceção de tamanho do 0M-47 (~500 linhas: `TypePickerTree` 163, `TypePickerPopup` 223, testes 108)~~: **aprovada**, só neste ticket (registrada no 0M-47);
  - ~~adiantar o 0M-08 para logo depois do 0M-47~~: **aprovado**. Ordem, "Por que essa ordem" e "Paralelo sem Editor" da seção 8 atualizados; nenhuma dependência muda.
- Continua aberto o passo manual do usuário de ligar o Pages com "Source: GitHub Actions" (0M-09).
- Abertas pelo D5 (host na 6000.3.13f1, decisão do usuário de 2026-10-29):
  - ~~emenda da ADR 0010~~: **resolvida**, aprovada pelo usuário em 2026-10-29 (consequência do host trocada e seção "Emenda: host local fora da 6000.0");
  - ~~ajuste do CA5 do PRD pelo Criativo~~: **resolvido**. O Criativo ajustou o CA5 (PRD, linha 242) e registrou o D5 no diário;
  - ~~adiantar o 0M-11 para logo depois do 0M-07~~: **resolvida**, aprovada pelo usuário em 2026-10-29 (ordem e Resumo da seção 8; a seção "Compatibilidade" do manual passou para o 0M-08).
- Decidida pelo usuário em 2026-10-29: **sem CI de testes.** O 0M-11 foi cancelado, o D5 virou decisão e as ADRs 0010 e 0004 ganharam a emenda "sem CI de testes". Aberto para o gate parcial (só as emendas, a Spec e o PRD):
  - ~~**Criativo (PRD)**~~: **resolvido** nesta revisão (o PRD também ajustou R34, o objetivo 5 e a persona Mantenedor). Escopo pedido: R42 (sai "os únicos secrets do CI são os da licença da Unity"; fica "nenhum workflow traz credencial escrita"); R43 cancelado ou trocado por "a suíte roda no host"; N1 ("passa nos testes no host, na versão da Spec; 6000.0 é o mínimo declarado"); CA56 (sai a parte dos secrets da Unity); CA57 cancelado; P14 e a linha I30 do diário marcadas como substituídas; linha nova no diário;
  - **Fábrica:** seção "Compatibilidade" de `manual/inicio/instalacao.md` no PR do 0M-08 (texto na seção 8, 0M-08); `Documentation~/README.md` ("desvio D5" vira "decisão D5");
  - **Controlador/DevOps:** fechar a issue #12 e o PR #64 sem merge.
  - **Rastreabilidade (re-revisão do Examinador, 2026-10-29):** os CAs do 0M-11 cancelado que continuam no PRD foram redistribuídos: CA56 para o 0M-09 (critério novo, sem mudar arquivos nem tamanho) e N1 para os CAs do DevOps no release (seção 8). A issue do 0M-09 precisa ganhar o CA56 nos critérios.
