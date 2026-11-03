# 0040 — Catálogo e API gerados pela Unity na máquina e commitados

Status: Aceita (gate 2026-11-01)
Data: 2026-11-01 (revista em 2026-11-01 pela G1 do gate)

O nome do arquivo ainda cita a Action, da primeira versão. A decisão vale pelo título acima.

## Contexto

O B-019g (PRD `prd/referencia-catalogo-e-api.md`) pede duas páginas de referência no mesmo site do manual: o catálogo de ações, condições e reações escolhidas por `[SerializeReference]`, gerado do código, e a referência da API pública em DocFX. O DocFX roda na mesma Action e publica no mesmo site (P3 do PRD). A 0007 (item 5) exige que o catálogo e o seletor do Inspector leiam o mesmo metadado, para nunca divergirem. Pela emenda de 2026-11-01 da 0007 (item 5), a página gerada entra no `nav` no ticket que cria o gerador, e a API entra como link absoluto.

A primeira versão desta ADR rodava a Unity num contêiner da Action, com a licença nos secrets. No gate de 2026-11-01 (G1), o usuário escolheu outro caminho, para manter a decisão de 2026-10-29 de não ter CI com a Unity nem secrets da Unity: a geração roda na máquina, com a Unity em batchmode ou pelo menu, e o resultado é commitado. Isso troca a P1 do PRD (catálogo só no build), que fica para o Criativo atualizar.

O código de hoje:
- O seletor do Inspector aparece em todo campo `[SerializeReference]` marcado com `[TypePicker]` (`Runtime/Core/Inspector/TypePickerAttribute.cs`). Há 13 campos assim em `Runtime/` (`EventListener.responses`, `EventResponse.reactions`, `GameplayAbility.Actions`, `GameplayEffect.OnApplication`...).
- Os candidatos de cada tipo base saem de `TypePickerCatalog.GetCandidates(Type)` (`Editor/Core/Inspector/TypePickerCatalog.cs:52-71`), que usa o `TypeCache` da Unity e lê o `[TypeCategory]` (`CreateEntry`, `:170-175`). A regra de candidato (`IsCandidate`, `:163-168`) e o `TypeCache` só existem dentro do Editor.
- Não há CI com a Unity: o 0M-11 foi cancelado, e a única Action é a `docs.yml`, que roda MkDocs em Python.
- O host não tem pacote de IDE (`Packages/manifest.json`), então a Unity não gera `.csproj`.

O DocFX trabalha em duas etapas. O `docfx metadata` compila as fontes com as referências e escreve um YAML por tipo (o modelo `ManagedReference`). O `docfx build` lê esses YAML e monta o HTML, sem compilar nada e sem as DLLs. Os tipos externos, como `MonoBehaviour`, já vêm descritos na seção `references` de cada YAML. Então a Action monta a API sem a Unity, desde que os YAML estejam no repositório. Commitar só o `docfx.json` com as fontes não basta: as referências são as DLLs da Unity instalada na máquina, que a Action não tem, e compilar sem elas pediria o `allowCompilationErrors`.

## Decisão

1. **O catálogo é o seletor.** Para cada tipo base de um campo com `[TypePicker]` num assembly do pacote (o tipo do elemento, quando o campo é lista), o catálogo lista exatamente o que `TypePickerCatalog.GetCandidates(base)` devolve, restrito aos tipos de assemblies do pacote. Assembly do pacote é o que começa por `SpellBook.`, sem `.Tests`, sem `SpellBook.Testing` e sem `SpellBook.Samples.`. Cada linha mostra o nome exibido, o caminho e o resumo do `[TypeCategory]`, e o link para a página do tipo na API. Nenhum atributo novo é criado: a lista de tipos base sai dos campos com `[TypePicker]`.
2. **A geração roda na Unity, na máquina de quem muda o código.** Uma entrada única do Editor, em `SpellBook.Core.Editor` (pasta `Editor/Core/Documentation/`), serve ao menu e ao `-executeMethod` em batchmode. Ela:
   - escreve a página `Documentation~/manual/referencia/catalogo.md`;
   - escreve a configuração de metadados do DocFX em `Documentation~/api/obj/`, com os arquivos-fonte e as referências de cada assembly do pacote lidos de `UnityEditor.Compilation.CompilationPipeline.GetAssemblies`;
   - roda o `docfx metadata` pela ferramenta local do .NET, que escreve os YAML em `Documentation~/api/reference/`;
   - escreve `Documentation~/api/reference.sha256`, a impressão digital das entradas da geração (item 4).

   A entrada não referencia nenhum outro módulo: acha os tipos por reflexão, e a 0012 não muda.
