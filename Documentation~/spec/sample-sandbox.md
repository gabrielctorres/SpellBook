# Spec: Sample Sandbox, cena de teste manual dos módulos
PRD: [prd/sample-sandbox.md](../prd/sample-sandbox.md) · Backlog: B-032 · ADRs: 0003, 0004, 0007, 0013, 0014, 0015, 0016, 0027, 0028 (Aceita)
Status: **Aprovada** no gate único de 2026-10-31 (PRD, T1–T6 e ADR 0028); decisões na seção final · Data: 2026-10-31

Caminhos de pacote (`Runtime/`, `Tests/`, `Samples~/`, `package.json`, `CHANGELOG.md`) são relativos a
`Packages/com.spellbook/` (ADR 0027). `Documentation~/` é relativo à raiz do repositório.

Legenda (a mesma da Spec da modularização):
- **Inferido:** valor escolhido pelo Arquiteto que fica para o playtest (seção 9 do PRD, `/ajustar`).
- **Guarda:** teste estrutural de uma regra; quando não tem red natural, o ticket diz como forçar o red (ADR 0004).
- **Achado:** comportamento do pacote descoberto ao escrever a Spec. Não é corrigido aqui; vira sugestão de backlog.

---

## Contexto do código

### GAS
- `Runtime/GAS/Core/AbilitySystemComponent.cs`
  - `:21`, `:46-53`: campo `timeSource` (`TimeSourceAsset`). Vazio usa `TimeSourceAsset.Scaled`.
  - `:80-87`: avança efeitos e recargas por diferença da fonte de tempo.
  - `:37`: evento `OnAbilityFailed(GameplayAbility, AbilityActivationResult)`, com o motivo da falha (`OnCooldown`, `CannotAffordCost`).
  - `:34`: evento `OnTagChanged(GameplayTag, bool)`.
  - `:182-193`: `TryActivateAbility(GameplayAbility, Vector3 targetPos)`.
  - `:248-254`: aplica a recarga e depois o custo, no commit.
  - `:480-504`: `GetCooldownTimeRemaining`.
- `:469-476` (**Achado A1**): reaplicar um efeito com duração e `CanStack = false` cria uma **segunda instância paralela**, que não renova nem empilha. Para "renova" é preciso `CanStack = true`, `MaxStacks = 1` e `RefreshDurationOnStack = true` (`ActiveEffectInstance.cs:61-72`).
- `:553-557` e `:563-586`: o tique periódico aplica `PeriodicModifiers` no valor base e ignora `CurrentStacks`.
- `Runtime/GAS/Attributes/AttributeState.cs:17`: `OnValueChanged(old, new)`. `:88` limita só o `CurrentValue` a `[MinValue, MaxValue]`.
  - **Achado A2:** o `BaseValue` não é limitado (`:27-35`). Uma regeneração periódica com teto faz o base subir além do `MaxValue`, e o custo passa a sair do excedente invisível. Por isso o Sandbox **não usa** regeneração de mana.
- `Runtime/GAS/Attributes/AttributeSet.cs:12-18`: só os valores iniciais são serializados. O Inspector não mostra o valor atual durante o Play (R22).
- `Runtime/GAS/Effects/GameplayEffect.cs:30-69`: `DurationPolicy`, `Duration`, `Period`, `PeriodicModifiers`, `OnTickActions`, `GrantedTags`, `CanStack`, `MaxStacks`, `RefreshDurationOnStack`.
- `Runtime/GAS/Effects/Actions/CommonEffectActions.cs:9-18`: `DebugLogEffectAction` (mensagem fixa no Console). Serve para o log do tique da queimadura.
- `Runtime/GAS/Abilities/Actions/SpawnProjectileAction.cs`
  - `:44-53`: a origem é `Source.transform` + `SpawnOffset`, e a direção vai até `context.TargetPosition`.
  - `:82-87`: spawn pelo `SpawnerAsset` e `Setup` do projétil.
  - `:84` (**Achado A3**): com `PooledSpawnerAsset.canExpand = false` e o pool vazio, o `Spawn` devolve `null` e o `TryGetComponent` lança `NullReferenceException`. O Sandbox usa `canExpand = true`.
- `Runtime/GAS/Abilities/Actions/ApplyEffectAction.cs:15-33`: aplica a lista de efeitos aos `context.Targets`. No impacto, o alvo é o ASC atingido.
- `Runtime/GAS/Abilities/Projectiles/Projectile.cs`
  - `:56`: tem `timeSource` próprio.
  - `:143-167`: movimento e `Lifetime` no `FixedUpdate`, pela fonte de tempo.
  - `:302-310`: ignora o ASC do conjurador.
  - `:314`: sem `Source` (conjurador destruído), não roda as ações de impacto.
  - `:278-290`: volta pelo spawner.
