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

## Emenda (2026-10-29): paralelo só para tickets sem Editor

Decisão do usuário: "só tickets sem Editor" podem rodar em paralelo.

- O WIP 1 (item 3 da Decisão) continua valendo para os tickets que usam a Unity. Eles pegam a trava (0005) e só um fica em implementação ou revisão por vez.
- Tickets de docs, processo e CI, que não abrem a Unity e não pegam a trava, podem rodar em paralelo a um ticket com Unity e entre si. Cada um tem a própria issue, branch e PR, e a regra 1 ticket = 1 PR não muda.
- Dois tickets em paralelo não podem tocar o mesmo arquivo. A Spec marca quais tickets podem andar em paralelo.
- O item "Pegar um segundo ticket com outro em implementação ou em revisão" da lista de proibições vale entre tickets com Unity e, para os sem Editor, só quando os dois tocam o mesmo arquivo.
- Como a 0006 (worktrees) está adiada, o ticket em paralelo não pode trocar a branch do checkout que o ticket com Unity está usando. Ele precisa de uma cópia de trabalho própria, ou espera.

## Emenda (2026-10-30): movimento puro conta como 1 linha

Decisão do usuário no gate da 0027: em vez de dar uma exceção ao ticket de migração, vale uma regra permanente. Ela é mais um item das "Decisões incorporadas":

- **Movimento puro:** um arquivo movido sem alteração de conteúdo conta como **1 linha** no limite de ~400.
  - Ele **não conta no limite de 10 arquivos**, como a remoção pura (I42). Leitura do Arquiteto, **confirmada pelo usuário em 2026-10-30** (0027, item 10 das decisões do usuário).
  - O arquivo continua listado no PR e é revisado pela lista de caminhos.
- **Como reconhecer:** o git mostra o movimento como `R100` (similaridade de 100%) em `git diff -M --name-status` contra a base do ticket. O Examinador confere que todo arquivo contado como movimento puro aparece com `R100`.
- **Movido com alteração** (`R` abaixo de 100, ou apagado e criado de novo com outro conteúdo): conta como antes, 1 arquivo e as linhas da diferença real.
- O `.meta` que acompanha o arquivo continua fora dos dois limites. Movê-lo junto, com o mesmo GUID, é obrigatório (0027).
- A frase "Um arquivo movido conta uma vez", em "Contagem", passa a valer só para o movimento com alteração.
- **Exceção única, não regra (decisão do usuário, 2026-10-30):** no 0M-54, os 21 arquivos de `ProjectSettings/` gerados pela Unity entram sem contar nos dois limites (0027, item 11 das decisões do usuário). Fora desse ticket, arquivo criado em `ProjectSettings/` conta normalmente.

O resto desta ADR não muda.

## Emenda (2026-10-30): teto de ~500 linhas por ticket

Decisão do usuário no gate de 2026-10-30. A regra é permanente e vale para todos os tickets daqui em diante.

- **Teto de linhas:** o limite passa de **~400** para **~500 linhas de diff** por ticket. Onde esta ADR diz "~400" como regra (item 1 da Decisão, "Decisões incorporadas" e a emenda do movimento puro), leia "~500". O texto anterior fica como está, como histórico.
- **O que não muda:** o limite de **10 arquivos**; a "Contagem" (linhas adicionadas mais removidas, nos mesmos tipos de arquivo, sem `.meta`); páginas do manual e fixtures contam só nos arquivos (I35); a remoção pura (I42); o movimento puro (emenda acima).
- **Motivo:** os tickets com testes PlayMode e EditMode completos passam de 400 linhas de forma recorrente (0M-22 e 0M-27). Os testes são o que a 0004 pede, e o código de produção desses tickets continua pequeno.
- **Exceções anteriores:** a exceção do 0M-22 (teto de 500) passa a ser só um registro, porque o teto geral já a cobre. As exceções do 0M-07 (440), do 0M-47 (~500) e do 0M-48 (~440) também ficam só como registro.
- O item 4 da Decisão continua: acima de ~500 linhas ou de 10 arquivos, a Fábrica para e devolve ao Controlador, que leva ao Arquiteto.

O resto desta ADR não muda.

## Em aberto

Nada.

## Relacionadas

- 0002 (fluxo), 0004 (testes primeiro), 0006 (worktrees), 0008 (commits e versão), 0027 (estrutura do repositório).