3. **O que vai para o repositório.** A página do catálogo, os YAML de `api/reference/` e a impressão digital são commitados. A configuração de metadados (`api/obj/`) é ignorada pelo git, porque tem caminhos absolutos da máquina e das DLLs da Unity. O `api/docfx.json` commitado é fixo, escrito à mão, e só tem a seção `build`. Os YAML commitados não têm caminho absoluto da máquina nem nome de branch que não seja `main`.
4. **Um teste local de guarda cobra a geração.** Um teste EditMode falha, com a instrução "rode SpellBook > Documentação > Gerar referência", quando:
   - a página que o catálogo renderiza agora difere, byte a byte, da página commitada;
   - a impressão digital calculada agora difere da commitada. Ela cobre, nesta ordem, o texto de cada arquivo-fonte dos assemblies do pacote (caminho relativo e conteúdo com quebras normalizadas para `\n`), a versão da Unity e o `Documentation~/.config/dotnet-tools.json`.

   Todo ticket que muda um `.cs` do pacote roda a geração e commita o resultado no mesmo diff.
5. **O DocFX é ferramenta só da documentação.** Ele entra como ferramenta local do .NET, com versão travada em `Documentation~/.config/dotnet-tools.json`, e o SDK do .NET tem versão travada na Action. Nada disso entra no pacote nem no projeto Unity. O HTML vai para `api/` no mesmo artefato do Pages, e o `nav` do manual aponta para `https://gabrielctorres.github.io/SpellBook/api/` como link absoluto. A página inicial e o sumário fixos da API ficam em `Documentation~/api/`, commitados.
6. **O build continua estrito.**
   - O `mkdocs build --strict` continua como está.
   - O `docfx metadata` roda com os avisos tratados como erro na geração local, e o `docfx build` roda com `--warningsAsErrors` na Action. Uma referência quebrada num comentário XML derruba uma das duas etapas.
   - Um passo da Action confere que todo link do catálogo para a API tem o arquivo correspondente no site gerado.
   - O DocFX não avisa de tipo público sem comentário, então o build não falha por isso (C4 do PRD).
7. **A Action de documentação não usa a Unity.** O job `build` roda em `ubuntu-latest` o `mkdocs build --strict`, o `docfx build --warningsAsErrors` e a conferência dos links, a partir do que está commitado. Ele roda no push para a `main`, no `workflow_dispatch` e nos pull requests, e o job `deploy` publica só no push para a `main`. Os gatilhos de caminho ficam em `Documentation~/`, porque a geração commitada sempre muda algo ali quando o código muda.

## Motivo

- O catálogo e o seletor saem da mesma função, então não divergem por construção (C3 do PRD). Um teste EditMode compara as duas listas, mas a garantia não depende dele.
- Só a Unity sabe a regra completa do seletor (`TypeCache`, `[Serializable]`, construtor sem parâmetros, fora de `UnityEngine.Object`) e as referências que compilam o pacote. Fora dela, a regra seria copiada para outra linguagem, que é o mecanismo paralelo que o usuário recusa.
- O `docfx build` precisa só dos YAML. Commitá-los deixa a Action sem Unity, sem licença e sem secrets, e as DLLs da Unity nunca saem da máquina.
- A impressão digital é barata de calcular no teste e não chama processo externo. O teste de guarda roda junto com os outros, no EditMode.