- `Runtime/GAS.Bridges/RaiseEventAction.cs:21-25`: dispara uma `EventEmission` com **payload fixo**. O conjurador aparece como quem disparou.
- O relay de eventos do ASC (B-004) não existe. Também não existe `OnDepleted` nem morte (B-010).

### Events
- `Runtime/Events/EventChannel.cs`
  - `:124-136`: `Raise(payload, sender)` alimenta o Event Debugger (`Observed`) e o `logRaises`.
  - `:99-103`: `Raised` serve para ouvir por código.
- `Runtime/Events/Channels/FloatEventChannel.cs` e `VoidEventChannel.cs`: canais prontos.
- `Runtime/Events/EventResponse.cs:40-49`: resposta por canal, com `reactions` e `escape` (`UnityEvent<T>`). `BuiltInResponses.cs:21`: `FloatEventResponse`.
- `Runtime/Events/Reactions/LogReaction.cs:18-21`: mensagem fixa, sem o payload.
- `Runtime/Events/Reaction.cs:19-22`: `Execute<T>(listener, in T payload)` pode ser sobrescrito fora do pacote. O seletor acha a subclasse por `TypeCache` (`Editor/Core/Inspector/TypePickerCatalog.cs:56`).

### Time
- `Runtime/Time/PausableTimeSource.cs:24-35`: o grupo segue `Time.timeAsDouble` (tempo **escalado**) menos os trechos pausados. O comentário `:8` diz "Não tem escala própria". `:50-69`: `SetPaused`.
- `Runtime/Time/TimeSourceAsset.cs:54-67`: o estado de pausa volta ao inicial a cada entrada em Play. **Não** volta ao recarregar a cena nem ao reinício pelo `R`.

### Pooling
- `Runtime/Pooling/PooledSpawnerAsset.cs`
  - `:23` e `:26`: `initialSize` e `canExpand`.
  - `:72-85`: um pool por prefab, embaixo de `[SpellBook Pools]`, com `DontDestroyOnLoad`. O pool sobrevive ao reinício pelo `R`.
- `Runtime/Pooling/ComponentPooler.cs:48-85`: `Get` e `ReturnToPool` ativam e desativam o objeto. É o que aparece na Hierarchy (CA15).

### Estrutura, documentação e testes
- `package.json`: não tem `samples`, e a pasta `Samples~/` não existe.
- `Tests/Core/EditMode/Structure/PackageFiles.cs:37-72`: o `Find` pula as pastas com `~` **dentro** da varredura, mas não a pasta de partida. `Find("Samples~/Sandbox", ...)` lê o Sample. Os testes atuais (`AsmdefRulesTests`, `HelpUrlTests`) não enxergam o `Samples~`.
- `Editor/Core/HelpUrlRule.cs:56-70`: vale para todo assembly `SpellBook.*` de runtime, inclusive o do Sample depois de importado. Os `MonoBehaviour` públicos do Sample precisam de `[HelpURL]` iniciado por `DocUrls.Base`.
- `Runtime/Core/DocUrls.cs:7-22` e `Tests/Core/EditMode/DocUrlsTests.cs:24-41`: uma constante por página. `ManualLinksTests.cs:47-57` exige uma página para cada constante.
- `Documentation~/mkdocs.yml`: sem `nav`. Uma página nova entra sozinha no site.

---

## Design

### D1. Decisões das perguntas do PRD

**Q2, payload do dano recebido: valor real, por um relay do próprio Sample.**
- O `DamageTakenRelay`, no boneco, ouve o `AttributeState.OnValueChanged` da **própria** Vida.
- A cada queda, ele dispara o `FloatEventChannel` "Dano Recebido" com `old - new` e se informa como quem disparou.
- Ele cobre o dano imediato e cada tique da queimadura, que nenhuma `RaiseEventAction` alcança: a queimadura é um efeito, não uma ação de habilidade.
- É um substituto local do relay do B-004. O pacote não ganha API. Quando o B-004 entrar, o Sandbox troca o relay pelo do pacote (anotar no B-004).
- A `RaiseEventAction` continua na cena com **payload fixo**: no impacto, ela dispara o `VoidEventChannel` "Bola de Fogo Acertou". Assim a cena mostra a ponte GAS → Events do pacote (R15).
- Motivo: o payload fixo daria "20" também no tique de 5 e não daria o valor real que o R13 pede.

