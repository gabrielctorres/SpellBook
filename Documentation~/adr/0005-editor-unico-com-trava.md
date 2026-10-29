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

## Em aberto

Nada.

## Relacionadas

- 0003 (WIP 1), 0004 (testes), 0006 (worktrees).
