# 0019 — Hierarquia única de `Condition` (And/Or/Not)

Status: Aceita como direção (gate 2026-10-29; seções "Em aberto" detalhadas no épico)
Data: 2026-10-29

## Contexto

Hoje a única condição configurável é o enum `ConditionalAction.ConditionType` (`Runtime/GAS/Abilities/Actions/FlowActions.cs:75-80`: `SourceHasAllTags`, `SourceHasAnyTag`, `HasTargets`). As fases seguintes precisam de condições em vários lugares: ativação de habilidade (0022), ações condicionais e filtros de alvo (0018), reações de evento (0016), filtros de UI (0017). Cada lugar com o seu enum ou a sua interface multiplicaria tipos parecidos.

## Decisão

1. Uma **única hierarquia de condição**, com a base no `SpellBook.Core` e escolhida por `[SerializeReference]` (0013).
2. Composição pronta: **And**, **Or** e **Not**, que aceitam qualquer condição da mesma hierarquia.
3. Condições são puras: avaliam e devolvem verdadeiro ou falso, sem efeito colateral e sem alocar.
4. Uma condição pode informar um **motivo de falha** (tag ou chave de texto, não texto pronto), usado pela ativação (0022) e pela UI.
5. Cada módulo define as condições do seu domínio: Tags (query de tags), GAS (atributo acima de limite, tem alvos, alcance), Bridges (relação de time).
6. O enum `ConditionalAction.ConditionType` é removido (0009).

## Motivo

- Um vocabulário só, com composição, para todo lugar que decide "sim ou não".
- Motivo de falha estruturado alimenta a UI sem texto fixo (0023).
- A base no Core permite que módulos sem GAS (Events, UI) usem condições.

## Consequências

- A base precisa saber em que contexto a condição é avaliada, e contextos diferentes (ativação, execução de ação, evento de canal, UI) não são o mesmo tipo. Este é o principal ponto em aberto.
- Condições novas exigem página ou entrada de catálogo (0007).

## O que os agentes NÃO podem fazer

- Criar enum, interface ou lista de condições paralela a esta hierarquia.
- Escrever condição com efeito colateral (aplicar efeito, consumir recurso, disparar evento).
- Devolver texto pronto como motivo de falha.
- Colocar condição de time no `SpellBook.GAS` (vai para Bridges).

## Em aberto

- **Contexto genérico.** Opções:
  - (a) `Condition<TContext>` genérica no Core, com And/Or/Not por contexto. Instâncias genéricas em `[SerializeReference]` dependem do suporte confirmado na 6000.0 (0013); sem ele, cada contexto precisa de And/Or/Not concretos.
  - (b) `Condition` não genérica, com um contexto que oferece consulta por tipo (por exemplo, "tem um ASC de origem?"); mais flexível, menos verificável no Inspector.
  - (c) Um contexto único do GAS (0018) como único `TContext` do GAS, e outra base só para Events/UI.
- O motivo de falha é uma tag (`SpellBook.Tags`, o que faria o Core depender de Tags) ou uma chave de texto?
- O seletor do Inspector filtra condições pelo contexto aceito? Como?

## Relacionadas

- 0012 (Core sem domínio), 0013 (costura), 0016 (reações), 0017 (filtros de UI), 0018 (ações), 0022 (ativação), 0023 (texto).
