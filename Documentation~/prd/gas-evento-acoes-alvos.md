# PRD: Modelo evento → ações → alvos
Backlog: B-012 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção B ("Grafo evento → ações → alvos"), seção E (Fase 7) e decisões I.8 e I.12. ADRs [0018](../adr/0018-hierarquia-unica-de-acoes.md) (hierarquia única de ações), [0019](../adr/0019-hierarquia-unica-de-condition.md) (condições), [0022](../adr/0022-ordem-das-checagens-de-ativacao.md) (ordem das checagens e alvo da ativação), [0024](../adr/0024-pipeline-generico-de-dano.md) (dano), [0014](../adr/0014-tempo-injetavel-itimesource.md) (tempo) e [0016](../adr/0016-sistema-de-eventos-unificado.md) (eventos).
Depende de: **B-007** (`EffectContext`, `EffectSpec`, valores por nível), **B-009** (hierarquia `Condition`, `ActivationFailure`, `CastRangeCondition` por ponto), **B-010** (evento de morte e variação de atributo, base de `OnTakeDamage`/`OnDeath`), **B-003** (lista e eventos de efeito, `OnAbilityGranted`) e **B-004** (relay e `RaiseEventEffectAction`, que passam para a nova base). **B-011** (Teams) entra só nos filtros de alvo por relação, em `GAS.Bridges`. Este PRD usa o que eles entregam e não reescreve o escopo deles (seção 6).
Este PRD não tem foco em nenhum jogo. As menções a jogos no plano são histórico.

---

## 1. Problema (contexto)

O pacote tem duas famílias de ação que não se falam, e um único lugar para dizer "em quem":

- **Duas hierarquias incompatíveis.** `AbilityAction` (corrotina, com contexto de habilidade) roda na habilidade e no impacto do projétil. `EffectAction` (síncrona) roda no efeito (aplicado, tick, removido). Uma ação de uma não existe na outra: um efeito não consegue "esperar 2 s e aplicar outro efeito", e uma habilidade não usa as ações de efeito.
- **Alvo único.** Toda ação usa a mesma lista `context.Targets`. "Dano no alvo e cura em quem lançou" na mesma lista exige duas habilidades ou código.
- **Faltam ações de fluxo e de efeito.** Não há `Chance` (probabilidade), `Delayed` (atraso sem bloquear a sequência), `ForEachTarget` (repetir por alvo) nem `RemoveEffect` (ação que remove um efeito).
- **Não há eventos de gameplay.** Nada equivale a `SendGameplayEvent`, nem a gatilhos ("ativa esta habilidade quando receber dano"), nem a `OnTakeDamage`, `OnDeath` e `OnAbilityExecuted` de efeito.
- **Poucos comportamentos de habilidade.** Faltam **Passive** (liga ao ser concedida), **Toggle** (liga e desliga) e **Channel** (dura enquanto canaliza), além do alcance de cast e da ativação com **unidade-alvo**: `TryActivateAbility` só recebe um ponto.
- **Não há autocast.** "Esta habilidade se lança sozinha quando pronta, e o jogador liga e desliga isso" exige, hoje, um laço de script fora do pacote que repete a checagem de ativação, e que fica fora de sincronia com a ordem de checagens e com o motivo de falha.
- **O commit é implícito.** O custo e a recarga saem **antes** de qualquer ação, e não há como pagar depois de um "cast point" nem desistir sem custo.
- **Esperas inconsistentes (B-030).** O `RepeatAction.Interval` não conta um `Tick` que acontece no mesmo quadro do fim da repetição, mas o `SpawnProjectileAction.DelayBetweenShots` conta. O atraso vem do encadeamento de corrotinas e vale entre quaisquer duas ações de uma sequência.
- **Inspector com listas planas.** A habilidade tem uma lista só de ações, sem abas por evento, sem resumo e sem seletor de alvo inline.

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, pelo Inspector e sem código:

