# AGENTS.md

## Agent behavior

- For changes touching more than three files, outline the plan and wait for confirmation before editing.
- Ask a clarifying question when requirements are ambiguous instead of guessing.
- Keep diffs small and focused on the request; do not refactor unrelated code.
- Run the relevant tests, linter and type checker before reporting a task as done, and report failures honestly.
- Never delete, skip or loosen a failing test to make it pass; fix the cause.
- Do not add new dependencies without explicit approval; prefer the standard library and existing packages.
- Do not edit generated or vendored files; change the source and regenerate.
- Never read, print or commit secrets, .env files or credentials.
- Read the relevant code and tests before proposing a change; do not edit files you have not read.
- Fix the root cause rather than suppressing the symptom with a workaround or silenced warning.
- Do not add lint or type suppressions to get a clean run; fix the underlying issue.
- Report failing tests, skipped steps and unverified claims plainly; never claim success without evidence.
- Verify library APIs in the installed version or its docs before using them; do not invent functions.
- Do not change public behavior, file formats or APIs unless the task asks for it.
- Never hardcode values or special-case inputs to satisfy a test.
- If an approach fails twice, stop and ask instead of trying increasingly invasive workarounds.
- Before editing a directory, read the nearest AGENTS.md and follow it over the root file where they differ.
- Preserve unrelated working tree changes. Never revert or rewrite edits you did not make.

## Testing

- Add or update tests for every behavior change and bug fix.
- For bug fixes, write a failing test that reproduces the bug before fixing it.
- Prefer many fast unit tests, fewer integration tests and a small number of end-to-end tests.
- Mock only external boundaries (network, clock, filesystem); use real collaborators otherwise.
- Keep tests deterministic: no real time, randomness or network without control.
- Keep tests independent; no shared mutable state or required execution order.
- Structure tests as arrange, act, assert with one behavior per test.
- Build test data with small factories instead of large shared fixtures.
- Treat a flaky test as a bug: find the cause instead of adding retries or sleeps.
- Use snapshot tests only for small stable outputs and review snapshot diffs carefully.
- Test public behavior rather than private functions or implementation details.
- Make assertions specific enough that the test fails if the behavior breaks.

## Git and pull requests

- Write commit messages in Conventional Commits format (feat:, fix:, docs:, refactor:, test:, chore:) and mark breaking changes with ! or a BREAKING CHANGE footer.
- Keep pull requests small and focused on one change.
- Write commit subjects in the imperative mood, 72 characters or fewer.
- Never force-push to shared branches and never rewrite published history.
- Run lint, type check and tests before committing.
- Never commit secrets, build output or large binaries.
- Describe motivation, approach and testing in the pull request description.
- Make each commit one logical change that builds and passes tests.
- Do not amend or rebase commits that are already pushed to a shared branch.
- Add build output, local env files and editor files to .gitignore instead of committing them.
- Open pull requests only when CI is green and the diff is self-reviewed.
- Never bypass commit hooks or signing with --no-verify unless explicitly instructed.

## Security

- Load secrets from environment variables or a secret manager; never hardcode them.
- Use parameterized queries and safe APIs; never build SQL, shell commands or HTML by string concatenation with user input.
- Enforce authentication and authorization on the server for every protected operation.
- Pin dependency versions, commit lockfiles and review new packages before adding them.
- Grant the minimum permissions needed for tokens, service accounts and containers.
- Use well-reviewed cryptographic libraries; never invent your own algorithms or store plain passwords.
- Encode output for its context and avoid injecting raw HTML; sanitize rich text with a vetted library.
- Protect state-changing browser requests with CSRF tokens or SameSite cookies.
- Configure CORS with explicit origins; never reflect arbitrary origins with credentials.
- Use HttpOnly, Secure, SameSite cookies and rotate session identifiers on login.
- Validate and allow-list URLs before fetching user-supplied addresses to prevent SSRF.
- Hash passwords with Argon2id, or scrypt or bcrypt where it is unavailable, with a unique salt; never use fast hashes such as SHA-256.
- Run secret scanning in CI and rotate any credential that has been exposed.
- Validate untrusted input against a schema. Never run eval, exec or unpickling on it.

## Workflow and tooling

