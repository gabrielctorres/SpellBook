# PRD: Módulo Cues (feedback por tag: VFX e áudio em pool)
Backlog: B-013 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção B ("Gameplay Cues"), itens D15 e D16 do relatório, seção F, seção H.9 e decisões I5, I22 e I37. ADRs [0012](../adr/0012-mapa-de-modulos-e-asmdefs.md) (`SpellBook.Cues` referencia Core, Tags e Time; o GAS não referencia Cues), [0013](../adr/0013-serializereference-como-costura-entre-modulos.md) (costura), [0015](../adr/0015-spawnerasset-como-seam-de-spawn-e-pool.md) (`SpawnerAsset`; a exceção do `PlayCueAction` vale **até este item**), [0018](../adr/0018-hierarquia-unica-de-acoes.md) (ações; "ações que usam Cues ficam em Bridges") e [0014](../adr/0014-tempo-injetavel-itimesource.md) (tempo).
Depende de: nada para o **módulo** (parte 1). A **parte 2** (integração com o GAS em `SpellBook.GAS.Bridges`) depende de **B-003** (`OnEffectApplied`/`OnEffectRemoved`), de **B-012** (a base de ação nova) e, para o valor do cue vir do efeito, de **B-007** (`EffectContext`). Este PRD usa o que eles entregam e não reescreve o escopo deles (seção 6).
Este PRD não tem foco em nenhum jogo. As menções a jogos no plano são histórico.

---

## 1. Problema (contexto)

Hoje o "feedback" de uma habilidade (partícula, som) é uma ação com prefab e clipe dentro da própria habilidade:

- **Sem roteamento por tag.** `PlayCueAction` guarda um `GameObject` e um `AudioClip`. Trocar o som de "fogo" no jogo todo exige editar cada habilidade; o designer de áudio e o designer de habilidade trabalham no mesmo asset.
- **O som não usa pool e some do controle.** `PlayCueAction` toca o áudio com `AudioSource.PlayClipAtPoint`: cria um objeto a cada tocada, sem pool, sem volume ou altura (`pitch`) por tocada, sem controle de simultâneos e sem seguir um alvo.
- **O VFX não volta.** O `PlayCueAction` e o `SpawnVFXEffectAction` criam o prefab e **não o devolvem** (o ADR 0015 registrou a exceção "até o módulo Cues"): num jogo longo, os efeitos acumulam.
- **Não existe cue "enquanto o efeito dura".** "Aura de fogo enquanto o buff está ativo, some quando acaba" é código do jogo escutando o efeito e guardando a referência do objeto.
- **Sem parâmetros.** Um dano maior toca o mesmo som no mesmo volume: o cue não sabe a magnitude, o instigador nem a normal do impacto.
- **Cue amarrado ao GAS.** O mesmo mecanismo serviria a um jogo sem GAS (um pulo, um passo, um clique), mas hoje só existe dentro de uma ação de habilidade.

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, pelo Inspector e sem script no caso comum:

