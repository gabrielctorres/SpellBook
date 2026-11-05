# Design doc: Organização dos assemblies (asmdefs)
Épico: `epic:organizacao-dos-asmdefs` · Backlog: B-040 · ADRs: 0012 (emenda depois do gate), 0044 (emenda pela M2; antes era a 0040, que a 0044 substitui em parte) · Status: Rascunho (discussão de melhorias; gate parcial de 2026-11-04: M2 (b) decidida)

## Origem
O B-040 pedia que fosse retomado um levantamento já feito numa discussão entre agentes. Procurei em `Documentation~/` (backlog, `planos/`, `prd/`, `spec/`, `adr/`, `decisoes.md`) e em `.claude/`, e não achei esse registro. A única discussão sobre asmdefs registrada é a da 0-M, que deu origem à ADR 0012 e às emendas dela. Por isso, este documento é a discussão de melhorias que o item pedia nesse caso. Ele foi escrito a partir do inventário de 2026-11-04 dos 50 asmdefs: 13 de runtime, 13 de Editor, 22 de teste, o `SpellBook.Testing` e o do Sandbox.

## Problema
Para o dev que mantém o pacote ou estende um módulo, a organização tem quatro pontos fracos:
- **Ferramenta de manutenção junto do pacote.** As ferramentas que geram a documentação do repositório (`Editor/Core/Documentation/`: DocFX, catálogo, mapa da Unity e impressão digital, 6 arquivos) estão dentro do `SpellBook.Core.Editor`. Elas vão para todo projeto que instala o pacote.
- **Testes de unidade e guardas do repositório misturados.** O `SpellBook.Core.Tests.EditMode` junta os testes do Core com as guardas do repositório inteiro: `Structure/`, `Documentation/` e os exemplos do manual. Por causa delas, os testes do Core referenciam 9 assemblies, entre eles GAS, Events, Pooling e GAS.Bridges.
- **Namespace raiz curto.** O `rootNamespace` do `SpellBook.Core` e do `SpellBook.Core.Editor` é `SpellBook`, então um arquivo novo nasce fora do namespace do assembly. O `FolderStructureCreator` já está em `SpellBook.Editor`, mas sai no B-019a.
- **Regra espalhada.** A tabela da ADR 0012 está espalhada em seis emendas, e quem cria um módulo precisa ler todas.

## Propostas para a discussão
| # | Proposta | Recomendação |
|---|---|---|
| M1 | Mover as guardas do repositório (`Tests/Core/EditMode/Structure/`, `Documentation/` e o teste dos exemplos do manual) para um asmdef novo, `SpellBook.Repository.Tests.EditMode`, em `Tests/Repository/EditMode/`. Os testes do Core passam a referenciar só Core, Core.Editor, Time e Testing | Fazer |
| M2 | Tirar as ferramentas de documentação do `SpellBook.Core.Editor`. **(a)** Asmdef próprio no pacote, `SpellBook.Documentation.Editor`: fica isolado, mas ainda vai para o usuário. **(b)** Levar para o projeto host, em `Assets/Editor/Documentation/`, fora do pacote, com os testes delas em `Assets/`: o usuário não recebe nada disso | **Decidido (usuário, 2026-11-04): opção (b), em `Assets/`** |
| M3 | `rootNamespace` igual ao nome do asmdef no Core e no Core.Editor | Fazer (junto da M1) |
| M4 | Fundir os `.Editor` mínimos: Time.Editor (11 linhas), Pooling.Editor (27) e Localization.Editor (11) só registram o `SpellBookEditor` | **Não fazer.** Fundir quebraria a regra "um `.Editor` por módulo" da 0012, e um Inspector de fallback no Core.Editor afetaria os tipos do projeto do usuário e entraria em conflito com outros editores de fallback. O custo de compilação é pequeno |
| M5 | Juntar os testes PlayMode pequenos (Core 3 arquivos, Time 2, Cues 2, UI.UGUI 1) num único asmdef | **Não fazer.** O filtro de testes por módulo e a conferência das referências de cada módulo se perderiam, para economizar poucos arquivos |
| M6 | Consolidar a 0012 numa ADR nova, que a substitui, com a tabela final em um só lugar (módulos, `.Editor`, testes, módulo opcional e asmdefs fora do pacote) | Fazer, depois das respostas, pelo Desenhista e junto do gate |
| M7 | Separar os módulos em vários pacotes UPM (`com.spellbook.gas`, `com.spellbook.events`…) | Fora deste épico. Muda a instalação e a versão, e precisaria de uma ADR própria se o usuário quiser |

