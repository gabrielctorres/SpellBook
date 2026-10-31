---
name: revisao
description: Examinador revisa um ticket implementado (padrões, Spec, ADR, DS), roda os testes sob o editor-lock e dá o veredito APROVADO (nota ≥ 80% e zero achados) ou REPROVADO. Use depois de /implementar.
argument-hint: <spec> <ticket>
---

Revise o ticket: $ARGUMENTS

1. **Diff.** `git diff` (e `git diff --stat`) contra o último commit da `main`. Confirme que só os arquivos do escopo do ticket mudaram e que o diff cabe nos limites da ADR 0003:
   - até ~500 linhas: adicionadas mais removidas no diff de código (`.cs`, `.asmdef`, `.json`, `.yml` de CI, `package.json`);
   - até 10 arquivos: caminhos distintos no diff, e um arquivo movido conta uma vez;
   - `.meta` não conta; páginas de `Documentation~/manual/` e fixtures contam só nos arquivos;
   - remoção pura (arquivo apagado por inteiro, sem substituto no mesmo ticket) não conta em nenhum dos dois, mas é revisada pela lista de caminhos.
   - movimento puro (arquivo movido sem alteração de conteúdo, `R100` no `git diff -M --name-status`) conta 1 linha e fica fora dos 10 arquivos; confira com `git diff -M --name-status`;
   - ticket em paralelo (emenda de 2026-10-29 da ADR 0003): só é aceito se não usa a Unity nem a trava, se o par está marcado na Spec e se não toca arquivo do outro ticket em andamento.
2. **Checar quatro eixos.** Cada eixo vale 25%:
   - **Padrões:** regras do `CLAUDE.md` (C#, Unity, testes, segurança), `.meta` presentes, sem suppressions, sem dependência nova.
   - **Spec:** o ticket (`Documentation~/spec/<feature>.md`) faz o que pede, todos os critérios de pronto são atendidos e não há nada a mais.
   - **ADR:** respeita as decisões aceitas em `Documentation~/adr/`.
   - **DS:** segue nomes, padrões de API e de Inspector de `Documentation~/ds/`. Enquanto `Documentation~/ds/` não existir, confira nomes, padrões de API e de Inspector contra as ADRs aceitas (0011, 0026) e o código vizinho do mesmo módulo, e escreva no veredito "DS: sem DS, conferido por ADR e vizinhança". O critério de nota não muda.
3. **Testes** (ADR 0005). Ticket sem Unity (docs, processo): rode o critério verificável do ticket e não crie a trava. Ticket com Unity:
   1. Se `.claude/editor.lock` existir, não rode: avise o Controlador e reprove com "testes não executados (lock ativo)".
   2. Senão, crie a trava com uma linha `examinador; <ticket>; <horário ISO-8601>; <caminho do host>`.
   3. Rode os testes em batchmode no projeto da raiz, como em `/implementar` (o timeout de 600 s do PlayMode e a limpeza de `Assets/InitTestScene*` valem aqui também), e cole o resumo (total, passou, falhou). Não abra o Editor interativo e não altere assets.
   4. Remova a trava, também em caso de erro.
4. **Veredito.**

```markdown
## Revisão <spec> <ticket>
Nota: <0–100>%  (Padrões x/25 · Spec x/25 · ADR x/25 · DS x/25)
Testes: <n passou / n falhou>
Veredito: APROVADO | REPROVADO

### Achados
1. `arquivo:linha` — <problema>. Regra: <fonte>. Cenário de falha: <entrada → resultado errado>.
```

APROVADO exige nota ≥ 80%, zero achados e testes verdes (em ticket sem Unity, o critério verificável passando). Não corrija nada (a única escrita permitida é a trava); os achados voltam para a Fábrica. Depois do APROVADO, o DevOps cria a branch, commita e abre o PR (`/commit`).