- Run tools through the project scripts (npm run, make, task) instead of invoking underlying binaries directly, so flags and environments match CI.
- Run the single aggregate check command (for example pre-commit or a lint script) before finishing; fix every failure and re-run until it passes.
- Run the specific test files related to your change first, and run the full suite only before handing off.
- Before editing a directory, read the README and any nested AGENTS.md that applies to it.
- Put throwaway scripts and experiment output in a gitignored scratch directory and delete them when done.
- After switching branches or pulling dependency changes, reinstall and rebuild before running tests.
- When CI fails, find the exact failing job and command, reproduce it locally, then fix the cause instead of re-running blindly.
- Stop any dev servers or watchers you started once you are done.
- Ask before running destructive or irreversible commands such as dropping databases, deleting branches, rm -rf or force operations.
- Prefer existing scripts, generators and templates in the repo over writing new ad hoc tooling.
- When the repository documents skills or runbooks for a task (migrations, releases, dependency bumps), follow them instead of improvising.
- Never start long-running dev servers or watchers in the foreground. Use targeted one-shot commands.

## Verification and CI feedback

- Run the narrowest relevant test first, then widen to the package and workspace checks as confidence grows.
- Run the project formatter and linter on changed files before committing, using the repo scripts or hooks.
- For a bug fix, reproduce the failure first, then show the regression test failing without the fix and passing with it.
- Report a task as done only after running the checks that prove it. If you could not run them, say so.
- Before finishing, re-read the full diff as a reviewer with no knowledge of this session and fix what would confuse them.

## C# and .NET conventions

- Enable nullable reference types and treat warnings as errors.
- Use async/await end to end, never .Result or .Wait(), and pass CancellationToken.
- Use constructor injection with the built-in DI container.
- Bind configuration with the options pattern and validate it on startup.

## Unity conventions

- Use [SerializeField] private fields instead of public fields for inspector values.
- Cache component references in Awake and avoid GetComponent and Find in Update.
- Pool frequently spawned objects instead of instantiating and destroying them.
- Avoid allocations in Update such as LINQ, string concatenation and new collections.
- Commit .meta files and never edit scene or prefab YAML by hand without need.

## Time de desenvolvimento

Esta seção é a fonte única das regras de processo. As ADRs guardam a decisão e o porquê: 0002 (fluxo, design doc e papéis, com a emenda de 2026-11-03 sobre Codex e Antigravity), 0042 (tamanho do ticket) e 0005 (trava e WIP). Se esta seção divergir de uma ADR, vale a ADR, e esta seção é corrigida. Os papéis do Claude Code são agentes em `.claude/agents/` (subagente pela Agent tool, ou terminal com `claude --agent <papel>`). O Codex e o Antigravity são lançados pelo Orca e seguem só este arquivo.

