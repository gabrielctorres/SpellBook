# Guia do manual

Documento interno de quem escreve ou revisa o manual público em `Documentation~/manual/`. Ele fica fora de `manual/`, então o site não o publica. Vale pela emenda de 2026-11-01 da ADR 0007, pela ADR 0044 e pelo D1 ao D6 da Spec `spec/manual-reorganizacao.md`. As regras de processo (quem escreve no manual, quando e como o ticket conta) estão no `AGENTS.md`, seção "Time de desenvolvimento". Este guia só diz como escrever e como conferir.

## 1. Molde da página de sistema

A página de sistema tem nove seções, nesta ordem. As seções 1, 2 e 4 são obrigatórias. As outras entram quando há o que dizer, e a ordem não muda.

   ```markdown
   # Nome do sistema
   (1, obrigatória) Uma frase do que a página ensina. Um parágrafo: que problema o sistema resolve e quando usar.
       Linha do módulo: assembly `SpellBook.X`, depende de `SpellBook.Core`.

   ## Como funciona
   (2, obrigatória) As peças e como se ligam, na linguagem de quem monta a cena. Sem método, sem alocação, sem ordem interna.
       Diagrama em Mermaid quando ajudar, com legenda de uma frase.

   ## Uso rápido
   (3) Passos numerados no imperativo, com o menu exato (**Create > SpellBook > ...**) e valores concretos.
       Último passo: aperte Play e veja o que acontece.

   ## Nome do componente ou asset
   (4, obrigatória) O que é, em uma ou duas frases.
   ### Campos do Inspector
   - `Campo`: uma frase sobre o efeito no jogo.
   - `Modo`: escolha entre
       - `Opção A`: efeito.
       - `Opção B`: efeito.
   (repita para cada componente ou asset)

   ## Prontos para usar
   (5) Os tipos que vêm no pacote, com link para o catálogo gerado. Até o catálogo existir, a lista que já está no manual.

   ## Pelo código
   (6) Dois ou três trechos curtos (ler, aplicar, ouvir). Desempenho e ordem interna ficam aqui.

   ## Criar o seu
   (7) Quando estender vale a pena e um modelo de classe comentado.

   ## Limitações e armadilhas
   (8) Regra, exemplo errado, exemplo certo, saída quando a regra não cabe. Use `{% hint style="info" %}` e `{% hint style="warning" %}`, fechados por `{% endhint %}`.

   ## Veja também
   (9) Receita, guia, exemplo, ferramenta de depuração e validação ligados ao sistema.
   ```

Os marcadores `(1)` a `(9)` e o texto entre parênteses explicam o molde. Não os copie para a página.

Exemplo curto de bloco "Campos do Inspector", com campos reais de `AttributeDefinition`:

   ```markdown
   ## AttributeDefinition
   Asset que descreve um atributo, como vida ou mana. Crie em **Create > GAS > Attribute Definition**.

   ### Campos do Inspector
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

1. O título não tem âncora escrita à mão. A âncora é a que o GitBook gera do título, pela regra do `ManualMarkdown.Slug`: letras minúsculas; acento e cedilha viram a letra base; código e ênfase entram só com o texto; espaço vira hífen, e hífens seguidos viram um; `&`, `<` e `>` viram `and`, `less-than` e `greater-than`; a pontuação some, menos o ponto depois de dígito; slug que começa com dígito ganha `id-`; título repetido na página ganha `-1`, `-2`. O título `#` da página não tem âncora, nem os de `#####` e `######`.
2. A seção de um componente ou asset tem como título o nome do tipo, então a âncora é o nome em minúsculas (`attributedefinition`). Uma reescrita não muda esse título.
3. Mover uma seção para outra página pede, no mesmo ticket: atualizar `DocUrls`, atualizar os links das outras páginas e rodar o `ManualLinksTests`. Não há redirecionamento nem página de remendo no endereço antigo.
4. O `[HelpURL]` usa uma constante de `DocUrls` que já tem página e âncora, e não concatena âncora no atributo:

   ```csharp
   public const string AttributeDefinition = GasAttributes + "#attributedefinition";

   [HelpURL(DocUrls.AttributeDefinition)]
   ```

   Os testes que conferem a URL usam a mesma constante. Código novo já nasce assim, e os `[HelpURL]` antigos migram por módulo, antes da divisão da página daquele módulo.
5. Nenhum `[HelpURL]` aponta para a página do catálogo, que é gerada (0007, emenda de 2026-11-04, item 3).

## 5. Páginas antigas durante a transição

Até o módulo ser reescrito (etapa 2 da Spec), quem muda uma página antiga:

- segue as regras de escrita no trecho que escreve;
- põe o componente ou asset novo num bloco "Campos do Inspector";
- não reestrutura o resto da página.

