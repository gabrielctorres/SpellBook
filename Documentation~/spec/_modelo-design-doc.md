# Design doc: <épico>
Épico: `epic:<slug>` · Backlog: B-NNN · ADRs: <lista> · Status: Rascunho | Aprovado (gate AAAA-MM-DD)

<!-- Modelo da emenda de 2026-11-03 da ADR 0002. Copie para spec/<épico>.md e apague os comentários.
     Parte fixa: de 1 a 2 páginas, escrita pelo Desenhista quando o épico nasce. Muda raramente; mudar um CA volta ao gate. -->

## Problema
<O que falta hoje para quem usa o pacote (o dev ou o designer no Inspector), em 3 a 6 linhas. Sem solução aqui.>

## Critérios de aceite
1. **CA1.** <Um comportamento observável, verificável por teste automatizado ou pelo cenário do Sandbox.>
2. **CA2.** <...>

## Cenário no Sandbox
<A cena ou o passo a passo em `Packages/com.spellbook/Samples~/Sandbox` que prova o épico. Cada passo cita os CA que ele mostra.>

## Fora do escopo
- <Opcional. Um item por linha.>

## Decisões do épico
- <Opcional. Decisão que só vale para este épico. A que vale para mais de um épico vira ADR.>

## Tickets
| Ticket | Objetivo | CA | Depende de | Unity |
|---|---|---|---|---|
| XX1 | <uma frase> | CA1, CA2 | — | sim |

## Validação de uso
| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|

<!-- Parte JIT: uma seção por ticket, escrita pela Fábrica quando o ticket entra na fila, lendo o código na hora.
     Limites e contagem: ADR 0042. -->

## XX1 — <título>
### Detalhe (AAAA-MM-DD)
- CA: <números do design doc que o ticket cobre>
- Escopo: <arquivos, com caminho:linha do que muda>
- Testes red: <nomes, com EditMode ou PlayMode>
- Critério de pronto: <verificável>
- Tamanho: <~N linhas e N arquivos, pela 0042>. Exceção: <nenhuma, ou a linha registrada em `Documentation~/decisoes.md`>
- Commit: `<tipo>(<escopo>): <resumo>`