**Q6, câmera lenta: `Time.timeScale`.**
- O `PausableTimeSource` segue o tempo escalado (`PausableTimeSource.cs:29`). Baixar o `Time.timeScale` deixa o grupo do gameplay mais lento por inteiro: ASC, efeitos, recarga, projéteis e o piscar do boneco.
- A pausa continua no grupo (`SetPaused`). As duas se combinam sem conflito: pausar em câmera lenta e retomar mantém a escala.
- A física também fica lenta, o que é coerente com os projéteis.
- O módulo Time não muda. Não há fonte nova nem PRD do Time.
- Contra: o `timeScale` é global, então afeta tudo no projeto de destino durante o Play. O `SandboxDirector` devolve `1` no `R` e no `OnDestroy`.
- Escala da câmera lenta: **0,25** (Inferido).

**Q9, alvo que não morre: vida alta e `R`, sem piso nem regra.**
- O boneco começa com Vida 2000 e a `ATR_Vida` tem `MinValue = 0`.
- Com mana 400 e custo 20, cabem 20 conjurações por reinício (CA15). Cada uma tira no máximo 60 (dano 20 + queimadura 8 × 5), ou seja, 1200 por reinício, abaixo de 2000.
- O `R` devolve tudo ao início.
- A cena não tem nenhum código que olhe a vida zero. Mesmo fora do uso normal, a vida para em 0 pelo limite do atributo, e nada some nem dispara (CA11).
- Piso de vida e regeneração foram descartados:
  - o piso exigiria uma `AttributeDefinition` só do boneco com `MinValue > 0`, um padrão ruim de copiar;
  - a regeneração esbarra no Achado A2.

**Q10, texto dos controles: placa 3D com `TextMesh` e `README.md` ao lado da cena.**
- **Placa:** um `TextMesh` (componente embutido da Unity, fonte embutida) num objeto da cena, ao lado do jogador, com os controles e uma linha do que cada um demonstra. É texto estático, no mundo. Não é HUD, não usa Canvas e não depende de módulo de UI (R6, R23).
- **README:** `Samples~/Sandbox/README.md` repete o texto e traz o passo a passo dos cenários. Ele aparece junto da cena depois do Import.
- O `OnGUI` foi descartado: aloca a cada quadro e é a UI imediata da Unity, o que fica perto demais de um HUD.

### D2. Layout do Sample

```
Samples~/Sandbox/
  README.md
  Scenes/Sandbox.unity
  Scripts/SpellBook.Samples.Sandbox.asmdef     refs: Core, Time, Events, Tags, GAS
  Scripts/SandboxCaster.cs
  Scripts/SandboxDirector.cs
  Scripts/DamageTakenRelay.cs
  Scripts/TrainingDummyFeedback.cs
  Scripts/AttributeReadout.cs
  Scripts/LogPayloadReaction.cs
  Data/Attributes/ATR_Vida.asset, ATR_Mana.asset
  Data/Tags/TAG_Status_Queimando.asset
  Data/Effects/GE_BolaDeFogo_Custo.asset, GE_BolaDeFogo_Recarga.asset,
               GE_BolaDeFogo_Dano.asset, GE_Queimadura.asset
  Data/Abilities/GA_BolaDeFogo.asset
  Data/Events/CH_DanoRecebido.asset (Float), CH_BolaDeFogoAcertou.asset (Void)
  Data/Time/TS_Gameplay.asset (PausableTimeSource)
  Data/Pooling/SP_Projeteis.asset (PooledSpawnerAsset)
  Materials/M_Conjurador.mat, M_Boneco.mat, M_BolaDeFogo.mat, M_Chao.mat
  Prefabs/Conjurador.prefab, BonecoDeTreino.prefab, BolaDeFogo.prefab
```

- **Asmdef** `SpellBook.Samples.Sandbox`, namespace `SpellBook.Samples.Sandbox`.
  - As referências usam GUID, como as asmdefs do pacote (`Runtime/GAS.Bridges/SpellBook.GAS.Bridges.asmdef:4-10`), e são só Core, Time, Events, Tags e GAS.
  - `overrideReferences: true`, sem `precompiledReferences`, `autoReferenced: true` e `includePlatforms: []`.
  - Pooling e GAS.Bridges entram só por assets serializados. O código do Sample não os usa, então o asmdef não os referencia (R3).
