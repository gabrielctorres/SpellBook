# 0022 — Ordem das checagens de ativação

Status: Aceita como direção (gate 2026-10-29; seções "Em aberto" detalhadas no épico)
Data: 2026-10-29

## Contexto

`AbilitySystemComponent.GetActivationResult` (`Runtime/GAS/Core/AbilitySystemComponent.cs:111-126`) checa, nesta ordem: habilidade válida, concedida, já ativa, `BlockedTags`, `RequiredTags`, bloqueio por habilidade ativa, recarga, custo. O resultado é um enum fixo, e a checagem não recebe o alvo.

As próximas fases trazem cargas com recarga sequencial (decisão I.10) e condições plugáveis (0019). Com mais checagens, a ordem define qual motivo a UI mostra quando várias falham ao mesmo tempo. A decisão I.12 do usuário: **tags → cargas/recarga → custo → condições plugáveis**, e o primeiro motivo que falhar vai para a UI.

## Decisão

1. A ativação checa em ordem fixa e para na primeira falha:
   0. **Pré-requisitos:** habilidade válida, concedida e não ativa.
   1. **Tags:** tags que bloqueiam, tags exigidas e bloqueio por habilidade ativa.
   2. **Cargas/recarga:** sem carga disponível ou em recarga.
   3. **Custo.**
   4. **Condições plugáveis**, na ordem da lista da habilidade.
2. O resultado traz o tipo da falha e o motivo (tag ou chave de texto) da primeira checagem que falhou; é esse motivo que a UI mostra.
3. Consultar se pode ativar (sem ativar) usa a mesma ordem e não tem efeito colateral.
4. A consulta aceita o alvo pretendido (ponto e/ou unidade), para que condições de alcance e de alvo funcionem antes do commit.
5. Valores novos de enum entram sempre no fim, para não mudar os valores serializados existentes.

## Motivo

- Ordem previsível: o jogador vê o motivo mais "estrutural" primeiro (atordoado, em recarga) antes do mais circunstancial (fora de alcance).
- Uma ordem só para consulta e ativação evita a UI dizer "pode" e a ativação dizer "não pode".

## Consequências

- Com uma habilidade em recarga e o alvo fora de alcance, a UI mostra "em recarga".
- A assinatura da consulta e o evento de falha mudam para carregar alvo e motivo (quebra aceita pela 0009).

## O que os agentes NÃO podem fazer

- Mudar a ordem sem ADR.
- Fazer checagem com efeito colateral (consumir carga, aplicar custo) antes do commit.
- Ter caminhos diferentes para "pode ativar?" e "ativar".
- Inserir valores no meio de um enum serializado.

## Em aberto

- Com cargas habilitadas, a checagem de recarga é substituída pela de carga (uma habilidade com 1 de 3 cargas e recarga correndo pode ativar)? Proposta: sim.
- Um modo de depuração que coleta todas as falhas (para o GAS Debugger), além da primeira?
- As regras de cargas (máximo fixo ou por atributo, recarga sequencial, decisão I.10) ganham ADR própria ou ficam na Spec da Fase 5?

## Relacionadas

- 0018 (alvo da ativação e commit), 0019 (condições e motivo), 0023 (texto do motivo).
