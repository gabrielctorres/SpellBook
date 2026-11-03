# PRD: Módulo Teams (times e relação entre times)
Backlog: B-011 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção B ("Times"), seção F, seção H.9 e decisão I5. ADRs [0012](../adr/0012-mapa-de-modulos-e-asmdefs.md) (mapa de módulos: `SpellBook.Teams` referencia só Core; condições e filtros de time ficam em `SpellBook.GAS.Bridges`), [0013](../adr/0013-serializereference-como-costura-entre-modulos.md) (costura), [0019](../adr/0019-hierarquia-unica-de-condition.md) (condições; "condição de time vai para Bridges") e [0018](../adr/0018-hierarquia-unica-de-acoes.md) (filtros de alvo).
Depende de: nada para o **módulo** (parte 1). A **parte 2** (condição e filtros em `GAS.Bridges`) depende de **B-009** (a base `Condition` no Core), **B-012** (`TargetSelector` de área) e, para o projétil e o thinker, de **B-014**. Este PRD não reescreve o escopo deles (seção 6).
Este PRD não tem foco em nenhum jogo. As menções a jogos no plano (um `FactionFilter` do jogo) são histórico.

---

## 1. Problema (contexto)

O pacote não sabe quem é aliado de quem:

- **Sem relação de time.** "Cure só os aliados", "dano só nos inimigos" e "o projétil atravessa aliados" exigem código do jogo em cada habilidade: o `CircleTargeting` e o `Projectile` devolvem tudo que tem um ASC, e a regra "quem pode ser alvo" fica num filtro escrito à mão (no plano, o jogo reimplementou como `FactionFilter`).
- **Cada jogo inventa o seu modelo.** Enum de facções, camadas de física, tags: nenhum se reaproveita entre projetos, e nenhum diz "esta relação é aliada, hostil ou neutra" de forma configurável por asset.
- **O alvo padrão do GAS não filtra por relação.** O B-012 (seletor de alvo por ação) precisa de um filtro de time, e o ADR 0012 proíbe o GAS de conhecer Teams. Sem o módulo, o filtro não tem de onde vir.
- **Fogo amigo e neutros** (cura em aliado, dano em hostil, armadilha neutra que acerta todos) não têm lugar para serem decididos.

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, **sem script** no caso comum:

1. definir **times** como assets (`TeamDefinition`) e a **relação** entre cada par de times numa matriz (`TeamRelationMatrix`: aliado, hostil ou neutro);
2. dar a um objeto de jogo um time com o componente **`TeamMember`**, trocá-lo por código em runtime e ser avisado da troca;
3. perguntar por código "qual a relação de A com B?" e "B é inimigo de A?", sem alocação;
4. usar o módulo **sozinho**, sem o GAS: ele não conhece GAS, Events, Cues nem UI;
5. (parte 2) usar a relação de time **dentro do GAS**, pelo Inspector: uma condição "relação com o alvo" e um filtro de relação nos seletores de alvo de área, só com o módulo `GAS.Bridges` no projeto.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Designer técnico** | Criar times e a matriz de relação no Inspector; marcar numa habilidade "só inimigos" sem script |
| **Desenvolvedor de gameplay** | `TeamMember` num prefab; `GetRelation(a, b)` por código; trocar o time em runtime (controle mental, mudança de lado) |
| **Desenvolvedor de IA ou de UI** | Perguntar "é inimigo?" sem depender do GAS (ex.: cor da barra de vida por relação) |
| **Mantenedor do pacote** | Um módulo de infraestrutura mínimo (só Core), que o GAS não referencia; a integração é só no Bridges |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, relação por par:** times `Azul`, `Vermelho`, `Neutros`. A matriz diz `Azul→Vermelho = Hostil`, `Azul→Azul = Aliado`, `Azul→Neutros = Neutro`. `GetRelation(azul, vermelho)` devolve `Hostil`; `GetRelation(azul, azul)` devolve `Aliado`.
- **C2, membro e relação:** dois objetos com `TeamMember` (Azul e Vermelho). `IsHostile(a, b)` é verdadeiro; `IsAllied(a, a2)` com dois do Azul é verdadeiro; consultar um objeto com ele mesmo devolve a relação do time dele consigo mesmo (`Allied` por padrão); o `Self` só existe na máscara do filtro (P4).
- **C3, sem time:** um objeto sem `TeamMember`, ou com `TeamMember` sem time atribuído, é tratado como **sem time**: a relação com qualquer um é a **padrão para "sem time"** (P2) e a consulta não lança exceção.
- **C4, par ausente na matriz:** dois times sem célula na matriz devolvem a **relação padrão** da matriz (P2), não exceção.
- **C5, assimetria:** com `Azul→Vermelho = Hostil` e `Vermelho→Azul = Neutro`, a consulta é **por direção** (de quem pergunta para quem é perguntado) (P1).
- **C6, troca em runtime:** `SetTeam(novoTime)` muda o time, dispara `OnTeamChanged(membro, antes, depois)` **uma vez**, e as consultas seguintes usam o time novo. Trocar para o mesmo time não dispara.
- **C7, módulo sozinho:** um projeto só com `SpellBook.Teams` e `SpellBook.Core` compila e roda os cenários C1 a C6 (teste de assemblies).
- **C8, validação:** a matriz com um time repetido, um time nulo, ou sem a célula padrão aparece no validador do projeto, com o objeto clicável.
- **C9, condição no GAS (parte 2, S):** uma habilidade com a condição "relação com o alvo = hostil" ativa contra um inimigo e falha, com o motivo, contra um aliado.
- **C10, filtro de área (parte 2, T):** um seletor de área com filtro "só hostis" devolve só os inimigos de quem lança; com "aliados e quem lançou", os aliados e ele mesmo (P3).
- **C11, sem lixo:** 10 000 consultas de relação e 10 000 avaliações da condição não alocam memória gerenciada depois do aquecimento.

