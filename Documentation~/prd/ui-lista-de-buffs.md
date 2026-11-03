# PRD: UI-4, lista de buffs e debuffs
Backlog: B-018 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção H.5 (lista de buffs e ícone de efeito, `ActiveEffectsSource`), seção B (metadados de modifier para UI) e seção E (UI-4). ADRs [0017](../adr/0017-ui-em-tres-camadas.md) (UI em três camadas), [0015](../adr/0015-spawnerasset-como-seam-de-spawn-e-pool.md) (lista em pool), [0014](../adr/0014-tempo-injetavel-itimesource.md) (tempo), [0023](../adr/0023-texto-estruturado-e-itextprovider.md) (texto), [0012](../adr/0012-mapa-de-modulos-e-asmdefs.md) (módulos) e [0026](../adr/0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md) (editor).
Depende de: **B-015** (a base: `IView<T>`, `UISource<T>`, binder, `ActorResolver`, `UIUpdateDriver`, tema, widgets uGUI), **B-003** (lista pública de efeitos ativos, `OnEffectApplied`, `OnEffectRemoved`, `OnEffectStacksChanged`, instância com tempo restante e pilhas) e **B-005** (`IsBuff`, `IsDebuff`, `IsHidden`, `IsPurgable`, categoria, `TextRoleTheme` **e `DisplayName` e `Icon` do `GameplayEffect`, ticket AR7**: decisão do usuário, 2026-10-31). **B-017** (tooltip) entra para o tooltip do ícone; **B-008** para o nome por chave. Este PRD usa o que eles entregam e não reescreve o escopo deles (seção 6).
**Sample:** o HUD do Sample (S4) pode ganhar a lista de buffs depois, em uGUI e com o Input Manager legado (decisão do usuário, 2026-10-31); este item não altera o Sample.
Este PRD não tem foco em nenhum jogo. O `StatusEffects` do jogo no plano (registro paralelo para achar e remover por tag) é histórico.

---

## 1. Problema (contexto)

Mostrar "o que está agindo sobre mim" é parte de toda HUD de RPG e MOBA, e hoje:

- **Não há como saber o que está ativo.** Antes do B-003 o ASC guardava os efeitos em campo privado; o jogo lia por reflexão e mantinha um registro paralelo. O B-003 abre a lista; falta quem a **transforme em ícones**.
- **O `GameplayEffect` não tem nome nem ícone hoje.** Só o atributo e a habilidade têm `Icon` e `DisplayName`. O B-005 (AR7) entrega esses campos no efeito; falta quem os **mostre** para dizer ao jogador "Queimadura" com uma chama.
- **Sem separar buff de debuff.** Os metadados `IsBuff`, `IsDebuff` e `IsHidden` (B-005) são só dados: nada os usa para mostrar, agrupar ou esconder.
- **Duração e pilhas na tela são código do jogo.** O relógio do ícone, o número de pilhas e o piscar perto do fim são reescritos por projeto, com `Update` por ícone.
- **Ícones com `Instantiate` e `Destroy`.** A lista cresce e encolhe a cada efeito aplicado; sem pool, acumula lixo (ADR 0015).
- **Sem prévia.** O designer só vê a lista com efeitos no Play.

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, **pelo Inspector e sem script** no caso comum:

