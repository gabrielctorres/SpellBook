# Design doc: Migração do manual para o GitBook
Épico: `epic:migracao-gitbook` · Backlog: B-042 (com B-019g, B-035 e B-031) · ADRs: 0044 (Aceita) · Status: aprovado no gate de 2026-11-04 (Q1 a Q6); resultado do spike aprovado no gate de 2026-11-05 (Q7 a Q9 respondidas)

## Problema
O manual é publicado pelo MkDocs no GitHub Pages, junto com uma referência da API em DocFX. O usuário decidiu (2026-11-04, B-042) publicar o manual no GitBook, pelo Git Sync, tirar a API gerada e deixar os agentes escreverem o manual pelo MCP do GitBook. Quem usa o pacote passa a ter só o manual e os exemplos, no GitBook. Para isso, o repositório e o time precisam:
- de um layout que o Git Sync leia;
- de um sumário (`SUMMARY.md`) no lugar do `nav`;
- de outro jeito de pôr os exemplos dos testes nas páginas, porque o GitBook não inclui arquivo;
- de guardas que façam o papel do `mkdocs build --strict`;
- de um fluxo em que o agente escreve no GitBook sem que o texto entre na `main` por fora do PR e das guardas.

O botão de ajuda do Inspector (`[HelpURL]`) também precisa continuar abrindo a seção certa.

## Critérios de aceite
1. **CA1.** O manual está publicado no GitBook, no subdomínio `gitbook.io`, numa seção `Manual` com um só space público, sincronizado com a `main` e com raiz em `Documentation~/manual/`. Um só `SUMMARY.md`, na raiz do manual, tem como títulos de grupo os grupos do guia do manual que têm página, na ordem do guia, e cada página aparece nele uma vez. Nenhum documento interno (`prd/`, `spec/`, `adr/`, backlog) é publicado.
2. **CA2.** O que o site público mostra é o que está commitado na `main`, na pasta do manual. Nenhum agente escreve no space público: os agentes escrevem só no space do site de rascunho, ligado à branch do lote.
3. **CA3.** No Inspector, o botão de ajuda de qualquer componente ou asset do pacote abre a página e a seção do tipo no GitBook. O `documentationUrl` do `package.json` abre a página inicial.
4. **CA4.** Todo bloco C# do manual tem marcador de exemplo e é igual à seção atual do arquivo marcado. **SpellBook > Documentação > Gerar catálogo e exemplos** atualiza os blocos e o catálogo. A guarda falha:
   - com bloco diferente da seção;
   - com marcador para arquivo ou seção que não existe;
   - com bloco C# sem marcador;
   - com inclusão do Sandbox fora da página dele.
5. **CA5.** Uma guarda `Documentacao` falha nos casos abaixo, como o `mkdocs build --strict` falhava:
   - página fora do `SUMMARY.md`, repetida nele, ou entrada do sumário sem arquivo;
   - link relativo para arquivo ou âncora que não existe, ou link absoluto para o próprio site;
   - sobra de sintaxe do MkDocs (`{#`, `!!!`, `???`, `--8<--`).
6. **CA6.** Os diagramas Mermaid do manual aparecem desenhados no site, cada um com a legenda de uma frase.
7. **CA7.** O repositório não tem mais DocFX, `api/`, mapa da Unity, impressão digital, `.config/dotnet-tools.json`, `mkdocs.yml`, `requirements-docs.txt`, `docs.yml` nem Python na documentação. A suíte `Documentacao` passa sem o SDK do .NET e sem Python. O catálogo é gerado sem link para a API, com a guarda byte a byte.
8. **CA8.** As 16 páginas migradas têm as mesmas seções e o mesmo texto de antes. Mudam só a sintaxe (hints no lugar de admonitions, títulos sem `{#id}`), os links e os blocos expandidos.
9. **CA9.** O `AGENTS.md`, o guia do manual, as skills `/implementar`, `/revisao` e `/commit` e os agentes `controlador`, `examinador` e `fabrica` descrevem o passe de documentação com o GitBook e o MCP, sem `mkdocs` nem `docfx`.
10. **CA10.** No passe de documentação, a Fábrica escreve uma página num change request no space do site de rascunho, pelo MCP. Depois do merge, o commit do GitBook está na branch do lote, a geração preenche os blocos C#, as guardas `Documentacao` passam, e a página só chega ao site público pelo PR do lote. A Fábrica tem na lista `tools` só as ferramentas do MCP do item 13 da 0044.

