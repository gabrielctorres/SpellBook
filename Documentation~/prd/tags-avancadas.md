# PRD: Tags avançadas
Backlog: B-006 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção D (lacuna 19) e seção H.2 (módulo `SpellBook.Tags`, "depois `GameplayTagContainer` / `GameplayTagQuery`").
A modularização (0-M, R27) deixou a `GameplayTag` no módulo `SpellBook.Tags`, sem mudança de comportamento, e adiou para este item as mudanças de API. Este PRD não tem foco em nenhum jogo.

---

## 1. Problema (contexto)

Hoje o módulo de tags entrega só duas peças: `GameplayTag` (uma referência a uma definição) e `GameplayTagDefinition` (o asset, com um pai opcional). Quem precisa de mais que "esta tag casa com aquela?" improvisa:

- **Não há um conjunto de tags.** Quem quer guardar "as tags deste personagem" usa `List<GameplayTag>` e escreve de novo, a cada projeto, a busca hierárquica, "tem alguma", "tem todas" e a remoção de duplicados.
- **Não há como expressar uma condição sobre tags.** "Tem `Estado.Atordoado`, tem alguma entre `Elemento.Fogo` e `Elemento.Gelo`, e não tem `Imune`" vira código com vários laços, que o designer não consegue editar no Inspector.
- **A definição de uma tag não é acessível de fora do pacote.** `GameplayTag.GetDefinition()` é interno. Quem constrói ferramentas sobre tags (um painel de debug, uma lista filtrada, um validador próprio) não chega ao asset nem ao pai.

Várias fases do GAS dependem dessas peças (condições de ativação por consulta de tags, filtros de lista de efeitos, requisitos de alvo). Sem elas, cada fase reinventa a consulta.

## 2. Objetivo

Ao fim deste item, um desenvolvedor ou designer consegue:

1. guardar um conjunto de tags num campo serializado e perguntar se ele tem uma tag, alguma ou todas de outras, respeitando a hierarquia;
2. montar uma consulta "todas / alguma / nenhuma" pelo Inspector, sem código, e aplicá-la a um conjunto ou a uma tag;
3. acessar a definição de uma tag (e o pai dela) de código fora do pacote;
4. fazer tudo isso sem depender do GAS (o módulo `SpellBook.Tags` continua só com Core) e sem alocar memória nas consultas.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Desenvolvedor de gameplay** | Tipos pequenos, sem alocação, com a mesma regra de hierarquia de `GameplayTag.Matches`; acesso à definição para ferramentas próprias |
| **Designer técnico** | Editar o conjunto e a consulta no Inspector, ver os erros (tag vazia, duplicada) no topo, abrir a doc pelo ícone de ajuda |
| **Mantenedor do pacote** | Um único lugar com a regra de casamento de tags, que as fases seguintes reutilizam |

**Cenários** (cada um vira teste ou cenário no host):

- **C1, conjunto hierárquico:** um conjunto tem `Elemento.Fogo.Chama`. Perguntar por `Elemento.Fogo` e por `Elemento` responde sim; perguntar por `Elemento.Gelo` responde não.
- **C2, exato:** a mesma pergunta em modo exato (sem hierarquia) responde sim só para `Elemento.Fogo.Chama`.
- **C3, tem alguma / tem todas:** um conjunto com `A` e `B`. "Tem alguma de {B, C}" é sim; "tem todas de {B, C}" é não; "tem todas de {A, B}" é sim; conjunto vazio de perguntas: "tem todas" é sim e "tem alguma" é não.
- **C4, consulta pelo Inspector:** o designer cria uma consulta com *todas* = {Atordoado}, *alguma* = {Fogo, Gelo}, *nenhuma* = {Imune}. Um conjunto {Atordoado, Gelo} casa; {Atordoado, Gelo, Imune} não casa; {Atordoado} não casa (falta a "alguma").
- **C5, consulta vazia:** uma consulta sem nenhuma condição casa com qualquer conjunto (inclusive vazio), e o validador avisa que ela está vazia.
- **C6, ferramenta própria:** um desenvolvedor lista no seu painel o nome e a cadeia de pais de cada tag de um conjunto, usando só API pública.
- **C7, sem lixo:** consultar um conjunto 10 000 vezes (tem, tem alguma, tem todas, consulta) não aloca memória gerenciada.

