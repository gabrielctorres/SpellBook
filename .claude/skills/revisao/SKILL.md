---
name: revisao
description: Examinador revisa tickets com código contra os CA e o Detalhe, roda a suíte e dá APROVADO ou REPROVADO. Ticket do passe de documentação passa por /revisao para rodar as guardas Documentacao. Use depois de /implementar.
argument-hint: <design doc> <ticket>
---

Revise o ticket: $ARGUMENTS

**Escopo do gate (ADR 0002, emenda de 2026-11-03, e ADR 0007, emenda do passe).** Código inclui `.cs`, `.asmdef`, `.json`, `.yml` de CI, `package.json` e scripts (`.ps1`, `.sh` e scripts da fila). Só tickets com código passam por `/revisao`, exceto todo ticket do passe de documentação, inclusive os só de texto. Fora do passe, revise e reprove somente pelo código; texto `.md` do mesmo ticket não gera achado. Exceção: em ticket que quebra algo, confira a entrada de migração do `CHANGELOG.md`. Ticket só de texto fora do passe não passa por esta skill: o Controlador roda `git diff --check`, confere o diff contra objetivo e CA da issue e segue para `/commit`; o usuário revisa o texto no PR. No passe, o Controlador revisa o texto conforme a seção 7 do guia e a humanizer, e roda `mkdocs build --strict`; o Examinador roda somente as guardas `Documentacao` e reprova se alguma falhar, sem gerar achado sobre o texto. Se o texto mudar depois das guardas, elas rodam novamente antes do `/commit`.

1. **Diff.** `git diff`, `git diff --stat` e `git diff -M --name-status` contra a base do ticket, que é o último commit da branch do lote (num worktree, rode dentro dele). Confirme, para o código, que só mudaram os arquivos do Detalhe, mais os que são consequência direta do ticket, e que o diff cabe na ADR 0042:
   - até ~500 linhas: adicionadas mais removidas em `.cs`, `.asmdef`, `.json`, `.yml` de CI e `package.json`;
   - até 10 arquivos: caminhos distintos no diff. Os outros tipos de arquivo contam só aqui;
   - não contam: `.meta`, saída gerada com guarda (0040), remoção pura, a entrada do `nav` da página criada ou apagada, o Detalhe do ticket e a linha dele no `decisoes.md`. A remoção pura é revisada pela lista de caminhos;
   - movimento puro (`R100`) conta 1 linha e nenhum arquivo;
   - acima do teto e até +30%, confira se o motivo está no Detalhe; no modo autônomo, também no `decisoes.md`. Acima de +30%, reprove para dividir;
   - paralelo (ADR 0005, emenda de 2026-11-03): o outro ticket em andamento não toca nenhum arquivo deste.
2. **Checar quatro eixos.** Cada eixo vale 25%:
   - **Padrões:** regras do `AGENTS.md` (C#, Unity, testes, segurança), `.meta` presentes, sem suppressions, sem dependência nova.
   - **CA e Detalhe:** o código atende os CA que o Detalhe cita (no design doc `Documentation~/spec/<épico>.md`; num épico antigo, na Spec e no PRD) e o critério de pronto, sem nada a mais.
   - **ADR:** respeita as decisões aceitas em `Documentation~/adr/`. Não registre achado por falta de página do manual, nem por catálogo ou referência desatualizados (emenda de 2026-11-03 da 0007). Registre achado se faltar `[HelpURL]` com constante de `DocUrls`, ou `[TypeCategory]` com `Summary`, ou se faltar a entrada de quebra com migração no `CHANGELOG.md` de um ticket com `!` (essa é a única verificação de texto, além do código, nesse caso). No passe, não gere achado sobre o texto: o Controlador faz as conferências da seção 7 do guia, a humanizer e o `mkdocs build --strict`.
   - **DS:** segue nomes, padrões de API e de Inspector de `Documentation~/ds/`. Enquanto `Documentation~/ds/` não existir, confira nomes, padrões de API e de Inspector contra as ADRs aceitas (0011, 0026) e o código vizinho do mesmo módulo, e escreva no veredito "DS: sem DS, conferido por ADR e vizinhança". O critério de nota não muda.
3. **Testes** (ADR 0005). Ticket comum sem Unity (docs, processo): rode o critério verificável do ticket e não crie a trava. Ticket do passe de documentação: crie a trava e rode as guardas com `-testCategory "Documentacao"` (somente essas guardas para ticket só de texto); se o ticket também tiver código, rode também a suíte completa de código com `-testCategory "!Documentacao"`. Ticket comum com Unity:
   1. Se `.claude/editor.lock` existir na raiz do checkout, não rode: avise o Controlador e reprove com "testes não executados (lock ativo)".
   2. Senão, crie a trava com uma linha `examinador; <ticket>; <horário ISO-8601>; <caminho do host>`.
   3. Rode a **suíte completa** de EditMode e de PlayMode em batchmode na raiz do checkout do ticket (num worktree, a raiz dele), sem `-testFilter` nem `-assemblyNames`. Na implementação a Fábrica rodou só os testes do ticket (ADR 0042, item 7), e esta é a única execução que prova que nada quebrou. Crie `Logs/` antes, se não existir:
      ```
      "$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testCategory "!Documentacao" -testResults "$PWD/Logs/TestResults-EditMode.xml" -logFile "$PWD/Logs/unity-tests.log"
      timeout 600 "$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testCategory "!Documentacao" -testResults "$PWD/Logs/TestResults-PlayMode.xml" -logFile "$PWD/Logs/unity-tests.log"
      ```
      Para código incluído no passe, rode também o teste de categoria `Documentacao` sob a mesma trava. Se o texto mudar depois de uma execução verde dessas guardas, rode-as novamente antes do `/commit`. O timeout de 600 s do PlayMode e a limpeza de `Assets/InitTestScene*` de `/implementar` valem aqui também. Cole o resumo (total, passou, falhou). Não abra o Editor interativo e não altere assets.
   4. Remova a trava, também em caso de erro.
   5. Num par de worktrees (ADR 0005, emenda de 2026-11-03), depois do cherry-pick do segundo ticket, rode a suíte completa mais uma vez no checkout do lote, sob a trava dele. Se falhar, devolva o par ao Controlador.
4. **Veredito.**

```markdown
## Revisão <design doc> <ticket>
Nota: <0–100>%  (Padrões x/25 · CA x/25 · ADR x/25 · DS x/25)
Testes: <n passou / n falhou>
Veredito: APROVADO | REPROVADO

### Achados
1. `arquivo:linha` — <problema>. Regra: <fonte>. Cenário de falha: <entrada → resultado errado>.
```

APROVADO exige nota ≥ 80%, zero achados pelo código e testes verdes (em ticket comum sem Unity, o critério verificável passando). No passe, as guardas `Documentacao` também precisam estar verdes; uma falha reprova pela guarda, sem achado sobre o texto. Não corrija nada (a única escrita permitida é a trava); os achados voltam para a Fábrica. Depois do APROVADO e da conferência do Controlador sobre o diff final, o passo de commit do script da fila commita o ticket (`/commit`).
