# 0008 — Conventional Commits e versionamento semântico

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

O `CLAUDE.md` exige Conventional Commits. O `CHANGELOG.md` já segue Keep a Changelog e SemVer, com as seções Quebras, Migração, Adicionado e Corrigido, e o `package.json` está em `2.0.0`. A skill `/commit` faz o release num commit separado. Para um pacote UPM, quebra não é só assinatura de método: mudar o tipo, o nome ou o assembly de uma classe serializada também quebra os assets de quem usa o pacote.

## Decisão

1. **Commits:**
   - Conventional Commits: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`; escopo opcional com o módulo (`feat(gas): ...`, `fix(events): ...`).
   - Assunto no imperativo, até 72 caracteres.
   - Quebra marcada com `!` e rodapé `BREAKING CHANGE:` descrevendo a migração.
   - Um commit por mudança lógica; nunca `--no-verify`.
2. **Versão (SemVer no `package.json`):**
   - **MAJOR:** quebra de API pública, de formato serializado (renomear campo serializado, mover ou renomear tipo usado em `[SerializeReference]`, mudar valor de enum serializado), remoção de componente ou asset, ou aumento da Unity mínima.
   - **MINOR:** funcionalidade nova compatível.
   - **PATCH:** correção compatível.
3. **CHANGELOG:** uma seção por versão, com Quebras, Migração, Adicionado e Corrigido. Quebras sempre vêm com o passo de migração.
4. **Release:** commit `chore: release vX.Y.Z` separado, com `package.json` e `CHANGELOG.md`. A tag `vX.Y.Z` é criada depois do merge na `main`.

## Motivo

- Mensagens padronizadas permitem montar o CHANGELOG e achar quebras no histórico.
- Tratar formato serializado como API protege os assets de quem usa o pacote, que não aparecem em erro de compilação.

## Consequências

- Durante o ciclo da 3.0 (0009), a `main` acumula quebras antes do `3.0.0` publicado.
- O Arquiteto precisa apontar na Spec toda mudança de formato serializado, porque ela define a versão.

## O que os agentes NÃO podem fazer

- Publicar quebra (inclusive de formato serializado) fora de uma MAJOR.
- Commitar quebra sem `!`/`BREAKING CHANGE` e sem passo de migração.
- Alterar `version` do `package.json` fora do commit de release.
- Criar tag antes do merge ou sem confirmação do usuário.
- Reescrever histórico publicado ou fazer force-push em branch compartilhada.

## Decisões incorporadas (2026-10-29)

- **Pré-versões por épico** (decisão I24): cada épico da 3.0 fecha com um release `3.0.0-pre.N` (N começa em 1 e sobe a cada épico). A `3.0.0` final sai quando GAS e UI estiverem completos. O `version` do `package.json` continua mudando só no commit de release.
- **Escopos de commit** (decisão técnica): o nome do módulo em minúsculas (`core`, `time`, `events`, `pooling`, `tags`, `gas`, `gas-bridges`, `setup`), mais `manual`, `ci`, `host` e `process` para o que não é módulo. Tipos aceitos além dos do `CLAUDE.md`: `build` (asmdef, `package.json`) e `ci` (workflows), comuns no Conventional Commits.

- **2.x congelada** (decisão I43): a 2.x não recebe correções e não existe branch `release/2.x`. Toda correção entra na linha 3.0.

## Em aberto

Nada.

## Relacionadas

- 0003 (um ticket, um PR), 0009 (3.0 com quebras livres), 0010 (Unity mínima).