1. ver o **nome** e o **ícone** que o `GameplayEffect` já traz (B-005, AR7) na lista, sem script;
2. ter uma **lista de efeitos ativos** de um ASC com **um ícone por efeito** (o mesmo efeito de várias fontes vira um ícone só), com **duração restante, pilhas** e papel (buff ou debuff), atualizada por **evento**, com tick só enquanto há efeito com duração;
3. **filtrar** a lista por buff ou debuff, por categoria, por `GameplayTagQuery` e por `IsHidden` (efeito oculto não aparece), na **ordem de aplicação**;
4. ter a lista em **pool**: o ícone é criado e devolvido pelo `SpawnerAsset`;
5. **abrir o tooltip** do efeito ao passar o mouse (B-017), sem script;
6. **ver a lista em edição** (prévia com buffs e debuffs de exemplo), sem Play;
7. usar uma **view própria** (`IView<EffectIconModel>`) sem tocar na fonte.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Designer técnico / de UI** | Pôr a lista sobre a barra de vida, escolher "só debuffs", ver a prévia, ligar o tooltip |
| **Desenvolvedor de gameplay** | Marcar um efeito como buff, dar ícone e nome (campos do B-005); fonte e view próprias por subclasse |
| **Jogador** | Ver o que o afeta, quanto tempo falta e quantas pilhas tem |
| **Mantenedor do pacote** | Uma lista que lê o ASC por eventos do B-003, sem registro paralelo e sem conhecer a view |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, aplicar e remover:** aplicar `Queimadura` (debuff, 5 s) acrescenta um ícone; ao fim dos 5 s o ícone some e volta ao pool. Remover por código, por tag ou por pilhas zeradas também remove o ícone (B-003), **quando era a última instância do efeito** (C8).
- **C2, duração:** o ícone mostra a fração restante (radial) e os segundos; o tempo vem do `ITimeSource` (pausa pausa o relógio); um efeito **infinito** não mostra relógio.
- **C3, pilhas:** o ícone mostra o número de pilhas (a **soma** das pilhas do efeito, C8) e atualiza por `OnEffectStacksChanged`; com 1 pilha não mostra o número (padrão configurável).
- **C4, buff e debuff:** `IsBuff` e `IsDebuff` (B-005) escolhem o **papel** do ícone (`Beneficial`, `Harmful`, `Neutral`); a view usa o papel e o `TextRoleTheme` para a moldura. Efeito sem marca é neutro.
- **C5, filtros:** a lista "só debuffs" não mostra buffs; "só categoria Veneno" só mostra os dessa categoria; uma `GameplayTagQuery` (B-006) filtra por tag do efeito; efeitos `IsHidden` **nunca** aparecem; um efeito que **deixa de casar** o filtro (tag muda) sai da lista.
- **C6, ordem de aplicação:** a lista segue a **ordem de aplicação** (mais antigo primeiro). Efeito novo entra no fim; nada se reordena (P1 respondida: sem outras ordens). A posição de um ícone é a de quando o efeito **apareceu** na lista e **não muda** enquanto o ícone existe, mesmo que a instância mais antiga saia.
- **C7, limite visível:** com `MaxVisible = 8` e 11 efeitos (ícones), a lista mostra 8 ícones e um indicador "+3" (P2); ao remover um, o mais antigo oculto entra. O limite conta **ícones**, não instâncias.
- **C8, mesmo efeito, várias fontes, um ícone (P3):** o mesmo `GameplayEffect` aplicado por duas fontes (ou duas vezes sem `CanStack`) aparece como **um ícone só**. O ícone mostra: **pilhas = a soma** das pilhas de todas as instâncias do efeito (instância sem `CanStack` conta 1); **duração = a maior restante** entre as instâncias (o efeito só some do jogador quando a última acaba; P6); se **qualquer** instância é infinita, o ícone é infinito (sem relógio). Uma instância que acaba **atualiza** pilhas e duração; o ícone só some quando a última sai. Efeitos de assets diferentes nunca se fundem, mesmo com o mesmo nome. Uma regra só, valendo igual para efeito com e sem `CanStack`.
- **C9, aviso de fim:** o ícone de um efeito perto de acabar (a duração mostrada, C8, nos últimos N segundos configuráveis) sinaliza no modelo (`EndingSoon`); a view decide como (piscar).
- **C10, tooltip:** passar o mouse num ícone abre o tooltip do efeito (B-017, `EffectTooltipSource`) com título, linhas de modificador, pilhas e tempo restante; sem `TooltipBinder` na cena, o ícone funciona sem tooltip.
- **C11, concessão antes da lista:** a lista que abre com o ASC **já com** efeitos ativos mostra todos de uma vez (não só os aplicados depois).
- **C12, dono e respawn:** a lista encontra o ASC por `ActorResolver` (âncora, B-015); se o ator renasce, a lista se refaz para o ASC novo e devolve os ícones do antigo ao pool.
- **C13, prévia (S):** no host, a lista em edição mostra três ícones de exemplo (um buff com pilhas, um debuff com relógio, um permanente), sem Play.
- **C14, ícones em pool:** os ícones saem do `SpawnerAsset` e voltam; depois de aplicar e remover 100 efeitos, a contagem de vivos não cresce.
- **C15, sem lixo:** 10 000 atualizações de tick com 8 ícones e 1000 aplicações e remoções alocam 0 bytes gerenciados depois do aquecimento.
- **C16, Inspector (S):** no host, o binder da lista mostra o resumo e a validação no topo.

