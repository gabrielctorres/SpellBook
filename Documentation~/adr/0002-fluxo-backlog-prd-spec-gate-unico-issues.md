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

## Emenda (2026-11-03): Spec "just-in-time"

**Status da emenda: Aceita** (decisão do usuário, aprovada pelo usuário em 2026-11-03).

**Contexto.** Nos lotes 7 a 9, Specs escritas lotes antes chegaram à fila com o código já mudado. O resultado foram devoluções e divisões: o EV3 voltou 4 vezes, e o EV3c, o EV1a e o EV1b, o EV3c e o EV3d, o DM1a e o DM1b, o VN2a e o VN2b, o AV1a e o AV1b e o AV4a e o AV4b nasceram de recontagens.

**Regra.**
1. **Spec de longo prazo (item 4 da Decisão).** No gate, o ticket da Spec leva só o objetivo, os critérios de aceite (CA do PRD ou critério de pronto verificável), as dependências e as ADRs que valem. Escopo de arquivos, testes red por nome, tamanho estimado e caminhos com linha ficam de fora.
2. **Detalhamento na entrada.** Antes de o ticket entrar na fila, o Controlador pede o detalhamento ao Arquiteto. O Arquiteto lê o código atual e escreve, no ticket da Spec, uma subseção "Detalhe (AAAA-MM-DD)" com escopo, testes red, critério de pronto e tamanho (formato e limites da 0003). O Controlador copia o detalhe para a issue.
3. **Gate do detalhe.** O detalhe não passa por gate novo quando mantém o objetivo, os CA e as ADRs. Volta ao gate (parcial, só aquela parte) quando:
   - muda um CA;
   - pede ADR nova ou emenda;
   - quebra API ou formato serializado que a Spec não previa;
   - passa dos limites da 0003. Nesse caso valem as regras de exceção dela, como a exceção automática de até ~30%.
4. **Tickets já detalhados.** Os tickets que já têm escopo, testes e tamanho nas Specs e ainda não entraram na fila ficam como estão. O detalhamento deles vale só como rascunho e é refeito pelo item 2 na entrada, sem reescrever o texto antigo.
5. **Issues (item 6 da Decisão).** A issue pode ser criada depois do gate só com objetivo e CA. O detalhe entra nela pelo item 2.

**Consequências.**
- Uma recontagem na entrada não é devolução nem emenda: é o passo normal do fluxo.
- O Arquiteto entra no ciclo de cada ticket, uma vez, antes da Fábrica.
- A Fábrica implementa pelo "Detalhe" mais recente do ticket. Se ele não existir, devolve ao Controlador.

## Emenda (2026-11-03): design doc por épico e papéis enxutos

**Status da emenda: Aceita** (decisão do usuário, aprovada pelo usuário em 2026-11-03). Revê a Decisão e a emenda "Spec just-in-time" acima. Vem com a 0042, que substitui a 0003, e com a emenda de 2026-11-03 da 0005.

1. **Design doc no lugar de PRD + Spec** (itens 3 e 4 da Decisão). Cada épico tem um design doc em `Documentation~/spec/<épico>.md`, pelo modelo `spec/_modelo-design-doc.md`, com duas partes:
   - **parte fixa**, de 1 a 2 páginas: o problema, os critérios de aceite (CA) numerados e o cenário no Sandbox (`Packages/com.spellbook/Samples~/Sandbox`) que prova o épico, mais a lista de tickets com objetivo, CA e dependências. Ela muda raramente, e mudar um CA volta ao gate;
   - **parte JIT**: uma seção "Detalhe (AAAA-MM-DD)" por ticket, escrita lendo o código na hora em que o ticket entra na fila.
2. **ADR** só para decisão que vale para mais de um épico. A decisão de um épico só fica no design doc.
3. **Gate único** (item 5 da Decisão): o usuário aprova a parte fixa e as ADRs novas. O gate parcial continua como está.
4. **Detalhe** (item 2 da emenda "Spec just-in-time"): passa do Arquiteto para a Fábrica, que detalha, implementa e testa o mesmo ticket. O Detalhe volta ao gate só nos casos do item 3 daquela emenda, menos o tamanho, que segue a 0042: até +30% com uma linha no `decisoes.md`, e acima disso o ticket se divide.
5. **Épicos que já têm PRD e Spec** não são reescritos. A Spec faz o papel do design doc, e o PRD o da parte fixa. O Detalhe entra no ticket da Spec, na entrada de cada ticket. Um detalhe escrito antes de um commit que mudou o código do escopo vale só como rascunho.
6. **Papéis** (Contexto e "O que os agentes NÃO podem fazer"):
   - fixos: a Fábrica (detalha, implementa e testa) e o Examinador (revisor separado, que confere contra os CA e o Detalhe, roda a suíte e é o único gate do ticket);
   - sob demanda: o Desenhista, fusão do Arquiteto com o Criativo, só quando nasce um épico, para a parte fixa e as ADRs; e o Prisma, para shaders;
   - o DevOps deixa de ser papel. O commit é um passo do script da fila depois do APROVADO (skill `/commit`);
   - o Controlador é a sessão principal: monta a fila, cria as issues e leva os gates ao usuário. Não lê código. Pergunta técnica vai para a Fábrica do ticket ou, se for de design, para o Desenhista.
