---
name: implementar
description: Fábrica implementa UM ticket da Spec com testes red primeiro e depois o mínimo de código. Use quando houver um ticket de Documentation~/spec pronto para implementação.
argument-hint: <spec> <ticket, ex. 0M-07>
---

Implemente o ticket: $ARGUMENTS

1. **Ler.** Leia a Spec (`Documentation~/spec/<feature>.md`), as ADRs citadas e todo o código e os testes no escopo do ticket. Se o ticket for ambíguo ou passar dos limites da ADR 0003 (~400 linhas e 10 arquivos; `.meta` não conta, páginas do manual e fixtures contam só nos arquivos, remoção pura não conta; movimento puro, `R100` no `git diff -M --name-status`, conta 1 linha e fica fora dos 10 arquivos), pare e devolva a dúvida ao Controlador. Se outro ticket estiver em implementação ou revisão, só siga no paralelo sem Editor (emenda de 2026-10-29 da ADR 0003): este ticket não usa a Unity nem a trava, o par está marcado na Spec, nenhum arquivo em comum e cópia de trabalho própria. Senão, pare.
2. **Editor-lock** (só em ticket que usa a Unity; ADR 0005). Se `.claude/editor.lock` existir, pare e avise o Controlador. Senão, crie o arquivo com uma linha `fabrica; <ticket>; <horário ISO-8601>; <caminho do host>`, em que o caminho do host é a raiz do repositório.
3. **Red.** Escreva os testes listados no ticket (Unity Test Framework, NUnit; EditMode por padrão e PlayMode só se precisar de frame/física). Rode e mostre que **falham pelo motivo esperado**. Em ticket sem código (docs, processo), o red é o critério verificável do ticket falhando antes da mudança.
4. **Green.** Escreva o mínimo de código para os testes passarem, sem refatorar código fora do ticket.
5. **Verificar.** Em ticket sem Unity (docs, processo, CI), rode o critério verificável do ticket e mostre que passa, sem batchmode e sem trava. Em ticket com Unity, rode os testes do ticket e depois todos os testes do pacote (`Packages/com.spellbook/`) no projeto da raiz. Rode da raiz e crie `Logs/` antes, se não existir (`mkdir -p Logs`):
   ```
   "$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD/Logs/TestResults-EditMode.xml" -logFile "$PWD/Logs/unity-tests.log"
   "$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testResults "$PWD/Logs/TestResults-PlayMode.xml" -logFile "$PWD/Logs/unity-tests.log"
   ```
   Rode o PlayMode só se o ticket ou o pacote tiver testes PlayMode. Se `UNITY_EDITOR` não estiver definido, peça o caminho do `Unity.exe` (versão em `ProjectSettings/ProjectVersion.txt`). O batchmode não roda com o Editor aberto no mesmo projeto.
6. **Liberar** (só em ticket que usa a Unity). Remova a `.claude/editor.lock` que você criou no passo 2, mesmo se algo falhar. Nunca apague uma trava que você não criou; em ticket sem Unity, não mexa na trava.
7. **Relatório.** Devolva o ticket, os arquivos alterados, a saída red, a saída green e as pendências. Não commite: o próximo passo é `/revisao`. Depois do APROVADO, o DevOps cria a branch, commita e abre o PR (`/commit`).
