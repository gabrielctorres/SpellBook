# PRD: Resultado de aplicação e relay de eventos do GAS
Backlog: B-004 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção C (itens 9 e 10), seção H.3 (Ponte do GAS) e decisões I22, I23, I39 e I40. ADR [0016](../adr/0016-sistema-de-eventos-unificado.md).
Depende do Lote A (**B-002**: remoção idempotente; **B-006**: `GameplayTag`/`GameplayTagContainer`) e, para os eventos de efeito, de **B-003** (`OnEffectApplied`, `OnEffectRemoved`, `OnEffectStacksChanged` e a instância com dados públicos). Este PRD usa o que eles entregam e não reescreve o escopo deles (ver seção 6).
Este PRD não tem foco em nenhum jogo. As menções a jogos no plano são histórico.

---

## 1. Problema (contexto)

O GAS fala por eventos C# do ASC, e o módulo Events fala por canais em asset. Falta a ponte entre os dois, e falta o motivo quando uma aplicação de efeito não acontece:

- **`ApplyEffect` devolve `null` em dois casos opostos.** Devolve `null` quando aplica um efeito instantâneo (que não cria instância) e quando bloqueia por imunidade ou requisito. Quem chama não distingue "deu certo" de "foi barrado". Para mostrar "Imune!" na tela, o desenvolvedor checa a imunidade por conta própria antes de aplicar.
- **Não há evento de bloqueio.** Nem o ASC nem a UI sabem que um efeito foi barrado, e por quê.
- **O designer não consegue ligar o GAS a nada sem código.** Para tocar um som quando a vida chega a zero, ou acender um ícone quando o personagem é atordoado, alguém precisa escrever um script que escute o ASC e dispare o canal. A decisão I23 aceitou essa lacuna até este item.
- **A `RaiseEventAction` não leva dados do GAS.** Pela decisão I39 ela dispara canais sem payload ou com payload fixo digitado no Inspector. Não há como mandar "o dano que acabou de acontecer" ou "o efeito que acabou de entrar".
- **Um efeito não consegue disparar um canal.** Existe a ação de habilidade, mas não a ação equivalente para as ações de um efeito (aplicado, tick, removido).
- **O Sample Sandbox (B-032) contornou a falta do relay.** Ele publica o dano recebido por um script de apoio que escuta `AttributeState.OnValueChanged`, porque o relay do ASC não existia na hora da Spec (ver o Q2 do PRD do Sample).

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue:

1. aplicar um efeito e saber exatamente o que aconteceu (aplicado, empilhado, instantâneo aplicado, bloqueado por imunidade, bloqueado por requisito, inválido), sem checar nada antes;
2. escutar `OnEffectBlocked` no ASC, com o efeito, a origem e o motivo;
3. ligar um ator ao módulo Events **sem escrever código**: um componente opcional (o relay) lê os eventos C# do ASC daquele ator e dispara canais em asset e `UnityEvent`s locais;
4. disparar um canal com **dados do GAS** (valor, atributo, efeito, tag) a partir de uma ação de habilidade e de uma ação de efeito;
5. fazer tudo isso sem que o GAS passe a depender do módulo Events: a ponte vive em `SpellBook.GAS.Bridges`.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Desenvolvedor de gameplay** | `TryApplyEffect` com resultado claro, `OnEffectBlocked` no ASC, nada de checagem prévia de imunidade |
| **Designer técnico** | Arrastar o relay num personagem, escolher canais e reagir no Inspector (som, VFX, UI), sem script |
| **Desenvolvedor de UI** | Fonte de dados continua sendo o ASC (ADR 0017); o relay serve a reações de designer, não a dados de ator |
| **Mantenedor do pacote** | Uma única ponte GAS→Events, opcional, por ator, sem acoplar os módulos |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, resultado:** aplicar veneno num alvo normal dá `Applied`; reaplicar com empilhamento dá `Stacked`; aplicar um dano instantâneo dá `AppliedInstant`; aplicar veneno num alvo imune a `Status.Veneno` dá `BlockedByImmunity`; aplicar um efeito cujo alvo não tem a tag exigida dá `MissingRequirements`; aplicar um efeito nulo dá `Invalid`.
- **C2, instância:** `TryApplyEffect` devolve a instância em `Applied` e `Stacked`; em `AppliedInstant` e nos bloqueios, a instância é nula, e o resultado diz por quê.
- **C3, compatibilidade:** `ApplyEffect` continua com a mesma assinatura e o mesmo valor de retorno de antes.
- **C4, evento de bloqueio:** o ASC dispara `OnEffectBlocked(efeito, origem, resultado)` uma vez por aplicação barrada.
- **C5, relay de bloqueio:** um personagem com o relay; no Inspector, `OnEffectBlocked` aponta para um canal `GameObject`/`String` e, em outro objeto, um `EventListener` toca um som. Aplicar veneno num alvo imune toca o som.
- **C6, relay de atributo:** o relay dispara um canal `Float` com o novo valor quando a vida muda.
- **C7, dano recebido (Sample):** o relay dispara um canal com o valor do dano recebido (a variação negativa do atributo), de modo que o Sample Sandbox possa trocar o script de apoio por ele.
- **C8, ação de efeito:** um efeito de veneno com `RaiseEventEffectAction` no tick dispara um canal `Float` com o dano do tick.
- **C9, desligar:** desabilitar ou destruir o relay desinscreve de todos os eventos do ASC (sem vazamento, sem exceção ao destruir antes do ASC).
- **C10, sem relay:** um ator sem o relay não paga custo algum; o GAS compila e roda sem o módulo Events (teste de assemblies).

