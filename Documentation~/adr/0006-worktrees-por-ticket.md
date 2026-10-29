# 0006 — Worktrees por ticket

Status: Adiada
Data: 2026-10-29

> **Adiada em 2026-10-29** (decisão I34 do usuário). Enquanto adiada, vale: **uma branch por ticket no mesmo checkout** (`feat|fix|refactor/<feature>-<ticket>`, criada pelo DevOps a partir da `main`), com o stage seletivo do `/commit` para separar documentos de código. Condição para retomar: conflitos recorrentes entre documentos e código no mesmo checkout, ou necessidade de manter dois tickets abertos ao mesmo tempo. O texto abaixo fica como registro da proposta original e não vale como regra.

## Contexto

Na mesma pasta de trabalho convivem documentos (backlog, PRD, Spec, ADR, editados pelo Controlador, Criativo e Arquiteto) e código do ticket (Fábrica). O `CLAUDE.md` pede preservar alterações do working tree que não são da tarefa, e o `/commit` faz stage seletivo por causa disso. Misturar os dois na mesma árvore aumenta o risco de um commit levar arquivo errado.

Pela regra atual, só o DevOps altera estado do git, e só quando o usuário pede. A Fábrica pode rodar apenas `git status` e `git diff`.

## Decisão

1. Cada ticket é implementado num `git worktree` próprio, numa branch `feat|fix/<feature>-<ticket>` criada a partir da `main` atualizada.
2. Os worktrees ficam fora da pasta do repositório. Proposta: `../SpellBook-wt/<feature>-<ticket>/`.
3. O checkout principal fica na `main` e serve aos documentos (Controlador, Criativo, Arquiteto).
4. O DevOps cria o worktree no início do ticket e o remove depois do merge, sempre com a confirmação do usuário.
5. A Fábrica trabalha só dentro do worktree do ticket. O Examinador revisa o diff desse worktree contra a `main`.
6. Worktrees isolam, não paralelizam: o WIP 1 (0003) e a trava única (0005) continuam valendo.

## Motivo

- Documentos e código não se misturam no stage.
- A `main` fica sempre limpa para ler ADRs e Specs aprovadas.
- Descartar um ticket é remover o worktree, sem tocar no trabalho de outros papéis.

## Consequências

- O DevOps entra no fluxo no início de cada ticket, não só no fim.
- Cada worktree precisa de um projeto host capaz de compilar e testar o pacote dele (0004). Se cada worktree tiver uma `Library/` própria, a primeira importação é lenta.
- A trava do Editor não pode ficar relativa ao worktree (0005).

## O que os agentes NÃO podem fazer

- A Fábrica implementar no checkout principal ou num worktree de outro ticket.
- Qualquer papel que não seja o DevOps criar, mover ou remover worktrees e branches.
- Remover um worktree com alterações não commitadas sem confirmação do usuário.
- Editar documentos de `Documentation~/` (exceto `manual/`, que acompanha o código) dentro do worktree do ticket.

## Em aberto (congelado enquanto a ADR estiver Adiada)

- Como o projeto host enxerga o worktree: um host por worktree (custo de importação) ou um host único cujo `manifest.json` aponta (`file:`) para o worktree ativo?
- O DevOps deve criar o worktree sem um pedido explícito do usuário a cada ticket, ou o "sobe só quando o usuário pedir" continua valendo também para isso?

## Relacionadas

- 0003 (um ticket, um PR), 0004 (projeto host de testes), 0005 (trava), 0008 (branches e commits).
