# PRD: UI-1, base de UI e widgets de atributo, tag e números flutuantes
Backlog: B-015 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção H.5 ("Sistema de UI modular"), seção H.7 (prévia de UI), seção E (UI-1) e decisão I.6. ADRs [0017](../adr/0017-ui-em-tres-camadas.md) (UI em três camadas), [0012](../adr/0012-mapa-de-modulos-e-asmdefs.md) (módulos `UI`, `UI.UGUI`, `GAS.UI`), [0013](../adr/0013-serializereference-como-costura-entre-modulos.md) (fontes por `[SerializeReference]`), [0014](../adr/0014-tempo-injetavel-itimesource.md) (driver de tempo), [0015](../adr/0015-spawnerasset-como-seam-de-spawn-e-pool.md) (listas e números em pool), [0023](../adr/0023-texto-estruturado-e-itextprovider.md) (texto) e [0026](../adr/0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md) (editor).
Depende de: **B-003** (eventos de efeito aplicado e removido), **B-005** (`AttributeDisplay.Format`, `TextRoleTheme`, `Hidden`, `OnReachedMax`), **B-004** (`OnEffectBlocked`, para o "bloqueado" nos números flutuantes) e **B-010** (só os tipos de dano por tag, para o estilo por tag dos números flutuantes, R13; **a barra não depende dele**, P1 respondida). **B-008** entra para o formato do texto (valor e rótulo). Este PRD usa o que eles entregam e não reescreve o escopo deles (seção 6).
**Consumidores:** B-016, B-017 e B-018 usam a base deste item. **O Sample ganha o HUD (S4) depois de B-015 e B-016** (decisão do usuário, 2026-10-31; ver 4.8).
Este PRD não tem foco em nenhum jogo. Os nomes do HUD do jogo no plano (`HudProvider`, `UiBar`) são histórico.

---

## 1. Problema (contexto)

O pacote não tem código de UI, e quem usa o GAS precisa mostrar o estado dele:

- **Cada jogo reescreve a HUD.** Barra de vida, texto de mana, ícone de "atordoado" e número de dano que sobe são código do jogo, preso ao GAS, ao kit visual e a singletons (`RunPlayer` estático no plano).
- **A UI acha o dono por busca.** Sem um jeito padrão de dizer "esta barra mostra o jogador", o código usa `Find`, tag de objeto ou estático.
- **A UI faz polling.** Cada widget lê o ASC em `Update`, a cada quadro, mesmo parado.
- **Números flutuantes usam `Instantiate` e `Destroy`**, acumulando lixo num jogo longo (ADR 0015).
- **Sem prévia.** Montar a HUD exige entrar no Play para ver a barra com 50 %.
- **Sem separação.** Um widget que lê o GAS não serve a outro sistema (um inventário, um jogo sem GAS).

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, **pelo Inspector e sem script** no caso comum:

1. montar uma **barra de atributo** (vida, mana, com atributo máximo; **barra simples**, um atributo por barra), um **texto de atributo** e um **indicador de tag** ("atordoado") ligando uma **fonte**, um **resolvedor de dono** e uma **view**;
2. dizer **de quem** é a HUD por `ActorResolver`: no próprio objeto ou pai, por `RuntimeAnchor` (que religa quando o ator renasce) ou explícito;
3. ter **números flutuantes** em pool, com **estilo** por regra (dano, cura, bloqueado), sem `Instantiate` em regime;
4. ter atualização por **evento** (push), com tick **só enquanto algo corre no tempo**, num único driver;
5. **ver a HUD em edição**, com um modelo de prévia por fonte, sem entrar no Play;
6. trocar a view por uma **do projeto** (`IView<T>`) sem tocar na fonte, e usar a base **sem o GAS** (módulo `SpellBook.UI` sozinho).

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Designer técnico / de UI** | Arrastar um prefab de barra, escolher atributo e dono, ver a prévia, trocar a cor pelo tema |
| **Desenvolvedor de gameplay** | `RuntimeAnchor` do jogador; fonte e view próprias por subclasse; nenhum `Find` |
| **Desenvolvedor de UI com kit próprio** | Implementar `IView<BarModel>` e reaproveitar fontes, binders e resolvedor |
| **Mantenedor do pacote** | Três camadas sem vazamento: a view não conhece o GAS; o `SpellBook.UI` não tem uGUI |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, barra de atributo:** um `AttributeSource` com `Vida` e `VidaMax` produz um `BarModel` com valor atual, máximo e fração. Mudar a vida (ou o máximo) atualiza a view **por evento**, sem `Update`.
- **C2, texto de atributo:** o texto mostra o valor pelo formato do atributo (B-005), com a cultura injetada; o `BarModel` traz o texto pronto sem rich text e o papel (a view escolhe a cor).
- **C3, uma barra por atributo (P1):** duas barras sobrepostas ou lado a lado, uma com `Vida` e outra com `Escudo`, cada uma com a sua `AttributeSource`, funcionam de forma independente, sem a barra conhecer o escudo. Não existe camada de escudo dentro da barra.
- **C4, indicador de tag:** o indicador acende quando o ASC tem a tag (ou a consulta `GameplayTagQuery` casa, B-006) e apaga quando sai, por evento.
- **C5, dono por âncora:** o jogador registra o ASC num `RuntimeAnchor` ao nascer; a barra o encontra sem busca. Se o jogador é destruído e **renasce**, a barra religa sozinha ao novo.
- **C6, dono pelo pai e explícito:** `SelfOrParent` acha o ASC no próprio objeto ou num pai; `Explicit` usa o campo. Dono não encontrado **não lança exceção** e a view mostra o estado vazio, com aviso em desenvolvimento.
- **C7, tick só quando precisa:** um driver único lê o `ITimeSource`; sem nada correndo (sem recarga, sem duração) ele **não roda**. Em pausa, o que corre no tempo pausa.
- **C8, números flutuantes:** perder 30 de vida cria um número "-30" no alvo, em pool, estilo "dano"; ganhar vida cria "+20" estilo "cura"; um efeito bloqueado cria "Imune" (B-004). Depois da duração, o objeto **volta ao pool** (a contagem de vivos não cresce).
- **C9, estilo por regra:** a regra "atributo `Vida`, variação negativa → estilo Dano" é um asset (`FloatingTextStyle`); trocar a cor muda o número seguinte.
- **C10, prévia em edição (S):** no host, a barra mostra 50 % e o texto "50 / 100" **sem Play**; no Play, a prévia desliga.
- **C11, view própria:** uma `IView<BarModel>` do teste recebe o modelo sem conhecer o GAS nem a fonte; trocá-la não muda a fonte.
- **C12, módulos:** um projeto só com `SpellBook.UI` (sem GAS e sem uGUI) compila e roda os contratos (teste de assemblies).
- **C13, sem lixo:** 10 000 atualizações de modelo e 1000 números flutuantes em pool alocam 0 bytes gerenciados depois do aquecimento (a string do texto sai de um buffer reaproveitado, P2 do Arquiteto).
- **C14, Inspector (S):** no host, o binder mostra a fonte (seletor de tipo), o resolvedor e a view, com a validação no topo.

## 4. Requisitos funcionais

### 4.1 Contratos (`SpellBook.UI`, sem uGUI nem GAS)

R1. **`IView<TModel>`.** Interface que desenha: recebe o **struct** do modelo e nada além. Não conhece o GAS, a fonte nem um componente de gameplay (ADR 0017).

R2. **`UISource<TModel>`.** Classe abstrata `[SerializeReference]`: lê um sistema para um **dono** e produz um modelo; tem um **modelo de prévia** (R15). O contrato de ligação (`IDisposable` ou `IUIBinding` com `WantsTick`/`Tick`) e o suporte a genéricos em `[SerializeReference]` são do Arquiteto (ADR 0017, "em aberto"; P1 e P2 do Arquiteto).