## 4. Requisitos funcionais

### 4.1 Resultado de aplicação

R1. **`EffectApplicationResult`** é um enum do `SpellBook.GAS` com: `Applied` (instância criada), `Stacked` (empilhou numa instância existente), `AppliedInstant` (efeito instantâneo aplicado), `BlockedByImmunity`, `MissingRequirements` e `Invalid` (efeito nulo ou sem alvo válido). Novos valores entram **no fim** do enum (serializado como inteiro).

R2. **`TryApplyEffect`.** O ASC oferece `TryApplyEffect(efeito, origem, out instância)` que devolve o `EffectApplicationResult`. A instância é preenchida só em `Applied` e `Stacked`. O método **não lança exceção** para efeito nulo (devolve `Invalid`). Os mesmos requisitos e imunidades que já bloqueiam `ApplyEffect` bloqueiam o `TryApplyEffect`: não há regra nova de bloqueio.

R3. **`ApplyEffect` não muda.** Mesma assinatura, mesmo retorno e mesmo comportamento observável. Ele passa a ser um invólucro fino do `TryApplyEffect`. (Pela decisão I1 não há fachada, mas aqui não há quebra: é API nova ao lado de uma que fica.)

R4. **Resultado coerente com o B-002 e o B-003.** Reaplicar sem `CanStack` **renova** a instância (B-002, R6a): o resultado é `Stacked`? ou `Applied`? Decisão do Arquiteto (P3). O resultado nunca é `Applied` para um efeito instantâneo.

R5. **Sem alocação.** `TryApplyEffect` não aloca além do que `ApplyEffect` já alocava (a própria instância, quando criada).

### 4.2 Evento de bloqueio no ASC

R6. **`OnEffectBlocked`** é um evento C# do ASC, `OnEffectBlocked(GameplayEffect efeito, AbilitySystemComponent origem, EffectApplicationResult motivo)`, disparado quando `TryApplyEffect` (e, por consequência, `ApplyEffect`) devolve `BlockedByImmunity` ou `MissingRequirements`. Não dispara em `Invalid`.

R7. **Bloqueios que não são efeitos.** O ASC oferece `ReportBlocked(GameplayTag motivo)` para um jogo ou habilidade avisar uma barreira que não passa por efeito (por exemplo, "empurrão ignorado por estar enraizado"). Ele dispara um evento C# `OnBlocked(GameplayTag motivo, AbilitySystemComponent origem)` do ASC. Tag inválida é ignorada. (Origem: item 10 do plano.)

### 4.3 Relay de eventos (`SpellBook.GAS.Bridges`)

R8. **`AbilitySystemEventRelay`** é um `MonoBehaviour` opcional, **por ator**, que se inscreve nos eventos C# do ASC do mesmo ator (ou do ASC escolhido no Inspector) e os repassa para **canais em asset** (módulo Events) e para **`UnityEvent`s locais**. É a ponte prevista na ADR 0016 (item 6). Ele **não** substitui os eventos C# e **não** é lido pela UI de dados de ator (ADR 0017).

