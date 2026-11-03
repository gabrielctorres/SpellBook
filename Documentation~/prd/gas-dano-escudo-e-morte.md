# PRD: Dano, escudo e morte
Backlog: B-010 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção C (itens 6 e 7), seção E (Fase 6), decisão I.7. ADR [0024](../adr/0024-pipeline-generico-de-dano.md) (pipeline genérico de dano), com a 0013 (processadores por `[SerializeReference]`) e a 0018 (contexto).
Depende de: **B-005** (`OnDepleted`/`OnReachedMax`, limites dinâmicos e a ordem de composição), **B-007** (`EffectContext`: instigador, habilidade, nível, ponto; valores do chamador) e **B-002** (valor base limitado entre mínimo e máximo, que o B-010 apenas usa). Usa também **B-003** (lista e eventos de efeito) e **B-004** (relay). Este PRD não reescreve o escopo deles (seção 6).
Este PRD não tem foco em nenhum jogo, e o SpellBook **não** tem componente de vida: "dano" aqui é uma alteração negativa de atributo e "morte" é um sinal (ADR 0024, item 4).

---

## 1. Problema (contexto)

O GAS altera atributos, mas não sabe que uma alteração é "dano":

- **Não há gancho antes de uma alteração.** Nada pode reduzir, redirecionar ou anular uma alteração pendente de atributo. Resistência a fogo, imunidade a veneno e escudo viram código fora do pacote.
- **Não há absorção.** Um escudo que consome o dano antes da vida precisa de um sistema de terceiros ou de script que escuta o atributo e "devolve" vida, que atrasa um quadro e é fácil de errar.
- **Morte não tem sinal próprio.** O B-005 entrega "o atributo chegou ao mínimo" (`OnDepleted`), mas quem usa o pacote ainda não sabe **quem causou** o último dano, nem consegue bloquear habilidades por uma tag de estado sem código.
- **Tipo de dano não existe.** Fogo, veneno e físico são o mesmo número; resistências e imunidades por tipo exigem condicionais em cada efeito.
- **Cada jogo repete a conta.** O dano com origem e alvo (ataque do instigador contra defesa do alvo) é recalculado em scripts próprios.
- **O Sample Sandbox (B-032) cortou a morte.** Por decisão do usuário, o Sample **não** trata vida zero nem evento de morte (PRD do Sample, R9 e seção 6). Este é o item que a entrega.

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, pelo Inspector:

1. ligar **processadores de pré-alteração** a um atributo, numa lista ordenada, que podem reduzir, redirecionar ou anular uma alteração antes de ela valer;
2. usar um **escudo**: um atributo que absorve a alteração antes do atributo protegido, com o "escudo quebrou" por evento;
3. aplicar dano por um **meta-atributo opcional** (por exemplo, dano recebido), que os processadores convertem em alterações nos atributos reais;
4. marcar um efeito com **tipos de dano por tag** (fogo, veneno, físico) e ter resistências e imunidades por tipo;
5. receber o **evento de morte**: o atributo chegou ao mínimo, com **quem causou** o último dano, e opcionalmente uma **tag de estado** (por exemplo, "morto") concedida;
6. fazer tudo isso **sem** que o GAS tenha vida, morte ou destruição de objeto: quem usa o pacote decide o que é morrer.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Designer técnico** | Montar escudo, resistência por tipo de dano e tag de "morto" no Inspector, sem script |
| **Desenvolvedor de gameplay** | Evento de morte com o causador; processador próprio como subclasse; quem já tem sistema de vida pode ignorar o meta-atributo e escutar só o evento |
| **Desenvolvedor de UI** (B-015 a B-018) | Barra de escudo (atributo), "escudo quebrou" e dano recebido por leitura e evento |
| **Mantenedor do pacote** | Peças pequenas e substituíveis; nenhum conceito de engine, gênero ou terceiro no pipeline (ADR 0024, item 7) |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, escudo absorve:** `Escudo` 50, `Vida` 100. Dano instantâneo de 30 na `Vida`: `Escudo` vai a 20 e `Vida` fica em 100.
- **C2, absorção parcial:** `Escudo` 20, dano de 30: `Escudo` vai a 0 e `Vida` perde 10.
- **C3, escudo quebrou:** o `Escudo` chegando a 0 dispara o `OnDepleted` dele (B-005) uma vez; mais dano vai direto à `Vida`.
- **C4, sem processador:** um atributo sem processadores recebe o dano como hoje (comportamento idêntico, mesmos valores).
- **C5, meta-atributo:** o efeito aplica 40 em `DanoRecebido`; os processadores convertem em alterações em `Escudo` e `Vida`; o próprio `DanoRecebido` não guarda valor.
- **C6, resistência por tipo:** um efeito com a tag `Dano.Fogo` e 100 de dano contra um alvo com 50 % de resistência a `Dano.Fogo` reduz 50 da `Vida`. O mesmo efeito com `Dano.Veneno` reduz 100.
- **C7, tipos múltiplos e hierarquia:** um efeito com `Dano.Fogo` e `Dano.Magico`; resistência a `Dano` (pai) casa com os dois (casamento hierárquico, B-002 R8).
- **C8, imunidade:** processador de imunidade por tipo anula a alteração (valor 0), sem mexer em nada e sem disparar mudança de valor.
- **C9, ordem:** com processadores `[Resistência, Absorção]` e `[Absorção, Resistência]`, o resultado muda como esperado e é o da ordem da lista.
- **C10, morte com causador:** `Vida` chega ao mínimo por dano de um instigador: `OnDepleted` dispara uma vez e carrega o **instigador** e o efeito (`EffectContext`). Mais dano no mínimo não dispara de novo.
- **C11, tag de estado:** com "conceder tag enquanto no mínimo" ligado e a tag `Estado.Morto`, a tag é concedida ao chegar ao mínimo e devolvida ao sair dele (cura). Uma habilidade com `Estado.Morto` em `BlockedTags` deixa de ativar (resultado de tags, B-009).
- **C12, cura e base:** `Vida` na base 0 (mínimo): a cura seguinte sobe a `Vida` (a base não ficou negativa, B-002).
- **C13, relay (S):** no host, o relay de um ator dispara um canal quando `Vida` chega ao mínimo, com o instigador no payload.
- **C14, sem lixo:** 10 000 alterações com 3 processadores não alocam memória gerenciada.

## 4. Requisitos funcionais

### 4.1 Processadores de pré-alteração

R1. **Alteração pendente.** Antes de o valor **base** de um atributo mudar por efeito instantâneo ou por tick periódico, o GAS monta uma **alteração pendente** (atributo, operação, variação, contexto do efeito) e a passa pelos processadores do atributo, **na ordem da lista**. O resultado final é o que é aplicado. Se a variação final for zero, não há mudança de valor e nenhum evento de valor dispara.

R2. **Processador.** Um `AttributeChangeProcessor` é uma classe base escolhida por `[SerializeReference]` (ADR 0013), com um método que recebe a alteração pendente e o `EffectContext` e pode: **reduzir** ou **aumentar** a variação, **redirecionar** parte dela para outro atributo (gerar alterações adicionais) ou **anulá-la**. O processador **não** aplica efeitos, não dispara eventos de gameplay e não guarda estado de execução na sua instância (ADR 0013).

R3. **Onde se configura.** O designer liga a lista de processadores **por atributo no Inspector**. A Spec decide se a lista mora na definição do atributo (vale para todo ASC), no `AttributeSet` (por entidade) ou nos dois (ADR 0024, "em aberto"; P5). A ordem é a da lista; com dois níveis (definição e entidade), a Spec fixa a ordem entre eles.

R4. **Escopo das alterações (P7).** Os processadores agem nas alterações do **valor base** (efeito instantâneo e tick periódico). Os **modificadores de efeitos com duração** não passam por processadores: não são "dano". Proposta, confirmada ou ajustada pela Spec.

R5. **Sem processadores, sem custo.** Um atributo sem processadores não aloca nem faz trabalho extra, e o resultado é idêntico ao de hoje (C4).

R6. **Contexto nos processadores.** Origem (instigador), alvo, habilidade, nível, ponto, **tags de dano** e valores do chamador vêm do `EffectContext` (B-007). Sem contexto (alteração por código sem efeito), os processadores recebem um contexto vazio e devem funcionar.

### 4.2 Meta-atributo de dano (opcional)