1. montar habilidades, efeitos e impactos de projétil com **uma só família de ação**, em **eventos nomeados** do dono (começou, terminou, impacto, aplicado, tick, removido);
2. escolher, **por ação**, em quem ela age (`TargetSelector`: quem lançou, alvos do evento, ponto, área com filtros por tag, máximo e aleatório; relação de time via Bridges);
3. usar as ações **Chance**, **Delayed**, **ForEachTarget** e **RemoveEffect**, e condições da hierarquia do B-009 em ações condicionais;
4. enviar e escutar **eventos de gameplay** (`SendGameplayEvent`), disparar habilidades por gatilho e reagir a `OnTakeDamage`, `OnDeath` e `OnAbilityExecuted` nos efeitos;
5. criar habilidades **Passive**, **Toggle** e **Channel**;
6. ativar com **unidade-alvo** e **alcance de cast**, com o **commit explícito** (pagar custo e recarga em um ponto escolhido);
7. contar com esperas **corretas e testáveis**: uma espera conta o tempo do ASC a partir do fim do passo anterior, inclusive um `Tick` no mesmo quadro (B-030);
8. editar tudo isso num Inspector com **abas por evento**, resumo de uma linha por ação e seletor de alvo inline, como descreve o [DS do Inspector de ações](../ds/inspector-de-acoes.md);
9. dar a uma habilidade o **autocast**: um interruptor que o jogador liga e desliga e que faz a habilidade tentar se ativar sozinha, pelo mesmo caminho e pela mesma ordem de checagens da ativação manual.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Designer técnico** | Habilidades e efeitos completos só no Inspector: ações por evento, alvo por ação, gatilhos, passiva, toggle e canalização |
| **Desenvolvedor de gameplay** | Ações próprias como subclasse única; contexto de execução com tudo que precisa; evento de gameplay enviado por código; esperas testáveis com `ManualTimeSource` |
| **Desenvolvedor de UI** (B-016) | Estados de toggle, canalização e motivo de falha por leitura e evento, sem script |
| **Mantenedor do pacote** | Um vocabulário só (habilidade, efeito, projétil, thinker), base para a Fase 8 (thinkers, auras, cues e projéteis) |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, uma hierarquia:** a mesma ação `ApplyEffect` roda na lista "ao ativar" de uma habilidade e na lista "ao ser aplicado" de um efeito; não existe mais `AbilityAction` nem `EffectAction`.
- **C2, alvo por ação:** na mesma lista, `ApplyEffect(Dano)` com seletor "alvos do evento" e `ApplyEffect(Cura)` com seletor "quem lançou" agem em ASCs diferentes.
- **C3, sem seletor:** uma ação sem seletor age no alvo padrão do evento (alvo da habilidade, alvo do impacto, dono do efeito).
- **C4, área com filtros:** seletor de área (raio 5) com `RequiredTags` e `maxTargets = 2` devolve no máximo 2 alvos que têm a tag, sem o dono (se "incluir dono" estiver desligado).
- **C5, aleatório:** seletor com "sorteio de N" devolve N alvos distintos; com o mesmo seed devolve os mesmos (a fonte de aleatoriedade é injetável).
- **C6, `Chance`:** ação com 30 %: com a fonte de aleatoriedade fixa em 0,2 roda; em 0,5 não roda.
- **C7, `Delayed`:** `Delayed(2 s, ApplyEffect)` não bloqueia a sequência; a ação seguinte roda no mesmo quadro, e a ação atrasada roda 2 s depois (no tempo do ASC).
- **C8, `ForEachTarget`:** com 3 alvos, as ações filhas rodam 3 vezes, cada uma com um alvo diferente como alvo padrão.
- **C9, `RemoveEffect`:** a ação remove os efeitos ativos de um `GameplayEffect` ou de uma tag no alvo (usa a API do B-003); sem efeito ativo, não faz nada e não lança exceção.
- **C10, espera conta o `Tick` no mesmo quadro (B-030):** no `RepeatAction.Interval`, no `SpawnProjectileAction.DelayBetweenShots` e no `WaitAction`, um `Tick` no mesmo quadro do fim do passo **conta** para a espera. Teste "Tick no mesmo quadro do fim do passo conta para a espera" para os três.
- **C11, evento de efeito:** um efeito com ações em "ao ser aplicado", "a cada tick" e "ao ser removido" executa cada lista uma vez no evento certo, com o contexto do efeito (instigador, nível).
- **C12, ação que dura num efeito:** um efeito com `Wait(1 s)` seguido de `ApplyEffect`; se o efeito é removido antes de 1 s, a ação em curso é cancelada e `ApplyEffect` não roda.
- **C13, gameplay event:** `SendGameplayEvent(Evento.Acerto, dados)` num ASC: um gatilho de habilidade que escuta essa tag ativa a habilidade; um efeito com ação em `OnAbilityExecuted` reage; ninguém reage a uma tag sem ouvinte (sem exceção).
- **C14, gatilho:** habilidade com gatilho "ao receber `Evento.Acerto`" ativa sozinha, respeitando a ordem de checagens (tags, cargas, custo, condições do B-009); falha de ativação não lança exceção e informa pelo evento de falha.
- **C15, `OnTakeDamage` e `OnDeath`:** um efeito (por exemplo, "espinhos") com ação em `OnTakeDamage` roda quando o dono perde atributo por efeito, com instigador e variação no contexto; `OnDeath` roda quando o atributo configurado chega ao mínimo (B-010).
- **C16, Passive:** uma habilidade Passive é ativada ao ser concedida, sem custo nem recarga, e permanece ativa até ser retirada.
- **C17, Toggle:** liga ao ativar (aplica as ações e o efeito) e desliga ao ativar de novo (cancela e desfaz); o estado fica legível (`IsToggledOn`) e o evento avisa a mudança.
- **C18, Channel:** uma habilidade Channel roda as ações por uma duração (ou até ser cancelada), e termina de forma limpa ao fim; cancelada por tag bloqueadora ou por código, desfaz o que precisa (`OnCancel` em ordem reversa).
- **C19, unidade-alvo:** `TryActivateAbility(ability, alvo)` com alvo por **unidade**; `CastRangeCondition` (B-009) mede até a unidade; fora do alcance a ativação falha com o motivo e **sem custo**.
- **C20, commit explícito:** com "commit manual" ligado, o custo e a recarga só saem quando a ação `Commit` roda; cancelar antes dela não cobra custo nem recarga. Com o padrão (commit no início), o comportamento é o de hoje.
- **C21, Inspector (S):** no host, a habilidade mostra abas por evento, cada ação com resumo de uma linha e o seletor de alvo inline; o efeito mostra suas abas, conforme o DS.
- **C22, sem lixo:** 10 000 execuções de sequência (3 ações simples, 1 seletor com filtro) com lista reutilizada não alocam memória gerenciada depois do aquecimento.
- **C23, autocast liga e desliga:** uma habilidade com autocast permitido e gatilho "quando pronta" (alvo escolhido por um seletor de área). Com o autocast **desligado**, nada acontece sozinho. Ao ligar (`SetAutocast(true)`), a próxima checagem que passa ativa a habilidade com o alvo escolhido; ao desligar, para.
- **C24, gatilho só com autocast:** um gatilho por evento de gameplay marcado "só com autocast" ativa a habilidade apenas com o autocast ligado; com ele desligado o mesmo evento é ignorado. A ativação manual funciona nos dois estados.
- **C25, falha silenciosa e sem laço:** com o autocast ligado e a habilidade em recarga, nenhuma tentativa de autocast dispara o evento de falha; o seletor sem alvo pula a tentativa sem exceção; há no máximo **uma** tentativa por habilidade por `Tick`.
- **C26, estado legível:** `IsAutocastEnabled(spec)` e o evento `OnAutocastChanged(spec, ligado)` avisam a UI; o estado inicial vem de "começa ligado" no asset; habilidade sem autocast permitido ignora `SetAutocast` (devolve falso).