## 4. Requisitos funcionais

### 4.1 Regra de casamento

R1. **Uma só regra.** "A tag X casa com a tag Y" quer dizer: X é igual a Y ou X descende de Y na hierarquia de pais (a regra de `GameplayTag.Matches` de hoje, sem mudança). Todo método novo deste item usa essa regra, salvo os que dizem "exato".

R2. **Tag inválida.** Uma `GameplayTag` sem definição (campo vazio) nunca casa com nada, nem com outra tag vazia, em nenhum método novo. Ela não lança exceção.

### 4.2 `GameplayTagContainer`

R3. `GameplayTagContainer` é um tipo serializável pela Unity (aparece e é editável no Inspector, em qualquer componente ou asset) que guarda um conjunto de tags **sem repetição**.

R4. Operações de consulta, todas sem alocação e sem exceção para argumentos vazios:
- `HasTag(tag)`: alguma tag do conjunto casa com `tag` (R1).
- `HasTagExact(tag)`: o conjunto contém exatamente `tag`, sem hierarquia.
- `HasAny(outroConjunto)`: o conjunto tem alguma tag que case com alguma tag de `outroConjunto`; vazio dá falso.
- `HasAll(outroConjunto)`: para cada tag de `outroConjunto`, o conjunto tem uma que case; vazio dá verdadeiro.
- `IsEmpty` e `Count`.

R5. Operações de edição: adicionar uma tag (devolve se de fato entrou; repetida e inválida não entram), remover uma tag (devolve se existia) e limpar. As operações de edição são exatas (não removem filhos nem pais).

R6. **Enumeração sem alocação.** O conjunto pode ser percorrido por `foreach` sem alocar, em ordem estável (a ordem de inserção).

R7. **Igualdade e ordem não importam.** Dois conjuntos com as mesmas tags em ordens diferentes são considerados iguais para os fins de `HasAll` nos dois sentidos. (Não é exigida igualdade por `==`; ver P3.)

R8. **Montagem em código.** Existe um jeito de criar um conjunto a partir de uma lista de tags, para uso em testes e em código de jogo, sem precisar de Inspector.

### 4.3 `GameplayTagQuery`

R9. `GameplayTagQuery` é um tipo serializável com três listas de tags: **todas** (o conjunto precisa ter todas), **alguma** (precisa ter ao menos uma, se a lista não estiver vazia) e **nenhuma** (não pode ter nenhuma). Cada lista usa a regra do R1.

R10. `Matches(GameplayTagContainer)` devolve verdadeiro se as três condições valem ao mesmo tempo. Lista vazia não restringe: "todas" vazia é verdadeira, "alguma" vazia é verdadeira, "nenhuma" vazia é verdadeira. Logo, uma consulta totalmente vazia casa com qualquer conjunto (C5).

R11. `Matches(GameplayTag)` aplica a consulta a uma tag isolada, como se fosse um conjunto de um elemento. Sem alocação.

R12. A consulta expõe `IsEmpty` (as três listas vazias). Nenhuma consulta é avaliada por texto: ela é só dados, sem expressões nem nomes.

### 4.4 Acesso à definição

R13. `GameplayTag.GetDefinition()` passa a ser **público** e devolve a `GameplayTagDefinition` (nula se a tag é inválida).

R14. `GameplayTagDefinition` expõe o pai por uma propriedade pública somente leitura (`Parent`, nula na raiz), de modo que uma ferramenta consiga percorrer a cadeia de ancestrais usando só API pública (C6). Esta é a única mudança em `GameplayTagDefinition`; ela não altera o casamento.

### 4.5 Editor, validação e documentação

R15. **Inspector.** `GameplayTagContainer` e `GameplayTagQuery` têm drawer próprio em UI Toolkit (decisões I46 e I48), no assembly `SpellBook.Tags.Editor`: o conjunto é uma lista editável de tags; a consulta mostra as três listas com rótulos claros ("Todas", "Alguma", "Nenhuma"). Nenhum dado serializado se perde se o drawer falhar: sem o drawer, o Inspector padrão da Unity mostra os mesmos campos.