R3. **Binder.** Um componente por tipo de modelo (`BarBinder`, `TextBinder`, `TagIndicatorBinder`), com a fonte, o `ActorResolver` e a view. Resolve o dono, liga a fonte, recebe as atualizações e entrega o modelo à view. Atualiza por **push** (eventos C# do componente observado); não usa `Update` por widget (ADR 0017, item 1).

R4. **`UIUpdateDriver`.** Um único componente (por campo serializado, sem singleton) que lê o `ITimeSource` (ADR 0014) e chama o `Tick` só dos binders que pedem (`WantsTick`). Sem binder pedindo, ele não faz trabalho. Tem um padrão de tempo (o escalado) escolhido por `TimeSourceAsset`.

R5. **`ActorResolver`.** Resolve o dono (ADR 0017, item 2) em três modos:
- **`SelfOrParent`:** no próprio objeto ou num pai;
- **`FromAnchor`:** por um **`RuntimeAnchor<T>`** (asset, Core) que o ator registra ao nascer e limpa ao morrer; o binder **religa** quando a âncora muda (respawn);
- **`Explicit`:** por referência no Inspector.

Nunca por `Find`, singleton, tag de objeto ou canal global (ADR 0016, 0017). O `RuntimeAnchor<T>` ainda não existe no código e **este item o entrega** no `SpellBook.Core`, com o componente que registra o ator (`AnchorRegistrar`, P3 do Arquiteto).

R6. **`UITheme`.** Um `ScriptableObject` que referencia o `TextRoleTheme` (B-005) e guarda o que a view padrão precisa (cores por papel, fonte, tamanhos). Há um tema padrão do pacote. Cor e estilo são **da view** (ADR 0023).

R7. **Modelos** (structs, sem alocação): `BarModel` (atual, máximo, fração, texto, papel e fração do "rastro"; sem campo de escudo, P1), `TextModel` (texto, papel) e `TagIndicatorModel` (ativo, tag). `SlotModel`, `CastBarModel`, `TooltipModel` e `EffectIconModel` são dos itens B-016 a B-018.

### 4.2 Fontes do GAS (`SpellBook.GAS.UI`)

R8. **`AttributeSource`.** Lê o atual, o máximo (e um secundário opcional) de atributos do ASC dono, e escuta as mudanças de valor (eventos C# do ASC). Produz `BarModel` e `TextModel`. O texto usa o formato do atributo e a cultura injetada (B-005, B-008). `Hidden` (B-005) não impede a leitura; o filtro é do projeto.

R9. **`TagSource`.** Lê uma tag ou uma `GameplayTagQuery` (B-006) no ASC e escuta a entrada e a saída da tag. Produz `TagIndicatorModel`.

R10. **Dependências do módulo.** `SpellBook.GAS.UI` referencia **GAS, UI, Tags, Time** e **não** o Bridges (ADR 0012, 0017). Nenhuma fonte usa o relay de eventos (B-004); a UI lê o ASC pela fonte, nunca por canal.

### 4.3 Widgets padrão (`SpellBook.UI.UGUI`, uGUI + TextMeshPro)

R11. **Views padrão.** O pacote entrega views uGUI que implementam `IView<T>`: **barra** (preenchimento, **rastro atrasado**, texto opcional), **texto** e **indicador de tag** (mostra e esconde um objeto). Cada uma com um **prefab** pronto. Servem a protótipo, Samples e projetos sem kit; não precisam cobrir todo estilo visual (ADR 0017, consequências).

R12. **Dependência de uGUI.** `SpellBook.UI.UGUI` referencia `com.unity.ugui` (uGUI e TextMeshPro). Como o `package.json` declara isso (ADR 0012, "em aberto") é do Arquiteto (P4 do Arquiteto). `SpellBook.UI` e `SpellBook.GAS.UI` **não** referenciam uGUI nem TextMeshPro.

### 4.4 Números flutuantes

R13. **`FloatingTextStyle` e regras.** Um asset de **regras**: "atributo X, variação negativa/positiva → estilo" e "efeito bloqueado → estilo". O estilo traz o papel, a duração, o movimento (subida, deslocamento) e o texto (formato do atributo, B-005). **Crítico e tipo de dano** entram só como **estilo por tag** quando B-010 as entregar; não há cálculo de crítico no pacote (P2).

R14. **`FloatingTextSource` e spawner.** Uma fonte escuta o ASC (variação de atributo e bloqueio) e emite `FloatingTextRequest` (struct: posição, texto, papel, estilo). O spawner uGUI cria o número **só pelo `SpawnerAsset`** e o devolve ao fim da duração, pelo tempo do `ITimeSource` (ADR 0014, 0015). Nunca `Instantiate`, `Destroy` nem corrotina da Unity. Spawner sem instância pula o número com aviso em desenvolvimento, sem exceção (continuidade do RP-07).

### 4.5 Prévia em edição

R15. **`PreviewModel`.** Cada fonte tem um modelo de prévia (a barra em 50 %, o texto "50 / 100", o indicador aceso, um número "-30"), mostrado **fora do Play** (decisão H.7 do plano). No Play, a prévia desliga. Ligar a prévia ao ASC selecionado no Play fica fora (P3).

### 4.6 Editor, validação e documentação

R16. **Inspector** (UI Toolkit, ADR 0026, herdando de `SpellBookEditor`): o binder mostra a fonte com o seletor de tipo (`[TypePicker]`), o resolvedor e a view; o resumo de uma linha ("Barra · Vida / VidaMax · dono: âncora Jogador"); a validação no topo.

R17. **Validação** (`IValidatable`): binder sem fonte ou sem view; fonte sem atributo; atributo máximo igual ao atual (aviso); `FromAnchor` sem âncora; `Explicit` sem referência; `SelfOrParent` num objeto sem ASC (aviso, em cena aberta); regra de número sem estilo; spawner de número ausente; prefab de view sem o componente da view. Mensagens clicáveis.

R18. **Documentação.** Uma página por widget (barra, texto, indicador, números flutuantes) e uma **Visão geral da UI** (as três camadas, o resolvedor, o driver, o tema, a prévia, como criar uma fonte própria e uma view própria). Os tipos novos têm `[HelpURL]` (decisão I15). O CHANGELOG cita os módulos novos e o `RuntimeAnchor<T>`.

### 4.7 Dependência com o Sample

R19. **Sample ganha o HUD depois.** O Sample Sandbox (B-032) **não** usa este módulo agora (R4 do PRD dele). Depois de B-015 e B-016, o Sample ganha o **HUD (S4)**: barras de vida e mana, indicador de recarga, números de dano e mensagens, **em uGUI**, com a entrada pelo **Input Manager legado** (decisões do usuário, 2026-10-31). Este item **não** altera o Sample; a emenda ou o PRD do S4 é do Sample.

## 5. Requisitos não funcionais

N1. **Alocação:** atualizar o modelo, o driver e os números flutuantes não alocam depois do aquecimento (C13). Disparar eventos sem ouvinte não aloca.
N2. **Módulos (ADR 0012, 0017):** `SpellBook.UI` referencia **Core e Time**; `SpellBook.UI.UGUI` referencia UI e uGUI/TMP; `SpellBook.GAS.UI` referencia GAS, UI, Tags e Time. O GAS **não** referencia UI. Nenhuma referência circular. O teste de assemblies cobre.
N3. **Sem estado global:** nenhum singleton, `Find`, `FindObjectOfType` ou estático mutável; o dono chega pelo resolvedor (ADR 0017).
N4. **Tempo:** só `ITimeSource` pelo driver; testes com `ManualTimeSource`.
N5. **Serialização:** nativa; fontes por `[SerializeReference]` (ADR 0013); a fonte guarda **configuração**, nunca o estado da ligação (ADR 0013).
N6. **Determinismo:** testes com factories (`GasFactory`, factory de views de teste), sem assets fixos nem Canvas real nos testes de lógica; a view uGUI tem teste PlayMode no host.
N7. **Asmdefs (ADR 0012, emenda 0026):** `SpellBook.UI`, `SpellBook.UI.UGUI`, `SpellBook.GAS.UI` e os `.Editor` e de teste correspondentes seguem o mapa; a **Spec** cria os asmdefs, **não** este PRD.
N8. **Sem Odin e sem IMGUI** no editor (ADR 0026).

## 6. Fora do escopo

- **Slot e barra de habilidades, recarga, cargas e motivo de falha:** **B-016**.
- **Tooltip e `TooltipTrigger`:** **B-017**. **Lista de buffs:** **B-018**. **Barra de cast** (`ChannelSource`): **B-016** (P4 respondida; ver o [PRD do B-016](ui-slot-e-barra-de-habilidades.md)).
- **Camada de escudo dentro da barra:** fora (P1 respondida). Quem quer escudo usa uma segunda barra com o atributo de escudo (C3). Se um dia a camada interna for pedida, entra como item novo e aditivo.
- **Atributos, formato, tema e metadados, eventos de efeito, relay, dano e escudo:** **B-005**, **B-003**, **B-004**, **B-010**. Aqui são só lidos.
- **Descrição estruturada e `ITextProvider`:** **B-008**. O texto exibido aqui usa o formato do atributo e o provedor quando o B-008 existir.
- **UI Toolkit em runtime** (`SpellBook.UI.Toolkit`): depois (ADR 0017); basta implementar `IView<T>` num asmdef novo.
- **Kit visual completo:** os widgets são de protótipo e Sample.
- **Câmera, posicionamento mundo para tela** além do ponto do número flutuante (barra de vida sobre a cabeça, nameplate): a barra funciona em Canvas de tela ou de mundo; o seguimento do alvo é do projeto, com a receita no manual.
- **Entrada** (teclas, mouse): não há entrada neste item.
- **Rede e replicação.**

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

- CA1 (T). Cenário C1: `AttributeSource` produz o `BarModel` e a view atualiza por evento, sem `Update`.
- CA2 (T). Cenário C2: o texto usa o formato do atributo e a cultura injetada, sem rich text.
- CA3 (T). Cenário C3: duas barras com atributos diferentes (vida e escudo) atualizam de forma independente; o `BarModel` não tem campo de escudo.
- CA4 (T). Cenário C4: o indicador acende e apaga pela entrada e saída da tag, por evento.
- CA5 (T). Cenário C5: o dono por âncora é achado sem busca e a barra religa ao respawn.
- CA6 (T). Cenário C6: `SelfOrParent`, `FromAnchor` e `Explicit` acham o dono; dono ausente não lança exceção.
- CA7 (T). Cenário C7: o driver só roda com binder pedindo tick e respeita pausa do `ITimeSource`.
- CA8 (T). Cenário C8: os números de dano, cura e "bloqueado" nascem em pool e voltam ao fim; a contagem de vivos não cresce.
- CA9 (T). Cenário C9: a regra do asset escolhe o estilo; mudar o asset muda o número seguinte.
- CA10 (S). Cenário C10: a prévia aparece em edição e some no Play.
- CA11 (T). Cenário C11: uma `IView<T>` própria recebe o modelo; trocar a view não muda a fonte.
- CA12 (T). Cenário C12 e N2: o teste de assemblies da 0-M passa; `SpellBook.UI` e `SpellBook.GAS.UI` não referenciam uGUI nem TMP; o GAS não referencia UI.
- CA13 (T). Cenário C13: 10 000 atualizações e 1000 números alocam 0 bytes gerenciados depois do aquecimento.
- CA14 (S). Cenário C14: no host, o Inspector do binder mostra o seletor de fonte, o resolvedor e a validação.
- CA15 (E). Nenhum tipo do módulo usa singleton, `Find`, `FindObjectOfType`, `Instantiate` ou `Destroy` em regime, `Time.time` nem corrotina da Unity (busca de código).
- CA16 (T). O validador cobre os casos do R17, com o objeto clicável.
- CA17 (E). O CHANGELOG cita os módulos e o `RuntimeAnchor<T>`; as páginas existem; os tipos novos têm `[HelpURL]`.
- CA18 (E). O PRD do Sample (B-032) fica sem alteração neste item; a nota do R19 está registrada.

## 8. Perguntas em aberto

### Com o usuário

- **P1. Camada de escudo na barra.** *Respondida (usuário, 2026-10-31): **barra simples, sem camada de escudo** (diferente da proposta). Escudo é uma segunda barra com outro atributo (C3). R7, R11, C3, CA3 e a seção 6 ajustados.*
- **P2. Crítico e tipo de dano nos números flutuantes.** *Respondida (usuário, 2026-10-31): aceita. Estilo por regra (dano, cura, bloqueado) e, quando B-010 entregar os tipos de dano por tag, estilo por tag, sem lógica de crítico no pacote (R13).*
- **P3. Prévia ligada ao ASC selecionado.** *Respondida (usuário, 2026-10-31): aceita. Prévia só em edição (R15).*
- **P4. Barra de vida sobre a cabeça.** *Respondida (usuário, 2026-10-31): aceita. Sem `WorldAnchorFollower`; o seguimento é receita do manual (seção 6).*

### Com o Arquiteto (decisão técnica da Spec)

- **P1. Contrato da ligação** (ADR 0017, "em aberto"): a fonte devolve `IDisposable` ou um `IUIBinding` com `WantsTick`/`Tick`; como o driver chama sem alocar.
- **P2. Genéricos em `[SerializeReference]`** (ADR 0013): `UISource<TModel>` fechado por tipo ou base não genérica; como o texto do modelo sai sem alocar (buffer reaproveitado por view).
- **P3. `RuntimeAnchor<T>` e `AnchorRegistrar`:** assinatura (evento de mudança, valor atual), onde mora (Core, proposta do plano) e como religa no respawn sem vazar ouvinte.
- **P4. Dependência de `com.unity.ugui`** (ADR 0012, "em aberto"): como o `package.json` a declara e qual a versão mínima na 6000.0.
- **P5. Eventos que o GAS precisa expor para as fontes** (ADR 0017, consequências): variação de atributo (incluindo o máximo), entrada e saída de tag e `OnEffectBlocked`; o que o B-003, o B-005 e o B-004 já entregam e o que falta.
- **P6. Ordem entre tickets:** contratos e resolvedor primeiro; `AttributeSource` e a barra depois; indicador de tag; números flutuantes por último (dependem de B-004 e, para o escudo, de B-010); prévia e Inspector no fim.

## 9. Ajustes de playtest

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | PRD escrito pelo Criativo a partir do B-015, do plano (H.5, H.7, UI-1) e das ADRs 0017, 0012, 0013, 0014, 0015, 0023 e 0026; o Controlador pediu o PRD inteiro e a Spec divide em tickets | Todo o documento | Rascunho |
| 2026-10-31 | usuário (via Controlador): o HUD do Sample (S4) usa uGUI e o Input Manager legado quando o módulo de UI existir; o Sample ganha o HUD depois de B-015 e B-016 | R19 | Aplicado |
| 2026-10-31 | Dependências registradas sem puxar escopo: B-003, B-004, B-005, B-008, B-010; B-016 a B-018 consomem a base | Seção 6 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P1: barra simples, sem camada de escudo; escudo = segunda barra. Critério de design: sistema modular, sem remendos | Objetivo 1, C3, R7, R11, CA3, seção 6, cabeçalho (B-010 só para estilo por tag) | Aplicado |
| 2026-10-31 | usuário (via Controlador) P2, P3, P4: propostas aceitas (estilo por tag; prévia só em edição; sem `WorldAnchorFollower`) | Seção 8 (sem mudança de texto nos requisitos) | Aplicado |
| 2026-10-31 | usuário (via Controlador) B-016 P4: a barra de cast entra no B-016 | Seção 6 (barra de cast aponta para o B-016) | Aplicado |
