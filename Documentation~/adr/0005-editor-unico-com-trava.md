# 0005 — Editor único com trava (editor-lock)

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

A Unity não roda em batchmode num projeto que já está aberto no Editor, e duas instâncias no mesmo projeto corrompem a `Library/`. Vários papéis podem rodar ao mesmo tempo (subagentes e terminais dedicados). O `TIME.md` já descreve o editor-lock: só a Fábrica abre o Editor ou roda batchmode, com `.claude/editor.lock`; o Examinador só roda testes sem lock. O lock está no `.gitignore`.

Há uma lacuna: rodar testes é rodar a Unity em batchmode, e o Examinador roda testes sem criar o lock. Com worktrees (0006), um lock relativo à pasta de trabalho deixa de ser único.

## Decisão

1. Só a Fábrica abre o Unity Editor ou roda a Unity para implementar. O Examinador pode rodar a Unity em batchmode **só para testes**, também sob a trava.
2. Antes de usar a Unity, o papel verifica a trava. Se ela existir, para e avisa o Controlador. Senão, cria a trava com: papel, ticket, horário e caminho da pasta de trabalho.
3. A trava é removida ao terminar, também em caso de erro.
4. A trava é **uma só por projeto host**, num caminho fixo, e não por worktree. Proposta: `.claude/editor.lock` do checkout principal do repositório.
5. Trava órfã (processo morto) só é removida pelo usuário, ou pelo Controlador com a confirmação do usuário.
6. A trava é local e nunca é commitada.

## Motivo

- Evita corromper a `Library/` e falhas de batchmode por Editor aberto.
- Um caminho único funciona com qualquer número de worktrees.
- Registrar papel e pasta permite saber de quem é uma trava esquecida.

## Consequências

- O agente `examinador` passa a criar e remover um arquivo (a trava). Isso muda o "não edita arquivos" dele e precisa constar no agente e na skill `/revisao`.
- Implementação e revisão nunca rodam a Unity ao mesmo tempo, o que é coerente com o WIP 1 (0003).

## O que os agentes NÃO podem fazer

- Abrir o Editor ou rodar batchmode sem a trava, ou com a trava de outro papel.
- Apagar uma trava que não criaram.
- Commitar `.claude/editor.lock`.
- O Examinador abrir o Editor interativo ou alterar assets ao rodar testes.

## Decisões incorporadas (2026-10-29)

- **O Examinador cria e remove a trava** ao rodar testes em batchmode (decisão I35). O agente `examinador` e a skill `/revisao` mudam no ticket 0M-02 (B-022). O "não edita arquivos" do Examinador passa a ter essa única exceção.
- **Caminho da trava:** `.claude/editor.lock` na raiz do checkout. Como os worktrees foram adiados (0006 Adiada, decisão I34) e o host fica dentro do repositório (0004), existe um só checkout e um só host, e o caminho é único. Formato: uma linha `papel; ticket; horário ISO-8601; caminho do host`.
- A trava também vale para o host em `HostProject~/` (0004): quem abre ou roda a Unity nesse projeto cria a trava antes.

## Emenda (2026-10-30): o host é a raiz, pela 0027

A partir do ticket 0M-54, o projeto host é a raiz do repositório (0004, emenda de 2026-10-30).

- A trava continua sendo `.claude/editor.lock` na raiz do checkout, uma por checkout, com o mesmo formato.
- O campo "caminho do host" da trava passa a ser o caminho da raiz do repositório.
- O item "A trava também vale para o host em `HostProject~/`" passa a valer para o projeto da raiz. Quem abre esse projeto no Hub, ou roda a Unity nele em batchmode, cria a trava antes.
- Durante o próprio 0M-54, a Unity roda nos dois projetos (antes do movimento em `HostProject~/` e depois na raiz). A mesma trava vale para os dois, e ela é criada uma vez só, no começo do ticket.

O resto não muda.

## Emenda (2026-11-03): uma Unity por checkout e WIP por worktree

**Status da emenda: Aceita** (decisão do usuário, aprovada pelo usuário em 2026-11-03). O experimento de WIP 2 da 0003 (emendas de 2026-11-01 e de 2026-11-02) deixa de ser experimento e vira esta regra. Vem com a 0042, que substitui a 0003.

1. **Uma Unity por checkout.** Cada checkout, seja o do repositório ou um worktree, é um projeto host com pasta e `Library/` próprias e com a própria trava `.claude/editor.lock` na raiz, no formato de 2026-10-29. O item 4 da Decisão passa a valer por checkout. Dentro de um checkout a trava continua única, então implementação e revisão do mesmo ticket não rodam a Unity ao mesmo tempo.
2. **Limite da máquina:** no máximo **2** processos da Unity ao mesmo tempo, contando o batchmode de qualquer papel e o Editor aberto pelo usuário. O motivo é a memória (16 GB), que já derrubou um processo em 2026-11-03.
3. **Dois tickets com Unity em paralelo** podem rodar, cada um no seu worktree, quando valem as três condições:
   - nenhum depende do outro, nem por um ticket no meio;
   - os Detalhes dos dois não têm arquivo em comum. Contam os `.asmdef`, os `.meta` de pasta nova, os testes e o `CHANGELOG.md` quando os dois quebram algo. Não contam o design doc do épico nem o `Documentation~/decisoes.md`, porque cada ticket escreve só a própria seção ou linha;
   - o XR3 (Spec `referencia-catalogo-e-api.md`) já está na branch do lote. Sem ele, os testes que usam a pasta temporária do sistema se apagam entre as duas Unity (emenda de 2026-11-02 da 0003, "o WIP 2 depende do XR3").

   O Controlador forma o par pela lista de arquivos dos dois Detalhes, sem ler código. A Spec não precisa marcar o par.
4. **Worktree.** O Controlador cria o worktree a partir da branch do lote (`orca worktree create`, pasta `C:/Users/gabri/orca/workspaces/SpellBook/wt-<ticket>`, branch local `gabrielctorres/wt-<ticket>`) e copia para ele os docs de processo ainda sem commit. A Fábrica e o Examinador trabalham dentro dele. Depois do APROVADO, o passo de commit commita no worktree e leva o commit para a branch do lote por `git cherry-pick`. Em seguida o Controlador remove o worktree (`orca worktree rm`).
5. **Suíte do par.** Depois do cherry-pick do segundo ticket do par, a suíte completa roda uma vez no checkout do lote, sob a trava dele e dentro do limite do item 2. Se falhar, o par volta ao Controlador.
6. **Queda por memória.** Se uma execução cair por memória (processo morto pelo sistema, erro de alocação ou log cortado sem resultado), o ticket que caiu roda de novo sozinho, e nenhum par novo começa até ele terminar. A regra não acaba por isso.
7. **Ticket sem Unity** não pega trava e não conta no limite do item 2. Ele roda em paralelo numa cópia de trabalho própria, sem arquivo em comum com outro ticket em andamento.
8. A 0006 continua Adiada no resto (checkout principal fixo na `main`, worktree para todo ticket). Esta emenda é a forma de worktree que vale.

O resto desta ADR não muda.

## Em aberto

Nada.

## Relacionadas

- 0003 (WIP 1, substituída pela 0042), 0004 (testes), 0006 (worktrees), 0027 (estrutura do repositório), 0042 (tamanho do ticket).
