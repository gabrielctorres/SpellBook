# 0041 — Links da API para a documentação da Unity e exemplos do manual incluídos do código

Status: Aceita (gate 2026-11-01)
Data: 2026-11-01 (revista em 2026-11-01 pelas respostas X1 e X2 do gate)

## Contexto

A emenda de 2026-11-01 do PRD `prd/referencia-catalogo-e-api.md` (seção 10) traz dois pedidos do usuário:
- R9 (E1): na referência da API, os tipos da Unity nas assinaturas viram links para docs.unity3d.com;
- R10 (E2): os exemplos de código do manual saem de código real, que compila com o projeto.

Pela 0040, a API sai do `docfx metadata`, que roda na máquina com a Unity, e o `docfx build` monta o HTML na Action a partir dos YAML commitados. O DocFX liga um tipo externo pelo `uid` num mapa de referências cruzadas (`xrefmap`). Sem mapa, o tipo aparece como texto.

O manual tem 41 blocos C# escritos à mão, em 8 páginas, e nada confere que eles compilam. O código do Sandbox fica em `Samples~/`, que pela 0028 não compila na suíte normal. Os testes de cada módulo compilam e rodam sempre.

Existe um mapa da Unity de terceiros, o UnityXrefMaps (NormandErwan, no GitHub). Até onde se sabe, usa licença MIT e gera os mapas a partir do repositório UnityCsReference, que tem licença só de referência. A cobertura da 6000.3 e a manutenção não foram conferidas.

## Decisão

1. **O SpellBook gera o próprio mapa da Unity.** Uma etapa da geração da 0040 lê as referências externas dos YAML de `api/reference/`, resolve cada uma por reflexão no Editor e escreve `Documentation~/api/unity.xrefmap.yml`:
   - os módulos do motor (`UnityEngine.*`, `UnityEditor.*`) apontam para o ScriptReference mais recente, sem versão na URL;
   - os pacotes `com.unity.*` apontam para a API mais recente do pacote (`@latest` na URL);
   - os tipos do .NET (`System.*`, `mscorlib`, `netstandard`) não ganham link e aparecem como texto. Nenhum mapa do .NET entra no build.

   O mapa é commitado, conta como saída gerada (0003, emenda de 2026-11-01) e passa por duas guardas EditMode: ele é igual ao gerado a partir dos YAML commitados, e toda referência da Unity tem entrada. Referência da Unity que não resolve faz a geração falhar. O `api/docfx.json` declara o mapa na chave `xref`, e só ele.
2. **Os exemplos C# do manual são incluídos de arquivos de teste.** O MkDocs usa a extensão `pymdownx.snippets`, que já vem instalada com o `mkdocs-material`, com a versão travada no `requirements-docs.txt`, `check_paths` ligado e a busca restrita à raiz do repositório. Cada exemplo fica entre marcadores, dentro de um `[Test]` em `Tests/<Módulo>/EditMode/Examples/`, que compila e roda na suíte. Só a página do Sandbox inclui de `Samples~/Sandbox/Scripts/`. Uma guarda EditMode falha com bloco C# escrito à mão no manual e com inclusão para arquivo ou seção que não existe.
3. **Os gatilhos da Action ganham as pastas de exemplos** (`Packages/com.spellbook/Tests/**/Examples/**` e `Packages/com.spellbook/Samples~/Sandbox/Scripts/**`). É a única exceção à regra de gatilhos só em `Documentation~/` (0040, item 7).

## Motivo

- O mapa gerado cobre só o que a API usa, cobre os pacotes e não faz a Action baixar nada de terceiros. O teste de guarda cobra o mapa como cobra o catálogo.
- O usuário escolheu a documentação mais recente da Unity (X1), para o link ficar sempre atual, e tipos do .NET como texto (X2), para o build não depender de rede.
- O exemplo dentro de um teste quebra a suíte quando a API muda, então o manual não guarda código velho.
- A extensão de inclusão já está instalada. A trava só deixa a versão explícita.

## Alternativas consideradas

- **Mapa do UnityXrefMaps por URL no `docfx.json`.** Não pede código, mas a Action depende de um site de terceiro sem versão travada, os pacotes ficam sem link, e a cobertura da 6000.3 não foi conferida.
- **Copiar o mapa de terceiros para o repositório.** Leva junto dados derivados do UnityCsReference, que tem licença só de referência.
- **Links na versão `maior.menor` da Unity do host (`6000.3`).** O link não mudaria quando sai uma Unity nova, mas ficaria velho. Recusada no gate (X1).
- **Link do .NET pelo mapa da Microsoft (`learn.microsoft.com`).** O `docfx build` baixaria o mapa na Action a cada build. Recusada no gate (X2).
- **Mapa com toda a API da Unity, por reflexão.** Seria um arquivo de vários megabytes, quase todo sem uso.
- **Exemplos do Sandbox em qualquer página.** O Sandbox só compila com a pasta renomeada (0028), então a suíte não pegaria o exemplo quebrado.
- **Hook próprio do MkDocs para incluir arquivos.** Evita a extensão, mas é código Python nosso para manter.

## Consequências