## 4. Requisitos funcionais

### Parte 1: o módulo `SpellBook.Teams` (só Core)

#### 4.1 Times e matriz

R1. **`TeamDefinition`.** Um `ScriptableObject` que representa um time: nome de exibição, cor (para UI e Gizmos) e um **identificador estável** (um valor serializado que não muda ao renomear, para a matriz e os assets que a citam continuarem válidos). Menu `Create > SpellBook > Teams > Team`. Implementa `IValidatable` (nome vazio, identificador duplicado entre times da mesma matriz).

R2. **`TeamRelation`.** Um enum fechado com **`Allied`**, **`Hostile`** e **`Neutral`**. O "mesmo objeto" é a relação **`Self`** só no filtro (R10), não um valor da matriz (P4).

R3. **`TeamRelationMatrix`.** Um `ScriptableObject` com a lista de times que ele conhece e a relação de **cada par ordenado** (de → para) (P1). Ele tem: a **relação padrão para o par ausente**, a **relação padrão do time consigo mesmo** (`Allied`) e a **relação de "sem time"** (P2). O Inspector mostra a matriz como uma **tabela de linhas e colunas** (UI Toolkit, ADR 0026) com um **botão "Espelhar"** (copia a relação de A→B para B→A).

R4. **Consulta pela matriz.** O módulo oferece `matrix.GetRelation(TeamDefinition de, TeamDefinition para)`, que devolve a relação **sem alocar** e sem exceção para time nulo ou ausente (aplica os padrões do R3). A matriz é um **asset de leitura**: o módulo não a altera em runtime (P5).

R5. **Validação da matriz** (`IValidatable`): time nulo na lista; mesmo time duas vezes; relação padrão fora do enum; times citados por outro asset e fora da matriz (aviso). Mensagens clicáveis.

#### 4.2 Membros

R6. **`TeamMember`.** Um `MonoBehaviour` com o time inicial (`TeamDefinition`) e a matriz que vale para ele (`TeamRelationMatrix`; campo por membro, para projetos com mais de um conjunto de times, P6). Expõe `Team`, `Matrix`, `SetTeam(TeamDefinition)` e o evento C# `OnTeamChanged(membro, antes, depois)`, que dispara **só em mudança real** (C6). Implementa a interface **`ITeamMember`** (R7). Não usa `Find` nem singleton.

R7. **`ITeamMember`.** Interface mínima (`Team`, `Matrix`) para que **código do projeto** (um componente próprio, uma entidade que não é `MonoBehaviour`) participe das consultas sem herdar do `TeamMember`. O módulo oferece as consultas estáticas **sobre a interface**.

R8. **Consultas por membro.** Métodos estáticos (e de extensão) sobre `ITeamMember`: `GetRelation(a, b)`, `IsAllied(a, b)`, `IsHostile(a, b)`, `IsNeutral(a, b)`. A direção é de **a para b** (C5). Membro nulo conta como "sem time" (R3). Sem alocação.

R9. **Troca segura.** `SetTeam` com o mesmo time não faz nada. Um ouvinte do `OnTeamChanged` que troca o time de novo não corrompe a iteração. O evento é **C# no componente**, sem barramento (o módulo só referencia o Core).

