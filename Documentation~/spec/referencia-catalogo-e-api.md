# Spec: catálogo gerado e referência da API em DocFX (B-019g)
PRD: Documentation~/prd/referencia-catalogo-e-api.md · ADRs: 0040 (Aceita), 0025 (emenda de 2026-11-01, Aceita), 0003 (emenda de 2026-11-01 da saída gerada, Aceita), 0007 (emenda de 2026-11-01, aceita), 0026, 0027; 0041 (Proposta, só para a emenda E1 e E2)

Estado: Aprovada no gate de 2026-11-01. Issues: RC1 a RC8 são #286 a #293. A emenda dos extras E1 e E2, no fim desta Spec, foi aprovada no gate de 2026-11-01. A emenda de 2026-11-02 do caminho das páginas da API (D3, D4 e RC5) foi aprovada em 2026-11-02. A emenda de 2026-11-02 da versão do pacote na URL (D8, XR1 e XR2, com a pergunta X6 no fim da Spec e a emenda da 0041) foi aprovada no gate parcial de 2026-11-02, com o X6 como proposto. A emenda de 2026-11-02 do lote 8 (recontagem dos EX, o EX6 novo, a ordem nova da cadeia e os pares do WIP 2), no fim desta Spec, foi aprovada no gate parcial de 2026-11-02, com L1, L2 e L3 como propostos.

Respostas do usuário no gate:
- G1: a geração roda na máquina e o resultado é commitado; a Action só monta o site, sem Unity e sem secrets (D2, D4 a D6, RC3 a RC5).
- G2: resumo obrigatório (D7, RC6 a RC8).
- G3: a documentação é montada também nos pull requests, sem publicar (D5).
- G4: a saída gerada commitada não conta no teto do ticket. A regra virou a emenda de 2026-11-01 da 0003, "saída de gerador commitada".

Pela Q6 do gate da Spec `manual-reorganizacao.md` (2026-11-01), os tickets entram na fila com Unity logo depois do Lote 6, antes do lote das constantes e da divisão do GAS (MN8 a MN17). Assim, as páginas divididas já apontam para o catálogo.

Caminhos de código seguem a regra de prefixo da 0027: `Runtime/...`, `Editor/...` e `Tests/...` querem dizer `Packages/com.spellbook/...`.

## Contexto do código

- `Runtime/Core/Inspector/TypeCategoryAttribute.cs:8-21`: `[TypeCategory(path, Summary = ...)]`, só em classe e sem herança. O caminho usa `/` entre níveis.
- `Runtime/Core/Inspector/TypePickerAttribute.cs`: marca o campo `[SerializeReference]`, simples ou lista, que recebe o seletor. Há 13 campos marcados em `Runtime/`:
  - Eventos: `EventListener.responses` (`Runtime/Events/EventListener.cs:19`), `EventResponse.reactions` (`Runtime/Events/EventResponse.cs:45`), `DelayedReaction.reactions` (`Runtime/Events/Reactions/DelayedReaction.cs:24`);
  - GAS: `GameplayAbility.Actions` (`Runtime/GAS/Abilities/GameplayAbility.cs:69`), `AbilityDescription.Elements` (`AbilityDescription.cs:96`), `RepeatAction.Actions`, `ParallelAction.Actions`, `ConditionalAction.Then` e `Else` (`Actions/FlowActions.cs:20-95`), `SpawnProjectileAction.OnHitActions` (`:24`), `GameplayEffect.OnApplication`, `OnPeriodic` e `OnRemoval` (`Runtime/GAS/Effects/GameplayEffect.cs:114-124`).
- `Editor/Core/Inspector/TypePickerCatalog.cs`:
  - `GetCandidates(Type)` (`:52-71`) usa `TypeCache.GetTypesDerivedFrom`, filtra por `IsCandidate` (`:163-168`: concreto, não genérico, `[Serializable]`, com construtor sem parâmetros, fora de `UnityEngine.Object`) e ordena pelo nome exibido (ordinal). O resultado fica em cache;
  - `TypePickerEntry` (`:11-31`) tem `Type`, `DisplayName` (`ObjectNames.NicifyVariableName`), `CategoryPath` e `Summary`, lidos do `[TypeCategory]` em `CreateEntry` (`:170-175`).
- `Tests/Core/EditMode/Inspector/TypePickerCatalogTests.cs` cobre a regra de candidato com tipos de teste (`PickBase`, `PickCategorized`...). O assembly de testes do Core já usa o `SpellBook.Core.Editor` e vê os `internal` dele (`Editor/Core/AssemblyInfo.cs:3`).
- Hoje o `[TypeCategory]` está nas reações e respostas dos Eventos, nas respostas do `GAS.Bridges` e na `RaiseEventAction`. As respostas (`Runtime/Events/BuiltInResponses.cs`, `Runtime/GAS.Bridges/Channels/GasEventResponses.cs`) não têm `Summary`. As ações e os elementos de descrição do GAS não têm o atributo: `AnimationAction`, `RepeatAction`, `ParallelAction`, `ConditionalAction`, `ApplyEffectAction`, `PlayCueAction`, `SpawnProjectileAction`, `TargetingAction`, `WaitAction`, `DebugLogEffectAction`, `SpawnVFXEffectAction`, `TextElement`, `AttributeElement` e `TemplateElement`, em 9 arquivos.
- `Runtime/Core/DocUrls.cs:10`: `DocUrls.Base`, a única fonte da URL do site.
- `Tests/Core/EditMode/ManualLinksTests.cs`: `PackageAssemblies` (`:81-89`) define assembly do pacote como nome começando por `SpellBook.`, sem `.Tests` e sem `SpellBook.Testing`. `NoStubPages` (`:59-79`) varre `manual/` inteiro, e o catálogo commitado passa por ela.
- `.github/workflows/docs.yml`: um job `build` (Python, `mkdocs build --strict`) e um `deploy`, disparados no push para a `main` em `Documentation~/manual/**`, `mkdocs.yml`, `requirements-docs.txt` e no próprio workflow. Não há outra Action: o CI de testes com a Unity (0M-11) foi cancelado.
- `Packages/manifest.json` não tem pacote de IDE, então a Unity do host não gera `.csproj`.
- O `nav` explícito e a validação do `--strict` chegam no MN1, e o guia do manual no MN2 (`spec/manual-reorganizacao.md`). Os dois estarão na `main` antes do Lote 6.

## Design

### D1. O catálogo lê o seletor (P3 do PRD)

O catálogo não tem regra própria. Ele junta duas leituras que o Inspector já faz.

1. Tipos base: todo campo de instância com `[TypePicker]` declarado num tipo de assembly do pacote. O tipo base é o tipo do campo ou, se o campo é `List<T>` ou `T[]`, o `T`.
2. Entradas de cada base: `TypePickerCatalog.GetCandidates(base)`, na ordem que o seletor mostra, restritas a tipos de assemblies do pacote. Tipo de jogo, de teste ou de Sample aparece no seletor do projeto, mas não no catálogo do pacote.

Assembly do pacote é a regra do `ManualLinksTests` mais os Samples: nome começa por `SpellBook.`, sem `.Tests`, sem `SpellBook.Testing` e sem `SpellBook.Samples.`. A regra fica numa função pública do catálogo, `TypeCatalog.IsPackageAssembly(string)`.

```csharp
namespace SpellBook.Core.Editor.Documentation
{
    public sealed class TypeCatalogSection
    {
        public TypeCatalogSection(Type baseType, IReadOnlyList<string> fields, IReadOnlyList<TypePickerEntry> entries);
        public Type BaseType { get; }
        public IReadOnlyList<string> Fields { get; }          // "EventListener > Responses", nome do Inspector
        public IReadOnlyList<TypePickerEntry> Entries { get; } // mesma ordem do seletor
    }

    public static class TypeCatalog
    {
        public static IReadOnlyList<TypeCatalogSection> Build();                           // assemblies do pacote
        internal static IReadOnlyList<TypeCatalogSection> Build(IEnumerable<Assembly> assemblies);
        public static bool IsPackageAssembly(string assemblyName);
    }
}
```

- As seções saem em ordem ordinal do nome completo do tipo base, e os campos de cada seção em ordem ordinal. Uma base sem nenhuma entrada do pacote não gera seção.
- O campo aparece como o Inspector o mostra: o nome do tipo que o declara e o nome do campo em `ObjectNames.NicifyVariableName` (`EventListener > Responses`).
- O código fica em `Editor/Core/Documentation/`, no `SpellBook.Core.Editor`. Ele acha os tipos dos outros módulos por reflexão e não ganha referência nova de asmdef (0012).

Como o catálogo chama a mesma função do seletor, as duas listas não divergem por construção (C3). Um teste compara as duas listas mesmo assim, para pegar uma mudança futura que troque a chamada.

### D2. A geração roda na Unity, na máquina (P1 do PRD, G1)

Uma entrada só gera tudo, pelo menu no Editor e pelo `-executeMethod` em batchmode na máquina (0040, item 2):

```csharp
public static class ReferenceGenerator
{
    [MenuItem("SpellBook/Documentação/Gerar referência")]
    public static void GenerateFromMenu();
    public static void GenerateFromCommandLine();          // batchmode: sai com 0 ou 1
    public static void Generate(string documentationRoot); // as quatro etapas abaixo
    internal static void Generate(string documentationRoot, IDocfxMetadataRunner runner);
}

internal interface IDocfxMetadataRunner
{
    void Run(string documentationRoot, string metadataConfigPath); // lança exceção se o processo sair com código diferente de 0
}

public static class ReferenceFingerprint
{
    public static string Compute(string repositoryRoot); // SHA-256 em hexadecimal minúsculo
}
```

`documentationRoot` é `Documentation~/`, na raiz do repositório, que também é a raiz do projeto Unity (0027). O `Generate`, nesta ordem:
1. escreve `manual/referencia/catalogo.md` (D3);
2. escreve `api/obj/docfx.metadata.json` (D4), que o git ignora;
3. roda o `docfx metadata` pelo runner, que escreve os YAML em `api/reference/`;
4. escreve `api/reference.sha256` com o `ReferenceFingerprint.Compute`, só se a etapa 3 passou.

O runner real chama `dotnet docfx metadata api/obj/docfx.metadata.json` com o tratamento de aviso como erro, com `Documentation~/` como pasta de trabalho, por `System.Diagnostics.Process`, sem shell e com argumentos fixos. Se o processo sair com erro, a exceção leva a saída dele, e a impressão digital não é escrita. Os testes usam um runner falso, porque o processo do .NET é uma fronteira externa.

A impressão digital é o SHA-256 de um texto montado nesta ordem:
- para cada arquivo-fonte dos assemblies do pacote, em ordem ordinal do caminho relativo à raiz do repositório com `/`, o caminho e o conteúdo com as quebras normalizadas para `\n`;
- `Application.unityVersion`;
- o conteúdo de `Documentation~/.config/dotnet-tools.json`, com as quebras normalizadas.

A lista de fontes sai do `CompilationPipeline`, a mesma que o D4 usa. O código do gerador está num assembly do pacote, então mudar o gerador também muda a impressão digital.

Outras regras:
- A entrada registra um aviso no log com a lista dos tipos do catálogo sem `Summary`.
- No batchmode, uma exceção na geração sai com código 1.
- Commitados: `manual/referencia/catalogo.md`, `api/reference/**` e `api/reference.sha256`. Ignorados: `api/obj/` e `api/_site/`.

### D3. Página do catálogo

`TypeCatalogPage.Render(IReadOnlyList<TypeCatalogSection>)` devolve o texto da página. O formato:

```markdown
<!-- Página gerada do código por SpellBook.Core.Editor.Documentation. Não edite: mude o [TypeCategory] do tipo e rode SpellBook > Documentação > Gerar referência. -->
# Catálogo de tipos {#catalogo}

Os tipos que o seletor do Inspector oferece nos campos de ações, reações, respostas e elementos do pacote.
Cada nome leva à página do tipo na referência da API.

## `Reaction` {#spellbook-events-reaction}

Aparece em: `DelayedReaction > Reactions`, `EventResponse > Reactions`.

| Tipo | Categoria | Resumo |
|---|---|---|
| [Delayed Reaction](https://gabrielctorres.github.io/SpellBook/api/SpellBook.Events.DelayedReaction.html) | Fluxo | Roda uma lista de reações depois de um atraso. |
```

- O título da seção é o nome do tipo base em código. A âncora é o nome completo em minúsculas, com `.` e `+` trocados por `-`, então duas bases com o mesmo nome em módulos diferentes não colidem.
- **Emenda de 2026-11-02 (base genérica fechada).** A regra acima não dizia o que fazer com genéricos, e o RC2 usou `Type.Name` e `Type.FullName` direto. Para `Condition<ConditionContext>`, isso gera o título `` `Condition`1` ``, que quebra o código em linha, e uma âncora com colchetes, vírgulas e a versão do assembly, que muda a cada versão. Para um tipo genérico fechado, o título e a âncora passam a ser montados assim:
  - título: o nome sem o sufixo de aridade (`` `1 ``), seguido dos títulos dos argumentos entre `<` e `>`, separados por `, `, como em C#. `Condition<ConditionContext>` sai `` ## `Condition<ConditionContext>` ``. Os argumentos usam a mesma regra, então um argumento genérico também sai na forma de C#;
  - âncora: a âncora da definição genérica, com o nome completo sem o sufixo de aridade, seguida de `-` e da âncora de cada argumento, na ordem. `Condition<ConditionContext>` sai `{#spellbook-core-conditions-condition-spellbook-gas-conditions-conditioncontext}`. A âncora não tem nome nem versão de assembly, nem acento grave, colchete, vírgula, espaço ou `=`;
  - tipo não genérico continua como antes, e as outras seções do catálogo não mudam.
- O link de cada tipo é `DocUrls.Base + "api/" + uid + ".html"`, e o `uid` é o nome completo com `+` trocado por `.` (tipo aninhado). Quem garante que a página está nesse caminho é o `api/docfx.json`, que tira a pasta `reference/` do caminho de saída (emenda de 2026-11-02 do D4). A conferência de links da Action (D5) cobra o formato a cada build.
- **Emenda de 2026-11-02 (caminho das páginas da API).** No RC5, a Fábrica viu que o `docfx build` grava as páginas em `api/reference/<uid>.html`, porque o `api/docfx.json` do RC3 lista os YAML como `reference/**.yml` e o DocFX mantém o caminho relativo à pasta da configuração. O catálogo aponta para `api/<uid>.html`, então todo link quebraria. O formato do link continua o mesmo, e a correção vai para o `api/docfx.json` (D4). A pasta `reference/` é onde a geração guarda os YAML (`DocfxConfig.Destination`, `ReferenceGenerator`), um detalhe da máquina de geração. Pôr essa pasta no link deixaria a URL pública presa a esse detalhe. Mudar o link também mexeria no `TypeCatalogPage.cs`, o que muda a impressão digital e o catálogo e obriga a regenerar na Unity, num ticket que não usa a Unity. O conserto entra no RC5.
- Categoria vazia aparece como "Raiz", que é onde o seletor põe o tipo. Resumo vazio fica em branco.
- `|` vira `\|`, e quebra de linha vira espaço.
- A saída usa só `\n`, termina com uma quebra de linha e não tem data nem hora. A mesma fonte gera o mesmo texto (N2), e é isso que deixa o teste de guarda comparar byte a byte.
- O texto fixo da página é escrito pelas regras da humanizer (`C:/Users/gabri/.claude/skills/humanizer/SKILL.md`), como todo texto do manual.

### D4. DocFX: metadados na máquina, build na Action (P2 do PRD)

O DocFX tem duas etapas, e a 0040 (item 3) põe cada uma num lugar:
- o `docfx metadata` compila as fontes com as DLLs da Unity e escreve um YAML por tipo. Ele roda na máquina, dentro do `Generate`;
- o `docfx build` lê os YAML e monta o HTML, sem compilar e sem as DLLs. Os tipos da Unity aparecem pelo nome, a partir da seção `references` de cada YAML. Ele roda na Action e no build local.

Por isso a Action monta a API a partir do que está commitado. Commitar só uma configuração com as fontes não bastaria: o `docfx metadata` na Action não teria as DLLs da Unity e só passaria com o `allowCompilationErrors`, que a 0040 proíbe. Se, na versão travada, o `docfx build` pedir as DLLs ou falhar a partir dos YAML commitados, a Fábrica para e devolve no RC5.

