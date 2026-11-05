# 0044 — Manual no GitBook por Git Sync, editado pelo MCP, sem referência da API gerada

Status: Aceita (gate de 2026-11-04: Q1 a Q6 respondidas pelo usuário; os pontos de "Em aberto" são fatos a confirmar no spike GB1, não decisões)
Data: 2026-11-04

## Contexto

O manual é publicado pelo MkDocs Material no GitHub Pages (0025). A referência da API sai do DocFX, a partir de YAML que a Unity gera na máquina e que são commitados (0040). Os links para a Unity saem de um mapa gerado (0041, item 1), e os exemplos de código são incluídos de testes pelo `pymdownx.snippets` (0041, item 2). O build estrito e o `nav` validado nos dois sentidos vêm do MkDocs (0007, emenda de 2026-11-01).

No B-042, o usuário avaliou o GitBook, com o [Gasify](https://feliperoddd.gitbook.io/gasify) como referência. A emenda Proposta de 2026-11-04 da 0025 recomendava manter o MkDocs. Em 2026-11-04, o usuário decidiu o contrário: migrar o manual para o GitBook e tirar a referência da API. A documentação final tem só o manual e os exemplos, sem API gerada.

No gate de 2026-11-04, o usuário decidiu também que **os agentes editam o manual pelo servidor MCP do GitBook** (`https://mcp.gitbook.com/mcp`, já instalado no ambiente do Controlador, com autenticação do usuário). Isso muda a premissa da primeira versão desta ADR, em que o GitBook só publicava e o repositório era a única porta de entrada do texto.

Inventário de 2026-11-04 do que a troca afeta:
- 16 páginas em `Documentation~/manual/`, com 92 linhas de inclusão `--8<--` em 7 páginas;
- 232 marcas de sintaxe do MkDocs em 16 páginas: âncoras `{#id}`, admonitions `!!!` e blocos `mermaid`;
- `Documentation~/mkdocs.yml`, `requirements-docs.txt`, `.github/workflows/docs.yml`, `Documentation~/api/` (com `docfx.json`, `index.md`, `toc.yml`, o mapa da Unity, a impressão digital e ~267 YAML em `reference/`) e `Documentation~/.config/dotnet-tools.json`;
- 6 arquivos de geração em `Editor/Core/Documentation/` (`DocfxConfig`, `ReferenceFingerprint`, `ReferenceGenerator`, `TypeCatalog`, `TypeCatalogPage`, `UnityXrefMap`) e as guardas da categoria `Documentacao` (`ReferenceUpToDateTests`, `ManualCodeExamplesTests` e três testes do `ManualLinksTests`).

O que a pesquisa do Controlador achou sobre o GitBook (2026-11-04):
- o Git Sync funciona nas duas vias com GitHub e GitLab, e o plano gratuito cobre espaço público, domínio próprio e o sync;
- o `.gitbook.yaml` tem `root`, `structure.readme`, `structure.summary` e `redirects`;
- o `gitbook-docs.yaml` liga cada espaço a uma pasta (`content.directory`);
- o `SUMMARY.md` define o sumário, e cada arquivo Markdown aparece nele uma vez;
- editar o `README.md` no GitBook com o sync ligado pode gerar conflito;
- o MCP do GitBook edita por change request, e a ligação do Git Sync só se faz pela interface (instruções do servidor);
- não há sintaxe nativa para incluir trecho de arquivo. Isso só existia por plugin no GitBook 3.x, que é legado.

Ainda não foram confirmados vários pontos, entre eles o Mermaid, o caminho com til, as âncoras, o comentário HTML no sync e o comportamento do MCP com o Git Sync. Eles ficam em "Em aberto" e são o objeto do spike (GB1 do design doc `spec/migracao-gitbook.md`).

### O que a edição pelo MCP muda

Com o Git Sync de duas vias, juntar (merge) um change request no GitBook vira um commit do GitBook na branch ligada ao espaço. Se o espaço editado fosse o público, ligado à `main`, cada edição de agente entraria na `main` sem PR, sem as guardas `Documentacao` e sem o `/commit`. Isso quebraria três regras: o merge na `main` é do usuário (0002), as guardas rodam antes do merge (0007) e o exemplo do manual é o código do teste (0041, item 2). Esta ADR evita isso separando o espaço onde os agentes escrevem do espaço que o leitor vê (item 13).

Nenhuma guarda fica inviável. Duas coisas mudam de lugar:
- a guarda do exemplo (0041, item 2) passa a rodar depois que o change request entra na branch do lote, e não antes. O texto do change request pode ter um bloco C# vazio por um tempo, mas o rascunho não é publicado, e a `main` só recebe o que passou pela guarda;
- o "um ticket, um commit" (0042) deixa de valer no passe de documentação: o ticket passa a ter o commit do change request e o do `/commit`.

## Decisão

1. **Plataforma.** O manual público é publicado num espaço público do GitBook, ligado ao repositório `gabrielctorres/SpellBook` pelo Git Sync, na branch `main`. O endereço é o subdomínio gratuito do GitBook (`<conta>.gitbook.io/spellbook`; aprovado no gate de 2026-11-04, Q4). O GitHub Pages deixa de ser usado.
2. **O Git continua a fonte do que é publicado.** O site público mostra só o que está na `main`, e a `main` só recebe o manual pelo PR da branch do lote, com as guardas verdes. O texto pode nascer no GitBook (item 13), mas entra no repositório pela branch do lote, nunca direto na `main`. O motivo da recusa original do GitBook na 0025 (uma segunda verdade fora do repositório) continua valendo: o rascunho no GitBook é uma etapa de escrita, e não uma segunda versão do manual.
3. **Dois espaços (aprovado no gate de 2026-11-04, Q6).**
   - **Público** (`SpellBook`): ligado à `main`, publicado. Só recebe do Git. Nenhum agente escreve nele, pelo MCP ou pelo editor. Se o GitBook permitir travar o conteúdo publicado, ele fica travado.
   - **Rascunho** (`SpellBook — rascunho`): privado, nunca publicado, ligado pelo Git Sync de duas vias à branch do lote do passe de documentação. É o único lugar onde os agentes escrevem pelo MCP. No começo de cada lote com passe, o usuário religa o rascunho à branch nova (passo do usuário).
4. **Layout.** A configuração do GitBook fica na raiz do repositório. Ela usa o `.gitbook.yaml` (`root` na pasta do manual, `structure.readme` e `structure.summary`) ou o `gitbook-docs.yaml`, o que o spike mostrar que vale para o Git Sync atual. A raiz dos dois espaços é a pasta do manual, e só ela, então `prd/`, `spec/`, `adr/`, `planos/` e o backlog nunca são publicados nem aparecem no rascunho. A pasta continua `Documentation~/manual/` se o spike (GB1) mostrar que o Git Sync aceita o til; se não aceitar, o manual vai para `docs/` na raiz do repositório, que a Unity também ignora (aprovado no gate de 2026-11-04, Q2).
5. **Navegação.** O `SUMMARY.md`, na raiz do manual, substitui o `nav` do `mkdocs.yml`. Ele segue os sete grupos e a ordem do guia do manual, e cada página aparece uma vez. Ele muda nos dois sentidos: no repositório, a entrada acompanha a página no mesmo ticket; no rascunho, o GitBook o reescreve quando um agente cria, move ou apaga uma página pelo MCP. Nos dois casos, a guarda do item 10 confere o resultado na branch do lote.
6. **Sem referência da API (aprovado no gate de 2026-11-04, Q5).** Saem do repositório, por inteiro: o DocFX, os YAML de `api/reference/`, o `api/docfx.json` e as outras páginas fixas da API, o mapa da Unity, a impressão digital, o `.config/dotnet-tools.json` e o SDK do .NET travado, a etapa de metadados da geração, o `docs.yml`, o `mkdocs.yml`, o `requirements-docs.txt` e todo uso de Python na documentação. Os comentários XML da API pública continuam obrigatórios (0007, emenda de 2026-11-03, item 3), porque servem ao IntelliSense de quem usa o pacote.
7. **Catálogo (aprovado no gate de 2026-11-04, Q1).** O catálogo continua gerado pela Unity a partir do `TypePickerCatalog` e commitado, como nos itens 1 e 4 da 0040, e a guarda byte a byte continua. Cada linha mostra o nome exibido, o caminho e o resumo do `[TypeCategory]`, sem link para a API. Nenhum agente edita a página do catálogo pelo MCP.
8. **Exemplos expandidos na máquina.** A regra do item 2 da 0041 continua: todo bloco C# do manual sai de uma seção marcada num teste, ou do Sandbox na página dele. Muda o mecanismo, porque o GitBook não inclui arquivo:
   - antes do bloco, a página tem um marcador que diz o arquivo e a seção, um por seção, na ordem em que aparecem no bloco;
   - quem escreve pelo MCP põe só o marcador e um bloco C# vazio, ou mantém o bloco que já existe. Não digita código;
   - uma etapa da geração local, na mesma entrada de menu do catálogo, reescreve o bloco com o trecho atual, sem o recuo comum. Ela roda no checkout do ticket, depois que o change request entra na branch do lote;
   - os blocos expandidos são saída de gerador com guarda (0042), e uma guarda `Documentacao` falha quando um bloco difere da seção atual, quando a seção não existe ou quando há bloco C# sem marcador.

   A proposta de marcador é um comentário HTML (`<!-- snippet: <caminho>:<seção> -->`), se o spike confirmar que ele sobrevive à edição pelo MCP e ao sync nas duas vias sem aparecer na página. Se não sobreviver, o marcador passa a ser o título nativo do bloco de código do GitBook (`{% code title="<caminho>:<seção>" %}`), que o leitor vê. A troca não volta ao gate.
9. **Âncoras e URLs.** Os títulos perdem o `{#id}`, que o GitBook não lê. A âncora passa a ser a que o GitBook gera a partir do título. A seção de um componente ou asset tem como título o nome do tipo, então a âncora continua sendo o nome do tipo em minúsculas, se o spike confirmar a regra. `DocUrls.Base` e o `documentationUrl` do `package.json` apontam para o endereço do item 1. A constante por tipo (0007, emenda de 2026-11-01, item 7) continua. Os links entre páginas do manual são relativos, no formato em que o GitBook os grava no sync (o spike confirma o formato); link absoluto para o próprio site é proibido no manual.
10. **Validação no lugar do build estrito.** O GitBook não tem build que falhe. As conferências do `mkdocs build --strict` viram guardas EditMode da categoria `Documentacao`, que rodam na branch do lote. Elas falham quando:
    - uma página está fora do `SUMMARY.md`, aparece nele duas vezes ou é apontada por ele sem existir;
    - um link relativo leva a arquivo ou âncora que não existe, ou um link aponta para o endereço do próprio site;
    - um `[HelpURL]` ou uma constante de `DocUrls` não tem página ou âncora;
    - um exemplo está fora de dia (item 8);
    - sobra sintaxe do MkDocs (`{#`, `!!!`, `???`, `--8<--`).
11. **Sintaxe e diagramas.** As páginas usam o Markdown do GitBook: blocos `{% hint %}` no lugar das admonitions. A regra da 0007 continua: só diagramas Mermaid, com legenda, e nenhuma imagem no manual. Quem escreve pelo MCP não sobe imagem nem usa bloco de desenho do GitBook. Se o GitBook não desenhar o bloco `mermaid` nativo nem por integração oficial, a regra dos diagramas volta ao gate antes da conversão das páginas.
12. **Sem Action e sem Python.** O GitBook publica o espaço público a cada push na `main` que muda a pasta do manual. Não há Action de documentação.
13. **Edição pelo MCP (aprovado no gate de 2026-11-04, Q3 e Q6).**
    - **Quem.** A Fábrica do ticket do passe é o único papel que escreve pelo MCP. O Controlador já tem o servidor no ambiente e usa só a leitura, para conferir o texto. O Examinador, o Desenhista, o Codex e o Antigravity não usam o MCP.
    - **Ferramentas.** O servidor se descreve assim (instruções dele, lidas em 2026-11-04): o conteúdo publicado só muda por change request; a ligação do Git Sync só se faz pela interface; o fluxo de edição é `create_change_request`, `invoke_operation` com a operação `updateChangeRequestContent` e `submit_or_merge_change_request`; a leitura é `list_sites`, `get_site_structure` e `get_page`. O agente `fabrica` recebe essas ferramentas e o `get_usage_guide`, com o nome completo que a sessão mostrar (`mcp__<servidor>__<ferramenta>`).
    - **Limite da lista.** O `invoke_operation` executa qualquer operação da API do GitBook, inclusive publicar e configurar. Por isso, a lista `tools` do agente não basta para impedir a escrita no espaço público. Ela é impedida pela regra deste item, pela conferência do Controlador antes do merge e, se o plano permitir, por uma conta do GitBook só com acesso ao rascunho (spike). Fechar o `invoke_operation` para a operação de edição pede uma permissão por argumento que o Claude Code talvez não tenha; o spike confere.
    - **Como.** Um ticket = um change request no rascunho, com o número do ticket no título. A Fábrica abre o change request, escreve as páginas do Detalhe, ajusta o `SUMMARY.md` pela estrutura do GitBook e aplica a humanizer. Não toca em página fora do Detalhe, na página do catálogo nem no corpo de bloco C#.
    - **Conferência antes do merge.** O Controlador lê o change request pelo MCP e faz as conferências de texto do passe: humanizer, molde, siglas, sumário e legendas. Ele também olha a prévia do change request, para ver os diagramas desenhados. Problema volta à Fábrica, no mesmo change request.
    - **Merge do change request.** Com o texto conferido, a Fábrica junta o change request no rascunho. O GitBook faz o commit na branch do lote. Esse é o único caso em que um papel cria commit fora do `/commit`, e ele nunca acontece no espaço público.
    - **Depois do merge.** Um passo do script da fila traz o commit para o checkout do ticket, só por avanço rápido. A Fábrica roda a geração (item 8) e o código do ticket, se houver. O Examinador roda as guardas pelo `/revisao`, e o `/commit` commita a expansão, o Detalhe e o código. Se uma guarda reprovar o texto, a correção é um change request novo do mesmo ticket.
    - **Conflito.** No checkout do ticket, ninguém edita à mão arquivo da pasta do manual: só a geração escreve nela, e só depois do merge. Uma página só pode estar em um change request aberto por vez. Se o GitBook acusar conflito no merge, a Fábrica para e devolve ao Controlador. Na briga entre os dois lados, vence o repositório para o que é gerado (blocos C# e catálogo, que a geração reescreve) e o change request para o texto.
    - **Autenticação.** O MCP entra com a conta do usuário, então tem os direitos dele nos dois espaços. Ver "Limite da lista", acima.
14. **Passe de documentação.** O passe da 0007 (emendas de 2026-11-03) continua, com estas mudanças:
    - o texto do manual é escrito no rascunho pelo MCP (item 13), e não no checkout;
    - a geração passa a ser **SpellBook > Documentação > Gerar catálogo e exemplos**;
    - no lugar do `mkdocs build --strict`, valem as guardas `Documentacao` do item 10, rodadas pelo Examinador depois do merge do change request;
    - o Controlador faz as conferências de texto no change request, antes do merge, e o `git diff --check` e as conferências por busca na branch do lote, antes do `/commit`. Ele não roda mais build;
    - o `/revisao` não muda o que confere: as guardas `Documentacao`, sem achado sobre o texto, e a suíte completa se houver código. Ele roda depois do merge do change request e da geração.

## Motivo

- O usuário quer o visual, a busca e as integrações do GitBook no lugar do site estático, e quer que os agentes escrevam no próprio GitBook, com a prévia do que o leitor vai ver.
- Sem a API, somem a ferramenta do .NET, a etapa de metadados, o mapa da Unity, os ~267 YAML commitados, a Action e o Python. Quem usa o pacote lê a API pelo IntelliSense, a partir dos comentários XML.
- O espaço de rascunho ligado à branch do lote deixa o agente escrever no GitBook sem pular o PR, as guardas e o merge do usuário. O manual público continua revisado como docs-as-code.
- Expandir os exemplos na máquina mantém a garantia da 0041 (exemplo que compila e roda) sem depender de um recurso que o GitBook não tem, e sem pedir que o agente copie código pelo MCP.
- As guardas `Documentacao` já rodam no passe. Pôr nelas o que o build estrito conferia não cria etapa nova.

## Alternativas consideradas

- **Manter o MkDocs** (emenda Proposta de 2026-11-04 da 0025). Recusada pelo usuário em 2026-11-04.
- **GitBook só publica, edição pelo repositório** (primeira versão desta ADR). Recusada pelo usuário no gate de 2026-11-04 (Q3): os agentes editam pelo MCP.
- **MCP direto no espaço público, com change request.** Um espaço só, mais simples. Recusada pelo usuário no gate de 2026-11-04 (Q6): cada merge de change request vira commit direto na `main`, sem PR, sem guardas antes e sem o merge do usuário. A guarda do exemplo só pegaria o erro com o código já publicado.
- **Rascunho sem Git Sync, copiado para o repositório pelo agente.** Mantém o "um ticket, um commit". Recusada com a Q6: o agente leria a página pelo MCP e a escreveria no checkout, então haveria duas cópias do texto, e a forma como o GitBook grava o Markdown no sync não seria a mesma da cópia.
- **Rascunho ligado a uma branch fixa (`docs/rascunho`), que entra na branch do lote por merge.** Evita religar o espaço a cada lote. Recusada com a Q6: cria uma branch de vida longa e merges entre branches fora do fluxo do lote.
- **GitBook com a API num segundo site, no GitHub Pages.** Recusada no gate de 2026-11-04 (Q5).
- **Exemplos escritos à mão no GitBook.** Perde a garantia da 0041, e o manual voltaria a guardar código velho.
- **Plugin de inclusão do GitBook 3.x.** É legado e não roda no GitBook atual.

## Consequências

- **0025:** substituída por esta ADR. O item 1 (o manual em Markdown, numa pasta própria) e a proibição de publicar documento interno continuam aqui. A proibição de segunda verdade continua no item 2. A emenda Proposta de 2026-11-04 fica recusada.
- **0040:** perde os itens 2 (na parte do DocFX), 3, 5, 6 e 7, a impressão digital do item 4 e as emendas. Os itens 1 e 4 continuam só para o catálogo.
- **0041:** o item 1 (mapa da Unity) deixa de valer. O item 2 continua como regra, com o mecanismo do item 8 desta ADR; a guarda passa a rodar depois do merge do change request, na branch do lote. O item 3 já tinha caído.
- **0007:** na emenda de 2026-11-01, o item 4 troca o `nav` pelo `SUMMARY.md` e o build estrito pelas guardas, o item 5 perde a API, e o item 6 troca o `{#id}` pela âncora do título. Na emenda de 2026-11-03, o item 6 troca a geração e o build, e o texto do passe passa a ser escrito pelo MCP. Na emenda "quem revisa", o Controlador não roda mais o build e confere o texto no change request.
- **0042:** "a entrada do `nav`" passa a ler "a entrada do `SUMMARY.md`". A lista de saída de gerador com guarda passa a ser a desta ADR: a página do catálogo e os blocos C# expandidos do manual. No passe de documentação, o ticket tem o commit do change request (e o de cada correção) e o do `/commit`; o teto de tamanho soma os dois.
- **0002:** a Fábrica do passe passa a criar o commit do change request no rascunho. O `/commit` continua sendo o único passo que commita no checkout, e o merge na `main` continua do usuário.
- **0005:** a trava não muda; escrever pelo MCP não usa a Unity. A geração e as guardas, sim.
- **0003:** já substituída pela 0042. Não muda.
- As emendas curtas da 0002, da 0007, da 0040, da 0041 e da 0042 que apontam para cá, e a troca do status da 0025 para "Substituída por 0044", são o ticket GB11 de `spec/migracao-gitbook.md`, escrito pelo Desenhista no lote da migração. Até o merge desse lote, o MkDocs continua no ar, e as ADRs antigas descrevem o que roda.
- Os testes do DocFX e do mapa (`DocfxConfigTests`, `UnityXrefMapTests`, `ReferenceGeneratorTests`) saem com o código. Com isso, o XR3 deixa de ter objeto nesses testes, mas a guarda contra a pasta temporária do sistema continua.
- Uma referência quebrada num comentário XML (`cref`) não derruba mais nenhum build. Ela só aparece como aviso do compilador, se o aviso estiver ligado.
- O site e o rascunho ficam num serviço de terceiros. O plano, os limites e o MCP podem mudar sem aviso.
- O usuário ganha um passo por lote com passe: religar o rascunho à branch do lote.
- O B-031 (ligar o GitHub Pages) perde o objeto.

## O que os agentes NÃO podem fazer

- Escrever no espaço público, pelo MCP ou pelo editor, ou juntar change request nele.
- Usar ferramenta do MCP de publicação, de configuração de site ou de espaço.
- Juntar change request no rascunho antes da conferência de texto do Controlador.
- Editar à mão, no checkout, arquivo da pasta do manual: lá, só a geração escreve.
- Publicar documento interno: a raiz dos espaços é só a pasta do manual.
- Editar um bloco C# expandido ou a página do catálogo, pelo MCP ou à mão.
- Escrever bloco C# no manual sem marcador de exemplo.
- Pôr imagem, captura de tela ou diagrama que não seja Mermaid no manual.
- Trazer de volta DocFX, Python ou uma Action de documentação sem uma ADR nova.
- Mudar uma guarda `Documentacao` para aceitar página fora do sumário, link quebrado ou exemplo desatualizado.

## Em aberto

Pontos a verificar no spike (GB1). Até lá, não são fato:
- se o Git Sync aceita a raiz `Documentation~/manual/` (caminho com til);
- qual arquivo de configuração vale hoje: o `.gitbook.yaml` ou o `gitbook-docs.yaml`;
- se o `structure.readme` aceita o `index.md`, ou se a página inicial precisa se chamar `README.md`, e o risco de conflito ao editá-la;
- a regra das âncoras: o slug gerado para `## AttributeDefinition`, para títulos com acento e para títulos repetidos, e se existe âncora própria;
- o formato da URL de página a partir do caminho do arquivo (`modulos/gas.md`), e o formato em que o GitBook grava no sync um link entre páginas criado no editor ou pelo MCP;
- se o bloco `mermaid` é desenhado de forma nativa, por integração oficial ou de jeito nenhum;
- se um comentário HTML sobrevive à edição pelo MCP e ao sync nas duas vias sem aparecer na página; se não, se o `{% code title %}` sobrevive;
- se o GitBook regrava, no sync, o Markdown que não mudou (espaços, listas, escapes), e se o commit dele passa no `git diff --check`;
- o que acontece com o `SUMMARY.md` quando o MCP cria ou move uma página;
- o nome completo das ferramentas do MCP na sessão da Fábrica, e se a permissão do Claude Code consegue limitar o `invoke_operation` à operação `updateChangeRequestContent`;
- se o merge de um change request num espaço com Git Sync gera um commit só, com que autor e com que mensagem, e o que o GitBook faz em conflito entre o change request e um commit novo na branch;
- se o espaço de rascunho pode ser privado e não publicado no plano gratuito, e se religá-lo a outra branch preserva o histórico;
- se o conteúdo do espaço público pode ser travado contra edição, e se o plano permite uma conta só com acesso ao rascunho;
- se a autenticação do MCP feita pelo usuário vale para a sessão da Fábrica num terminal do Orca;
- o preço e os limites do plano gratuito.

## Emenda (2026-11-05): resultado do spike GB1

**Status da emenda: Aceita** (gate de 2026-11-05; ticket GB1, #356, de `spec/migracao-gitbook.md`). Registra o que o spike mostrou sobre cada ponto de "Em aberto" e as decisões do usuário no gate. A decisão 3, no fim desta emenda, mudava os itens 3, 4 e 5 da Decisão. Mais tarde, no mesmo gate, a decisão 8 a substituiu: um space só para o manual inteiro. Com ela, os itens 3 e 5 voltam a valer quase como estavam, e só o item 4 muda. A decisão 9 substitui a 6: as pastas do manual continuam as de hoje. A decisão 4 muda a leitura dos itens 9 e 10.

Base: branch `spike/gitbook`, com os commits `7160b42` (conteúdo), `6d1255a` (`gitbook-docs.yaml`) e `934c949` (`GITBOOK-1`, gravado pelo GitBook no merge do change request). A organização do GitBook (`Rook Studio`) está no plano gratuito.

**Confirmado:**
1. **Caminho com til.** O Git Sync aceita. O import funcionou com o diretório `./Documentation~/manual` e `./Documentation~/manual/modulos`. Pelo item 4 e pela Q2, a pasta continua `Documentation~/manual/`, e o `docs/` não entra.
2. **Arquivo de configuração.** O Git Sync do site exige o `gitbook-docs.yaml` (schema `https://api.gitbook.com/gitbook-docs.yaml`), e não o `.gitbook.yaml`. O arquivo fica em `Documentation~/manual/gitbook-docs.yaml`, e não na raiz do repositório, como o item 4 previa. O spike usou um site com uma seção (`Manual`) e 4 spaces. Cada space tem a sua pasta (`content.directory`) e o seu Git Sync (`installationProjectDirectory`). O `structure.readme` e o `structure.summary` do `.gitbook.yaml` não se aplicam.
3. **Primeira página.** O GitBook renomeou `modulos/gas.md` para `modulos/README.md`: a primeira página de cada space vira `README.md`. Logo, o nome de arquivo no repositório segue o GitBook, e uma guarda ou um `DocUrls` que cite página por nome precisa contar com isso. O `index.md` da raiz e o conflito ao editar um `README.md` não foram testados.
4. **Link entre páginas.** Um link escrito pelo MCP como `/pages/<caminho>` foi gravado no sync como link relativo para o `.md` (`efeitos-de-exemplo.md`, `./`). Isso confirma o formato relativo do item 9.
5. **Sumário.** O GitBook criou um `SUMMARY.md` em cada space (`modulos/SUMMARY.md`, com `# Table of contents` e uma lista), e não um só na raiz do manual, como o item 5 previa.
6. **Change request pelo MCP.** O fluxo funcionou de ponta a ponta: criar o change request, `update_page`, `insert_page`, link entre páginas e merge. O merge gerou um commit, `GITBOOK-1`, na branch ligada (`spike/gitbook`), e não na `main`. Isso confirma o desenho do item 13: o commit cai na branch do espaço editado.
7. **Instruções injetadas.** No mesmo commit `GITBOOK-1`, o GitBook escreveu 22 linhas de "GitBook Documentation Editing" no `AGENTS.md` e no `CLAUDE.md` da raiz do repositório, entre `<!-- gitbook-agent-instructions:start -->` e `<!-- gitbook-agent-instructions:end -->`. São arquivos fora da pasta do manual, e o item 4 supunha que nada fora dela seria tocado. Ligar o espaço público à `main` pode levar isso para lá. É um risco às regras dos agentes. O gate aceitou o bloco (decisão 2, abaixo).
8. **BOM.** A escrita do GitBook tira o BOM dos arquivos. É neutro.

**Não sobrevive:**
9. **Comentário HTML.** O GitBook apaga o `<!-- snippet: ... -->` na importação e quando o MCP escreve a página. O marcador proposto no item 8 não serve. A alternativa do item 8 (`{% code title %}`) não foi testada. O gate a escolheu, com um teste antes do código do GB4 (decisão 1, abaixo). O cabeçalho em comentário HTML da página do catálogo (0040, item 4) corre o mesmo risco se o GitBook regravar a página.

**Parcial:**
10. **Mermaid.** O bloco `mermaid` chega ao GitBook. O spike não conferiu o site publicado. Depois dele, o usuário conferiu que o diagrama aparece desenhado (decisão 5, abaixo).
11. **Títulos.** O título com acento chegou ao GitBook. O slug gerado para ele, para `## AttributeDefinition` e para títulos repetidos, e a existência de âncora própria, não foram medidos.

**Não conferido:**
- a URL publicada a partir do caminho do arquivo;
- se o GitBook regrava o Markdown que não mudou (fora o BOM) e se o commit dele passa no `git diff --check`;
- o nome completo das ferramentas do MCP na sessão da Fábrica e se a permissão do Claude Code limita o `invoke_operation`;
- o autor do commit do merge e o que o GitBook faz em conflito com um commit novo na branch;
- o sync da `main` para o GitBook quando um PR entra;
- o espaço de rascunho privado e não publicado no plano gratuito, e se religá-lo a outra branch preserva o histórico;
- a trava do espaço público e uma conta só com acesso ao rascunho;
- a autenticação do MCP na sessão da Fábrica num terminal do Orca;
- o preço e os limites do plano gratuito.

**Decisões do gate de 2026-11-05** (as mesmas de "Resultado do spike" em `spec/migracao-gitbook.md`):
1. **Marcador de exemplo.** O marcador do item 8 é o título do bloco de código do GitBook, `{% code title="<caminho>:<marcador>" %}`, que o leitor vê. O item 8 já previa essa troca, e a regra dos exemplos não muda. Antes do código, o GB4 testa se o bloco sobrevive à importação e à escrita pelo MCP, e se o comentário HTML do topo da página do catálogo sobrevive ao sync. Se o bloco não sobreviver, o marcador volta ao gate.
2. **Instruções injetadas.** Aceitas. O bloco do GitBook fica no `AGENTS.md` e no `CLAUDE.md`, entre `<!-- gitbook-agent-instructions:start -->` e `<!-- gitbook-agent-instructions:end -->`. O texto fica registrado no repositório e é revisado no PR, como qualquer mudança.
3. **Um space por pasta.** *Substituída pela decisão 8.* O manual segue o formato do GitBook, como no spike. Isso muda três itens da Decisão:
   - **item 3:** o espaço público passa a ser uma seção `Manual` do site, com um space público por pasta do manual, todos ligados à `main`. O rascunho passa a ser um só site, privado e não publicado, com a mesma seção e os mesmos spaces do público, ligados à branch do lote (decisão 7). No começo de cada lote com passe, o usuário religa o rascunho. O resto do item continua;
   - **item 4:** a configuração é o `gitbook-docs.yaml`, em `Documentation~/manual/`, com uma entrada por space (`content.directory` e `installationProjectDirectory`). A pasta continua `Documentation~/manual/`, e nada fora dela é publicado;
   - **item 5:** cada space tem o seu `SUMMARY.md`, na pasta dele, e cada página aparece uma vez, no sumário do space dela. O resto do item vale para cada sumário. A primeira condição do item 10 passa a ser lida por space e falha também com pasta do manual sem space no `gitbook-docs.yaml`.

   Cada pasta é um grupo do guia do manual (decisão 6).
4. **`README.md` na primeira página.** Aceito. A primeira página de cada space se chama `README.md` no repositório. As guardas do item 10, o `DocUrls` do item 9 e os links seguem esse nome. Com a decisão 8, só a página inicial muda de nome: o `index.md` vira o `README.md` da raiz do manual. O `referencia/` não é mais raiz de space, então o `catalogo.md` mantém o nome, e a página gerada do item 7 não muda de caminho.
5. **Pontos não conferidos.** O usuário conferiu que o Mermaid aparece desenhado no site publicado. A condição do item 11 está cumprida, e a regra dos diagramas não volta ao gate. O sync da `main` para o GitBook num PR e o plano gratuito da organização ficam para o GB9. Os outros pontos da lista "Não conferido" ficam com os tickets do design doc (GB7, GB9 e GB10).
6. **Uma pasta por grupo.** *Substituída pela decisão 9.* Pedia que as páginas fossem, por movimento puro, para uma pasta por grupo do guia (GB12 e GB13), para que cada space fosse um grupo.
7. **Um rascunho só.** *Resolvida pela decisão 8.* O rascunho do item 3 é um site só, e não um por pasta. Como ele fica com o Git Sync, que é por space, é a Q8 do design doc, e o GB9 o confere.
8. **Um space só (Q8 do design doc, mais tarde no mesmo gate).** Substitui a decisão 3. O manual inteiro é um só space, com raiz em `Documentation~/manual/`. Assim:
   - **item 3:** o público é uma seção `Manual` com um space público, ligado à `main`. O rascunho é um site privado e não publicado, com um space, ligado à branch do lote. A cada lote com passe, o usuário religa esse space uma vez. O resto do item continua;
   - **item 4:** a configuração é o `gitbook-docs.yaml`, em `Documentation~/manual/`, com um space só, se o schema pedir o arquivo para um site de um space. O GB2 confere isso antes de escrever, e também se o mesmo arquivo serve aos dois sites. A pasta continua `Documentation~/manual/`, e nada fora dela é publicado;
   - **item 5:** vale como escrito. Há um só `SUMMARY.md`, na raiz do manual, e os grupos do guia são títulos de grupo dentro dele. A pasta de uma página não precisa bater com o grupo dela. A primeira condição do item 10 volta a valer como escrita, sem a leitura por space.
9. **As pastas de hoje (Q9 do design doc).** Substitui a decisão 6. Com um space só (decisão 8), o grupo vem do título de grupo no `SUMMARY.md`. As páginas ficam nas pastas de hoje, e os tickets GB12 e GB13 saem do design doc.

## Relacionadas

- 0001 (pastas), 0002 (fluxo e papéis), 0005 (trava), 0007 (documentação e passe), 0025 (substituída), 0027 (projeto na raiz), 0040 (catálogo e API), 0041 (exemplos e mapa), 0042 (tamanho do ticket).