## 4. Requisitos funcionais

### 4.1 Hierarquia única e eventos nomeados (ADR 0018)

R1. **Uma base de ação.** Uma única classe base de ação no `SpellBook.GAS`, escolhida por `[SerializeReference]` (ADR 0013). `AbilityAction` e `EffectAction` **deixam de existir**, sem adaptadores nem fachada (ADR 0009, I1). Todas as ações atuais (`Wait`, `Animation`, `Targeting`, `ApplyEffect`, `PlayCue`, `SpawnProjectile`, `Repeat`, `Parallel`, `Conditional`, `DebugLog`, `SpawnVFXEffect` e `RaiseEvent`/`RaiseEventEffect` do B-004 em `GAS.Bridges`) são **reescritas** sobre a nova base. Os assets do pacote que as usam (Samples, fixtures) são **refeitos**; não há ferramenta de migração (decisão I1) e o CHANGELOG avisa.

R2. **Eventos nomeados por dono.** Cada dono expõe pontos de evento, cada um com a sua lista de ações:
- **Habilidade:** ao ativar (início), ao terminar, ao ser cancelada, ao ser concedida e retirada (útil para passiva), e, para Channel, a cada passo de canalização (P10);
- **Efeito:** ao ser aplicado, a cada tick, ao ser removido (e os eventos de reação do R30);
- **Projétil:** no impacto (o `OnHitActions` de hoje) e, na Fase 8, ao terminar.
A **lista fechada** de eventos por dono na 3.0 é do Arquiteto (ADR 0018, "em aberto"). Thinkers e auras ficam para a Fase 8.

R3. **Contexto único de execução.** A ação recebe um **contexto de execução** com: origem (quem lançou), alvos do evento, ponto, nível, o `EffectContext` quando houver (B-007), os valores por nível e o relógio do ASC. **Estado da execução fica no contexto, nunca na ação** (ADR 0013, 0018). Um contexto único ou dois (execução e efeito) é decisão do Arquiteto (P7; ADR 0018, "em aberto").

R4. **Mesmo contrato para ações que duram.** Ações instantâneas e ações que duram (espera, repetição, canalização) seguem o mesmo contrato, com cancelamento. Ações disparadas por efeito **podem durar**; se o efeito é removido, a ação em curso é **cancelada** (cenário C12; ADR 0018, "em aberto").

### 4.2 Modelo de execução e espera (critério do B-030)

R5. **Tempo do ASC.** As ações que duram usam o tempo do `ITimeSource` do ASC, avançado pelo `Tick` (ADR 0014). **Não** usam `StartCoroutine`, `WaitForSeconds` nem o relógio da Unity (ADR 0018, "não podem fazer"). O modelo de execução (executor próprio avançado pelo `Tick` do dono, que é a recomendação do Arquiteto na ADR, ou outra forma) é decisão da Spec (P8).

R6. **Regra da espera (B-030).** Uma espera entre passos conta o tempo do ASC **a partir do fim do passo anterior**, e um `Tick` que ocorre **no mesmo quadro** do fim do passo **conta** para a espera. É o que o `DelayBetweenShots` faz hoje. O modelo de execução **garante** a regra também para o `RepeatAction.Interval` e para o `WaitAction`, sem depender do encadeamento de corrotinas.

R7. **Teste da regra.** Existe um teste nomeado "Tick no mesmo quadro do fim do passo conta para a espera" para o `Interval`, o `DelayBetweenShots` e o `WaitAction`, com `ManualTimeSource` (o teste do `DelayBetweenShots` da 0-M segue valendo como referência e passa sem edição).

R8. **Quadro entre ações sem espera.** Quando duas ações consecutivas de uma sequência **não** têm espera entre elas, elas rodam no mesmo quadro (proposta; hoje cada passo custa um quadro por causa das corrotinas aninhadas, e o `Count = 3` com `Interval = 0` gasta um quadro por repetição). A Spec confirma e lista os testes antigos que mudam, com aviso no CHANGELOG (P6).

### 4.3 Alvo por ação (`TargetSelector`)

R9. **Seletor por ação.** Cada ação tem um campo opcional `TargetSelector` (`[SerializeReference]`, ADR 0018, item 3). O seletor escolhe a partir do contexto: **quem lançou** (Caster), **alvos do evento** (Targets), **ponto** (Point) e **área** (Area: forma e raio, filtros). Sem seletor, a ação usa o **alvo padrão** do evento (C3).

R10. **Filtros de área.** O seletor de área aceita: tags exigidas e ignoradas (`GameplayTagQuery`, B-006), incluir ou não o dono, máximo de alvos, ordem (mais perto primeiro ou aleatório), camadas de física e modo 2D ou 3D (como a `TargetingStrategy` de hoje). A relação de **time** (aliado, inimigo) e a facção são filtros de `SpellBook.GAS.Bridges` (B-011), não do GAS.

R11. **Aleatoriedade injetável.** Seletores aleatórios e `Chance` usam uma **fonte de aleatoriedade injetável** (com seed nos testes), nunca `UnityEngine.Random` direto; o padrão em jogo é uma fonte do pacote (P9).

R12. **Convivência com a `TargetingStrategy`.** O seletor de alvo por ação convive com a `TargetingStrategy` atual (hoje um `ScriptableObject`) ou a substitui. Decisão do Arquiteto (P9; ADR 0018, "em aberto"); em qualquer caso, a `TargetingAction` continua como a ação que preenche os alvos do evento.