`DocfxConfig` monta a configuração de metadados a partir de `UnityEditor.Compilation.CompilationPipeline.GetAssemblies(AssembliesType.Editor)`, filtrado por `TypeCatalog.IsPackageAssembly`:
- os arquivos-fonte dos assemblies do pacote, com caminho relativo à pasta da configuração (`api/obj/`) e barras `/`;
- como referências, as DLLs que esses assemblies usam, sem as saídas dos próprios assemblies do pacote e sem a biblioteca base do .NET da Unity. Esses caminhos são absolutos e da máquina, e é por isso que a configuração fica em `api/obj/`, ignorada;
- a biblioteca base do .NET vem só do DocFX (emenda de 2026-11-02). Para fontes soltas, o DocFX 2.81 sempre junta as referências do SDK do .NET (net10.0), sem opção para desligar. Se a configuração também passa o `mscorlib`, o `netstandard` e os `System.*` da Unity, o mesmo tipo aparece duas vezes (CS0433) e o build falha. O `Read` pergunta à Unity quais pastas são da base, com `CompilationPipeline.GetSystemAssemblyDirectories` para o `compilerOptions.ApiCompatibilityLevel` de cada assembly do pacote, e o `Render` tira toda referência que está dentro de uma dessas pastas. O filtro é por pasta, e não por nome, porque a Unity já diz quais pastas são da base e uma lista de nomes teria de acompanhar cada versão do .NET. A Fábrica confere a API na Unity 6000.3 e que as pastas devolvidas cobrem `Editor/Data/NetStandard/` e `Editor/Data/UnityReferenceAssemblies/`. Se não cobrirem, ela para e devolve;
- **Emenda de 2026-11-02 (segunda), aprovada pelo usuário em 2026-11-02.** A primeira versão do `ReadSystemAssemblyDirectories` não cobre os assemblies de Editor. O `compilerOptions.ApiCompatibilityLevel` devolve o nível do player para todos os assemblies (no projeto, `apiCompatibilityLevel: 6`, .NET Standard 2.1), mas a Unity compila os de Editor com o nível de Editor (`editorAssembliesCompatibilityLevel: 1`, o padrão, que é o .NET Framework 4.8). Por isso as pastas devolvidas são só as de `NetStandard/`, e o `mscorlib` e os `System.*` de `UnityReferenceAssemblies/unity-4.8-api/` e `unity-4.8-api/Facades/` continuam na configuração (20 erros no `docfx metadata`: 7 CS0433 e 13 CS0518). A correção muda só a leitura: o `ReadSystemAssemblyDirectories` devolve a união das pastas de `CompilationPipeline.GetSystemAssemblyDirectories` para todos os níveis de `ApiCompatibilityLevel` sem `[Obsolete]`, lidos dos campos do enum. A pergunta deixa de ser "qual nível este assembly usa", que a Unity não expõe para o Editor, e passa a ser "quais pastas são da base do .NET da Unity", que é o que o filtro precisa. Assim não há mapa do nível de Editor para manter nem dependência da configuração do projeto. O `Render` e a assinatura dele não mudam. Essas pastas só têm a base do .NET (`mscorlib`, `netstandard`, `System.*` e as fachadas), que o net10.0 do DocFX cobre, e o net10.0 traz as fachadas `mscorlib.dll` e `netstandard.dll`, então o `UnityEngine.dll`, o `UnityEditor.dll` e o `nunit.framework.dll` continuam resolvendo os tipos da base. Se algum código de Editor do pacote usar uma API que só existe no .NET Framework 4.8, o `docfx metadata` falha com erro de tipo ausente e a Fábrica para e devolve. Hoje o pacote não usa nenhuma (conferido: `System.Data` e `AppDomain` existem no net10.0). A Fábrica confere na 6000.3 que a chamada não lança exceção para nenhum dos níveis lidos e registra no relatório as pastas devolvidas;
- **Emenda de 2026-11-02 (terceira), aprovada pelo usuário em 2026-11-02.** Substitui, na segunda emenda, só a leitura por "todos os níveis sem `[Obsolete]`". Na 6000.3.13f1, o `NET_2_0_Subset`, o `NET_Web` e o `NET_Micro` não têm `[Obsolete]`, e o `GetSystemAssemblyDirectories` escreve "Unknown API compatibility level" como erro no log para cada um, o que derruba o teste. A segunda emenda partiu de que a Unity não diz o nível dos assemblies de Editor, e isso estava errado: a 6000.3.13f1 tem `ScriptCompilerOptions.EditorAssembliesCompatibilityLevel` (conferido no `UnityEditor.CoreModule.xml` e no `UnityEditor.CoreModule.dll` instalados), com os valores `Default`, `NET_Unity_4_8` e `NET_Standard` do enum `EditorAssembliesCompatibilityLevel`, e a documentação diz que o `Default` equivale ao `NET_Unity_4_8`. O `ReadSystemAssemblyDirectories()` volta a ler os assemblies do pacote em `CompilationPipeline.GetAssemblies(AssembliesType.Editor)` (filtro `TypeCatalog.IsPackageAssembly`) e, para cada um, junta dois níveis do `compilerOptions`: o `ApiCompatibilityLevel` e o `EditorAssembliesCompatibilityLevel` convertido para `ApiCompatibilityLevel` (`Default` e `NET_Unity_4_8` viram `NET_Unity_4_8`; `NET_Standard` vira `NET_Standard`; qualquer outro valor lança `ArgumentOutOfRangeException` com o valor na mensagem, para que um nível novo numa versão futura da Unity apareça como erro em vez de sumir do filtro). Só esses níveis, sem repetição, vão para o `GetSystemAssemblyDirectories`. Os dois níveis entram para todo assembly, sem escolher pelo `AssemblyFlags.EditorAssembly`, porque a Spec não sabe qual deles a Unity usa para um assembly de runtime compilado no Editor, e uma pasta da base a mais só tira referência que o net10.0 do DocFX já cobre. A conversão tem três casos e fica dentro do `DocfxConfig`, privada. A lista fixa de níveis (alternativa a da Fábrica) repetiria o que a Unity já informa e incluiria nível que o projeto não usa. O `PlayerSettings.GetEditorAssembliesCompatibilityLevel()`, que também existe na 6000.3.13f1, daria um valor global, fora do `CompilationPipeline` de onde o `DocfxConfig` lê todo o resto. O log não é filtrado nem escondido: o teste `ReadSystemAssemblyDirectories_CoversCoreLibraryOfEveryPackageAssembly` roda sem `LogAssert.ignoreFailingMessages` e continua sendo o teste red, que hoje cai pelo erro no log. O `Render`, a assinatura dele e o filtro por pasta não mudam. Se o `compilerOptions.EditorAssembliesCompatibilityLevel` vier diferente do que o `PlayerSettings.GetEditorAssembliesCompatibilityLevel()` devolve no projeto, a Fábrica para e devolve, sem trocar pelo `PlayerSettings` por conta própria. Ela registra no relatório os níveis lidos, as pastas devolvidas e o resultado do `docfx metadata --warningsAsErrors`;
- o destino `../reference`, ou seja, `api/reference/`;
- o ajuste que tira o nome da branch e os caminhos absolutos dos YAML. A Fábrica confere na documentação da versão travada qual chave ou variável de ambiente faz isso (por exemplo, `DOCFX_SOURCE_BRANCH_NAME=main` no processo do runner, ou desligar os recursos de git) e registra no relatório. O teste de guarda cobra o resultado (RC4).

Mais regras:
- `DocfxConfig.Render(IEnumerable<DocfxAssembly>, IReadOnlyCollection<string> systemAssemblyDirectories, string configDir)` recebe os dados já lidos, para o teste não depender do `CompilationPipeline`. `DocfxAssembly` tem nome, fontes e referências. O `DocfxConfig.ReadSystemAssemblyDirectories()` lê as pastas da base (emenda de 2026-11-02; antes o `Render` não tinha o segundo parâmetro).
- Os tipos `System.*` não mudam na referência com a base do net10.0 no lugar da base da Unity. Na seção `references` de cada YAML, o DocFX escreve o `uid` (namespace e nome), o nome e o `isExternal`, sem o assembly. O `docfx metadata` 2.81 também escreve o `href` para learn.microsoft.com (`GetMicrosoftLearnUrl`, fixo no código, sem download e sem opção para desligar), e o `docfx build` mostra esses tipos como link (emenda de 2026-11-02 do X2). Nenhum mapa do .NET entra no `docfx.json`.
- A mesma entrada gera o mesmo texto: as listas saem em ordem ordinal.
- A API cobre todos os assemblies do pacote, de runtime e de Editor, porque o Editor tem pontos de extensão públicos (`SpellBookEditor`, `TypePickerCatalog`). Código dentro de `#if` aparece ou não conforme os símbolos que o DocFX aceita para fontes soltas. A Fábrica confere isso na versão travada e registra no relatório.

O `api/docfx.json` commitado é fixo e só tem a seção `build`: os YAML de `reference/`, `index.md` e `toc.yml` de `Documentation~/api/`, e o título "SpellBook: referência da API".

Emenda de 2026-11-02 (caminho das páginas da API, ver D3; aprovada em 2026-11-02). O RC3 listou os YAML como `reference/**.yml`, e o `docfx build` grava as páginas em `_site/reference/<uid>.html`. O `content` passa a ter duas entradas:

```json
"content": [
  { "src": "reference", "files": [ "**.yml" ], "exclude": [ "toc.yml" ] },
  { "files": [ "reference/toc.yml", "index.md", "toc.yml" ] }
]
```

- A primeira entrada usa `reference/` como origem, então a página de cada tipo e de cada namespace sai em `_site/<uid>.html`, e no site em `api/<uid>.html`, o caminho do link do catálogo.
- O `reference/toc.yml` (o índice de namespaces que o `docfx metadata` escreve) fica na segunda entrada, com o caminho de hoje. Se ele fosse junto na primeira, sairia em `_site/toc.yml`, no mesmo caminho do `api/toc.yml` escrito à mão. Assim o `api/toc.yml` continua apontando a entrada "API" para `reference/`. Os itens do índice são por `uid`, então os links dele acham as páginas no caminho novo.
- O `docfx metadata`, o `DocfxConfig`, o `ReferenceGenerator`, o `api/toc.yml`, o catálogo e a saída gerada não mudam. O `docfx.json` não entra na impressão digital (D2), então a guarda `CommittedFingerprint_MatchesSources` não muda.
- A Fábrica confere na documentação do DocFX travado os nomes `src` e `exclude` na entrada de `content`. Se o `docfx build --warningsAsErrors` avisar de saída repetida ou de `toc` sem destino, ou se a página de um tipo não sair em `_site/<uid>.html`, ela para e devolve ao Arquiteto. Não serve mudar o link do catálogo nem o destino do `docfx metadata` por conta própria.
- O XR2 depois acrescenta a chave `xref` na mesma seção `build`, sem mexer no `content`.

Antes de escrever, a Fábrica confere na documentação da versão travada do DocFX os nomes das chaves (`metadata`, `src`, `references`, `dest`, `build`) e se o `docfx metadata` e o `docfx build` aceitam o `--warningsAsErrors`.

O build estrito fica assim:
- `mkdocs build --strict` como no MN1; o catálogo entra no `nav` e passa pela validação de links e âncoras;
- o `docfx metadata` com aviso como erro, no `Generate`, e o `docfx build --warningsAsErrors`, na Action. Um `<see cref>` quebrado num comentário XML derruba uma das duas etapas, e a Fábrica registra qual no RC4;
- um passo da Action lista os links `api/...html` do catálogo e confere que cada arquivo existe no site gerado, porque o MkDocs não valida link absoluto;
- tipo público sem comentário XML não falha o build (C4).

A navegação ganha, no grupo Referência, nesta ordem: `referencia/catalogo.md`, a entrada `API: https://gabrielctorres.github.io/SpellBook/api/`, `inicio/conceitos.md` e `regras/migracao-2-para-3.md`. O catálogo entra no RC4, com o gerador, e a API entra no RC5, quando a Action passa a publicá-la.

### D5. A Action de documentação (G1, G3)

O `docs.yml` continua com dois jobs e sem Unity (0040, item 7):

| Job | Onde roda | O que faz |
|---|---|---|
| `build` | `ubuntu-latest` | `mkdocs build --strict`; instala o SDK do .NET (versão travada) e o DocFX (`dotnet tool restore`); `docfx build api/docfx.json --warningsAsErrors` com saída em `site/api/`; a conferência dos links do catálogo; envia o artefato do Pages |
| `deploy` | `ubuntu-latest` | publica, só no push para a `main` (`if: github.ref == 'refs/heads/main' && github.event_name == 'push'`) |

- Gatilhos: push para a `main`, `pull_request` e `workflow_dispatch`, nos caminhos `Documentation~/manual/**`, `Documentation~/mkdocs.yml`, `Documentation~/requirements-docs.txt`, `Documentation~/api/**`, `Documentation~/.config/**` e o próprio workflow. O código do pacote não entra nos gatilhos: quando ele muda, a geração commitada muda `Documentation~/api/`.
- Em pull request, o `build` roda inteiro e o `deploy` não roda (G3). Como não há secret, PR de fork também monta.
- Toda action nova é travada por SHA, como as de hoje. O `build` pede só `contents: read`; o `deploy` mantém as permissões da 0025.

### D6. Build local e o dia a dia dos tickets

O guia do manual (MN2) ganha uma seção com dois fluxos.

Quem muda um `.cs` do pacote:
1. roda **SpellBook > Documentação > Gerar referência** na Unity, com o SDK do .NET instalado;
2. commita o catálogo, os YAML de `api/reference/` e o `api/reference.sha256` no mesmo diff. Sem isso, o teste de guarda falha.

Quem quer ver o site, sem a Unity, dentro de `Documentation~/`:
1. `mkdocs build --strict -f mkdocs.yml` no venv do manual;
2. `dotnet tool restore` e `dotnet docfx build api/docfx.json --warningsAsErrors`.

**Emenda de 2026-11-03 (aprovada pelo usuário em 2026-11-03):** o primeiro fluxo de D6 deixa de valer por ticket. A geração roda no passe de documentação (emendas de 2026-11-03 da 0007 e da 0040), e as guardas ficam na categoria `Documentacao` (ticket PD1 da `processo-doc-no-fim.md`). Pelo mesmo motivo, deixa de valer a frase do resumo dos tickets que diz que dois tickets com Unity que mudam `.cs` "sempre têm arquivo em comum". Os tickets desta Spec ainda fora da fila têm o detalhamento como rascunho, refeito na entrada (emenda de 2026-11-03 da 0002).

### D7. Resumo obrigatório (G2)

Um teste de guarda exige `[TypeCategory]` com `Summary` não vazio em todo tipo do catálogo. A regra entra na 0007 como emenda: a entrada no catálogo só vale como documentação de um tipo novo (item 2 da Decisão) se o tipo tem resumo.

A guarda entra com uma lista de pendências por prefixo de namespace, que só diminui:
- o RC6 cria a guarda com `SpellBook.GAS` pendente e põe o resumo nos tipos de Eventos e do `GAS.Bridges`;
- o RC7 tira `SpellBook.GAS.Abilities` da lista, com o resumo nas ações e nos elementos de descrição;
- o RC8 cobre o resto do GAS e apaga a lista.

Ticket aprovado que cria um tipo de catálogo depois do RC6, fora da lista, põe o resumo no mesmo diff. É uma linha no atributo, e o teste de guarda a cobra. Cada um desses tickets regenera o catálogo e a API (D6).

## Quebras de compatibilidade

Nenhuma no pacote: o `SpellBook.Core.Editor` ganha tipos públicos e um item de menu, e nenhuma API sai ou muda. No site, o grupo Referência ganha o catálogo e a API. No fluxo do time, todo ticket que muda código do pacote passa a regenerar a referência (D6). O RC5 registra o catálogo e a API no CHANGELOG, em "Documentação" (CA5).

## Tamanho de tickets com saída gerada

Vale a emenda de 2026-11-01 da 0003, "saída de gerador commitada" (G4). Nesta Spec, a saída gerada é `manual/referencia/catalogo.md`, `api/reference/**` e `api/reference.sha256`, coberta pelas guardas do RC4. Ela não conta em linhas nem em arquivos, e o Examinador revisa o gerador, não os YAML.

## Tickets

### Resumo

