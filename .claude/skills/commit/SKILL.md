---
name: commit
description: Passo do script da fila depois do APROVADO. Commita o ticket na branch do lote com stage seletivo; versão, CHANGELOG de release, tag e PR só quando o usuário pedir.
argument-hint: <design doc> <ticket> [release]
disable-model-invocation: true
---

Publique o ticket aprovado: $ARGUMENTS

O commit e o push do ticket aprovado na branch do lote fazem parte do fluxo (`AGENTS.md`, "Time de desenvolvimento"). Abrir PR, criar tag e publicar release só com a confirmação do usuário.

1. **Pré-condição.** Se o ticket tem código (`.cs`, `.asmdef`, `.json`, `.yml` de CI, `package.json` ou scripts `.ps1`, `.sh` e scripts da fila), existe revisão APROVADO para ele. Se é só de texto, o Controlador rodou `git diff --check` e conferiu o diff contra o objetivo e os CA da issue. Em ambos os casos, não há `.claude/editor.lock` na raiz do checkout. Se faltar algo, pare.
2. **Branch.** Commite na branch do lote, nunca na `main`. Num worktree (ADR 0005), commite no worktree e leve o commit para a branch do lote com `git cherry-pick`, um commit por ticket.
3. **Stage seletivo.** Adicione só os arquivos do Detalhe do ticket (`git add <caminhos>`), com os `.meta`, a seção do Detalhe no design doc e a linha do ticket em `Documentation~/decisoes.md`, se houver. Preserve as outras alterações do working tree.
4. **Commit.** Use a linha "Commit" do Detalhe: Conventional Commits no imperativo, assunto com até 72 caracteres, `!`/`BREAKING CHANGE` quando o ticket quebra algo. Nunca use `--no-verify`.
5. **Push** da branch do lote, sem force.
6. **Versão e CHANGELOG** (só com `release`). SemVer em `Packages/com.spellbook/package.json` (MAJOR para quebra, MINOR para feature, PATCH para fix) e uma seção nova em `Packages/com.spellbook/CHANGELOG.md` no formato existente (Quebras, Migração, Adicionado, Corrigido). Esses arquivos vão em um commit `chore: release vX.Y.Z` separado.
7. **PR** (só quando o usuário pedir). `gh pr create --base main` a partir da branch do lote, com motivação, abordagem e testes (skill `pr`) e os links das issues.
8. **Tag** (só com `release`, depois do merge). `git tag vX.Y.Z` e `git push origin vX.Y.Z`.

Nunca faça force-push em branch compartilhada nem reescreva histórico publicado.