### Papéis
- **Controlador** (sessão principal, `claude --agent controlador`, Sonnet): cuida do backlog (`Documentation~/backlog.md`), monta a fila e o WIP, cria issues com `/issue` em `gabrielctorres/SpellBook`, abre e fecha terminais, despacha o Codex e o Antigravity pelo Orca e leva os gates ao usuário. Não lê código, não implementa e não commita. Em ticket só de texto fora do passe, roda `git diff --check`, confere o diff contra o objetivo e os CA da issue e segue para `/commit`; o usuário revisa o texto no PR. No passe de documentação, antes do merge do change request, lê o change request pelo MCP, só para ler, e confere molde, siglas, `SUMMARY.md`, legendas e diagramas na prévia, o texto novo pelas regras da humanizer (`C:/Users/gabri/.claude/skills/humanizer/SKILL.md`) e a declaração da Fábrica. Antes do `/commit`, na branch do lote, roda `git diff --check` e as conferências por busca da seção 7 de `Documentation~/guia-do-manual.md`. Não roda build: as guardas `Documentacao`, que o Examinador roda pelo `/revisao` depois da geração, fazem esse papel. Problema encontrado volta à Fábrica; repete as conferências antes de encaminhar. O usuário lê o texto no PR. Pergunta técnica vai para a Fábrica do ticket ou, se for de design, para o Desenhista.
- **Fábrica** (`fabrica`, fixo): escreve o "Detalhe (AAAA-MM-DD)" do ticket lendo o código na hora, implementa com testes red primeiro e testa (`/implementar`). No passe de documentação, escreve o texto no space de rascunho do GitBook pelo MCP, num change request do ticket, e o junta depois da conferência do Controlador.
- **Examinador** (`examinador`, fixo): revisor separado e único gate do ticket com código. Confere e pode reprovar pelo código, contra os CA e o Detalhe, roda a suíte completa e dá APROVADO ou REPROVADO (`/revisao`). O texto do mesmo ticket não gera achado; em ticket que quebra algo, confere a entrada de migração do `CHANGELOG.md`. Ticket só de texto fora do passe não passa por `/revisao`. Todo ticket do passe de documentação passa por `/revisao`: sob a trava, roda somente as guardas com `-testCategory "Documentacao"` e reprova se alguma falhar; não gera achado sobre o texto. Se houver código, também o revisa e roda a suíte completa conforme a regra de código. Se o texto mudar após as guardas, elas rodam de novo.
- **Desenhista** (`desenhista`, sob demanda): só quando nasce um épico. Escreve a parte fixa do design doc (`/design-doc`) e as ADRs que valem para mais de um épico, e registra a validação de uso (`/ajustar`).
- **Prisma** (`prisma`, sob demanda): shaders e arte técnica.
- **Codex** (externo, Orca): se você é o Codex, você faz três coisas. Implementa ticket da via rápida sem Unity que tenha código ou seja só de texto (docs internas, scripts, CI, skills, agentes e `.md`). Código inclui `.cs`, `.asmdef`, `.json`, `.yml` de CI, `package.json` e scripts (`.ps1`, `.sh` e scripts da fila). Também faz refatoração mecânica, inclusive em `.cs`, sem mudar comportamento, e escreve o texto do PR da branch do lote. Trabalha só no worktree que o Controlador criou a partir da branch do lote, sem a trava de Editor. Não roda a Unity, não commita e não usa git que altere estado. Ticket com código passa por `/revisao` do Examinador; ticket só de texto segue a checagem do Controlador e vai para `/commit`. Como não há como compilar nem testar o C# fora da Unity, diga no resultado o que não foi verificado. Entrega o resultado no worktree e um resumo ao Controlador. O texto do PR vai ao Controlador, que abre o PR.
- **Antigravity** (externo, Orca): se você é o Antigravity, você só lê. Pesquisa, mapeia o código e mede o tamanho de um ticket antes do Detalhe, em linhas e arquivos pela regra de "Tamanho do ticket" abaixo. Entrega ao Controlador a lista de arquivos com linhas estimadas e o que não soube medir. Não edita arquivo e não usa git. O Detalhe continua da Fábrica, que confere a medição lendo o código.
- **MCP do GitBook** (0044, item 13): só a Fábrica do passe escreve, e só no space de rascunho, ligado à branch do lote. O Controlador só lê. O Examinador, o Desenhista, o Codex e o Antigravity não o usam. Nenhum agente escreve no space público.
- Git não é papel. Depois do APROVADO, o passo de commit do script da fila (`/commit`) commita o ticket na branch do lote. No passe de documentação, o merge do change request no rascunho cria um commit do GitBook na branch do lote, o único fora do `/commit` (0002, emenda de 2026-11-04). O `/commit <design doc> <ticket> change-request` o traz ao checkout do ticket, só por avanço rápido. O PR é da branch do lote contra a `main`, e o merge é do usuário.

### Fluxo
1. Toda ideia, bug ou pendência entra no backlog: bruta → cristalizada → em pesquisa → design aprovado → em execução → feito.
2. Num épico novo, o Desenhista escreve o design doc em `Documentation~/spec/<épico>.md`, pelo `spec/_modelo-design-doc.md`. A parte fixa tem de 1 a 2 páginas: o problema, os CA numerados, o cenário no Sandbox (`Packages/com.spellbook/Samples~/Sandbox`) que prova o épico e os tickets com objetivo, CA e dependências. ADR só para decisão que vale para mais de um épico.
3. Gate único: o usuário aprova a parte fixa e as ADRs novas (gate parcial permitido). Depois, o Controlador cria uma issue por ticket, com objetivo e CA.
4. Na entrada do ticket na fila, a Fábrica lê o código e escreve o Detalhe no design doc: escopo, testes red, critério de pronto, tamanho e linha de commit. O Detalhe vale como o plano para mudanças em mais de três arquivos. Ele volta ao usuário só se mudar um CA, pedir ADR nova ou quebrar API ou formato serializado que o design doc não previa.
5. `/implementar` (Fábrica) → se o ticket tem código, `/revisao` (Examinador), APROVADO → `/commit` (passo do script). Se o ticket é só de texto fora do passe, o Controlador roda `git diff --check`, confere o diff contra o objetivo e os CA da issue, e segue para `/commit`; o usuário revisa o texto no PR. No passe de documentação, a Fábrica escreve o texto num change request do space de rascunho, pelo MCP. O Controlador confere molde, siglas, `SUMMARY.md`, legendas, diagramas na prévia, regras e declaração da humanizer no change request, e a Fábrica o junta. O `/commit <design doc> <ticket> change-request` traz o commit do GitBook ao checkout por avanço rápido, a Fábrica roda **SpellBook > Documentação > Gerar catálogo e exemplos**, e todo ticket passa pelo `/revisao`, que roda as guardas `Documentacao` sob a trava. Antes do `/commit`, o Controlador faz `git diff --check` e as conferências da seção 7 do guia. Se uma guarda reprovar o texto, a correção é um change request novo do mesmo ticket. Não há build. Só segue com as duas partes aprovadas para o diff final. Se o texto mudar depois das guardas, elas rodam de novo. O usuário lê o texto no PR.
6. Épicos que já têm PRD e Spec não são reescritos: a Spec faz o papel do design doc, o PRD o da parte fixa, e o Detalhe entra no ticket da Spec.
7. Via rápida: ticket avulso sem design doc, com o Detalhe na issue. Ela não cria ADR, não quebra API pública e não muda formato serializado.

