# Guia do manual

Documento interno de quem escreve ou revisa o manual público em `Documentation~/manual/`. Ele fica fora de `manual/`, então o site não o publica. Vale pela emenda de 2026-11-01 da ADR 0007 e pelo D1 ao D6 da Spec `spec/manual-reorganizacao.md`. As regras de processo (quem escreve no manual, quando e como o ticket conta) estão no `CLAUDE.md`, seção "Time de desenvolvimento". Este guia só diz como escrever e como conferir.

## 1. Molde da página de sistema

A página de sistema tem nove seções, nesta ordem. As seções 1, 2 e 4 são obrigatórias. As outras entram quando há o que dizer, e a ordem não muda.

   ```markdown
   # Nome do sistema {#id}
   (1, obrigatória) Uma frase do que a página ensina. Um parágrafo: que problema o sistema resolve e quando usar.
       Linha do módulo: assembly `SpellBook.X`, depende de `SpellBook.Core`.

   ## Como funciona {#como-funciona}
   (2, obrigatória) As peças e como se ligam, na linguagem de quem monta a cena. Sem método, sem alocação, sem ordem interna.
       Diagrama em Mermaid quando ajudar, com legenda de uma frase.

   ## Uso rápido {#uso-rapido}
   (3) Passos numerados no imperativo, com o menu exato (**Create > SpellBook > ...**) e valores concretos.
       Último passo: aperte Play e veja o que acontece.

   ## Nome do componente ou asset {#nometipo}
   (4, obrigatória) O que é, em uma ou duas frases.
   ### Campos do Inspector {#nometipo-campos}
   - `Campo`: uma frase sobre o efeito no jogo.
   - `Modo`: escolha entre
       - `Opção A`: efeito.
       - `Opção B`: efeito.
   (repita para cada componente ou asset)

   ## Prontos para usar {#prontos}
   (5) Os tipos que vêm no pacote, com link para o catálogo gerado. Até o catálogo existir (B-019g), a lista que já está no manual.

   ## Pelo código {#codigo}
   (6) Dois ou três trechos curtos (ler, aplicar, ouvir). Desempenho e ordem interna ficam aqui. Link para a referência da API.

   ## Criar o seu {#criar-o-seu}
   (7) Quando estender vale a pena e um modelo de classe comentado.

   ## Limitações e armadilhas {#armadilhas}
   (8) Regra, exemplo errado, exemplo certo, saída quando a regra não cabe. Use `!!! note` e `!!! warning`.

   ## Veja também {#veja-tambem}
   (9) Receita, guia, exemplo, ferramenta de depuração e validação ligados ao sistema.
   ```

Os marcadores `(1)` a `(9)` e o texto entre parênteses explicam o molde. Não os copie para a página.

Exemplo curto de bloco "Campos do Inspector", com campos reais de `AttributeDefinition`:

   ```markdown
   ## AttributeDefinition {#attributedefinition}
   Asset que descreve um atributo, como vida ou mana. Crie em **Create > GAS > Attribute Definition**.

   ### Campos do Inspector {#attributedefinition-campos}
   - `Display Name`: nome que aparece para o jogador em barras e textos.
   - `Icon`: ícone usado nas interfaces que mostram o atributo.
   ```

Exemplo curto de diagrama com legenda, com tipos reais do pacote. A legenda é uma linha logo abaixo do bloco:

   ````markdown
   ```mermaid
   flowchart LR
       GameplayEffect --> AttributeDefinition
   ```
   *O `GameplayEffect` muda o valor de um atributo descrito por um `AttributeDefinition`.*
   ````

## 2. Moldes curtos para os outros tipos de página

- Ferramenta: o que é, como abrir, como ler a janela, exemplo.
- Guia: objetivo em uma frase, plano em passos, uma seção por peça, caso de borda.
- Exemplo: o que mostra, como importar, passo a passo por cena, onde ficam os arquivos.
- Entrada: para quem é, o que fazer, próximo passo.

## 3. Regras de escrita