## Critérios de aceite (com M1, M2 (b), M3 e M6)
1. **CA1.** Nenhum tipo de `Editor/Core/Documentation/` vai no pacote. O menu e o `-executeMethod` da geração continuam funcionando no projeto host, e a saída gerada é a mesma (as guardas do catálogo e dos exemplos da 0044 passam).
2. **CA2.** O `SpellBook.Core.Tests.EditMode` referencia só Core, Core.Editor, Time, `SpellBook.Testing` e os assemblies de teste da Unity.
3. **CA3.** As guardas do repositório rodam no asmdef novo, na categoria de antes (as guardas `Documentacao` continuam `Documentacao`), e a contagem de testes da suíte não muda.
4. **CA4.** Um arquivo novo criado em `Runtime/Core/` ou em `Editor/Core/` nasce no namespace do assembly.
5. **CA5.** A guarda de asmdefs (`AsmdefRulesTests`) confere a tabela consolidada da M6 e falha com um asmdef fora dela.

## Cenário no Sandbox
O épico não muda comportamento em jogo. O Sandbox prova que nada quebrou:
1. Importe o Sandbox (`Packages/com.spellbook/Samples~/Sandbox`) num projeto sem o host do repositório. Ele compila sem erro, e `SpellBook > Documentação` não aparece no menu (CA1).
2. Rode a cena: Bola de Fogo, queimadura, pausa e câmera lenta funcionam como antes.

## Fora do escopo
- M4, M5 e M7.
- Mudar namespaces públicos ou mover tipos de runtime entre assemblies.

## Decisões do épico
- **Gate parcial de 2026-11-04 (usuário):** M2 na opção (b), com as ferramentas de documentação em `Assets/`. As outras propostas (M1, M3 e M6 a fazer; M4, M5 e M7 recusadas) continuam esperando o gate.
- A M2 (b) leva a ferramenta para `Assets/`. A 0012 já permite asmdefs em `Assets/` fora da tabela do pacote (emenda de 2026-10-30).
- **Migração para o GitBook (B-042, 2026-11-04).** O GB3 de [migracao-gitbook.md](migracao-gitbook.md) apaga a parte do DocFX (`DocfxConfig`, `ReferenceFingerprint`, `UnityXrefMap` e os testes deles), e o GB4 acrescenta o expansor de exemplos. Por isso, o AS2 vem depois do GB4 e move só o que sobra: o catálogo, a geração e o expansor, com os testes deles. A emenda que muda o lugar da "entrada única do Editor" passa a ser da 0044, e não mais da 0040. A conta do AS2 caiu de ~120 linhas e ~8 arquivos para ~60 linhas e ~6 arquivos, porque os movimentos puros (`R100`) contam 1 linha e nenhum arquivo, e os arquivos do DocFX já não existem.

## Tickets
| Ticket | Objetivo | CA | Depende de | Unity |
|---|---|---|---|---|
| AS1 | Asmdef `SpellBook.Repository.Tests.EditMode` com as guardas do repositório (movimento puro) e `rootNamespace` do Core e do Core.Editor. ~80 linhas, ~5 arquivos | CA2, CA3, CA4 | gate (M1, M3) | sim |
| AS2 | Ferramentas de documentação que sobram depois do GitBook (catálogo, geração e expansor de exemplos) e os testes delas em `Assets/` (M2 b); `AGENTS.md`, `guia-do-manual.md` e as skills que citam o caminho são atualizados. ~60 linhas, ~6 arquivos | CA1 | AS1, GB4 de [migracao-gitbook.md](migracao-gitbook.md), 0044 aceita | sim |
| AS3 | `AsmdefRulesTests` contra a tabela da ADR consolidada (M6). ~150 linhas, ~2 arquivos | CA5 | AS2, ADR da M6 aceita | sim |

## Validação de uso
| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|