R13. **Sem alocação.** Escolher alvos usa listas reutilizadas fornecidas pelo contexto e buffers de física pré-alocados; não aloca em regime (C22).

### 4.4 Ações novas e fluxo

R14. **`Chance`.** Roda as ações filhas com uma probabilidade (de 0 a 1, `ScalableFloat` do B-007 permitido). Uma fonte fixa nos testes torna o resultado determinístico (C6).

R15. **`Delayed`.** Agenda as ações filhas para depois de um tempo, **sem bloquear** a sequência (C7). O tempo é o do ASC. Se o dono (habilidade ou efeito) termina ou é cancelado antes, as ações atrasadas pendentes são canceladas (padrão), com opção "continuar mesmo assim" por ação, documentada (P13).

R16. **`ForEachTarget`.** Roda as ações filhas **uma vez por alvo** do conjunto escolhido (seletor próprio ou os alvos do evento), com o alvo corrente como alvo padrão das filhas (C8). Reentrada segura se uma ação filha altera o conjunto.

R17. **`RemoveEffect`.** Ação que remove, no alvo escolhido, os efeitos de um `GameplayEffect` ou de uma tag, usando a API do B-003; devolve o número de pilhas removidas ao contexto (para uso em condições). Sem efeito, não faz nada (C9).

R18. **`ConditionalAction` usa `Condition`.** A ação condicional avalia uma condição da hierarquia do B-009 (And, Or, Not e as condições do GAS), e o enum `ConditionType` é **removido** (ADR 0019, item 6). As condições têm acesso ao contexto de execução (o contexto da condição é do Arquiteto, P4 do B-009).

R19. **Ações das outras fases.** Projétil, cue, VFX e evento (`SpawnProjectile`, `PlayCue`, `SpawnVFXEffect`, `RaiseEvent`) migram para a base nova e mantêm o comportamento. **Novos recursos** dessas ações (perfuração do projétil, filtro de time no impacto, cues por tag, thinker) são da Fase 8, não deste item.

### 4.5 Eventos de gameplay e gatilhos

R20. **`SendGameplayEvent`.** O ASC oferece `SendGameplayEvent(GameplayTag evento, in GameplayEventData dados)`, com `GameplayEventData` (instigador, alvo, magnitude, tags, `EffectContext` opcional). Evento de gameplay é **semântica do GAS**, não do barramento global (ADR 0016); o relay do B-004 pode repassá-lo depois (aditivo). Evento sem ouvinte não faz nada e não aloca. Tag inválida é ignorada.

R21. **Gatilhos de habilidade.** O `GameplayAbility` tem uma lista de **gatilhos** (`[SerializeReference]`): "quando este ASC recebe o evento de gameplay X" e "quando a tag Y é adicionada ou removida" (ADR 0018; plano, H.4). O gatilho tenta ativar a habilidade com o alvo do evento, **pela mesma ordem de checagens** (ADR 0022) e **pelo mesmo caminho** da ativação manual. A falha informa pelo evento de falha, sem exceção e sem laço infinito (um gatilho que dispara a si mesmo no mesmo quadro tem proteção de reentrada, P11).

R22. **Eventos de reação do efeito** (estilo Dota): além de aplicado, tick e removido, o efeito expõe os eventos `OnTakeDamage` (o dono perdeu atributo por efeito; contexto: instigador, variação, tags de dano do B-010), `OnDeath` (um atributo configurado do dono chegou ao mínimo; B-010) e `OnAbilityExecuted` (o dono ativou uma habilidade). A lista exata de eventos de reação na 3.0 é do Arquiteto (P12). Eles se apoiam nos eventos C# do ASC e do atributo (B-003, B-005, B-010), sem `EventManager`.

### 4.6 Comportamentos de habilidade

R23. **`AbilityBehavior`** por habilidade, escolhido no Inspector: `Instant` (o comportamento de hoje, padrão), `Passive`, `Toggle` e `Channel`.

R24. **Passive.** Ativada **automaticamente ao ser concedida** (e reativada se for interrompida, quando configurado), sem custo e sem recarga por padrão, permanecendo ativa até a retirada ou o cancelamento. As ações do evento "ao ativar" rodam uma vez; o "ao terminar" roda na retirada.

R25. **Toggle.** A ativação alterna o estado: ligar roda "ao ativar" e liga as tags e efeitos da habilidade; desligar cancela e desfaz (`OnCancel` em ordem reversa). `IsToggledOn` e o evento de mudança de estado são públicos. O custo é cobrado **só ao ligar** (P3 do usuário); o custo contínuo é um efeito infinito periódico que o designer põe na habilidade.

R26. **Channel.** Roda as ações enquanto canaliza, por uma **duração** configurável (`ScalableFloat`; zero ou "sem duração" significa até ser cancelada, P4 do usuário) e termina de forma limpa. A canalização é cancelada por: tag bloqueadora ou de cancelamento (ADR existente de bloqueio por tag), `CancelAbility` por código e fim da duração. Interromper por **movimento** ou entrada é do jogo, por código. A UI lê a duração e o progresso por uma API pública (a barra de cast é do módulo de UI, não daqui).

R27. **Efeitos e tags por comportamento.** Toggle e Channel concedem `ActivationOwnedTags` enquanto ativos e as devolvem ao terminar, usando o mesmo caminho da habilidade instantânea.

### 4.7 Alcance de cast, unidade-alvo e commit

R28. **Ativação com alvo.** `TryActivateAbility` e `GetActivationResult` aceitam um **alvo de ativação** (`AbilityTarget`: ponto e/ou unidade, ADR 0022 item 4). O alvo chega ao evento "ao ativar" como alvo padrão. A sobrecarga só com ponto continua funcionando como atalho.