- Leia `C:/Users/gabri/.claude/skills/humanizer/SKILL.md` e aplique as regras dele a todo texto novo, ao escrever e ao revisar. A regra é ler o arquivo e aplicá-lo ao texto.
- Escreva em segunda pessoa, com uma ideia por parágrafo e verbo no imperativo nos passos.
- Escreva o nome de campo como aparece no Inspector, em código.
- Ponha o caminho de menu em negrito.
- Não ponha sigla em título. Fora do título, a sigla só aparece depois do nome por extenso, na mesma página. "GAS" é a exceção: pode aparecer depois de "sistema de habilidades" por extenso, e no nome das ferramentas que o usam (GAS Debugger). Nome de tipo em código (`AbilitySystemComponent`) não é sigla.
- Não use imagem, captura de tela nem gif. O diagrama é Mermaid, tem legenda de uma frase, diz que parte do pacote representa e usa os nomes do Inspector.
- No relatório do ticket, declare: "texto do manual revisado pelas regras da humanizer (SKILL.md)", e liste as páginas.

## 4. Âncoras e constante por tipo

1. Todo título tem âncora `{#id}`.
2. A seção de um componente ou asset usa o nome do tipo em minúsculas como âncora (`{#attributedefinition}`). Uma reescrita não muda essa âncora.
3. Mover uma seção para outra página pede, no mesmo ticket: atualizar `DocUrls`, atualizar os links das outras páginas e rodar o `ManualLinksTests`. Não há redirecionamento nem página de remendo no endereço antigo.
4. O `[HelpURL]` usa uma constante de `DocUrls` que já tem página e âncora, e não concatena âncora no atributo:

   ```csharp
   public const string AttributeDefinition = GasAttributes + "#attributedefinition";

   [HelpURL(DocUrls.AttributeDefinition)]
   ```

   Os testes que conferem a URL usam a mesma constante. Código novo já nasce assim, e os `[HelpURL]` antigos migram por módulo, antes da divisão da página daquele módulo.
5. Nenhum `[HelpURL]` aponta para página gerada no build, porque o `ManualLinksTests` lê o disco do repositório.

## 5. Páginas antigas durante a transição

Até o módulo ser reescrito (etapa 2 da Spec), quem muda uma página antiga:

- segue as regras de escrita no trecho que escreve;
- põe o componente ou asset novo num bloco "Campos do Inspector";
- não reestrutura o resto da página.

Página nova nasce no molde.

## 6. Mapa de páginas e `nav`

O `nav` do `mkdocs.yml` tem sete grupos, nesta ordem: Começar; Sistemas de habilidade; Sistemas de apoio; Editor e ferramentas; Exemplos; Guias; Referência. Grupo sem página fica fora do `nav`. O rótulo de cada página é o título `#` dela.

Toda página nova entra no `nav` no mesmo ticket que a cria. Sem isso, o `mkdocs build --strict` falha.

| Grupo | Páginas que já existem ou que as Specs aprovadas criam |
|---|---|
| Começar | `index.md`, `inicio/instalacao.md`; na etapa 3: primeira habilidade, requisitos mínimos da cena, como os módulos se ligam |
| Sistemas de habilidade | `modulos/gas.md` (visão geral); da divisão (etapa 2): atributos, efeitos, habilidades e ações, ativação, alvos e projéteis; `modulos/dano-escudo-e-morte.md`, `modulos/thinkers-e-auras.md`, `modulos/descricao.md` |
| Sistemas de apoio | `modulos/eventos.md` e as páginas que saem dela (etapa 2); `modulos/tags.md`, `tempo.md`, `pooling.md`, `times.md`, `cues.md`, `texto.md`; `ui/*.md` |
| Editor e ferramentas | `inicio/inspector.md`, `ferramentas/*.md` |
| Exemplos | `exemplos/sandbox.md`; receitas (B-019f) |
| Guias | `guias/*.md` (etapa 3) |
| Referência | catálogo gerado (B-019g), referência da API (B-019g), `inicio/conceitos.md`, `regras/migracao-2-para-3.md`, perguntas frequentes (etapa 3) |

Regras do mapa:

- As páginas novas dos sistemas ficam soltas em `modulos/` (`modulos/atributos.md`, e não `modulos/gas/atributos.md`), porque o `ManualLinksTests` não acha `pasta/index.md`.
- O catálogo e a referência da API são gerados na máquina e commitados, pela 0040 revista. A referência da API fica fora da pasta do manual e entra no `nav` como link absoluto.

