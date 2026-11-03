# PRD: Ativação avançada (condições e cargas)
Backlog: B-009 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção C (itens 4 e 5), seção E (Fase 5). ADRs [0019](../adr/0019-hierarquia-unica-de-condition.md) (condições), [0022](../adr/0022-ordem-das-checagens-de-ativacao.md) (ordem das checagens) e [0013](../adr/0013-serializereference-como-costura-entre-modulos.md) (costura).
Depende de: **B-003** (`CooldownReductionPolicy`, controle de recarga, `OnAbilityGranted/Removed`), **B-007** (`ScalableFloat` e `%nome`, para `MaxCharges` e `RechargeTime` por nível), **B-006** (`GameplayTagQuery`) e **B-002** (recarga normalizada, casamento hierárquico de tags). Este PRD usa o que eles entregam e não reescreve o escopo deles (seção 6).
Este PRD não tem foco em nenhum jogo. As menções a jogos no plano são histórico.

---

## 1. Problema (contexto)

Hoje a pergunta "esta habilidade pode ser ativada?" tem poucas respostas e nenhuma é configurável:

- **A checagem é um enum fixo e não recebe o alvo.** `GetActivationResult` testa tags, recarga e custo, e devolve um dos 9 valores. Uma regra como "só ativa com alvo no alcance" ou "só ativa com vida abaixo de 30 %" exige código fora do pacote, chamado **antes** do `TryActivateAbility`, porque o pacote aplica custo e recarga antes de qualquer ação rodar.
- **O motivo da falha é pobre.** A UI recebe o enum, sem tag nem chave de texto para mostrar "sem alvo" ou "mana insuficiente" com localização.
- **A ordem das checagens não é definida.** Com várias falhas ao mesmo tempo, não está escrito qual motivo a UI mostra.
- **Não existem cargas.** "3 usos, uma carga volta a cada 8 s" é reimplementado por cada jogo com um relógio paralelo e uma cópia da conta de redução de recarga.
- **A única condição configurável é um enum** (`ConditionalAction.ConditionType`: tem tags, tem alvos), que não compõe (sem E, OU, NÃO) e só serve àquela ação.

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, pelo Inspector e sem script:

1. listar **condições de ativação** numa habilidade (E, OU, NÃO compõem), escolhidas por `[SerializeReference]`;
2. saber, ao falhar uma ativação, **qual checagem falhou e por quê** (resultado, tag ou chave de texto), sempre pelo primeiro motivo da ordem fixa **tags → cargas/recarga → custo → condições**;
3. consultar "pode ativar?" **sem efeito colateral**, passando o ponto-alvo, e receber o mesmo resultado da ativação real;
4. dar **cargas** a uma habilidade: máximo constante, por atributo ou por nível, recarga sequencial, eventos e controle por código;
5. reagir à falha de ativação e à mudança de cargas por eventos C# do ASC.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Designer técnico** | Montar a regra de ativação arrastando condições (alcance, atributo, tag, tem alvo) e configurar cargas no asset |
| **Desenvolvedor de gameplay** | `GetActivationResult(ability, alvo)` sem efeito colateral; evento com motivo; `AddCharges`/`RefillCharges`; condição própria como subclasse |
| **Desenvolvedor de UI** (B-016) | Motivo de falha estruturado para o slot, cargas atuais e recarga da próxima carga por leitura e evento |
| **Mantenedor do pacote** | Uma hierarquia de condição só (ADR 0019), reutilizada por ações, filtros e UI nos itens seguintes |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, condição passa e falha:** uma habilidade com `AttributeThresholdCondition` (mana ≥ 20). Com mana 30 ativa; com 10 devolve `BlockedByCondition` e o motivo configurado.
- **C2, composição:** `Or(TagQuery, HasTargets)` passa com qualquer um dos dois; `Not(HasTargets)` falha com alvos; `And` exige todos.
- **C3, ordem:** habilidade atordoada (tag bloqueadora), em recarga e sem custo: o motivo é o da **tag**. Só em recarga e sem custo: o motivo é a **recarga**. Sem custo e fora de alcance: o motivo é o **custo**; com custo pago e fora de alcance: o da **condição**.
- **C4, sem efeito colateral:** `GetActivationResult` não consome carga, não aplica custo, não aplica recarga nem dispara `OnAbilityActivated`, e devolve o mesmo resultado que `TryActivateAbility` daria.
- **C5, alvo na consulta:** `CastRangeCondition` com alcance 5: alvo a 3 passa, a 8 falha, e a consulta recebe o ponto sem ativar.
- **C6, cargas:** `MaxCharges = 3`. Três ativações seguidas gastam as 3 cargas; a quarta devolve `NoCharges`. Passado `RechargeTime`, volta 1 carga; passado o dobro, volta a segunda (recarga **sequencial**: uma carga por vez).
- **C7, cargas e recarga:** com 1 de 3 cargas e a recarga de carga correndo, a habilidade **ativa** (a checagem de carga substitui a de recarga; P2).
- **C8, máximo por nível:** `MaxCharges` por `%maxCargas` (B-007): nível 1 dá 2, nível 3 dá 4. Ao subir de nível o máximo sobe; ao descer, as cargas atuais são limitadas ao novo máximo.
- **C9, controle:** `AddCharges(spec, 1)` respeita o máximo; `RefillCharges(spec)` enche; ambos disparam `OnChargesChanged`.
- **C10, redução de recarga:** a recarga de carga usa a `CooldownReductionPolicy` (B-003): piso e teto valem para o tempo de recarga das cargas.
- **C11, condição própria:** uma subclasse de `Condition` do projeto aparece no seletor do Inspector e é avaliada na ordem da lista.
- **C12, motivo para a UI (S):** no host, o evento de falha traz resultado e motivo; a UI de teste mostra a chave ou a tag da primeira checagem que falhou.
- **C13, sem lixo:** 10 000 consultas de `GetActivationResult` com 4 condições e avaliação de cargas não alocam memória gerenciada.