R7. **Meta-atributo.** Um atributo pode ser marcado como **meta-atributo** (por exemplo, `DanoRecebido`): um efeito o altera, e os processadores convertem esse valor em alterações nos atributos reais (escudo, vida). O meta-atributo **não guarda valor** depois de processado (volta a zero, sem disparar mudança visível ao jogo). Quem não precisa aplica direto no atributo de vida (C4). A forma exata (atributo comum marcado ou tipo especial) é da Spec (P6).

R8. **Cálculo com origem e alvo.** O valor do dano pode vir de atributo do **instigador** e do alvo (B-007, R15), por exemplo, "ataque do instigador × 1,5". Um "cálculo de execução" mais geral (crítico, dependência dos dois ASCs numa fórmula) **não** é entregue como tipo próprio (P3 do usuário); o designer pode compor com fórmula, `Chance` (B-012) ou um processador próprio.

### 4.3 Absorção (escudo)

R9. **`AbsorbWithAttribute`.** Processador pronto: dado o atributo de escudo, ele consome primeiro a variação negativa do atributo protegido, reduzindo o escudo e deixando só o resto passar. Respeita o piso do escudo (B-002), suporta configurar **quais tags de dano** ele absorve (todas, por padrão) e **a que fração** absorve (de 0 a 100 %, padrão 100 %).

R10. **Escudo quebrou.** O `OnDepleted` do atributo de escudo (B-005) é o evento "quebrou". O pipeline não cria evento novo para isso. O relay (R20) pode repassar.

R11. **Dono do escudo (P1).** O escudo é um **atributo base consumido**: ele sobe por efeito instantâneo, tick ou por um efeito de partida; ele não expira sozinho. O "escudo que some depois de N segundos" (escudo como modificador de um efeito com duração, ou remoção por efeito) **não** entra neste item.

### 4.4 Tipos de dano por tag

R12. **Tags de dano no efeito.** O `GameplayEffect` tem uma lista de **tags de dano** (`GameplayTag`, B-006), levadas no `EffectContext` quando o efeito é aplicado. Tipo de dano é **sempre tag**, nunca enum (ADR 0024, "não podem fazer"). A tag segue o casamento hierárquico (`Dano.Fogo` casa com `Dano`).

R13. **Processadores prontos por tipo.** Além do `AbsorbWithAttribute`: `ReduceByAttribute` (reduz a variação por uma porcentagem ou valor plano vindo de um atributo do alvo, só para variações que tenham alguma das tags configuradas) e `ImmuneToTags` (anula a variação quando o efeito tem alguma das tags). A imunidade por **tag de efeito** que o GAS já tem (bloqueio de aplicação) continua como está; este processador é para anular **só o dano**, sem barrar o efeito inteiro (P4).

R14. **Processador próprio.** O projeto cria processadores como subclasses de `AttributeChangeProcessor`, que aparecem no seletor do Inspector. Sem alocação por avaliação.

### 4.5 Morte como sinal

R15. **Evento de morte.** O ASC expõe um evento C# (nome da Spec) quando um atributo chega ao mínimo (`OnDepleted`, B-005), com: o atributo, o `EffectContext` da última alteração que causou a transição (instigador, efeito, habilidade) e as tags de dano. Se a transição vier de uma alteração sem efeito (por código), o contexto é vazio. O evento dispara **na transição** (regra do B-005 R7), depois do valor final.

R16. **Instigador destruído ou desabilitado.** O evento nunca lança exceção por causa do instigador: se ele foi destruído, a referência chega nula (e a Spec decide se um identificador sobrevive, P8).

R17. **Tag de estado opcional.** No `AttributeDefinition`, um campo opcional `StateTagWhileDepleted` (uma `GameplayTag`) faz o ASC conceder a tag **enquanto o atributo estiver no mínimo** e devolvê-la ao sair dele (cura, aumento do mínimo). Padrão **desligado** (P2). A tag entra no sistema de tags do ASC como qualquer outra (com contagem), então `BlockedTags` e `RequiredTags` (B-009, ordem) já funcionam com ela.

R18. **O GAS não destrói nada.** Nenhum tipo do pipeline destrói objetos, desabilita componentes ou chama `Destroy`. O que acontece com o personagem é do jogo, por quem escuta o evento (ou o relay) e a tag.