## 4. Requisitos funcionais

### 4.1 Nome e ícone do efeito (vêm do B-005)

R1. **Leitura de `DisplayName` e `Icon`.** O nome exibido (texto padrão e chave, ADR 0023) e o `Sprite` do `GameplayEffect` são **entregues pelo B-005 (AR7)** e lidos aqui por propriedade pública somente leitura. **Este item não acrescenta nenhum campo ao GAS.** Efeito sem ícone cai no ícone padrão da lista (R9); sem nome, o modelo traz o nome vazio e a view decide. O B-017 (R4) e o B-008 usam os mesmos campos.

### 4.2 Modelo e fonte (`SpellBook.UI` e `SpellBook.GAS.UI`)

R2. **`EffectIconModel`** (struct, em `SpellBook.UI`): ícone, nome (por chave e texto padrão), papel (`Beneficial`, `Harmful`, `Neutral`), **pilhas** (soma das instâncias do efeito, C8, e "mostrar pilhas?"), **fração restante** (0 a 1) e **segundos restantes** (a maior restante, C8), marca **infinito**, marca **`EndingSoon`**, e um identificador do **efeito** (o grupo de instâncias) para a view e o tooltip (sem referência ao GAS). A view lê o modelo e nada mais.

R3. **`ActiveEffectsSource`.** Em `SpellBook.GAS.UI`: lê a **lista de efeitos ativos** do ASC dono (B-003) e produz a lista de `EffectIconModel`, na ordem de aplicação (R5), com um ícone por efeito (R7) e os **filtros** do R4. Atualiza por **push** (`OnEffectApplied`, `OnEffectRemoved`, `OnEffectStacksChanged`, mudança de tag) e pede tick ao `UIUpdateDriver` **só enquanto há efeito com duração** na lista (ADR 0017, item 1). Ao abrir, **lê o que já está ativo** (C11). Não altera o ASC.

R4. **Filtros.** Por **papel** (`Buffs`, `Debuffs`, `Ambos`), por **categoria** (B-005), por **`GameplayTagQuery`** (B-006) sobre as tags do efeito, e por **`IsHidden`** (efeito oculto nunca aparece; sem opção de ligar, P4). O filtro é configuração do asset `ActiveEffectsFilter` ou campos da fonte, **sem estado**.

R5. **Ordem.** **Só a ordem de aplicação** (P1 respondida): o efeito novo entra no fim e nenhum ícone muda de lugar por causa de tempo, pilhas ou valor. Não há opção de ordenação nem campo de ordem no efeito. Se o jogo quiser outra ordem, escreve uma `UISource` própria (R8 do B-015).

R6. **Limite visível.** `MaxVisible` (0 = sem limite). Acima dele, a lista entrega um indicador "+N" (contagem dos ocultos) no modelo da lista, e a view o desenha (P2).