| Ticket | Issue | Título da issue | Unity? | Depende de | Par com Unity (WIP 2) | Tamanho |
|---|---|---|---|---|---|---|
| RC1 | #286 | `RC1: feat(core-editor): catálogo de tipos lido do seletor do Inspector` | sim | gate | nenhum | 2 arquivos; ~280 linhas |
| RC2 | #287 | `RC2: feat(core-editor): página do catálogo em Markdown determinístico` | sim | RC1 | RC3 | 2 arquivos; ~300 linhas |
| RC3 | #288 | `RC3: feat(core-editor): configuração de metadados do DocFX a partir da compilação da Unity` | sim | RC1 | RC2 | 5 arquivos; ~350 linhas |
| RC4 | #289 | `RC4: feat(core-editor): gerar e commitar a referência, com teste de guarda` | sim | RC2, RC3, MN1, MN2 | nenhum | 12 arquivos; ~610 linhas, fora a saída gerada (emendas de 2026-11-02) |
| RC5 | #290 | `RC5: ci(docs): montar a API e conferir os links na Action, também em PR` | não | RC4 | não se aplica (sem Unity) | 5 arquivos; ~130 linhas (emenda de 2026-11-02) |
| RC6 | #291 | `RC6: test(core-editor): resumo obrigatório no catálogo, com Eventos e pontes do GAS` | sim | RC4 | nenhum | 3 a 5 arquivos; ~80 linhas |
| RC7 | #292 | `RC7: feat(gas): resumo no catálogo das ações e dos elementos de descrição` | sim | RC6 | nenhum | ~9 arquivos; ~60 linhas |
| RC8 | #293 | `RC8: feat(gas): resumo no catálogo dos efeitos e condições, guarda completa` | sim | RC7 | nenhum | até 10 arquivos; ~60 linhas |

- Os tickets com Unity entram como um lote próprio logo depois do Lote 6, na ordem RC1 → RC2 → RC3 → RC4 → RC6 → RC7 → RC8.
- Pares com Unity (emenda de 2026-11-01 da 0003, experimento de WIP 2): só o RC2 e o RC3. Os dois dependem só do RC1, e os escopos não têm arquivo em comum, nem `.asmdef`, nem `CHANGELOG.md`, nem saída gerada. A partir do RC4, todo ticket com Unity muda `.cs` do pacote e regenera `api/reference/**` e `api/reference.sha256` (D6), então dois deles sempre têm arquivo em comum e não formam par. Os demais seguem o WIP 1.
- O RC5 não usa a Unity e pode rodar em paralelo com o RC6 em diante, depois do RC4.
- Os RC6 a RC8 dependem do RC4, e não só do RC1, porque mudam `.cs` do pacote e precisam regenerar a referência.
- O RC8 é contado de novo quando entrar na fila, porque os Lotes 5 e 6 criam tipos novos no GAS (condições, processadores). Se passar de 10 arquivos, a Fábrica para e devolve, e o Arquiteto divide o ticket por namespace.

### RC1 — `feat(core-editor): catálogo de tipos lido do seletor do Inspector`

- Escopo: `Editor/Core/Documentation/TypeCatalog.cs` (novo, com `TypeCatalogSection`), `Tests/Core/EditMode/Documentation/TypeCatalogTests.cs` (novo, com os tipos de teste).
- Testes red:
  - `Build_ListField_BaseIsElementType`: um tipo de teste com `[SerializeReference, TypePicker] List<ICatalogFixture>` gera a seção de `ICatalogFixture`;
  - `Build_SingleField_BaseIsFieldType`;
  - `Build_FieldsUseInspectorNames`: o campo aparece como `Holder > Items`;
  - `Build_EntriesEqualSelectorCandidates` (C3): para toda seção do pacote, as entradas são `TypePickerCatalog.GetCandidates(base)` restritas ao pacote, na mesma ordem;
  - `Build_HasPackageBases`: as seções incluem `SpellBook.Events.Reaction`, `SpellBook.Events.EventResponse` e a base das ações da habilidade, comparadas pelo nome completo, sem referência nova de asmdef;
  - `Build_BaseWithoutPackageEntries_HasNoSection`;
  - `IsPackageAssembly_ExcludesTestsTestingAndSamples`: `SpellBook.GAS` sim; `SpellBook.GAS.Tests.EditMode`, `SpellBook.Testing`, `SpellBook.Samples.Sandbox` e `Assembly-CSharp` não;
  - `Build_TwiceSameOrder`.
- Critério de pronto: os testes passam no EditMode; o `TypePickerCatalog` não muda; nenhuma referência nova de asmdef.
- Tamanho estimado: ~280 linhas (~110 de código, ~170 de teste); 2 arquivos.

### RC2 — `feat(core-editor): página do catálogo em Markdown determinístico`

- Escopo: `Editor/Core/Documentation/TypeCatalogPage.cs` (novo), `Tests/Core/EditMode/Documentation/TypeCatalogPageTests.cs` (novo). Os testes montam `TypeCatalogSection` à mão.
- Testes red:
  - `Render_StartsWithGeneratedNotice` (R6), com o nome do menu de geração;
  - `Render_SameSectionsTwice_SameText` (N2, CA2);
  - `Render_UsesLfAndEndsWithNewline`;
  - `Render_SectionAnchorFromBaseFullName`, com um tipo aninhado (`+`);
  - `Render_ApiLinkFromDocUrlsBase`: o link é `DocUrls.Base + "api/SpellBook.X.Y.html"`, com `+` trocado por `.`;
  - `Render_EmptyCategory_ShowsRaiz`, `Render_EmptySummary_LeavesCellEmpty`;
  - `Render_EscapesPipeAndNewline`;
  - `Render_ListsFieldsUnderSection`.
- Critério de pronto: os testes passam; o texto fixo da página segue o guia do manual (sem sigla, segunda pessoa) e as regras da humanizer, e o relatório da Fábrica declara isso.
- Tamanho estimado: ~300 linhas (~130 de código, ~170 de teste); 2 arquivos.

### RC3 — `feat(core-editor): configuração de metadados do DocFX a partir da compilação da Unity`

- Escopo:
  - `Editor/Core/Documentation/DocfxConfig.cs` (novo, com `DocfxAssembly`);
  - `Tests/Core/EditMode/Documentation/DocfxConfigTests.cs` (novo);
  - `Documentation~/api/docfx.json` (novo, fixo, só a seção `build`);
  - `Documentation~/api/index.md` (novo, página inicial da API, com link de volta ao manual);
  - `Documentation~/api/toc.yml` (novo).
- Testes red:
  - `Render_SourcesOnlyFromPackageAssemblies`: um assembly de teste na entrada não aparece;
  - `Render_SourcePathsRelativeToConfigDirWithForwardSlashes`;
  - `Render_ReferencesExcludePackageOutputs`: a DLL de um assembly do pacote não entra como referência;
  - `Render_DestinationIsApiReference`;
  - `Render_SameInputTwice_SameText`;
  - `Read_FromCompilationPipeline_FindsCoreSources`: a leitura real acha `Runtime/Core/DocUrls.cs` no `SpellBook.Core`.
- Critério de pronto:
  - os testes passam;
  - a Fábrica confere as chaves e o ajuste de branch e caminhos (D4) na versão do DocFX que vai travar no RC4 e os registra no relatório;
  - `index.md` segue as regras da humanizer.
- Tamanho estimado: ~350 linhas (~170 de código, ~150 de teste, ~30 de JSON e YAML); 5 arquivos.

### RC4 — `feat(core-editor): gerar e commitar a referência, com teste de guarda`

Este é o ticket que liga o gerador ao site. Pelo item 5 da emenda de 2026-11-01 da 0007, ele põe no mesmo diff o comando que escreve a página, a entrada no `nav` e o comando do build local. A "geração antes do build, na Action" daquele item vira geração antes do commit, cobrada pelo teste de guarda (0040, Consequências).

- Escopo:
  - `Editor/Core/Documentation/ReferenceGenerator.cs` (novo, D2, com `IDocfxMetadataRunner` e o runner real);
  - `Editor/Core/Documentation/ReferenceFingerprint.cs` (novo);
  - `Tests/Core/EditMode/Documentation/ReferenceGeneratorTests.cs` (novo; runner falso e pasta temporária apagada no `TearDown`);
  - `Tests/Core/EditMode/Documentation/ReferenceUpToDateTests.cs` (novo, o teste de guarda);
  - `Editor/Core/Documentation/DocfxConfig.cs`: o `ReadSystemAssemblyDirectories` e o filtro da base do .NET no `Render` (emenda de 2026-11-02 do D4);
  - `Tests/Core/EditMode/Documentation/DocfxConfigTests.cs`: o teste red novo e o parâmetro novo nas chamadas que já existem;
  - `.gitignore`: `Documentation~/api/obj/`, `Documentation~/api/_site/`;
  - `Documentation~/.config/dotnet-tools.json` (novo; DocFX com versão travada);
  - `Documentation~/mkdocs.yml`: a entrada do catálogo no grupo Referência (D4);
  - `Documentation~/guia-do-manual.md`: a seção do D6;
  - `Editor/Core/Documentation/TypeCatalogPage.cs`: título e âncora de base genérica fechada (emenda de 2026-11-02 do D3);
  - `Tests/Core/EditMode/Documentation/TypeCatalogPageTests.cs`: os testes red da emenda, com um tipo de teste genérico `PageGenericBase<T>` ao lado do `PageBase`;
  - saída gerada, fora da contagem: `manual/referencia/catalogo.md` (regenerado, com a seção de `Condition<ConditionContext>` na regra nova), `api/reference/**`, `api/reference.sha256`.
