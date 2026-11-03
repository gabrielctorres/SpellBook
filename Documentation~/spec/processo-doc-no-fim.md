# Spec: documentação no fim do projeto (guardas no passe de documentação)
PRD: nenhum. É mudança de processo, decisão do usuário de 2026-11-03, aprovada pelo usuário em 2026-11-03. ADRs: emendas de 2026-11-03 da 0002, da 0003, da 0007 e da 0040.

## Contexto do código

Guardas que comparam o código com o manual ou com a referência commitada. Todas estão no assembly `SpellBook.Core.Tests.EditMode` (`Packages/com.spellbook/Tests/Core/EditMode/SpellBook.Core.Tests.EditMode.asmdef`):
- `Tests/Core/EditMode/Documentation/ReferenceUpToDateTests.cs:16`, classe com 5 testes:
  - `CommittedCatalog_MatchesGenerated` (`:25`);
  - `CommittedFingerprint_MatchesSources` (`:37`);
  - `CommittedReference_HasNoMachinePathOrForeignBranch` (`:49`);
  - `CommittedXrefMap_MatchesCommittedReference` (`:74`);
  - `EveryUnityReference_HasXrefEntry` (`:86`).

  Os dois primeiros falham em todo ticket que muda `.cs` sem rodar a geração.
- `Tests/Core/EditMode/ManualCodeExamplesTests.cs:15`, classe com 3 testes, todos lendo o manual:
  - `NoHandWrittenCSharpBlocks` (`:37`);
  - `SnippetsPointToExistingSections` (`:69`);
  - `SnippetSourcesAreCompiledCode` (`:88`).
- `Tests/Core/EditMode/ManualLinksTests.cs:18`:
  - `EveryHelpUrlHasPageAndAnchor` (`:30`), `EveryDocUrlHasPage` (`:47`) e `NoStubPages` (`:60`) leem o manual;
  - `PackageFiles_RepositoryRoot_HasManual` (`:21`) é estrutural e fica fora da categoria.
- Ficam na suíte dos tickets, porque só leem o código: `TypeCatalogSummaryTests`, `ReferenceGeneratorTests`, `DocfxConfigTests` e o teste de `[HelpURL]` dos validadores.
- O Unity Test Framework 1.6.0 instalado aceita `-testCategory` com itens separados por `;`. Cada item é uma expressão regular, e um item que começa com `!` exclui (`Library/PackageCache/com.unity.test-framework@aebcb98460c6/UnityEngine.TestRunner/TestRunner/RuntimeTestRunnerFilter.cs:31` e `:68-91`; opção em `UnityEditor.TestRunner/CommandLineTest/SettingsBuilder.cs:47`).
- `.github/workflows/docs.yml:18-20` e `:29-31`: gatilhos de caminho em `Packages/com.spellbook/Tests/**/Examples/**` e `Packages/com.spellbook/Samples~/Sandbox/Scripts/**`, no `push` e no `pull_request`.
- `Documentation~/guia-do-manual.md:150-153` e `:160`: o fluxo "quem muda um `.cs` do pacote" regenera a referência no mesmo diff.

## Design

- Uma constante única para o nome da categoria, `"Documentacao"`, sem acento para casar a expressão do filtro sem surpresa. Ela fica num arquivo novo do assembly de testes do Core: `Tests/Core/EditMode/Documentation/TestCategories.cs`, com `public static class TestCategories { public const string Documentation = "Documentacao"; }`, no namespace `SpellBook.Core.Tests.Documentation`.
- `[Category(TestCategories.Documentation)]` vai:
  - na classe `ReferenceUpToDateTests`;
  - na classe `ManualCodeExamplesTests`;
  - nos três métodos de `ManualLinksTests` citados acima.

  Nenhum assert muda.
