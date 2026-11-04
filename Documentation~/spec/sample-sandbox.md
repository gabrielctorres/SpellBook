# Spec: Sample Sandbox, cena de teste manual dos módulos
PRD: [prd/sample-sandbox.md](../prd/sample-sandbox.md) · Backlog: B-032 · ADRs: 0003, 0004, 0007, 0013, 0014, 0015, 0016, 0027, 0028 (Aceita); na emenda do HUD, também 0037 (âncoras), 0038 e 0039 (Aceitas)
Status: **Aprovada** no gate único de 2026-10-31 (PRD, T1–T6 e ADR 0028); decisões na seção final · Data: 2026-10-31
Emenda de 2026-11-01 (HUD S4, seção 10 do PRD aprovada no gate de 2026-11-01): D7, T7 e T8 **propostos para gate parcial**; o HUD em si está nos SL12 e SL13 de [ui-slot-e-barra-de-habilidades.md](ui-slot-e-barra-de-habilidades.md)

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

### Para a emenda do HUD (2026-11-01)
Tipos que ainda não existem no código e chegam por Specs aprovadas; o T7 e o T8 dependem deles.
- `Samples~/Sandbox/Scripts/SandboxCaster.cs:92-112`: hoje escreve a falha direto no Console (`Debug.Log`). O AV5 (B-009) troca a assinatura para `ActivationFailure`.
- `Samples~/Sandbox/Scripts/SandboxDirector.cs:47-50`, `:91-99`: `Start` e `Restart`, os dois pontos em que a cena se monta.
- `Samples~/Sandbox/Data/Effects/GE_Queimadura.asset`: o tique escreve "Queimadura: tique" por `DebugLogEffectAction`. Uma ação dentro de um asset de efeito não enxerga a cena, então não há como desligá-la pela presença do HUD. O EV21 (B-012) refaz este asset.
- `RuntimeAnchor` (base não genérica, `UntypedValue`, `ValueType`, `Changed`) e `AnchorRegistrar` (publica no `OnEnable`, sai no `OnDisable`): UI1 do B-015, sobre o `RuntimeAnchor<T>` do CU1 (B-013). A âncora volta a vazia ao entrar em Play (Spec do B-013, Design 1).
- `RaiseEventAction` única, que roda também no tique de efeito: EV15 do B-012.
- `Runtime/Events/Channels/StringEventChannel.cs`: existe. `Runtime/Events/EventListener.cs:24-40`: inscreve no `OnEnable` e desinscreve no `OnDisable`; desligado, não recebe disparos.

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
- **Emenda de 2026-11-01 (R6 revisto, Q13):** a placa fica, com o mesmo texto, também com o HUD na cena. O HUD não a substitui.

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
| `GE_Queimadura` | HasDuration 4 s, Period 0,5 s. `PeriodicModifiers`: Vida Add −5. `OnTickActions`: `DebugLogEffectAction` "Queimadura: tique" (no T8 vira `RaiseEventAction` em `CH_QueimaduraTique`, ver D7). `GrantedTags`: `TAG_Status_Queimando`. `CanStack` true, `MaxStacks` 1, `RefreshDurationOnStack` true (**renova**, Achado A1) |
| `GA_BolaDeFogo` | `CostEffect` Custo e `CooldownEffect` Recarga. `Actions`: `SpawnProjectileAction` com `BolaDeFogo.prefab`, `Spawner` `SP_Projeteis`, `SpawnOffset (0, 1.2, 1)` e `OnHitActions` = [`ApplyEffectAction` (Dano, Queimadura), `RaiseEventAction` (`CH_BolaDeFogoAcertou`)] |
| `SP_Projeteis` | `initialSize` 5, `canExpand` true (Achado A3). No máximo 2 projéteis voam juntos (`Lifetime` 3 s / recarga 1,5 s), então 5 nunca se esgota |
| `BolaDeFogo.prefab` | Esfera 0,3. `SphereCollider` trigger. `Rigidbody` cinemático com `Interpolate`, para ficar suave em câmera lenta. `Projectile` com Speed 12, Lifetime 3 e `timeSource` `TS_Gameplay` |
| Conjurador.prefab | Cápsula com `AttributeSet`, ASC (`startingAbilities` [GA], `timeSource` `TS_Gameplay`), `SandboxCaster` e `AttributeReadout` (Vida, Mana) |
| BonecoDeTreino.prefab | Cápsula com `CapsuleCollider` (não trigger), `AttributeSet`, ASC (`timeSource` `TS_Gameplay`), `DamageTakenRelay`, `EventListener` (`FloatEventResponse` em `CH_DanoRecebido`, escape `TrainingDummyFeedback.OnDamaged` dinâmico), `TrainingDummyFeedback` (`TS_Gameplay`) e `AttributeReadout` (Vida) |
| Cena `Sandbox.unity` | Câmera `MainCamera` inclinada, de cima. Chão sem collider. Pontos de spawn com o boneco a ~8 m. `SandboxDirector`. Objeto "Ouvinte de Log" com `EventListener`: `CH_DanoRecebido` → `LogPayloadReaction` e `CH_BolaDeFogoAcertou` → `LogReaction` "Bola de Fogo acertou". Placa `TextMesh` com os controles. Nenhum Canvas fora do prefab do HUD (emenda de 2026-11-01: o objeto "Console sem HUD" e o HUD estão no D7) |

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
- **Emenda de 2026-11-01 (APROVADA no gate parcial de 2026-11-01, H1; substitui o "Não muda" acima só neste ponto):** o `Runtime/Core` ganha o `TransformAnchor` (T7), e o `Editor/Core` ganha a linha dele. É a única mudança de pacote que a emenda pede. O resto do HUD usa só tipos dos B-015 e B-016. `ProjectSettings` continua intocado.
- **Autoria:** segue a ADR 0028 (Aceita). O `Samples~` é renomeado para `Samples` só durante a edição na Unity e volta antes dos testes e do commit.