1. definir um **cue** como asset (`CueDefinition`) ligado a uma **tag** (por exemplo, `Cue.Impacto.Fogo`) com uma lista de VFX e de sons;
2. **tocar um cue por tag** de qualquer lugar (um roteador), com **parâmetros** (posição, rotação, alvo, instigador, magnitude);
3. ter VFX e áudio **em pool**, devolvidos sozinhos ao fim (partículas, duração, ou fim do efeito), pelo `SpawnerAsset` (sem singleton, sem `PlayClipAtPoint`);
4. ter cues de **explosão** (`Burst`, toca uma vez) e **contínuos** (`Looping`, liga e desliga por um identificador);
5. usar o módulo **sozinho**, sem o GAS;
6. (parte 2) ligar cues a habilidades e a efeitos **por tag**, só com o `GAS.Bridges` no projeto: duas ações, "tocar cue" (`ExecuteCueAction`) e "ligar cue" (`AddCueAction`, some quando o efeito ou a ativação acaba). Sem lista de cues no efeito e sem componente de ponte.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Designer de som e de VFX** | Editar `CueDefinition` num lugar só, trocar o som de uma tag sem tocar em habilidade, ver quantos cues simultâneos tocam |
| **Designer técnico** | Em habilidade ou efeito, escolher uma **tag de cue** em vez de arrastar prefab e clipe |
| **Desenvolvedor de gameplay** | `router.Play(tag, parâmetros)` por código, com um identificador para parar o cue contínuo; sem NRE por pool ausente |
| **Mantenedor do pacote** | Um módulo de feedback que o GAS não referencia; objetos criados e devolvidos só pelo `SpawnerAsset` |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, tocar por tag:** a biblioteca tem `Cue.Impacto.Fogo` com um VFX e dois sons. `router.Execute(tag, parâmetros)` cria o VFX na posição dos parâmetros e toca **um** dos sons (P3: sorteio ou todos).
- **C2, tag sem cue:** tocar uma tag sem definição não lança exceção e registra um aviso **só em desenvolvimento**; o retorno diz que nada tocou.
- **C3, hierarquia de tags:** com `Cue.Impacto` e `Cue.Impacto.Fogo` definidos, tocar `Cue.Impacto.Fogo.Forte` usa a definição **mais específica que casa** (P4).
- **C4, parâmetros:** `CueParameters.Magnitude = 0,5` ajusta o volume e a escala conforme a configuração do cue; sem magnitude, vale o padrão da definição.
- **C5, VFX volta sozinho:** um VFX de partícula volta ao `SpawnerAsset` quando as partículas acabam; um VFX com duração fixa volta ao fim da duração; ambos pelo tempo do `ITimeSource` (pausa e câmera lenta valem).
- **C6, áudio em pool:** o som toca num `AudioSource` de um objeto criado pelo spawner e **volta ao fim do clipe**; dois sons simultâneos usam dois objetos; depois de acabarem, os objetos voltam ao pool (a contagem de objetos vivos não cresce).
- **C7, cue contínuo:** `var handle = router.Add(tag, parâmetros)` liga um cue `Looping` (partícula em loop, som em loop); `router.Remove(handle)` desliga e devolve os objetos; `Remove` repetido não faz nada.
- **C8, seguir o alvo:** um cue com "anexar ao alvo" segue o `Transform` dos parâmetros; se o alvo é destruído ou desativado, o cue contínuo é **removido sozinho** sem exceção.
- **C9, limite de simultâneos:** com `MaxSimultaneous = 3`, o quarto cue igual **não toca** (ou substitui o mais antigo, P5) e nenhum objeto a mais é criado.
- **C10, sem lixo:** 10 000 `Execute` com pool aquecido não alocam memória gerenciada.
- **C11, módulo sozinho:** um projeto só com Cues, Tags, Time e Core compila e roda os cenários C1 a C10 (teste de assemblies).
- **C12, ação e efeito por tag (parte 2, T):** uma habilidade com a `ExecuteCueAction` toca o cue ao rodar; um efeito com uma `AddCueAction` liga o cue ao ser aplicado, desliga ao ser removido (expirou, removido por código, por tag ou pilhas zeradas), e **não** liga de novo ao empilhar (R19a, R19b).
- **C13, spawner ausente (parte 2):** com o spawner sem instância e `canExpand` desligado, o cue pula o VFX com aviso, sem exceção (continuidade do RP-07).
- **C14, Inspector (S):** no host, a `CueDefinition` mostra a lista de VFX e de sons com resumo de uma linha, e o botão "Testar" toca o cue no Play.

## 4. Requisitos funcionais

### Parte 1: o módulo `SpellBook.Cues`

#### 4.1 `CueDefinition` e biblioteca

R1. **`CueDefinition`.** Um `ScriptableObject` (`Create > SpellBook > Cues > Cue`) ligado a uma **`GameplayTag`** (módulo Tags). Campos:
- `Tag`: a tag de roteamento (obrigatória);
- `Kind`: **`Burst`** (toca uma vez e termina) ou **`Looping`** (dura até ser removido) (P2);
- uma lista de **VFX** (`[SerializeReference]`, R2) e uma lista de **sons** (`[SerializeReference]`, R3);
- `MaxSimultaneous` (limite de instâncias do mesmo cue, 0 = sem limite) e, se atingido, a política (P5).

R2. **Item de VFX.** Cada item guarda: o prefab, o `SpawnerAsset` (campo vazio usa o padrão, ADR 0015), como se posiciona (**no mundo** no ponto dos parâmetros, **anexado ao alvo** ou **seguindo o alvo**), um deslocamento (posição e rotação), como a **escala** reage à magnitude (nenhuma, ou uma faixa) e **como termina**: ao parar as partículas (`SpawnerAsset.ReleaseWhenParticlesStop`, ADR 0015), após uma **duração fixa** (segundos, pelo `ITimeSource`) ou **ao ser removido** (só `Looping`).