R19. **Morte e o Sample.** O **Sample Sandbox (B-032) pode ganhar a morte depois** deste item: um alvo que chega a vida zero, mostra o estado e reinicia. Essa evolução é ticket do Sample, não parte deste PRD (o Sample atual continua sem morte, CA11 dele). Este PRD só garante que o cenário C10 é possível.

### 4.6 Relay e eventos

R20. **Relay.** O `AbilitySystemEventRelay` (B-004) ganha, de forma **aditiva**, os pares "atributo chegou ao mínimo" e "atributo chegou ao máximo" (B-005), com um payload que inclui o instigador e o efeito (cenário C13). O B-004 não os entrega por não existirem na época (B-004, seção 6); este item os acrescenta, no `SpellBook.GAS.Bridges`.

R21. **Dano recebido com tipo.** O par "atributo diminuiu" do relay (B-004, R12) passa a poder incluir as tags de dano do último efeito no payload. O relay não interpreta nada: só repassa.

### 4.7 Editor, validação e documentação

R22. **Inspector** (UI Toolkit, ADR 0026, herdando do `SpellBookEditor`): o `AttributeDefinition` mostra a lista de processadores com resumo de uma linha, o campo de meta-atributo e o da tag de estado; o `GameplayEffect` mostra as tags de dano. Uma **prévia** mostra, para um dano de exemplo, o caminho "dano → processadores → alterações" (útil para ordenar a lista).

R23. **Validação** (`IValidatable`, 0026): processador nulo na lista; `AbsorbWithAttribute` sem atributo de escudo ou com escudo igual ao protegido; fração fora de 0 a 100 %; processador referindo atributo ausente do ASC; meta-atributo com processadores que redirecionam para si mesmo (ciclo); `StateTagWhileDepleted` com tag inválida; tag de dano inválida. Mensagens clicáveis.

R24. **Documentação.** Uma página **Dano, escudo e morte** descreve os processadores, a ordem, os três exemplos (escudo, resistência a fogo, tag de "morto") e o que o GAS **não** faz (destruir objetos). A página de Atributos e a de Efeitos citam a lista e as tags. Os tipos novos têm `[HelpURL]` (decisão I15).

R25. **CHANGELOG.** Cita: `AttributeChangeProcessor`, `AbsorbWithAttribute`, `ReduceByAttribute`, `ImmuneToTags`, meta-atributo, tags de dano no efeito, evento de morte com causador, `StateTagWhileDepleted`, os pares novos do relay.

## 5. Requisitos não funcionais

N1. **Alocação:** passar uma alteração por três processadores, o evento de morte e a tag de estado não alocam depois do aquecimento (C14). A alteração pendente é uma struct.
N2. **Módulos (ADR 0012, 0024):** processadores, meta-atributo, tags de dano e evento em `SpellBook.GAS`; o relay e seus pares em `SpellBook.GAS.Bridges`. **Nenhum processador depende de Teams, Cues, Events ou outro módulo** fora do GAS (isso vai em `Bridges`). O GAS não referencia Events, Pooling ou UI.
N3. **Sem conceito de gênero:** nenhum campo ou tipo se chama "Health", "Vida" ou "Morto" no pacote; são nomes de exemplo em testes e manual.
N4. **Determinismo:** testes com `ManualTimeSource` e factories (`GasFactory`), sem assets fixos.
N5. **Serialização:** `[SerializeReference]` para processadores; listas vazias por padrão; assets existentes abrem sem perda de dados e se comportam como antes.
N6. **Sem regressão:** a suíte do GAS (com B-002, B-005, B-007) passa sem edição de teste antigo.
N7. **Reentrada:** um ouvinte do evento de morte que aplica efeitos ou cura o próprio atributo não corrompe a iteração (a mesma regra do B-005 R9).

## 6. Fora do escopo