### D6. Riscos
1. **Import de duas versões do Sample** (requisito não funcional do PRD). O comportamento do Package Manager com GUIDs repetidos entre versões não foi verificado nesta Spec. O T5 confere à mão: importar, mudar a versão no `package.json` local, importar de novo e abrir as duas cenas. Se quebrar, o T5 para e volta ao Arquiteto.
2. **`TextMesh` no URP.** Ele desenha no URP pelo passe sem `LightMode`, mas o T5 confere nos dois pipelines. Se não desenhar, o T5 para. Não há troca para `OnGUI` sem gate.
3. **Código do Sample fora da suíte.** O `Samples~` não compila no projeto de desenvolvimento. A compilação é conferida na Unity com a pasta renomeada (ADR 0028). Os testes de estrutura cobrem o que dá para ler em arquivo.

### D7. Emenda de 2026-11-01: HUD do Sandbox (S4)

Vale a seção 10 do PRD, aprovada no gate de 2026-11-01 (Q11 a Q14). O HUD é montado nos SL12 e SL13 da
Spec do B-016. Aqui fica o que muda no resto do Sandbox.

**Revisões do PRD aplicadas nesta Spec**
- **R4 e CA3:** S1 a S3 continuam sem módulo de UI. A asmdef do Sample segue com Core, Time, Events, Tags e GAS (D2), sem UI (R25). Os componentes do HUD vêm das assemblies do pacote (`UI`, `UI.UGUI`, `GAS.UI`) e do `com.unity.ugui` 2.0.0 que o UI8 declara no `package.json`. O Sample não declara dependência própria.
- **R6:** a placa `TextMesh` fica (D1, Q10).
- **R23 e CA19:** o único Canvas da cena é o do `HUD.prefab`. Sem o prefab, a cena não tem Canvas, barra nem número flutuante.
- **R11 e R21:** com o HUD, o tique e a falha saem do Console; sem ele, voltam. A detecção está abaixo.
- **R32:** a cena não tem `EventSystem` e o Canvas do HUD não tem `GraphicRaycaster`. A entrada segue só pelo `SandboxCaster` e pelo `SandboxDirector` (Input Manager legado).

**Como a cena sabe que o HUD está presente (decisão do Arquiteto pedida no R21 revisto)**

A presença de um objeto de cena já tem um caminho no pacote: o objeto se publica numa âncora no
`OnEnable` e sai no `OnDisable` (ADR 0037, item 2; UI1 do B-015). O HUD usa o mesmo caminho, e o
Console de reserva é um ouvinte comum de canais, ligado ou desligado conforme a âncora.

1. **Âncora do HUD.** A raiz do `HUD.prefab` tem um `AnchorRegistrar` com a âncora `ANC_HUD` e `value`
   vazio, que publica o `Transform` do próprio objeto. A `ANC_HUD` é um `TransformAnchor`, tipo novo e
   genérico do Core (T7): "este objeto está na cena". Nenhum tipo de UI entra no Sample.