R9. **Eventos repassados.** O relay cobre os eventos que o GAS já expõe e os que o B-003 e este item criam:
- de efeito: aplicado, removido (com o motivo), pilhas mudaram (B-003), bloqueado (R6);
- de habilidade: ativada, terminada, falhou (com o resultado), concedida e retirada (B-003);
- de atributo: valor mudou (atributo, valor antigo, novo valor);
- de tag: tag entrou ou saiu do ASC (tag, contagem);
- de bloqueio genérico (R7).
Cada um é um **par opcional** "canal + `UnityEvent`" no Inspector: o designer liga só o que precisa. Evento sem canal e sem ouvinte não custa nada além da inscrição.

R10. **Payloads.** Os dados saem em structs do GAS que vivem no `GAS.Bridges`: `AbilityEventData`, `EffectEventData`, `AttributeChangedData` e `TagChangedData` (nomes do plano, H.3). Cada struct carrega o que o evento sabe (por exemplo, `EffectEventData`: efeito, origem, alvo, pilhas, motivo). **Os canais tipados desses payloads** (`EventChannel<AbilityEventData>` e os demais, mais as respostas `EventResponse<T>` correspondentes) são entregues aqui, em `GAS.Bridges`, e aparecem no seletor do `EventListener` filtrado por tipo (ADR 0016).

R11. **Canais com valor simples.** Para o caso comum "só o número", o relay também pode disparar os canais prontos do módulo Events (`Float`, `String`, `GameObject`): o valor novo de um atributo, o nome do efeito, o dono. A escolha do canal vale por par.

R12. **Dano recebido** (cenário C7). O relay oferece o par "atributo diminuiu" com um filtro opcional por atributo, que dispara `AttributeChangedData` apenas quando o valor **cai**, com a variação (negativa) no payload. Isso permite ao Sample (B-032) e a qualquer jogo mostrar "dano recebido" sem script. Dano com tipo, crítico e meta-atributo ficam no B-010.

R13. **Ciclo de vida.** O relay se inscreve em `OnEnable` e se desinscreve em `OnDisable` e `OnDestroy`. Destruir o ASC antes ou depois do relay não lança exceção. Trocar o ASC no Inspector em Play não vaza inscrição. O relay não guarda estado de gameplay: só repassa.

R14. **Reentrada.** Um ouvinte do canal que aplica um efeito no mesmo ator, dentro do callback, não quebra a iteração do relay nem a do ASC (a segurança de reentrada dos canais é da ADR 0016; o relay não pode ser o elo frágil).

### 4.4 Ações com dados do GAS

R15. **`RaiseEventEffectAction`** (em `GAS.Bridges`) é a ação, dentro das listas de ações de um efeito (aplicado, tick, removido), que dispara um canal. Aceita a mesma `EventEmission` da `RaiseEventAction` (ADR 0016, I50): canal sem payload ou com payload fixo. Está disponível no seletor de tipo das ações de efeito.

R16. **Payload dinâmico.** A `RaiseEventAction` (de habilidade) e a `RaiseEventEffectAction` ganham a opção de **payload vindo do GAS**, escolhida no Inspector por canal de payload compatível: para `Float`, o valor do atributo escolhido, o dano do tick ou a magnitude do efeito; para `String`, o nome do efeito ou da habilidade; para `GameObject`, o dono ou o alvo; para os tipos do R10, o struct preenchido com os dados do evento. É o que a decisão I39 deixou para este item. O que não for suportado para o tipo do canal é impedido pelo Inspector (campo escondido) e acusado pelo validador.

R17. **Sem lógica na ação.** A ação só lê o valor e dispara o canal. Estado fica no contexto de execução, nunca na ação (ADR 0013, 0018).

### 4.5 Editor, validação e documentação

R18. **Inspector.** O relay tem Inspector em UI Toolkit, no `SpellBook.GAS.Bridges.Editor`, herdando do `SpellBookEditor` (ADR 0026): lista os pares "evento, canal, UnityEvent" agrupados por categoria (efeito, habilidade, atributo, tag), recolhidos, e mostra em Play os últimos disparos recebidos (limitado). O seletor de canal filtra pelo tipo do payload do evento.

R19. **Validação** (`IValidatable`, 0026): relay sem ASC encontrável (no ator ou no campo), par com canal de tipo incompatível com o payload, `UnityEvent` sem alvo, e payload dinâmico escolhido para um canal de tipo que a ação não sabe preencher. Mensagens clicáveis no topo do Inspector e na janela do validador.