#### 4.3 Relação como máscara

R10. **`TeamRelationMask`.** Um enum de **flags** `Self | Allied | Hostile | Neutral`, para o Inspector ("aceita estas relações") e para o código, com o método `Matches(relationOf a to b)` que diz se a relação (incluindo `Self` quando `a` é `b`) está na máscara. A máscara vazia não casa com ninguém (e o validador avisa). É o único tipo que as partes 1 e 2 compartilham para filtrar.

### Parte 2: integração com o GAS (em `SpellBook.GAS.Bridges`)

R11. **Referência de módulo.** O `SpellBook.GAS.Bridges` passa a referenciar `SpellBook.Teams` (ADR 0012, a lista das referências cresce "quando o primeiro tipo que usa entrar no Bridges"). **O GAS e o Teams não se referenciam** (ADR 0012).

R12. **Time do ASC.** O Bridges oferece a leitura "o time deste ASC": o `TeamMember` que está no **mesmo objeto** do ASC ou num **pai** dele (a mesma busca `GetComponentInParent` que o targeting usa), **guardada em cache** por consulta; sem membro, é "sem time". Como o ASC é guardado no cache e a procura é cara, a Spec define onde o cache mora (P7).

R13. **`TeamRelationCondition`.** Uma condição da hierarquia do B-009 (ADR 0019), em `Bridges`, que testa a relação entre **quem lança** e **o alvo** (ponto de vista do ASC de origem) contra uma `TeamRelationMask`. Informa o motivo de falha como **chave de texto** (ADR 0019, 0023), nunca texto pronto. Pura, sem efeito colateral e sem alocar. Tem `[HelpURL]` e página do manual.

R14. **Filtro de relação nos seletores de alvo.** O seletor de área do B-012 (R10 do PRD do B-012: "a relação de time e a facção são filtros de `SpellBook.GAS.Bridges` (B-011)") ganha **um filtro de relação** (`TeamRelationMask`: quem lança é a referência; `Self` inclui o próprio ASC). O B-012 define o **gancho** (a lista de filtros do seletor); este item entrega **o filtro de time** que se encaixa nele. Sem este módulo, o gancho fica vazio e o seletor funciona sem filtro de time.

R15. **Filtro no impacto de projétil e na aura.** O mesmo filtro (R14) serve de **condição de impacto** do projétil e de **filtro da aura**: os pontos de extensão são do **B-014**, que usa `TeamRelationCondition` (R13) e o filtro (R14) sem reescrevê-los. O B-011 só garante que eles aceitem um alvo e uma origem **sem depender de `SpellBook.GAS.UI`**.

R16. **Sem time significa neutro (padrão).** Um alvo sem time é avaliado pela relação "sem time" da matriz (P2). Se a origem **não** tem time, a condição e o filtro tratam a origem como "sem time" e usam o mesmo padrão. Não há exceção nem aviso em runtime por falta de time (o validador avisa em edição, R17).

### 4.4 Editor, validação e documentação

R17. **Validação** (`IValidatable`, ADR 0026): `TeamMember` sem matriz (aviso) ou sem time (aviso); matriz vazia; `TeamRelationCondition` ou filtro com máscara vazia (aviso: não casa com ninguém); filtro de time num seletor cujo alvo nunca tem time (aviso, quando a Spec conseguir detectar). Mensagens clicáveis, em português.

R18. **Inspector** (UI Toolkit, ADR 0026, herdando de `SpellBookEditor`): `TeamDefinition` com a cor; `TeamRelationMatrix` com a **tabela de relações** e "Espelhar"; `TeamMember` com o time e a matriz; a máscara com um **seletor de quatro caixas** (`Self`, `Aliado`, `Hostil`, `Neutro`) em vez do campo de flags cru.

R19. **Documentação.** Uma página **Times** descreve o módulo, a matriz, o `TeamMember`, a troca em runtime e a máscara; uma seção "Times no GAS" (parte 2) mostra a condição e o filtro com um exemplo "só inimigos" e "só aliados". Os tipos novos têm `[HelpURL]` (decisão I15). A página mostra como criar um **componente próprio** via `ITeamMember`.

R20. **CHANGELOG.** Cita: o módulo `SpellBook.Teams` (`TeamDefinition`, `TeamRelationMatrix`, `TeamMember`, `ITeamMember`, `TeamRelationMask`) e, na parte 2, `TeamRelationCondition` e o filtro de relação no `GAS.Bridges`.