## Cenário no Sandbox
Com o Sandbox importado (`Packages/com.spellbook/Samples~/Sandbox`) e o site público e o de rascunho sincronizados:
1. Abra a cena, selecione o objeto do `SandboxDirector` e clique no ícone de ajuda do componente. O navegador abre a página do Sandbox no GitBook, na seção do `SandboxDirector` (CA3).
2. Na mesma página, os blocos C# mostram o código atual de `Samples~/Sandbox/Scripts/`, e o diagrama da cena aparece desenhado (CA4, CA6).
3. Mude uma linha marcada de um exemplo em `Tests/` e rode a suíte `Documentacao`: a guarda do exemplo falha. Rode **Gerar catálogo e exemplos**, e ela volta a passar (CA4).
4. No space do site de rascunho, a Fábrica abre um change request que acrescenta um parágrafo e um marcador com bloco vazio na página do Sandbox, e o junta. O commit aparece na branch do lote, a geração preenche o bloco e as guardas passam. O site público não muda até o merge do PR (CA2, CA10).
5. No site público, percorra o sumário do space da seção `Manual`: os grupos do guia na ordem do guia, nenhuma página repetida, nada de `spec/` ou `adr/` (CA1).

## Fora do escopo
- Reescrever o texto das páginas. A migração mexe só na sintaxe, nos links e nos exemplos (CA8). O texto novo continua nos tickets do passe (MN4 a MN36).
- A conversão das páginas pelo MCP. A migração em si (GB2 a GB7) é feita no repositório, antes de o fluxo do MCP valer.
- Tradução do manual.
- Versões do manual por release do pacote.
- Uso do MCP para estatísticas do site, membros ou plano.

## Decisões do épico
- **Respostas do gate de 2026-11-04 (aprovadas).** Q1: o catálogo fica, gerado e sem link para a API. Q2: a pasta continua `Documentation~/manual/` se o GB1 mostrar que o Git Sync aceita o til; se não aceitar, vai para `docs/` na raiz. Q4: subdomínio `gitbook.io`. Q5: remoção completa do DocFX, de `api/`, do mapa da Unity, da impressão digital, do `docs.yml`, do `mkdocs.yml` e do Python. Q3: os agentes editam pelo MCP. Q6: dois espaços, o público ligado à `main`, sem agente escrevendo, e o de rascunho ligado à branch do lote, onde a Fábrica edita por change request (0044, itens 3 e 13).
- **O spike vem primeiro.** Os pontos não confirmados (ADR 0044, "Em aberto") são verificados no GB1. Até lá, eles não são tratados como fato. Os GB2, GB4, GB5, GB6 e GB10 esperam o resultado. O resultado e as decisões do gate de 2026-11-05 estão em "Resultado do spike", abaixo, e na emenda de 2026-11-05 da 0044.
- **A migração entra num lote só.** O site do GitHub Pages fica no ar até o merge do lote. Por isso, o `docs.yml` e o `mkdocs.yml` saem no GB2, e o PR do lote não roda mais o build do MkDocs no meio da conversão.
- **Ordem dos geradores.** O GB3 (sem a API) vem antes do GB4 (exemplos), para que a entrada de menu única nasça já sem o DocFX.
- **O fluxo do MCP só vale depois do GB9.** Até lá, as páginas mudam no repositório, como hoje.
- **O passe de documentação continua.** Os MN4 a MN36 que ainda não foram feitos, e o EX3 e o EX6 (`modulos/gas.md`, `dano-escudo-e-morte.md` e a migração 2→3, ainda pendentes na guarda), entram depois do GB9, já escritos pelo MCP, na sintaxe do GitBook e com o marcador do GB4. O MN1 (o `nav` do MkDocs) fica como histórico. O MN36 passa a conferir pelas guardas, não pelo build.
- **Organização dos asmdefs (B-040).** O AS2 leva para `Assets/` só o que sobra da geração (catálogo e exemplos) e vem depois do GB4.