- **Materiais com `Unlit/Color`** (shader embutido e sem `LightMode`). Ele desenha no Built-in e no URP sem trocar o material. Os materiais padrão de cada pipeline ficariam magenta no outro. O HDRP não tem garantia (U3, aprovado no gate). A cor do feedback vai por `MaterialPropertyBlock` em `_Color`, sem instanciar material.
- **Chão sem collider.** A mira usa um plano matemático, e não um raycast. Um tiro que erra voa reto até o fim do `Lifetime` (R10, CA10), sem bater no chão. Nenhuma camada nem matriz de física muda, porque elas ficam em `ProjectSettings`.

### D3. Scripts do Sample (API pública mínima)

Todos são `public sealed`, com `[HelpURL(DocUrls.Samples + "#<nome-em-minúsculas>")]` (regra da `HelpUrlRule`) e campos `[SerializeField] private`. As referências são guardadas no `Awake`. Não há LINQ, concatenação de string nem coleção nova no `Update`. As mensagens de log saem só em eventos (tecla, falha, dano), não a cada quadro.

| Tipo | Onde fica | Campos (Inspector) | Comportamento |
|---|---|---|---|
| `SandboxCaster : MonoBehaviour` | prefab Conjurador | `GameplayAbility ability`, `KeyCode castKey = Alpha1`, `float aimHeight = 1.2f` | `Awake`: guarda o ASC (mesmo objeto) e o `Camera.main`. `Update`: projeta o mouse no plano `y = aimHeight` e gira o conjurador no eixo Y para o ponto. Com `castKey`, chama `TryActivateAbility(ability, ponto)`. Ouve `OnAbilityActivated` e escreve "Bola de Fogo conjurada". Ouve `OnAbilityFailed`: `OnCooldown` escreve "Bola de Fogo em recarga: {s:0.0} s" (com `GetCooldownTimeRemaining`), `CannotAffordCost` escreve "Sem mana para a Bola de Fogo" e os outros motivos escrevem "Bola de Fogo não disparou: {motivo}" (R11, R21). |
| `SandboxDirector : MonoBehaviour` | objeto da cena | `GameObject casterPrefab`, `GameObject dummyPrefab`, `Transform casterSpawn`, `Transform dummySpawn`, `PausableTimeSource gameplayTime`, `float slowScale = 0.25f`, teclas `P`, `T`, `R` | `Start`: instancia os dois prefabs. `P` alterna `gameplayTime.SetPaused` e escreve "Jogo pausado" ou "Jogo retomado". O `Update` não depende do grupo, então `P` é lido durante a pausa (R19, CA17). `T` alterna `Time.timeScale` entre 1 e `slowScale` e escreve "Câmera lenta: escala 0,25" ou "Velocidade normal: escala 1" (R20). `R` faz `Resume()`, `Time.timeScale = 1`, destrói as duas instâncias, instancia de novo e escreve "Cena reiniciada" (R5, CA20). `OnDestroy`: `Time.timeScale = 1`. |
| `DamageTakenRelay : MonoBehaviour` | prefab Boneco | `AttributeDefinition health`, `FloatEventChannel damageTaken` | `Start`: pega o `AttributeState` da Vida pelo `AttributeSet` do mesmo objeto e assina o `OnValueChanged`. A cada queda (`new < old`), faz `damageTaken.Raise(old - new, this)`. `OnDestroy`: cancela a assinatura. Não tem campo para nenhum ASC (CA13). |
| `TrainingDummyFeedback : MonoBehaviour` | prefab Boneco | `Renderer target`, `GameplayTag burningTag`, `Color normal`, `Color hit`, `Color burning`, `float hitFlash = 0.15f`, `TimeSourceAsset timeSource` | Método público `OnDamaged(float amount)`, chamado pelo escape do `EventListener` do próprio boneco: pinta `hit` por `hitFlash` segundos, medidos pela fonte (param na pausa). Ouve o `OnTagChanged` do ASC **do próprio objeto**: enquanto `burningTag` estiver ativa, a cor de base é `burning`, e senão `normal`. Prioridade: hit, depois burning, depois normal. Usa um `MaterialPropertyBlock` guardado, que só é aplicado quando a cor muda (R22, CA19). |
| `AttributeReadout : MonoBehaviour` | os dois prefabs | `List<Entry> entries`, com `Entry { AttributeDefinition attribute; float current; }` | `Start`: assina o `OnValueChanged` de cada atributo e copia o valor atual em `current`. O Inspector mostra vida e mana durante o Play, sem `Update` (R22). |
| `LogPayloadReaction : Reaction` | reação do ouvinte de log | `string format = "Dano recebido: {0:0.#}"`, `LogType type` | `[Serializable, TypeCategory("Depuração", Summary = "Escreve uma mensagem com o payload.")]`. `Execute<T>` escreve `string.Format(format, payload)` com o ouvinte como contexto. `Validate` acusa `format` vazio. |