## 5. Requisitos não funcionais

N1. **Alocação:** `GetRelation`, `IsHostile` e a avaliação da condição e do filtro não alocam depois do aquecimento (C11). Disparar `OnTeamChanged` sem ouvinte não aloca.
N2. **Módulos (ADR 0012):** `SpellBook.Teams` referencia **só o Core**; não referencia GAS, Tags, Time, Events, Cues nem UI. O GAS não referencia Teams. Condição e filtro ficam no `GAS.Bridges`. O teste de assemblies da 0-M cobre.
N3. **Sem estado global:** nenhum singleton, `Find` ou `FindObjectOfType`; a matriz chega por campo serializado (ADR 0013, 0015).
N4. **Serialização:** nativa; o identificador do time é estável ao renomear o asset; a matriz referencia times por asset **e** pelo identificador (a Spec decide, P8).
N5. **Determinismo:** os testes usam factories (`GasFactory` e uma factory de times, ADR 0004), sem assets fixos.
N6. **Asmdefs (ADR 0012, emenda 0026):** `SpellBook.Teams` (runtime), `SpellBook.Teams.Editor` (Core, Teams, `Core.Editor`) e os asmdefs de teste do módulo seguem o mapa; a Spec cria os asmdefs, **não** este PRD.

## 6. Fora do escopo

- **`GameplayTagQuery` e tags de facção:** o filtro por tag é do B-006 e do seletor do B-012. Time é **outro eixo** (relação), não substitui tag.
- **Base `Condition` e `TargetSelector`:** B-009 e B-012. Aqui só entram a condição e o filtro **de time** que se encaixam neles (parte 2).
- **Projétil com filtro de time e aura:** **B-014** entrega os pontos de extensão e usa o filtro deste item.
- **Relações dinâmicas** (diplomacia: mudar a relação entre dois times em runtime, tratados de paz): fora (P5). O jogo troca o **time do membro**.
- **Facção, reputação e níveis de amizade** (numéricos): fora; a relação é um enum de três valores.
- **Camadas de física** (Layer Collision Matrix): continuam sendo da Unity; o módulo não gera nem altera camadas.
- **UI de times** (cor de barra por relação): módulo de UI (B-015 e seguintes); aqui só a consulta.
- **Rede, replicação e save/load** do time do membro.
- **IA de unidades** (quem atacar): do jogo.

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

**Módulo (parte 1)**
- CA1 (T). Cenário C1: `GetRelation` devolve `Hostile`, `Allied` e `Neutral` conforme a matriz; o time consigo mesmo é `Allied` por padrão.
- CA2 (T). Cenário C2: `IsAllied`, `IsHostile` e `IsNeutral` entre dois `TeamMember`.
- CA3 (T). Cenário C3: objeto sem `TeamMember`, ou com `TeamMember` sem time, usa a relação de "sem time"; nenhuma exceção.
- CA4 (T). Cenário C4: par ausente na matriz devolve a relação padrão da matriz.
- CA5 (T). Cenário C5: a consulta tem direção (A→B diferente de B→A quando a matriz é assimétrica).
- CA6 (T). Cenário C6: `SetTeam` dispara `OnTeamChanged` uma vez com antes e depois; o mesmo time não dispara; um ouvinte que troca o time de novo não corrompe a iteração.
- CA7 (T). `TeamRelationMask`: cada combinação de `Self`, `Allied`, `Hostile`, `Neutral` casa com a relação esperada; `Self` só casa quando `a` é `b`; a máscara vazia não casa com nada.
- CA8 (T). Um componente próprio que implementa `ITeamMember` participa das consultas (cenário de teste com um tipo de teste, sem `MonoBehaviour` do pacote).
- CA9 (T). Cenário C8: o validador acusa time nulo e repetido na matriz, `TeamMember` sem time ou sem matriz, e máscara vazia, com o objeto clicável.
- CA10 (T). Cenário C7: o módulo compila e passa os testes sem referenciar GAS, Tags, Time, Events, Cues nem UI (teste de assemblies).

**Integração (parte 2)**
- CA11 (T). O `TeamMember` do mesmo objeto e o de um pai do ASC são encontrados; ASC sem membro vira "sem time" (R12).
- CA12 (T). Cenário C9 em teste: `TeamRelationCondition` passa e falha pelo valor da máscara, devolvendo o motivo como chave de texto (ADR 0019).
- CA13 (T). Cenário C10: o filtro de relação no seletor de área devolve só os hostis, ou os aliados mais quem lança com `Self`.
- CA14 (T). Origem sem time e alvo sem time usam a relação de "sem time" sem exceção (R16).
- CA15 (T). O teste de assemblies da 0-M passa: o GAS não referencia Teams; o Teams não referencia o GAS; o `Bridges` referencia Teams.