R16. **Validação.** O validador do projeto (0-M, `IValidatable`) aponta, com o objeto clicável: tag vazia em conjunto ou consulta; tag repetida num conjunto; a mesma tag em duas listas da mesma consulta; consulta totalmente vazia (como aviso, não erro). As mensagens aparecem no topo do Inspector e na janela do validador.

R17. **Módulo sem GAS.** Os tipos deste item ficam em `SpellBook.Tags`, que referencia só o Core (0-M, R1 e R2). Nenhum deles referencia o GAS.

R18. **Documentação.** A página do módulo Tags no manual descreve conjunto, consulta, a regra do R1 e o exemplo C4, com conteúdo real. Os tipos novos têm `[HelpURL]` apontando para a seção correspondente (decisão I15).

## 5. Requisitos não funcionais

N1. **Alocação:** consultas e enumeração (R4, R6, R10, R11) não alocam memória gerenciada depois do aquecimento (C7).
N2. **Independência:** `SpellBook.Tags` compila só com o Core (reforça o CA2 da 0-M).
N3. **Serialização:** os dois tipos novos usam serialização nativa da Unity, sem dependência de terceiros (decisão I45). Eles sobrevivem a recarregar o script (domain reload) e a copiar e colar um componente.
N4. **Desempenho:** a consulta é linear no tamanho das listas; não há cache nem índice neste item.
N5. **Testes:** tags de teste são criadas por factory (`ScriptableObject.CreateInstance`), sem assets fixos.

## 6. Fora do escopo

- **Trocar os campos `List<GameplayTag>` existentes** no GAS (habilidades, efeitos, targeting) por `GameplayTagContainer` ou `GameplayTagQuery`. É migração de assets e mudança de serialização; cada fase do GAS adota os tipos onde precisar (ver seção 8, P1).
- `TagQueryCondition` e as demais condições de ativação: **B-009**.
- Filtro por consulta de tags em listas de efeitos ativos e em fontes de UI (`ActiveEffectsSource`): **B-003** (busca por tag) e **B-016 a B-018** (UI).
- Sobrecargas no `AbilitySystemComponent` que recebam conjunto ou consulta (ver P2).
- Consultas com expressões em texto, operadores aninhados (E/OU/NÃO de consultas dentro de consultas) e pesos.
- Eventos de mudança num conjunto, contagem de referências por tag dentro do conjunto (o ASC já faz a sua) e conjuntos "replicados".
- Tags criadas em tempo de execução ou por nome em texto.
- Editor de árvore de tags (janela para criar e organizar definições): sem item no backlog.

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode no host; E = verificação estrutural; S = cenário verificável num Sample ou no host.

**Regra de casamento**
- CA1 (T). Cenário C1: `Elemento.Fogo.Chama` casa com `Elemento.Fogo` e `Elemento`, e não casa com `Elemento.Gelo`.
- CA2 (T). Uma tag sem definição não casa com nada nem com outra tag vazia, em `HasTag`, `HasAny`, `HasAll` e nas consultas (sem exceção).

**`GameplayTagContainer`**
- CA3 (T). Adicionar uma tag repetida ou inválida devolve falso e não altera o conjunto; adicionar uma nova devolve verdadeiro.
- CA4 (T). Remover devolve verdadeiro só se a tag existia; remover um pai não remove os filhos.
- CA5 (T). Cenário C2: `HasTagExact` não usa hierarquia.
- CA6 (T). Cenário C3: `HasAny` e `HasAll` nos quatro casos descritos, incluindo o argumento vazio.
- CA7 (T). `HasAll` e `HasAny` não dependem da ordem das tags.
- CA8 (T). O `foreach` passa por todas as tags na ordem de inserção.
- CA9 (T). Um conjunto serializado e desserializado (`JsonUtility`) volta com as mesmas tags na mesma ordem.
- CA10 (T). O conjunto criado a partir de uma lista (R8) elimina repetidas e inválidas.