### D4. Dados (valores Inferidos, ajustáveis no playtest: U4, aprovado no gate)

| Asset | Configuração |
|---|---|
| `ATR_Vida` | Min 0, Max 9999 |
| `ATR_Mana` | Min 0, Max 9999 |
| Conjurador (`AttributeSet`) | Vida 100, Mana 400 |
| Boneco (`AttributeSet`) | Vida 2000 |
| `GE_BolaDeFogo_Custo` | Instant. Mana Add −20 |
| `GE_BolaDeFogo_Recarga` | HasDuration 1,5 s. Sem modificadores |
| `GE_BolaDeFogo_Dano` | Instant. Vida Add −20 |
| `GE_Queimadura` | HasDuration 4 s, Period 0,5 s. `PeriodicModifiers`: Vida Add −5. `OnTickActions`: `DebugLogEffectAction` "Queimadura: tique". `GrantedTags`: `TAG_Status_Queimando`. `CanStack` true, `MaxStacks` 1, `RefreshDurationOnStack` true (**renova**, Achado A1) |
| `GA_BolaDeFogo` | `CostEffect` Custo e `CooldownEffect` Recarga. `Actions`: `SpawnProjectileAction` com `BolaDeFogo.prefab`, `Spawner` `SP_Projeteis`, `SpawnOffset (0, 1.2, 1)` e `OnHitActions` = [`ApplyEffectAction` (Dano, Queimadura), `RaiseEventAction` (`CH_BolaDeFogoAcertou`)] |
| `SP_Projeteis` | `initialSize` 5, `canExpand` true (Achado A3). No máximo 2 projéteis voam juntos (`Lifetime` 3 s / recarga 1,5 s), então 5 nunca se esgota |
| `BolaDeFogo.prefab` | Esfera 0,3. `SphereCollider` trigger. `Rigidbody` cinemático com `Interpolate`, para ficar suave em câmera lenta. `Projectile` com Speed 12, Lifetime 3 e `timeSource` `TS_Gameplay` |
| Conjurador.prefab | Cápsula com `AttributeSet`, ASC (`startingAbilities` [GA], `timeSource` `TS_Gameplay`), `SandboxCaster` e `AttributeReadout` (Vida, Mana) |
| BonecoDeTreino.prefab | Cápsula com `CapsuleCollider` (não trigger), `AttributeSet`, ASC (`timeSource` `TS_Gameplay`), `DamageTakenRelay`, `EventListener` (`FloatEventResponse` em `CH_DanoRecebido`, escape `TrainingDummyFeedback.OnDamaged` dinâmico), `TrainingDummyFeedback` (`TS_Gameplay`) e `AttributeReadout` (Vida) |
| Cena `Sandbox.unity` | Câmera `MainCamera` inclinada, de cima. Chão sem collider. Pontos de spawn com o boneco a ~8 m. `SandboxDirector`. Objeto "Ouvinte de Log" com `EventListener`: `CH_DanoRecebido` → `LogPayloadReaction` e `CH_BolaDeFogoAcertou` → `LogReaction` "Bola de Fogo acertou". Placa `TextMesh` com os controles. Nenhum Canvas |

**Fluxo de um acerto:**
1. `1` → `TryActivateAbility`: aplica a recarga e o custo e spawna a `BolaDeFogo` do pool.
2. O trigger acerta o boneco → `ApplyEffectAction`.
3. Dano −20 → `OnValueChanged` → `DamageTakenRelay` → `CH_DanoRecebido(20)`. O boneco pisca e o ouvinte escreve "Dano recebido: 20".
4. A queimadura entra → tag `Queimando` → cor de queimadura.
5. A `RaiseEventAction` dispara `CH_BolaDeFogoAcertou` → "Bola de Fogo acertou".
6. O projétil volta ao pool.
7. A cada 0,5 s: "Queimadura: tique" e "Dano recebido: 5".
8. Aos 4 s, a tag sai e a cor volta ao normal.

**Reinício com projétil em voo:** o projétil fica com `Source` destruído. No impacto, ele não roda ações (`Projectile.cs:314`) e só volta ao pool. Não causa dano depois do `R`.

### D5. O que muda no pacote e o que não muda
- **Muda:**
  - `package.json` ganha `samples`;
  - `DocUrls` ganha `Samples = Base + "exemplos/sandbox/"`;
  - entram a pasta `Samples~/Sandbox/`, os testes de estrutura do Sample, a página do manual `Documentation~/manual/exemplos/sandbox.md` e a entrada no CHANGELOG.