- Suíte dos tickets (`/revisao`): EditMode e PlayMode com `-testCategory "!Documentacao"`. No passe de documentação, a suíte roda com `-testCategory "Documentacao"` e depois sem filtro, e as duas precisam ficar verdes.
- `docs.yml`: os dois gatilhos de código saem do `push` e do `pull_request` (emenda de 2026-11-03 da 0040). O `workflow_dispatch` continua.
- Guia do manual, parte 8: o fluxo "quem muda um `.cs`" passa a ser "no passe de documentação", e o texto cita a categoria e os dois comandos.
- O que não muda: os geradores, as guardas, a impressão digital e o build `--strict`.

## Quebras de compatibilidade

Nenhuma no pacote. No processo, a suíte da revisão passa a excluir a categoria `Documentacao`. Não entra no CHANGELOG, porque não muda nada para quem usa o pacote.

## Tickets

### PD1 — Guardas de documentação na categoria `Documentacao`
- Commit: `test(core): guardas de documentação só no passe de documentação`
- Escopo:
  - `Tests/Core/EditMode/Documentation/TestCategories.cs` (novo, com `.meta`);
  - `Tests/Core/EditMode/Documentation/ReferenceUpToDateTests.cs`;
  - `Tests/Core/EditMode/ManualCodeExamplesTests.cs`;
  - `Tests/Core/EditMode/ManualLinksTests.cs`;
  - `Tests/Core/EditMode/Documentation/DocumentationCategoryTests.cs` (novo, com `.meta`);
  - `.github/workflows/docs.yml`;
  - `Documentation~/guia-do-manual.md` (parte 8).
- Testes red:
  - `DocumentationCategory_HoldsEveryManualAndReferenceGuard` (EditMode, em `DocumentationCategoryTests`). Por reflexão no próprio assembly, confere duas coisas:
    - cada um dos 11 testes listados no Contexto existe e tem `[Category("Documentacao")]`, direto ou herdado da classe;
    - `PackageFiles_RepositoryRoot_HasManual` não tem a categoria.

    Antes do PD1, ele falha pela categoria ausente. Ele também impede que uma guarda saia da categoria ou seja apagada sem ninguém ver.
  - Critério de linha de comando, que falha antes: um EditMode com `-testCategory "Documentacao"` dá `total="0"` no `<test-run>`.
- Critério de pronto:
  - o teste novo fica verde;
  - `-testCategory "Documentacao"` dá `total="11"`;
  - `-testCategory "!Documentacao"` não tem nenhum dos 11 no XML, e o resto da suíte fica verde mesmo com a referência e o manual atrasados;
  - o diff não muda nenhum assert das guardas;
  - o `docs.yml` não tem mais os caminhos de `Tests/**/Examples/**` nem de `Samples~/Sandbox/Scripts/**`.
- Unity: sim, com WIP 1. Entra logo depois do EV10, antes do EV11. Depende só do que está na branch do lote.
- Tamanho estimado: ~70 linhas e 7 arquivos. São ~45 do teste novo, ~6 de constante, ~5 de atributos e ~8 do `docs.yml`. O guia conta só nos arquivos.

## Regra provisória até o PD1

Vale a da emenda de 2026-11-03 da 0007: nenhum ticket escreve no manual. A Fábrica regenera a referência só quando uma guarda de `ReferenceUpToDateTests` reprova, e isso acontece em todo ticket que muda `.cs`. Se reprovar uma guarda de `ManualLinksTests` ou de `ManualCodeExamplesTests`, ela devolve o ticket. O EV10 segue esta regra. Ele não precisa voltar se já escreveu no `gas.md`: o texto fica, e o passe o revê.

## Specs com regra de saída gerada por ticket

As regras abaixo deixam de valer pelas emendas de 2026-11-03 da 0007 e da 0040. Cada uma recebeu uma nota com a data:
- `gas-evento-acoes-alvos.md`, emenda de 2026-11-02 "lote 8, saída gerada e catálogo";
- `gas-dano-escudo-e-morte.md`, nota "Saída gerada" do DM7 e do DM8;
- `referencia-catalogo-e-api.md`, D6 e o resumo dos pares.