R29. **Alcance de cast.** O `GameplayAbility` tem um campo de alcance (`ScalableFloat`, por nível, B-007), usado pela `CastRangeCondition` do B-009 agora **com unidade-alvo** (distância até a unidade ou até a borda dela, configurável). Fora do alcance a ativação falha pelo motivo da condição, na posição da ordem do ADR 0022, **antes do commit**, sem custo e sem recarga.

R30. **Commit explícito.** O `GameplayAbility` tem a opção `CommitMode`: `OnStart` (o **padrão**, comportamento de hoje: custo e recarga e consumo de carga **antes** de qualquer ação) e `Manual` (**opcional por habilidade**: a ação `Commit` roda no ponto escolhido: depois de um "cast point", ao fim da canalização, etc.). No modo `Manual`, cancelar antes do `Commit` **não** gasta custo, recarga nem carga; chamar `Commit` duas vezes não cobra duas vezes. Falha de custo ou carga **no commit** cancela a habilidade e informa pelo evento de falha (a checagem de ativação continua sem efeito colateral, ADR 0022).

### 4.8 Inspector, validação e documentação

R31. **Abas por evento e resumo.** O Inspector do `GameplayAbility` e do `GameplayEffect` (UI Toolkit, ADR 0026, herdando do `SpellBookEditor`) mostra **abas por evento** (ação por aba), cada ação com **resumo de uma linha** (o que faz, em quem, com que condição), categorias no seletor de tipo de ação e o **seletor de alvo inline**. O desenho (layout, cores por categoria, comportamento ao arrastar e reordenar) é **do DS**, em [`ds/inspector-de-acoes.md`](../ds/inspector-de-acoes.md) (ADR 0018, item 8), **escrito antes da Spec** e aprovado no mesmo gate (P5, decidida).

R32. **Validação** (`IValidatable`, 0026): ação nula na lista; ação num evento que o dono não oferece; seletor incompatível com o evento (por exemplo, "alvo do impacto" em evento de habilidade sem projétil); `ForEachTarget` vazio; `Delayed` com tempo negativo; `Chance` fora de 0 a 1; gatilho de uma habilidade para um evento inexistente; gatilho que ativa a própria habilidade que o dispara sem proteção; Passive com custo ou recarga (aviso); Channel sem duração e sem cancelamento (aviso); `CommitMode.Manual` sem ação `Commit` (erro); ação `Commit` em habilidade com `CommitMode.OnStart` (aviso); `RemoveEffect` sem efeito e sem tag; autocast permitido em Passive ou Toggle (erro); autocast permitido sem nenhum gatilho "quando pronta" nem gatilho "só com autocast" (aviso: o interruptor não faz nada); gatilho "quando pronta" com seletor que exige alvo mas sem alcance de cast (aviso). Mensagens clicáveis.

R33. **Documentação.** A página de Habilidades e a de Efeitos descrevem o modelo evento → ações → alvos, os seletores, as ações novas, os gatilhos, os comportamentos, o commit e o autocast. Uma página de **migração para a 3.0** lista cada ação antiga e a nova. A regra da espera (R6) e o exemplo "Interval conta o Tick do mesmo quadro" entram no manual (substitui a nota do RP-06 do B-030, que passa a descrever o comportamento antigo). Os tipos novos têm `[HelpURL]` (decisão I15).

R34. **CHANGELOG.** Cita: fim de `AbilityAction` e `EffectAction` (quebra, ADR 0009), a nova base e os eventos nomeados, `TargetSelector`, `Chance`/`Delayed`/`ForEachTarget`/`RemoveEffect`, `ConditionalAction` sem enum, `SendGameplayEvent` e gatilhos, comportamentos, autocast, alvo de ativação, alcance de cast, `CommitMode`, a regra das esperas (R6) e o novo quadro entre ações (R8).

### 4.9 Autocast

R35. **Interruptor por habilidade.** O `GameplayAbility` tem a configuração `Autocast`: `Allowed` (padrão **falso**: nada muda para quem não usa) e `StartsOn` (se o autocast nasce ligado ao conceder a habilidade). O `AbilitySpec` guarda o estado ligado ou desligado. O ASC oferece `SetAutocast(spec, bool)` (devolve `false` se a habilidade não permite ou se o estado não mudou), `IsAutocastEnabled(spec)` e o evento C# `OnAutocastChanged(spec, ligado)`, que dispara só em mudança real. Sem alocação.

R36. **Autocast liga gatilhos.** O autocast **não** é um mecanismo de ativação novo: ele liga e desliga **gatilhos** da lista do R21. Cada gatilho tem a opção "só com autocast" (padrão desligada). Um gatilho marcado só dispara com o autocast **ligado**; os demais gatilhos não mudam. Existe um tipo de gatilho a mais, **"quando pronta"**: a cada `Tick`, se a habilidade não está ativa, tenta ativá-la (é o caso típico do autocast). Com o autocast desligado, os gatilhos "só com autocast" são ignorados.

R37. **Alvo da tentativa.** O gatilho "quando pronta" tem um `TargetSelector` opcional (R9), avaliado a partir de quem lança, que escolhe o alvo da tentativa (por exemplo, o inimigo mais perto no alcance). Sem seletor, a tentativa é sem alvo (habilidade em si mesmo). Se o seletor não devolve alvo, a tentativa é **pulada**, sem custo, sem evento e sem exceção. Gatilhos por evento de gameplay usam o alvo do evento, como no R21.

R38. **Mesmo caminho, mesma ordem.** A tentativa de autocast usa a **mesma função** de checagem e de ativação do uso manual (ordem tags → cargas/recarga → custo → condições, ADR 0022; `CommitMode`, cargas e custo valem como no uso manual). Autocast não cobra nada além do que a habilidade já cobra.