**`GameplayTagQuery`**
- CA11 (T). Cenário C4: os três conjuntos de exemplo dão casa, não casa e não casa, respectivamente.
- CA12 (T). Cenário C5: consulta vazia casa com conjunto vazio e com conjunto qualquer; `IsEmpty` é verdadeiro.
- CA13 (T). Cada lista isolada: só "todas", só "alguma" e só "nenhuma" se comportam como no R9.
- CA14 (T). `Matches(GameplayTag)` dá o mesmo resultado de `Matches` de um conjunto com só aquela tag.
- CA15 (T). Hierarquia nas três listas: uma tag filha satisfaz "todas" e "alguma" pelo pai e é barrada por "nenhuma" pelo pai.

**Acesso à definição**
- CA16 (T). `GetDefinition()` é público e devolve a definição usada na criação da tag; para tag inválida, devolve nulo.
- CA17 (T). Cenário C6: um teste em assembly externo ao `SpellBook.Tags` percorre a cadeia de pais de uma tag usando só `GetDefinition()` e `Parent`.

**Editor e validação**
- CA18 (S). No host, `GameplayTagContainer` e `GameplayTagQuery` em um componente de teste aparecem no Inspector, permitem adicionar e remover tags e mostram os rótulos "Todas", "Alguma" e "Nenhuma".
- CA19 (T). O validador aponta tag vazia, tag repetida, a mesma tag em duas listas da consulta e consulta vazia (aviso), com o objeto clicável.
- CA20 (E). Os tipos novos têm `[HelpURL]` e a página do manual do módulo Tags existe com conteúdo real (R18).

**Independência e desempenho**
- CA21 (T). O teste de assemblies da 0-M continua passando: `SpellBook.Tags` não referencia o GAS.
- CA22 (T). Cenário C7: 10 000 consultas de cada operação (R4, R10, R11) e uma enumeração completa alocam 0 bytes gerenciados depois do aquecimento.

## 8. Perguntas em aberto

### Com o usuário

- **P1. Adotar os tipos nos campos existentes do GAS. RESPONDIDA (2026-10-31): adotar o container só nas fases que precisarem.** Os `List<GameplayTag>` de habilidades, efeitos e targeting não são trocados neste item; cada fase do GAS migra o campo de que precisar (B-009 usa a consulta nas condições; B-003 na busca por tag). Já refletido na seção 6.
- **P2. Sobrecargas no ASC. RESPONDIDA (2026-10-31): sem `HasMatching` no ASC agora.** Fica para B-003 ou B-009, onde a consulta é usada. Já refletido na seção 6.

### Com o Arquiteto (decisão técnica da Spec)

- **P3. Igualdade do conjunto.** Se `==`/`Equals` entre conjuntos for útil para as fases seguintes (por exemplo, detectar mudança), a Spec define a semântica; este PRD só exige o R7.
- **P4. Representação interna** do conjunto para atender ao R6 e ao N1 (enumerador por struct, lista de tags serializada), incluindo como dois campos de `GameplayTagContainer` num mesmo componente se comportam no Inspector.
- **P5. Onde moram as listas "todas / alguma / nenhuma"** na consulta (três `GameplayTagContainer` ou três listas de tags) e o efeito disso no drawer.

## 9. Diário de decisões e ajustes

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Origem | Decisão ou observação | Efeito neste PRD | Status |
|---|---|---|---|---|
| 2026-10-31 | Controlador | PRD escrito para o Lote A do backlog, a partir do texto de B-006 e do plano | Todo o documento | Rascunho |
| 2026-10-31 | usuário (via Controlador) | P1 = adotar o container só nas fases que precisarem; P2 = sem `HasMatching` no ASC agora (fica para B-003/B-009) | Seção 6 (confirmada); P1 e P2 marcadas como respondidas | Aplicado |
| 2026-10-29 | usuário | I5: Tags é módulo próprio da 3.0 (`SpellBook.Tags`, referencia só o Core) | R17, N2, CA21 | Aplicado |
| 2026-10-29 | usuário | I45, I46 e I48: sem Odin, editor próprio em UI Toolkit, Inspector base com validação no topo | R15, R16, N3 | Aplicado |
