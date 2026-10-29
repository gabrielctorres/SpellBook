---
name: issue
description: Controlador cria uma issue no repositório gabrielctorres/SpellBook, com as labels de épico, tipo e via (ADR 0002). Use para registrar um ticket aprovado no gate ou um item avulso (bug, tarefa) do SpellBook.
argument-hint: <ticket da Spec, ex. 0M-03, ou descrição curta>
---

Crie uma issue do SpellBook a partir de: $ARGUMENTS

Repositório: `gabrielctorres/SpellBook`. Todo comando `gh` leva `--repo gabrielctorres/SpellBook`.

1. **Pré-condições** (ADR 0002):
   - Via completa: só crie a issue de um ticket aprovado no gate, cujas dependências (tickets e ADRs) também estejam aprovadas.
   - Via rápida: a descrição do ticket (escopo, testes red, critério de pronto) vai direto no corpo da issue.
2. **Monte o rascunho.**
   - Título:
     - ticket da Spec: `<ticket>: <tipo>(<escopo>): <resumo>`, igual ao título do ticket. Ex.: `0M-01: chore(process): apontar a skill /issue para gabrielctorres/SpellBook`;
     - item avulso: Conventional Commits, `<tipo>(<escopo>): <resumo no imperativo>`.
   - Labels:
     - épico: `epic:<slug>` (ex. `epic:0-M`). Item avulso fora de épico fica sem essa label;
     - tipo: o prefixo Conventional Commits do título (`feat`, `fix`, `refactor`, `test`, `docs`, `ci`, `build`, `chore`);
     - `via:rapida` quando o ticket for da via rápida.
   - Corpo:
     - ticket da Spec: **Objetivo**, **Arquivos**, **Depende de**, **Testes / critério verificável**, **Links** (seção do PRD, ticket da Spec, ADRs e DS);
     - item avulso: **Contexto**, **Comportamento esperado**, **Comportamento atual** (para bug), **Critérios de aceite**, **Próximo papel** (Criativo para feature nova, Arquiteto para bug ou feature com PRD pronto).
3. **Confira duplicatas e labels.**
   - `gh issue list --repo gabrielctorres/SpellBook --state all --search "<termos>"`
   - `gh label list --repo gabrielctorres/SpellBook` para confirmar que as labels existem. Se faltar alguma (ex. `fix` ou um `epic:<slug>` novo), pergunte ao usuário antes de criá-la.
4. **Mostre o rascunho ao usuário e só crie depois da confirmação** (é uma ação externa). Grave o corpo num arquivo no scratchpad e rode:
   `gh issue create --repo gabrielctorres/SpellBook --title "<título>" --label "epic:<slug>" --label "<tipo>" [--label "via:rapida"] --body-file <arquivo no scratchpad>`
5. Devolva o link da issue e em qual posição da fila ela entrou.

O Controlador não lê código para escrever a issue. Se faltar contexto técnico, deixe a pergunta registrada para o Arquiteto.