## 4. Requisitos funcionais

### 4.1 Hierarquia de condição (ADR 0019)

R1. **Base de condição.** Uma hierarquia única de `Condition`, com a base em `SpellBook.Core`, escolhida por `[SerializeReference]`. Como a base ainda não existe no código (o `modularizacao.md` a deixou para "a fase que a usa"), **este item a entrega**. Contextos diferentes (ativação, execução de ação, canal, UI) não são o mesmo tipo: o desenho do contexto genérico é do Arquiteto (ADR 0019, "em aberto"; P5).

R2. **Composição pronta:** `And`, `Or` e `Not`, aceitando qualquer condição do mesmo contexto. Lista vazia em `And` é verdadeira; em `Or`, falsa (e o validador avisa).

R3. **Condições são puras.** Avaliam e devolvem verdadeiro ou falso, sem efeito colateral e **sem alocar**. Uma condição pode informar um **motivo de falha**: uma tag (`GameplayTag`) ou uma **chave de texto**, nunca texto pronto (ADR 0019, 0023).

R4. **Contexto de ativação.** A condição de ativação recebe: o ASC, o `AbilitySpec` (habilidade e nível), e o alvo pretendido (ponto; a unidade-alvo entra no B-012). Ele não permite alterar o ASC.

### 4.2 Condições de ativação da habilidade

R5. **`ActivationConditions`.** O `GameplayAbility` tem uma lista `[SerializeReference]` de condições, avaliadas **na ordem da lista**, e a ativação só passa se **todas** passarem (a lista é um `And` implícito; o designer usa `Or` e `Not` explícitos para o resto).

R6. **Condições prontas** (cada uma com `[HelpURL]` e página no manual, ADR 0019):
- `HasTargetsCondition`: há ao menos um alvo válido (hoje o `ConditionalAction` já testa "tem alvos");
- `AttributeThresholdCondition`: atributo do ASC comparado a um valor (`ModifierValueSource`: constante, atributo ou `%nome` do B-007), com comparação maior, menor ou igual, e opção de usar a razão (valor / máximo);
- `CastRangeCondition`: o ponto-alvo está dentro do alcance (distância configurável por `%nome`, pelo B-007). A versão com unidade-alvo e alcance até a borda do alvo é do B-012;
- `TagQueryCondition`: as tags do ASC casam com um `GameplayTagQuery` (B-006) e informam o motivo.