- **Já entregue pelo Lote A:** limitar o valor base entre mínimo e máximo (B-002); tipos de tag (B-006). A ADR 0024 (item 6) fica atendida por eles. Aqui só são usados.
- **B-005:** `OnDepleted`/`OnReachedMax`, limites dinâmicos, ordem de composição e `startingEffects`. Aqui eles ganham significado de dano, escudo e morte (R10 do B-005).
- **B-007:** `EffectContext`, valores do chamador, atributo do instigador. Os processadores consomem o contexto, sem reescrevê-lo.
- **B-003 e B-004:** lista e eventos de efeito; `TryApplyEffect` e o relay. Os pares novos do relay entram aqui (R20), sem mudar o resto do B-004.
- **Eventos de efeito do estilo Dota (`OnTakeDamage`, `OnDeath`) e `SendGameplayEvent`:** **B-012**, que os constrói **sobre** os eventos deste item (a morte e a variação do atributo).
- **Escudo com duração** (que expira sozinho): fora (P1).
- **Cálculo de execução** (`ExecutionCalculation`), crítico e esquiva: fora (P3). Podem ser feitos com fórmula, `Chance` (B-012) ou processador próprio.
- **Ganchos de pós-alteração:** fora; o `OnValueChanged` do atributo já cumpre (P9).
- **Time e relação** (aliado, inimigo, fogo amigo): B-011 e Bridges.
- **UI de barras, números flutuantes de dano e tipos de dano na UI:** B-015 a B-018.
- **Respawn, destruição, ragdoll, drop de itens:** do jogo.
- **Morte no Sample Sandbox:** ticket do Sample, depois deste item (R19).
- Rede e replicação.

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

**Processadores**
- CA1 (T). Cenário C4: sem processadores, as alterações dão os mesmos valores de hoje (teste de equivalência com os casos do B-002).
- CA2 (T). Cenário C9: a ordem da lista muda o resultado como esperado; a mesma lista dá sempre o mesmo resultado.
- CA3 (T). Um processador pode reduzir, aumentar, redirecionar e anular; variação final zero não dispara mudança de valor.
- CA4 (T). Processador próprio (subclasse) aparece no seletor e funciona.
- CA5 (T). Modificadores de efeito com duração não passam pelos processadores (R4).
- CA6 (T). Alteração por código sem efeito entrega contexto vazio e não lança exceção.

**Escudo e meta-atributo**
- CA7 (T). Cenários C1, C2 e C3: absorção total, parcial e quebra; um `OnDepleted` do escudo.
- CA8 (T). `AbsorbWithAttribute` absorve só as tags configuradas e na fração configurada.
- CA9 (T). Cenário C5: o meta-atributo é convertido em alterações reais e volta a zero; sem processadores, ele não afeta ninguém (aviso do validador).
- CA10 (T). Cenário C12: após chegar ao mínimo, a cura sobe a vida (a base não fica negativa).

**Tipos de dano**
- CA11 (T). Cenário C6: resistência por tag reduz o dano do tipo certo e só dele.
- CA12 (T). Cenário C7: tags múltiplas e casamento hierárquico (resistência a `Dano` casa com `Dano.Fogo`).
- CA13 (T). Cenário C8: `ImmuneToTags` anula só o dano; o efeito continua sendo aplicado (outras partes valem).
- CA14 (T). Tag de dano inválida no efeito não lança exceção e o validador a aponta.

**Morte**
- CA15 (T). Cenário C10: `OnDepleted` dispara na transição com o instigador e o efeito; não repete no mínimo; dispara de novo após curar e depletar.
- CA16 (T). Instigador destruído: o evento dispara com a referência nula, sem exceção.
- CA17 (T). Cenário C11: a tag de estado é concedida ao chegar ao mínimo e devolvida ao sair; com o campo vazio, nada é concedido (padrão desligado).
- CA18 (T). Habilidade com a tag de estado em `BlockedTags` falha na checagem de tags quando o atributo está no mínimo.
- CA19 (E). Revisão estrutural: nenhum tipo do pipeline chama `Destroy`, desabilita componente ou usa nome de gênero (N3, R18).
- CA20 (T). Um ouvinte do evento de morte que cura o atributo não lança exceção nem pula eventos.

**Relay e módulos**
- CA21 (T). Cenário C13 em teste: o relay dispara o canal ao chegar ao mínimo e ao máximo, com o payload do instigador e do efeito; desligar o relay desinscreve (continuidade do B-004 C9).
- CA22 (T). O par "atributo diminuiu" inclui as tags de dano do efeito (R21).
- CA23 (T). O teste de assemblies da 0-M continua passando: processadores e evento no GAS; o relay no `GAS.Bridges`; o GAS sem Events.