R3. **Item de som.** Cada item guarda: o `AudioClip`, faixa de **volume** e de **altura** (`pitch`; um valor sorteado por tocada dentro da faixa, com fonte de aleatoriedade injetável nos testes), mistura espacial (`spatialBlend`), o `AudioMixerGroup` opcional, se **dá loop** (só `Looping`) e como reage à magnitude (nenhuma, ou volume por faixa). O som toca num **`AudioSource` de um objeto criado pelo `SpawnerAsset`** (o pacote entrega um prefab padrão e um componente que se devolve ao fim do clipe), **nunca** por `AudioSource.PlayClipAtPoint`.

R4. **Mais de um item.** O cue com **vários VFX** toca todos. O cue com **vários sons** toca **um** sorteado (padrão) ou **todos**, por um campo da definição (P3).

R5. **`CueLibrary`.** Um `ScriptableObject` (`Create > SpellBook > Cues > Library`) com a lista de `CueDefinition`. O roteador lê a biblioteca; o projeto pode ter várias (uma por área) e juntá-las no roteador. O validador acusa **tag repetida** em duas definições da mesma biblioteca e definição **sem tag**.

#### 4.2 Roteador, parâmetros e identificador

R6. **`CueParameters`.** Uma **struct** (sem alocação) com: ponto de origem, rotação, normal do impacto (opcional), `Transform` alvo (para anexar ou seguir), `Transform` do instigador (opcional), `Magnitude` (float) e `NormalizedMagnitude` (0 a 1), e um **par de valores livres** (um `float` e um `int`) para uso do projeto. Não referencia nenhum tipo do GAS (o módulo só conhece Core, Tags e Time). O Bridges **preenche** a struct a partir do `EffectContext` (parte 2).

R7. **`CueRouter`.** O ponto de entrada. Oferece:
- `Execute(GameplayTag tag, in CueParameters parâmetros)`: toca um cue `Burst` (um cue `Looping` chamado por `Execute` toca uma vez e termina por uma duração, ou é rejeitado com aviso, P2);
- `Add(GameplayTag tag, in CueParameters parâmetros)`: liga um cue `Looping` e devolve um **`CueHandle`** (struct);
- `Remove(CueHandle handle)`: desliga, devolve os objetos ao spawner; repetido ou com handle inválido não faz nada nem lança exceção;
- `RemoveAll(GameplayTag tag)` e `RemoveAll()`: desligam todos os cues ativos, para trocar de cena;
- retorno que diz se algo foi tocado (C2).

R8. **Como o roteador é encontrado.** **Sem singleton, sem `Find`** (ADR 0015). O roteador é um componente (`MonoBehaviour`) ou um asset, referenciado por **campo serializado** e/ou publicado num `RuntimeAnchor` (Core) escolhido no Inspector; um campo vazio num sistema do pacote usa o roteador **padrão** do projeto, se houver, ou avisa **em desenvolvimento** e não toca nada (sem exceção). A forma exata (componente por cena, `RuntimeAnchor<CueRouter>`, asset) é do Arquiteto (P7).

R9. **Casamento por tag e hierarquia.** O roteador usa o casamento hierárquico de `GameplayTag` (B-006): a definição **mais específica que casa** com a tag pedida é a usada (C3). Sem definição que case, nada toca (C2). A busca é indexada por tag, **sem alocar** e sem varrer a lista a cada `Execute`.

R10. **Tempo.** A duração fixa do VFX e do som usa o `ITimeSource` do roteador (campo `TimeSourceAsset`; padrão: o escalado, como o `Projectile` hoje, ADR 0014). Em pausa, os cues pausados não terminam. **Nenhum cue usa `Time.time`** nem corrotina da Unity.

R11. **Cue contínuo e dono destruído.** Um cue `Looping` anexado a um alvo é removido sozinho se o alvo é destruído ou desativado (C8). O roteador **não** guarda referência forte a objeto destruído e **não** lança exceção ao limpar.

R12. **Limite de simultâneos e orçamento.** Se `MaxSimultaneous` é atingido, a política é **descartar o novo** (padrão, P5) ou **substituir o mais antigo**. Não há limite global no módulo (o `SpawnerAsset` pode limitar pelo pool).