R20. **Documentação.** A página do manual de Eventos ganha a seção "Ligar o GAS ao Events" (como adicionar o relay, passo a passo com o exemplo "som ao ser imune"), e a página de Efeitos descreve `TryApplyEffect` e `OnEffectBlocked`. Os tipos novos têm `[HelpURL]` (decisão I15). A página dos Samples registra que o Sample pode migrar para o relay (ver seção 6).

R21. **CHANGELOG.** Cita: `TryApplyEffect`, `EffectApplicationResult`, `OnEffectBlocked`, `ReportBlocked`, `AbilitySystemEventRelay`, `RaiseEventEffectAction` e payload dinâmico nas ações de evento.

## 5. Requisitos não funcionais

N1. **Módulos (ADR 0012, 0016):** `EffectApplicationResult`, `TryApplyEffect`, `OnEffectBlocked` e `OnBlocked` ficam em `SpellBook.GAS`. O relay, os payloads, os canais tipados e as duas ações ficam em `SpellBook.GAS.Bridges`, que referencia GAS e Events. **O GAS não referencia Events.** Teste de assemblies obrigatório.
N2. **Alocação:** com o relay desligado ou sem ouvintes, nenhum evento do ASC aloca. Disparar canal com payload struct não aloca depois do aquecimento. O `UnityEvent` pode alocar na primeira chamada (limite conhecido, ADR 0016).
N3. **Determinismo:** testes usam `ManualTimeSource`, factories (`GasFactory`, `EventsFactory`) e nenhum asset fixo.
N4. **Sem regressão:** a suíte do GAS e do Events existentes passa sem edição; `ApplyEffect` mantém o comportamento (C3).
N5. **Serialização:** usa serialização nativa da Unity (`[SerializeReference]` para as ações). Assets existentes abrem sem perda; `RaiseEventAction` e `EventEmission` existentes continuam válidas.
N6. **Fora de Play e em build:** o relay funciona em build de player; o painel de últimos disparos do Inspector é só Editor.

## 6. Fora do escopo

- **Já entregue pelo Lote A:** remoção idempotente (B-002, R5) e as tags (B-006). Este PRD só os usa.
- **Eventos C# `OnEffectApplied`/`OnEffectRemoved`/`OnEffectStacksChanged`, `OnAbilityGranted/Removed`, lista pública de efeitos:** **B-003**. O relay **repassa** esses eventos; se o B-003 ainda não existir, os pares de efeito aplicado, removido, pilhas e habilidade concedida/retirada ficam para quando existir. Só `OnEffectBlocked`, `OnBlocked`, atributo e tag são entregues sem depender do B-003 (ver P1).
- **Migração do Sample Sandbox (B-032) para o relay.** O Sample foi escrito sem o relay porque ele não existia (Q2 do PRD do Sample: payload fixo pela `RaiseEventAction`, ou script de apoio que escuta `AttributeState.OnValueChanged`). Quando este item estiver pronto, **o Sample pode migrar para o relay** e o script de apoio deixa de ser necessário. Essa migração é um ticket do Sample, não parte deste PRD. Este PRD só garante que o cenário C7 é possível.
- Dano com tipos por tag, crítico, meta-atributo de dano, absorção e morte (`OnDepleted`): **B-010**. O relay repassa "atributo diminuiu"; ele não interpreta isso como dano.
- `OnDepleted`/`OnReachedMax` e metadados `IsBuff` etc.: **B-005**. Quando existirem, o relay ganha o par correspondente (aditivo, fora deste item).
- `SendGameplayEvent`, gatilhos de habilidade por evento e eventos de efeito do estilo Dota (OnTakeDamage, OnDeath): **B-012**. Eles são semântica do GAS, não do barramento (ADR 0016).
- Barramento global de código com os eventos do GAS (`EventBus<T>`): o relay é só por ator, pela decisão da ADR 0016. Não há um relay global.
- Widgets e fontes de UI: **B-015 a B-018**; a UI lê o ASC pela fonte, nunca por relay (ADR 0017).
- Wizard "Create Event Type": B-019.
- Replicação e predição em rede.

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host ou num Sample.