7. **Fonte única.** As regras de processo vigentes ficam no `CLAUDE.md`, seção "Time de desenvolvimento". O `TIME.md` e a parte de processo do `guia-do-manual.md` viram ponteiros para ela. Se o `CLAUDE.md` divergir de uma ADR, vale a ADR, e o `CLAUDE.md` é corrigido.
8. **Registro.** Toda decisão tomada sem o usuário e toda exceção de tamanho entram numa linha de `Documentation~/decisoes.md`, no formato `data | ticket | decisão | base | ADR afetada`.
9. **Backlog** (item 1 da Decisão): o estado `Spec aprovada` passa a se chamar `design aprovado`.
10. **Via rápida** (item 2 da Decisão): o ticket avulso não tem design doc, e o Detalhe vai na issue. As restrições da via rápida não mudam.

**Fora desta emenda:** juntar módulos ou asmdefs (0012 não muda).

**Aplicação:** ticket PS1 da `spec/processo-enxuto.md`. Até o PS1 entrar na branch, os agentes e as skills antigos continuam valendo.

## Emenda (2026-11-03): Codex e Antigravity como papéis externos

**Status da emenda: Aceita** (decisão do usuário, aprovada pelo usuário em 2026-11-03). Revê o item 6 da emenda "design doc por épico e papéis enxutos". Vale a partir do próximo lote.

**Contexto.** Todo o trabalho do time roda hoje no Claude Code, inclusive o que não precisa da Unity nem de julgamento de design: docs internas, scripts, CI, refatorações mecânicas, o texto do PR, pesquisa e contagem de tamanho antes do Detalhe. Esse trabalho gasta a mesma cota dos papéis que implementam e revisam. O Codex e o Antigravity rodam fora do Claude Code, lançados pelo Orca, e leem o `AGENTS.md` da raiz, mas não `.claude/agents/` nem `.claude/skills/`. Os testes do pacote são todos testes da Unity, então nenhum dos dois compila nem testa o C#.

**Regra.**
1. **Codex** (externo, Orca `--agent codex`):
   - faz: tickets da via rápida que não precisam da Unity (docs internas, scripts, CI, skills e agentes, `.md`); refatorações mecânicas, inclusive em `.cs`; e o texto do PR da branch do lote;
   - trabalha sempre num worktree próprio criado a partir da branch do lote. Não pega a trava de Editor (0005: ticket sem Unity não pega trava) e não divide arquivo com outro ticket em andamento;
   - não roda a Unity e não commita. O texto do PR vai para o Controlador, que abre o PR;
   - o ticket do Codex passa por `/revisao` do Examinador, que continua o único gate, e só depois pelo `/commit`. Uma refatoração em `.cs` só é verificada pela suíte do Examinador, então ele a trata como ticket com Unity;
   - não escreve o Detalhe de ticket de design doc, não cria ADR e segue as restrições da via rápida.
2. **Antigravity** (externo, Orca `--agent antigravity`):
   - só leitura: pesquisa, mapa do código e medição de tamanho de um ticket antes do Detalhe, em linhas e arquivos pela regra da 0042;
   - entrega o resultado ao Controlador, que o repassa à Fábrica. A Fábrica continua dona do Detalhe e confere a medição lendo o código;
   - não edita arquivo e não usa git.
3. **Claude Code.** O Controlador continua no Claude Code e abre com `claude --agent controlador`, em Sonnet. A Fábrica, o Examinador e o Desenhista continuam no Claude Code, em Opus (o `model` do agente não depende do modelo da sessão).
4. **Contas.** O Codex tem duas contas no Orca. A conta de cada painel é escolhida na interface do Orca; quando uma bate o limite, usa-se a outra.

**Motivo.**
- Tira do Claude Code o trabalho que não pede Unity nem julgamento de design, sem mudar o gate: todo código e todo texto que entra na branch passa pelo Examinador.
- A medição antes do Detalhe reduz recontagens, mas a Fábrica confere, porque o Detalhe é dela e a 0042 é medida lá.
- O Controlador só orquestra, então Sonnet basta e economiza cota.

**Consequências.**
- O texto de processo que o Codex e o Antigravity precisam seguir fica no `AGENTS.md`, porque eles não leem `.claude/`.
- Um ticket do Codex com `.cs` só descobre erro de compilação na revisão. Por isso o Codex fica em refatoração mecânica, e mudança de comportamento continua com a Fábrica.
- Nasce o agente `.claude/agents/controlador.md`, com `model: sonnet` e todas as ferramentas.

## Em aberto

Nada.

## Relacionadas

- 0001 (pastas), 0003 (ticket, issue e PR, substituída pela 0042), 0005 (trava e WIP), 0007 (gate de doc), 0042 (tamanho do ticket).
