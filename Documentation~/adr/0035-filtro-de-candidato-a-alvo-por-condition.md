# 0035 — Filtro de candidato a alvo: uma `Condition<ConditionContext>` com sujeito, no seletor de área, no impacto do projétil e na aura

Status: Aceita (gate 2026-10-31)
Data: 2026-10-31

## Contexto

Três pontos do pacote decidem "este candidato pode ser alvo?":

- o `AreaSelector` do B-012 (Spec `gas-evento-acoes-alvos.md`, Design 3), que nasce com um campo próprio `GameplayTagQuery filter` (EV4b);
- o filtro de impacto do projétil (PRD B-014, R17), que o PRD descreve como uma lista de `Condition` (B-009);
- os filtros da aura (PRD B-014, R11), que repetem os do seletor de área.

O B-011 (Teams) precisa encaixar a relação de time nos três sem que o GAS conheça o Teams (0012). O PRD do B-011 (P11) pede "um gancho de filtros" e a Spec do B-012 deixou em aberto "subclasse de `TargetSelector` ou condição".

Restrições:
- a 0032 fixa um contexto só no GAS (`ConditionContext`) e proíbe um segundo contexto ou um And/Or/Not paralelo;
- a `TagQueryCondition` do B-009 testa as tags do `Owner` (Spec do B-009, Design 3), não as do candidato;
- `AbilityTarget.Unit` chega no EV8 do B-012.

Sem uma regra única, cada ponto teria o próprio filtro (consulta de tags no seletor, lista de condições no projétil, campos na aura), e o time entraria três vezes.

## Decisão

1. **Um contrato só de "filtro de candidato".** Onde o pacote filtra candidatos a alvo, o filtro é um campo `[SerializeReference] Condition<ConditionContext>`, avaliado uma vez por candidato com:
   - `Owner` = quem procura (quem lançou a habilidade, o dono do efeito, o criador do thinker);
   - `Target` = `AbilityTarget.AtUnit(candidato)`;
   - `Ability`, `Spec`, `Level` e `Targets` da execução em curso.

   Condição nula aceita todos. Composições (`And`, `Or`, `Not`) são as do GAS (0032).
2. **A `TagQueryCondition` ganha um sujeito.** `ConditionSubject { Owner, TargetUnit }`, padrão `Owner`, que é o comportamento do B-009. `TargetUnit` testa as tags da unidade do `Target` (sem unidade: falso). Nenhuma condição nova de tag.
3. **O `AreaSelector` troca o `GameplayTagQuery filter` pelo filtro de candidato.** `includeOwner`, `maxTargets`, `Order`, raio, origem e camadas continuam campos do seletor, porque não são predicados sobre um candidato só. "Só quem tem `Estado.Vivo`" é `TagQueryCondition` com sujeito `TargetUnit`.
4. **Projétil e aura usam o mesmo campo.** As condições de impacto do projétil são um filtro de candidato (R17 do B-014). A aura usa um `AreaSelector` para achar quem está dentro, e por isso herda o filtro dele (R11 do B-014).
5. **O time é só uma condição.** O B-011 entrega `TeamRelationCondition : Condition<ConditionContext>` no `SpellBook.GAS.Bridges`. Ela funciona nos três pontos sem nenhum gancho a mais. Sem o módulo Teams, o tipo não existe e não aparece no seletor.
6. **Onde entra no B-012 (emenda da Spec aprovada):**
   - o EV4b deixa de ter o `GameplayTagQuery filter` e o teste de tags;
   - o EV8, que já traz `AbilityTarget.Unit`, passa a ter o campo `filter` do `AreaSelector`, o `ConditionSubject` da `TagQueryCondition` e o teste `Area_FilterTagOnTarget_Max2_ExcludesOwner` (era `Area_RequiredIgnoredTags_Max2_ExcludesOwner`, CA12).

## Motivo

- Um vocabulário só: o mesmo predicado vale na ativação, na ação condicional, no seletor, no impacto e na aura, como a 0019 pede ("hierarquia única").
- O GAS continua sem conhecer Teams. O ponto de extensão é o tipo do campo (0013, 0032 item 3), sem lista de filtros nova.
- O `GameplayTagQuery` solto no seletor seria um segundo mecanismo de filtro de tags ao lado da `TagQueryCondition`.

## Consequências

- O `AreaSelector` avalia uma condição por candidato. O custo é uma chamada virtual por candidato, sem alocação, porque o contexto é uma `readonly struct`.
- Um seletor de área sem filtro funciona como antes do filtro (todos os candidatos, menos o dono quando `includeOwner` está desligado).
- As Specs do B-009 e do B-012 recebem a emenda do item 6 (o B-009 não muda: o sujeito nasce no EV8). As issues do EV4b e do EV8 precisam ser atualizadas.
- O validador do B-012 (EV16) passa a acusar `TagQueryCondition` com sujeito `TargetUnit` fora de um filtro de candidato. Isso é aviso, porque sem unidade a condição é sempre falsa.

## O que os agentes NÃO podem fazer

- Criar um filtro de candidato com outro tipo (lista de tags, interface própria, subclasse de seletor por relação).
- Criar uma segunda condição de tag para o alvo.
- Fazer o GAS referenciar Teams para filtrar.

## Em aberto

Nada.

## Relacionadas

- 0012 (módulos), 0013 (costura), 0018 e 0034 (ações e seletor), 0019 e 0032 (condições), 0036 (thinker e aura), Specs `teams.md`, `gas-thinkers-auras-e-projeteis.md`, `gas-evento-acoes-alvos.md`.