R7. **Um ícone por efeito (P3 respondida).** As instâncias ativas do **mesmo `GameplayEffect`** (mesmo asset), de qualquer fonte, viram **um ícone** com a regra do C8: **pilhas = soma** (instância sem `CanStack` conta 1), **duração = a maior restante**, **infinito** se qualquer instância é infinita, posição fixa desde que o efeito apareceu. A regra é **uma só**, para efeito com e sem `CanStack`; o B-003 continua agregando as pilhas dentro de uma instância e a fonte soma entre instâncias. O ícone só some quando a última instância sai. A lista **não** mostra quem aplicou cada instância (fora, seção 6).

### 4.3 Binder e lista (`SpellBook.UI` e `SpellBook.UI.UGUI`)

R8. **`EffectListBinder`.** Um componente (B-015: binder + resolvedor + view) que liga a `ActiveEffectsSource`, escuta o dono, cria **um ícone por modelo** pelo `SpawnerAsset` (ADR 0015) e o devolve ao remover; reutiliza o ícone quando só o conteúdo muda (sem recriar). Nunca `Instantiate`, `Destroy` nem corrotina. Spawner sem instância pula o ícone com aviso em desenvolvimento, sem exceção (continuidade do RP-07).

R9. **View de ícone e de lista (uGUI + TextMeshPro).** O pacote entrega uma view de **ícone** (imagem, moldura por papel, radial de duração, texto de segundos, número de pilhas, sinalização de fim) e uma de **lista** (layout horizontal por padrão, com o "+N"), com **prefabs**. A cor da moldura vem do papel e do `TextRoleTheme` (B-005) pelo `UITheme` (B-015). São de protótipo e Sample (ADR 0017, consequências).

R10. **Tooltip do ícone.** O prefab do ícone traz um `TooltipTrigger` (B-017) apontando para uma `EffectTooltipSource` do **efeito** do ícone no ASC dono (com as mesmas pilhas e duração que o ícone mostra, R7); sem `TooltipBinder` alcançável, o trigger não faz nada (aviso de validação). Este item **não** reescreve o tooltip.

R11. **Tempo.** O relógio do ícone usa o `ITimeSource` do driver (ADR 0014); em pausa, o relógio pausa. Nenhum `Time.time`.

### 4.4 Prévia em edição

R12. **Prévia.** A fonte tem um `PreviewModel` (lista de exemplo: buff com pilhas, debuff com relógio, efeito permanente), mostrada **fora do Play** (H.7 do plano e regra da base do B-015); no Play, a prévia desliga.

### 4.5 Editor, validação e documentação

R13. **Inspector** (UI Toolkit, ADR 0026, `SpellBookEditor`): o binder mostra a fonte (seletor de tipo), os filtros, `MaxVisible`, o resolvedor e a view; resumo de uma linha ("Lista de efeitos · só debuffs · até 8"). O Inspector do `GameplayEffect` (nome e ícone) é do B-005.

R14. **Validação** (`IValidatable`): binder sem fonte, sem view ou sem prefab de ícone; `MaxVisible` negativo; filtro com tag inválida; filtro por papel e por `IsBuff` contraditório (aviso); efeito **visível** (não `IsHidden`) **sem ícone** (aviso: o ícone cai no padrão da lista); efeito `IsBuff` e `IsDebuff` juntos (aviso do B-005, repetido); prefab de ícone sem a view. Mensagens clicáveis.

R15. **Documentação.** Página **Lista de buffs e debuffs** (fonte, filtros, ordem de aplicação, regra do ícone único, limite, view, prévia, tooltip, como criar uma view e uma fonte próprias). Nome e ícone do efeito ficam na página de Efeitos do B-005. Os tipos novos têm `[HelpURL]` (decisão I15). O CHANGELOG cita `EffectIconModel`, `ActiveEffectsSource` e `EffectListBinder`.

### 4.6 Dependência com o Sample

R16. **O Sample pode ganhar a lista depois.** O Sample Sandbox (B-032) **não** é alterado por este item. Quando o HUD (S4) existir (depois de B-015 e B-016, em uGUI e com o Input Manager legado, decisão do usuário, 2026-10-31), a lista de buffs é complemento dele, com PRD ou emenda do Sample.

