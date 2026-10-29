# 0003 — Um ticket = uma issue = um PR

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

Diffs grandes são difíceis de revisar e escondem regressões. O Examinador dá veredito por ticket (`/revisao`), e o DevOps publica por ticket (`/commit`). O `CLAUDE.md` pede PRs pequenos, um commit por mudança lógica e plano aprovado antes de mudar mais de três arquivos.

## Decisão

1. Cada ticket da Spec:
   - entrega um comportamento testável sozinho;
   - depende só de tickets anteriores da mesma Spec ou de Specs já entregues;
   - tem até **~400 linhas de diff** e até **10 arquivos**. Arquivos `.meta` não contam nos dois limites.
2. Um ticket vira exatamente uma issue, uma branch (`feat/<feature>-<ticket>` ou `fix/<feature>-<ticket>`) e um PR contra a `main`. A descrição do PR traz motivação, abordagem, testes e o link da issue.
3. **WIP 1:** só um ticket em implementação. A Fábrica só pega o próximo depois do APROVADO do Examinador.
4. Se, na implementação, o ticket passar dos limites ou exigir arquivo fora do escopo, a Fábrica para e devolve ao Controlador, que leva ao Arquiteto para dividir o ticket.
5. O ticket aprovado no gate (0002) vale como o plano exigido pelo `CLAUDE.md` para mudanças em mais de três arquivos.

## Motivo

- Revisão de verdade só acontece em diffs pequenos.
- Rastreabilidade simples: issue ↔ ticket ↔ PR ↔ entrada no CHANGELOG.
- Reverter um PR pequeno é barato.

## Consequências

- Specs grandes viram muitos tickets; o Arquiteto precisa ordenar dependências com cuidado.
- Mudanças mecânicas grandes (mover arquivos entre asmdefs, por exemplo) podem precisar de vários tickets em sequência.

## O que os agentes NÃO podem fazer

- Juntar dois tickets num mesmo PR ou commit.
- Incluir no diff arquivos fora do escopo do ticket, inclusive "aproveitando" para refatorar.
- Pegar um segundo ticket com outro em implementação ou em revisão.
- O Examinador aprovar um diff que passe dos limites ou toque arquivos fora do escopo.

## Decisões incorporadas (2026-10-29)

- **Páginas do manual e fixtures** (decisão I35): páginas em `Documentation~/manual/` e fixtures de teste (YAML de assets, arquivos de dados) **não contam** no limite de ~400 linhas, mas **contam** no limite de 10 arquivos.
- **Contagem** (decisão técnica do Arquiteto, para o Examinador aplicar sempre do mesmo jeito): "linhas" são as linhas adicionadas mais as removidas no diff de código (`.cs`, `.asmdef`, `.json`, `.yml` de CI, `package.json`), sem `.meta`, páginas do manual e fixtures. "Arquivos" são os caminhos distintos no diff (criados, alterados, movidos ou apagados), sem `.meta`. Um arquivo movido conta uma vez.

- **Remoção pura** (decisão I42): um arquivo apagado por inteiro, sem substituto no mesmo ticket, não conta no limite de 10 arquivos nem no de ~400 linhas. Ele continua listado no PR e é revisado pela lista de caminhos. Um arquivo apagado que tem substituto no mesmo ticket (por exemplo, movido ou reescrito com outro nome) conta normalmente.

## Em aberto

Nada.

## Relacionadas

- 0002 (fluxo), 0004 (testes primeiro), 0006 (worktrees), 0008 (commits e versão).