2. **Mensagens que o HUD substitui passam por canal.**
   - Tique: o `OnTickActions` da `GE_Queimadura` troca a `DebugLogEffectAction` por uma `RaiseEventAction`
     em `CH_QueimaduraTique` (`VoidEventChannel`).
   - Falha: o `SandboxCaster` deixa de chamar `Debug.Log` na falha. Ele monta o mesmo texto de hoje (com
     o formato do AV5) e dispara `CH_FalhaDeConjuracao` (`StringEventChannel`). A mensagem "Bola de Fogo
     conjurada" não muda.
3. **Console de reserva.** O objeto de cena "Console sem HUD" tem um `EventListener` com
   `CH_QueimaduraTique` → `LogReaction` "Queimadura: tique" e `CH_FalhaDeConjuracao` →
   `LogPayloadReaction` "{0}". É o único lugar dessas duas mensagens.
4. **Quem decide.** O `SandboxDirector` ganha `RuntimeAnchor hudAnchor` (o tipo base do Core) e
   `EventListener consoleWithoutHud`. No `Start` e no `R`, ele faz
   `consoleWithoutHud.enabled = hudAnchor == null || hudAnchor.UntypedValue == null`. Os `OnEnable` da
   cena rodam antes de qualquer `Start`, então o `AnchorRegistrar` do HUD já publicou quando o diretor lê.
   Não há sondagem por quadro nem assinatura do `Changed`: assim o diretor não religa um ouvinte durante
   o descarregamento da cena, quando o HUD sai da âncora no `OnDisable`.

Por que este caminho:
- Apagar o prefab da cena não deixa referência "Missing": o diretor aponta para um asset (`ANC_HUD`), não
  para o objeto do HUD (CA2, CA22, CA28).
- Um ponto de decisão só, à vista na Hierarchy: o que o HUD substitui mora no "Console sem HUD".
- Funciona sem o HUD existir ainda: o T8 entra antes dos SL12 e SL13 e já é conferível com um
  `AnchorRegistrar` temporário na cena.
- Descartados:
  - procurar um `Canvas` na cena: é heurística e casa com qualquer Canvas do projeto de destino;
  - referência direta do diretor ao objeto do HUD: vira "Missing" ao apagar o prefab;
  - "pergunta e resposta" por canais (o diretor dispara, o HUD responde): protocolo próprio do Sample, em
    paralelo à âncora que o pacote já tem;
  - o próprio HUD desligar o Console: o prefab precisaria apontar para um objeto da cena e deixaria de
    ser removível sozinho;
  - um tipo de âncora de UI (`UIUpdateDriver` ou `Canvas`): serviria só a este caso, enquanto o
    `TransformAnchor` serve a qualquer objeto de cena (ponto de spawn, alvo de câmera).

**O que fica no Console com o HUD (CA30)**

| Mensagem | Origem | Com HUD | Sem HUD |
|---|---|---|---|
| Acerto: "Bola de Fogo acertou" e "Dano recebido: N" | Ouvinte de Log (S2) | fica | fica |
| Tique: "Queimadura: tique" | Console sem HUD | sai | volta |
| Falha: recarga, sem mana, outro motivo | Console sem HUD | sai | volta |
| Pausa, câmera lenta (com a escala), reinício | `SandboxDirector` | fica | fica |
| "Bola de Fogo conjurada" | `SandboxCaster` | fica | fica |

O "Dano recebido: 5" de cada tique continua, porque o canal de dano recebido leva todo dano e é o que o
S2 demonstra (CA12, CA14). O que sai é a linha própria do tique. Leitura do Arquiteto sobre o CA30, a
confirmar no gate parcial.

**Dados novos**

| Asset | Configuração | Ticket |
|---|---|---|
| `Data/UI/ANC_HUD.asset` | `TransformAnchor` | T8 |
| `Data/Events/CH_QueimaduraTique.asset` | `VoidEventChannel` | T8 |
| `Data/Events/CH_FalhaDeConjuracao.asset` | `StringEventChannel` | T8 |
| `GE_Queimadura` | `OnTickActions`: `RaiseEventAction` (`CH_QueimaduraTique`) no lugar da `DebugLogEffectAction` | T8 |
| `Conjurador.prefab` | `SandboxCaster.failureChannel` = `CH_FalhaDeConjuracao` | T8 |
| Cena: "Console sem HUD" | `EventListener` do item 3 | T8 |
| Cena: `SandboxDirector` | `hudAnchor` = `ANC_HUD`, `consoleWithoutHud` = o ouvinte acima | T8 |
| `HUD.prefab` (raiz) | `AnchorRegistrar` (`ANC_HUD`, `value` vazio), Canvas sem `GraphicRaycaster` | SL13 |