R13. **Spawner sem instância.** Um `SpawnerAsset` que devolve `null` (pool vazio, `canExpand` desligado, RP-07) faz o cue **pular** aquele item com aviso em desenvolvimento, sem exceção e sem tocar o restante.

#### 4.3 Editor, validação e documentação

R14. **Inspector** (UI Toolkit, ADR 0026, herdando de `SpellBookEditor`): `CueDefinition` com a lista de VFX e de sons, cada item com **resumo de uma linha** ("VFX `Fogo_Impacto` · anexado ao alvo · termina ao parar as partículas"), o seletor de tipo de item (`[TypePicker]`), o botão **Testar** (só em Play, com um ponto de teste) e a validação no topo. `CueLibrary` com busca por tag. `CueRouter` com a biblioteca, o spawner e a fonte de tempo.

R15. **Validação** (`IValidatable`): definição sem tag; tag repetida na biblioteca; definição sem VFX e sem som (aviso); prefab de VFX nulo; clipe nulo; faixa de volume ou de altura invertida (mínimo maior que o máximo) ou fora de 0 a 1 (volume); duração fixa negativa; item `Looping`-only (loop, "ao ser removido") numa definição `Burst`; roteador sem biblioteca. Mensagens clicáveis.

R16. **Documentação.** Uma página **Cues** descreve: o que é, `CueDefinition`, biblioteca, roteador, parâmetros, `Burst` e `Looping`, pool e devolução, como criar um item de VFX ou de som próprio (subclasse por `[SerializeReference]`, ADR 0013), e o que o módulo **não** faz. Os tipos novos têm `[HelpURL]` (decisão I15).

R17. **Itens próprios.** Os itens de VFX e de som são pontos de extensão `[SerializeReference]` (ADR 0013): o projeto cria o seu (um cue que mexe num `Volume` de pós-processamento, num shake de câmera ou num feedback háptico) como subclasse, sem tocar no módulo. O pacote entrega **só** o item de VFX e o de som. O que entra em um item não pode guardar estado de execução na instância serializada (ADR 0013).

### Parte 2: integração com o GAS (em `SpellBook.GAS.Bridges`)

R18. **Referência de módulo.** O `SpellBook.GAS.Bridges` passa a referenciar `SpellBook.Cues` (ADR 0012, "quando o primeiro tipo que o usa entrar no Bridges"). **O GAS não referencia Cues e o Cues não referencia o GAS.**

R19. **`ExecuteCueAction` (ação por tag).** Uma ação da base de ações do B-012, em `Bridges`, que toca um cue `Burst` **por tag** com os parâmetros montados do contexto de execução: ponto, alvo (por `TargetSelector` da ação), instigador, nível e **magnitude** (de um valor da ação ou do `EffectContext`, B-007). Substitui o `PlayCueAction` por prefab e clipe (P1). Não bloqueia a sequência (instantânea).

R19a. **`AddCueAction` (cue ligado).** Outra ação do Bridges, que liga um cue `Looping` **por tag** (mesmos parâmetros do R19) e o **remove sozinho** quando o **efeito** ou a **ativação** que a executou termina (expirou, removido por código, por tag, pilhas a zero; ativação terminada ou cancelada). A ação guarda o `CueHandle` no contexto de execução (sem estado na instância serializada, ADR 0013); empilhar o efeito **não** liga de novo.

R19b. **Só por ações.** O GAS dispara cues **somente** por `ExecuteCueAction` e `AddCueAction`. **Não há** lista de cues no `GameplayEffect` nem no `GameplayAbility`, e **não há** componente de ponte entre o ASC e o roteador. Decisão do gate de 2026-10-31 (ADR 0037).

R19c. **Sem roteador configurado.** Se a ação não acha o roteador (R8), ela **avisa em desenvolvimento e não toca**; sem exceção.

R20. **`PlayCueAction` e `SpawnVFXEffectAction` saem do GAS.** O ADR 0015 deixou a exceção "`AudioSource.PlayClipAtPoint` e a falta de devolução do VFX" **até este item**. Ao fim dele: o `PlayCueAction` por prefab e clipe **e** o `SpawnVFXAction` (`SpawnVFXEffectAction`) **deixam de existir no GAS** (quebra aceita, ADR 0009). VFX e áudio passam a existir **só no Cues**, por tag, com devolução ao pool. O `PlayClipAtPoint` **não existe mais** no pacote.