**Resultado de aplicação**
- CA1 (T). Cenário C1: os seis resultados, um teste por valor do enum.
- CA2 (T). Cenário C2: a instância vem preenchida em `Applied` e `Stacked`, e nula nos demais.
- CA3 (T). `TryApplyEffect(null, …)` devolve `Invalid` sem exceção.
- CA4 (T). Cenário C3: `ApplyEffect` devolve exatamente o mesmo valor que antes em cada um dos cenários do CA1 (nulo para instantâneo e bloqueio, instância para duração).
- CA5 (T). `AppliedInstant` nunca acompanha instância; um efeito de duração nunca devolve `AppliedInstant`.
- CA6 (T). Aplicar duas vezes o mesmo efeito sem `CanStack` devolve o resultado definido pela Spec (P3) e deixa uma única instância (continuidade do B-002, CA25).
- CA7 (T). Os valores do enum seguem a ordem do R1 (teste de ordem, valores só no fim).
- CA8 (T). 10 000 chamadas de `TryApplyEffect` em efeito instantâneo alocam 0 bytes gerenciados depois do aquecimento.

**Evento de bloqueio**
- CA9 (T). Cenário C4: `OnEffectBlocked` dispara uma vez por imunidade e uma vez por requisito, com efeito, origem e motivo corretos; não dispara em `Applied`, `Stacked`, `AppliedInstant` nem `Invalid`.
- CA10 (T). `ReportBlocked` dispara `OnBlocked` com a tag e a origem; tag inválida é ignorada, sem exceção.

**Relay**
- CA11 (T). Cenário C5: com o relay, o bloqueio por imunidade dispara o canal configurado; um `EventListener` ligado ao canal executa sua reação.
- CA12 (T). O par `UnityEvent` do relay dispara sem canal configurado (e o canal sem `UnityEvent`).
- CA13 (T). Cenário C6: o relay dispara o canal `Float` com o novo valor do atributo.
- CA14 (T). Cenário C7: o par "atributo diminuiu" dispara só quando o valor cai e carrega a variação negativa; aumento não dispara; o filtro por atributo ignora os outros atributos.
- CA15 (T). Eventos de tag: entrar e sair do ASC disparam com a tag e a contagem.
- CA16 (T). Eventos de habilidade: ativada, terminada e falhou (com o resultado) disparam uma vez cada.
- CA17 (T). Cenário C9: desabilitar e destruir o relay desinscreve de todos os eventos; o ASC aplicando efeitos depois não chama nenhum ouvinte do relay; destruir o ASC antes do relay não lança exceção.
- CA18 (T). Reentrada: um ouvinte que aplica um efeito no mesmo ator dentro do callback não lança exceção nem repete o disparo.
- CA19 (T). Eventos de efeito aplicado, removido (com o motivo), pilhas mudaram e habilidade concedida ou retirada são repassados. O B-004 só começa depois do B-003 (P1, respondida), então estes testes não ficam pendentes.
- CA20 (T). Os tipos de payload (`AbilityEventData`, `EffectEventData`, `AttributeChangedData`, `TagChangedData`) e seus canais tipados aparecem criáveis no menu `Create` e no seletor do `EventListener`, filtrados por tipo.

**Ações**
- CA21 (T). `RaiseEventEffectAction` no tick de um efeito dispara o canal uma vez por tick (cenário C8).
- CA22 (T). Payload dinâmico: um canal `Float` recebe o dano do tick; um canal `String` recebe o nome do efeito; um canal `GameObject` recebe o dono.
- CA23 (T). Um canal de tipo não suportado pela ação para o payload dinâmico é apontado pelo validador, e a ação com payload fixo (comportamento da 0-M) continua funcionando.

**Módulos e editor**
- CA24 (T). Cenário C10: o teste de assemblies da 0-M continua passando; o GAS não referencia o Events; o `GAS.Bridges` referencia ambos.
- CA25 (S). No host, o relay aparece no Inspector com os pares agrupados e mostra os últimos disparos em Play; o seletor de canal só lista canais do tipo compatível.
- CA26 (T). O validador aponta: relay sem ASC, canal de tipo incompatível, `UnityEvent` sem alvo.
- CA27 (S). Num projeto de teste, um designer configura "som ao ser imune" só com o Inspector (relay no personagem, canal em asset, `EventListener` com reação) seguindo a página do manual, sem escrever código.

**Documentação e compatibilidade**
- CA28 (E). O CHANGELOG cita os itens do R21; a página de Eventos tem a seção do R20 com o exemplo do R20; os tipos novos têm `[HelpURL]`.
- CA29 (E). A página dos Samples ou o PRD do Sample registra que o Sample pode migrar para o relay.
- CA30 (T). A suíte do GAS e do Events existentes passa sem edição de teste antigo (N4).