## 7. Conferências por busca

Quem implementa e quem revisa rodam estas conferências a partir da raiz do repositório:

- Imagem: `grep -rnE '!\[|<img' Documentation~/manual` não acha nada.
- Sigla em título: `grep -rnE '^#{1,6} .*\b[A-Z]{2,}\b' Documentation~/manual` só acha nomes de tipo em código, GAS (pela exceção da parte 3) e nomes de produto (Unity, JSON).
- Diagrama sem legenda: todo bloco ```` ```mermaid ```` tem uma linha de legenda logo abaixo do fechamento.
- Build: de dentro de `Documentation~/`, `mkdocs build --strict -f mkdocs.yml`, com o site gerado fora do repositório (`-d` para uma pasta temporária). De outra pasta, as inclusões de exemplo não acham o arquivo e o build falha.
- Diagrama novo: abra a página no site gerado, porque o `--strict` confere o Markdown e o navegador é que desenha o Mermaid.

## 8. Catálogo e referência da API gerados

O catálogo (`manual/referencia/catalogo.md`), os YAML da API (`api/reference/`), o mapa da Unity (`api/unity.xrefmap.yml`) e a impressão digital (`api/reference.sha256`) saem do código, pela Unity, e vão para o repositório (0040). Não edite esses arquivos à mão.

Na API, os tipos da Unity levam à documentação da Unity pelo mapa, que a geração monta a partir dos YAML (0041). Os módulos do motor apontam para o ScriptReference mais recente, sem versão na URL. Os pacotes `com.unity.*` apontam para a versão instalada no projeto, então quem atualiza um desses pacotes precisa gerar a referência de novo. Os tipos do .NET levam ao learn.microsoft.com pelo `href` que o `docfx metadata` grava nos YAML. O `docfx.json` não declara mapa do .NET, e o build não usa a rede.

As guardas que comparam o código com o manual ou com a referência commitada ficam na categoria `Documentacao` do NUnit: o `ReferenceUpToDateTests`, o `ManualCodeExamplesTests` e três testes do `ManualLinksTests`. Quando cada suíte roda, e o que um ticket de código deixa para o passe de documentação, está no `CLAUDE.md`, seção "Time de desenvolvimento".

No passe de documentação:

1. Rode **SpellBook > Documentação > Gerar referência** na Unity, com o SDK do .NET instalado. Em batchmode, a mesma geração roda com `-executeMethod SpellBook.Core.Editor.Documentation.ReferenceGenerator.GenerateFromCommandLine` e sai com 0 se deu certo ou 1 se falhou.
2. Commite o catálogo, a pasta `api/reference/`, o `api/unity.xrefmap.yml` e o `api/reference.sha256` no mesmo diff. Sem isso, o `ReferenceUpToDateTests` falha.
3. Rode a suíte com `-testCategory "Documentacao"` e depois sem filtro. As duas rodadas precisam passar.

Para ver o site sem a Unity, rode dentro de `Documentation~/`:

1. `mkdocs build --strict -f mkdocs.yml`, no venv do manual;
2. `dotnet tool restore` e `dotnet docfx build api/docfx.json --warningsAsErrors`. A API sai em `api/_site/`, que o git ignora, com a página de cada tipo em `<uid>.html`, o mesmo caminho do link do catálogo.

A Action `docs` roda nos pull requests que mexem no manual, na API, na configuração do DocFX ou no próprio workflow. Um PR que muda só código ou exemplos não a dispara; no passe de documentação, rode-a à mão pelo `workflow_dispatch` se o PR não mexer nesses caminhos. Ela monta o manual e a API como na publicação, mas não publica. Depois, confere cada link `api/...html` do catálogo no site montado, porque o MkDocs não valida link absoluto. Se um link aponta para um tipo sem página, o build falha e mostra o caminho. Isso costuma acontecer quando o código muda e a geração não roda de novo.

A versão do DocFX fica travada em `.config/dotnet-tools.json`. Se você trocar essa versão, a impressão digital muda e a geração precisa rodar de novo.

## 9. Exemplos de código

Todo bloco C# do manual sai de um arquivo de exemplos que compila e roda com os testes do pacote (Spec `referencia-catalogo-e-api.md`, D9; ADR 0041). A página não tem código escrito à mão: o bloco só tem linhas `--8<--`, que o `pymdownx.snippets` troca pelo trecho na hora do build.

O arquivo de exemplos de cada módulo é `Packages/com.spellbook/Tests/<Módulo>/EditMode/Examples/<Módulo>ManualExamples.cs`, no assembly de testes que o módulo já tem. O do Core é o `CoreManualExamples.cs`, com os exemplos de `inicio/conceitos.md`. A classe tem o nome do arquivo e fica no namespace `<namespace raiz dos testes>.Examples`, como `SpellBook.Core.Tests.Examples`. Só a página `exemplos/sandbox.md` pode incluir de `Packages/com.spellbook/Samples~/Sandbox/Scripts/`.

No arquivo, marque o trecho com dois comentários. O nome da seção tem letras minúsculas, números, `-` e `_`, e é único no arquivo:

   ```csharp
   [Test]
   public void MathTools_Map_DistanceToVolume()
   {
       float distance = 5f;

       // --8<-- [start:mathtools-map]
       float volume = MathTools.Map(distance, 0f, 20f, 1f, 0f);
       // --8<-- [end:mathtools-map]

       Assert.That(volume, Is.EqualTo(0.75f).Within(1e-5f));
   }
   ```

Na página, inclua a seção pelo caminho a partir da raiz do repositório:

   ````markdown
   ```csharp
   --8<-- "Packages/com.spellbook/Tests/Core/EditMode/Examples/CoreManualExamples.cs:mathtools-map"
   ```
   ````

Regras do arquivo de exemplos:

- O trecho que é chamada fica dentro de um `[Test]`, e o teste confere o que o texto da página diz do resultado. A preparação (as variáveis que o trecho usa) e as asserções ficam fora da seção.
- Uma declaração de tipo fica no namespace, fora da classe de testes, entre os marcadores. Um `[Test]` do mesmo arquivo usa o tipo e confere o que a página diz dele. Tipo de apoio que a página só cita, como o contexto `MyContext`, fica fora de qualquer seção.
- O `using` que a página mostra fica numa seção própria no topo do arquivo, com a linha em branco que o separa do resto dentro da seção. O bloco da página tem então duas linhas `--8<--`, uma para o `using` e outra para o trecho, nessa ordem.
- O recuo sai sozinho: o `mkdocs.yml` liga o `dedent_subsections`, que tira o recuo comum das linhas da seção. Um membro de classe (um campo, um método) ou uma linha dentro de um método aparece na página a partir da coluna zero.
- Um bloco pode ter mais de uma linha `--8<--`, uma por seção, quando junta partes de lugares diferentes do arquivo.

O `mkdocs build --strict` falha se o arquivo ou a seção não existe, pelo `check_paths`. O `ManualCodeExamplesTests` confere, na Unity:

- `NoHandWrittenCSharpBlocks`: nenhum bloco ` ```csharp `, ` ```cs ` ou ` ```c# ` tem linha que não seja `--8<--`, fora as páginas da lista de pendências. A lista só diminui, e a página que sai dela não volta;
- `SnippetsPointToExistingSections`: cada inclusão aponta para um arquivo que existe e para uma seção com `start` e `end`;
- `SnippetSourcesAreCompiledCode`: a inclusão vem de `Packages/com.spellbook/Tests/`, ou de `Samples~/Sandbox/Scripts/` só no Sandbox.

Configuração de asset e do Inspector continua em texto. Um bloco que não pode virar código que compila, como uma assinatura solta ou um contraexemplo que não compila, vira texto ou um link para a API, e não um bloco ` ```text `.

No texto em volta de um exemplo, um tipo da Unity aponta para a página mais recente do ScriptReference, sem versão na URL (`https://docs.unity3d.com/ScriptReference/MonoBehaviour.html`), como o mapa da parte 8. Um tipo do .NET não ganha link escrito à mão. Quem quiser o link encontra na página do tipo na API, que o DocFX liga ao learn.microsoft.com.

Mudar um arquivo de exemplos também dispara a Action `docs`, porque o site muda junto.