## 5. Requisitos não funcionais

N1. **Alocação:** atualizar o tick dos ícones, aplicar, remover e fundir instâncias de efeitos não alocam depois do aquecimento (C15). Evento sem ouvinte não aloca.
N2. **Módulos (ADR 0012, 0017):** `EffectIconModel` e contratos em `SpellBook.UI`; `ActiveEffectsSource` em `SpellBook.GAS.UI` (GAS, UI, Tags, Time; **não** Bridges); views e prefabs em `SpellBook.UI.UGUI`. A view não conhece o GAS. O GAS não referencia UI. O teste de assemblies cobre.
N3. **Sem registro paralelo:** a lista lê o ASC pelos eventos e pela lista do B-003; **não** mantém um segundo registro de efeitos (modelo limpo, sem mecanismo paralelo).
N4. **Estado:** a fonte guarda **configuração**; a lista de ícones vivos é estado do binder (ADR 0013). Duas listas do mesmo asset não compartilham estado.
N5. **Tempo:** só `ITimeSource` pelo driver; testes com `ManualTimeSource`.
N6. **Determinismo e serialização:** testes com `GasFactory` e uma view de teste, sem Canvas real nos testes de lógica; a view uGUI tem teste PlayMode no host; serialização nativa. Este item não altera o formato de nenhum asset.
N7. **Asmdefs (ADR 0012, emenda 0026):** a **Spec** cria os asmdefs, **não** este PRD.

## 6. Fora do escopo

- **A base de UI:** **B-015**. **Slot e barra de habilidades:** **B-016**. **Tooltip:** **B-017** (aqui só se liga o trigger).
- **Lista pública de efeitos, eventos de efeito, instância com tempo e pilhas, purge:** **B-003**. **`IsBuff`, `IsDebuff`, `IsHidden`, `IsPurgable`, categoria e `TextRoleTheme`:** **B-005**. Aqui são só lidos.
- **Nome por chave e `ITextProvider`:** **B-008**. **Os campos `DisplayName` e `Icon` do `GameplayEffect`:** **B-005** (AR7); aqui só são lidos (R1). O texto vem do provedor quando o B-008 existir.
- **Ordenar por tempo restante ou por campo de ordem:** fora (P1 respondida).
- **Mostrar de qual fonte veio cada instância** de um ícone fundido, e o tempo de cada uma: fora; o ícone mostra a soma das pilhas e a maior duração (R7). Quem precisar escreve uma fonte e um tooltip próprios.
- **Ação de remover o efeito pelo clique no ícone (purge manual)** e **cancelar efeito pelo jogador:** fora; do jogo (o `IsPurgable` é dado do B-005 e a remoção é do B-003).
- **Mostrar efeitos de outras unidades** (barra de vida do inimigo com seus debuffs): funciona pelo `ActorResolver` apontando outro ASC; a posição sobre a cabeça é do projeto, como no B-015.
- **Agrupar por categoria com cabeçalho, linhas múltiplas e rolagem:** fora; o layout padrão é uma linha com "+N".
- **UI Toolkit em runtime.**
- **Rede e replicação.**

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