- Todo bloco C# novo do manual precisa sair de um teste. Configuração de asset e de Inspector continua em texto e imagem.
- O mapa não depende da versão da Unity, só dos YAML. Quando a Unity muda ou remove um tipo, o link pode levar a uma página que mudou ou que não existe mais. Ninguém confere isso de forma automática.
- Na API, `string`, `float` e os outros tipos do .NET aparecem sem link.
- A Action não testa os links externos. Eles são conferidos à mão, por amostra, no ticket que liga o mapa.
- A regra de URL do ScriptReference é irregular. Um caso sem regra estável aponta para a página do tipo.
- A 0040 ganha a emenda de 2026-11-01, aceita no mesmo gate, que aponta para esta ADR.

## O que os agentes NÃO podem fazer

- Editar à mão o `unity.xrefmap.yml`.
- Escrever bloco C# no manual sem inclusão de um arquivo de teste, fora das páginas ainda pendentes na guarda.
- Incluir no manual trecho de arquivo fora da raiz do repositório, ou do Sandbox fora da página do Sandbox.
- Pôr a lista de pendências da guarda para crescer.
- Fazer o build depender de um mapa da Unity de terceiros.
- Pôr versão fixa da Unity nas URLs do mapa, ou declarar um mapa do .NET no `docfx.json`.

## Decisões do gate (2026-11-01)

- X1: alternativa. Os links vão para a documentação mais recente da Unity, sem a versão 6000.3.
- X2: alternativa. Sem link para o .NET; os tipos do .NET ficam como texto.
- X3: mapa gerado pelo SpellBook, como proposto.
- X4: exemplos só de testes EditMode; o Sandbox só na página dele, como proposto.
- X5: `pymdownx.snippets` com a versão travada, como proposto.

A ADR foi aceita no mesmo gate, com a revisão de X1 e X2.

## Emenda (2026-11-02): link do .NET que o DocFX grava

**Status da emenda: Aceita** (2026-11-02, decisão do usuário, revendo o X2). A regra do EX1 não muda: o texto do manual não ganha link para o .NET escrito à mão.

O `docfx metadata` 2.81 grava no YAML de cada referência `System.*` um `href` para learn.microsoft.com (`GetMicrosoftLearnUrl`, fixo no código, sem download e sem opção para desligar). O usuário decidiu manter esse link. Na quarta rodada do RC4, a geração saiu com 0, com 267 YAML em `api/reference/` (16 MB, ~1,5 MB deles nesses `href`), e o `docfx metadata` e o `docfx build --warningsAsErrors` passaram sem aviso nem erro.

- Item 1, terceiro ponto: os tipos do .NET não entram no mapa da Unity e aparecem na API como link para learn.microsoft.com, pelo `href` do YAML. Nenhum mapa do .NET entra no build, e o build continua sem rede.
- Consequências: na API, `string`, `float` e os outros tipos do .NET são links para learn.microsoft.com. Os YAML ficam ~1,5 MB maiores.
- O texto do manual continua sem link escrito à mão para o .NET.
- A proibição de declarar um mapa do .NET no `docfx.json` continua.

## Emenda (2026-11-02): versão do pacote nos links de pacote

**Status da emenda: Aceita** (gate parcial de 2026-11-02, pergunta X6 da Spec `spec/referencia-catalogo-e-api.md`, como proposta).

No XR1, a Fábrica viu que `https://docs.unity3d.com/Packages/<pacote>@latest/api/<uid>.html` dá 404. Conferimos em 2026-11-02 que abaixo de `@latest/` só existe a raiz, uma página que redireciona por JavaScript para a versão mais recente. As páginas de API existem só com a versão `maior.menor` na URL (`com.unity.test-framework@1.6`, `com.unity.ugui@2.0`, `com.unity.inputsystem@1.14`, todas com 200).

- Item 1, segundo ponto: os pacotes `com.unity.*` apontam para a API da versão `maior.menor` do pacote instalado no projeto, lida do `PackageInfo` do assembly na geração (`@1.6` na URL). Os pacotes `com.unity.modules.*` contam como módulos do motor e vão para o ScriptReference.
- Item 1, primeiro ponto: não muda. O `https://docs.unity3d.com/ScriptReference/<página>.html` hoje redireciona para `docs.unity.com/en-us/engine/current/...` e termina em 200; ele continua sendo o endereço da versão mais recente.
- Motivo: a página com a versão é estática, abre sem JavaScript e mostra a API contra a qual o `docfx metadata` compilou. A rota `@latest/index.html?subfolder=/api/<uid>.html`, que a Unity usa no próprio redirecionamento, depende de um parâmetro sem documentação e de JavaScript no navegador.
- Consequências: o mapa passa a depender também das versões dos pacotes `com.unity.*` do projeto. Atualizar um deles pede regenerar a referência, e a guarda do mapa falha até isso acontecer. O link de pacote não acompanha uma versão nova do pacote até essa atualização.
- O que os agentes não podem fazer: o item "Pôr versão fixa da Unity nas URLs do mapa" continua valendo para o ScriptReference. Fica proibido também escrever à mão a versão de um pacote instalado no código do mapa ou no teste que usa um pacote real; ela vem sempre do `PackageInfo`. Os testes da função que monta a URL usam versões de exemplo como entrada.

## Relacionadas

- 0003 (saída gerada), 0025 (site e Action), 0028 (Sandbox), 0040 (catálogo e API).