R7. **`HasMatching` no ASC.** Pela decisão do B-006 (P2), a sobrecarga `HasMatching(GameplayTagQuery)` e a versão com `GameplayTagContainer` entram neste item, como a leitura que `TagQueryCondition` usa e que a UI pode usar. A busca de **efeitos** por conjunto de tags é do B-003 (R4 daquele PRD) e não muda.

R8. **`ConditionalAction.ConditionType`.** O enum atual deixa de ser a única condição, mas a **ação condicional** continua como está até o B-012 reescrever as ações (ADR 0018). Este item **não** migra as ações; o B-012 faz o `ConditionalAction` usar a hierarquia do R1 e remove o enum (ADR 0019, item 6).

### 4.3 Resultado, ordem e motivo da falha (ADR 0022)

R9. **Ordem fixa, para na primeira falha:**
0. pré-requisitos (habilidade válida, concedida, não ativa);
1. tags (bloqueadoras, exigidas e bloqueio por habilidade ativa);
2. cargas ou recarga;
3. custo;
4. condições plugáveis, na ordem da lista.

R10. **`ActivationFailure`.** O resultado da checagem traz o **tipo da falha** (`AbilityActivationResult`), o **motivo** (tag ou chave de texto) da primeira checagem que falhou e, quando a falha vem de uma condição, a identificação dela. Novos valores do enum (`BlockedByCondition`, `NoCharges`) entram **no fim** (serializado como inteiro, ADR 0022).

R11. **Consulta sem efeito colateral e com alvo.** `GetActivationResult(ability, alvo)` usa **o mesmo caminho** da ativação, não altera estado (nem gasta carga, nem aplica custo ou recarga, nem dispara eventos) e aceita o ponto-alvo. A sobrecarga antiga sem alvo continua usando as mesmas checagens com alvo ausente. As condições que precisam de alvo informam a falha própria quando ele não existe.

R12. **Evento de falha com motivo.** O ASC dispara um evento C# com a habilidade e o `ActivationFailure`. A relação com o `OnAbilityFailed` atual (manter os dois ou unificar, pela decisão I1 de quebra livre) é do Arquiteto (P4). O relay do B-004 repassa o evento de falha com o novo payload (aditivo).

R13. **Um caminho só.** A UI nunca vê "pode" quando a ativação diz "não pode": consulta e ativação usam a mesma função de checagem (ADR 0022, "não podem fazer").

### 4.4 Cargas

R14. **`ChargesConfig`** no `GameplayAbility`: `Enabled`; `MaxCharges` como `ModifierValueSource` (constante, atributo ou `%nome` por nível, B-007); `RechargeTime` (segundos, aceita `%nome`); `RechargeMode` somente **`Sequential`** (uma carga por vez, como Dota e LoL). O modo paralelo **não** entra (P1). Padrão: cargas desligadas, comportamento igual ao de hoje.

R15. **Estado no `AbilitySpec`.** O spec guarda as cargas atuais e o tempo da carga em recarga. O tick vem do `ASC.Tick` com o `ITimeSource` (ADR 0014), nunca de `Time.time`. Cargas nascem **cheias** ao conceder a habilidade.

R16. **Gasto e recarga.** Uma ativação com sucesso gasta **uma** carga **no commit** (junto do custo, depois das checagens). Se a habilidade estava com todas as cargas e o gasto a deixa abaixo do máximo, a recarga da próxima carga começa. Voltou uma carga com o spec abaixo do máximo, a recarga da carga seguinte recomeça do zero. Chegou ao máximo, a recarga para.

R17. **Recarga e política.** O `RechargeTime` aplicado passa pela `CooldownReductionPolicy` (B-003, R14) e pela redução por atributo, com piso e teto. A conta é **uma só** (a mesma do `GameplayAbility`), não uma cópia.

R18. **Máximo muda.** Se o máximo cai abaixo das cargas atuais (nível, atributo), as cargas são limitadas ao novo máximo. Se o máximo sobe, as cargas atuais **não** aumentam sozinhas, e a recarga passa a repor até o novo máximo. Se o máximo cai com recarga correndo no máximo, a recarga para.