**Desempenho, editor e documentação**
- CA24 (T). Cenário C14: 10 000 alterações com 3 processadores, e 1000 eventos de morte com tag de estado, alocam 0 bytes gerenciados depois do aquecimento.
- CA25 (S). No host, o Inspector mostra a lista de processadores com a prévia "dano → processadores → alterações", o campo do meta-atributo, a tag de estado e as tags de dano do efeito.
- CA26 (T). O validador cobre os casos do R23, com o objeto clicável.
- CA27 (E). O CHANGELOG cita os itens do R25; a página nova existe com os três exemplos; os tipos novos têm `[HelpURL]`.
- CA28 (T). A suíte do GAS existente passa sem edição (N6).
- CA29 (E). O PRD ou a página do Sample registra que o Sample pode ganhar a morte depois (R19).

## 8. Perguntas em aberto

### Com o usuário

- **P1. Escudo que expira.** *Respondida (usuário, 2026-10-31): aceita. O escudo com duração própria fica **fora**; o escudo é um atributo consumido (sobe por efeito, é gasto pelo dano e não expira sozinho). O "escudo de 5 segundos" fica para um item futuro.*
- **P2. Tag de estado na morte.** *Respondida (usuário, 2026-10-31): aceita. Campo **opcional** por atributo (`StateTagWhileDepleted`), **desligado por padrão**.*
- **P3. Cálculo de execução.** *Respondida (usuário, 2026-10-31): aceita. **Sem** tipo próprio de "cálculo de execução" (nem crítico); o designer compõe com atributo do instigador (B-007), `Chance` (B-012) ou processador próprio.*
- **P4. Resistência e imunidade prontas.** *Respondida (usuário, 2026-10-31): aceita. Entram `ReduceByAttribute` e `ImmuneToTags`, além do `AbsorbWithAttribute`.*

### Com o Arquiteto (decisão técnica da Spec)

- **P5. Onde moram os processadores** (ADR 0024, "em aberto"): na definição do atributo, no `AttributeSet` ou nos dois; e a ordem entre fontes diferentes.
- **P6. Meta-atributo:** atributo comum marcado de algum jeito ou tipo especial que nunca guarda valor.
- **P7. Escopo das alterações:** confirmar que só o valor base (instantâneo e tick) passa pelos processadores (R4), e como isso convive com a ordem de composição do B-005.
- **P8. Instigador destruído:** referência nula ou um identificador que sobrevive (ligado à P8 do B-007).
- **P9. Pós-alteração:** confirmar que não há gancho de pós-alteração além do `OnValueChanged` (ADR 0024, "em aberto").
- **P10. Resultado do dano:** a UI de números flutuantes precisa do "dano pedido, absorvido e aplicado" de uma alteração (hoje só vê a variação final do atributo). O Arquiteto avalia se um **resultado da alteração** (pedido, absorvido, aplicado, matou) deve existir como payload do evento ou se a variação nos atributos basta.
- **P11. Transição do `OnDepleted` com processadores:** usa o valor base final (depois dos processadores) e o valor atual; ligado à P7 do B-005.
- **P12. Ordem de disparo** quando a mesma alteração quebra o escudo e mata a vida no mesmo instante (eventos de escudo e de vida).

## 9. Ajustes de playtest

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | PRD escrito pelo Criativo a partir do B-010, do plano (item 7 e Fase 6) e da ADR 0024; o Controlador pediu o PRD inteiro e a Spec divide em tickets | Todo o documento | Rascunho |
| 2026-10-31 | Dependências registradas sem puxar escopo: B-005 (`OnDepleted`), B-007 (`EffectContext`), B-002 (base limitada), B-003, B-004; B-012 constrói `OnTakeDamage`/`OnDeath` sobre este item | Seção 6 | Aplicado |
| 2026-10-31 | O evento de morte que o Sample (B-032) cortou entra aqui; o Sample pode ganhar a morte depois, em ticket próprio | R19, CA29 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P1: escudo com duração própria fica fora | R11, seção 6 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P2: tag de estado na morte opcional, desligada por padrão | R17, CA17 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P3: sem tipo próprio de execução (nem crítico) | R8, seção 6 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P4: entregar `ReduceByAttribute` e `ImmuneToTags` | R13, CA11 a CA13 | Aplicado |
