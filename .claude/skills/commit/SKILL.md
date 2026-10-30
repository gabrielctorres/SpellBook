---
name: commit
description: DevOps faz commit, atualiza versão e CHANGELOG, cria tag e abre PR contra a main para um ticket aprovado. Só roda por invocação manual do usuário.
argument-hint: <spec> <ticket> [release]
disable-model-invocation: true
---

Publique o ticket aprovado: $ARGUMENTS

Confirme com o usuário antes de cada passo que altera estado (branch, commit, push, tag, PR).

1. **Pré-condição.** Existe revisão APROVADO para o ticket e `.claude/editor.lock` não existe. Se faltar algo, pare.
2. **Branch.** Se estiver na `main`, crie `feat/<feature>-<ticket>` ou `fix/<...>`. Faça `git pull --rebase` da `main` antes, se a branch não tiver sido publicada.
3. **Stage seletivo.** Adicione só os arquivos do ticket (`git add <caminhos>`), incluindo os `.meta`. Preserve as outras alterações do working tree.
4. **Commit.** Conventional Commits no imperativo, assunto com até 72 caracteres, `!`/`BREAKING CHANGE` quando a Spec indicar quebra. Termine com a linha de atribuição da sessão. Nunca use `--no-verify`.
5. **Versão e CHANGELOG** (só com `release`). SemVer em `Packages/com.spellbook/package.json` (MAJOR para quebra, MINOR para feature, PATCH para fix) e uma seção nova em `Packages/com.spellbook/CHANGELOG.md` no formato existente (Quebras, Migração, Adicionado, Corrigido). Esses arquivos vão em um commit `chore: release vX.Y.Z` separado.
6. **PR.** `git push -u origin <branch>` e `gh pr create --base main`, com a descrição contendo motivação, abordagem e testes (use a skill `pr`) e o link da issue.
7. **Tag** (só com `release`, depois do merge). `git tag vX.Y.Z` e `git push origin vX.Y.Z`.

Nunca faça force-push em branch compartilhada nem reescreva histórico publicado.
