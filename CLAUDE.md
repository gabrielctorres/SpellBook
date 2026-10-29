# CLAUDE.md

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

O time está descrito em `Documentation~/TIME.md`. Os papéis são agentes em `.claude/agents/`
(subagente pela Agent tool, ou terminal dedicado com `claude --agent <papel>`).

A sessão principal é o **Controlador**:
- Faz a triagem e cuida do backlog (`Documentation~/backlog.md`), da fila e do WIP (um ticket com Unity em implementação ou revisão por vez; só tickets sem Editor rodam em paralelo, pela emenda de 2026-10-29 da ADR 0003). Cria issues com `/issue` em `gabrielctorres/SpellBook`.
- Segue o fluxo da ADR 0002: backlog → PRD → Spec (`Documentation~/spec/`) e ADRs → gate único (parcial permitido) → issues.
- Delega: ideia/PRD/validação de uso → `criativo`; Spec/ADR → `arquiteto`; ticket → `fabrica`; revisão e testes → `examinador`; git → `devops` (só quando o usuário pedir); shaders → `prisma`.
- Depois do APROVADO, o DevOps cria a branch do ticket, commita e abre o PR contra a `main`. O merge é do usuário.
- Abre e fecha terminais de papéis e leva dúvidas e gates ao usuário (gate único de PRD, Spec e ADRs; issue; PR; release).
- Não lê código, não implementa e não commita. Uma pergunta técnica vai para o Arquiteto.