R19. **Substitui a recarga (P2, decidida).** Com cargas habilitadas, a checagem da ordem (item 2) é "tem carga?" e **não** "está em recarga?". `NoCharges` é o resultado sem carga. Se a habilidade também tem o efeito de recarga (`Cooldown`) para o tempo entre usos (opcional, P3 decidida), ele continua valendo como checagem de recarga: com carga disponível mas o `Cooldown` correndo, o resultado é o de recarga.

R20. **API e eventos.** O ASC oferece: `GetCharges(spec)`, `GetMaxCharges(spec)`, `GetChargeRechargeRemaining(spec)` (e a versão normalizada), `AddCharges(spec, n)` (respeita o máximo), `RefillCharges(spec)` e o evento C# `OnChargesChanged(spec, antes, depois)`, que dispara só em mudança real. Sem alocação.

R21. **Controle de recarga do B-003 com cargas.** `ReduceCooldown`, `ResetCooldown` e `RestartCooldown` (B-003, R12) agem, em habilidade com cargas, sobre a **recarga da carga em curso** (`Reduce` reduz o tempo; `Reset` devolve a carga e começa a próxima; `Restart` volta ao tempo cheio). Os detalhes são do Arquiteto (P6).

### 4.5 Editor, validação e documentação

R22. **Inspector** (UI Toolkit, ADR 0026, herdando do `SpellBookEditor`): a lista de condições com o seletor de tipo, resumo de uma linha e composição aninhada; o bloco de cargas com a prévia "carga a cada X s, máximo Y no nível N"; o motivo de falha visível na condição.

R23. **Validação** (`IValidatable`, 0026): condição nula na lista; `And`/`Or` vazios; condição de atributo sem atributo; `CastRangeCondition` com alcance negativo; `TagQueryCondition` com consulta vazia ou tag inválida; cargas habilitadas com `MaxCharges` menor que 1 ou `RechargeTime` menor ou igual a zero; `%nome` inexistente em campos de cargas. Mensagens clicáveis.

R24. **Documentação.** A página de Habilidades descreve a ordem das checagens (R9), as condições prontas com um exemplo de uso e o motivo de falha para a UI; uma página de **Cargas** descreve a recarga sequencial. A página "como criar uma condição própria" mostra a subclasse mínima. Os tipos novos têm `[HelpURL]` (decisão I15).

R25. **CHANGELOG.** Cita: base `Condition` (Core), condições de ativação, `BlockedByCondition` e `NoCharges` no enum, a assinatura nova da consulta e do evento de falha (quebra, ADR 0009), cargas e `OnChargesChanged`.

## 5. Requisitos não funcionais

N1. **Alocação:** avaliar condições, consultar `GetActivationResult` e avançar o tempo das cargas não alocam depois do aquecimento (C13). Disparar eventos sem ouvintes não aloca.
N2. **Módulos (ADR 0012):** a base `Condition` em `SpellBook.Core`; condições do GAS, cargas e eventos em `SpellBook.GAS`. **Condições de time vão para `SpellBook.GAS.Bridges`** (B-011), não aqui. O GAS não referencia Events, Pooling, Teams ou UI.
N3. **Determinismo:** testes com `ManualTimeSource` e factories (`GasFactory`), sem assets fixos.
N4. **Serialização:** `[SerializeReference]` para condições; cargas desligadas por padrão; valores novos do enum só no fim; assets existentes abrem sem perda de dados.
N5. **Sem regressão:** a suíte do GAS (com B-002, B-003) passa sem edição de teste antigo, exceto os testes que usam a assinatura da consulta ou do evento de falha que o R11 e o R12 mudam; o Arquiteto os lista na Spec.
N6. **Sem efeito colateral na consulta** (R11): coberto por teste que compara o estado antes e depois.

## 6. Fora do escopo