**Mudança na tabela D3**

| Tipo | Campos novos | Comportamento novo |
|---|---|---|
| `SandboxCaster` | `StringEventChannel failureChannel` | Falha: dispara o canal com o texto, em vez de `Debug.Log`. Sem canal, não escreve nada. |
| `SandboxDirector` | `RuntimeAnchor hudAnchor`, `EventListener consoleWithoutHud` | `Start` e `R`: liga o Console sem HUD só quando a âncora está vazia. Sem `consoleWithoutHud`, não faz nada. |

**Riscos da emenda**
4. **TMP Essentials.** Num projeto sem os recursos do TextMeshPro, a Unity pede o import ao abrir a cena
   com o HUD. A página do Sample e o `README` trazem o passo (Q14). O SL13 confere num projeto vazio.
5. **Ordem dos `OnEnable` com o HUD desligado no Inspector.** Um HUD desligado não publica na âncora, então
   conta como ausente: o Console volta. É o comportamento esperado; o manual diz isso.

---

## Quebras de compatibilidade

Nenhuma. Entram uma constante pública (`DocUrls.Samples`) e um Sample opcional, o que é MINOR. A versão da entrega fica para o gate (Q8 do PRD).

CHANGELOG, na seção da versão em preparo, "Adicionado": "Sample **Sandbox** (Package Manager → SpellBook → Samples): cena 3D que liga GAS, Events, Pooling e Time, com Bola de Fogo, queimadura, canal de dano recebido, pausa e câmera lenta. Entrada pelo Input Manager legado."

**Emenda de 2026-11-01:** nenhuma quebra. Entra um tipo público no Core, `TransformAnchor` (MINOR, T7).
No Sample, o tique e a falha passam a sair por canal, o que não é API. CHANGELOG, "Adicionado":
"`TransformAnchor` (Core): âncora que publica o `Transform` de um objeto de cena pelo `AnchorRegistrar`."
A linha do HUD do Sandbox entra no SL13.

---

## Tickets

Ordem: T1 → T2a → T2b → T3 → T4 → T5 (o T2 foi dividido em 2026-10-31; ver abaixo). O T6 (sem Editor) pode andar em paralelo ao T5, pela emenda de 2026-10-29 da 0003, porque os dois não tocam nenhum arquivo em comum.

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

### T2 — dividido em T2a e T2b (emenda de 2026-10-31)

A implementação do T2 original ficou com ~610 linhas (scripts 503, testes +106/−1), acima do teto da
0003. O usuário decidiu dividir. O código já existe; a divisão só reparte os arquivos, **sem mudança de
código**:
- nenhum script do Sample referencia outro script do Sample (só tipos do pacote). A ligação
  `EventListener` → `TrainingDummyFeedback.OnDamaged` é serializada na cena (T5), não em código;
- as âncoras dos cinco `MonoBehaviour` já estão em `exemplos/sandbox.md` desde o T1, então o
  `SampleBehaviours_HaveHelpUrlToSamplesPage` passa com qualquer subconjunto que tenha ao menos um
  `MonoBehaviour`;
- cada `.cs` vai com o seu `.meta` no mesmo ticket, senão o `SampleFiles_EveryAssetHasMetaAndUniqueGuid` do T1
  fica red.

Os dois testes novos ficam no T2a, porque o T2a já tem `MonoBehaviour` com `Update` para eles cobrirem.
Nenhum teste precisa esperar o T2b.

### T2a — Guardas de scripts, conjurador, diretor e reação de log
- Usa Unity: **sim** (compilação com a pasta renomeada, mais a suíte). Depende de: T1. Issue: #100.
- Escopo (4 arquivos, mais os `.meta`):
  - `Samples~/Sandbox/Scripts/SandboxCaster.cs` (114) e `.meta`;
  - `Samples~/Sandbox/Scripts/SandboxDirector.cs` (107) e `.meta`;
  - `Samples~/Sandbox/Scripts/LogPayloadReaction.cs` (32) e `.meta`;
  - `Tests/Core/EditMode/Structure/SampleStructureTests.cs` (+106/−1: os dois casos abaixo e os auxiliares `AttributesBefore`, `MethodBody`, `SampleSources` e as regex).