## Tickets
Tamanho pela 0042, estimado aqui. A Fábrica confirma no Detalhe. Os arquivos de texto contam só nos 10 arquivos. Remoção pura e os blocos expandidos pelo gerador não contam.

| Ticket | Objetivo | CA | Depende de | Unity |
|---|---|---|---|---|
| GB1 | **Spike** (passo do usuário, com o Codex preparando a branch `spike/gitbook`): um espaço de teste ligado à branch, com três páginas copiadas do manual e raiz em `Documentation~/manual/`. As páginas têm um título de tipo, um título com acento, um hint, um bloco `mermaid` e um marcador em comentário HTML. Pelo MCP, o usuário (ou a Fábrica, com a autenticação dele) abre e junta um change request que muda uma página, cria outra e põe um link entre elas, e confere o commit, o `SUMMARY.md`, o link e o marcador no sync. Responde cada ponto do "Em aberto" da 0044. O resultado vai para "Resultado do spike", abaixo, e para uma emenda da 0044. Sem merge na `main` | — | — | não |
| GB2 | Layout e sumário (decisão 8 do gate de 2026-11-05). O primeiro passo do Detalhe, antes de escrever, é conferir no schema do `gitbook-docs.yaml` (`https://api.gitbook.com/gitbook-docs.yaml`) se um site com um space só precisa do arquivo e se o mesmo arquivo serve ao site público e ao de rascunho. Se o arquivo for preciso, ele fica em `Documentation~/manual/`, com um space só e raiz na pasta do manual; se não for, o GB2 não o cria. Depois: um `SUMMARY.md` na raiz do manual, tirado do `nav`, com os grupos do guia que têm página como títulos de grupo, na ordem do guia; o `index.md` passa a se chamar `README.md` (movimento puro, `R100`), a primeira página do space. As outras páginas ficam nas pastas de hoje. Remove o `mkdocs.yml`, o `requirements-docs.txt` e o `docs.yml` (remoção pura). 2 arquivos, ou 1 sem o `gitbook-docs.yaml` | CA1, CA7 | GB1 | não (Codex) |
| GB3 | Tirar a API: apaga `api/`, `.config/` e o `DocfxConfig`, o `ReferenceFingerprint` e o `UnityXrefMap`, com os testes deles (remoção pura). O `ReferenceGenerator` passa a gerar só o catálogo, a página do catálogo perde o link para a API e o `{#id}`, e o `ReferenceUpToDateTests` e o `DocumentationCategoryTests` ficam só com o catálogo. ~200 linhas, ~6 arquivos | CA7 | — | sim |
| GB4 | Exemplos expandidos. O marcador é o bloco `{% code title="<caminho>:<marcador>" %}` (decisão 1 do gate de 2026-11-05). O primeiro passo do Detalhe, antes do código, é testar no GitBook se esse bloco sobrevive à importação e à escrita pelo MCP. Se não sobreviver, o GB4 para e o marcador volta ao gate. Depois: o expansor de marcadores, a entrada **Gerar catálogo e exemplos**, o `ManualCodeExamplesTests` reescrito para o marcador e a expansão nas 7 páginas que já usam inclusão. ~350 linhas e ~11 arquivos: 4 de código e as 7 páginas, porque trocar as linhas `--8<--` pelo marcador é escrito à mão e conta. O código expandido não conta. Fica dentro da exceção de +30%, com o motivo no Detalhe. Se passar disso, a troca das páginas vira um ticket próprio | CA4 | GB1, GB3 | sim |
| GB5 | Sintaxe das páginas, parte 1: o `README.md` da raiz (o antigo `index.md`), `inicio/` (3), `ferramentas/` (3), `exemplos/` (1) e `regras/` (1). Títulos sem `{#id}`, hints, Mermaid e links pelo resultado do GB1. Nenhuma página liga hoje para o `index.md` (conferido por busca em 2026-11-05), então a troca de nome do GB2 não pede correção de link. 9 arquivos | CA6, CA8 | GB2, GB4 | não (Codex) |
| GB6 | Sintaxe das páginas, parte 2: as 6 páginas de `modulos/`. 6 arquivos | CA6, CA8 | GB5 | não (Codex) |
| GB7 | URLs e guardas de link: `DocUrls.Base` e as constantes no formato de URL do GitBook, o `documentationUrl` do `package.json` e o `ManualLinksTests` com a âncora do título. Inclui a guarda nova do sumário (o `SUMMARY.md` da raiz do manual), dos links relativos, do link absoluto para o próprio site, das âncoras e da sintaxe do MkDocs. Pela decisão 4 do gate de 2026-11-05, a guarda e o `DocUrls` acham a página inicial no `README.md` da raiz, e não no `index.md` (`ManualLinksTests.cs:105`). As outras páginas mantêm o nome. ~350 linhas, ~5 arquivos | CA3, CA5 | GB6 | sim |
| GB8 | Processo sem MkDocs: `AGENTS.md`, `guia-do-manual.md` (partes 4, 6, 7, 8 e 9), `Documentation~/README.md`, o `README.md` da raiz (link do manual), as skills `/implementar` e `/revisao` e os agentes `controlador` e `examinador`. Tira `mkdocs` e `docfx` e põe a geração e as guardas. 8 arquivos | CA9 | GB7, GB11 | não (Codex) |
| GB11 | Emendas das ADRs (Desenhista, só texto): emenda curta em cada ADR afetada, apontando para a 0044. **0042:** "um ticket, um commit" continua, salvo no passe de documentação, em que o ticket tem o commit do change request (e o de cada correção) e o do `/commit`, e o teto soma os dois; "a entrada do `nav`" vira "a entrada do `SUMMARY.md`"; a saída de gerador com guarda passa a ser a página do catálogo e os blocos C# expandidos. **0002:** a Fábrica do passe cria o commit do change request no rascunho; o `/commit` continua o único passo que commita no checkout, e o merge na `main` continua do usuário. **0007, 0040 e 0041:** as mudanças listadas em "Consequências" da 0044. **0025:** status "Substituída por 0044". Mais a linha de cada uma no `adr/README.md`. 7 arquivos | CA9 | — | não (Desenhista) |
| GB10 | Fluxo do MCP: a lista `tools` do agente `fabrica` com as ferramentas do item 13 da 0044, pelo nome que o GB1 levantou; o passo do script da fila que traz o commit do change request ao checkout do ticket, só por avanço rápido (skill `/commit` ou a que o Detalhe indicar); e o fluxo do change request no `AGENTS.md`, no guia do manual, no `/implementar` e nos agentes `fabrica` e `controlador`. ~6 arquivos | CA9, CA10 | GB8, GB1 | não (Codex) |
| GB9 | Ligar (passo do usuário): depois do merge do lote, ligar o space público à `main` e travar o conteúdo dele, se der; criar um site de rascunho, privado e não publicado, com um space só, com raiz em `Documentation~/manual/` e ligado à branch do próximo lote com passe (decisão 8 do gate de 2026-11-05). A cada lote com passe, o usuário religa esse space uma vez. Conferir se o plano gratuito permite o site privado e se religar o space a cada lote preserva o histórico; conferir o sync da `main` para o GitBook quando um PR entra e o plano gratuito da organização (decisão 5 do gate de 2026-11-05); desligar o GitHub Pages e percorrer o cenário. Sem diff | CA1, CA2, CA3, CA6, CA10 | GB10 e merge do lote | não |

