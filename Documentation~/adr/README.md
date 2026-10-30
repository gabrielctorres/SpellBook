# ADRs do SpellBook

Decisões que valem para o pacote inteiro. Regras de formato e de status (`Proposta`, `Aceita`, `Adiada`, `Rejeitada`, `Substituída por NNNN`) estão na [0001](0001-documentation-como-pasta-de-docs-internas-e-adrs.md):

- Uma ADR `Proposta` não vale como regra até o usuário aceitá-la no gate.
- Uma ADR `Aceita` não é reescrita: ganha emenda datada ou é substituída por outra.
- Números nunca são reusados.

Formato de cada ADR: Status, Data, Contexto, Decisão, Motivo, Consequências, O que os agentes NÃO podem fazer, Em aberto (quando houver), Relacionadas.

## Processo

| Nº | Título | Status | Data |
|---|---|---|---|
| [0001](0001-documentation-como-pasta-de-docs-internas-e-adrs.md) | `Documentation~/` como pasta de documentos internos e ADRs como registro de decisão | Aceita | 2026-10-29 |
| [0002](0002-fluxo-backlog-prd-spec-gate-unico-issues.md) | Fluxo backlog → PRD → Spec → gate único → issues no repositório do SpellBook | Aceita | 2026-10-29 |
| [0003](0003-um-ticket-uma-issue-um-pr.md) | Um ticket = uma issue = um PR | Aceita | 2026-10-29 |
| [0004](0004-testes-primeiro-red-green.md) | Testes primeiro (red → green) | Aceita | 2026-10-29 |
| [0005](0005-editor-unico-com-trava.md) | Editor único com trava (editor-lock) | Aceita | 2026-10-29 |
| [0006](0006-worktrees-por-ticket.md) | Worktrees por ticket | Adiada (decisão I34) | 2026-10-29 |
| [0007](0007-documentacao-como-codigo-e-gate-de-doc.md) | Documentação como código e gate de doc no `/revisao` | Aceita | 2026-10-29 |
| [0008](0008-conventional-commits-e-versionamento-semantico.md) | Conventional Commits e versionamento semântico | Aceita | 2026-10-29 |
| [0027](0027-estrutura-do-repositorio-projeto-unity-na-raiz.md) | Estrutura do repositório: projeto Unity na raiz e pacote embutido em `Packages/com.spellbook/` | Aceita | 2026-10-30 |

## Arquitetura

| Nº | Título | Status | Data |
|---|---|---|---|
| [0009](0009-versao-3-0-com-quebras-livres.md) | Versão 3.0 com quebras livres | Aceita | 2026-10-29 |
| [0010](0010-unity-minima-6000-0.md) | Unity mínima 6000.0 | Aceita | 2026-10-29 |
| [0011](0011-odin-obrigatorio-so-atributos-no-runtime.md) | Odin obrigatório: só atributos no runtime, serialização nativa | Substituída por [0026](0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md) | 2026-10-29 |
| [0012](0012-mapa-de-modulos-e-asmdefs.md) | Mapa de módulos e asmdefs sem ciclos | Aceita | 2026-10-29 |
| [0013](0013-serializereference-como-costura-entre-modulos.md) | `[SerializeReference]` como costura entre módulos | Aceita | 2026-10-29 |
| [0014](0014-tempo-injetavel-itimesource.md) | Tempo injetável (`ITimeSource`) | Aceita | 2026-10-29 |
| [0015](0015-spawnerasset-como-seam-de-spawn-e-pool.md) | `SpawnerAsset` como ponto único de spawn e pool | Aceita | 2026-10-29 |
| [0016](0016-sistema-de-eventos-unificado.md) | Sistema de eventos unificado | Aceita | 2026-10-29 |
| [0017](0017-ui-em-tres-camadas.md) | UI em três camadas (fonte → binder → view) | Aceita como direção | 2026-10-29 |
| [0018](0018-hierarquia-unica-de-acoes.md) | Hierarquia única de ações (evento → ações → alvos) | Aceita como direção | 2026-10-29 |
| [0019](0019-hierarquia-unica-de-condition.md) | Hierarquia única de `Condition` (And/Or/Not) | Aceita como direção | 2026-10-29 |
| [0020](0020-valores-por-nivel-scalablefloat.md) | Valores por nível (`ScalableFloat` em array ou curva) | Aceita como direção | 2026-10-29 |
| [0021](0021-magnitude-configuravel-e-pilhas.md) | Magnitude configurável (snapshot ou ao vivo) e pilhas | Aceita como direção | 2026-10-29 |
| [0022](0022-ordem-das-checagens-de-ativacao.md) | Ordem das checagens de ativação | Aceita como direção | 2026-10-29 |
| [0023](0023-texto-estruturado-e-itextprovider.md) | Texto estruturado + `ITextProvider` | Aceita como direção | 2026-10-29 |
| [0024](0024-pipeline-generico-de-dano.md) | Pipeline genérico de dano | Aceita como direção | 2026-10-29 |
| [0025](0025-documentacao-em-mkdocs-ou-zensical-no-github-pages.md) | Documentação em MkDocs Material ou Zensical, publicada no GitHub Pages | Aceita | 2026-10-29 |
| [0026](0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md) | SpellBook sem Odin: editor próprio em UI Toolkit (substitui a 0011) | Aceita | 2026-10-29 |