R39. **Falha é silenciosa; sem laço.** Uma tentativa de autocast que falha **não** dispara o evento de falha da ativação (ele existe para tentativa pedida por alguém e, com autocast, dispararia a cada `Tick`). Há **no máximo uma tentativa por habilidade por `Tick`**, e a proteção contra reentrada do R21 vale. A ativação manual continua funcionando com o autocast ligado ou desligado, e ela é a que dispara o evento de falha, como hoje.

R40. **Comportamentos compatíveis.** O autocast vale para `Instant` e `Channel` (P7). Não vale para `Passive` (já é automática) nem `Toggle` (o interruptor dele é outro, R25); o validador acusa. A decisão de **quando lançar entre várias habilidades** (prioridade, IA) é do jogo: aqui a ordem é a da concessão. Habilidades de IA e comportamento de unidade ficam fora.

## 5. Requisitos não funcionais

N1. **Alocação:** executar sequências, escolher alvos, `Delayed`, `ForEachTarget`, `SendGameplayEvent` e a verificação de autocast a cada `Tick` não alocam depois do aquecimento (C22). Criar a execução de ação que dura pode alocar uma vez por execução, nunca por quadro (a Spec avalia pool).
N2. **Módulos (ADR 0012, 0018):** base de ação, eventos, seletores, ações de fluxo e `RemoveEffect` em `SpellBook.GAS`. **Ações que usam Events, Pooling, Cues ou Teams ficam em `SpellBook.GAS.Bridges`**; o GAS não referencia nenhum deles. Condições de time em `Bridges`.
N3. **Tempo:** só `ITimeSource` e `ASC.Tick` (ADR 0014); testes com `ManualTimeSource`. Nenhuma corrotina da Unity em ação que dura.
N4. **Estado:** nenhum estado de execução em campo da ação; ações são dados reutilizáveis entre execuções simultâneas (ADR 0013). Teste com duas execuções simultâneas da mesma ação.
N5. **Determinismo:** aleatoriedade injetável (R11), factories (`GasFactory`), sem assets fixos.
N6. **Serialização:** nativa da Unity com `[SerializeReference]`; campos novos têm padrão que reproduz o comportamento de hoje (`CommitMode.OnStart`, `Instant`, sem seletor). Os **assets antigos de ação não abrem** (I1): o aviso está no CHANGELOG e na página de migração.
N7. **Reentrada e cancelamento:** cancelar durante uma sequência desfaz em ordem reversa (`OnCancel`) e é seguro com ações que alteram o conjunto de execuções.
N8. **Sem regressão:** a suíte do GAS (com B-002 a B-010) passa sem edição de teste antigo, **exceto** os testes de ação que usam as classes `AbilityAction`/`EffectAction` ou o quadro por passo (R8). O Arquiteto lista esses testes na Spec.

## 6. Fora do escopo

- **Já entregue pelo Lote A:** remoção idempotente (B-002) e tags (B-006). Aqui só são usados.
- **B-003:** lista pública, `RemoveEffect`/remoção por tag e eventos de efeito. `RemoveEffect` (ação) só os usa.
- **B-004:** `RaiseEventAction`, `RaiseEventEffectAction` e relay. Elas **migram** para a base nova (R1); o escopo delas não muda.
- **B-005:** `OnDepleted` e metadados. **B-010:** pipeline de dano, evento de morte, tags de dano. `OnTakeDamage` e `OnDeath` do R22 **se apoiam neles**, sem reimplementá-los.
- **B-007:** `ScalableFloat`, `EffectContext`, `EffectSpec`. O contexto único de execução (R3) deve ser compatível com o `EffectContext`; a unificação é daqui, a definição dele é de lá.
- **B-009:** `Condition`, `ActivationFailure`, `CastRangeCondition` (ponto). Aqui entram unidade-alvo, commit explícito e a migração do `ConditionalAction`.
- **B-011:** Teams. Aqui só o gancho dos filtros (R10).
- **Fase 8:** `GameplayThinker` e auras, sistema de cues por tag, projétil com perfuração, término e filtro de time, e o evento de fim de projétil.
- **Habilidades de IA** (decidir **quando** e **o que** lançar entre várias, prioridades, comportamento de unidade): fora. O **autocast** (interruptor que liga gatilhos, seção 4.9) **entra** neste item, por decisão do usuário (P1).
- **Barra de cast e widgets de UI:** módulo de UI (B-015 a B-018); aqui só a leitura (R26).
- **Ferramenta de migração de assets antigos de ação:** não há (I1).
- **Progressão, XP, pontos de habilidade, replicação e predição:** sem item.

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

**Hierarquia, contexto e eventos**
- CA1 (E). `AbilityAction` e `EffectAction` não existem mais; todas as ações do R1 herdam da base única (revisão estrutural e busca de código).
- CA2 (T). Cenário C1: a mesma ação roda em lista de habilidade e em lista de efeito.
- CA3 (T). Cenário C11: efeito executa as listas aplicado, tick e removido no evento certo, com contexto do efeito.
- CA4 (T). Cenário C12: ação que dura em efeito é cancelada quando o efeito é removido.
- CA5 (T). Duas execuções simultâneas da mesma ação não compartilham estado (N4).
- CA6 (T). Cancelar uma sequência chama `OnCancel` em ordem reversa e não lança exceção.

**Espera e tempo (B-030)**
- CA7 (T). Cenário C10: "Tick no mesmo quadro do fim do passo conta para a espera" passa para `RepeatAction.Interval`, `SpawnProjectileAction.DelayBetweenShots` e `WaitAction`.
- CA8 (T). O teste do `DelayBetweenShots` existente passa sem edição.
- CA9 (T). Duas ações consecutivas sem espera rodam no mesmo quadro (R8, conforme a Spec).
- CA10 (E). Nenhuma ação usa `StartCoroutine`, `WaitForSeconds` nem relógio da Unity (busca no código; ADR 0018).