## AS1 — Asmdef das guardas do repositório e rootNamespace do Core
### Detalhe (2026-11-04)
- **A Spec subestimou o CA2.** Mover `Structure/`, `Documentation/` e o `ManualCodeExamplesTests` não basta para o CA2. Quatro testes do Core que não são guardas também usam GAS, Events, Pooling ou GAS.Bridges: `ProjectValidationTests` (Events, GAS, GAS.Bridges e Pooling), `Inspector/ShowIfRuleTests` (1 caso com `ModifierValueSource`), `Inspector/SummaryFormatterTests` (campos `ScalableFloat` na fixture) e `Inspector/TypePickerCatalogTests` (1 caso com `GameplayAction`). O `Structure/PackageFiles` sai do asmdef, mas `ConditionCompositionTests.Core_HasNoTagsReference`, `DocUrlsTests` e `ManualLinksTests` o usam. Decisão do Controlador, 2026-11-04, opção A: `ProjectValidation` (só a parte entre módulos), `DocUrls` e `ManualLinks` vão para o Repository; os casos de GAS do ShowIf e do SummaryFormatter vão para o `SpellBook.GAS.Tests.EditMode`; o TypePicker passa a usar uma base de runtime do Core; o `Core_HasNoTagsReference` vai para o `AsmdefRulesTests`.
- **Divisão (ADR 0042, item 5; aprovada pelo Controlador em 2026-11-04).** Se os movidos trocassem de namespace, cada um contaria como 1 arquivo, e o AS1 inteiro passaria de ~35 arquivos, bem acima de +30%. O ticket se divide em **AS1a** (esta issue, #349; CA3 e CA4) e **AS1b** (issue nova; CA2; depende do AS1a).
- **Namespace dos movidos, de propósito.** Opção (b) do Controlador, em 2026-11-04: os arquivos movidos continuam em `SpellBook.Core.Tests.*`, com movimento puro (`R100`). O `rootNamespace` `SpellBook.Repository.Tests` do asmdef novo vale só para arquivo novo. Trocar o namespace dos movidos é uma melhoria opcional futura, fora do AS1a e do AS1b. Ela não cabe em dois tickets que compilam sozinhos: `Structure/` com os 5 `using` dos consumidores dá 14 arquivos.

#### AS1a
- CA: CA3, CA4 (parte de M1 e M3)
- Escopo:
  - `Packages/com.spellbook/Tests/Repository/EditMode/SpellBook.Repository.Tests.EditMode.asmdef` (novo): `rootNamespace` `SpellBook.Repository.Tests`; refs Core, Core.Editor, Tags, Time, Events, Pooling, GAS, GAS.Bridges, Testing e os TestRunner; `autoReferenced` false, `UNITY_INCLUDE_TESTS`, só Editor.
  - Movimento puro (`R100`), sem alterar o conteúdo, de `Tests/Core/EditMode/` para `Tests/Repository/EditMode/`: `Structure/` (menos o `AsmdefRulesTests`, que muda; o `PackageFiles` também vai sem mudança), `Documentation/` (6), `ManualCodeExamplesTests.cs`, `ManualLinksTests.cs` e `DocUrlsTests.cs`. Os namespaces continuam `SpellBook.Core.Tests.*` (ver acima).
  - `Structure/AsmdefRulesTests.cs` (movido com alteração): `AllExpectedAssembliesExist` (:695) espera também o `SpellBook.Repository.Tests.EditMode`; `AsmdefRules_Scan_FindsCoreAndThisTestAsmdef` (:43) procura o asmdef novo; entram o `Core_HasNoTagsReference`, vindo de `ConditionCompositionTests.cs:103`, e o teste novo do CA4. O teste do CA4 pergunta à Unity o namespace de um arquivo novo pela `CompilationPipeline.GetAssemblyRootNamespaceFromScriptPath`, a mesma API que dá o namespace a um script criado no Editor.
  - `ConditionCompositionTests.cs:103-111`: sai o `Core_HasNoTagsReference`, com os `using` de `Structure` e `Reflection`.
  - `ProjectValidationTests.cs` → `Tests/Repository/EditMode/ProjectValidationTests.cs` (movido com alteração, namespace `SpellBook.Core.Tests` mantido): ficam `Validator_ReportsEachR35Error`, `Validator_FixedObjects_NoErrors`, `Project_HasNoValidationErrors` e `ValidateTypes_RuntimeAssemblies_NoErrors`, com os ajudantes de Events, GAS e Pooling.
  - `Tests/Core/EditMode/ProjectValidatorTests.cs` (novo): os 4 casos que usam só o Core, com as fixtures do Core: `Validator_ExceptionInValidate_BecomesErrorAndContinues`, `Validator_MissingSerializeReferenceType_IsError_DataKept`, `ValidateTypes_TypeWithoutHelpUrl_IsErrorOnItsScript` e `ValidateTypes_TypeWithoutScript_IsErrorWithoutTarget`.
  - `Editor/Core/AssemblyInfo.cs:3`: `InternalsVisibleTo("SpellBook.Repository.Tests.EditMode")`, porque o `TypeCatalogTests` usa o `TypeCatalog.Build(IEnumerable<Assembly>)`, que é `internal`.
  - `Runtime/Core/SpellBook.Core.asmdef:3` → `SpellBook.Core`; `Editor/Core/SpellBook.Core.Editor.asmdef:3` → `SpellBook.Core.Editor`.
- Testes red (EditMode): `AsmdefRulesTests.AllExpectedAssembliesExist` (falta o asmdef novo) e `AsmdefRulesTests.AsmdefRules_NewFileInCore_GetsAssemblyNamespace`, com 2 casos, `Runtime/Core` e `Editor/Core` (a Unity devolve `SpellBook`). Os dois são escritos antes da mudança, ainda no asmdef do Core.
- Critério de pronto: os dois red passam; `AsmdefRulesTests`, `DocumentationCategoryTests`, `ProjectValidationTests`, `ProjectValidatorTests` e `ConditionCompositionTests` passam com filtro; o total de testes dessas classes, somando o Core e o Repository, é o mesmo de antes, mais os 2 casos novos do CA4.
- Tamanho: ~270 linhas e 8 arquivos, medidos na implementação: asmdef novo 32, os 2 asmdefs do Core 4, `AssemblyInfo` 1, `AsmdefRulesTests` 24, `ConditionCompositionTests` 12, `ProjectValidationTests` 66, `ProjectValidatorTests` 114 e os 17 movimentos puros 17. Exceção: nenhuma
- Commit: `refactor(tests): asmdef das guardas do repositório e rootNamespace do Core`

#### AS1b
- CA: CA2
- Escopo:
  - `Inspector/ShowIfRuleTests.cs:84-120`: sai o `ShowIf_ValueVisibleOnlyForValueType` com os ajudantes `GasShowIf` e `ValueSourceField`. Ele vai para `Tests/GAS/EditMode/ModifierValueSourceShowIfTests.cs` (novo).
  - `Inspector/SummaryFormatterTests.cs`: saem os campos e os marcadores `ScalableFloat` da fixture e o valor esperado é ajustado. O formato do `ScalableFloat` (constante, tabela e por nível) vai para `Tests/GAS/EditMode/ScalableFloatSummaryTests.cs` (novo), que espera `"2,5 %Dano por nível"`.
  - `Inspector/TypePickerCatalogTests.cs:46-52,172`: `Candidates_IncludeDerivedFromAnotherAssembly` usa `IRandomSource` (runtime do Core) no lugar de `GameplayAction`. A fixture passa a ser `PickRandomSource`.
  - `Tests/Core/EditMode/SpellBook.Core.Tests.EditMode.asmdef`: refs só Core, Core.Editor, Time, Testing e os TestRunner.
  - `Tests/Repository/EditMode/Structure/AsmdefRulesTests.cs` (o AS1a o moveu; conferido em 2026-11-04): teste novo `AsmdefRules_CoreEditModeTests_ReferenceOnlyCoreCoreEditorTimeAndTesting`, perto do `AsmdefRules_EveryModuleTestAsmdef_ReferencesTimeAndTesting` (:235).
- Testes red (EditMode): `AsmdefRules_CoreEditModeTests_ReferenceOnlyCoreCoreEditorTimeAndTesting` (o asmdef de hoje tem 9 referências do SpellBook).
- Critério de pronto: o red passa; `ShowIfRuleTests`, `SummaryFormatterTests`, `TypePickerCatalogTests`, `ModifierValueSourceShowIfTests` e `ScalableFloatSummaryTests` passam com filtro, com o mesmo número de casos de antes, mais o novo do formato.
- Tamanho: ~200 linhas e 7 arquivos. Exceção: nenhuma
- Commit: `refactor(tests): testes do Core só com Core, Core.Editor, Time e Testing`

## AS2 — Ferramentas de documentação e testes em Assets/
### Detalhe (2026-11-05)
- CA: CA1
- **O que sai do pacote.** Lido em 2026-11-05, na `main` (`d257005`), depois do GB3 e do GB4. Em `Editor/Core/Documentation/` sobram 3 arquivos: `ReferenceGenerator.cs` (menu, `-executeMethod` e o `ExpandExamples` do GB4), `TypeCatalog.cs` e `TypeCatalogPage.cs`. Sete testes usam esses tipos e saem junto: `ReferenceGeneratorTests`, `TypeCatalogTests`, `TypeCatalogPageTests`, `TypeCatalogSummaryTests`, as duas guardas `Documentacao` (`ReferenceUpToDateTests` e `ManualCodeExamplesTests`) e o `DocumentationCategoryTests`. O último entra na lista porque usa `typeof` das duas guardas, e um asmdef do pacote não pode referenciar um de `Assets/`. Ficam no pacote o `ManualLinksTests`, o `ManualMarkdown`, os testes dele e o `TestCategories`, porque nenhum deles usa tipo da geração.
- **Namespace mantido (decisão da Fábrica; segue o precedente do AS1a).** Os arquivos movidos continuam em `SpellBook.Core.Editor.Documentation` e `SpellBook.Core.Tests.*`, com movimento puro (`R100`). Assim, o `-executeMethod SpellBook.Core.Editor.Documentation.ReferenceGenerator.GenerateFromCommandLine` de `guia-do-manual.md:152` e de `.claude/skills/implementar/SKILL.md:18` continua igual. O `rootNamespace` dos asmdefs novos vale só para arquivo novo. Trocar o namespace dos movidos fica como melhoria futura opcional. Ela custaria uns 10 arquivos (3 ferramentas, 7 testes com `using`) e mais as duas linhas do comando nos documentos.
- **Nome dos asmdefs sem o prefixo `SpellBook.`** O `TypeCatalog.IsPackageAssembly` (`TypeCatalog.cs:90-95`) trata como do pacote todo assembly que começa por `SpellBook.` e não é de teste. Com o prefixo, a ferramenta entraria na varredura do catálogo como se fosse do pacote. A saída não mudaria, porque ela não tem campo com `[TypePicker]`, mas a regra ficaria falsa. O nome segue o padrão de `Assets/Scenarios/C1/C1.PoolingOnly.asmdef`.
- **Acesso aos ajudantes de teste.** Os testes movidos usam o `PackageFiles.RepositoryRoot` e o `TestScratch`, que são `internal` no `SpellBook.Repository.Tests.EditMode`. Eles passam a `public`, junto com os tipos que aparecem na assinatura pública do `PackageFiles`. A alternativa seria um `InternalsVisibleTo` no pacote apontando para um assembly do host, o que deixaria no pacote um rastro do host. O assembly de teste continua `autoReferenced: false` e sob `UNITY_INCLUDE_TESTS`, então nada disso vira API do pacote.
- Escopo:
  - **Novo** `Assets/Editor/Documentation/Repository.Documentation.Editor.asmdef`: `rootNamespace` `SpellBook.Repository.Documentation`; refs `SpellBook.Core` e `SpellBook.Core.Editor`; só Editor, `autoReferenced` false.
  - **Novo** `Assets/Editor/Documentation/AssemblyInfo.cs`: `InternalsVisibleTo("Repository.Documentation.Tests.EditMode")`, porque o `TypeCatalogTests` usa o `TypeCatalog.Build(IEnumerable<Assembly>)` (`TypeCatalog.cs:49`), que é `internal`.
  - **Movimento puro (`R100`)**, com o `.meta` de cada um, de `Packages/com.spellbook/Editor/Core/Documentation/` para `Assets/Editor/Documentation/`: `ReferenceGenerator.cs`, `TypeCatalog.cs`, `TypeCatalogPage.cs`. A pasta do pacote fica vazia e sai com o `.meta` dela (remoção pura).
  - `Packages/com.spellbook/Editor/Core/AssemblyInfo.cs:4`: sai o `InternalsVisibleTo("SpellBook.Repository.Tests.EditMode")`. Ele só existia para o `TypeCatalogTests` (Detalhe do AS1a), e nenhum outro teste do Repository usa `internal` do Core.Editor (conferido por busca em 2026-11-05: `TypePickerField`, `TypePickerNode`, `ShowIfDrawer` e os editores de `CoreEditors`). É remoção pura.
  - **Novo** `Assets/Tests/Documentation/Repository.Documentation.Tests.EditMode.asmdef`: `rootNamespace` `SpellBook.Repository.Documentation.Tests`; refs `Repository.Documentation.Editor`, `SpellBook.Core`, `SpellBook.Core.Editor`, `SpellBook.Repository.Tests.EditMode` (`TestCategories`, `PackageFiles`, `TestScratch` e o `typeof(ManualLinksTests)` do `DocumentationCategoryTests`) e os TestRunner; `nunit.framework.dll`, `overrideReferences` true, só Editor, `UNITY_INCLUDE_TESTS`, `autoReferenced` false. O pacote já está em `testables` (`Packages/manifest.json:11-13`), então o assembly de teste do Repository compila no host.
  - **Movimento puro (`R100`)**, com o `.meta`, para `Assets/Tests/Documentation/`: de `Tests/Repository/EditMode/Documentation/` saem `ReferenceGeneratorTests.cs`, `TypeCatalogTests.cs`, `TypeCatalogPageTests.cs`, `TypeCatalogSummaryTests.cs`, `ReferenceUpToDateTests.cs` e `DocumentationCategoryTests.cs`; de `Tests/Repository/EditMode/` sai `ManualCodeExamplesTests.cs`.
  - `Packages/com.spellbook/Tests/Repository/EditMode/Structure/PackageFiles.cs:16,92,99,134,147`: `PackageFiles`, `AsmdefKind`, `AsmdefFile`, `AsmdefData` e `VersionDefineData` passam de `internal` a `public`.
  - `Packages/com.spellbook/Tests/Repository/EditMode/Structure/TestScratch.cs:13`: `TestScratch` passa de `internal` a `public`.
  - `Packages/com.spellbook/Tests/Repository/EditMode/Structure/SourceRulesTests.cs` (perto de `:56`): teste novo `Package_HasNoDocumentationMenu`. Nenhum `.cs` de `Runtime/` ou `Editor/` do pacote tem `"SpellBook/Documentação/"`. A busca não olha `Tests/`, porque o próprio teste contém o texto.
  - **Novo** `Assets/Tests/Documentation/DocumentationToolsLocationTests.cs` (namespace `SpellBook.Repository.Documentation.Tests`):
    - `Generator_IsNotInThePackage`: `PackageInfo.FindForAssembly(typeof(ReferenceGenerator).Assembly)` é `null`, e o assembly é o `Repository.Documentation.Editor`.
    - `ExecuteMethod_InGuide_ResolvesToGenerator`: lê o valor de `-executeMethod` em `Documentation~/guia-do-manual.md`, acha o tipo nos assemblies carregados e confere que o método existe, é `public static` e não tem parâmetros. Este teste não é red, porque já passa hoje. Ele prova a parte do CA1 sobre o `-executeMethod` sem rodar a geração fora do passe de documentação (emenda de 2026-11-03 da 0007).
  - `AGENTS.md`, `guia-do-manual.md`, `Documentation~/README.md`, skills e agentes: **nenhuma mudança**. Busquei `Editor/Core/Documentation`, `Core.Editor`, `ReferenceGenerator`, `executeMethod`, `Tests/Repository` e `Core/EditMode/Documentation` em 2026-11-05. Os únicos achados são as duas linhas do `-executeMethod` (`guia-do-manual.md:152`, `implementar/SKILL.md:18`) e a lista das guardas pelo nome da classe (`guia-do-manual.md:148`). Nenhum deles muda com o namespace mantido.
- Testes red (EditMode):
  1. `SourceRulesTests.Package_HasNoDocumentationMenu` (no `SpellBook.Repository.Tests.EditMode`): falha hoje em `Editor/Core/Documentation/ReferenceGenerator.cs:20`.
  2. `DocumentationToolsLocationTests.Generator_IsNotInThePackage` (no `Repository.Documentation.Tests.EditMode`): o `FindForAssembly` devolve hoje o `com.spellbook`. Ordem: primeiro os dois asmdefs novos, ainda sem script na pasta da ferramenta, e o arquivo do teste. Depois a rodada red, e só então os movimentos.
- Critério de pronto:
  - Os 2 red passam, e o `ExecuteMethod_InGuide_ResolvesToGenerator` passa.
  - Com filtro, nos dois assemblies, passam as guardas `Documentacao` (`ReferenceUpToDateTests.CommittedCatalog_MatchesGenerated`, que prova o catálogo igual byte a byte, e os 4 testes do `ManualCodeExamplesTests`, com o `ExpandedBlocksMatchSections`, que prova os exemplos iguais), além de `DocumentationCategoryTests`, `ReferenceGeneratorTests`, `TypeCatalogTests`, `TypeCatalogPageTests`, `TypeCatalogSummaryTests`, `SourceRulesTests`, `TestScratchTests` e `AsmdefRulesTests`.
  - O total de testes dessas classes, somando o pacote e `Assets/`, é o de antes mais os 3 novos.
  - `git status` mostra os 10 movimentos como rename (`R100`). `Packages/com.spellbook/Editor/Core/Documentation/` não existe. Nenhum arquivo de `Documentation~/manual/` muda.
  - O passo 1 do cenário do Sandbox (importar num projeto sem o host, sem o menu **SpellBook > Documentação**) é conferência manual e fica para o usuário no PR.
- Tamanho: ~145 linhas e 7 arquivos, pela ADR 0042. Conta: asmdef da ferramenta ~22, asmdef dos testes ~28, `AssemblyInfo.cs` novo 3, `DocumentationToolsLocationTests.cs` ~45, `SourceRulesTests.cs` ~15, `PackageFiles.cs` 10, `TestScratch.cs` 2, os 10 movimentos puros 10 (1 linha cada, nenhum arquivo). Não contam: os `.meta` (inclusive os das pastas novas, gerados pela Unity), a remoção pura do `AssemblyInfo.cs` do Core.Editor e a pasta vazia do pacote. A estimativa do design doc era ~60 linhas e ~6 arquivos. A diferença vem do acesso aos ajudantes de teste e dos 2 testes do CA1. Exceção: nenhuma.
- Unity: **sim**. Arquivos em comum com o EX3 (#298, `modulos/gas.md`): **sim, um**. O escopo do EX3 (`referencia-catalogo-e-api.md:756`) inclui "a guarda", o `ManualCodeExamplesTests.cs`, que tira o `modulos/gas.md` da `PendingPages` (`:25-30`). O AS2 move esse arquivo. Por isso, os dois **não podem rodar em paralelo**. Em série, qualquer ordem funciona. Se o AS2 vier antes, o EX3 edita o arquivo em `Assets/Tests/Documentation/ManualCodeExamplesTests.cs`. Se vier depois, o movimento do AS2 continua puro sobre o conteúdo do dia. O `GasManualExamples.cs` e o `gas.md` não estão no AS2.
- Pendências fora do ticket (não são da Fábrica):
  - A ADR 0040 (`:25`, item 2) e a 0044 (`:18`) ainda dizem que a entrada única fica em `SpellBook.Core.Editor`, em `Editor/Core/Documentation/`. Pelo design doc ("Decisões do épico"), a emenda que muda o lugar é da 0044. Ela é do Desenhista e não existe ainda.
  - A emenda de 2026-10-30 da 0012 diz que um asmdef em `Assets/` "pode referenciar só os módulos de runtime que o cenário exercita". Essa regra foi escrita para os cenários. Os dois asmdefs novos referenciam o Core.Editor e um asmdef de teste do pacote. O design doc já registra que a 0012 permite asmdefs em `Assets/`, e a tabela consolidada da M6 (AS3) precisa cobrir os asmdefs de ferramenta.
- Commit: `chore(docs): ferramentas de documentação e testes em Assets/`
