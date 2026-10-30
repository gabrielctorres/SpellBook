# Time de desenvolvimento

Cada papel é um agente em `.claude/agents/` e pode rodar de dois jeitos:

- **Subagente:** o Controlador (sessão principal) aciona o papel pela Agent tool.
- **Terminal dedicado:** `claude --agent <papel>` abre uma sessão que já é aquele papel.

| Papel | Agente | Função | Não faz |
|---|---|---|---|
| Controlador | sessão principal (regras no `CLAUDE.md`) | Triagem, backlog (`backlog.md`), fila e WIP. Cria issues (`/issue`), abre e fecha terminais, leva dúvidas e gates ao usuário | Não lê código, não implementa, não commita |
| Criativo | `criativo` | Cristaliza ideias, escreve o PRD na visão de quem usa o pacote (`/prd-feature`), GDD e DS. Registra a validação de uso (`/ajustar`, seção 9 do PRD) | Código e Spec |
| Arquiteto | `arquiteto` | Pesquisa o código (só lê), escreve a Spec com os tickets (≤ ~500 linhas e ≤ 10 arquivos cada) e as ADRs | Implementar |
| Fábrica | `fabrica` | Implementa um ticket por vez (`/implementar`): testes red primeiro, depois o mínimo de código. Única que abre o Editor ou roda a Unity para implementar (editor-lock) | PRD, Spec, git |
| Examinador | `examinador` | Revisa (`/revisao`: padrões, Spec, ADR, DS) e roda os testes em batchmode, sob a trava. Veredito APROVADO com nota ≥ 80% e zero achados | Implementar |
| DevOps | `devops` | Git: branch, commit, versão, tag, PR contra a `main` (`/commit`), rebase e release. Sobe em modo manual | Código |
| Prisma | `prisma` | Shaders e trilha de arte técnica | — |

## Fluxo

O fluxo segue a ADR 0002. Toda ideia, bug ou pendência entra primeiro no backlog.

```
ideia ─► backlog.md (Controlador)          bruta ─► cristalizada ─► em pesquisa ─► Spec aprovada ─► em execução ─► feito
      ─► /prd-feature (Criativo)           Documentation~/prd/<feature>.md
      ─► Spec + ADRs (Arquiteto)           Documentation~/spec/<feature>.md, Documentation~/adr/NNNN-*.md
      ─► gate único (usuário)              PRD + Spec + ADRs novas numa rodada (parcial permitido)
      ─► /issue (Controlador)              uma issue por ticket aprovado, em gabrielctorres/SpellBook
      ─► /implementar <ticket> (Fábrica)   red ─► green, um ticket por vez
      ─► /revisao <ticket> (Examinador)    APROVADO ou REPROVADO com achados ─► volta para a Fábrica
      ─► /commit (DevOps)                  branch, Conventional Commits, PR contra a main ─► merge pelo usuário
validação de uso ─► /ajustar (Criativo)    seção 9 do PRD ─► nova rodada no Arquiteto
```

**Duas vias** (0002):
- **Completa:** PRD → Spec (+ ADRs novas) → gate único → issues.
- **Rápida:** um ticket só, sem PRD. A descrição do ticket (escopo, testes red, critério de pronto) vai direto na issue. A via rápida não cria ADR, não quebra API pública e não muda formato serializado; se precisar, vira via completa.

**Gates com o usuário** (o Controlador sempre pergunta antes):
- aprovar PRD, Spec e ADRs novas, num **gate único**;
- criar issue;
- abrir PR;
- publicar tag/release.

**Gate parcial** (0002): o usuário pode aprovar só parte da rodada, por exemplo o PRD e alguns tickets.
- Só vira issue o ticket aprovado cujas dependências (tickets e ADRs) também estejam aprovadas.
- Uma ADR devolvida continua `Proposta`, e nenhum ticket que dependa dela vira issue.
- O que foi devolvido volta ao Arquiteto (ou ao Criativo) e passa de novo pelo gate, só na parte alterada.

**WIP:** no máximo um ticket com Unity em implementação ou revisão (0003). A Fábrica não pega o
próximo ticket com Unity antes de o Examinador aprovar o atual. Tickets sem Editor (docs, processo,
CI) podem rodar em paralelo, nas condições da emenda de 2026-10-29 da 0003 (ver "Ticket, branch e PR").

## Ticket, branch e PR

Um ticket = uma issue = uma branch = um PR (0003).