- CA1 (T). Cenário C1: aplicar acrescenta o ícone; expirar, remover por código, por tag e por pilhas zeradas o removem e o devolvem ao pool.
- CA2 (T). Cenário C2: o relógio mostra fração e segundos pelo `ManualTimeSource`; em pausa não anda; efeito infinito não mostra relógio.
- CA3 (T). Cenário C3: as pilhas aparecem e atualizam por evento; com 1 pilha o número não aparece por padrão.
- CA4 (T). Cenário C4: o papel do ícone vem de `IsBuff` e `IsDebuff`; sem marca é neutro.
- CA5 (T). Cenário C5: papel, categoria, `GameplayTagQuery` e `IsHidden` filtram; um efeito que deixa de casar o filtro sai da lista.
- CA6 (T). Cenário C6: a ordem é a de aplicação; o efeito novo entra no fim e nenhum ícone muda de lugar por tempo ou pilhas.
- CA7 (T). Cenário C7: o limite visível mostra "+N" e promove o oculto ao liberar lugar.
- CA8 (T). Cenário C8: instâncias do mesmo efeito, de fontes diferentes, com e sem `CanStack`, viram um ícone com a soma das pilhas e a maior duração restante; infinito se qualquer uma é infinita; o ícone atualiza quando uma instância sai e só some com a última; efeitos de assets diferentes não se fundem.
- CA9 (T). Cenário C9: `EndingSoon` liga nos últimos N segundos configurados.
- CA10 (T). Cenário C10: o ícone abre o tooltip do efeito (B-017); sem binder de tooltip, funciona sem ele.
- CA11 (T). Cenário C11: a lista aberta com efeitos já ativos os mostra todos.
- CA12 (T). Cenário C12: o respawn refaz a lista para o ASC novo e devolve os ícones antigos.
- CA13 (S). Cenário C13: a prévia aparece em edição e some no Play.
- CA14 (T). Cenário C14: ícones vêm do `SpawnerAsset` e voltam; a contagem de vivos não cresce depois de 100 aplicações e remoções.
- CA15 (T). Cenário C15: 10 000 ticks com 8 ícones e 1000 aplicações e remoções alocam 0 bytes gerenciados depois do aquecimento.
- CA16 (S). Cenário C16: no host, o Inspector do binder e do efeito mostra o resumo e a validação.
- CA17 (T). O ícone e o nome do modelo vêm do `Icon` e do `DisplayName` do efeito (B-005, lidos pela API pública); efeito sem ícone usa o ícone padrão da lista; este item não acrescenta campo ao GAS.
- CA18 (T). O teste de assemblies da 0-M passa (N2); a lista não mantém registro paralelo de efeitos (N3: o teste confere que o estado vem só do ASC).
- CA19 (T). O validador cobre os casos do R14, com o objeto clicável.
- CA20 (E). Nenhum tipo do módulo usa singleton, `Find`, `FindObjectOfType`, `Instantiate` ou `Destroy` em regime, `Time.time` nem corrotina da Unity (busca de código).
- CA21 (E). O CHANGELOG cita os itens do R15; as páginas existem; os tipos novos têm `[HelpURL]`.
- CA22 (E). O PRD do Sample (B-032) fica sem alteração neste item; a nota do R16 está registrada.

## 8. Perguntas em aberto

### Com o usuário

- **P1. Ordem da lista.** *Respondida (usuário, 2026-10-31): **só a ordem de aplicação**, sem opções de ordenação (diferente da proposta). R5, C6, CA6 ajustados.*
- **P2. Muitos efeitos.** *Respondida (usuário, 2026-10-31): aceita. `MaxVisible` com indicador "+N" (R6, C7).*
- **P3. Mesmo efeito de fontes diferentes.** *Respondida (usuário, 2026-10-31): **um ícone só** para o mesmo efeito de fontes diferentes (diferente da proposta). A regra do que o ícone mostra (pilhas = soma, duração = a maior restante, infinito se alguma é infinita) está em R7 e C8; a duração é a P6.*
- **P4. Efeito oculto.** *Respondida (usuário, 2026-10-31): aceita. `IsHidden` sempre esconde (R4).*
- **P5. Nome e ícone do efeito.** *Respondida (usuário, 2026-10-31): **entram no B-005 (ticket AR7)**, não aqui (diferente da proposta). R1 só lê; o B-017 também consome de lá.*
- **P6. Duração do ícone fundido (consequência da P3).** *Respondida (usuário, 2026-10-31): aceita. O ícone único mostra a **maior** duração restante entre as instâncias e o `EndingSoon` segue essa duração (R7, C8, C9, CA8).*