R21. **Sem lista de cues no efeito.** Substituído pelo R19b: o `GameplayEffect` **não** tem lista de tags de cue. Para um cue "enquanto o efeito dura", o efeito usa uma `AddCueAction` (R19a); para um cue pontual, uma `ExecuteCueAction` (R19). Parâmetros: alvo = dono do efeito, instigador e magnitude do `EffectContext`/`EffectSpec` (B-007, ADR 0030).

R22. **Sem componente de ponte.** Não existe `AbilitySystemCueBridge`. A remoção do cue ligado (R19a) é feita pelo próprio fim do efeito ou da ativação, sem componente no ASC e sem inscrição em eventos pelo projeto.

R23. **Cue de habilidade.** Cue na ativação, no término ou no cancelamento da habilidade usa as mesmas ações nos eventos nomeados da habilidade (B-012): `ExecuteCueAction` para tocar, `AddCueAction` para ligar até a ativação acabar. Não há campo de cue no `GameplayAbility`.

R24. **Validação do Bridges** (`IValidatable`): tag de cue que **nenhuma biblioteca** define (aviso, quando o Editor consegue ler as bibliotecas do projeto); `ExecuteCueAction` com cue só `Looping` e `AddCueAction` com cue só `Burst` (aviso); ação sem tag ou com tag inválida; `AddCueAction` fora de efeito ou ativação (nada a que ligar o fim, erro).

## 5. Requisitos não funcionais

N1. **Alocação:** `Execute`, `Add` e `Remove` não alocam depois do aquecimento do pool (C10). `CueParameters` e `CueHandle` são structs. O sorteio de som e de altura não aloca.
N2. **Módulos (ADR 0012):** `SpellBook.Cues` referencia **Core, Tags e Time** (a necessidade de `Time` é a pergunta aberta da ADR 0012 e se resolve na Spec, P9: se a duração fica no módulo, referencia; se fica no spawner, sai). Não referencia GAS, Events, Pooling, Teams nem UI. O Bridges referencia Cues. O teste de assemblies cobre.
N3. **Sem singleton:** nenhum tipo do módulo usa `FindObjectOfType`, `PoolManager.Instance` ou estático mutável. O roteador padrão (R8) é só uma referência opcional, nunca uma busca.
N4. **Tempo:** só `ITimeSource` (ADR 0014); testes com `ManualTimeSource` e um `SpawnerAsset` de teste (factory), sem assets fixos nem áudio real (`AudioSource` pode ser trocado por um fake no teste de roteamento).
N5. **Serialização:** nativa, `[SerializeReference]` para os itens de VFX e de som (ADR 0013, 0026).
N6. **Áudio:** o módulo não toca áudio real em teste EditMode; o contrato do item de som é testável por um dublê; o teste PlayMode do host confirma o som real tocando e voltando ao pool.
N7. **Asmdefs (ADR 0012, emenda 0026):** `SpellBook.Cues`, `SpellBook.Cues.Editor` (Core, Tags, Time, Cues, `Core.Editor`) e os asmdefs de teste seguem o mapa; a **Spec** cria os asmdefs, **não** este PRD.
N8. **Sem regressão:** a suíte do GAS (com B-002 a B-012) passa sem edição de teste antigo, **exceto** os testes do `PlayCueAction` e do `SpawnVFXAction` que a quebra do R20 remove ou troca (o Arquiteto lista na Spec).

## 6. Fora do escopo

- **`SpawnerAsset` e pool:** o contrato e as implementações são do Core e do Pooling (ADR 0015); aqui só são usados. O vazamento do `PoolConfiguration` (RP-05) e a NRE de spawner vazio (RP-07) são tickets rápidos à parte.
- **Efeitos instantâneos e tick:** o fim do efeito que remove o `AddCueAction` (R19a) usa o que o B-003 entrega; a Spec lista o que cobre.
- **A base de ação nova, `TargetSelector`, `Chance`, `Delayed`:** **B-012**. `ExecuteCueAction` é uma ação da base, não a recria.
- **`GameplayThinker`, aura, projétil com perfuração e término:** **B-014**. O cue de "projétil acertou" usa `ExecuteCueAction` num evento do projétil.
- **Números flutuantes e widgets de UI:** módulo de UI (B-015). O "número que sobe" é UI, não cue.
- **Feedbacks de câmera** (shake, hitstop, pós-processamento): extensão do projeto por item próprio (R17); o pacote não os entrega.
- **Música e trilha adaptativa, mixagem e ducking:** fora; o cue entrega um `AudioMixerGroup` opcional e nada além.
- **Áudio 3D avançado** (oclusão, reverb por zona): fora.
- **Ferramenta de pré-visualização no Editor** (tocar o cue fora do Play): fora; o botão "Testar" é só em Play.
- **Rede e replicação.**

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

