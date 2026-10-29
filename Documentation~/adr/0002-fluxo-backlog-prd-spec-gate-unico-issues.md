# 0002 — Fluxo backlog → PRD → Spec → gate único → issues no repositório do SpellBook

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

O time tem papéis separados (Controlador, Criativo, Arquiteto, Fábrica, Examinador, DevOps). Sem um fluxo único, código pode nascer sem registro do que se quer, de como fazer e de quais regras valem.

Hoje o fluxo está descrito em `Documentation~/README.md`, `TIME.md` e `backlog.md`, com diferenças entre eles. A skill `/issue` cria issues em outro repositório (B-020). O SpellBook é planejado sem foco em nenhum jogo: quem usa o pacote é o dev e o designer que trabalham no Inspector.

## Decisão

1. **Backlog.** Toda ideia, bug ou pendência entra primeiro em `Documentation~/backlog.md`, cuidado pelo Controlador. Estados: `bruta` → `cristalizada` → `em pesquisa` → `Spec aprovada` → `em execução` → `feito`.
2. **Duas vias.**
   - **Completa:** PRD → Spec (+ ADRs novas) → gate único → issues.
   - **Rápida:** um ticket só, sem PRD. A descrição do ticket (escopo, testes red, critério de pronto) vai direto na issue. A via rápida não pode criar ADR, quebrar API pública nem mudar formato serializado; se precisar, vira via completa.
3. **PRD** (Criativo, `/prd-feature`) na visão de quem usa o pacote. Os critérios de aceite são numerados e cada um vira teste automatizado ou cenário de Sample. A validação de uso (o antigo "playtest") acontece num projeto host ou Sample e é registrada na seção 9 do PRD (`/ajustar`).
4. **Spec** (Arquiteto) com contexto do código, desenho, quebras, riscos e tickets (ver 0003). ADRs novas são escritas junto.
5. **Gate único.** O usuário aprova PRD, Spec e ADRs novas numa única rodada. Nada vira issue antes disso.
6. **Issues.** Depois do gate, o Controlador cria uma issue por ticket **no repositório do SpellBook**, com links para a seção do PRD, o ticket da Spec, as ADRs e o DS.
7. **Um épico por vez**, começando pela Fase 0-M.

## Motivo

- Um gate só reduz idas e vindas com o usuário e garante que PRD, Spec e ADRs sejam aprovados coerentes entre si.
- Issues criadas depois do gate não precisam ser reescritas quando a Spec muda.
- Issues no repositório do pacote mantêm o histórico junto do código, sem depender de nenhum jogo.

## Consequências

- A skill `/issue` e a seção "Issues" do `TIME.md` precisam apontar para o repositório do SpellBook (B-020, B-022).
- O Arquiteto escreve a Spec a partir de um PRD fechado pelo Criativo, mas ainda não aprovado; se o gate pedir mudança no PRD, a Spec é revista na mesma rodada.
- O backlog cresce com itens `cristalizada` que esperam o épico atual terminar.

## O que os agentes NÃO podem fazer

- Criar issue antes do gate (via completa) ou sem confirmação do usuário (qualquer via).
- Implementar algo que não tenha issue e ticket.
- Usar a via rápida para quebra de API, mudança de formato serializado ou decisão que caberia numa ADR.
- Abrir um segundo épico em paralelo sem decisão do usuário.
- O Controlador ler código ou responder pergunta técnica; ela vai ao Arquiteto.

## Decisões incorporadas (2026-10-29)

- **Repositório das issues:** `gabrielctorres/SpellBook` (decisões I4 e I33; repositório público). A skill `/issue` passa a apontar para ele no ticket 0M-01 (B-020).
- **Labels** (decisão técnica do Arquiteto, para o Controlador aplicar): uma label de épico (`epic:0-M`, `epic:<slug>`), uma de tipo igual ao prefixo do Conventional Commit do ticket (`feat`, `fix`, `refactor`, `test`, `docs`, `ci`, `chore`, `build`) e `via:rapida` para tickets da via rápida. Motivo: filtrar o épico em andamento e casar a issue com o commit sem reescrever o título.
- **Gate parcial permitido** (decisão I35). O usuário pode aprovar parte da rodada: por exemplo, o PRD e um subconjunto de tickets, devolvendo o resto. Regras:
  - só vira issue o ticket aprovado **e** cujas dependências (tickets e ADRs) também estejam aprovadas;
  - uma ADR devolvida mantém `Proposta`, e nenhum ticket que dependa dela vira issue;
  - o que foi devolvido volta ao Arquiteto (ou ao Criativo) e passa de novo pelo gate, só na parte alterada.

## Em aberto

Nada.

## Relacionadas

- 0001 (pastas), 0003 (ticket, issue e PR), 0007 (gate de doc).