**Alvo por ação**
- CA11 (T). Cenários C2 e C3: seletores "quem lançou" e "alvos do evento" na mesma lista; sem seletor usa o alvo padrão.
- CA12 (T). Cenário C4: área com tags exigidas e ignoradas, máximo de alvos e incluir dono.
- CA13 (T). Cenário C5: sorteio de N alvos distintos, reproduzível com o mesmo seed.
- CA14 (T). `Chance` com a fonte fixa (cenário C6) e fora de 0 a 1 é acusada pelo validador.
- CA15 (T). A relação de time como filtro não existe no GAS; o teste de assemblies prova que o GAS não referencia Teams (CA29).

**Ações novas**
- CA16 (T). Cenário C7: `Delayed` não bloqueia, a ação seguinte roda no mesmo quadro e a atrasada roda no tempo certo.
- CA17 (T). `Delayed` pendente é cancelada quando o dono termina ou é cancelado; com "continuar" ligado, roda.
- CA18 (T). Cenário C8: `ForEachTarget` roda uma vez por alvo com o alvo corrente; é seguro se as filhas alteram o conjunto.
- CA19 (T). Cenário C9: `RemoveEffect` por asset e por tag; sem efeito ativo, não faz nada.
- CA20 (T). `ConditionalAction` usa `And`, `Or` e `Not` do B-009; o enum `ConditionType` não existe mais.

**Gameplay events, gatilhos e reações**
- CA21 (T). Cenário C13: `SendGameplayEvent` chega ao gatilho e a `OnAbilityExecuted`; sem ouvinte, nada acontece e não aloca; tag inválida é ignorada.
- CA22 (T). Cenário C14: gatilho ativa a habilidade pela ordem de checagens do ADR 0022; falha informa pelo evento de falha; um gatilho que dispara a si mesmo no mesmo quadro não entra em laço.
- CA23 (T). Cenário C15: `OnTakeDamage` e `OnDeath` rodam com instigador e variação (B-010); `OnAbilityExecuted` roda ao ativar.

**Comportamentos**
- CA24 (T). Cenário C16: Passive ativa ao conceder, sem custo e recarga, e termina na retirada.
- CA25 (T). Cenário C17: Toggle liga e desliga, custo só ao ligar, `IsToggledOn` e evento de mudança.
- CA26 (T). Cenário C18: Channel dura a duração configurada, e sem duração dura até ser cancelado; cancelamento por tag e por código desfaz em ordem reversa.
- CA27 (T). `ActivationOwnedTags` são concedidas e devolvidas por Toggle e Channel.

**Alvo de ativação, alcance e commit**
- CA28 (T). Cenário C19: ativação com unidade-alvo; fora do alcance falha com o motivo e **sem custo**; a sobrecarga só com ponto continua funcionando.
- CA29 (T). Cenário C20: `CommitMode.Manual` não cobra antes da ação `Commit`; cancelar antes não cobra; `Commit` duas vezes cobra uma; falha de custo no commit cancela e informa.
- CA30 (T). Com `CommitMode.OnStart` (padrão), o custo e a recarga saem antes de qualquer ação, como hoje.

**Editor, validação, módulos e documentação**
- CA31 (S). Cenário C21: no host, abas por evento, resumo de uma linha por ação e seletor de alvo inline em habilidade e efeito, conforme o DS.
- CA32 (T). O validador cobre os casos do R32, com o objeto clicável.
- CA33 (T). O teste de assemblies da 0-M continua passando: o GAS não referencia Events, Pooling, Cues, Teams nem UI.
- CA34 (T). Cenário C22: 10 000 execuções de sequência com seletor e lista reutilizada alocam 0 bytes gerenciados depois do aquecimento.
- CA35 (E). O CHANGELOG cita os itens do R34; o manual tem as páginas do R33 (inclusive a de migração e a regra da espera); os tipos novos têm `[HelpURL]`.
- CA36 (T). A suíte do GAS existente passa sem edição, exceto os testes listados na Spec (N8).

**Autocast**
- CA37 (T). Cenário C23: com o autocast desligado nada é ativado sozinho; ligado, o gatilho "quando pronta" ativa a habilidade com o alvo do seletor; desligado de novo, para.
- CA38 (T). Cenário C24: gatilho "só com autocast" dispara só com o autocast ligado; gatilho sem a marca dispara nos dois estados; a ativação manual funciona nos dois.
- CA39 (T). Cenário C25: tentativa que falha não dispara o evento de falha; seletor sem alvo pula a tentativa sem custo e sem exceção; no máximo uma tentativa por habilidade por `Tick`; a ordem das checagens é a do ADR 0022 (um teste por posição, como o CA7 do B-009).
- CA40 (T). Cenário C26: `SetAutocast` devolve `false` sem `Allowed` ou sem mudança; `OnAutocastChanged` dispara uma vez por mudança real; `StartsOn` define o estado ao conceder; autocast com `CommitMode.Manual` e com cargas cobra como o uso manual.
- CA41 (T). O validador aponta autocast em `Passive` e `Toggle` (erro), autocast sem gatilho que o use (aviso) e "quando pronta" com seletor que exige alvo e sem alcance de cast (aviso), com o objeto clicável.
- CA42 (T). Habilidade com `Autocast.Allowed` falso e sem gatilho se comporta como antes (sem tentativa por `Tick`, sem alocação).

## 8. Perguntas em aberto

### Com o usuário