- **Já entregue pelo Lote A:** recarga normalizada e casamento hierárquico (B-002); `GameplayTagContainer` e `GameplayTagQuery` (B-006). Aqui só são usados.
- **B-003:** `CooldownReductionPolicy`, `ReduceCooldown`/`ResetCooldown`/`RestartCooldown` e `OnAbilityGranted/Removed`. Aqui as cargas reutilizam a política (R17) e os controles (R21).
- **B-007:** `ScalableFloat`, `AbilityValues`, `%nome` e `EffectContext`. `MaxCharges` e alcance por nível dependem deles.
- **B-004:** o relay repassa o novo evento de falha e `OnChargesChanged` depois (aditivo).
- **Recarga paralela por carga** (cada carga com relógio próprio): fora (P1).
- **Alvo por unidade, alcance até a borda do alvo, ativação com unidade-alvo, commit explícito:** **B-012** (ADR 0018, 0022). Aqui o alvo da consulta é um **ponto**.
- **Migração do `ConditionalAction` para a hierarquia e remoção do enum:** B-012 (R8).
- **Condições de time e filtros de relação:** B-011 e `GAS.Bridges`.
- **Elementos de descrição de cargas (`ChargesElement`):** B-008. **Slot de habilidade com cargas e motivo de falha na UI:** B-016.
- **Modo de depuração que coleta todas as falhas** (ADR 0022, "em aberto"): fora; o Debugger do B-003 pode ganhar depois (aditivo).
- **Cargas com recarga por "cooldown com stacks"** estilo Unreal: descartada pela decisão I10 (config explícita).

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

**Condições**
- CA1 (T). Cenário C1: `AttributeThresholdCondition` passa e falha; a falha devolve `BlockedByCondition` com o motivo configurado.
- CA2 (T). Cenário C2: `And`, `Or` e `Not` compõem; lista vazia segue o R2.
- CA3 (T). Cada condição pronta (R6) tem teste de passa e de falha; `TagQueryCondition` usa `all`, `any` e `none` (B-006).
- CA4 (T). Cenário C11: uma condição própria aparece no seletor e é avaliada na ordem.
- CA5 (T). Condições não alteram o ASC (estado antes e depois iguais).
- CA6 (T). `HasMatching(GameplayTagQuery)` e a versão com `GameplayTagContainer` devolvem o esperado; tag inválida não casa (B-006, R2).

**Ordem, resultado e consulta**
- CA7 (T). Cenário C3: um teste por posição da ordem (tags, cargas/recarga, custo, condições) com várias falhas ao mesmo tempo; vale o primeiro motivo.
- CA8 (T). Cenário C4: `GetActivationResult` não gasta carga, custo nem recarga e não dispara eventos; o resultado é o mesmo de `TryActivateAbility`.
- CA9 (T). Cenário C5: a consulta recebe o ponto e `CastRangeCondition` passa dentro do alcance e falha fora.
- CA10 (T). Os novos valores do enum estão no fim (teste de ordem); um asset serializado com valor antigo abre igual.
- CA11 (T). O evento de falha traz habilidade, resultado e motivo; é o mesmo `ActivationFailure` da consulta (R13).
- CA12 (T). Sem alvo, a condição que o exige informa a falha própria, sem exceção.

**Cargas**
- CA13 (T). Cenário C6: três ativações gastam as cargas, a quarta devolve `NoCharges`, e a recarga é sequencial (uma carga por `RechargeTime`).
- CA14 (T). Cenário C7: com carga disponível e recarga de carga correndo, a habilidade ativa.
- CA15 (T). Cenário C8: `MaxCharges` por `%nome` e por atributo; subir o máximo não enche, descer limita (R18).
- CA16 (T). Cenário C9: `AddCharges` respeita o máximo; `RefillCharges` enche; `OnChargesChanged` só dispara em mudança real.
- CA17 (T). Cenário C10: o tempo de recarga das cargas respeita piso e teto da `CooldownReductionPolicy`.
- CA18 (T). As cargas nascem cheias ao conceder a habilidade; cargas desligadas reproduzem o comportamento de hoje.
- CA19 (T). Com cargas e `Cooldown` configurados, o `Cooldown` barra o uso entre ativações mesmo com carga disponível (P3); sem `Cooldown`, só as cargas decidem.
- CA20 (T). `ReduceCooldown`, `ResetCooldown` e `RestartCooldown` agem sobre a recarga da carga em curso, conforme a Spec (R21).
- CA21 (T). O tempo das cargas usa o `ITimeSource` (teste com `ManualTimeSource`, sem relógio real).