- Testes red:
  - `Render_SkipsReferencesInSystemAssemblyDirectories`: com `C:/Unity/NetStandard/ref/2.1.0/netstandard.dll`, `C:/Unity/UnityReferenceAssemblies/unity-4.8-api/mscorlib.dll` e `C:/Unity/Managed/UnityEngine.dll` nas referências e as duas primeiras pastas como base, o texto tem o `UnityEngine.dll` e não tem o `netstandard.dll` nem o `mscorlib.dll`. Uma DLL numa pasta com o mesmo prefixo de nome (`C:/Unity/NetStandard2/x.dll`) fica;
  - `ReadSystemAssemblyDirectories_CoversCoreLibraryOfEveryPackageAssembly` (segunda emenda de 2026-11-02 do D4, aprovada em 2026-11-02): para cada assembly do pacote no `CompilationPipeline.GetAssemblies(AssembliesType.Editor)`, toda referência chamada `mscorlib.dll` ou `netstandard.dll` está dentro de uma das pastas devolvidas. Falha hoje com o `mscorlib.dll` de `unity-4.8-api/` dos assemblies de Editor;
  - `Render_ClosedGenericBase_TitleInCSharpForm` (emenda de 2026-11-02 do D3): uma seção com base `PageGenericBase<PageBase>` tem a linha `` ## `PageGenericBase<PageBase>` {#…} ``. Falha hoje com `` `PageGenericBase`1` ``;
  - `Render_ClosedGenericBase_AnchorWithoutAssembly`: a mesma seção tem a âncora `{#spellbook-core-tests-documentation-pagegenericbase-spellbook-core-tests-documentation-pagebase}`, sem `` ` ``, `[`, `,`, `=` nem `version`. Falha hoje com o nome qualificado pelo assembly;
  - `Generate_WritesCatalogConfigAndFingerprint`: numa pasta temporária, com o runner falso, `Generate` cria o catálogo, a configuração em `api/obj/` e a impressão digital, e chama o runner uma vez com o caminho da configuração;
  - `Generate_RunnerFails_NoFingerprint`: com o runner falso lançando exceção, a impressão digital não é escrita e a exceção sobe;
  - `Generate_Twice_SameBytes` (N2);
  - `Fingerprint_ChangesWhenSourceChanges` e `Fingerprint_IgnoresLineEndings`, sobre uma árvore de arquivos temporária;
  - guarda `CommittedCatalog_MatchesGenerated`: falha antes da primeira geração, porque o arquivo não existe;
  - guarda `CommittedFingerprint_MatchesSources`: idem;
  - guarda `CommittedReference_HasNoMachinePathOrForeignBranch`: nenhum YAML de `api/reference/` tem letra de unidade, `/Users/`, `\Users\` ou `branch:` diferente de `main`;
  - manual, no relatório: um `<see cref="NaoExiste"/>` de rascunho num arquivo do pacote faz a geração ou o `docfx build --warningsAsErrors` falhar (a Fábrica registra qual). O rascunho é apagado antes do diff;
  - manual, no relatório: depois de mudar uma linha de um `.cs` do pacote sem gerar, o `CommittedFingerprint_MatchesSources` falha com a instrução do menu.
- Critério de pronto:
  - os testes EditMode passam, inclusive o `ManualLinksTests` e as guardas;
  - o `GenerateFromCommandLine` em batchmode na máquina sai com 0 e deixa a árvore igual à do menu;
  - o `docfx metadata --warningsAsErrors` com a configuração gerada termina sem aviso e sem erro, e a configuração não tem nenhuma DLL das pastas da base;
  - o build local do D6, sem a Unity, passa nos dois passos;
  - o diff não tem nenhuma DLL nem nada de `api/obj/`.
- Tamanho estimado: ~510 linhas (~240 de código, ~240 de teste, ~30 de config e guia); 10 arquivos, fora a saída gerada. Com a emenda de 2026-11-02, o ticket fica no teto de arquivos e perto do teto de linhas. A segunda emenda não acrescenta arquivo: soma ~10 linhas no `ReadSystemAssemblyDirectories` e ~20 do teste novo, nos arquivos que já estavam no escopo. Fica ~2% acima do teto de linhas, dentro da tolerância de 30%. A terceira emenda (aprovada em 2026-11-02) troca a leitura dos campos do enum pela leitura do `compilerOptions` com a conversão de três casos: ~15 linhas a mais no `DocfxConfig.cs`, nenhum arquivo novo e nenhum teste novo, ~5% acima do teto de linhas. A revisão final contou ~553 linhas em 10 arquivos. A emenda de 2026-11-02 do D3 (base genérica fechada) soma ~20 linhas no `TypeCatalogPage.cs` e ~35 no `TypeCatalogPageTests.cs`, dois arquivos que já existem. O catálogo regenerado é saída gerada e não conta. Total: ~610 linhas em 12 arquivos, dentro da margem automática (650 linhas, 13 arquivos). A correção fica no RC4 porque é ele que põe o catálogo no `nav`.

### RC5 — `ci(docs): montar a API e conferir os links na Action, também em PR`

Não usa a Unity: só lê o que o RC4 commitou.

- Escopo:
  - `.github/workflows/docs.yml`: o job `build` do D5, com o `docfx build`, a conferência dos links e os gatilhos novos, inclusive `pull_request`; o `deploy` só no push para a `main`;
  - `Documentation~/mkdocs.yml`: a entrada da API no grupo Referência (D4);
  - `Documentation~/guia-do-manual.md`: a Action roda nos PRs e falha com link do catálogo para tipo sem página;
  - `Packages/com.spellbook/CHANGELOG.md`: catálogo e API em "Documentação" (CA5);
  - `Documentation~/api/docfx.json`: o `content` em duas entradas, para as páginas saírem em `api/<uid>.html` (emenda de 2026-11-02 do D4). O arquivo é do RC3 e já está commitado; o RC5 só troca o `content`.
- Testes red:
  - com o `api/docfx.json` de antes da emenda, a conferência de links falha em todo link do catálogo, porque as páginas saem em `api/reference/<uid>.html` (a Fábrica registra no relatório a saída da conferência antes e depois da troca);
  - um arquivo da API apagado do site local faz a conferência de links falhar;
  - o `docfx build --warningsAsErrors` roda a partir dos YAML commitados, numa máquina sem a Unity. Se pedir as DLLs da Unity, a Fábrica para e devolve (D4).
- Critério de pronto:
  - a Action no PR do lote roda o `build` sem erro e não roda o `deploy` (G3);
  - no site local, a página de `SpellBook.Events.DelayedReaction` está em `api/SpellBook.Events.DelayedReaction.html`, a entrada "API" do `api/toc.yml` abre o índice de namespaces e o `docfx build --warningsAsErrors` passa sem aviso;
  - o diff não muda `.cs`, o catálogo, `api/reference/**` nem `api/reference.sha256`;
  - nenhum passo usa a Unity nem secret;
  - toda action nova está travada por SHA;
  - depois do merge, o catálogo publicado lista as reações dos Eventos com categoria e resumo (CA1), e os links abrem a página do tipo na API (CA3).
- Tamanho estimado: ~130 linhas (~90 de YAML de CI, ~30 de guia, `nav` e CHANGELOG, ~10 do `docfx.json`); 5 arquivos. A emenda de 2026-11-02 soma um arquivo e ~10 linhas, e o ticket continua sem Unity e longe do teto.

### RC6 — `test(core-editor): resumo obrigatório no catálogo, com Eventos e pontes do GAS`

- Escopo: `Tests/Core/EditMode/Documentation/TypeCatalogSummaryTests.cs` (novo), `Runtime/Events/BuiltInResponses.cs`, `Runtime/GAS.Bridges/Channels/GasEventResponses.cs` e outros tipos de catálogo fora de `SpellBook.GAS` que existirem quando o ticket entrar (a Fábrica conta na hora; hoje não há outro). Mais a saída gerada, fora da contagem.
- Testes red: `EveryCatalogTypeHasSummary` falha hoje nas 12 respostas, que têm `[TypeCategory("Respostas")]` sem `Summary`. A lista de pendências tem só `SpellBook.GAS`, e o teste falha com a lista dos tipos sem resumo.
- Critério de pronto: o teste passa; as guardas do RC4 passam; cada resumo tem uma frase sobre o que o tipo faz no jogo, pelas regras da humanizer; a categoria de cada tipo continua a mesma.
- Tamanho estimado: ~80 linhas; 3 a 5 arquivos.

### RC7 — `feat(gas): resumo no catálogo das ações e dos elementos de descrição`

- Escopo: `[TypeCategory(caminho, Summary = ...)]` nos tipos de catálogo do namespace `SpellBook.GAS.Abilities`. Hoje são `Runtime/GAS/Abilities/AbilityDescription.cs` e, em `Runtime/GAS/Abilities/Actions/`, `AnimationAction.cs`, `FlowActions.cs`, `ApplyEffectAction.cs`, `PlayCueAction.cs`, `SpawnProjectileAction.cs`, `TargetingAction.cs` e `WaitAction.cs`. Inclui também `TypeCatalogSummaryTests.cs`, onde `SpellBook.GAS.Abilities` sai da lista de pendências, e a saída gerada, fora da contagem.
- Testes red: com o prefixo fora da lista, o `EveryCatalogTypeHasSummary` falha nos tipos desses arquivos.
- Critério de pronto: o teste e as guardas do RC4 passam; os caminhos de categoria seguem os que o PRD do B-012 e o DS `ds/inspector-de-acoes.md` usam para as ações. Se o DS não cobrir algum tipo, a Fábrica usa o caminho mais próximo e o lista no relatório.
- Tamanho estimado: ~60 linhas; ~9 arquivos (recontar na hora).

### RC8 — `feat(gas): resumo no catálogo dos efeitos e condições, guarda completa`

- Escopo: o resumo nos tipos de catálogo do resto de `SpellBook.GAS` (hoje, `Runtime/GAS/Effects/Actions/CommonEffectActions.cs`; até o Lote 6, também as condições e os processadores dos Lotes 5 e 6), `TypeCatalogSummaryTests.cs` sem a lista de pendências, e a saída gerada, fora da contagem.
- Testes red: sem a lista, o `EveryCatalogTypeHasSummary` falha nos tipos que faltam.
- Critério de pronto: o teste passa sem lista de pendências; as guardas do RC4 passam; nenhum tipo de catálogo do pacote fica sem resumo.
- Tamanho estimado: ~60 linhas; até 10 arquivos (recontar na hora; acima disso, devolver para dividir).

## Gate

G1 a G4 foram respondidas em 2026-11-01 (topo desta Spec). Os RC1 a RC8 estão aprovados, com as issues #286 a #293.

## Emenda de 2026-11-01: links para a Unity e exemplos tirados do código (E1, E2)

Estado: aprovada no gate de 2026-11-01, com X1 e X2 na alternativa e X3 a X5 como propostos (ver "Respostas do gate da emenda"). Vem da seção 10 do PRD (R9, R10, CA9, CA10) e da ADR 0041 (Aceita). Nada aqui muda os RC1 a RC8.

### Contexto do código para a emenda

- `Documentation~/mkdocs.yml:16-20`: as extensões são `attr_list`, `admonition`, `toc` e `fenced_code`. Não há inclusão de arquivo.
- `Documentation~/requirements-docs.txt:3-4`: `mkdocs==1.6.1` e `mkdocs-material==9.8.0`. O `pymdown-extensions`, que traz o `pymdownx.snippets`, já vem instalado como dependência do `mkdocs-material`, sem versão travada por nós.
- O manual tem 41 blocos ` ```csharp ` escritos à mão, em 8 páginas: `inicio/inspector.md` (9), `modulos/gas.md` (7), `modulos/pooling.md` (6), `modulos/eventos.md` (5), `modulos/tags.md` (5), `modulos/tempo.md` (5), `ferramentas/validador.md` (2) e `inicio/conceitos.md` (2). `exemplos/sandbox.md` não tem bloco C#.
- O código do Sandbox fica em `Samples~/Sandbox/Scripts/` (6 arquivos, `SpellBook.Samples.Sandbox.asmdef`). Pela 0028, ele não compila na suíte normal: a compilação só é conferida com a pasta renomeada e no projeto vazio.
- Os testes de cada módulo compilam e rodam sempre (`Tests/<Módulo>/EditMode/*.asmdef`, listados em `testables` no `Packages/manifest.json:10`).
- Os assemblies do pacote hoje só referenciam módulos do motor (`UnityEngine.*`, `UnityEditor.*`). Nenhum usa pacote `com.unity.*`, mas a 0039 já declara a uGUI para a UI.

### D8. Mapa de referências cruzadas para a Unity (E1)

O DocFX resolve um tipo externo pelo `uid` num mapa de referências cruzadas (`xrefmap`), declarado na chave `xref` da seção `build`. Sem mapa e sem `href` no YAML, o tipo aparece como texto. Os tipos da Unity chegam sem `href`; os do .NET já chegam com ele (D4). O SpellBook gera o próprio mapa, na mesma geração do D2 (0041, item 1). Pesei o mapa de terceiros abaixo.

```csharp
namespace SpellBook.Core.Editor.Documentation
{
    public static class UnityXrefMap
    {
        public static string Render(string referenceDirectory); // texto do mapa, determinístico
        internal static IReadOnlyList<ExternalReference> ReadExternalReferences(string referenceDirectory);
        internal static string ResolveHref(ExternalReference reference); // null se não for da Unity
    }

    internal readonly struct ExternalReference
    {
        public string Uid { get; }
        public string CommentId { get; } // "T:", "M:", "P:", "F:" ou "E:" mais o nome
        public string Name { get; }
    }
}
```

- Entrada: as referências externas (`isExternal: true`) da seção `references` dos YAML que o `docfx metadata` escreveu em `api/reference/`. A leitura é por linha, só das chaves `uid`, `commentId`, `name` e `isExternal`. O mapa cobre o que a API usa, sem copiar a lista inteira da Unity.
- Cada referência é resolvida por reflexão: o tipo sai do `commentId`, procurado nos assemblies carregados no Editor. O assembly decide o destino:
  - módulo do motor (`UnityEngine.*` ou `UnityEditor.*`, sem pacote pelo `UnityEditor.PackageManager.PackageInfo.FindForAssembly`): `https://docs.unity3d.com/ScriptReference/<página>.html`, a documentação mais recente, sem versão na URL (X1). A página é o nome completo sem o prefixo `UnityEngine.` ou `UnityEditor.`, com `+` trocado por `.`. Para membro: método vira `<Tipo>.<Método>`, e propriedade, campo e evento viram `<Tipo>-<membro>`. Construtor vira `<Tipo>-ctor`, e sobrecargas dividem a página;
  - pacote `com.unity.*`: `https://docs.unity3d.com/Packages/<pacote>@latest/api/<uid do tipo>.html`, a versão mais recente do pacote (X1). Membro aponta para a página do tipo. A Fábrica confere numa página real que o `@latest` resolve; se não resolver, para e devolve ao Arquiteto. Substituído pela emenda de 2026-11-02 logo abaixo;
  - **Emenda de 2026-11-02 (versão do pacote na URL), aprovada em 2026-11-02 (X6).** A Fábrica devolveu o XR1: `@latest/api/<uid>.html` dá 404. Conferimos em 2026-11-02, com `curl -L`, que abaixo de `@latest/` só existe a raiz, uma página que redireciona por JavaScript para a versão mais recente (`com.unity.test-framework@latest/index.html` leva à 1.9, a `com.unity.ugui` à 2.7). `test-framework@latest/api/UnityEngine.TestTools.UnityTestAttribute.html`, `ugui@latest/api/UnityEngine.UI.Button.html` e `inputsystem@latest/api/UnityEngine.InputSystem.InputAction.html` dão 404. As mesmas páginas com a versão `maior.menor` dão 200 (`test-framework@1.4`, `@1.5` e `@1.6`, `ugui@2.0`, `inputsystem@1.14`). A regra de pacote passa a ser esta:
    - o destino é `https://docs.unity3d.com/Packages/<pacote>@<maior>.<menor>/api/<uid do tipo>.html`. A versão vem do `UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly).version`, a mesma chamada que já decide se o assembly é de pacote. Dela saem os dois primeiros números; o sufixo de pré-lançamento (`-pre.1`) e o patch ficam fora, porque a Unity publica a documentação por `maior.menor`. Uma versão que não começa com dois números faz a geração falhar com o `uid` e a versão na mensagem;
    - o link leva à documentação da versão do pacote instalada no projeto, a mesma contra a qual o `docfx metadata` compilou a API. A Spec não lê o `manifest.json` nem o `packages-lock.json`: o `PackageInfo` já devolve a versão resolvida, e uma segunda leitura seria outra fonte para manter igual a ele;
    - pacote cujo nome começa por `com.unity.modules.` conta como módulo do motor e vai para o ScriptReference. Esses pacotes só ligam os módulos internos (`com.unity.modules.physics` e outros estão no `manifest.json`) e não têm página de API própria. A regra vale qualquer que seja a resposta do `FindForAssembly` para a DLL de um módulo;
    - membro continua apontando para a página do tipo.
  - **Emenda de 2026-11-02 (ScriptReference por redirecionamento), aprovada em 2026-11-02 (X6).** Na mesma conferência, `https://docs.unity3d.com/ScriptReference/GameObject.html` respondeu 302 para `https://docs.unity.com/en-us/engine/current/script-reference/unityengine/gameobject` e terminou em 200, na página da 6000.6. O formato do ScriptReference continua o do D8: ele é o endereço que a Unity mantém para a versão mais recente, e o site novo não tem regra de URL documentada. A Fábrica confere no XR1, por amostra, que tipo, método, propriedade, construtor, tipo aninhado, genérico e tipo de Editor terminam em 200 depois dos redirecionamentos e registra a cadeia no relatório. Um caso que termina em 404 aponta para a página do tipo, pela regra do último item do D8.
  - qualquer outro assembly (`System.*`, `mscorlib`, `netstandard`): não entra no mapa. O link desses tipos vem do `href` para learn.microsoft.com que o `docfx metadata` já grava no YAML (D4, X2). Nenhum mapa do .NET é declarado no `docfx.json`.
- O mapa não depende da versão da Unity: sai só dos YAML. Emenda de 2026-11-02, aprovada em 2026-11-02 (X6): as entradas de pacote `com.unity.*` dependem também da versão do pacote instalada no projeto. Quem atualiza um pacote `com.unity.*` usado pela API regenera a referência, e a guarda `CommittedXrefMap_MatchesCommittedReference` (XR2), que gera o mapa no Editor com os pacotes do projeto, falha até isso acontecer. A impressão digital (D2) não muda. Quando a Unity muda ou remove um tipo, o link pode levar a uma página que mudou ou que não existe mais, e ninguém confere isso de forma automática (0041, Consequências).
- Referência da Unity que não resolve (tipo não achado) faz a geração falhar com a lista dos `uid`. Nenhuma entrada é descartada em silêncio.
- Saída: `Documentation~/api/unity.xrefmap.yml`, com o cabeçalho `### YamlMime:XRefMap`, `sorted: true` e as entradas `uid`, `name` e `href` em ordem ordinal de `uid`. Só `\n`, sem data. É saída gerada, pela emenda da 0003.
- O `Generate` (D2) ganha a etapa 3b: depois do `docfx metadata` e antes da impressão digital, escreve o mapa. A impressão digital não muda, porque o mapa sai dos YAML, que ela já cobre.
- O `api/docfx.json` ganha `"xref": ["unity.xrefmap.yml"]` na seção `build`, e só isso. O `docfx build` não baixa mapa nenhum da rede.
- Na quarta rodada do RC4, o `docfx build --warningsAsErrors` da versão travada passou sem aviso, com os `href` do .NET nos YAML. Se ele passar a avisar de tipo do .NET, a Fábrica para e devolve ao Arquiteto. Desligar o aviso como erro não é saída (0040).
- A regra de URL do ScriptReference é irregular (genéricos, tipos aninhados, namespaces como `UIElements`). A Fábrica confere a regra em páginas reais de cada caso e registra no relatório. Se um caso não tiver regra estável, ele aponta para a página do tipo.

Validação (CA9). O `--strict` do MkDocs não lê a API, e o DocFX pode não avisar de tipo externo sem mapa. A Fábrica confere isso na versão travada. A validação fica em duas guardas EditMode, no `ReferenceUpToDateTests`:
- `CommittedXrefMap_MatchesCommittedReference`: o mapa gerado a partir dos YAML commitados é igual, byte a byte, ao commitado. Roda sem o DocFX e sem rede;
- `EveryUnityReference_HasXrefEntry`: toda referência externa de assembly da Unity tem entrada no mapa.

A Action não testa os links externos, porque um teste que depende da rede é instável. Os links reais são conferidos à mão no ticket, por amostra.

Mapa de terceiros. O projeto UnityXrefMaps (NormandErwan, no GitHub) publica mapas da Unity por versão para o DocFX. Até onde sei, ele usa licença MIT e gera os mapas a partir do repositório UnityCsReference. Não consegui conferir daqui se ele cobre a 6000.3, se continua mantido e qual a licença dos dados. Mesmo que tudo isso se confirme, ele tem três limites para nós:
- a Action passaria a baixar um arquivo de um site de terceiro, sem versão travada;
- ele não cobre os pacotes `com.unity.*`;
- copiar o mapa para o repositório levaria junto dados derivados do UnityCsReference, que tem licença só de referência.

Gerar o mapa custa um ticket de código. Em troca, não depende de terceiros e passa pelo teste de guarda. O gate aprovou esse caminho (X3).

### D9. Exemplos de código incluídos de arquivos reais (E2)

A página inclui o trecho a partir do arquivo-fonte, com o `pymdownx.snippets` (0041, item 2):

````markdown
```csharp
--8<-- "Packages/com.spellbook/Tests/GAS/EditMode/Examples/GasManualExamples.cs:ativar-habilidade"
```
````

```csharp
[Test]
public void AtivarHabilidade()
{
    // --8<-- [start:ativar-habilidade]
    var asc = ...;
    asc.TryActivateAbility(ability);
    // --8<-- [end:ativar-habilidade]
    Assert.That(...);
}
```

O código acima só ilustra o formato. O teste real é escrito pela Fábrica, com a API do dia.

- Fonte dos exemplos (pergunta X4): `Tests/<Módulo>/EditMode/Examples/<Módulo>ManualExamples.cs`, no asmdef de testes que o módulo já tem, sem asmdef novo. Cada exemplo fica dentro de um `[Test]` que roda, então o trecho compila com o projeto e também é executado. Os marcadores `// --8<-- [start:nome]` e `// --8<-- [end:nome]` saem da página. A página `exemplos/sandbox.md` pode incluir de `Samples~/Sandbox/Scripts/`, que só compila na conferência da 0028.
- Configuração no `mkdocs.yml`: `pymdownx.snippets` com `base_path: [".."]` (a raiz do repositório, já que o build roda dentro de `Documentation~/`, pelo D6), `check_paths: true` e `restrict_base_path: true`. O Action e o guia rodam o MkDocs da mesma pasta. A Fábrica confere na versão travada se seção inexistente também derruba o build com o `check_paths`. Se não derrubar, a guarda EditMode cobre.
- O `requirements-docs.txt` ganha `pymdown-extensions==<versão>`, a que o `mkdocs-material==9.8.0` instala hoje. O pacote já está instalado, e a trava só deixa a versão explícita.
- Configuração de asset e Inspector fica fora do E2 e continua em texto e imagem. O CA10 fala só de bloco C#.
- Guarda EditMode `Tests/Core/EditMode/ManualCodeExamplesTests.cs`, no molde do `ManualLinksTests`:
  - `NoHandWrittenCSharpBlocks`: todo bloco ` ```csharp `, ` ```cs ` ou ` ```c# ` de `manual/**` tem só linhas `--8<--`. A guarda entra com uma lista de páginas pendentes que só diminui, como no D7;
  - `SnippetsPointToExistingSections`: cada inclusão aponta para um arquivo que existe e para uma seção com `start` e `end`;
  - `SnippetSourcesAreCompiledCode`: o arquivo fica em `Packages/com.spellbook/Tests/`, ou em `Samples~/Sandbox/Scripts/` só para `exemplos/sandbox.md`.
- Os gatilhos da Action (D5) ganham `Packages/com.spellbook/Tests/**/Examples/**` e `Packages/com.spellbook/Samples~/Sandbox/Scripts/**`, porque o site muda quando o exemplo muda. É a única exceção à regra de gatilhos só em `Documentation~/` (0040, item 7), e a 0041 a registra.
- A migração dos 41 blocos termina antes do MN8. Assim, os tickets que dividem as páginas (MN8 a MN17) só movem linhas `--8<--`, e a lista de pendências já está vazia quando os caminhos das páginas mudam. Bloco que não pode virar código real (assinatura solta, contraexemplo) vira texto ou uma referência à API.

### Quebras de compatibilidade da emenda

Nenhuma no pacote. O `SpellBook.Core.Editor` ganha um tipo público (`UnityXrefMap`). No fluxo do time, todo bloco C# novo do manual precisa sair de um teste.

### Tickets da emenda

| Ticket | Título da issue | Unity? | Depende de | Par com Unity (WIP 2) | Tamanho |
|---|---|---|---|---|---|
| XR1 | `XR1: feat(core-editor): mapa de referências cruzadas da API para a documentação mais recente da Unity` | sim | RC4 | nenhum | 2 arquivos; 599 linhas, fora a saída gerada (exceção de 2026-11-02 da 0003) |
| XR2 | `XR2: feat(core-editor): ligar o mapa da Unity à geração e ao build da API, sem mapa do .NET` | sim | XR1, RC5 | nenhum | 6 arquivos; ~170 linhas, fora a saída gerada |
| EX1 | `EX1: feat(docs): exemplos do manual incluídos de testes, com guarda` | sim | RC5, XR2 | nenhum (lote 8) | 8 arquivos; ~380 linhas |
| EX2 | `EX2: docs(manual): exemplos do Inspector e do Validador tirados de testes` | sim | EX1, DM7 | DM8 (lote 8) | 4 arquivos; ~270 linhas |
| EX3 | `EX3: docs(manual): exemplos do GAS tirados de testes` | sim | EX5, EV7 | marcado no lote 9 | 3 arquivos; recontar na entrada |
| EX4 | `EX4: docs(manual): exemplos de Eventos e Tags tirados de testes` | sim | EX2 | EV1 (lote 8) | 5 arquivos; ~290 linhas |
| EX5 | `EX5: docs(manual): exemplos de Pooling e Tempo tirados de testes` | sim | EX4 | EV2 (lote 8) | 5 arquivos; ~210 linhas |
| EX6 | `EX6: docs(manual): exemplos de Dano e da migração tirados de testes, guarda completa` | sim | EX3, DM8 | marcado no lote 9 | 4 arquivos; ~300 linhas, recontar na entrada |

Emenda de 2026-11-02, aprovada em 2026-11-02 (L1 a L3): as dependências, os pares e os tamanhos acima são os da emenda do lote 8 e do detalhamento do EX2, do EX4 e do EX5 (seção "EX2 a EX5 — migração por página"). O título do EX5 perdeu o ", guarda completa", porque quem apaga a lista de pendências passa a ser o EX6. Se a issue do EX5 já existir com o título antigo, o Controlador troca o título.

O XR1 e o XR2 não formam par com nenhum ticket com Unity: os dois mudam `.cs` do pacote e regeneram `api/reference.sha256`, como os RC6 a RC8.

Ordem na fila com Unity, que não muda a dos RC aprovados: RC4 → RC6 → RC7 → RC8 → XR1 → XR2 → EX1 → EX2 → EX3 → EX4 → EX5 → MN8. O XR1 só precisa do RC4 e pode subir para logo depois dele, se o usuário preferir. O XR2 e o EX1 esperam o RC5 (sem Unity), porque mexem no `guia-do-manual.md` e no `docs.yml` depois dele. Os EX2 a EX5 são contados de novo quando entrarem na fila, porque os tickets do manual anteriores podem mudar os blocos.

A recontagem foi feita em 2026-11-02, e a ordem dos EX mudou: veja a emenda de 2026-11-02 do lote 8, no fim desta Spec (aprovada em 2026-11-02). A ordem que vale é EX1 → EX2 → EX4 → EX5 → EX3 → EX6.

As respostas X1 e X2 mudam só o XR1 e o XR2. Os EX1 a EX5 tratam da inclusão dos exemplos e ficam como estavam, com uma regra a mais: o texto do manual em volta de um exemplo não ganha link escrito à mão para a documentação do .NET (a emenda de 2026-11-02 do X2 trata só da API, onde o link vem do DocFX), e um tipo da Unity citado nele aponta para a página mais recente, sem versão na URL, como o mapa. O guia do EX1 registra isso.

#### XR1 — `feat(core-editor): mapa de referências cruzadas da API para a documentação mais recente da Unity`

- Escopo:
  - `Editor/Core/Documentation/UnityXrefMap.cs` (novo, com `ExternalReference`);
  - `Tests/Core/EditMode/Documentation/UnityXrefMapTests.cs` (novo; YAML de entrada montados como texto na pasta temporária, apagada no `TearDown`);
  - saída gerada, fora da contagem: `api/reference/**` (o YAML do tipo público novo e o que mais o `docfx metadata` mudar) e `api/reference.sha256`. O `.cs` novo muda a impressão digital, e sem a regeneração a guarda `CommittedFingerprint_MatchesSources` do RC4 falha (D6). O catálogo não muda, porque o `UnityXrefMap` não é tipo de seletor. O `unity.xrefmap.yml` ainda não é escrito: a etapa 3b entra no XR2.
- Testes red:
  - `Read_OnlyExternalReferences`: entradas sem `isExternal: true` ficam fora;
  - `Href_EngineType_LatestScriptReference`: `UnityEngine.GameObject` vira `https://docs.unity3d.com/ScriptReference/GameObject.html`, sem versão na URL;
  - `Href_EditorType_DropsUnityEditorPrefix`;
  - `Href_NestedNamespace_KeepsSubNamespace` (por exemplo, `UnityEngine.UIElements.VisualElement`);
  - `Href_Method_UsesDot`, `Href_Property_UsesHyphen`, `Href_Constructor_UsesCtor`;
  - ~~`Href_PackageType_UsesLatest`~~, trocado pelos quatro testes abaixo na emenda de 2026-11-02 do D8 (aprovada em 2026-11-02, X6). A versão sai de uma função interna `PackageApiHref(string packageName, string packageVersion, string typeUid)` no `UnityXrefMap`, que o `ResolveHref` chama, para os casos de versão serem testados sem depender do pacote instalado:
    - `PackageApiHref_UsesMajorMinor`: `("com.unity.ugui", "2.0.0", "UnityEngine.UI.Button")` vira `https://docs.unity3d.com/Packages/com.unity.ugui@2.0/api/UnityEngine.UI.Button.html`;
    - `PackageApiHref_DropsPrereleaseAndPatch`: `"1.6.0-pre.1"` e `"1.6.12"` viram `@1.6`;
    - `PackageApiHref_InvalidVersion_Throws`: `"latest"` e `""` lançam exceção com o `uid` e a versão na mensagem;
    - `Href_PackageType_UsesInstalledVersion`: `UnityEngine.TestTools.UnityTestAttribute`, do `com.unity.test-framework` que os testes já carregam, vira um link que casa com `^https://docs\.unity3d\.com/Packages/com\.unity\.test-framework@\d+\.\d+/api/UnityEngine\.TestTools\.UnityTestAttribute\.html$`. O teste não escreve o número da versão, para não quebrar quando o pacote for atualizado. Nenhum assembly do pacote usa pacote `com.unity.*` hoje;
  - `Href_BuiltInModuleType_UsesScriptReference` (emenda de 2026-11-02): `UnityEngine.Rigidbody`, do módulo de física, vira `https://docs.unity3d.com/ScriptReference/Rigidbody.html`, sem `/Packages/`;
  - `Href_SystemType_IsNull`, com `System.String` e `System.Collections.Generic.List`1`;
  - `Render_SystemReference_NotInMap`: uma referência `System.*` na entrada, com o `href` para learn.microsoft.com como o `docfx metadata` escreve, não gera entrada nem erro;
  - `Render_UnresolvedUnityReference_Throws`, com o `uid` na mensagem;
  - `Render_SameInputTwice_SameText`, `Render_SortedByUidWithXrefHeader`.
- Critério de pronto: os testes passam; as guardas do RC4 passam com a referência regenerada; nenhuma URL do mapa tem número de versão da Unity; a Fábrica registra no relatório a regra de URL conferida em páginas reais da documentação mais recente (tipo, método, propriedade, construtor, tipo aninhado, genérico, tipo de Editor, tipo de pacote com `@latest`) e o formato do `xrefmap` conferido na documentação do DocFX travado.
  - Emenda de 2026-11-02, aprovada em 2026-11-02 (X6): "nenhuma URL do mapa tem número de versão da Unity" continua valendo para o ScriptReference. As URLs de pacote têm a versão `maior.menor` do pacote, nunca `@latest`. No relatório, o "tipo de pacote com `@latest`" vira "tipo de pacote com a versão instalada": a Fábrica abre o link que o `Href_PackageType_UsesInstalledVersion` gera e registra o status. Para o ScriptReference, ela registra a cadeia de redirecionamentos e o status final de cada caso da amostra (emenda do D8).
- Tamanho estimado: ~400 linhas (~170 de código, ~230 de teste); 2 arquivos, fora a saída gerada. A saída gerada não muda a contagem (emenda de 2026-11-01 da 0003, "saída de gerador commitada"). A emenda de 2026-11-02 soma ~15 linhas de código (`PackageApiHref` e a regra dos `com.unity.modules.*`) e ~30 de teste, nos mesmos 2 arquivos: ~445 linhas.
  - Tamanho real, na revisão de 2026-11-02: **599 linhas e 2 arquivos**, fora a saída gerada. Passa pela margem de ~30%, como exceção única registrada na emenda de 2026-11-02 da 0003 ("exceção única de linhas para o XR1 do B-019g"). O escopo não muda: os mesmos 2 arquivos e a mesma saída gerada.

#### XR2 — `feat(core-editor): ligar o mapa da Unity à geração e ao build da API, sem mapa do .NET`

- Escopo:
  - `Editor/Core/Documentation/ReferenceGenerator.cs`: a etapa 3b do D8;
  - `Tests/Core/EditMode/Documentation/ReferenceGeneratorTests.cs`: o mapa é escrito, e não é escrito se o runner falha;
  - `Tests/Core/EditMode/Documentation/ReferenceUpToDateTests.cs`: as duas guardas do D8;
  - `Documentation~/api/docfx.json`: a chave `xref`, só com o mapa da Unity;
  - `Documentation~/guia-do-manual.md`: o mapa entra na lista do que se commita (D6), e o guia diz que os links da Unity vão para a documentação mais recente e que os do .NET vão para learn.microsoft.com pelo `href` que o `docfx metadata` grava, sem mapa do .NET e sem rede no build. Pela emenda de 2026-11-02 do D8 (aprovada em 2026-11-02, X6), o guia também diz que os links de pacote `com.unity.*` vão para a versão instalada no projeto e que atualizar um desses pacotes pede gerar a referência de novo;
  - `Packages/com.spellbook/CHANGELOG.md`: links para a documentação mais recente da Unity, em "Documentação";
  - saída gerada, fora da contagem: `api/unity.xrefmap.yml` e o que mais a geração mudar.
- Testes red: `Generate_WritesUnityXrefMap`; `Generate_RunnerFails_NoXrefMap`; as guardas `CommittedXrefMap_MatchesCommittedReference` e `EveryUnityReference_HasXrefEntry` falham antes da primeira geração, porque o mapa não existe.
- Critério de pronto: os testes e as guardas passam; o `docfx build --warningsAsErrors` local, sem rede, e o da Action passam; no site gerado, `MonoBehaviour` e `GameObject` numa assinatura são links para `docs.unity3d.com/ScriptReference/`, sem versão na URL (CA9), e `string` ou `float` são links para learn.microsoft.com, sem chave `xref` do .NET no `docfx.json`; a Fábrica abre 10 links do mapa, de tipos diferentes, e registra no relatório que todos abrem.
- Tamanho estimado: ~170 linhas (~40 de código, ~90 de teste, ~40 de config, guia e CHANGELOG); 6 arquivos, fora a saída gerada. A emenda de 2026-11-02 do D8 soma ~3 linhas no guia, no mesmo arquivo. A entrada do CHANGELOG diz que os links de pacote vão para a versão instalada. O critério de pronto não muda, porque a API de hoje não usa tipo de pacote `com.unity.*`.

#### EX1 — `feat(docs): exemplos do manual incluídos de testes, com guarda`

- Escopo:
  - `Documentation~/mkdocs.yml`: o `pymdownx.snippets` (D9);
  - `Documentation~/requirements-docs.txt`: a trava do `pymdown-extensions`;
  - `.github/workflows/docs.yml`: os gatilhos do D9;
  - `Tests/Core/EditMode/ManualCodeExamplesTests.cs` (novo), com as 7 páginas que sobram na lista de pendências;
  - `Documentation~/guia-do-manual.md`: como escrever um exemplo (arquivo, marcadores, inclusão) e a regra de links do texto em volta (Unity na versão mais recente, .NET sem link escrito à mão);
  - a primeira página migrada, `inicio/conceitos.md` (2 blocos), e o arquivo de exemplos do módulo que ela usa, em `Tests/<Módulo>/EditMode/Examples/` (novo);
  - `Packages/com.spellbook/CHANGELOG.md`: exemplos do manual tirados de testes, em "Documentação".
- Testes red:
  - `NoHandWrittenCSharpBlocks` falha hoje em `inicio/conceitos.md`, que não está na lista;
  - `SnippetsPointToExistingSections` falha com uma inclusão de rascunho para uma seção que não existe (manual, no relatório; o rascunho sai antes do diff);
  - manual, no relatório: o `mkdocs build --strict` falha com um caminho de inclusão inexistente.
- Critério de pronto: os testes passam; o `mkdocs build --strict` local e o da Action passam; o HTML de `conceitos` mostra o trecho sem os marcadores; nenhuma inclusão sai da raiz do repositório.
- Tamanho estimado: ~330 linhas (~150 de teste da guarda, ~100 de exemplos, ~80 de config e CI); 8 arquivos (a página do manual conta só nos arquivos).

#### EX2 a EX5 — migração por página

Todos têm a mesma forma, e cada um tira suas páginas da lista de pendências do `ManualCodeExamplesTests.cs`:
- Escopo: as páginas, o arquivo de exemplos de cada módulo em `Tests/<Módulo>/EditMode/Examples/` e o `ManualCodeExamplesTests.cs`.
- Testes red: com as páginas fora da lista, o `NoHandWrittenCSharpBlocks` falha nelas.
- Critério de pronto: os testes de exemplo rodam e passam; a guarda passa; o `mkdocs build --strict` passa; o texto em volta de cada bloco continua dizendo o mesmo, e o bloco mostra a API do dia. Onde o bloco antigo divergia da API, o relatório lista a correção.

| Ticket | Páginas | Blocos | Arquivos | Linhas |
|---|---|---|---|---|
| EX2 | `inicio/inspector.md`, `ferramentas/validador.md` | 11 | até 6 (2 páginas, até 3 arquivos de exemplo, a guarda) | ~400 |
| EX3 | `modulos/gas.md` | 7 | 3 | ~300 |
| EX4 | `modulos/eventos.md`, `modulos/tags.md` | 10 | 5 | ~380 |
| EX5 | `modulos/pooling.md`, `modulos/tempo.md`; a lista de pendências é apagada | 11 | 5 | ~400 |

As linhas são dos arquivos de exemplo, com ~25 por bloco somando o `[Test]` e a asserção. Se a contagem na hora passar de ~500 linhas, a Fábrica para e devolve, e o Arquiteto divide por página.

A tabela acima é de 2026-11-01. Os escopos, os testes e os tamanhos que valem são os do detalhamento abaixo, de 2026-11-02, recontados nas páginas de hoje. O EX3 é detalhado quando entrar no lote 9, depois do EV7.

##### Regras comuns do EX2 ao EX6 (detalhamento de 2026-11-02)

- O arquivo de exemplos segue a forma que o EX1 registrou no guia do manual: onde ficam as declarações de tipo, o namespace e os marcadores. Nenhum EX daqui muda o `mkdocs.yml`, o `requirements-docs.txt` ou o `docs.yml`.
- Um bloco pode ter mais de uma linha `--8<--`, uma por seção. É assim que um bloco que mistura declaração de tipo e chamada (por exemplo, a classe `Placar` e o `EventBus<PartidaComecou>.Raise` de `eventos.md`) sai de um arquivo só. O comentário que o bloco tem entre as duas partes (`// Em qualquer outro sistema:`) fica dentro de uma das seções.
- Um trecho que é membro de uma classe (um campo, um método) sai sem o recuo da classe se o guia do EX1 disser como. Se o guia não cobrir, o bloco mostra o tipo inteiro em volta do trecho, e o texto em volta continua dizendo o mesmo.
- Uma declaração de tipo não cabe dentro de um método. Ela fica no arquivo de exemplos, fora do `[Test]`, e um `[Test]` do mesmo arquivo usa o tipo e confere o que a página diz dele. Exemplo: o `TypePickerCatalog.GetCandidates(typeof(MinhaAcao))` tem a `MinhaEspera`, com a categoria `Fluxo/Espera` e o resumo do bloco.
- O EditMode não chama o `Awake`, o `OnEnable` nem o `Update` de um `MonoBehaviour` criado no teste. Quando o exemplo depende desse ciclo, o `[Test]` chama a API do pacote que o exemplo usa, na ordem que o ciclo usaria (por exemplo, `Subscribe` e depois `Raise`), e o tipo do exemplo só precisa compilar. Não vale pôr fora da seção um atributo ou código que mude o comportamento do exemplo, como `[ExecuteAlways]`.
- Dois blocos de uma página que declaram o mesmo nome de tipo (`MinhaEspera`, em `inspector.md`) não compilam no mesmo arquivo. A Fábrica troca o nome de um deles, ajusta o texto em volta e lista a troca no relatório.
- Atributos que registram algo no Editor (`[CreateAssetMenu]`, `[CustomEditor]`, `[CustomPropertyDrawer]`) ficam como na página. Eles só valem para os tipos de exemplo e só nos projetos que compilam os testes do pacote.
- Bloco que não pode virar código real vira texto ou referência à API, pela regra do D9: assinatura solta, contraexemplo que não compila ou cópia de um tipo do próprio pacote. Mudar a cerca para ` ```text ` só para escapar da guarda não vale.
- Os testes do ticket, para o filtro da emenda de 2026-11-01 da 0003, são a classe de exemplos de cada módulo do escopo (`\.<Módulo>ManualExamples\.`) e o `\.ManualCodeExamplesTests\.`. Os testes de exemplo passam desde o primeiro run, porque a API já existe. O red do ticket é o da guarda.
- Nenhum EX daqui muda `.cs` de `Runtime/` ou `Editor/`, o `CHANGELOG.md` ou a saída gerada. Os assemblies de teste ficam fora da impressão digital (D2). Se um exemplo pedir mudança no pacote ou uma referência nova num `.asmdef` de teste, a Fábrica para e devolve.

##### EX2 — `docs(manual): exemplos do Inspector e do Validador tirados de testes`

- Escopo:
  - `Documentation~/manual/inicio/inspector.md` (9 blocos);
  - `Documentation~/manual/ferramentas/validador.md` (2 blocos, depois do DM7);
  - `Tests/Core/EditMode/Examples/CoreManualExamples.cs` (do EX1): os exemplos das duas páginas;
  - `Tests/Core/EditMode/ManualCodeExamplesTests.cs`: as duas páginas saem da lista de pendências.
- Blocos de `inspector.md`: o campo `[SerializeReference, TypePicker]` numa lista; o `[TypeCategory]` com `Summary`; o alias `SB.TypeCategory`; o `[MovedFrom]`; o `[ShowIf]` com `enum` e com `bool`; o alias `SB.ShowIf`; o editor de uma linha que herda de `SpellBookEditor`, sem e com `true` no segundo argumento; o `PropertyDrawer` com `CreatePropertyGUI`. Todos são declarações de tipo, e os nove viram código. O assembly `SpellBook.Core.Tests.EditMode` já referencia o `SpellBook.Core.Editor` e roda só no Editor, então o `SpellBookEditor`, o `UnityEditor` e o `UnityEditor.UIElements` estão disponíveis.
- Blocos de `validador.md`: a classe de teste com o `ProjectValidator.Validate(ProjectValidator.CollectProjectTargets(), ...)`, que já é um `[Test]`, e a `Porta` com `IValidatable`. O primeiro valida o projeto inteiro de novo, como o `Project_HasNoValidationErrors` (`Tests/Core/EditMode/ProjectValidationTests.cs:116`). O segundo tem um `[Test]` que monta a `Porta` sem chave e com tempo zero e confere um erro em `chave` e um aviso em `tempoAberta`.
- Testes red: com as duas páginas fora da lista de pendências, o `NoHandWrittenCSharpBlocks` falha em `inicio/inspector.md` e em `ferramentas/validador.md`.
- Critério de pronto:
  - o mesmo dos EX2 a EX5, acima;
  - os testes de `CoreManualExamples` e o `ManualCodeExamplesTests` passam, e o `TypePickerCatalogTests` e o `TypeCatalogTests` continuam passando com os tipos de exemplo novos no assembly de testes;
  - o relatório lista a troca de nome do segundo `MinhaEspera`.
- Par com Unity: DM8 (emenda do lote 8). O EX2 não cria arquivo de exemplo fora de `Tests/Core/EditMode/Examples/`.
- Tamanho estimado: ~270 linhas (~200 dos 9 exemplos do Inspector, ~60 dos 2 do Validador, ~10 da guarda); 4 arquivos, com as duas páginas contando só nos arquivos. A tabela de 2026-11-01 previa até 3 arquivos de exemplo; todos ficam no `CoreManualExamples.cs`.

##### EX4 — `docs(manual): exemplos de Eventos e Tags tirados de testes`

- Escopo:
  - `Documentation~/manual/modulos/eventos.md` (5 blocos);
  - `Documentation~/manual/modulos/tags.md` (5 blocos);
  - `Tests/Events/EditMode/Examples/EventsManualExamples.cs` (novo);
  - `Tests/Tags/EditMode/Examples/TagsManualExamples.cs` (novo);
  - `Tests/Core/EditMode/ManualCodeExamplesTests.cs`: as duas páginas saem da lista de pendências.
- Blocos de `eventos.md`: o `struct PartidaComecou : IEvent`; o `Placar` com `Subscribe`, `Unsubscribe` e o `Raise` (duas seções); a `BarraDeVida` com o `IntEventChannel` e o `Raise(80)` (duas seções); a reação `TocarSom`; o payload `Dano` com o `DanoEventChannel` e a `DanoEventResponse`. Os cinco viram código. O `[Test]` do `Placar` e o da `BarraDeVida` seguem a regra do ciclo de vida acima.
- Blocos de `tags.md`: criar uma `GameplayTag`; o `GameplayTagContainer` com `Add`, `Remove` e `HasTag`; a `GameplayTagQuery` com `All`, `Any` e `None`; o `Validate` que repassa para o conjunto e para a consulta; o `TagDebug.LogChain`. Os cinco viram código. As definições de tag (`stunnedDefinition` e as outras) são criadas no `SetUp` com `ScriptableObject.CreateInstance` e apagadas no `TearDown`, e os valores dos comentários do bloco (`true`, `false`) viram asserções. O `TagDebug` é conferido com `LogAssert.Expect` na cadeia `State.Debuff.Slowed → State.Debuff → State`.
- Os dois assemblies de teste já referenciam o que os exemplos usam: o dos Eventos tem `SpellBook.Events` e `SpellBook.Core`, e o das Tags tem `SpellBook.Tags` e `SpellBook.Core` (para o `ValidationResult`).
- Testes red: com as duas páginas fora da lista, o `NoHandWrittenCSharpBlocks` falha em `modulos/eventos.md` e em `modulos/tags.md`.
- Critério de pronto: o mesmo dos EX2 a EX5; os testes de `EventsManualExamples`, de `TagsManualExamples` e o `ManualCodeExamplesTests` passam.
- Par com Unity: EV1 (emenda do lote 8).
- Tamanho estimado: ~290 linhas (~150 dos exemplos dos Eventos, ~130 dos das Tags, ~10 da guarda); 5 arquivos. Os `.meta` das pastas `Examples/` novas não contam.

##### EX5 — `docs(manual): exemplos de Pooling e Tempo tirados de testes`

- Escopo:
  - `Documentation~/manual/modulos/pooling.md` (6 blocos);
  - `Documentation~/manual/modulos/tempo.md` (5 blocos);
  - `Tests/Pooling/EditMode/Examples/PoolingManualExamples.cs` (novo);
  - `Tests/Time/EditMode/Examples/TimeManualExamples.cs` (novo);
  - `Tests/Core/EditMode/ManualCodeExamplesTests.cs`: as duas páginas saem da lista de pendências. A lista não é apagada; isso fica com o EX6.
- Blocos de `pooling.md`:
  - as duas assinaturas abstratas de `SpawnerAsset` (`Spawn` e `Release`) não compilam soltas e viram texto com link para a página do `SpawnerAsset` na API;
  - os outros cinco viram código: o campo de spawner com o `SpawnerAsset.OrDefault` (duas seções: o campo e a chamada), o `CountingSpawnerAsset`, o `ReleaseWhenParticlesStop`, o `Spawn` e o `Release` por um `PooledSpawnerAsset` e o `Bullet : IPoolable`.
- Blocos de `tempo.md`:
  - a interface `ITimeSource` é cópia de um tipo do pacote e vira texto com link para a página dela na API;
  - o contraexemplo do namespace `SpellBook.MeuJogo`, que não compila de propósito, vira uma frase com o `Time.deltaTime` em código em linha;
  - os outros três viram código: o `PausableTimeSource` com `Pause` e `Resume`, a `ManualTimeSource` com os valores dos comentários conferidos em asserções, e o `Cronometro`, conferido pela regra do ciclo de vida.
- Os dois assemblies de teste já referenciam o que os exemplos usam (`SpellBook.Core` e `SpellBook.Pooling`; `SpellBook.Core` e `SpellBook.Time`).
- Testes red: com as duas páginas fora da lista, o `NoHandWrittenCSharpBlocks` falha em `modulos/pooling.md` e em `modulos/tempo.md`.
- Critério de pronto: o mesmo dos EX2 a EX5; os testes de `PoolingManualExamples`, de `TimeManualExamples` e o `ManualCodeExamplesTests` passam; o relatório lista os três blocos que viraram texto ou link e mostra que o texto em volta diz o mesmo.
- Par com Unity: EV2 (emenda do lote 8).
- Tamanho estimado: ~210 linhas (~120 dos 5 exemplos do Pooling, ~80 dos 3 do Tempo, ~10 da guarda); 5 arquivos. Dos 11 blocos, 8 viram código.

### Respostas do gate da emenda (2026-11-01)

- X1: alternativa. Os links vão para a documentação mais recente da Unity, sem a versão 6000.3 (D8, XR1, XR2).
- X2: alternativa. Sem link para o .NET; os tipos do .NET ficam como texto, e o build não depende de rede (D8, XR1, XR2).
  - Emenda de 2026-11-02, decisão do usuário: na API, os tipos `System.*` ficam com o link para learn.microsoft.com que o `docfx metadata` 2.81 grava sozinho no YAML (`GetMicrosoftLearnUrl`, fixo no código, sem download e sem opção para desligar). O build continua sem rede e sem mapa do .NET no `docfx.json`. Na quarta rodada do RC4, a geração saiu com 0, com 267 YAML em `api/reference/` (16 MB, ~1,5 MB deles nesses `href`), e o `docfx metadata` e o `docfx build --warningsAsErrors` passaram sem aviso nem erro. Mudam o D4, o D8, o XR1 (entrada do teste), o XR2 (guia e critério de pronto) e a regra do EX1. O título do XR2 continua certo.
- X3: proposta. Mapa gerado pelo SpellBook.
- X4: proposta. Exemplos só de testes EditMode; o Sandbox só na página dele.
- X5: proposta. `pymdownx.snippets` com a versão travada.

A ADR 0041 e a emenda de 2026-11-01 da 0040 foram aceitas no mesmo gate. As perguntas, como foram ao gate:

- X1. Versão dos links da Unity. Proposta: a `maior.menor` da Unity do host (`6000.3`), lida na geração; o link não muda quando sai uma Unity nova. Alternativa: a documentação mais recente, sem versão na URL; o link fica sempre atual, mas pode levar a uma API que mudou.
- X2. Tipos do .NET (`System.*`). Proposta: link para learn.microsoft.com pelo mapa que a Microsoft publica (`https://learn.microsoft.com/en-us/dotnet/.xrefmap.json`), baixado pelo `docfx build` na Action. A Fábrica confere a URL na versão travada. Alternativa: sem link, só o nome, e o build não depende de rede.
- X3. Origem do mapa da Unity. Proposta: gerado pelo SpellBook (D8, XR1 e XR2). Alternativa: o mapa do UnityXrefMaps por URL, sem o XR1, depois de conferir a cobertura da 6000.3 e a licença dos dados. A Action passaria a depender de um site de terceiro, e os pacotes `com.unity.*` ficariam sem link.
- X4. De onde vêm os exemplos. Proposta: só de testes EditMode, que compilam e rodam na suíte, e do Sandbox só na página `exemplos/sandbox.md`. Alternativa: o Sandbox em qualquer página; o exemplo fica mais próximo de um jogo, mas a compilação só é conferida com a pasta renomeada (0028).
- X5. Inclusão de arquivo no MkDocs. Proposta: a extensão `pymdownx.snippets`, que já vem instalada com o Material, com a versão travada no `requirements-docs.txt`. Alternativa: um hook próprio do MkDocs, em Python no repositório, sem extensão nova, mas com código nosso para manter.

### Pergunta do gate parcial de 2026-11-02 (X6)

Resposta (2026-11-02): X6 como proposto. Os links de pacote `com.unity.*` usam a versão `maior.menor` instalada; o ScriptReference continua sem versão. A emenda de 2026-11-02 da 0041 foi aceita no mesmo gate.

Vem da devolução do XR1: a URL `@latest/api/<uid>.html` dos pacotes `com.unity.*` dá 404 (emenda de 2026-11-02 do D8). O gate cobre o D8, o XR1, o XR2 e a emenda de 2026-11-02 da 0041. O resto da Spec não muda.

- X6. Versão dos links de pacote `com.unity.*`.
  - Proposta: a versão `maior.menor` do pacote instalado no projeto, lida do `PackageInfo` na geração (`com.unity.test-framework@1.6/api/...`). A página é estática, abre sem JavaScript e é a da versão contra a qual a API foi compilada. O link de pacote deixa de ser o "mais recente" do X1, que continua valendo para o ScriptReference. Atualizar um pacote pede regenerar a referência, e a guarda cobra.
  - Alternativa A: a rota que a própria Unity usa no redirecionamento do ScriptReference, `https://docs.unity3d.com/Packages/<pacote>@latest/index.html?subfolder=/api/<uid>.html`. Mantém o X1 ao pé da letra, mas depende de um parâmetro sem documentação, de um script na página e de JavaScript no navegador. Sem JavaScript, o leitor cai no índice da versão, e um verificador de links vê só a página de redirecionamento. Na conferência de 2026-11-02, essa URL não foi aberta num navegador.
  - Alternativa B: sem link para pacote. Tipos `com.unity.*` ficam como texto, e a guarda `EveryUnityReference_HasXrefEntry` passa a cobrir só o motor. Hoje a API não usa nenhum tipo de pacote, mas a uGUI declarada pela 0039 vai usar.

## Emenda de 2026-11-02: recontagem dos EX e lote 8

Estado: aprovada no gate parcial de 2026-11-02, com L1, L2 e L3 como propostos (respostas no fim desta seção). Vem da montagem do lote 8 e da emenda de 2026-11-02 da 0003, que estende o experimento de WIP 2 a esse lote. O D8, o D9, o XR1, o XR2 e os RC não mudam.

### Recontagem dos blocos (2026-11-02)

O manual tem hoje 53 blocos ` ```csharp ` em 10 páginas, e não 41 em 8. Contagem por página, com a busca do `NoHandWrittenCSharpBlocks` (cerca ` ```csharp `, ` ```cs ` ou ` ```c# `, inclusive recuada):

| Página | Blocos na Spec | Blocos hoje | Por quê |
|---|---|---|---|
| `inicio/conceitos.md` | 2 | 4 | condições do Core (`AndConditionBase`, `Condition<T>` com `Line`), do B-009 |
| `inicio/inspector.md` | 9 | 9 | |
| `ferramentas/validador.md` | 2 | 2 | o DM7 escreve nesta página e pode mudar o número |
| `modulos/gas.md` | 7 | 9 | `EffectSpec` e `SetByCallerValues` (B-007) e condição própria (B-009); EV1 a EV7 escrevem mais |
| `modulos/eventos.md` | 5 | 5 | |
| `modulos/tags.md` | 5 | 5 | |
| `modulos/pooling.md` | 6 | 6 | |
| `modulos/tempo.md` | 5 | 5 | |
| `modulos/dano-escudo-e-morte.md` | não existia | 7 | página nova do DM1 a DM6; o DM8 acrescenta três exemplos completos |
| `regras/migracao-2-para-3.md` | 0 | 1 | `OnAbilityActivated`, dentro de um item de lista |

Consequências:
- O EX1 migra 4 blocos. A lista de pendências da guarda nasce com 9 páginas, e não 7.
- O EX2, o EX4 e o EX5 têm os mesmos blocos de antes e cabem no teto.
- O EX3 tem 9 blocos hoje e vai ter mais depois do B-012. Ele é recontado quando entrar na fila.
- Nenhum ticket cobria a página de dano nem a de migração, e o EX5 não podia apagar a lista de pendências com as duas ainda nela. Entra o EX6 (L2).

### Ordem nova da cadeia

A cadeia EX1 → EX2 → EX3 → EX4 → EX5 era em série porque todos mudam a lista de pendências do `ManualCodeExamplesTests.cs`. Nenhum EX depende do conteúdo de outro, então a ordem pode mudar. A ordem nova é EX1 → EX2 → EX4 → EX5 → EX3 → EX6:
- EX1, EX2, EX4 e EX5 mexem só em páginas do Core, dos Eventos, das Tags, do Pooling e do Tempo, que o B-012 não muda. Eles entram no lote 8.
- O EX3 (`gas.md`) espera o EV7 (L1). Os EV1 a EV7 escrevem no `gas.md`, e a troca da base das ações no EV3 muda a API que os exemplos mostram. Com o `gas.md` ainda na lista de pendências, cada EV escreve o bloco à mão, e o EX3 migra tudo uma vez, com a API do dia. Se o EX3 viesse antes, todo EV que escreve bloco C# no `gas.md` teria de pôr `Tests/GAS/EditMode/Examples/GasManualExamples.cs` no escopo. O EV4a já está em 10 arquivos e passaria do limite, e o EV3, que tem exceção revisada pela lista de arquivos, ganharia um arquivo fora dela.
- O EX6 (dano e migração) espera o DM8, que escreve os exemplos completos da página de dano, e o EX3, porque os dois escrevem o mesmo arquivo de exemplos do GAS. Ele apaga a lista de pendências.
- O EX3 e o EX6 continuam antes do MN8. A regra do D9 (a migração termina antes do MN8) não muda.

O EX2 passa a depender também do DM7, porque os dois escrevem `ferramentas/validador.md`.

### Tickets recontados

| Ticket | Páginas | Blocos | Arquivos | Linhas | Depende de | Lote |
|---|---|---|---|---|---|---|
| EX1 | `inicio/conceitos.md` | 4 | 8 | ~380 | RC5, XR2 | 8 |
| EX2 | `inicio/inspector.md`, `ferramentas/validador.md` | 11 | 4 | ~270 | EX1, DM7 | 8 |
| EX4 | `modulos/eventos.md`, `modulos/tags.md` | 10 | 5 | ~290 | EX2 | 8 |
| EX5 | `modulos/pooling.md`, `modulos/tempo.md` | 11, dos quais 8 viram código | 5 | ~210 | EX4 | 8 |
| EX3 | `modulos/gas.md` | 9 hoje, mais os do EV1 a EV7 | 3 | recontar na entrada | EX5, EV7 | 9 |
| EX6 | `modulos/dano-escudo-e-morte.md`, `regras/migracao-2-para-3.md`; a lista de pendências é apagada | 8 hoje, mais os do DM8 | 4 | ~300, recontar na entrada | EX3, DM8 | 9 |

- EX1: o escopo não muda. Os exemplos de `conceitos.md` ficam em `Tests/Core/EditMode/Examples/CoreManualExamples.cs`, e a lista de pendências nasce com as 9 páginas da tabela da recontagem, menos `conceitos.md`. A conta: ~150 de guarda, ~150 de exemplos (4 blocos, com os tipos de condição de teste) e ~80 de configuração e CI.
- EX2: os exemplos do Inspector e do Validador são do Core e ficam no mesmo `CoreManualExamples.cs`. Num par com outro ticket, o EX2 não cria arquivo de exemplo fora de `Tests/Core/EditMode/Examples/`; se precisar, a Fábrica para e devolve.
- EX4: `Tests/Events/EditMode/Examples/EventsManualExamples.cs` e `Tests/Tags/EditMode/Examples/TagsManualExamples.cs`, novos.
- EX5: `Tests/Pooling/EditMode/Examples/PoolingManualExamples.cs` e `Tests/Time/EditMode/Examples/TimeManualExamples.cs`, novos. O EX5 tira as duas páginas da lista e não a apaga mais.
- EX3: `modulos/gas.md`, `Tests/GAS/EditMode/Examples/GasManualExamples.cs` (novo) e a guarda. Se a recontagem na entrada passar de ~500 linhas, a Fábrica para e devolve, e o Arquiteto divide o ticket por seção do `gas.md`.

O escopo, os testes e o tamanho de cada um do EX2, do EX4 e do EX5 estão no detalhamento de 2026-11-02, dentro da seção "EX2 a EX5 — migração por página". As linhas da tabela acima já são as recontadas lá.

#### EX6 — `docs(manual): exemplos de Dano e da migração tirados de testes, guarda completa`

Ticket novo da L2, aprovado em 2026-11-02. Issue nova: `EX6: docs(manual): exemplos de Dano e da migração tirados de testes, guarda completa`. Usa a Unity. Depende do EX3 e do DM8 e, pelo EX3, do EV7. Entra no lote 9, e o par de WIP 2, se houver, é marcado quando o lote 9 for montado. As regras comuns do EX2 ao EX6, na seção "EX2 a EX5 — migração por página", valem aqui.

- Escopo:
  - `Documentation~/manual/modulos/dano-escudo-e-morte.md`: os 7 blocos de hoje e os três exemplos completos que o DM8 escreve à mão;
  - `Documentation~/manual/regras/migracao-2-para-3.md`: o bloco do `OnAbilityActivated`, dentro de um item de lista da seção "Eventos do GAS";
  - `Tests/GAS/EditMode/Examples/GasManualExamples.cs` (criado no EX3): os exemplos das duas páginas;
  - `Tests/Core/EditMode/ManualCodeExamplesTests.cs`: saem a lista de pendências e o código que a lê. A guarda passa a valer para o manual inteiro, sem exceção.
- Blocos de `dano-escudo-e-morte.md`, todos viram código:
  - o `ApplyAttributeChange` sem contexto e com contexto e efeito;
  - o ouvinte do `OnAttributeChangeApplied`;
  - o `TryGetActiveChangeSource`;
  - o ouvinte do `OnAttributeDepleted`;
  - o processador `CortaPelaMetade`;
  - o trecho do `queue.Redirect`, que é corpo de método e segue a regra do trecho de membro;
  - os três exemplos do DM8, contados quando o EX6 entrar.
  
  Os valores que o texto da página dá (o pedido, o final, o aplicado, o dano cortado pela metade, a parte redirecionada) viram asserções.
- Bloco de `migracao-2-para-3.md`: o `asc.OnAbilityActivated += spec => ...`, com um `[Test]` que ativa uma habilidade e confere que o ouvinte recebeu o `AbilitySpec` dela.
- Risco do EditMode: os testes de hoje do pipeline de dano e da morte são todos PlayMode (`Tests/GAS/PlayMode/AttributePipelineTests.cs`, `DeathSignalTests.cs`, `ShieldTests.cs` e outros), e a X4 limita os exemplos ao EditMode. O EX3 passa pela mesma questão antes, com o `gas.md`, e a forma que ele usar para montar o `AbilitySystemComponent` no EditMode vale aqui. Se o EX3 não tiver resolvido isso e um exemplo de dano ou da ativação depender do `Awake` do ASC ou do Play, a Fábrica para e devolve ao Arquiteto antes de escrever. Mudar a X4 ou o pacote é decisão do gate, não do ticket.
- Testes red:
  - com as duas páginas fora da lista de pendências, o `NoHandWrittenCSharpBlocks` falha em `modulos/dano-escudo-e-morte.md` e em `regras/migracao-2-para-3.md`;
  - manual, no relatório: com a lista apagada, um bloco C# de rascunho escrito à mão em qualquer página do manual (por exemplo, `inicio/instalacao.md`) faz o `NoHandWrittenCSharpBlocks` falhar com o caminho da página. O rascunho sai antes do diff;
  - manual, no relatório: o `SnippetsPointToExistingSections` lê a linha `--8<--` recuada do item de lista da migração. Se a guarda do EX1 não ler linha recuada, a correção entra no `ManualCodeExamplesTests.cs`, que já está no escopo.
- Critério de pronto:
  - os testes de `GasManualExamples` e o `ManualCodeExamplesTests` passam;
  - a guarda não tem mais lista de pendências nem código que a leia;
  - nenhuma página de `Documentation~/manual/` tem bloco ` ```csharp `, ` ```cs ` ou ` ```c# ` com linha que não seja `--8<--`. Com isso fecha o CA10 do PRD;
  - o `mkdocs build --strict` passa. No HTML da migração, o bloco aparece dentro do item de lista, com o recuo certo. A Fábrica confere isso na versão travada do `pymdownx.snippets`. Se o recuo não sair certo, o bloco passa para o nível da página, e o texto em volta continua dizendo o mesmo;
  - o texto em volta de cada bloco continua dizendo o mesmo, e o relatório lista as correções onde o bloco antigo divergia da API;
  - o diff não muda `.cs` de `Runtime/` ou `Editor/`, `.asmdef`, o `CHANGELOG.md` nem a saída gerada.
- Tamanho estimado: ~300 linhas. A conta: ~50 de montagem do ASC e dos atributos, compartilhada pelos exemplos de dano; ~110 dos 5 exemplos curtos; ~30 do processador e do `Redirect`; ~75 dos três exemplos do DM8; ~20 da migração; ~15 da remoção da lista. São 4 arquivos, e as duas páginas contam só nos arquivos. Recontar na entrada, porque o DM8 e o EX3 mudam a base da conta. Se passar de ~500 linhas, a Fábrica para e devolve, e o Arquiteto divide por página. Nesse caso, o ticket da página que entrar por último apaga a lista.

### Lote 8 e pares do WIP 2

O lote 8 junta tickets de três Specs. A ordem e os pares estão aqui e nas emendas de 2026-11-02 da `gas-dano-escudo-e-morte.md` e da `gas-evento-acoes-alvos.md`.

| Ordem | Ticket | Spec | Depende de | Par com Unity (WIP 2) |
|---|---|---|---|---|
| 1 | DM7 | dano | DM5, AR6, AR7 | nenhum |
| 2 | EX1 | esta | RC5, XR2 | nenhum |
| 3 | DM8 | dano | DM7, AV1, AR6 | EX2 |
| 3 | EX2 | esta | EX1, DM7 | DM8 |
| 4 | EV1 | evento | DM8, AV9 | EX4 |
| 4 | EX4 | esta | EX2 | EV1 |
| 5 | EV2 | evento | EV1 | EX5 |
| 5 | EX5 | esta | EX4 | EV2 |
| 6 | EV3 | evento | EV2, VN9, RP-07, AV5 | nenhum |
| 7 | EV4a | evento | EV3 | nenhum |

Por que cada par cabe nas condições da emenda de 2026-11-01 da 0003:
- Os DM e os EV mudam `.cs` do pacote, escrevem o `CHANGELOG.md` e regeneram o catálogo, `api/reference/**` e `api/reference.sha256`. O EX2, o EX4 e o EX5 só mudam testes, que ficam fora da impressão digital (D2: assemblies `.Tests` não são do pacote), páginas do manual e a guarda dos exemplos. Eles não escrevem o `CHANGELOG.md` nem regeneram nada.
- DM8∥EX2: o DM8 escreve `Editor/GAS/AttributeDefinitionEditor.cs`, `Editor/GAS/DamagePreviewModel.cs`, `Tests/GAS/EditMode/DamagePreviewModelTests.cs`, a página de dano, `gas.md`, `exemplos/sandbox.md`, o `CHANGELOG.md` e a saída gerada. O EX2 escreve `inicio/inspector.md`, `ferramentas/validador.md`, `Tests/Core/EditMode/Examples/CoreManualExamples.cs` e `ManualCodeExamplesTests.cs`. A pasta `Examples/` do Core já existe desde o EX1. O DM8 depende do DM7, e o EX2 do EX1 e do DM7, que vêm antes.
- EV1∥EX4: o EV1 escreve os quatro arquivos novos de `Runtime/GAS/Actions/` (com o `.meta` da pasta), `AbilitySystemComponent.cs`, `Tests/GAS/PlayMode/ActionRunnerTests.cs`, `gas.md`, o `CHANGELOG.md` e a saída gerada. O EX4 escreve `modulos/eventos.md`, `modulos/tags.md`, os dois arquivos de exemplo novos (com o `.meta` das pastas `Examples/` dos Eventos e das Tags) e a guarda.
- EV2∥EX5: o EV2 escreve `Runtime/GAS/Actions/ActionEventAttribute.cs`, `ActionContext.cs`, `GameplayEffect.cs`, `ActiveEffectInstance.cs`, `AbilitySystemComponent.cs`, `CommonEffectActions.cs`, `SpawnActionsTests.cs`, `EffectEventActionsTests.cs`, o `CHANGELOG.md` e a saída gerada. O EX5 escreve `modulos/pooling.md`, `modulos/tempo.md`, os dois arquivos de exemplo novos (com o `.meta` das pastas) e a guarda.
- Nos três pares, a página que o EX tira da lista de pendências não é escrita pelo DM ou pelo EV do par. Depois dos dois cherry-picks, a guarda continua passando.
- O EX1 não forma par: ele escreve o `CHANGELOG.md`, como todos os DM e EV do lote.
- O EV3 e o EV4a não formam par. Os dois mudam `.cs` do pacote, e não sobra EX no lote 8. O EV3 também tem a exceção de tamanho revisada pela lista de arquivos, e uma suíte do par que falhasse misturaria um diff de ~23 arquivos com o de outro ticket.
- O EX3 e o EX6 ficam para o lote 9 (L1). Os pares do lote 9 são marcados quando o lote for montado.

#### Revisão de 2026-11-02: EV1 dividido

O usuário dividiu o EV1 em EV1a e EV1b em 2026-11-02 (emenda da mesma data da `gas-evento-acoes-alvos.md`). A ordem e o par abaixo substituem as linhas 4 a 7 da tabela acima. Aprovados no gate parcial de 2026-11-02, com as perguntas E1 e E2 daquela Spec respondidas como propostas. O EV1a é a issue #174, renomeada, e o EV1b é a #307.

| Ordem | Ticket | Spec | Depende de | Par com Unity (WIP 2) |
|---|---|---|---|---|
| 4 | EX4 | esta | EX2 | nenhum |
| 5 | EV1a | evento | DM8, AV9 | EX5 |
| 5 | EX5 | esta | EX4 | EV1a |
| 6 | EV1b | evento | EV1a | nenhum |
| 7 | EV2 | evento | EV1b | nenhum |
| 8 | EV3 | evento | EV2, VN9, RP-07, AV5 | nenhum |

- O EV4a sai do lote 8 e abre o lote 9, antes do EV4b, para o lote continuar com 10 tickets.
- EV1a∥EX5: o EV1a escreve os quatro arquivos novos de `Runtime/GAS/Actions/` (com o `.meta` da pasta), `AbilitySystemComponent.cs`, `Tests/GAS/PlayMode/ActionRunnerTests.cs`, `gas.md`, o `CHANGELOG.md` e a saída gerada. O EX5 escreve `modulos/pooling.md`, `modulos/tempo.md`, os dois arquivos de exemplo novos (com o `.meta` das pastas) e a guarda. Não há arquivo em comum nem dependência entre os dois, e o `gas.md` continua na lista de pendências.
- O EX4 fica sem par. O EV2 perde o par com o EX5, e o EV1b, o EV2 e o EV3 seguem o WIP 1, porque os três mudam `.cs` do pacote.
- Na tabela "Tickets da emenda", a coluna de par passa a ser "nenhum (lote 8)" para o EX4 e "EV1a (lote 8)" para o EX5.

#### Revisão de 2026-11-02: EV3 só com a troca, EV3c e EV3d

Decisão do usuário de 2026-11-02 (emenda da mesma data na `gas-evento-acoes-alvos.md` e na ADR 0003). O lote 8 não muda e fecha com o EV3 como 10º ticket. O EV3 passa a ter 35 arquivos na lista da exceção, no lugar dos ~23 citados acima. O lote 9 começa por XR3 → EV3c → EV3d → EV4a → EV4b. Os quatro EV seguem o WIP 1, porque mudam `.cs` do pacote. O XR3 continua o primeiro ticket do lote 9 (R1).

### MN8

O MN8 existe só como linha do esboço da etapa 2 da `manual-reorganizacao.md` ("MN8 a MN11: constantes por tipo"). Ele não tem escopo detalhado, testes red nem issue; as issues do B-035 são só as do MN1 a MN3 (#283 a #285). Ele não entra no lote 8. Antes de entrar na fila, o MN8 a MN11 (e o MN12 a MN17) precisam de uma emenda da `manual-reorganizacao.md` com os tickets detalhados, gate parcial e issues. Pela ordem, o MN8 entra depois do EX6. A numeração "entre o Lote 7 e o Lote 8" do esboço é a do plano de 2026-11-01, em que o Lote 7 era DM7, DM8 e EV1 a EV7. O lote do catálogo tomou o número 7, e aquele lote ficou dividido entre o lote 8 e o lote 9.

### Perguntas do gate parcial do lote 8

Respostas do usuário (2026-11-02), todas como propostas:
- L1: o EX3 vai para depois do EV7, no lote 9.
- L2: entra o EX6, com issue nova, para a página de dano e a de migração. Ele vem depois do EX3 e do DM8 e apaga a lista de pendências.
- L3: o lote 8 é DM7 → EX1 → DM8∥EX2 → EV1∥EX4 → EV2∥EX5 → EV3 → EV4a. O EV4b abre o lote 9.

A emenda de 2026-11-02 da 0003 ("composição do lote 8 aprovada") registra a composição do lote. As perguntas, como foram ao gate:

- L1. Lugar do EX3 (`gas.md`).
  - Proposta: depois do EV7, no lote 9, junto com o EX6 e os EV4b a EV7. Os EV1 a EV7 escrevem o `gas.md` com a guarda ainda tolerando bloco à mão, e o EX3 migra tudo de uma vez com a API nova.
  - Alternativa: no lote 8, como estava, depois do EX2. Todo EV que escreve bloco C# no `gas.md` põe o arquivo de exemplos do GAS no escopo, e os exemplos do EX3 que mostram ações são reescritos no EV3. O EV4a passa de 10 arquivos e precisa ser dividido, e a lista de arquivos da exceção do EV3 ganha mais um.
- L2. Ticket novo para a página de dano e a de migração.
  - Proposta: o EX6, com issue nova, depois do EX3 e do DM8; ele apaga a lista de pendências.
  - Alternativa: as duas páginas ficam na lista de pendências até a reescrita delas (MN25 para a migração; a de dano não tem ticket de reescrita, porque nasceu no molde), e a lista não é apagada no B-019g. O CA10 do PRD (nenhum bloco C# escrito à mão) fica aberto até lá.
- L3. Composição do lote 8.
  - Proposta: DM7, EX1, DM8∥EX2, EV1∥EX4, EV2∥EX5, EV3 e EV4a. O DM7 e o DM8 estão abertos (#172 e #173 pelo mapa da fila) e o EV1 depende do DM8 pela fila da `gas-evento-acoes-alvos.md`, porque os dois escrevem `AbilitySystemComponent.cs`, `GameplayEffect.cs` e `gas.md`. O EV4b sai porque o lote passaria de 10 tickets; ele abre o lote 9.
  - Alternativa: a composição sugerida (EX1 a EX5, EV1, EV2, EV3, EV4a e EV4b), com o EV1 antes do DM7 e do DM8, que vão para o lote 9. Isso quebra a ordem "depois do DM8" aprovada no gate do Lote C, deixa o B-010 aberto por mais um lote e esbarra no L1, se o EX3 ficar no lote 8.

## Emenda de 2026-11-02: XR3, pasta dos testes dentro do projeto host

Estado: proposta, via rápida (ADR 0002), backlog B-037, issue #308. A R1 foi aprovada pelo usuário em 2026-11-02, como proposta: o XR3 abre o lote 9, antes do EV4a, em WIP 1 (resposta em "Perguntas para o usuário (XR3)"). Vem do achado do Examinador na revisão do EX5, no par EV1a∥EX5 do experimento de WIP 2. A ADR 0003 recebe uma emenda da mesma data: o WIP 2 depende deste ticket. O D8, o D9 e os outros tickets não mudam.

Cabe na via rápida porque muda só testes do pacote. Não há ADR nova, API pública, formato serializado, `CHANGELOG.md` nem saída gerada: os assemblies de teste ficam fora da impressão digital (D2).

### Contexto do código

- `Tests/Core/EditMode/Documentation/ReferenceGeneratorTests.cs:15` (XR2): a raiz falsa do repositório é `%TEMP%\spellbook-reference-generator`, um caminho fixo. O `SetUp` (`:24-29`) e o `TearDown` (`:32-35`) apagam a pasta inteira (`DeleteRoot`, `:135-141`).
- `Tests/Core/EditMode/Documentation/UnityXrefMapTests.cs:14` (XR1): o mesmo padrão, em `%TEMP%\spellbook-unity-xrefmap`, com `SetUp` e `TearDown` que apagam a pasta (`:16-27`, `:261-267`).
- `Tests/Core/EditMode/Documentation/DocfxConfigTests.cs:14` (RC3): usa `%TEMP%\spellbook-docfx-config` como raiz falsa, mas só para montar caminhos. Nenhum teste dele escreve ou apaga nada ali, então ele não entra na disputa.
- O `%TEMP%` é do usuário do Windows, e as duas Unity do WIP 2 rodam com o mesmo usuário. Pela emenda de 2026-11-01 da 0003, cada worktree tem pasta e `Library/` próprias, mas um caminho fora do projeto é o mesmo para as duas. O `SetUp` de uma apaga a pasta enquanto a outra está entre o `Generate` e a leitura. Por isso o `Generate_Twice_SameBytes` e o `Generate_WritesUnityXrefMap` falham de vez em quando, e os testes do `UnityXrefMapTests` que escrevem YAML correm o mesmo risco.
- Os outros testes do pacote que escrevem em disco usam pastas dentro do projeto (`Assets/__SpellBookTestTemp` em `Tests/Events/EditMode/FindReferencesTests.cs:19` e `Tests/Core/EditMode/Inspector/MissingReferenceTypesTests.cs:97`; `Assets/__ScalableMigrationTests` em `Tests/GAS/EditMode/GasAssetMigrationTests.cs:16` e `ScalableMigrationTests.cs:20`). Cada worktree tem o próprio `Assets/`, então eles não disputam nada e ficam fora do escopo. Nenhum teste usa `Library/` nem caminho absoluto para escrever.
- `Tests/Core/EditMode/Structure/PackageFiles.cs` lê os arquivos do pacote pelo caminho real. O `NoOdinTests.cs` mostra a forma de uma guarda que varre as fontes de `Tests/` e monta os termos por partes para não casar consigo mesma.
- `Editor/Core/Documentation/ReferenceGenerator.cs:96-99` acha a raiz do projeto host por `Application.dataPath`. O `.gitignore:3` já ignora `/[Tt]emp/`.

### Design

- Uma classe interna de teste, `TestScratch`, em `Tests/Core/EditMode/Structure/TestScratch.cs`, dá a pasta de trabalho de cada classe de teste dentro do `Temp/` do projeto host aberto:
  - `string For(string name)`: `<host>/Temp/SpellBookTests/<name>`, com `<host>` = `Path.GetFullPath(Path.Combine(Application.dataPath, ".."))`;
  - `string For(string hostRoot, string name)`: a mesma regra com a raiz dada. O teste da disputa usa esta forma para simular outro host no mesmo processo;
  - `string Recreate(string path)`: apaga a pasta, se existir, cria de novo e devolve o caminho;
  - `void Delete(string path)`: apaga a pasta, se existir;
  - `name` só aceita letras, dígitos e `-`. Nome vazio, com separador ou com `..` lança `ArgumentException`. `Recreate` e `Delete` lançam `InvalidOperationException` para um caminho que não esteja dentro de `<raiz>/Temp/SpellBookTests/`, porque apagam a pasta inteira.
- O nome é fixo por classe de teste, e não aleatório. Duas Unity abertas são dois projetos host e nunca chegam à mesma pasta. Dentro de uma Unity, o NUnit roda os testes um de cada vez, e o `SetUp` recria a pasta. Isso mantém os testes determinísticos, sem GUID nem número de processo no caminho.
- `ReferenceGeneratorTests` usa `TestScratch.For("reference-generator")`, `UnityXrefMapTests` usa `TestScratch.For("unity-xrefmap")` e `DocfxConfigTests` usa `TestScratch.For("docfx-config")`. O `SetUp` chama `Recreate` e o `TearDown` chama `Delete`; o `DeleteRoot` de cada classe sai. O `DocfxConfigTests` continua sem escrever nada e só troca a raiz falsa, para a guarda abaixo valer sem exceção.
- O resto dos testes das três classes não muda. O `ReferenceFingerprint.Compute(Root)` continua achando as fontes reais, porque o caminho relativo dentro do mesmo disco ainda resolve.
- A guarda `NoTestUsesSystemTempFolders` varre `Tests/**/*.cs` com o `PackageFiles` e falha em todo arquivo que chame `Path.GetTempPath`, `Path.GetTempFileName`, `Environment.GetFolderPath`, `Application.temporaryCachePath` ou `Application.persistentDataPath`. A mensagem lista os arquivos. Os termos são montados por partes, como no `NoOdinTests`. Literal de caminho absoluto que nunca é escrito (as raízes falsas do `DocfxConfigTests.cs:56-61`) não é procurado.

### Quebras de compatibilidade

Nenhuma. Só mudam testes do pacote.

### XR3 — `test(core): pasta temporária dos testes dentro do projeto host`

- Escopo:
  - `Tests/Core/EditMode/Structure/TestScratch.cs` (novo);
  - `Tests/Core/EditMode/Structure/TestScratchTests.cs` (novo): os testes do `TestScratch` e a guarda;
  - `Tests/Core/EditMode/Documentation/ReferenceGeneratorTests.cs`;
  - `Tests/Core/EditMode/Documentation/UnityXrefMapTests.cs`;
  - `Tests/Core/EditMode/Documentation/DocfxConfigTests.cs`: só a raiz falsa.
- Testes red:
  - `NoTestUsesSystemTempFolders` falha hoje e lista `ReferenceGeneratorTests.cs`, `UnityXrefMapTests.cs` e `DocfxConfigTests.cs`;
  - `Recreate_OtherHostSameName_KeepsThisHostFiles` reproduz a disputa sem duas Unity e sem tempo real. Ele cria `For(hostA, "disputa")` com `Recreate` e escreve um arquivo nela. Depois faz o que o `SetUp` da outra Unity faria, `Recreate(For(hostB, "disputa"))`, e confere que o arquivo do host A continua lá. `hostA` é a raiz do projeto aberto, e `hostB` é uma pasta dentro do `Temp/SpellBookTests/` dele, que faz o papel do outro worktree. Com a pasta no `%TEMP%`, os dois hosts caíam no mesmo caminho e o arquivo sumia. O `TearDown` apaga as duas;
  - `For_SameNameInTwoHosts_DifferentFolders`: `For("C:/wt-a", "x")` é diferente de `For("C:/wt-b", "x")`, e cada um fica dentro de `<raiz>/Temp/SpellBookTests/`;
  - `For_InvalidName_Throws`, com `""`, `".."` e `"a/b"`;
  - `Recreate_PathOutsideScratch_Throws`: um caminho fora de `Temp/SpellBookTests/` lança exceção e nada é apagado.
  - Os testes do `TestScratch` falham antes da implementação porque a classe não existe. O red que mostra o bug no código de hoje é o da guarda.
- Critério de pronto:
  - os testes do ticket passam, com o filtro da emenda de 2026-11-01 da 0003: `\.TestScratchTests\.;\.ReferenceGeneratorTests\.;\.UnityXrefMapTests\.;\.DocfxConfigTests\.`, no assembly `SpellBook.Core.Tests.EditMode`;
  - nenhum arquivo de `Tests/` chama as APIs da guarda, e o `%TEMP%` não ganha `spellbook-*` depois da suíte;
  - depois da suíte, `Temp/SpellBookTests/` do projeto não tem sobra das três classes;
  - o diff não muda `Runtime/`, `Editor/`, `.asmdef`, `CHANGELOG.md` nem a saída gerada, e a guarda `CommittedFingerprint_MatchesSources` continua passando sem regenerar nada.
- Unity: sim. Depende do XR2 (lote 7, PR #306). Não forma par de WIP 2, porque os próximos pares dependem dele.
- Issue: #308. Lugar na fila: primeiro ticket do lote 9, antes do EV4a (R1, aprovada em 2026-11-02).
- Tamanho estimado: ~200 linhas (~50 do `TestScratch`, ~110 dos testes dele e da guarda, ~40 nas três classes migradas); 5 arquivos. O `.meta` dos dois arquivos novos não conta.

### Perguntas para o usuário (XR3)

- R1. Lugar do XR3 na fila.
  - Proposta: primeiro ticket do lote 9, antes do EV4a, em WIP 1. O lote 8 fica com 10 tickets. Os tickets do lote 8 depois do EX5 (EV1b, EV2 e EV3) já seguem o WIP 1, então nenhum par espera por ele.
  - Alternativa: entrar no lote 8 logo depois do EX5, como 11º ticket do lote.
  - **Resposta do usuário (2026-11-02): aprovada como proposta.** O XR3 (issue #308) abre o lote 9, antes do EV4a. O lote 8 continua com 10 tickets.
- R2. Revisão do EX5.
  - Proposta: o Examinador roda de novo a suíte completa no worktree do EX5 sem outra Unity aberta. O EV1a já está na branch do lote (`7f564aa`), então essa rodada não espera ninguém. As falhas do `Generate_Twice_SameBytes` e do `Generate_WritesUnityXrefMap` com duas Unity abertas não contam como achado do EX5, que não toca esses arquivos. A suíte do par, no checkout do lote, também roda com uma Unity só, como a emenda de 2026-11-01 da 0003 já prevê.
  - Alternativa: o EX5 espera o XR3 entrar na branch do lote e só então termina a revisão. Isso põe o XR3 no lote 8, como na alternativa da R1.