- **Não muda:** nenhum tipo de `Runtime/` além de `DocUrls`, nenhum Editor e nenhum `ProjectSettings`.
- **Autoria:** segue a ADR 0028 (Aceita). O `Samples~` é renomeado para `Samples` só durante a edição na Unity e volta antes dos testes e do commit.

### D6. Riscos
1. **Import de duas versões do Sample** (requisito não funcional do PRD). O comportamento do Package Manager com GUIDs repetidos entre versões não foi verificado nesta Spec. O T5 confere à mão: importar, mudar a versão no `package.json` local, importar de novo e abrir as duas cenas. Se quebrar, o T5 para e volta ao Arquiteto.
2. **`TextMesh` no URP.** Ele desenha no URP pelo passe sem `LightMode`, mas o T5 confere nos dois pipelines. Se não desenhar, o T5 para. Não há troca para `OnGUI` sem gate.
3. **Código do Sample fora da suíte.** O `Samples~` não compila no projeto de desenvolvimento. A compilação é conferida na Unity com a pasta renomeada (ADR 0028). Os testes de estrutura cobrem o que dá para ler em arquivo.

---

## Quebras de compatibilidade

Nenhuma. Entram uma constante pública (`DocUrls.Samples`) e um Sample opcional, o que é MINOR. A versão da entrega fica para o gate (Q8 do PRD).

CHANGELOG, na seção da versão em preparo, "Adicionado": "Sample **Sandbox** (Package Manager → SpellBook → Samples): cena 3D que liga GAS, Events, Pooling e Time, com Bola de Fogo, queimadura, canal de dano recebido, pausa e câmera lenta. Entrada pelo Input Manager legado."

---

## Tickets

Ordem: T1 → T2 → T3 → T4 → T5. O T6 (sem Editor) pode andar em paralelo ao T5, pela emenda de 2026-10-29 da 0003, porque os dois não tocam nenhum arquivo em comum.

Contagem (0003): os YAML de assets do Sample (`.unity`, `.prefab`, `.asset`, `.mat`) e os `.md` entram como fixtures e páginas. Contam no limite de 10 arquivos e não contam nas linhas. Os `.meta` ficam fora dos dois limites. Em todos os tickets com Unity, a autoria segue a ADR 0028.

### T1 — Sample declarado, asmdef, `DocUrls.Samples`, página e guardas de estrutura
- Usa Unity: **sim** (testes EditMode; o `.meta` da asmdef é gerado com a pasta renomeada). Depende de: nada.
- Escopo:
  - `package.json` (`samples`: `displayName` "Sandbox", `description`, `path` "Samples~/Sandbox");
  - `Samples~/Sandbox/Scripts/SpellBook.Samples.Sandbox.asmdef` e `Samples~/Sandbox/README.md` (primeira versão: o que é e os controles);
  - `Runtime/Core/DocUrls.cs` (`Samples`);
  - `Tests/Core/EditMode/DocUrlsTests.cs` (caso novo);
  - `Tests/Core/EditMode/Structure/SampleStructureTests.cs` (novo);
  - `Documentation~/manual/exemplos/sandbox.md`, com âncoras `{#sandboxcaster}`, `{#sandboxdirector}`, `{#damagetakenrelay}`, `{#trainingdummyfeedback}` e `{#attributereadout}`, e conteúdo real: o que o Sample mostra, como importar e os controles.
- Testes red:
  - `Package_DeclaresSandboxSample`: um item em `samples` com `displayName` "Sandbox", `path` "Samples~/Sandbox" e `description` não vazia, e a pasta existe;
  - `SampleAsmdef_ReferencesOnlyAllowedModules`: a asmdef existe no caminho do D2. As referências resolvidas são um subconjunto de {Core, Time, Events, Tags, GAS}, sem `.Editor`. Mais `overrideReferences` true, sem precompiladas, `autoReferenced` true e `includePlatforms` vazio;
  - `DocUrls_PageConstantPointsToPageUnderBase("Samples", "exemplos/sandbox/")`;
  - `ManualLinksTests.EveryDocUrlHasPage`, que fica red só com a constante e sem a página;
  - Guardas (red forçado: criar no ticket o arquivo proibido, ver o teste falhar e apagar):
    - `Package_HasNoUntildedSamplesFolder`: não existem `Samples/` nem `Samples.meta` na raiz do pacote (ADR 0028);
    - `SampleSources_UseNoInputSystemUiOrEditor`: nenhum `.cs` do Sample contém `UnityEngine.InputSystem`, `UnityEngine.UI`, `TMPro`, `UnityEditor` ou `Sirenix` (R4, R7, R23);
    - `SampleFiles_EveryAssetHasMetaAndUniqueGuid`: todo arquivo do Sample tem `.meta`, e nenhum GUID se repete dentro do Sample nem colide com um `.meta` do pacote fora do `Samples~`.