**Desempenho, editor e documentação**
- CA16 (T). Cenário C11: 10 000 consultas de relação e 10 000 avaliações da condição alocam 0 bytes gerenciados depois do aquecimento.
- CA17 (S). No host, a matriz mostra a tabela de relações com "Espelhar"; a máscara aparece como quatro caixas; o `TeamMember` mostra o time e a matriz.
- CA18 (E). O CHANGELOG cita os itens do R20; a página **Times** existe e mostra o exemplo "só inimigos"; os tipos novos têm `[HelpURL]`.
- CA19 (E). Nenhum tipo do módulo usa singleton, `Find` ou `FindObjectOfType`.

## 8. Perguntas em aberto

### Com o usuário

- **P1. Relação assimétrica.** *Respondida (usuário, 2026-10-31): aceita. A matriz é por **par ordenado** (de → para); o botão "Espelhar" faz o caso simétrico em um clique.*
- **P2. Padrões.** *Respondida (usuário, 2026-10-31): aceita. O mesmo time consigo mesmo é `Allied`; par sem célula é `Neutral`; **objeto sem time** é `Neutral` com todos; o designer pode **mudar** os três padrões na matriz.*
- **P3. `Self` no filtro.** *Respondida (usuário, 2026-10-31): aceita. `Self` é caixa **própria** na máscara, separada de `Allied`.*
- **P4. Relação com o próprio objeto na consulta.** *Respondida (usuário, 2026-10-31): confirmada. `GetRelation(a, a)` devolve a relação do time de `a` consigo mesmo (`Allied` por padrão); o `Self` existe **só na máscara** do filtro (R10).*
- **P5. Relação dinâmica.** *Respondida (usuário, 2026-10-31): confirmada. A matriz é um **asset de leitura**; o jogo muda a relação só trocando o time do membro (`SetTeam`). Diplomacia em runtime fica fora.*
- **P6. Uma matriz por membro.** *Respondida (usuário, 2026-10-31): aceita. Cada `TeamMember` aponta a sua matriz por campo.*

### Com o Arquiteto (decisão técnica da Spec)

- **P7. Onde mora o cache do time do ASC** (R12): um componente auxiliar no `Bridges`, o próprio `TeamMember` ou um `RuntimeSet`; e como evitar a procura no pai a cada avaliação.
- **P8. Identificador estável do time** (R1, N4): `[SerializeField] string/Guid` próprio ou o GUID do asset; e como a matriz referencia os times (por asset, por identificador, ou os dois).
- **P9. `ITeamMember` e entidades sem `MonoBehaviour`** (R7): o menor conjunto de membros da interface, e se a interface exige `GameObject`.
- **P10. Condição de time e o contexto genérico** (B-009, P5 do PRD do B-009): como `TeamRelationCondition` recebe origem e alvo no contexto de ativação e no de execução de ação, sem o Core conhecer Teams.
- **P11. Gancho de filtros do seletor de área** (B-012, R10): quem define a lista de filtros e a assinatura, para o filtro de time (R14) e o filtro por tag poderem coexistir, e como o B-014 (projétil e aura) usa a mesma lista.
- **P12. Ordem e divisão em tickets:** a parte 1 não depende de nenhum item aberto e pode ir **antes** do B-012; a parte 2 só depois de B-009 e B-012. A Spec decide se vira dois lotes.

## 9. Ajustes de playtest

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | PRD escrito pelo Criativo a partir do B-011, do plano (itens "Times" e seção H.9) e das ADRs 0012, 0013, 0018 e 0019; o Controlador pediu o PRD inteiro e a Spec divide em tickets | Todo o documento | Rascunho |
| 2026-10-31 | Dependências registradas sem puxar escopo: o módulo (parte 1) não depende de nada; a parte 2 depende de B-009 (`Condition`) e B-012 (`TargetSelector`); B-014 usa o filtro | Seção 6, R14, R15 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P1 a P6: todas as propostas aceitas (matriz assimétrica com "Espelhar"; padrões do R3; `Self` separado; `GetRelation(a, a)` é a relação do time consigo mesmo; matriz de leitura; matriz por membro) | R2, R3, R6, R10 (sem mudança de texto: já descreviam isso) | Aplicado |
