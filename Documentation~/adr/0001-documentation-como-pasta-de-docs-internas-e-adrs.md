# 0001 — `Documentation~/` como pasta de documentos internos e ADRs como registro de decisão

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

Os papéis do time (`Documentation~/TIME.md`) são agentes sem memória entre sessões. Tudo o que orienta o trabalho (o quê, como, regras) precisa estar escrito no repositório, e não em conversa.

O SpellBook é um pacote UPM. A Unity ignora pastas terminadas em `~`: elas não geram `.meta` e não entram no pacote importado. Hoje já existem `Documentation~/README.md`, `TIME.md`, `backlog.md` e `planos/`. Ainda há documentação dentro de `Runtime/` (`Runtime/GAS/GAS_Documentation.md`), que é importada junto com o código.

Os documentos divergem no nome da pasta das Specs: `Documentation~/README.md` e a decisão I.17 do plano usam `spec/`; `TIME.md`, o agente `arquiteto` e a skill `/implementar` usam `specs/`.

## Decisão

1. Todo documento interno do pacote fica em `Documentation~/`, com o índice em `Documentation~/README.md`:

   | Pasta ou arquivo | Conteúdo | Dono |
   |---|---|---|
   | `README.md` | Índice e fluxo | Controlador |
   | `backlog.md` | Porta de entrada do trabalho | Controlador |
   | `TIME.md` | Papéis e fluxo do time | Controlador |
   | `gdd/` | Visão do pacote (regras e sistemas, com selos implementado/parcial/proposta) | Criativo |
   | `prd/` | Um PRD por feature | Criativo |
   | `spec/` | Uma Spec por feature, com os tickets | Arquiteto |
   | `adr/` | Decisões que valem para o pacote inteiro, com índice em `adr/README.md` | Arquiteto |
   | `ds/` | Design System: nomes, padrões de API, Inspector, UI | Criativo + Arquiteto |
   | `planos/` | Estudos de origem | Arquiteto |
   | `manual/` | Manual público (ver 0007 e 0025) | Writer / Arquiteto |

2. Nenhum documento interno fica em `Runtime/`, `Editor/`, `Tests/` ou `Samples~/`. Na raiz do pacote ficam só os arquivos que o pacote publica: `README.md`, `CHANGELOG.md`, `LICENSE` (se houver) e `package.json`.
3. ADRs:
   - Um arquivo por decisão, `adr/NNNN-titulo-em-kebab.md`, numeração sequencial com 4 dígitos que nunca é reusada.
   - Status: `Proposta` → `Aceita` (somente pelo gate do usuário) → `Substituída por NNNN`. Uma proposta recusada fica como `Rejeitada`, com o motivo. Uma proposta que o usuário decide não adotar agora, mas pode retomar, fica como `Adiada`, com o motivo e a condição de retomada (exemplo: 0006).
   - Uma ADR aceita não é reescrita: ganha uma emenda datada no fim ou é substituída por outra.
   - Uma ADR `Proposta` não vale como regra até ser aceita.
4. Em conflito, vale a ADR aceita sobre o `CLAUDE.md`, como já diz o `Documentation~/README.md`.

## Motivo

- Agentes leem arquivos, não lembram de conversas. Um lugar só, com índice, evita a segunda verdade.
- A convenção `~` mantém os documentos versionados junto com o código sem poluir o pacote importado.
- ADRs imutáveis preservam o histórico de por que uma regra existe.

## Consequências

- `Runtime/GAS/GAS_Documentation.md` sai de `Runtime/` (já previsto no B-001) e o `.meta` dele deixa de existir.
- Os arquivos que citam `specs/` precisam ser alinhados ao nome escolhido (B-022): `TIME.md`, `.claude/agents/arquiteto.md`, `.claude/skills/implementar/SKILL.md`.
- Toda nova pasta de documento precisa entrar no índice `Documentation~/README.md`.

## O que os agentes NÃO podem fazer

- Criar documento interno fora de `Documentation~/`.
- Editar o corpo de uma ADR `Aceita`. Só cabe emenda datada ou ADR nova que a substitua.
- Mudar o status de uma ADR para `Aceita` sem a aprovação explícita do usuário no gate.
- Tratar uma ADR `Proposta` como regra em revisão (`/revisao`) ou em Spec.
- Reusar o número de uma ADR, mesmo rejeitada.

## Decisões incorporadas (2026-10-29)

- **Pasta das Specs: `spec/`** (decisão I35). `TIME.md`, `.claude/agents/arquiteto.md` e `.claude/skills/implementar/SKILL.md` são corrigidos no ticket 0M-02 da Spec [modularizacao](../spec/modularizacao.md) (B-022).

## Em aberto

Nada.

## Relacionadas

- 0002 (fluxo e gate), 0007 (documentação como código), 0025 (ferramenta do manual).