**Módulo (parte 1)**
- CA1 (T). Cenário C1: `Execute` cria os VFX e toca um som da definição; com "todos os sons" toca todos.
- CA2 (T). Cenário C2: tag sem definição não lança exceção e devolve "nada tocou".
- CA3 (T). Cenário C3: o roteador usa a definição mais específica que casa; a mais geral serve de padrão; o casamento usa a hierarquia de `GameplayTag`.
- CA4 (T). Cenário C4: a magnitude ajusta volume e escala conforme a definição; sem magnitude vale o padrão.
- CA5 (T). Cenário C5: VFX volta ao `SpawnerAsset` ao parar as partículas e ao fim da duração fixa, pelo tempo do `ManualTimeSource`; em pausa não volta.
- CA6 (T). Cenário C6: o som toca num objeto do spawner e volta ao fim do clipe; dois sons simultâneos usam dois objetos; a contagem de vivos não cresce depois.
- CA7 (T). Cenário C7: `Add` liga, `Remove` desliga e devolve; `Remove` repetido e com handle inválido não faz nada; `RemoveAll` limpa tudo.
- CA8 (T). Cenário C8: o cue anexado segue o alvo; alvo destruído ou desativado remove o cue contínuo sem exceção.
- CA9 (T). Cenário C9: `MaxSimultaneous` respeita a política (descartar o novo ou substituir o mais antigo).
- CA10 (T). Spawner que devolve `null` pula o item com aviso e toca o restante (R13).
- CA11 (T). `PlayClipAtPoint` não existe no pacote (busca de código; ADR 0015) e nenhum tipo do módulo usa `Time.time` nem corrotina da Unity.
- CA12 (T). Fonte de aleatoriedade injetada torna o sorteio de som e de altura determinístico no teste.

**Integração (parte 2)**
- CA13 (T). Cenário C12: `ExecuteCueAction` toca o cue com os parâmetros do contexto; o efeito com uma `AddCueAction` liga o cue ao aplicar e desliga ao remover pelos quatro motivos (expirou, removido, por tag, pilhas a zero); empilhar não toca de novo.
- CA14 (T). `AddCueAction` liga o cue e o remove quando o efeito ou a ativação termina; sem roteador, avisa e não toca (R19c).
- CA15 (T). Cenário C13: spawner vazio no cue pula com aviso, sem exceção.
- CA16 (T). O `PlayCueAction` e o `SpawnVFXAction` não existem mais no GAS (R20).
- CA17 (T). O teste de assemblies da 0-M passa: o GAS não referencia Cues; o Cues não referencia o GAS; o Bridges referencia Cues.

**Desempenho, editor e documentação**
- CA18 (T). Cenário C10: 10 000 `Execute` com pool aquecido alocam 0 bytes gerenciados.
- CA19 (S). Cenário C14: no host, a `CueDefinition` mostra a lista de VFX e sons com resumo de uma linha e o botão "Testar" toca o cue em Play, com som real que volta ao pool.
- CA20 (T). O validador cobre os casos do R15 e do R24, com o objeto clicável.
- CA21 (E). O CHANGELOG cita o módulo `Cues`, o fim do `PlayCueAction` por prefab e `PlayClipAtPoint`, `ExecuteCueAction`, `AddCueAction` e o fim do `SpawnVFXAction`; a página **Cues** existe; os tipos novos têm `[HelpURL]`.
- CA22 (T). A suíte do GAS existente passa sem edição, exceto os testes do `PlayCueAction` e do `SpawnVFXAction` listados na Spec (N8).

## 8. Perguntas em aberto

### Com o usuário