## Alternativas consideradas

- **Unity num contêiner da Action, com a licença nos secrets.** Era a primeira versão desta ADR. O usuário a recusou na G1, para manter a decisão de 2026-10-29: sem CI com a Unity e sem secrets da Unity.
- **Commitar só o `docfx.json` com as fontes e rodar o `docfx metadata` na Action.** As referências apontam para as DLLs da Unity na máquina, que a Action não tem. Compilar sem elas pediria o `allowCompilationErrors`, e a API mostraria os tipos da Unity como não resolvidos.
- **Análise estática sem a Unity.** A regra do seletor viveria em duas linguagens, e o teste do C3 não teria como comparar as duas listas.
- **Teste de guarda que roda o `docfx metadata` e compara os YAML.** Pega também mudança só de comentário, que a impressão digital já pega, mas chama um processo externo em todo teste EditMode e depende do .NET instalado em quem roda os testes.
- **`.csproj` gerado por pacote de IDE no host.** Pediria uma dependência nova no projeto só para a documentação, e o `CompilationPipeline` já dá a mesma informação.

## Consequências

- A Action de documentação continua sem Unity e sem secrets. O B-036 (secrets da licença) foi cancelado.
- Todo ticket com Unity que muda um `.cs` do pacote roda a geração, com o SDK do .NET na máquina, e o diff ganha os YAML regenerados. Quem roda os testes sem gerar vê o teste de guarda falhar.
- Os YAML de `api/reference/` mudam em muitos arquivos a cada mudança de API. A saída do gerador commitada não conta nos limites de tamanho, e o Examinador revisa o gerador, não a saída (G4, emenda de 2026-11-01 da 0003).
- Dois tickets com Unity em paralelo colidiriam na impressão digital e nos YAML. A regra de WIP 1 para tickets com Unity já evita isso.
- O build local do site não precisa mais da Unity: `mkdocs build --strict` e `docfx build` leem o que está commitado.
- Depois que o catálogo estiver no site, vale a regra da 0007 (item 2): uma ação, condição ou reação nova precisa de página ou de entrada no catálogo. A frase "até lá, ações e condições exigem página", em "Decisões incorporadas" da 0007, deixa de valer.
- A 0025 ganha uma emenda que aponta para esta ADR (job de build, gatilhos, DocFX travado).
- O item 5 da emenda de 2026-11-01 da 0007 fala em "páginas geradas no build" e em pôr "a geração antes do build, na Action". Com esta ADR, a geração fica antes do commit, e a Action só monta. O resto do item vale como está.

## O que os agentes NÃO podem fazer

- Editar à mão a página do catálogo, os YAML de `api/reference/` ou a impressão digital.
- Commitar código do pacote sem a geração em dia, ou mudar o teste de guarda para aceitar saída desatualizada.
- Commitar a configuração de metadados de `api/obj/`, caminho absoluto da máquina ou DLL da Unity.
- Criar uma segunda regra de "tipo oferecido no seletor" fora de `TypePickerCatalog`.
- Instalar o DocFX ou o SDK do .NET sem versão travada, ou pôr qualquer um deles no pacote ou no projeto Unity.
- Pôr a Unity, a licença da Unity ou qualquer secret da Unity na Action.
- Ligar `allowCompilationErrors` ou tirar o tratamento de aviso como erro do DocFX para o build passar.

## Decisões do gate (2026-11-01)

- G1: geração local, commitada, com teste de guarda (esta revisão).
- G2: resumo obrigatório no catálogo, como proposto (RC6 a RC8 da Spec).
- G3: a documentação é montada também nos pull requests, sem publicar.
- G4: a saída gerada commitada não conta no teto do ticket (emenda de 2026-11-01 da 0003).

A ADR foi aceita no mesmo gate, com a revisão da G1.

## Emenda (2026-11-01): mapa da Unity e gatilhos dos exemplos, pela 0041

