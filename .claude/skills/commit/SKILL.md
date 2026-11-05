---
name: commit
description: Passo do script da fila depois do APROVADO. Commita o ticket na branch do lote com stage seletivo; no passe de documentação, também traz ao checkout do ticket o commit do change request, só por avanço rápido. Versão, CHANGELOG de release, tag e PR só quando o usuário pedir.
argument-hint: <design doc> <ticket> [change-request|release]
disable-model-invocation: true
---

Publique o ticket aprovado: $ARGUMENTS

O commit e o push do ticket aprovado na branch do lote fazem parte do fluxo (`AGENTS.md`, "Time de desenvolvimento"). Abrir PR, criar tag e publicar release só com a confirmação do usuário.

**Com `change-request`** (passe de documentação, depois do merge do change request no rascunho; 0044, item 13). Faça só estes passos e pare:

1. **Pré-condição.** A Fábrica avisou o merge do change request do ticket; não há `.claude/editor.lock` na raiz do checkout; `git status --porcelain -- AGENTS.md CLAUDE.md` está vazio. Se faltar algo, pare.
2. Na correção (um change request novo do mesmo ticket), o checkout pode ter a saída da geração anterior em `Documentation~/manual/`. Ela é refeita pela geração, então descarte-a: `git restore --staged --worktree -- Documentation~/manual`. Fora desse caso, `git status --porcelain -- Documentation~/manual` precisa estar vazio; se não estiver, pare.
3. `git fetch origin <branch do lote>`.
4. Confira o que chega: `git log --format='%h %an %s' HEAD..origin/<branch do lote>`. Para cada commit cujo assunto começa por `GITBOOK-`, o assunto cita o ticket, `git show --format= --name-only <commit>` lista só caminhos em `Documentation~/manual/`, mais o `AGENTS.md` e o `CLAUDE.md` (bloco da decisão 2 da emenda de 2026-11-05 da 0044), e `git show --check --format= <commit>` não acusa nada. Se algo falhar, pare e devolva ao Controlador com a saída.
5. Avanço rápido, sem merge nem rebase: `git merge --ff-only origin/<branch do lote>`. Num worktree (ADR 0005), faça o mesmo também no checkout da branch do lote, para o cherry-pick do item 2 cair em cima do commit do GitBook. Se o avanço rápido não der, pare e devolva ao Controlador.
6. Relate os commits trazidos (hash, autor e assunto) e os caminhos de cada um. Não commite: o próximo passo é a geração, pela Fábrica.

Para os outros modos, siga os passos abaixo:

1. **Pré-condição.** Se o ticket tem código (`.cs`, `.asmdef`, `.json`, `.yml` de CI, `package.json` ou scripts `.ps1`, `.sh` e scripts da fila), existe revisão APROVADO para ele. Se é só de texto, o Controlador rodou `git diff --check` e conferiu o diff contra o objetivo e os CA da issue. Em ambos os casos, não há `.claude/editor.lock` na raiz do checkout. Se faltar algo, pare.
   No passe de documentação, o commit do change request do ticket já chegou ao checkout pelo modo `change-request`.
2. **Branch.** Commite na branch do lote, nunca na `main`. Num worktree (ADR 0005), commite no worktree e leve o commit para a branch do lote com `git cherry-pick`, um commit por ticket.
   Antes do cherry-pick, a branch do lote precisa estar em dia com `origin` por avanço rápido (`git merge --ff-only origin/<branch do lote>`); se não der, pare.
3. **Stage seletivo.** Adicione só os arquivos do Detalhe do ticket (`git add <caminhos>`), com os `.meta`, a seção do Detalhe no design doc e a linha do ticket em `Documentation~/decisoes.md`, se houver. No passe, entram também as páginas e o catálogo que a geração mudou em `Documentation~/manual/`. Preserve as outras alterações do working tree.
4. **Commit.** Use a linha "Commit" do Detalhe: Conventional Commits no imperativo, assunto com até 72 caracteres, `!`/`BREAKING CHANGE` quando o ticket quebra algo. Nunca use `--no-verify`.
5. **Push** da branch do lote, sem force.
6. **Versão e CHANGELOG** (só com `release`). SemVer em `Packages/com.spellbook/package.json` (MAJOR para quebra, MINOR para feature, PATCH para fix) e uma seção nova em `Packages/com.spellbook/CHANGELOG.md` no formato existente (Quebras, Migração, Adicionado, Corrigido). Esses arquivos vão em um commit `chore: release vX.Y.Z` separado.
7. **PR** (só quando o usuário pedir). `gh pr create --base main` a partir da branch do lote, com motivação, abordagem e testes (skill `pr`) e os links das issues.
8. **Tag** (só com `release`, depois do merge). `git tag vX.Y.Z` e `git push origin vX.Y.Z`.

Nunca faça force-push em branch compartilhada nem reescreva histórico publicado.