**Desempenho, módulos, editor e documentação**
- CA22 (T). Cenário C13: 10 000 consultas com 4 condições e 1000 ticks de cargas alocam 0 bytes gerenciados depois do aquecimento.
- CA23 (T). O teste de assemblies da 0-M continua passando: `Condition` no Core, o GAS sem Events, Pooling, Teams ou UI.
- CA24 (S). No host, o Inspector mostra a lista de condições com o seletor de tipo, o resumo de uma linha e o bloco de cargas com a prévia.
- CA25 (T). O validador cobre os casos do R23, com o objeto clicável.
- CA26 (E). O CHANGELOG cita os itens do R25; o manual descreve R9, as condições e as cargas; os tipos novos têm `[HelpURL]`.
- CA27 (T). A suíte do GAS existente passa sem edição, exceto os testes listados na Spec (N5).
- CA28 (S). Cenário C12: no host, a UI de teste mostra o motivo (chave ou tag) da primeira checagem que falhou.

## 8. Perguntas em aberto

### Com o usuário

- **P1. Recarga paralela de cargas.** *Respondida (usuário, 2026-10-31): aceita. Só recarga sequencial; o modo paralelo fica fora e pode virar item depois.*
- **P2. Cargas substituem a checagem de recarga.** *Respondida (usuário, 2026-10-31): aceita. Com cargas habilitadas, uma habilidade com 1 de 3 cargas e a recarga de carga correndo **ativa** (ADR 0022, "em aberto", fica decidido).*
- **P3. Recarga entre usos junto com cargas.** *Respondida (usuário, 2026-10-31): aceita. O `Cooldown` (tempo mínimo entre dois usos) é **opcional** e coexiste com as cargas (por exemplo, "3 cargas, mas no mínimo 1 s entre usos").*

### Com o Arquiteto (decisão técnica da Spec)

- **P4. Evento de falha.** Manter `OnAbilityFailed` (enum) e acrescentar `OnAbilityBlocked(ability, ActivationFailure)` (plano, item 4), ou unificar num só evento com o `ActivationFailure` (ADR 0022, "consequências", e I1 sem fachada). A proposta do Criativo é **um só evento**.
- **P5. Contexto genérico da `Condition`** (ADR 0019, "em aberto"): `Condition<TContext>` com `[SerializeReference]` genérico (suporte a confirmar na 6000.0), base não genérica com consulta por tipo, ou contexto único do GAS; e se o motivo de falha em tag faz o Core depender de Tags (a chave de texto não faz).
- **P6. Cargas e o controle de recarga do B-003** (R21): semântica exata de `Reduce`, `Reset` e `Restart` por carga; e se a recarga de carga é um estado do spec ou "efeito de recarga ativo" (ligado à P4 do B-003).
- **P7. Filtro do seletor do Inspector** por contexto aceito (ADR 0019, "em aberto"): como a lista de ativação só oferece condições compatíveis.
- **P8. Ordem entre as condições da lista e o custo:** o custo é só checado (sem consumo) até o commit; confirmar que condições nunca consomem (ADR 0022).
- **P9. Cargas com `EffectContext`:** quando `%nome` de `MaxCharges` é resolvido (na concessão e na mudança de nível), sem alocação por consulta (B-007, R7).

## 9. Ajustes de playtest

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | PRD escrito pelo Criativo a partir do B-009, do plano (itens 4 e 5) e das ADRs 0019 e 0022; o Controlador pediu o PRD inteiro e a Spec divide em tickets | Todo o documento | Rascunho |
| 2026-10-31 | Dependências registradas sem puxar escopo: B-003, B-007, B-006, B-002; B-012 fica com unidade-alvo, commit explícito e migração do `ConditionalAction` | Seção 6 | Aplicado |
| 2026-10-31 | B-006 P2 (usuário): `HasMatching` no ASC fica para B-003/B-009; o B-003 não o assumiu, então entra aqui | R7 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P1: só recarga sequencial; o modo paralelo fica fora | R14, seção 6 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P2: com carga disponível a habilidade ativa mesmo com a recarga de carga correndo | R19, cenário C7, CA14 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P3: `Cooldown` entre usos é opcional e coexiste com as cargas | R19, CA19 | Aplicado |