O GB11 entra no começo do lote, antes do GB8 e do GB10, porque o `AGENTS.md` segue as ADRs ("se divergir, vale a ADR"). As emendas descrevem o fluxo que passa a valer depois do GB9.

Do GB2 até o GB7, o `ManualLinksTests` fica vermelho: ele procura a página inicial no `index.md`, que o GB2 renomeia para `README.md`. O GB7 o reescreve. Acontece o mesmo com as âncoras depois do GB5. A revisão dos tickets comuns do lote roda com `!Documentacao`.

## Perguntas ao usuário
Nenhuma pergunta aberta. As Q1 a Q6 foram respondidas no gate de 2026-11-04 (ver "Decisões do épico"). As perguntas que o GB1 abriu e as Q7 a Q9 foram respondidas no gate de 2026-11-05: são as decisões 1 a 9 de "Decisões do gate de 2026-11-05", em "Resultado do spike".

## Resultado do spike
GB1, 2026-11-05. Branch `spike/gitbook`: `7160b42` (conteúdo), `6d1255a` (`gitbook-docs.yaml`) e `934c949` (commit `GITBOOK-1`, gravado pelo GitBook no merge do change request). Organização do GitBook `Rook Studio`, no plano gratuito. Um ponto do "Em aberto" da 0044 por linha. A resposta completa está na emenda de 2026-11-05 da 0044.

