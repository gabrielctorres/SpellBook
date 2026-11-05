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
  - `AsmdefRulesTests.cs`: teste novo `AsmdefRules_CoreEditModeTests_ReferenceOnlyCoreCoreEditorTimeAndTesting`.
- Testes red (EditMode): `AsmdefRules_CoreEditModeTests_ReferenceOnlyCoreCoreEditorTimeAndTesting` (o asmdef de hoje tem 9 referências do SpellBook).
- Critério de pronto: o red passa; `ShowIfRuleTests`, `SummaryFormatterTests`, `TypePickerCatalogTests`, `ModifierValueSourceShowIfTests` e `ScalableFloatSummaryTests` passam com filtro, com o mesmo número de casos de antes, mais o novo do formato.
- Tamanho: ~200 linhas e 7 arquivos. Exceção: nenhuma
- Commit: `refactor(tests): testes do Core só com Core, Core.Editor, Time e Testing`