### Tamanho do ticket (ADR 0042)
- Um ticket = uma issue = um commit. No passe de documentação, o ticket tem também o commit de cada change request, e o teto soma todos (0042, emenda de 2026-11-04). Teto de ~500 linhas e 10 arquivos, medido no Detalhe.
- Linhas são as adicionadas mais as removidas em `.cs`, `.asmdef`, `.json`, `.yml` de CI e `package.json`. Os outros arquivos contam só nos 10 arquivos.
- Não contam: `.meta`; saída de gerador com guarda (a página do catálogo e os blocos C# expandidos, 0044); remoção pura; a entrada do `SUMMARY.md` junto da página; o Detalhe e a linha do ticket no `decisoes.md`. Movimento puro (`R100`) conta 1 linha e nenhum arquivo.
- Até +30% (~650 linhas ou 13 arquivos), quem detalha registra o motivo no Detalhe do ticket (no modo autônomo, também no `decisoes.md`). A exceção vale só para aquele ticket e termina no commit, sem emenda e sem pergunta. Acima de +30%, o ticket se divide.
- Arquivo fora do Detalhe só entra se for consequência direta do ticket, e conta.

### WIP, trava e testes (ADR 0005)
- Uma Unity por checkout. Cada checkout (o do repositório ou um worktree) tem a própria trava `.claude/editor.lock` na raiz, com uma linha `papel; ticket; horário ISO-8601; caminho do host`. Quem cria remove, também em caso de erro; trava órfã só sai com o usuário.
- No máximo 2 Unity ao mesmo tempo na máquina, por causa da memória. Dois tickets com Unity rodam em paralelo só em worktrees separados, sem dependência entre eles, sem arquivo em comum nos Detalhes e depois do XR3 na branch do lote. Se uma Unity cair por memória, o ticket roda de novo sozinho.
- Ticket sem Unity não pega trava e roda em paralelo numa cópia própria, sem arquivo em comum.
- Na implementação, rodam só os testes do ticket, com filtro. Na revisão de ticket comum, roda a suíte completa com `-testCategory "!Documentacao"`. No passe de documentação, o Examinador roda somente as guardas com `-testCategory "Documentacao"`; se houver código no ticket do passe, também roda a suíte completa de revisão com `!Documentacao`.
- Manual, catálogo e exemplos expandidos só no passe de documentação (0007, 0040 e 0044). O `CHANGELOG.md` entra só no ticket que quebra algo, com a migração.
- No checkout, ninguém edita à mão a pasta do manual: só a geração escreve nela, depois do merge do change request. Uma página fica em um change request aberto por vez (0044, item 13).

### Registro
- Fora do modo autônomo, dúvida ou decisão volta ao Controlador, que a leva ao usuário. Nada vai para o `Documentation~/decisoes.md`.
- No modo autônomo (usuário ausente), toda decisão tomada sem o usuário entra numa linha temporária de `Documentation~/decisoes.md`: `data | ticket | decisão | base | ADR afetada`. O usuário a revisa ao voltar.
- A exceção de tamanho de até +30% fica registrada no Detalhe do ticket (ADR 0042, emenda de 2026-11-03), e no modo autônomo também no `decisoes.md`.

<!-- gitbook-agent-instructions:start -->

## GitBook Documentation Editing

This repository contains documentation synced with GitBook via Git Sync.

Before editing GitBook-synced Markdown, YAML, or asset files, make sure the GitBook skill is available and up to date in your local agent environment. Prefer installing or updating it with:

```bash
npx skills add gitbookio/gitbook-skills
```

This command may add or update local agent skill files. Use them only as local agent instructions; do not commit those installed skill files or any tool-generated agent configuration unless the user explicitly asks for it.

If `npx` is unavailable, load the skill from:

https://gitbook.com/docs/skill.md

When making changes, preserve GitBook sync metadata such as frontmatter, `SUMMARY.md`, `gitbook-docs.yaml`, `.gitbook/`, and asset links unless the requested edit explicitly requires changing them.

<!-- gitbook-agent-instructions:end -->