## 8. Perguntas em aberto

### Com o usuário

- **P1. Dependência do B-003 e ordem.** *Respondida (usuário, 2026-10-31): o relay espera o B-003 e sai completo, com todos os pares do R9 (inclusive efeito aplicado/removido/pilhas e habilidade concedida/retirada). Não há entrega em duas partes.*
- **P2. Relay global opcional.** *Respondida (usuário, 2026-10-31): só relay **por ator**, sem relay global (regra da ADR 0016). Quem precisar de "qualquer personagem foi imune" liga o relay de cada ator ao mesmo canal.*
- PRD inteiro: a Spec divide em tickets (resultado de aplicação e `OnEffectBlocked`, relay e payloads, ações com payload dinâmico).

### Com o Arquiteto (decisão técnica da Spec)

- **P3. Resultado ao reaplicar sem `CanStack`** (B-002, R6a): renova a instância. O resultado é `Stacked`, `Applied` ou um valor novo (`Refreshed`)? A proposta do Criativo é `Stacked` só quando as pilhas aumentam e um valor novo `Refreshed` (no fim do enum) para a renovação, para a UI poder diferenciar. A Spec decide.
- **P4. Onde o relay lê o "valor antigo"** de um atributo para calcular a variação do R12 (o `AttributeState.OnValueChanged` já entrega antigo e novo?).
- **P5. Payload dinâmico das ações** (R16): quais tipos de canal a ação sabe preencher e como o Inspector oferece isso (a `EventEmission` precisa de um modo "fonte do valor").
- **P6. Estrutura dos pares** do relay (R9): uma lista de entradas com tipo de evento, ou campos fixos agrupados; efeito no custo de memória do componente e na serialização.
- **P7. Reentrada e ordem:** a ordem entre os eventos C# do ASC e o disparo do relay no mesmo quadro (o relay dispara dentro do evento C#, ou enfileira para o fim do `Tick`?).

## 9. Diário de decisões e ajustes

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Origem | Decisão ou observação | Efeito neste PRD | Status |
|---|---|---|---|---|
| 2026-10-31 | Controlador | PRD escrito para o Lote B do backlog, a partir do texto de B-004, do plano e das ADRs 0016 e 0012 | Todo o documento | Rascunho |
| 2026-10-31 | Controlador | O B-004 é o relay que o Sample Sandbox (B-032) não usou por não existir. Registrado: o Sample pode migrar para ele depois, em ticket do Sample | Seção 1, seção 6, R12, CA14, CA29 | Aplicado |
| 2026-10-31 | Controlador | Dependências registradas, sem puxar escopo: B-003 (eventos de efeito e de habilidade), B-002 e B-006 (Lote A), B-010 (dano) | Seção 6, P1 | Aplicado |
| 2026-10-29 | usuário | I23: os eventos C# do ASC continuam; o relay vem neste item | R8 | Aplicado |
| 2026-10-29 | usuário | I22, I39 e I40: `RaiseEventAction` na 0-M, payload fixo; payload dinâmico fica neste item | R15, R16 | Aplicado |
| 2026-10-31 | usuário (via Controlador) | P1: o relay espera o B-003 e sai completo (sem entrega em duas partes) | Seção 6, R9, CA19, seção 8 (P1) | Aplicado |
| 2026-10-31 | usuário (via Controlador) | P2: relay só por ator, sem relay global | R8, seção 6, seção 8 (P2) | Aplicado |
| 2026-10-31 | usuário (via Controlador) | PRD inteiro; a Spec divide em tickets | Seção 8 | Aplicado |
| 2026-11-01 | usuário (gate parcial, via Controlador) | O lado do efeito do R15 (`RaiseEventEffectAction`, CA21 e CA22) sai do RL6 e passa a ser entregue pelo EV15 do B-012 (`Documentation~/spec/gas-evento-acoes-alvos.md`), com efeito, nível e evento vindos do `ActionContext` (ADR 0034; emenda da ADR 0031). O RL6 fica só com o payload dinâmico do lado da habilidade e o CA23. O texto original do R15, do CA21 e do CA22 foi mantido, sem reescrita | R15 (lado do efeito), CA21, CA22, CA23, RL6 | Aplicado |