Página nova nasce no molde.

## 6. Mapa de páginas e `SUMMARY.md`

O `SUMMARY.md` da raiz do manual (`manual/SUMMARY.md`) tem como títulos de grupo (`## Grupo`) os sete grupos, nesta ordem: Começar; Sistemas de habilidade; Sistemas de apoio; Editor e ferramentas; Exemplos; Guias; Referência. Grupo sem página fica fora do sumário. O rótulo de cada página é o título `#` dela, e cada página aparece uma vez.

Toda página nova entra no `SUMMARY.md` no mesmo ticket que a cria. Sem isso, a guarda `SummaryListsEveryPageOnce` falha.

| Grupo | Páginas que já existem ou que as Specs aprovadas criam |
|---|---|
| Começar | `README.md`, `inicio/instalacao.md`; na etapa 3: primeira habilidade, requisitos mínimos da cena, como os módulos se ligam |
| Sistemas de habilidade | `modulos/gas.md` (visão geral); da divisão (etapa 2): atributos, efeitos, habilidades e ações, ativação, alvos e projéteis; `modulos/dano-escudo-e-morte.md`, `modulos/thinkers-e-auras.md`, `modulos/descricao.md` |
| Sistemas de apoio | `modulos/eventos.md` e as páginas que saem dela (etapa 2); `modulos/tags.md`, `tempo.md`, `pooling.md`, `times.md`, `cues.md`, `texto.md`; `ui/*.md` |
| Editor e ferramentas | `inicio/inspector.md`, `ferramentas/*.md` |
| Exemplos | `exemplos/sandbox.md`; receitas (B-019f) |
| Guias | `guias/*.md` (etapa 3) |
| Referência | catálogo gerado (B-019g), `inicio/conceitos.md`, `regras/migracao-2-para-3.md`, perguntas frequentes (etapa 3) |

Regras do mapa:

- As páginas novas dos sistemas ficam soltas em `modulos/` (`modulos/atributos.md`, e não `modulos/gas/atributos.md`), porque o `ManualLinksTests` procura a página de cada URL em `<caminho>.md` e só usa o `README.md` na raiz do manual.
- O catálogo é gerado na máquina e commitado (0040 e 0044, item 7).

## 7. Conferências por busca

Quem implementa e quem revisa rodam estas conferências a partir da raiz do repositório:

- Imagem: `grep -rnE '!\[|<img' Documentation~/manual` não acha nada.
- Sigla em título: `grep -rnE '^#{1,6} .*\b[A-Z]{2,}\b' Documentation~/manual` só acha nomes de tipo em código, GAS (pela exceção da parte 3) e nomes de produto (Unity, JSON).
- Diagrama sem legenda: todo bloco ```` ```mermaid ```` tem uma linha de legenda logo abaixo do fechamento.
- Guardas: no lugar do build, as guardas `Documentacao` da parte 8 rodam na Unity, sob a trava, com `-testCategory "Documentacao"`. Quem não roda a Unity confere o resultado na `/revisao`.

## 8. Catálogo e exemplos gerados

O catálogo (`manual/referencia/catalogo.md`) e o corpo dos blocos C# marcados das páginas (parte 9) saem do código, pela Unity, e vão para o repositório (0040 e 0044, itens 7 e 8). Não edite esses trechos à mão.

As guardas que comparam o código com o manual ficam na categoria `Documentacao` do NUnit: o `ReferenceUpToDateTests` (catálogo), o `ManualCodeExamplesTests` (exemplos, parte 9) e seis testes do `ManualLinksTests`: `EveryHelpUrlHasPageAndAnchor`, `EveryDocUrlHasPage`, `NoStubPages`, `SummaryListsEveryPageOnce` (página fora do sumário ou repetida nele, entrada sem arquivo), `EveryRelativeLinkHasTarget` (link relativo para arquivo ou âncora que não existe, link que sai do manual, que começa em `/` ou que aponta para o próprio site) e `NoMkDocsSyntax` (sobra de `{#`, `!!!`, `???` ou `--8<--` na página). Quando cada suíte roda, e o que um ticket de código deixa para o passe de documentação, está no `AGENTS.md`, seção "Time de desenvolvimento".

No passe de documentação:

1. Rode **SpellBook > Documentação > Gerar catálogo e exemplos** na Unity, sob a trava. Em batchmode, a mesma geração roda com `-executeMethod SpellBook.Core.Editor.Documentation.ReferenceGenerator.GenerateFromCommandLine` e sai com 0 se deu certo ou com 1 se falhou. Um marcador quebrado não para os outros: a geração expande o resto e lista os quebrados no log.
2. O catálogo e as páginas que a geração mudou entram no mesmo diff. Sem isso, o `ReferenceUpToDateTests` ou o `ExpandedBlocksMatchSections` falha.
3. Rode a suíte com `-testCategory "Documentacao"`. Se o ticket tem código, a revisão roda também a suíte com `-testCategory "!Documentacao"`.

Não há build local nem Action de documentação (0044, item 12). O GitBook publica o space público a cada push na `main` que muda a pasta do manual, e as guardas acima são a única validação.


## 9. Exemplos de código

Todo bloco C# do manual sai de um arquivo de exemplos que compila e roda com os testes do pacote (Spec `referencia-catalogo-e-api.md`, D9; ADR 0041 e 0044, item 8). A página não tem código escrito à mão: antes do bloco vai um marcador com o arquivo e as seções, e a geração (parte 8) escreve o trecho no bloco.

O arquivo de exemplos de cada módulo é `Packages/com.spellbook/Tests/<Módulo>/EditMode/Examples/<Módulo>ManualExamples.cs`, no assembly de testes que o módulo já tem. O do Core é o `CoreManualExamples.cs`, com os exemplos de `inicio/conceitos.md`. A classe tem o nome do arquivo e fica no namespace `<namespace raiz dos testes>.Examples`, como `SpellBook.Core.Tests.Examples`. Só a página `exemplos/sandbox.md` pode incluir de `Packages/com.spellbook/Samples~/Sandbox/Scripts/`.

No arquivo, marque o trecho com dois comentários. O nome da seção tem letras minúsculas, números, `-` e `_`, e é único no arquivo. O comentário guarda o formato antigo, `--8<--`, que a geração lê no arquivo de exemplos. Na página ele não entra:

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

Na página, escreva o marcador e um bloco C# vazio. O título do marcador é o caminho a partir da raiz do repositório, dois-pontos e as seções do mesmo arquivo, separadas por vírgula, na ordem em que entram no bloco. Depois rode a geração, que preenche o bloco:

   ````markdown
   {% code title="Packages/com.spellbook/Tests/Core/EditMode/Examples/CoreManualExamples.cs:mathtools-using,mathtools-map" %}
   ```csharp
   ```
   {% endcode %}
   ````

Regras do arquivo de exemplos:

- O trecho que é chamada fica dentro de um `[Test]`, e o teste confere o que o texto da página diz do resultado. A preparação (as variáveis que o trecho usa) e as asserções ficam fora da seção.
- Uma declaração de tipo fica no namespace, fora da classe de testes, entre os marcadores. Um `[Test]` do mesmo arquivo usa o tipo e confere o que a página diz dele. Tipo de apoio que a página só cita, como o contexto `MyContext`, fica fora de qualquer seção.
- O `using` que a página mostra fica numa seção própria no topo do arquivo, com a linha em branco que o separa do resto dentro da seção. O título do marcador lista então duas seções, a do `using` primeiro e a do trecho depois.
- O recuo sai sozinho: a geração tira o recuo comum das linhas de cada seção e põe o recuo da cerca. Um membro de classe (um campo, um método) ou uma linha dentro de um método aparece na página a partir da coluna zero.
- Um bloco junta seções do mesmo arquivo, na ordem do título. Seções de arquivos diferentes ficam em blocos diferentes.

A geração falha se o arquivo ou a seção não existe. O `ManualCodeExamplesTests` confere, na Unity:

- `NoHandWrittenCSharpBlocks`: todo bloco ` ```csharp `, ` ```cs ` ou ` ```c# ` tem o marcador logo antes, fora as páginas da lista de pendências. A lista só diminui, e a página que sai dela não volta;
- `SnippetsPointToExistingSections`: cada marcador vem logo antes de um bloco C#, tem `{% endcode %}` logo depois do bloco e aponta para um arquivo que existe e para seções com `start` e `end`;
- `SnippetSourcesAreCompiledCode`: o arquivo do marcador fica em `Packages/com.spellbook/Tests/`, ou em `Samples~/Sandbox/Scripts/` só no Sandbox;
- `ExpandedBlocksMatchSections`: cada bloco marcado é igual ao que a geração escreveria. Se não for, rode a geração.

Configuração de asset e do Inspector continua em texto. Um bloco que não pode virar código que compila, como uma assinatura solta ou um contraexemplo que não compila, vira texto, e não um bloco ` ```text `.

No texto em volta de um exemplo, um tipo da Unity aponta para a página mais recente do ScriptReference, sem versão na URL (`https://docs.unity3d.com/ScriptReference/MonoBehaviour.html`). Um tipo do .NET não ganha link escrito à mão.

Mudar um arquivo de exemplos deixa o bloco da página atrás do código até a próxima geração, no passe. O `ExpandedBlocksMatchSections` acusa a diferença.