- **P1. Autocast.** *Respondida (usuário, 2026-10-31): o **autocast ENTRA** neste item (o PRD propunha deixá-lo fora). Requisitos R35 a R40, cenários C23 a C26 e CA37 a CA42 acrescentados. Os detalhes de desenho continuam em aberto em P6 a P8.*
- **P2. Commit padrão.** *Respondida (usuário, 2026-10-31): aceita. O padrão é o **commit no início** (como hoje); o commit manual é **opção por habilidade** (`CommitMode.Manual`).*
- **P3. Custo do Toggle.** *Respondida (usuário, 2026-10-31): aceita. O Toggle cobra o custo **só ao ligar**; o custo contínuo é um efeito infinito periódico posto pelo designer.*
- **P4. Duração do Channel.** *Respondida (usuário, 2026-10-31): aceita. A duração é **opcional**: zero (ou "sem duração") dura até ser cancelada.*
- **P5. Ordem de entrega e do DS.** *Respondida (usuário, 2026-10-31): SIM. O DS do Inspector de ações (abas, resumo, seletor inline) foi escrito em [`ds/inspector-de-acoes.md`](../ds/inspector-de-acoes.md), antes da Spec, para entrar no mesmo gate de PRD, DS, Spec e ADRs.*
- **P6. Gatilho "quando pronta" e alvo (R36, R37).** *Respondida (usuário, 2026-10-31): aceita. O autocast liga gatilhos já existentes (R21) e um tipo novo, "quando pronta", com `TargetSelector` opcional para escolher o alvo (R37).*
- **P7. Comportamentos com autocast (R40).** *Respondida (usuário, 2026-10-31): aceita. Autocast só em `Instant` e `Channel`; `Passive` e `Toggle` com autocast são erro do validador.*
- **P8. Falha de autocast (R39).** *Respondida (usuário, 2026-10-31): aceita. A tentativa de autocast que falha é **silenciosa** (sem evento de falha), com uma tentativa por habilidade por `Tick`.*

### Com o Arquiteto (decisão técnica da Spec)

- **P6. Quadro entre ações sem espera** (R8): confirmar que passam a rodar no mesmo quadro e listar os testes que mudam (o `Count = 3` com `Interval = 0` deixa de gastar um quadro por repetição).
- **P7. Contexto de execução** (ADR 0018, "em aberto"): contexto único que substitui `AbilityContext` e o `EffectContext`, ou o efeito carrega o seu e a ação recebe os dois; e o contexto da `Condition` (P5 do B-009).
- **P8. Modelo de execução** das ações que duram (R5): executor próprio avançado pelo `Tick` do dono (recomendação da ADR) e como ele garante a regra do R6 sem corrotinas.
- **P9. Seletor e `TargetingStrategy`** (R12): convivem ou o seletor substitui; e a fonte de aleatoriedade (R11): onde mora e como é injetada.
- **P10. Lista fechada de eventos por dono** na 3.0 (R2 e R22), inclusive o evento de canalização por passo.
- **P11. Gatilhos** (R21): proteção contra laço (profundidade ou fila para o fim do `Tick`), e como o gatilho passa o alvo do evento.
- **P12. `OnTakeDamage`, `OnDeath` e `OnAbilityExecuted`** (R22): quais atributos disparam (todos ou os marcados) e a ordem frente aos eventos do B-005 e do B-010.
- **P13. Cancelamento de `Delayed` pendente** (R15) e de ações de efeito removido (R4): quem é dono da execução e como o cancelamento é reentrante.
- **P14. Quebra dos assets** (R1, N6): o pacote tem Samples e fixtures que usam as ações atuais; o que é refeito dentro do item e quais testes de carga YAML mudam.
- **P15. Projétil:** como o `Projectile` roda a lista de impacto na base nova sem `Source.StartCoroutine`, mantendo o comportamento atual (sem os recursos da Fase 8).
- **P16. Autocast no `Tick`** (R36 a R39): onde roda a verificação "quando pronta" (no `ASC.Tick`, antes ou depois dos gatilhos por evento), como garante "no máximo uma tentativa por `Tick`" sem alocar, e como o estado do autocast convive com a proteção de reentrada dos gatilhos (P11).

## 9. Ajustes de playtest

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | PRD escrito pelo Criativo a partir do B-012, do plano (Fase 7) e das ADRs 0018, 0019, 0022 e 0024; o Controlador pediu o PRD inteiro e a Spec divide em tickets | Todo o documento | Rascunho |
| 2026-10-31 | Critério do B-030 (decidido pelo Arquiteto e aprovado no gate de 2026-10-31): a espera conta o tempo do ASC a partir do fim do passo anterior, inclusive um `Tick` no mesmo quadro; o modelo de execução garante isso para `Interval`, `DelayBetweenShots` e `WaitAction`, com o teste nomeado | R6, R7, CA7, CA8 | Aplicado |
| 2026-10-31 | Dependências registradas sem puxar escopo: B-007, B-009, B-010, B-003, B-004, B-011; Fase 8 fica fora | Seção 6 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P1: o **autocast entra** no B-012 (decisão contrária à proposta do PRD) | Objetivo 9, C23 a C26, 4.9 (R35 a R40), R32 a R34, seção 6, CA37 a CA42, P6 a P8, P16 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P2: commit no início por padrão, manual opcional por habilidade | R30 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P3: Toggle cobra só ao ligar | R25 (sem mudança de texto: já descrevia isso) | Aplicado |
| 2026-10-31 | usuário (via Controlador) P4: duração do Channel opcional; zero dura até cancelar | R26 (sem mudança de texto: já descrevia isso) | Aplicado |
| 2026-10-31 | usuário (via Controlador) P5: o DS do Inspector de ações é escrito antes da Spec e entra no mesmo gate | R31, Objetivo 8, [ds/inspector-de-acoes.md](../ds/inspector-de-acoes.md) | Aplicado |
| 2026-10-31 | usuário (via Controlador) P6 a P8: todas as propostas aceitas (autocast com gatilhos existentes mais "quando pronta" e seletor opcional; só `Instant` e `Channel`; falha silenciosa, uma tentativa por `Tick`) | R36, R37, R39, R40 (sem mudança de texto: já descreviam isso) | Aplicado |