- Critério de pronto:
  - a suíte EditMode inteira passa com a pasta como `Samples~`;
  - com a pasta renomeada, a Unity compila a asmdef vazia sem erro (ou sem aviso, já que não há script);
  - o `mkdocs build --strict` passa, se disponível (senão, registrar como não verificado).
- Tamanho estimado: ~230 linhas (testes ~190, `package.json` ~8, asmdef ~20, `DocUrls` ~2). 7 arquivos.

### T2 — Scripts de apoio do Sample
- Usa Unity: **sim** (compilação com a pasta renomeada, mais a suíte). Depende de: T1.
- Escopo:
  - `Samples~/Sandbox/Scripts/`: `SandboxCaster.cs`, `SandboxDirector.cs`, `DamageTakenRelay.cs`, `TrainingDummyFeedback.cs`, `AttributeReadout.cs` e `LogPayloadReaction.cs` (D3);
  - `Tests/Core/EditMode/Structure/SampleStructureTests.cs` (casos novos).
- Testes red:
  - `SampleBehaviours_HaveHelpUrlToSamplesPage`: varre os `.cs` do Sample, acha ao menos um `: MonoBehaviour` (red antes do T2) e exige `[HelpURL(DocUrls.Samples + "#...")]` em cada um, com a âncora presente em `exemplos/sandbox.md`;
  - `SampleSources_HaveNoFindOrGetComponentInUpdate`: guarda textual; nenhuma linha de corpo de `Update` com `GetComponent`, `Find`, `new List` ou `Linq`. Red forçado como no T1;
  - o comportamento dos scripts não tem teste automático (decisão do usuário: a cena é conferida à mão). Por isso a revisão do Examinador lê os scripts contra a tabela D3.
- Critério de pronto:
  - com a pasta renomeada: zero erros e zero avisos de compilação do `SpellBook.Samples.Sandbox`, e a janela `SpellBook > Validar projeto` sem violação de `[HelpURL]` nos tipos do Sample;
  - com a pasta de volta como `Samples~`: a suíte inteira verde.
- Tamanho estimado: ~470 linhas (scripts ~430, testes ~40). 7 arquivos.

### T3 — Dados: atributos, tag, efeitos, canais e fonte de tempo
- Usa Unity: **sim** (assets criados pelos menus `Create`). Depende de: T1. Pelo WIP 1 (0003), roda depois do T2.
- Escopo: `Samples~/Sandbox/Data/`: `ATR_Vida`, `ATR_Mana`, `TAG_Status_Queimando`, `GE_BolaDeFogo_Custo`, `GE_BolaDeFogo_Recarga`, `GE_BolaDeFogo_Dano`, `GE_Queimadura`, `CH_DanoRecebido`, `CH_BolaDeFogoAcertou` e `TS_Gameplay` (D4). São 10 arquivos.
- Testes red: nenhum novo, porque só há assets de dados. As guardas do T1 (`.meta` e GUID único) passam a cobrir estes arquivos. Não há guarda de referências por GUID (U2, recusado no gate).
- Critério de pronto:
  - com a pasta renomeada, cada asset abre no Inspector sem "Missing" e sem erro na `SpellBook > Validar projeto`;
  - a suíte verde com `Samples~`.
- Tamanho estimado: 0 linhas de código; 10 arquivos (fixtures).

### T4 — Pool, materiais, prefabs e habilidade
- Usa Unity: **sim**. Depende de: T3.
- Escopo:
  - `Samples~/Sandbox/Data/Pooling/SP_Projeteis.asset` e `Data/Abilities/GA_BolaDeFogo.asset`;
  - `Materials/` (4 `.mat` com `Unlit/Color`);
  - `Prefabs/BolaDeFogo`, `Conjurador` e `BonecoDeTreino`.
  - São 9 arquivos.