- **P1. Destino do `PlayCueAction` do GAS.** *Respondida (usuário, 2026-10-31): aceita. O `PlayCueAction` por prefab e clipe **deixa de existir** e dá lugar a `ExecuteCueAction` por **tag** em `Bridges` (quebra, ADR 0009).*
- **P2. Cue contínuo.** *Respondida (usuário, 2026-10-31): aceita. O módulo tem **os dois tipos**: `Burst` e `Looping`.*
- **P3. Vários sons.** *Respondida (usuário, 2026-10-31): aceita. A definição **sorteia um** som por padrão, com a opção "tocar todos".*
- **P4. Cue mais específico.** *Respondida (usuário, 2026-10-31): aceita. O roteador usa a **definição mais específica** que casa (hierarquia de tags).*
- **P5. Limite de simultâneos.** *Respondida (usuário, 2026-10-31): aceita. `MaxSimultaneous` opcional (0 = sem limite); ao atingir, o padrão é **descartar o novo** (substituir o mais antigo é opção).*
- **P6. Cues no efeito entram neste item?** *Respondida e superada no gate de 2026-10-31: sem lista nem ponte; só ações (R19 a R19c).*

### Com o Arquiteto (decisão técnica da Spec)

- **P7. Como o roteador é encontrado** (R8): componente por cena, `RuntimeAnchor<CueRouter>`, asset, ou os três; qual o padrão do projeto e como evita `Find`.
- **P8.** *Respondida no gate de 2026-10-31: o `SpawnVFXAction` sai do GAS (R20); a devolução é do Cues.*
- **P9. `SpellBook.Cues` precisa de `Time`?** (ADR 0012, "em aberto"): a duração fixa e o fim do clipe ficam no módulo (referencia Time) ou no spawner; recomendação do Criativo: no módulo, pelo `ITimeSource`, para pausa e câmera lenta valerem.
- **P10. `CueParameters` e `CueHandle`:** o menor conjunto de campos que atende aos itens do B-014 e do Bridges, e se o handle é índice com geração (seguro contra reuso) ou id.
- **P11. Prefab de áudio padrão:** o pacote entrega um prefab com `AudioSource` e o componente de devolução; onde moram (Core, Cues ou `Samples~`), e como a devolução funciona sem corrotina da Unity.
- **P12. Momentos de efeito e habilidade em que as ações rodam** (R19a, R21, R23): "ao aplicar" de efeito instantâneo e o tick dependem do B-003 e do B-004 (relay); a lista exata de momentos que a Spec entrega. Sem inscrição em eventos pelo Bridges (R22).
- **P13. Guarda do `CueHandle`** (R19a): onde o contexto de execução guarda o handle do `AddCueAction` sem alocar (Arquiteto, ADR 0037).

## 9. Ajustes de playtest

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | PRD escrito pelo Criativo a partir do B-013, do plano (D15, D16, seção B e H.9) e das ADRs 0012, 0013, 0014, 0015 e 0018; o Controlador pediu o PRD inteiro e a Spec divide em tickets | Todo o documento | Rascunho |
| 2026-10-31 | Dependências registradas sem puxar escopo: o módulo (parte 1) não depende de nenhum item; a parte 2 depende de B-003, B-012 e B-007; B-014 usa `ExecuteCueAction` | Seção 6, R18 a R24 | Aplicado |
| 2026-10-31 | Gate do Lote D: o GAS dispara cues **só por ações** (`ExecuteCueAction` toca; `AddCueAction` liga e some com o efeito ou a ativação); sem lista de cues no efeito/habilidade e sem componente de ponte; `SpawnVFXAction` sai do GAS junto com o `PlayCueAction` (VFX só no Cues); sem roteador: avisa e não toca; filtro de alvo único (ADR 0035). Supera o P6 | R19, R19a a R19c, R20 a R24 (reescritos); demais menções à lista, à ponte (C12, CA13 a CA16, CA21, P8, P13, seção 6) a conferir na Spec | Aplicado |
| 2026-10-31 | Conferência pós-gate do Lote D: C12, CA13, N8, CA22 e P12 ainda citavam cue "enquanto ativo" no efeito, o `PlayCueAction` sozinho e eventos do Bridges; alinhados a `ExecuteCueAction`/`AddCueAction`, ao fim do `SpawnVFXAction` e à ausência de lista e de ponte. Sem outra menção pendente | C12, N8, CA13, CA22, P12 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P1 a P6: todas as propostas aceitas (`PlayCueAction` por prefab sai; `Burst` e `Looping`; sorteio de som; hierarquia de tags; descartar o novo no limite; cues no efeito entram no último ticket) | R1, R4, R7, R9, R12, R19 a R23 (sem mudança de texto: já descreviam isso) | Aplicado |