- **Limites:** até ~500 linhas e até 10 arquivos por ticket (emenda de 2026-10-30 da 0003). Se passar disso ou precisar de arquivo fora do escopo, a Fábrica para e devolve ao Controlador, que leva ao Arquiteto.
- **Contagem** (0003):
  - "linhas" são as adicionadas mais as removidas no diff de código (`.cs`, `.asmdef`, `.json`, `.yml` de CI, `package.json`);
  - "arquivos" são os caminhos distintos no diff, e um arquivo movido conta uma vez;
  - `.meta` não conta em nenhum dos dois limites;
  - páginas de `Documentation~/manual/` e fixtures contam só nos 10 arquivos;
  - **remoção pura** (I42): um arquivo apagado por inteiro, sem substituto no mesmo ticket, não conta em nenhum dos dois limites. Ele continua listado no PR;
  - **movimento puro** (0003): arquivo movido sem mudar o conteúdo (`R100` no `git diff -M --name-status`) conta 1 linha e fica fora dos 10 arquivos. Ele continua listado no PR.
- **Paralelo sem Editor** (emenda de 2026-10-29 da 0003): um ticket que não abre a Unity nem pega a trava pode rodar em paralelo só se a Spec marcar o par e os dois não tocarem o mesmo arquivo. Ele usa a própria branch e uma cópia de trabalho própria, sem trocar a branch do checkout do ticket com Unity. Os detalhes estão na emenda e na seção 8 da Spec.
- **Branch:** uma branch por ticket no mesmo checkout, `feat|fix|refactor/<feature>-<ticket>`, criada pelo DevOps a partir da `main`. Os worktrees por ticket estão adiados (0006 Adiada).
- **Depois do APROVADO:** o DevOps cria a branch, commita com stage seletivo (`/commit`) e abre o PR contra a `main`, confirmando cada passo com o usuário. O merge é do usuário.
- **Versões:** a linha 3.0 recebe todas as mudanças. A 2.x está congelada: não recebe correções e não tem branch `release/2.x` (0008, I43).

## Documentos

```
Documentation~/
  README.md        índice dos documentos e donos
  TIME.md          este arquivo
  backlog.md       porta de entrada de toda ideia, bug ou pendência
  gdd/             visão do pacote e dos sistemas
  ds/              Design System (padrões de API, nomes, UX do Inspector)
  prd/             um PRD por feature
  spec/            uma Spec por feature, com os tickets
  adr/             decisões de arquitetura (NNNN-titulo.md, índice em adr/README.md)
  manual/          manual público (GitHub Pages)
  planos/          estudos de origem
```

`Documentation~/` segue a convenção UPM: a Unity ignora a pasta, então ela não gera `.meta` nem
entra no pacote importado. Uma ADR aceita não é reescrita: ganha emenda datada ou é substituída.

## Projeto de desenvolvimento

A raiz do repositório é o projeto Unity de desenvolvimento (0004 e 0027), e o pacote fica embutido
em `Packages/com.spellbook/`. O `Packages/manifest.json` lista `com.spellbook` em `testables`. O
`.gitignore` é o padrão Unity (`Library/`, `Temp/`, `Logs/`, `UserSettings/` e afins). Os caminhos
da Spec seguem a regra de prefixo do topo dela: `Runtime/...` é `Packages/com.spellbook/Runtime/...`.
Os relatórios de teste ficam em `Logs/`.

## Editor-lock

A trava segue a ADR 0005.

- Só a Fábrica abre o Unity Editor ou roda a Unity para implementar.
- O Examinador roda a Unity em batchmode **só para testes**, também sob a trava. Ele cria e remove a trava; é a única exceção ao "não edita arquivos" dele.
- Antes de usar a Unity, o papel verifica `.claude/editor.lock`. Se existir, para e avisa o Controlador. Senão, cria a trava com uma linha `papel; ticket; horário ISO-8601; caminho do host`, onde o "caminho do host" é a raiz do repositório.
- Quem criou remove a trava ao terminar, mesmo se falhar. Ninguém apaga a trava de outro papel. Trava órfã só é removida pelo usuário, ou pelo Controlador com a confirmação do usuário.
- Ticket sem Unity (docs, processo) não cria a trava.
- A trava é local, fica na raiz do checkout e nunca é commitada.

## Issues

As issues do SpellBook ficam em `gabrielctorres/SpellBook` (0002), criadas pelo Controlador com a
skill `/issue`, sempre com `--repo gabrielctorres/SpellBook` e depois da confirmação do usuário.

- **Título:** ticket da Spec como `<ticket>: <tipo>(<escopo>): <resumo>`; item avulso em Conventional Commits.
- **Labels:** épico (`epic:<slug>`, ex. `epic:0-M`), tipo igual ao prefixo do Conventional Commit e `via:rapida` para a via rápida.
- **Corpo:** links para a seção do PRD, o ticket da Spec, as ADRs e o DS.

O código é corrigido aqui, de forma genérica e reutilizável entre jogos.