- Testes red: nenhum novo. A guarda de referências por GUID (`SampleAssets_GuidReferencesResolve`) saiu pelo U2, recusado no gate. O "Missing" (CA2) é conferido pelo critério de pronto abaixo e no T5. As guardas do T1 (`.meta` e GUID único) passam a cobrir estes arquivos.
- Critério de pronto:
  - com a pasta renomeada, cada prefab abre sem "Missing", o `GA_BolaDeFogo` mostra as ações do D4 e a `SpellBook > Validar projeto` não acusa erro nos assets do Sample;
  - a suíte verde com `Samples~`.
- Tamanho estimado: 0 linhas de código; 9 arquivos (fixtures).

### T5 — Cena Sandbox e conferência manual
- Usa Unity: **sim**. Depende de: T4.
- Escopo: `Samples~/Sandbox/Scenes/Sandbox.unity` (D4: câmera, chão, pontos de spawn, `SandboxDirector`, Ouvinte de Log e placa `TextMesh`). É 1 arquivo.
- Testes red: nenhum automático (decisão do usuário). As guardas do T1 (`.meta` e GUID único) passam a cobrir a cena.
- Critério de pronto: CA1 a CA21 do PRD conferidos à mão pelo Examinador, num projeto vazio da 6000.3.13f1 com o pacote instalado por caminho local e **sem** a pasta renomeada (Import pela aba Samples). Os pontos marcados nos Riscos 1 e 2 também são conferidos, no Built-in e no URP. O resultado vai para a seção 9 do PRD (playtest).
- Tamanho estimado: 0 linhas de código; 1 arquivo.

### T6 — Documentação final e CHANGELOG
- Usa Unity: **não**. Depende de: T1 (página existente). Pode andar em paralelo ao T5.
- Escopo:
  - `Documentation~/manual/exemplos/sandbox.md` (passo a passo de S1 a S3, tabela de dados do D4, o relay como substituto do B-004 e a política de renovação da queimadura, R12);
  - `Samples~/Sandbox/README.md` (texto igual ao da placa e passo a passo);
  - `Documentation~/manual/inicio/instalacao.md` (seção "Importar o Sample");
  - `CHANGELOG.md`.
  - São 4 arquivos.
- Testes red: nenhum (só docs). `ManualLinksTests.NoStubPages` continua verde, e o Examinador roda `mkdocs build --strict`, se disponível.
- Critério de pronto: a legenda de controles da página, do README e do D4 dizem o mesmo. O CHANGELOG tem a entrada da seção "Quebras de compatibilidade".
- Tamanho estimado: 0 linhas de código; 4 arquivos.

---

## Decisões do gate (2026-10-31)

- **Aprovados:** o PRD, os tickets T1 a T6 e a ADR 0028, que passa a Aceita.
- **U1:** sim. Entram as guardas EditMode que leem arquivos do `Samples~` (T1 e T2).
- **U2:** não. A guarda de referências por GUID saiu do T4 e do T5. A guarda `.meta` + GUID único do T1 continua, porque faz parte do U1.
- **U3:** sim. Os materiais cobrem o Built-in e o URP; o HDRP fica sem garantia.
- **U4:** sim. Os números do D4 são ajustáveis no playtest (`/ajustar`).
- **Q8 (versão da entrega):** continua em aberto.
- **Achados:**
  - A1 e A2 foram para o PRD do B-002 (Criativo).
  - A3 virou o B-033 no backlog, via rápida, com o ticket RP-07 em [rapidos-2026-10-31.md](rapidos-2026-10-31.md), que ainda vai ao gate.
  - O Sandbox continua com `canExpand = true`.

## Perguntas do gate (registro)

- **U1. Testes de estrutura do Sample.** O PRD tira o teste automático **da cena**. Esta Spec propõe guardas EditMode que leem arquivos do `Samples~`: `package.json`, asmdef, fontes, `.meta`/GUIDs e `[HelpURL]`. Elas não abrem a cena nem rodam nada. Elas entram?
- **U2. Guarda de referências por GUID** (T4), que também varre o YAML da cena no T5. Ela fica perto de "testar a cena" em EditMode. Entra (recomendado: pega "Missing" sem abrir a Unity) ou fica de fora?
- **U3. Pipelines.** Os materiais `Unlit/Color` cobrem o Built-in e o URP. O HDRP fica sem garantia. Pode ser assim?
- **U4. Valores Inferidos do D4** (mana 400, vida do boneco 2000, recarga 1,5 s, câmera lenta 0,25). Ficam para o playtest?
- **Achados A1 a A3:** viram itens do backlog? O A1 é sobre empilhamento sem `CanStack`, o A2 é o `BaseValue` sem limite e o A3 é o NRE com `canExpand = false`. Nenhum é corrigido nesta entrega.
- **Q8 do PRD** (versão da entrega) continua no gate.