| Ponto da 0044 | O que o spike mostrou | Afeta |
|---|---|---|
| Caminho com til | Aceito. O Git Sync importou com o diretório `./Documentation~/manual` e `./Documentation~/manual/modulos`. A pasta fica em `Documentation~/manual/` (Q2) | GB2 não move a pasta |
| Arquivo de configuração | Vale o `gitbook-docs.yaml` (schema `https://api.gitbook.com/gitbook-docs.yaml`), e não o `.gitbook.yaml`. Ele ficou em `Documentation~/manual/gitbook-docs.yaml`. O spike montou um site com uma seção (`Manual`) e 4 spaces, cada um com a sua pasta (`content.directory`) e o seu Git Sync (`installationProjectDirectory`) | GB2; decisão 3 |
| `index.md` ou `README.md` | O GitBook renomeou `modulos/gas.md` para `modulos/README.md`: a primeira página do space vira `README.md`. O `index.md` e o conflito ao editar o `README.md` não foram testados | GB2, GB7; decisão 4 |
| Regra das âncoras | Não conferida. O título com acento chegou ao GitBook, mas o slug gerado não foi medido | GB7 |
| URL e formato do link | Um link escrito pelo MCP como `/pages/<caminho>` foi gravado no sync como link relativo `.md` (`efeitos-de-exemplo.md`, `./`). A URL publicada a partir do caminho do arquivo não foi conferida | GB5, GB7 |
| Mermaid | O bloco `mermaid` chegou ao GitBook. Depois do spike, o usuário conferiu que ele aparece desenhado no site publicado | GB5; decisão 5 |
| Comentário HTML | **Não sobrevive.** O GitBook apaga o `<!-- snippet: ... -->` na importação e quando o MCP escreve a página. O `{% code title %}` não foi testado | GB4 testa antes do código; decisão 1 |
| Regravação do Markdown | A escrita do GitBook tirou o BOM (neutro). O resto da regravação e o `git diff --check` no commit dele não foram conferidos | GB10 |
| `SUMMARY.md` | O GitBook criou um `SUMMARY.md` dentro de cada space (`modulos/SUMMARY.md`, com `# Table of contents` e uma lista), e não um só na raiz do manual | GB2, GB7; decisão 3 |
| Ferramentas do MCP | O fluxo funcionou: criar o change request, `update_page`, `insert_page`, link e merge. O nome completo das ferramentas na sessão da Fábrica e o limite do `invoke_operation` pela permissão não foram conferidos | GB10 |
| Merge do change request | Gerou um commit, `GITBOOK-1`, na `spike/gitbook`, e não na `main`. Nesse commit, o GitBook também injetou 22 linhas de "GitBook Documentation Editing" entre `<!-- gitbook-agent-instructions:start -->` e `<!-- gitbook-agent-instructions:end -->` no `AGENTS.md` e no `CLAUDE.md` da raiz. Autor do commit e conflito não foram conferidos | GB9, GB10; decisão 2 |
| Rascunho privado no plano gratuito | Não conferido | GB9 |
| Travar o público e conta só do rascunho | Não conferido | GB9 |
| Autenticação do MCP na sessão da Fábrica | Não conferida | GB10 |
| Preço e limites do plano gratuito | Não conferidos. A organização está no plano gratuito | GB9; decisão 5 |
| (fora da lista) Sync da `main` para o GitBook num PR | Não conferido | GB9; decisão 5 |