### Com o Arquiteto (decisão técnica da Spec)

- **P1. Campos do efeito no AR7** (R1): confirmar que a Spec do B-005 (AR7) inclui `DisplayName` (texto padrão e chave sem quebrar o asset, B-008 P2 do Arquiteto) e `Icon` no `GameplayEffect`, com leitura pública, e que a ordem da fila põe AR7 antes dos tickets de B-017 e B-018 que os leem.
- **P2. Eventos que a fonte escuta:** `OnEffectApplied`, `OnEffectRemoved` e `OnEffectStacksChanged` do B-003, e a mudança de tag do efeito para os filtros; o que falta, sem polling.
- **P3. Fusão sem alocar** (R5, R7): estrutura da lista de modelos (lista pré-alocada, agrupamento por efeito sem alocar, ordem de aplicação estável) e como o binder reaproveita ícones.
- **P4. Identificador do efeito** no `EffectIconModel` (R2, R7): como a view e o tooltip o usam sem referenciar o GAS (identificador do asset do efeito mais o dono), já que o ícone agrupa instâncias.
- **P5. `ActiveEffectsFilter` como asset ou campos da fonte** (R4): o que é dado compartilhável entre listas.
- **P6. Tick enquanto há duração** (R3): o `UIUpdateDriver` só roda com ícone de duração; como o binder pede e solta o tick quando o último efeito com duração sai.
- **P7. Ordem entre tickets:** (AR7 do B-005 antes, com `DisplayName` e `Icon`); modelo e fonte com a fusão por efeito; binder, view e pool; filtros; limite "+N"; tooltip do ícone (depende de B-017); prévia no fim.

## 9. Ajustes de playtest

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | PRD escrito pelo Criativo a partir do B-018, do plano (H.5, metadados de modifier, UI-4) e das ADRs 0017, 0015, 0014, 0023, 0012 e 0026; o Controlador pediu o PRD inteiro e a Spec divide em tickets | Todo o documento | Rascunho |
| 2026-10-31 | usuário (via Controlador): o HUD do Sample (S4) usa uGUI e o Input Manager legado; a lista de buffs do Sample é complemento posterior | R16 | Aplicado |
| 2026-10-31 | Achado: o `GameplayEffect` não tem nome nem ícone (só atributo e habilidade têm); o B-018 propôs entregá-los (P5) | R1, seção 6 | Resolvido pela linha abaixo |
| 2026-10-31 | usuário (via Controlador) P5: nome e ícone do `GameplayEffect` entram no B-005 (AR7); o B-018 só os lê. Critério de design: sistema modular, sem remendos | Cabeçalho, problema, objetivo 1, R1, R13, R15, N6, CA17, seção 6, seção 8 (P1 do Arquiteto) | Aplicado |
| 2026-10-31 | usuário (via Controlador) P1: só ordem de aplicação, sem opções de ordenação | Objetivo 3, R5, C6, CA6, seção 6 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P3: um ícone só para o mesmo efeito de fontes diferentes; o Criativo definiu o que o ícone mostra (pilhas = soma, duração = a maior restante, infinito se alguma é infinita) e abriu a duração como P6 | Objetivo 2, C1, C3, C7, C8, C9, R2, R7, R10, CA8, seção 6, seção 8 (P6) | Aplicado (P6 aberta) |
| 2026-10-31 | usuário (via Controlador) P2 e P4: propostas aceitas (`MaxVisible` com "+N"; `IsHidden` sempre esconde) | Seção 8 (sem mudança de texto nos requisitos) | Aplicado |
| 2026-10-31 | usuário (via Controlador) P6: o ícone único mostra a **maior** duração restante; o texto do R7, C8 e C9 já dizia isso | Seção 8 (P6 respondida) | Aplicado |
| 2026-10-31 | Dependências registradas sem puxar escopo: B-015 (base), B-003, B-005, B-008 e B-017; B-016 não é dependência | Seção 6 | Aplicado |