- Testes red:
  - `SampleBehaviours_HaveHelpUrlToSamplesPage`: varre os `.cs` do Sample, acha ao menos um `: MonoBehaviour` (red antes do T2a, quando não há script) e exige `[HelpURL(DocUrls.Samples + "#...")]` em cada um, com a âncora presente em `exemplos/sandbox.md`;
  - `SampleSources_HaveNoFindOrGetComponentInUpdate`: guarda textual; nenhuma linha de corpo de `Update`, `LateUpdate` ou `FixedUpdate` com `GetComponent`, `Find`, `new List` ou `Linq`. Red forçado como no T1 (pôr um `GetComponent` no `Update` do `SandboxCaster`, ver falhar e tirar);
  - o comportamento dos scripts não tem teste automático (decisão do usuário: a cena é conferida à mão). A revisão do Examinador lê os scripts contra a tabela D3.
- Critério de pronto:
  - com a pasta renomeada: zero erros e zero avisos de compilação do `SpellBook.Samples.Sandbox` só com estes três scripts, e a `SpellBook > Validar projeto` sem violação de `[HelpURL]` nos tipos do Sample;
  - com a pasta de volta como `Samples~`: a suíte inteira verde.
- Tamanho: ~358 linhas (scripts 253, testes ~105). 4 arquivos.

### T2b — Scripts do boneco e leitura de atributos
- Usa Unity: **sim**. Depende de: T2a. Issue: nova.
- Escopo (3 arquivos, mais os `.meta`):
  - `Samples~/Sandbox/Scripts/DamageTakenRelay.cs` (59) e `.meta`;
  - `Samples~/Sandbox/Scripts/TrainingDummyFeedback.cs` (109) e `.meta`;
  - `Samples~/Sandbox/Scripts/AttributeReadout.cs` (82) e `.meta`.
- Testes red: nenhum novo. As guardas do T1 e do T2a passam a cobrir estes três scripts (`[HelpURL]` com âncora, `Update` do `TrainingDummyFeedback`, termos proibidos, `.meta` e GUID único). Red forçado de conferência: tirar o `[HelpURL]` do `DamageTakenRelay`, ver o `SampleBehaviours_HaveHelpUrlToSamplesPage` falhar e devolver.
- Critério de pronto: o mesmo do T2a, agora com os seis scripts do D3; a revisão do Examinador lê os três contra a tabela D3.
- Tamanho: ~250 linhas (só scripts). 3 arquivos.

### T3 — Dados: atributos, tag, efeitos, canais e fonte de tempo
- Usa Unity: **sim** (assets criados pelos menus `Create`). Depende de: T1. Pelo WIP 1 (0003), roda depois do T2b.
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

### Emenda de 2026-11-01: tickets do HUD

Ordem: T7 → T8 → SL12 → SL13 (os dois últimos na Spec do B-016). Todos usam a Unity e seguem o WIP 1.
O T8 toca `Conjurador.prefab` e `Sandbox.unity`, que o SL12 e o SL13 tocam de novo: em série.

### T7 — `TransformAnchor` no Core
- Usa Unity: **sim**. Depende de: UI1 (B-015; base não genérica e `AnchorRegistrar`).
- Escopo:
  - `Runtime/Core/Anchors/TransformAnchor.cs` (novo): `public sealed class TransformAnchor : RuntimeAnchor<Transform>`, `[CreateAssetMenu(menuName = "SpellBook/Core/Transform Anchor", fileName = "ANC_")]`, `[HelpURL(DocUrls.Concepts + "#ancoras")]`;
  - `Editor/Core/Inspector/CoreEditors.cs` (a linha do tipo, como as do UI1);
  - `Tests/Core/PlayMode/TransformAnchorTests.cs` (novo);
  - `Documentation~/manual/inicio/conceitos.md` (seção de âncoras: o `TransformAnchor` e o uso como "objeto presente");
  - `CHANGELOG.md`.