### Decisões do gate de 2026-11-05
1. **Marcador de exemplo.** O GB4 usa o bloco `{% code title="<caminho>:<marcador>" %}`, a alternativa que o item 8 da 0044 já previa. A sobrevivência dele ao sync não foi testada. Por isso, o primeiro passo do Detalhe do GB4 é esse teste, antes do código. O mesmo teste olha o comentário HTML do topo da página do catálogo (`TypeCatalogPage`), que pode sumir se o GitBook regravar a página.
2. **Instruções injetadas.** Aceitas. O bloco que o GitBook escreve entre `<!-- gitbook-agent-instructions:start -->` e `<!-- gitbook-agent-instructions:end -->` fica no `AGENTS.md` e no `CLAUDE.md`. O texto fica registrado no repositório e é revisado no PR, como qualquer mudança.
3. **Um space por pasta.** *Substituída pela decisão 8.* O manual segue o formato do GitBook, como no spike: uma seção `Manual`, um space por pasta e um `SUMMARY.md` por space. O `gitbook-docs.yaml` fica em `Documentation~/manual/`. Isso muda o CA1, o CA5, o GB2 e os itens 3, 4 e 5 da 0044 (emenda de 2026-11-05). Cada pasta é um grupo do guia (decisão 6).
4. **`README.md` na primeira página.** Aceito, com as guardas ajustadas. A primeira página de cada space se chama `README.md` no repositório. Com a decisão 8, isso vale só para a página inicial: o GB2 renomeia o `index.md` para `README.md`, e o GB7 ajusta a guarda e o `DocUrls`. O GB5 e o GB6 não têm link a corrigir, porque nenhuma página liga para o `index.md`. Como o `referencia/` deixa de ser raiz de space, o `catalogo.md` mantém o nome, e o `ReferenceGenerator` continua gravando em `manual/referencia/catalogo.md` sem ajuste. O GB9 confere, no primeiro sync, que o GitBook não renomeia nenhuma outra página.
5. **Pontos não conferidos.** O usuário conferiu que o Mermaid aparece desenhado no site publicado, então o item 11 da 0044 está cumprido. O sync da `main` para o GitBook num PR e o plano gratuito da organização ficam para o GB9. Os outros pontos ficam nos tickets da tabela acima.
6. **Uma pasta por grupo (Q7).** *Substituída pela decisão 9.* Pedia que as páginas fossem, por movimento puro, para uma pasta por grupo do guia (tickets GB12 e GB13), para que cada pasta fosse um space.
7. **Um rascunho só.** *Resolvida pela decisão 8.* No lugar de um rascunho por pasta, há um só site de rascunho, privado e não publicado, com a mesma seção `Manual` e os mesmos spaces do público, ligados à branch do lote. O GB9 o cria e confere. Como isso fica com o Git Sync por space está na Q8.
8. **Um space só (Q8, mais tarde no mesmo gate).** Substitui a decisão 3 e resolve a 7. O manual inteiro é um só space, com raiz em `Documentation~/manual/`:
   - **público:** uma seção `Manual` com um space público, ligado à `main`;
   - **rascunho:** um site privado e não publicado, com um space, ligado à branch do lote. A cada lote com passe, o usuário religa esse space uma vez (GB9);
   - **sumário:** um `SUMMARY.md` na raiz do manual, com os grupos do guia que têm página como títulos de grupo, na ordem do guia. As pastas não precisam bater com os grupos (decisão 9);
   - **configuração:** o `gitbook-docs.yaml` em `Documentation~/manual/`, com um space só, se o schema pedir o arquivo. O GB2 confere isso antes de escrever, e também se o mesmo arquivo serve aos dois sites.

   Isso muda o CA1, o CA2, o CA5, o CA10, o cenário, o GB2, o GB5, o GB6, o GB7 e o GB9, e a emenda de 2026-11-05 da 0044.
9. **As pastas de hoje (Q9).** Substitui a decisão 6. O GB12 e o GB13 saem. Com um space só (decisão 8), o grupo vem do título de grupo no `SUMMARY.md`, e as páginas ficam onde estão: o `README.md` da raiz (o antigo `index.md`), `inicio/`, `modulos/`, `ferramentas/`, `exemplos/`, `regras/` e `referencia/`. O GB2 e o GB4 não dependem de mover pastas, e o GB5 e o GB6 seguem as pastas de hoje, como na tabela de tickets.

## Validação de uso
| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|