**Status da emenda: Aceita** (gate de 2026-11-01 da emenda E1 e E2 da Spec `referencia-catalogo-e-api.md`, junto com a 0041).

- **Item 2:** a geração ganha uma etapa, depois do `docfx metadata` e antes da impressão digital, que escreve `Documentation~/api/unity.xrefmap.yml` (0041, item 1). O mapa sai só dos YAML, porque os links vão para a documentação mais recente da Unity, sem versão na URL. A impressão digital não muda.
- **Item 3:** o mapa é commitado, como os YAML.
- **Item 5:** o `api/docfx.json` declara só o mapa da Unity na chave `xref`. Os tipos do .NET ficam como texto, e o `docfx build` não baixa nada da rede.
- **Item 7:** os gatilhos da Action ganham as pastas de exemplos incluídos no manual (0041, item 2). É a única exceção à regra de gatilhos só em `Documentation~/`.

## Emenda (2026-11-02): link do .NET pelo `href` do DocFX, pela emenda de 2026-11-02 da 0041

**Status da emenda: Aceita** (2026-11-02, decisão do usuário, junto com a emenda de 2026-11-02 da 0041).

- **Item 5:** o `api/docfx.json` continua declarando só o mapa da Unity na chave `xref`, e o `docfx build` não baixa nada da rede. Os tipos do .NET aparecem como link para learn.microsoft.com, pelo `href` que o `docfx metadata` 2.81 grava nos YAML commitados.

## Emenda (2026-11-03): geração só no passe de documentação, pela emenda de 2026-11-03 da 0007

**Status da emenda: Aceita** (decisão do usuário, aprovada pelo usuário em 2026-11-03).

- **Item 4.** A frase "Todo ticket que muda um `.cs` do pacote roda a geração e commita o resultado no mesmo diff" deixa de valer. A geração roda no passe de documentação (emenda de 2026-11-03 da 0007, item 6). Entre dois passes, a página do catálogo, os YAML de `api/reference/`, o mapa da Unity e a impressão digital commitados podem estar atrás do código.
- **Guardas (item 4).** O `ReferenceUpToDateTests` inteiro passa para a categoria de teste `Documentacao` (ticket PD1 da Spec `processo-doc-no-fim.md`). A suíte dos tickets roda com `-testCategory "!Documentacao"`, e o passe roda com `-testCategory "Documentacao"`. As guardas continuam iguais: mesma comparação byte a byte e mesma impressão digital.
- **Item 7 e item 7 da emenda de 2026-11-01.** Os gatilhos da Action deixam de incluir as pastas de exemplos de código (`Packages/com.spellbook/Tests/**/Examples/**` e `Packages/com.spellbook/Samples~/Sandbox/Scripts/**`). Fica a regra original de gatilhos só em `Documentation~/`, que muda só no passe. Assim, o PR de um lote de código não monta o site com o manual atrasado. O passe pode rodar a Action pelo `workflow_dispatch`. A exceção de gatilho da 0041 (item 2) deixa de valer, e o resto da 0041 continua.
- **"O que os agentes NÃO podem fazer".** Continuam proibidos: editar a saída gerada à mão e mudar a guarda para aceitar saída desatualizada. "Commitar código do pacote sem a geração em dia" passa a valer só para o commit do passe de documentação.
- **Consequências.** A saída gerada sai dos diffs de ticket. Assim, a regra "dois tickets com Unity colidem na impressão digital" deixa de valer. O WIP continua pela 0003.
- **Até o PD1 entrar**, vale a regra provisória da emenda de 2026-11-03 da 0007.

## Relacionadas

- 0003 (tamanho do ticket, emenda de 2026-11-01 da saída gerada), 0007 (gate de doc, catálogo, emenda de 2026-11-01), 0012 (mapa de módulos), 0013 (`[SerializeReference]`), 0025 (site e Action), 0026 (item 4: `[TypeCategory]` e seletor), 0027 (projeto na raiz), 0041 (links para a Unity e exemplos incluídos do código).