- Testes red:
  - `TransformAnchor_ValueTypeIsTransform`;
  - `Registrar_EmptyValue_ProvidesOwnTransform`: o `AnchorRegistrar` sem `value` publica o `Transform` do próprio objeto no `OnEnable`;
  - `Registrar_Disable_LeavesAnchorEmpty_UntypedValueNull`;
  - `Registrar_DestroyedObject_AnchorReadsEmpty`.
- Critério de pronto: os quatro verdes; a suíte inteira verde; a cobertura do Editor do Core segue verde com o tipo novo.
- Tamanho estimado: ~100 linhas; 5 arquivos.

#### Detalhe (2026-11-04)
- CA: sem CA numerado próprio; emenda de 2026-11-01 (H1, D5) e a base do T8 (`ANC_HUD` como "objeto presente").
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/Core/Anchors/TransformAnchor.cs` (novo): `public sealed class TransformAnchor : RuntimeAnchor<Transform>`, `[CreateAssetMenu(menuName = "SpellBook/Core/Transform Anchor", fileName = "ANC_")]`, `[HelpURL(DocUrls.Concepts + "#ancoras")]`, sem membros. O `AnchorRegistrar` (`Runtime/Core/Anchors/AnchorRegistrar.cs:25-28,71-85`) já resolve o valor vazio por `GetComponent(anchor.ValueType)`, que acha o `Transform` do próprio objeto: o registrar não muda.
  - `Editor/Core/Inspector/CoreEditors.cs:7-9`: `TransformAnchorEditor`, uma linha, logo depois da do `AnchorRegistrar` (cobertura de Inspector).
  - `Tests/Core/PlayMode/TransformAnchorTests.cs` (novo; campos do registrar pelo `SerializedObject` com o objeto inativo, como `Tests/Core/PlayMode/AnchorRegistrarTests.cs:117-142`).
  - Sem `Documentation~/manual/inicio/conceitos.md` e sem `CHANGELOG.md`, embora o escopo acima os cite: pelo `AGENTS.md` ("WIP, trava e testes"), o manual só entra no passe de documentação (emenda de 2026-11-03 da 0007), e o CHANGELOG só em ticket que quebra algo. O T7 é MINOR, sem quebra. A seção de âncoras do `conceitos.md` (o `TransformAnchor` como "objeto presente") e a linha "Adicionado" da seção "Quebras de compatibilidade" ficam para o passe de documentação. Nenhum CA muda.
  - Paralelo com o UI5 (#239): o UI5 toca `UIEditors.cs`, `Runtime/UI/*`, `Tests/UI/PlayMode/WidgetBindersTests.cs`, `visao-geral.md` e `CHANGELOG.md`. O T7 não toca `CHANGELOG.md` nem nenhum desses, e o UI5 não toca `CoreEditors.cs`. Não há arquivo em comum.
- Testes red (PlayMode, `SpellBook.Core.Tests.PlayMode`, classe `TransformAnchorTests`): `TransformAnchor_ValueTypeIsTransform`; `Registrar_EmptyValue_ProvidesOwnTransform`; `Registrar_Disable_LeavesAnchorEmpty_UntypedValueNull`; `Registrar_DestroyedObject_AnchorReadsEmpty`. Antes do tipo existir, falham na compilação (`TransformAnchor` não existe).
- Critério de pronto: os quatro verdes no filtro `\.TransformAnchorTests\.`; `SpellBookEditorCoverageTests` e `HelpUrlTests` (EditMode) verdes com o tipo novo; a suíte completa roda na `/revisao`.
- Tamanho: ~110 linhas e 3 arquivos (mais o Detalhe, que não conta), pela ADR 0042. Exceção: nenhuma.
- Commit: `feat(core): TransformAnchor para publicar objeto de cena`

### T8 — Console sem HUD: tique e falha por canal, ligados pela âncora do HUD
- Usa Unity: **sim** (pasta renomeada pela ADR 0028). Depende de: T7, AV5 (assinatura nova do `OnAbilityFailed` no `SandboxCaster`), EV21 (Sample refeito, `RaiseEventAction` no tique de efeito pelo EV15) e T6.
- Escopo:
  - `Samples~/Sandbox/Scripts/SandboxCaster.cs` (`failureChannel`, D7);
  - `Samples~/Sandbox/Scripts/SandboxDirector.cs` (`hudAnchor`, `consoleWithoutHud`, D7);
  - `Samples~/Sandbox/Data/UI/ANC_HUD.asset`, `Data/Events/CH_QueimaduraTique.asset`, `Data/Events/CH_FalhaDeConjuracao.asset` (novos), `Data/Effects/GE_Queimadura.asset`;
  - `Samples~/Sandbox/Prefabs/Conjurador.prefab`, `Samples~/Sandbox/Scenes/Sandbox.unity` (objeto "Console sem HUD" e campos do diretor);
  - `Documentation~/manual/exemplos/sandbox.md` (tabela dos scripts e origem das mensagens do Console).
- Testes red: nenhum automático de comportamento (a cena é conferida à mão, decisão do usuário). As guardas do T1 e do T2a passam a cobrir os assets e os scripts mudados (`[HelpURL]`, `Update` sem `Find`/`GetComponent`, sem `UnityEngine.UI`/`TMPro`, `.meta` e GUID único). Red forçado de conferência: pôr `using TMPro;` no `SandboxDirector`, ver o `SampleSources_UseNoInputSystemUiOrEditor` falhar e tirar.
- Critério de pronto:
  - com a pasta renomeada: zero erros e avisos de compilação do `SpellBook.Samples.Sandbox`, a asmdef sem referência nova, e a `SpellBook > Validar projeto` sem erro nos assets do Sample;
  - em Play, sem HUD (a âncora vazia): CA8, CA9 e o "Queimadura: tique" a cada 0,5 s como antes do ticket, e nenhum "Missing" (CA2);
  - em Play, com um `AnchorRegistrar` temporário publicando qualquer objeto na `ANC_HUD`: tique e falha somem do Console, e acerto, pausa, câmera lenta e reinício continuam (CA30, parte). O objeto temporário sai antes do commit;
  - a suíte verde com `Samples~`.
- Tamanho estimado: ~60 linhas de código; 9 arquivos (6 fixtures, 1 página).

#### Detalhe (2026-11-04)
- CA: CA2, CA8, CA9 e CA30 (parte: tique e falha) do PRD, pela D7 (itens 2 a 4) e H2/H3 do gate parcial de 2026-11-01. Nenhum CA muda.
- Escopo (caminhos em `Packages/com.spellbook/Samples~/Sandbox/`; autoria pela ADR 0028, com a pasta renomeada):
  - `Scripts/SandboxCaster.cs:19-27`: campo `[SerializeField] private StringEventChannel failureChannel`; `:92-112`: o `HandleFailed` monta o mesmo texto de hoje e faz `failureChannel.Raise(texto, this)` no lugar dos três `Debug.Log`; sem canal, não escreve nada. O "Bola de Fogo conjurada" (`:84-90`) não muda. `using SpellBook.Events` (Events já está na asmdef).
  - `Scripts/SandboxDirector.cs:142-167`: campos `RuntimeAnchor hudAnchor` e `EventListener consoleWithoutHud`; `:172-175` (`Start`) e `:216-224` (`Restart`): `consoleWithoutHud.enabled = hudAnchor == null || hudAnchor.UntypedValue == null`, só quando `consoleWithoutHud` não é nulo. Sem sondagem por quadro e sem assinar o `Changed` (D7, item 4).
  - `Data/UI/ANC_HUD.asset` (novo, `TransformAnchor`), `Data/Events/CH_QueimaduraTique.asset` (novo, `VoidEventChannel`), `Data/Events/CH_FalhaDeConjuracao.asset` (novo, `StringEventChannel`), criados pela Unity com `.meta` gerado (e o `Data/UI.meta` da pasta nova).
  - `Data/Effects/GE_Queimadura.asset`: o `OnTick` troca a `DebugLogAction` "Queimadura: tique" por uma `RaiseEventAction` com `EventEmission<Void>` em `CH_QueimaduraTique` e `payloadSource` Fixed.
  - `Prefabs/Conjurador.prefab`: `SandboxCaster.failureChannel` = `CH_FalhaDeConjuracao`. Ao salvar, a Unity também grava o `startingEffects: []` do ASC, campo que já existe no pacote e faltava no YAML (sem mudança de valor).
  - `Scenes/Sandbox.unity`: objeto novo "Console sem HUD" com `EventListener` (`VoidEventResponse` em `CH_QueimaduraTique` → `LogReaction` "Queimadura: tique"; `StringEventResponse` em `CH_FalhaDeConjuracao` → `LogPayloadReaction` "{0}"); no "Sandbox Director", `hudAnchor` = `ANC_HUD` e `consoleWithoutHud` = esse ouvinte.
  - A edição dos assets, do prefab e da cena é feita na Unity por um script de Editor descartável em `Assets/__SpellBookTestTemp_T8/` (ignorado pelo `.gitignore`), rodado em batchmode com `-executeMethod` e apagado antes dos testes. Nenhum YAML é escrito à mão.
  - Fora do ticket, embora o escopo acima cite `Documentation~/manual/exemplos/sandbox.md`: pelo `AGENTS.md` ("WIP, trava e testes") e pela emenda de 2026-11-03 da 0007, o manual só entra no passe de documentação. A tabela dos scripts (campos novos) e a origem das mensagens do Console (tabela "O que fica no Console com o HUD" da D7 e o risco 5) ficam para o passe. O `Samples~/Sandbox/README.md` também fica para o passe. Sem `CHANGELOG.md`: a emenda não quebra nada (seção "Quebras de compatibilidade").
  - Paralelo com o UI5a: o UI5a toca `Runtime/UI/`, `Editor/UI/` e `Tests/UI/`; o T8 só toca `Samples~/Sandbox/` e este Detalhe. Não há arquivo em comum.
- Testes red (EditMode, `SpellBook.Core.Tests.EditMode`, classe `SampleStructureTests`): nenhum teste novo de comportamento (cena conferida à mão, decisão do usuário). As guardas do T1 e do T2a cobrem os scripts e os assets mudados (`SampleSources_UseNoInputSystemUiOrEditor`, `SampleSources_HaveNoFindOrGetComponentInUpdate`, `SampleBehaviours_HaveHelpUrlToSamplesPage`, `SampleFiles_EveryAssetHasMetaAndUniqueGuid`, `Package_HasNoUntildedSamplesFolder`). Red forçado: `using TMPro;` no `SandboxDirector`, ver o `SampleSources_UseNoInputSystemUiOrEditor` falhar e tirar.
- Critério de pronto:
  - com a pasta renomeada: zero erros e avisos de compilação do `SpellBook.Samples.Sandbox`, a asmdef sem referência nova, e o `ProjectValidator` (o mesmo da `SpellBook > Validar projeto`, aplicado aos assets, prefabs e cena do Sample) sem erro;
  - com a pasta de volta como `Samples~` e sem `Samples.meta`: as sete guardas da `SampleStructureTests` verdes;
  - pendente para o usuário (conferência manual no Editor): em Play sem HUD, CA8, CA9, o "Queimadura: tique" a cada 0,5 s e nenhum "Missing" (CA2); com um `AnchorRegistrar` temporário na `ANC_HUD`, tique e falha somem do Console e acerto, pausa, câmera lenta e reinício continuam (CA30, parte);
  - a suíte completa roda na `/revisao`.
- Tamanho: ~45 linhas e 8 arquivos (2 scripts, 6 fixtures; `.meta` e o Detalhe não contam), pela ADR 0042. Exceção: nenhuma.
- Commit: `feat(samples): Console sem HUD ligado pela âncora do HUD`

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

## Perguntas do gate parcial da emenda (2026-11-01)

- **H1. `TransformAnchor` no Core (T7).** É API pública nova no pacote, pedida pelo Sample, mas genérica: serve a qualquer objeto de cena. Muda o D5 aprovado ("nenhum tipo de `Runtime/` além de `DocUrls`"). Entra?
- **H2. Detecção do HUD pela âncora (D7).** Diretor lê a `ANC_HUD` no `Start` e no `R` e liga ou desliga o "Console sem HUD". O tique sai da `DebugLogEffectAction` e a falha sai do `Debug.Log` do `SandboxCaster`, e os dois passam por canal (dois scripts existentes mudam; nenhum script novo, R25). Aprovado?
- **H3. Leitura do CA30.** O "Dano recebido: 5" do Ouvinte de Log (S2) continua a cada tique, também com o HUD. Sai só a linha própria do tique. Confirma?

**Respostas do usuário (2026-11-01):** H1, H2 e H3 aprovadas como propostas.
- **H1:** o `TransformAnchor` entra no Core (T7). A D5 recebe a emenda marcada como aprovada.
- **H2:** a detecção do HUD é pela `ANC_HUD` (D7). O tique e a falha passam por canal, e o "Console sem HUD" só fica ligado quando a âncora está vazia.
- **H3:** o "Dano recebido: 5" do Ouvinte de Log (S2) continua a cada tique, também com o HUD. Sai só a linha própria do tique.