## GB3 — Tirar a API
### Detalhe (2026-11-04)
- CA: CA7 (a parte da API: DocFX, `api/`, mapa da Unity, impressão digital, `.config/dotnet-tools.json`; catálogo sem link para a API, com a guarda byte a byte). O `docs.yml`, o `mkdocs.yml` e o Python ficam no GB2.
- Escopo:
  - Remoção pura: `Documentation~/api/` inteira (`docfx.json`, `index.md`, `toc.yml`, `unity.xrefmap.yml`, `reference.sha256`, `reference/`), `Documentation~/.config/dotnet-tools.json`, `Packages/com.spellbook/Editor/Core/Documentation/DocfxConfig.cs`, `ReferenceFingerprint.cs`, `UnityXrefMap.cs` e os testes `Tests/Core/EditMode/Documentation/DocfxConfigTests.cs` e `UnityXrefMapTests.cs`, com os `.meta`.
  - `Editor/Core/Documentation/ReferenceGenerator.cs:13-17,20-22,36,61-94,122-173`: sai o `IDocfxMetadataRunner`, o `DotnetDocfxRunner`, a sobrecarga interna com o runner e as etapas de `api/`. O `Generate` escreve só `manual/referencia/catalogo.md`. O `MenuPath` continua **Gerar referência**; a entrada única **Gerar catálogo e exemplos** nasce no GB4.
  - `Editor/Core/Documentation/TypeCatalogPage.cs:19-22,36,50,59,80-109`: a introdução perde a frase do link da API, o título da página e os das seções perdem o `{#id}`, e a coluna **Tipo** mostra o nome sem link (saem `Anchor`, `AnchorOfName` e `ApiUrl`).
  - `Documentation~/manual/referencia/catalogo.md`: regenerado pelo menu (saída de gerador com guarda).
  - `Tests/Core/EditMode/Documentation/ReferenceGeneratorTests.cs`: reescrito para o catálogo só.
  - `Tests/Core/EditMode/Documentation/TypeCatalogPageTests.cs:43-60,67,75,83,95,105,108-120`: títulos sem âncora e tipo sem link.
  - `Tests/Core/EditMode/Documentation/ReferenceUpToDateTests.cs:12-15,38-125`: fica só `CommittedCatalog_MatchesGenerated`.
  - `Tests/Core/EditMode/Documentation/DocumentationCategoryTests.cs:17-20`: saem as quatro guardas da API.
  - `Packages/com.spellbook/CHANGELOG.md`, em **Quebras**: entrada da remoção dos tipos públicos `DocfxConfig`, `DocfxAssembly`, `ReferenceFingerprint` e `UnityXrefMap`, com a migração. A remoção já estava prevista no design doc (Q5), então não volta ao gate.
- Testes red (EditMode, `SpellBook.Core.Tests.EditMode`):
  - `ReferenceGeneratorTests.Generate_WritesCatalogFromTypePicker` e `ReferenceGeneratorTests.Generate_WritesOnlyTheCatalog` (hoje o `Generate` chama o `dotnet docfx` e escreve em `api/`);
  - `TypeCatalogPageTests.Render_PageTitleWithoutAnchor`, `Render_SectionTitleWithoutAnchor`, `Render_ClosedGenericBase_TitleInCSharpForm` e `Render_TypeNameWithoutLink`, mais os três testes de célula (`Render_EmptyCategory_ShowsRaiz`, `Render_EmptySummary_LeavesCellEmpty`, `Render_EscapesPipeAndNewline`) sem o `.html)` na linha esperada.
- Critério de pronto: os testes red passam; `ReferenceUpToDateTests`, `DocumentationCategoryTests`, `TypeCatalogTests` e `TypeCatalogSummaryTests` passam, com o `total` do XML conferido; nenhum `.cs` do pacote cita `DocfxConfig`, `ReferenceFingerprint`, `UnityXrefMap` ou `IDocfxMetadataRunner`; `Documentation~/api/` e `Documentation~/.config/` não existem; o `catalogo.md` não tem `{#` nem `](`.
  - `.gitignore:44-46` (consequência direta, ADR 0042, item 6): saem as linhas de `api/obj/` e `api/_site/`.
- Tamanho: 438 linhas medidas (adicionadas mais removidas, sem a remoção pura) em 8 arquivos (os 2 do Editor, os 4 testes, o `CHANGELOG.md` e o `.gitignore`; o catálogo é saída de gerador com guarda e não conta). Exceção: nenhuma.
- Commit: `chore(docs)!: tira o DocFX, a API gerada e o mapa da Unity`
