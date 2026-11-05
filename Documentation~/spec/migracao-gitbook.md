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
| GB4 | Exemplos expandidos. O marcador é o bloco `{% code title="<caminho>:<seção>,<seção>,..." %}`, com as seções do mesmo arquivo na ordem do bloco (decisões 1 e 10 do gate de 2026-11-05). O primeiro passo do Detalhe, antes do código, é testar no GitBook se esse bloco sobrevive à importação e à escrita pelo MCP. Se não sobreviver, o GB4 para e o marcador volta ao gate. Depois: o expansor de marcadores, a entrada **Gerar catálogo e exemplos**, o `ManualCodeExamplesTests` reescrito para o marcador e a expansão nas 7 páginas que já usam inclusão. ~350 linhas e ~11 arquivos: 4 de código e as 7 páginas, porque trocar as linhas `--8<--` pelo marcador é escrito à mão e conta. O código expandido não conta. Fica dentro da exceção de +30%, com o motivo no Detalhe. Se passar disso, a troca das páginas vira um ticket próprio | CA4 | GB1, GB3 | sim |
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
1. **Marcador de exemplo.** *Formato ajustado pela decisão 10.* O GB4 usa o bloco `{% code title %}`, a alternativa que o item 8 da 0044 já previa. A sobrevivência dele ao sync não foi testada. Por isso, o primeiro passo do Detalhe do GB4 é esse teste, antes do código. O mesmo teste olha o comentário HTML do topo da página do catálogo (`TypeCatalogPage`), que pode sumir se o GitBook regravar a página.
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
10. **Título com a lista de seções (opção A, no Detalhe do GB4).** O título do bloco é `{% code title="<caminho>:<seção>,<seção>,..." %}`: uma ou mais seções do mesmo arquivo, separadas por vírgula, na ordem em que entram no bloco. Um título por seção não serviria, porque 27 dos 33 blocos de hoje juntam mais de uma seção num bloco só. Isso ajusta a decisão 1 e o item 8 da 0044 (emenda de 2026-11-05).

## Validação de uso
| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|

## GB2 — Layout e sumário
### Detalhe (2026-11-05)
- CA: CA1 (o `SUMMARY.md` único na raiz do manual, com os grupos do guia na ordem do guia e cada página uma vez; a configuração do space) e CA7 (a parte do `mkdocs.yml`, do `requirements-docs.txt`, do `docs.yml` e do Python). A publicação e o site de rascunho são do GB9; a guarda do sumário é do GB7.
- Verificação do `gitbook-docs.yaml`, feita antes do escopo:
  - O schema (`https://api.gitbook.com/gitbook-docs.yaml`, lido em 2026-11-05) não dispensa o arquivo para um site de um space. Ele se descreve como o contrato do Git Sync para a estrutura do site, fica na raiz do diretório do projeto e aceita tanto um space solto na raiz quanto uma seção com um space. Como o CA1 pede a seção `Manual`, a estrutura tem uma `section` com um `space`. A emenda de 2026-11-05 da 0044 (item 2) registra que o Git Sync do site exige o arquivo, e o spike (`6d1255a`) importou por ele. O Git Sync por space, sem o arquivo e com a seção montada na interface, fica fora do schema e não foi testado. Por isso o GB2 cria o arquivo.
  - O mesmo arquivo serve aos dois sites. O schema não tem campo de visibilidade nem de publicação: privado e não publicado são ajustes do site na interface (GB9). O `key` precisa ser único só dentro do arquivo, e o `content.directory: ./` é lido a partir do diretório do projeto de cada Git Sync (`Documentation~/manual/` nos dois). O rascunho mostra o mesmo título e a mesma seção `Manual`, com um space só, o que a decisão 8 permite. O que o schema não responde (dois sites ligados ao mesmo arquivo no mesmo repositório, e se o GitBook regrava o arquivo) o GB9 confere no primeiro sync. No spike, o commit `GITBOOK-1` (`934c949`) não mexeu no arquivo.
- Escopo:
  - `Documentation~/manual/gitbook-docs.yaml` (novo). O primeiro bloco do spike, sem os outros 3 spaces:
    ```yaml
    $schema: https://api.gitbook.com/gitbook-docs.yaml
    site:
      title: Manual do SpellBook
      structure:
        - type: section
          key: section-manual
          title: Manual
          path: manual
          default: true
          children:
            - type: space
              key: space-manual
              title: Manual do SpellBook
              path: spellbook
              default: true
              content:
                directory: ./
    ```
    O GB7 tira dos `path` o formato da URL do `DocUrls`, e o GB9 a confere no site publicado.
  - `Documentation~/manual/SUMMARY.md` (novo), tirado do `nav` de `Documentation~/mkdocs.yml:20-44`. Os títulos de grupo são os do guia (`guia-do-manual.md:115`), na ordem dele, sem o `Guias`, que não tem página. O rótulo é o título `#` da página sem o `{#id}`. O cabeçalho é o que o GitBook grava (`modulos/SUMMARY.md` em `934c949`). O link da API (`mkdocs.yml:41`) sai, porque o GB3 tira a API:
    ```markdown
    # Table of contents

    ## Começar

    * [SpellBook](README.md)
    * [Instalação](inicio/instalacao.md)

    ## Sistemas de habilidade

    * [GAS](modulos/gas.md)
    * [Dano, escudo e morte](modulos/dano-escudo-e-morte.md)

    ## Sistemas de apoio

    * [Eventos](modulos/eventos.md)
    * [Tags](modulos/tags.md)
    * [Tempo](modulos/tempo.md)
    * [Pooling](modulos/pooling.md)

    ## Editor e ferramentas

    * [Inspector](inicio/inspector.md)
    * [Validador](ferramentas/validador.md)
    * [GAS Debugger](ferramentas/gas-debugger.md)
    * [Event Debugger](ferramentas/event-debugger.md)

    ## Exemplos

    * [Sample Sandbox](exemplos/sandbox.md)

    ## Referência

    * [Catálogo de tipos](referencia/catalogo.md)
    * [Conceitos](inicio/conceitos.md)
    * [Migração 2.x → 3.0](regras/migracao-2-para-3.md)
    ```
  - `Documentation~/manual/index.md` → `Documentation~/manual/README.md`: troca de nome sem mudar nenhum byte. O Codex renomeia no disco, e o `/commit` registra como `R100`.
  - Remoção pura: `Documentation~/mkdocs.yml`, `Documentation~/requirements-docs.txt` e `.github/workflows/docs.yml`.
  - `.gitignore:41-42` (consequência direta, ADR 0042, item 6): saem o comentário e a linha `/Documentation~/site/`, a saída do `mkdocs build`.
- O que referencia esses arquivos e o `nav`, e com qual ticket fica:
  - **GB2:** os três arquivos removidos, o `index.md` e o `.gitignore:41-42`. Nenhum outro arquivo de CI cita o MkDocs (`.github/workflows/` só tem o `docs.yml`). Nenhum script da fila e nenhum `.cs` lê o `mkdocs.yml` ou o `nav`. O `ManualNavTests` não existe.
  - **GB7:** `ManualLinksTests.cs:105` procura a página inicial em `index.md` e `:115` procura a âncora `{#id}`; os dois passam a usar o `README.md` e a âncora do título. A guarda nova do `SUMMARY.md` (página fora, repetida ou sem arquivo) faz o papel da validação do `nav` (`mkdocs.yml:45-50`). `DocUrls.cs:10`, `DocUrlsTests.cs:14` e `package.json:10` passam para a URL do GitBook. Entre o GB2 e o GB7, só a conferência por busca do critério de pronto abaixo confere o sumário.
  - **GB8:** `AGENTS.md:114,128` (o `mkdocs build --strict` e o `nav` nas conferências do Controlador); `guia-do-manual.md:113-117` (parte 6: o `nav` vira o `SUMMARY.md`), `:132`, `:141-142` (parte 7: o build), `:158-165` (parte 8: o build e a Action `docs`) e `:202,205` (parte 9: o `dedent_subsections` e o `check_paths`, junto com o marcador do GB4); `Documentation~/README.md:14` ("Site MkDocs (GitHub Pages)"); `README.md:18` (link do manual); `.claude/agents/controlador.md:3,14`, `examinador.md:19`, `.claude/skills/implementar/SKILL.md:17` e `revisao/SKILL.md:9,21`.
  - **GB11, e depois GB8:** `AGENTS.md:135` e `revisao/SKILL.md:14` ("a entrada do `nav`") seguem a emenda da 0042. As ADRs 0007, 0025, 0040 e 0041 citam o MkDocs e o `nav` e são do GB11. A Spec, o PRD, o backlog e as ADRs antigas ficam como histórico.
  - Até o GB8, o `AGENTS.md`, o guia e as skills mandam rodar o `mkdocs build --strict`, que não existe mais. Não tem efeito no lote, porque o passe de documentação só volta depois do GB9.
- Testes red: nenhum, porque o ticket não tem código. Guarda `Documentacao` que fica vermelha, como a Spec prevê: `ManualLinksTests.EveryDocUrlHasPage`. O `DocUrls.Base` é uma constante da classe, e a guarda o procura em `index.md`. Ela já está vermelha hoje pelas páginas que faltam (`modulos/times.md`, `cues.md` e `ui/`), e o GB2 soma a linha da página inicial. O GB7 a corrige. O `NoStubPages` lê o `SUMMARY.md` como página e passa, porque ele tem lista e não só títulos. O `EveryHelpUrlHasPageAndAnchor` não muda: nenhum `[HelpURL]` usa o `Base` puro. O ticket é do Codex e não roda a Unity; o Examinador confere essas guardas na `/revisao`.
- Critério de pronto (da raiz do repositório, sem Unity):
  - `Documentation~/mkdocs.yml`, `Documentation~/requirements-docs.txt`, `.github/workflows/docs.yml` e `Documentation~/manual/index.md` não existem;
  - `git hash-object Documentation~/manual/README.md` é igual a `git rev-parse HEAD:Documentation~/manual/index.md`;
  - cada página aparece uma vez no sumário, e cada entrada tem arquivo: `cd Documentation~/manual && diff <(find . -name '*.md' ! -name SUMMARY.md | sed 's|^\./||' | sort) <(grep -oE '\]\([^)]+\.md\)' SUMMARY.md | sed -E 's/^\]\(//; s/\)$//' | sort)` não mostra nada;
  - `grep '^## ' Documentation~/manual/SUMMARY.md` mostra, nesta ordem: Começar, Sistemas de habilidade, Sistemas de apoio, Editor e ferramentas, Exemplos, Referência;
  - o `gitbook-docs.yaml` é conferido à mão contra o schema: `site.title` e `site.structure` presentes; uma seção com `default: true`; um space com `default: true`, `key`, `title`, `path` e `content.directory`;
  - `git grep -n -E "mkdocs|requirements-docs|docs\.yml" -- .github .gitignore Documentation~/manual` não acha nada;
  - `git diff --check` limpo.
- Tamanho: 1 linha (o `R100`) em 3 arquivos (`gitbook-docs.yaml`, `SUMMARY.md` e `.gitignore`). Nenhum dos três é tipo que conta linha. A remoção pura e o `R100` não contam como arquivo. Exceção: nenhuma.
- Commit: `chore(docs): SUMMARY.md e gitbook-docs.yaml no lugar do MkDocs`

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

## GB4 — Exemplos expandidos
### Detalhe (2026-11-05)
- CA: CA4 (marcador em todo bloco C#, bloco igual à seção, **Gerar catálogo e exemplos**, guarda nos quatro casos) e a parte do CA7 sobre o catálogo gerado com a guarda byte a byte.
- Teste do marcador (primeiro passo): o Controlador mediu em 2026-11-05, num space de teste e pelo MCP, que o `{% code title="caminho:marcador" %}...{% endcode %}` sobrevive à escrita pelo MCP, e que o comentário HTML é apagado na importação e na escrita. A importação de um `.md` com o bloco pelo Git Sync não foi testada separadamente; o GB9 a confere no primeiro sync.
- Formato do marcador (opção A, aprovada pelo usuário em 2026-11-05): o título lista as seções de um mesmo arquivo na ordem do bloco, separadas por vírgula: `{% code title="<caminho>:<seção>,<seção>,..." %}`, a cerca ` ```csharp `, o código e `{% endcode %}`. O expansor junta as seções nessa ordem, cada uma sem o seu recuo comum, como o `dedent_subsections` fazia, então as páginas ficam iguais (CA8). Isso muda o formato da Spec e da 0044 de uma seção para uma lista. O texto no singular está em: Spec, `migracao-gitbook.md:65` (linha do GB4) e `:104` (decisão 1); 0044, `:51` (item 8, "um [marcador] por seção, na ordem em que aparecem no bloco"), `:56` (item 8, `{% code title="<caminho>:<seção>" %}`) e `:186` (emenda, decisão 1). O ajuste do texto é do Desenhista, a pedido do Controlador. A parte 9 do guia do manual (`guia-do-manual.md:169-208`, sobre o `--8<--`) é do GB8.
- Catálogo (decisão da Fábrica, aprovada pelo Controlador em 2026-11-05, dentro do escopo): o `TypeCatalogPage` deixa de gerar o comentário HTML do topo, porque o GitBook o apaga, e a página não ganha aviso visível. A guarda continua byte a byte contra o `Render`, sem ler o comentário. A regra de não editar continua no item 13 da 0044 e na mensagem da guarda.
- Escopo:
  - `Packages/com.spellbook/Editor/Core/Documentation/ReferenceGenerator.cs:17-18`: o `MenuPath` passa a ser `SpellBook/Documentação/Gerar catálogo e exemplos`. `:27,52-59`: o `Generate` escreve o catálogo e expande os blocos de todas as páginas de `manual/`, gravando só a página que muda. Se algum marcador estiver quebrado, lança exceção com a lista; o menu a registra no log, e o `-executeMethod` sai com 1. Entra o `ExpandExamples(page, readSource, problems)` público, a etapa de exemplos da mesma geração (0044, item 8), que a guarda usa. O expansor fica na classe da geração para o ticket caber no teto de arquivos (ver Tamanho).
  - `Editor/Core/Documentation/TypeCatalogPage.cs:15-17,34`: sai o `Notice`, e a página começa no `# Catálogo de tipos`.
  - `Documentation~/manual/referencia/catalogo.md`: regenerado (saída de gerador com guarda, não conta).
  - `Tests/Repository/EditMode/ManualCodeExamplesTests.cs`: reescrito para o marcador. Mantém os três nomes de teste, que estão na lista do `DocumentationCategoryTests`: `NoHandWrittenCSharpBlocks` (bloco C# sem `{% code title %}` logo antes, fora das pendências), `SnippetsPointToExistingSections` (os problemas que o `ExpandExamples` acusa: arquivo, seção, `start`/`end`, bloco C#, `{% endcode %}`) e `SnippetSourcesAreCompiledCode` (Sandbox só na página dele). Ganha `ExpandedBlocksMatchSections` (página igual à saída do `ExpandExamples`), que fica na categoria pela classe.
  - `Tests/Repository/EditMode/Documentation/ReferenceGeneratorTests.cs`: testes do `ExpandExamples` e da expansão no `Generate`.
  - `Tests/Repository/EditMode/Documentation/TypeCatalogPageTests.cs:12-21`: `Render_StartsWithGeneratedNotice` vira `Render_StartsWithPageTitle`; `:47`: `Render_PageTitleWithoutAnchor` passa a esperar o título na primeira linha.
  - `Tests/Repository/EditMode/Documentation/ReferenceUpToDateTests.cs:15` (consequência direta): a instrução cita o menu novo.
  - As 7 páginas com `--8<--` (`ferramentas/validador.md`, `inicio/conceitos.md`, `inicio/inspector.md`, `modulos/eventos.md`, `modulos/pooling.md`, `modulos/tags.md`, `modulos/tempo.md`; 33 blocos, 27 deles com mais de uma seção, e 92 linhas de inclusão): cada bloco vira o marcador com um bloco vazio, escrito à mão (por script de troca), e o código entra pela geração.
- Testes red (EditMode, `SpellBook.Repository.Tests.EditMode`):
  - `ReferenceGeneratorTests`: `ExpandExamples_FillsEmptyBlockWithSection`, `ExpandExamples_JoinsSectionsInTitleOrder`, `ExpandExamples_RemovesCommonIndentOfEachSection`, `ExpandExamples_IndentsCodeToFence`, `ExpandExamples_ReplacesOutdatedBlock`, `ExpandExamples_ExpandedPageIsStable`, `ExpandExamples_LeavesUnmarkedBlocksAlone`, `ExpandExamples_MissingFile_ReportsAndKeepsBlock`, `ExpandExamples_MissingSection_Reports`, `ExpandExamples_MarkerWithoutCSharpBlock_Reports`, `ExpandExamples_MissingEndcode_Reports`, `Generate_ExpandsManualPages`, `Generate_BrokenMarker_Throws`, `MenuPath_IsGerarCatalogoEExemplos`;
  - `TypeCatalogPageTests.Render_StartsWithPageTitle`;
  - `ManualCodeExamplesTests` (4 testes), vermelho enquanto as páginas usam `--8<--`.
- Critério de pronto: os testes red passam; `ReferenceUpToDateTests`, `DocumentationCategoryTests`, `TypeCatalogPageTests` e `ManualCodeExamplesTests` passam, com o `total` do XML conferido; `git grep -n -e "--8<-- \"" -- Documentation~/manual` não acha nada; o `catalogo.md` não tem `<!--`; rodar a geração de novo não muda nenhum arquivo.
- Tamanho: 576 linhas medidas (adicionadas mais removidas nos 6 `.cs`) em 13 arquivos (6 de código e as 7 páginas; o código expandido e o catálogo são saída de gerador com guarda e não contam). Exceção: 13 arquivos (+30% sobre o teto de 10) e 576 linhas (+15% sobre o teto de 500), dentro da faixa da ADR 0042. As linhas passam do teto porque os testes do expansor cobrem os quatro casos de falha do CA4 e a junção, o recuo e a estabilidade das seções. Motivo: a Spec estimou 11 (4 de código e as 7 páginas), mas a decisão do catálogo soma o `TypeCatalogPage` e o teste dele, e a troca do menu soma a instrução do `ReferenceUpToDateTests`. Para não passar de 13, o expansor fica no `ReferenceGenerator.cs`, e os testes dele no `ReferenceGeneratorTests.cs`, sem arquivo novo.
- Commit: `feat(docs): exemplos expandidos por marcador e Gerar catálogo e exemplos`

## GB5 — Sintaxe das páginas, parte 1
### Detalhe (2026-11-05)
- CA: CA8 nas 9 páginas do ticket (mesmas seções e mesmo texto; muda só a sintaxe). O CA6 não tem objeto aqui: nenhuma das 9 páginas tem bloco `mermaid`. O único do manual está em `modulos/dano-escudo-e-morte.md`, no GB6. A parte do CA5 sobre sobra de sintaxe (`{#`, `!!!`, `???`) passa a valer nestas páginas; a guarda que a confere é do GB7.
- O que as páginas têm hoje (contado em 2026-11-05, depois do GB4):
  - 81 títulos com `{#id}`: `README.md` 3, `inicio/conceitos.md` 9, `inicio/inspector.md` 10, `inicio/instalacao.md` 6, `ferramentas/event-debugger.md` 8, `ferramentas/gas-debugger.md` 7, `ferramentas/validador.md` 9, `exemplos/sandbox.md` 15, `regras/migracao-2-para-3.md` 14. Nenhuma página tem dois títulos iguais.
  - 2 admonitions, as duas `!!! note` com título e corpo recuado em 4 espaços: `README.md:34-36` ("Versão 3.0 em desenvolvimento") e `inicio/instalacao.md:34-36` ("Versões 2.x"). Nenhum `???`.
  - Nada de `<div>`, `<span>`, `attr_list` (`{: ...}`, `{ .classe }`), abas (`===`), ícone ou emoji do Material, nota de rodapé, abreviação ou `markdown="1"`.
  - Os links entre páginas já são relativos para o `.md`, o formato que o GB1 mostrou. Nenhum link absoluto para o site (`github.io/SpellBook` ou `gitbook.io`), nenhum `/pages/...` e nenhum link para o `index.md`. As URLs `https://github.com/...` de `inicio/instalacao.md:12,19,29,35` e `regras/migracao-2-para-3.md:13` são a URL de instalação do pacote, em código, e ficam.
  - 15 blocos `{% code title %}` expandidos pelo GB4 (`conceitos.md` 4, `inspector.md` 9, `validador.md` 2) e os blocos ` ```json ` e ` ```text `. Ficam como estão.
- Regra que vale agora: o guia do manual ainda manda pôr `{#id}` em todo título (`guia-do-manual.md:89`, parte 4, item 1; o molde da parte 1, `:10-43`) e usar `!!! note` e `!!! warning` (`:41`). Essas partes são do GB8. Até lá, valem os itens 9 e 11 da 0044: o título não tem `{#id}`, a âncora é a que o GitBook gera do título, e a admonition vira `{% hint %}`. A regra do slug foi medida pelo Controlador em 2026-11-05, pelo MCP, num space de teste, lendo o `meta.id` gerado para cada título. Uma segunda medição, no mesmo dia, corrigiu a primeira nos casos de `<`, `>` e título que começa com dígito. O GB7 usa a mesma regra:
  - letras minúsculas; acento e cedilha viram a letra base (`ç` → `c`, `ã` → `a`, `é` → `e`, `ñ` → `n`);
  - a formatação dentro do título (código, ênfase, escape com `\`) entra só com o texto;
  - espaço vira hífen, e hífens seguidos viram um só;
  - `&` vira `and`, `<` vira `less-than` e `>` vira `greater-than`, cada um como palavra separada;
  - dois-pontos, parênteses, setas e as outras pontuações somem, mas o ponto entre dígitos ou logo depois de um dígito fica (`1.0`, `4.`);
  - título cujo slug começa com dígito ganha o prefixo `id-`;
  - um título repetido na mesma página ganha `-1`, `-2`.

  Exemplos medidos: `Mapa dos módulos` → `mapa-dos-modulos`; `Cast Range: alcance & custo` → `cast-range-alcance-and-custo`; `Regras de migração (2 → 3)` → `regras-de-migracao-2-3`; `ÁRVORE Ç ñ` → `arvore-c-n`; `EventBus\<T\>` → `eventbus-less-than-t-greater-than`; `4. Ciclo de vida, cooldown e custo` → `id-4.-ciclo-de-vida-cooldown-e-custo`; `1.5 AbilitySystemComponent (ASC)` → `id-1.5-abilitysystemcomponent-asc`. O item 2 da parte 4 continua: a seção de um tipo tem o nome do tipo como título, então a âncora segue sendo o nome em minúsculas (as 5 seções dos scripts em `exemplos/sandbox.md:127-150`, de `### SandboxCaster` a `### AttributeReadout`, já são assim). Valem também a parte 5 (página antiga: não reestruturar) e as buscas da parte 7, menos a linha do build.
- Escopo:
  - As 9 páginas, em cada linha de título: sai o ` {#id}` do fim. O texto do título não muda.
  - `README.md:34-36` e `inicio/instalacao.md:34-36`: `!!! note "<título>"` vira `{% hint style="info" %}`, o formato do GB1 (`934c949`, `modulos/README.md:5-7`). O título da admonition vira a primeira linha do hint, em negrito, seguida de uma linha em branco. O corpo perde o recuo de 4 espaços, que no GitBook faria dele um bloco de código, e o hint fecha com `{% endhint %}`. As palavras não mudam. O negrito, e não um título `##` dentro do hint, evita uma âncora e uma entrada nova no índice da página.
  - Links com âncora que saem das 9 páginas (decisão do usuário de 2026-11-05, opção c): cada um passa a apontar para o slug que a regra acima dá ao título atual da página de destino. Para destino em `modulos/`, vale o título que já está lá, porque o GB6 só tira o `{#id}` e não muda o texto. O caminho do arquivo e o texto do link não mudam, só o que vem depois do `#`. São 38 links com âncora nas 9 páginas. Em 22, o slug já é o `id` antigo (por exemplo `#validacao-no-inspector`, `#tipo-nao-encontrado`, `#condicoes`, `#raiseeventaction`, `#cargas`), e eles ficam. Os 16 que mudam:

    | Origem | Destino (título) | Antes | Depois |
    |---|---|---|---|
    | `README.md:32` | `inicio/conceitos.md:12` (Mapa de assemblies) | `#mapa` | `#mapa-de-assemblies` |
    | `inicio/inspector.md:122` | `inicio/inspector.md:85` (Renomear tipos sem perder dados) | `#movedfrom` | `#renomear-tipos-sem-perder-dados` |
    | `ferramentas/event-debugger.md:59` | `modulos/eventos.md:32` (`EventBus\<T\>`) | `#eventbus` | `#eventbus-less-than-t-greater-than` |
    | `ferramentas/gas-debugger.md:4` | `modulos/gas.md:328` (1.5 AbilitySystemComponent (ASC)) | `#abilitysystemcomponent` | `#id-1.5-abilitysystemcomponent-asc` |
    | `ferramentas/gas-debugger.md:66` | `modulos/gas.md:515` (4. Ciclo de vida, cooldown e custo) | `#ciclo-de-vida` | `#id-4.-ciclo-de-vida-cooldown-e-custo` |
    | `ferramentas/validador.md:54` | `modulos/eventos.md:298` (Inspector e validação do relay) | `#relay-inspector` | `#inspector-e-validacao-do-relay` |
    | `ferramentas/validador.md:63` | `modulos/gas.md:332` (Efeitos de partida) | `#starting-effects` | `#efeitos-de-partida` |
    | `ferramentas/validador.md:73` | `modulos/gas.md:232` (Níveis e tabela de valores) | `#abilityvalues` | `#niveis-e-tabela-de-valores` |
    | `ferramentas/validador.md:83` | `modulos/gas.md:85` (Valores por nível (ScalableFloat)) | `#scalablefloat` | `#valores-por-nivel-scalablefloat` |
    | `ferramentas/validador.md:84` | `modulos/gas.md:159` (Snapshot ou ao vivo) | `#magnitude-ao-vivo` | `#snapshot-ou-ao-vivo` |
    | `ferramentas/validador.md:107` | `modulos/gas.md:99` (1.3 Gameplay Effects (GE)) | `#gameplayeffect` | `#id-1.3-gameplay-effects-ge` |
    | `exemplos/sandbox.md:114` | `modulos/dano-escudo-e-morte.md:114` (Morte e outros limites) | `#morte` | `#morte-e-outros-limites` |
    | `regras/migracao-2-para-3.md:35` | `inicio/conceitos.md:6` (Módulos e assemblies) | `#modulos` | `#modulos-e-assemblies` |
    | `regras/migracao-2-para-3.md:108` | `inicio/conceitos.md:117` (`` `MathTools` ``) | `#math-tools` | `#mathtools` |
    | `regras/migracao-2-para-3.md:149` | `modulos/gas.md:684` (4.6 Tempo do ASC) | `#tempo-do-asc` | `#id-4.6-tempo-do-asc` |
    | `regras/migracao-2-para-3.md:200` | `modulos/gas.md:85` (Valores por nível (ScalableFloat)) | `#scalablefloat` | `#valores-por-nivel-scalablefloat` |

    Quatro destinos começam com dígito (`modulos/gas.md:99`, `:328`, `:515` e `:684`) e levam o `id-`. Um tem `<` e `>` (`modulos/eventos.md:32`). Nenhum dos 22 links que ficam vai para título que começa com dígito ou tem `<` ou `>`, então a correção da regra não muda a contagem: continuam 16. Nenhum destino tem título repetido antes dele na página, então nenhum slug leva `-1`. Quando o GB6 tirar os `{#id}` de `modulos/`, os 12 links da tabela que vão para lá passam a valer. Até lá, eles apontam para âncoras que ainda não existem.
  - Para o GB6 (links de `modulos/` para as páginas deste ticket, que o GB6 corrige pela mesma regra): `modulos/eventos.md:7`, `gas.md:7`, `tags.md:8` e `tempo.md:8` (`#modulos` → `#modulos-e-assemblies`); `modulos/gas.md:31` (`#regras` → `#regras-da-3.0`); `modulos/gas.md:103` (`#valores-por-nivel` → `#valores-por-nivel-no-gas`); `modulos/tags.md:200` (`#ivalidatable` → `#validar-um-tipo-seu`). Os de `modulos/gas.md:270` (`#condicoes`) e `:680` (`#eventos-do-gas`) já batem. Entre o GB5 e o GB6, esses 7 links ficam quebrados.
  - `Packages/com.spellbook/Tests/Repository/EditMode/Structure/SampleStructureTests.cs:359-387` (consequência direta, ADR 0042, item 6): o `SampleBehaviours_HaveHelpUrlToSamplesPage` procura `{#<âncora>}` no `sandbox.md` (`:378`) e não está na categoria `Documentacao`. Sem o ajuste, ele fica vermelho na suíte `!Documentacao` de todos os tickets do lote depois deste. Passa a conferir que o `sandbox.md` tem um título (`#` a `######`) cujo texto, em minúsculas, é a âncora do `[HelpURL]` (`SandboxCaster` → `sandboxcaster`), pelo item 2 da parte 4 do guia e pelo item 9 da 0044. Para um nome de tipo sem acento nem pontuação, isso é o slug da regra acima. A mensagem passa a dizer "nenhum título" no lugar de "âncora {#...}". O GB7 pode juntar essa conferência à do `ManualLinksTests`.
- Testes red: nenhum teste novo. O teste que prova a mudança é o `SampleStructureTests.SampleBehaviours_HaveHelpUrlToSamplesPage` (EditMode, `SpellBook.Repository.Tests.EditMode`): com o `sandbox.md` sem `{#id}` e o código antigo, ele falha com as 5 âncoras dos scripts do Sample; com o ajuste, passa. O ticket é do Codex, que não roda a Unity, então o Examinador mostra o red e o green na `/revisao`. Guardas `Documentacao` depois do GB5:
  - `ManualLinksTests.EveryHelpUrlHasPageAndAnchor`: segue vermelha, com 5 linhas novas (`DocUrls.Samples + "#sandboxcaster"`, `#sandboxdirector`, `#damagetakenrelay`, `#trainingdummyfeedback`, `#attributereadout`). Ela já está vermelha hoje, pelas páginas de `ui/` que faltam e pelo `DocUrls.Concepts + "#ancoras"`, que não tem título em `conceitos.md`. O GB7 a reescreve;
  - `ManualLinksTests.EveryDocUrlHasPage`: segue vermelha como hoje (o `index.md` do GB2 e as páginas que faltam). Nenhuma constante do `DocUrls` tem âncora, então o GB5 não muda o resultado;
  - `ManualLinksTests.NoStubPages`: verde;
  - `ManualCodeExamplesTests` (4 testes), `ReferenceUpToDateTests` e `DocumentationCategoryTests`: verdes, porque os blocos `{% code title %}` não mudam.

  As âncoras mudam: nas 9 páginas, passam a ser o slug do título. Até o GB7, nenhuma guarda confere link com âncora. Quem confere é o critério de pronto abaixo. Entre o GB5 e o GB6, ficam quebrados os 12 links da tabela que vão para `modulos/` e os 7 de `modulos/` que vêm para cá.
- Critério de pronto (da raiz do repositório, sem Unity; `P` são as 9 páginas):
  - `git grep -nE '\{#|^\s*(!!!|\?\?\?)|--8<--' -- $P` não acha nada;
  - para cada página `f`, `diff <(git show HEAD:"$f" | sed -E '/^#/ s/ \{#[^}]+\}\s*$//') "$f"` não mostra nada, salvo os dois hints em `README.md` e `inicio/instalacao.md` e as 16 linhas da tabela de links. Nessas linhas, só muda o que vem depois do `#`. Isso prova que os títulos, o texto dos links, os blocos de código e o resto do texto não mudaram (CA8);
  - cada link `](<caminho>#<âncora>)` e `](#<âncora>)` das 9 páginas leva a um título cujo slug é a âncora: o da página de destino depois de tirar o `{#id}`, pela regra acima. Para destino em `modulos/`, vale o título de hoje. A conferência é feita por script descartável na pasta de rascunho, sem Python (CA7), e mostra 38 links, nenhum quebrado;
  - `git grep -c '{% hint style="info" %}' -- $P` e `git grep -c '{% endhint %}' -- $P` dão 1 em `README.md` e 1 em `inicio/instalacao.md`, e nada nas outras;
  - nenhuma linha recuada em 4 espaços fora de bloco de código: `awk 'FNR==1{b=0} /^\s*```/{b=!b;next} !b && /^    [^ ]/{print FILENAME":"FNR}' $P` não mostra nada;
  - `git grep -c '{% code title=' -- $P` continua 4 em `conceitos.md`, 9 em `inspector.md` e 2 em `validador.md`;
  - nenhum link absoluto para o site: `git grep -nE 'github\.io/SpellBook|gitbook\.io|\]\(/' -- $P` não acha nada;
  - as buscas da parte 7 do guia: `grep -nE '!\[|<img' $P` não acha nada, e a de sigla em título acha só o que achava antes;
  - o `SampleBehaviours_HaveHelpUrlToSamplesPage` passa e o `git grep -n '{#' -- Packages/com.spellbook/Tests/Repository/EditMode/Structure/SampleStructureTests.cs` não acha nada (o Examinador roda o teste);
  - `git diff --check` limpo.
- Tamanho: ~12 linhas (só o `.cs` conta) em 10 arquivos (as 9 páginas e o `SampleStructureTests.cs`). A Spec estimou 9; o teste entra como consequência direta e conta. Os links corrigidos ficam nas mesmas 9 páginas e não somam arquivo. Exceção: nenhuma.
- Commit: `docs(manual): sintaxe do GitBook nas páginas fora de modulos/`

## GB6 — Sintaxe das páginas, parte 2
### Detalhe (2026-11-05)
- CA: CA8 nas 6 páginas de `modulos/` (mesmas seções e mesmo texto; muda só a sintaxe e os links, salvo as duas decisões do usuário abaixo) e CA6 no único diagrama do manual, `modulos/dano-escudo-e-morte.md:21-28`. O que o site desenha, o GB9 confere. A parte do CA5 sobre sobra de sintaxe (`{#`, `!!!`, `???`) passa a valer em todo o manual; a guarda é do GB7.
- Exceção ao CA8 (decisão do usuário de 2026-11-05, resposta à pergunta 1): saem as 2 frases com link absoluto para a API do site antigo, em `pooling.md:20-21` e `tempo.md:14-15`. É a única mudança de texto autorizada no ticket. O resto de cada parágrafo fica igual.
- Níveis de título (decisão do usuário de 2026-11-05, resposta à pergunta 2): os 5 `#####` de `gas.md` sobem para `####`. As seções continuam as mesmas, com o mesmo texto, e no índice da página aparecem como irmãs do `####` pai. Nenhuma outra mudança de nível.
- Níveis de título no GitBook, medidos pelo Controlador em 2026-11-05 no space de teste, pelo MCP, lendo o `meta.id`: o primeiro `#` da página vira o título da página, e não um título com âncora; `##`, `###` e `####` viram títulos de nível 1, 2 e 3, com âncora pela regra do slug (`Título 4 com Âncora` → `titulo-4-com-ancora`); `#####` e `######` viram parágrafo em negrito, sem nível e sem âncora, e um link para eles fica sem destino. Os 28 `####` de `modulos/` (`eventos.md` 5, `gas.md` 23) mantêm a âncora. Isso inclui os 8 `[HelpURL]` que vão para `####` (`#attributedefinition`, `#attributecategory`, `#textroletheme`, `#simpleattributeformula`, `#expressionformula`, `#projectile`, `#raiseeventaction`, `#cooldownreductionpolicy`). O manual inteiro tem 5 `#####`, todos em `gas.md`, e nenhum `######`:

  | Título `#####` | Pai `####` | Slug que teria | Quem liga para ele |
  |---|---|---|---|
  | `gas.md:142` (Origens do valor) | `:116` (Contexto e spec do efeito) | `origens-do-valor` | `gas.md:103` (GB6) |
  | `gas.md:159` (Snapshot ou ao vivo) | `:116` | `snapshot-ou-ao-vivo` | `gas.md:156` (GB6, hoje `#magnitude-ao-vivo`, na tabela de links abaixo) e `ferramentas/validador.md:84` (GB5) |
  | `gas.md:247` (Exemplo: Fireball com `damage` e `cooldown`) | `:232` (Níveis e tabela de valores) | `exemplo-fireball-com-damage-e-cooldown` | ninguém |
  | `gas.md:257` (No Inspector) | `:232` | `no-inspector` | ninguém |
  | `gas.md:304` (Condição própria) | `:269` (Condições) | `condicao-propria` | ninguém |

  Nenhum `[HelpURL]` nem página do Sandbox aponta para um `#####` (busca em todo o repositório pelos `id` antigos e pelos slugs). Três links, em `gas.md:103`, `gas.md:156` e `ferramentas/validador.md:84`, ficariam sem destino. Com os 5 títulos em `####` (decisão acima), os três passam a valer. O de `gas.md:103` (`#origens-do-valor`) e o de `validador.md:84` (`#snapshot-ou-ao-vivo`, corrigido no GB5) já usam o slug, e só o de `gas.md:156` muda (está na tabela de links). O `validador.md` não entra no escopo.
- O que as páginas têm hoje (contado em 2026-11-05, depois do GB5):
  - 135 títulos com `{#id}`: `dano-escudo-e-morte.md` 21, `eventos.md` 36, `gas.md` 44, `pooling.md` 11, `tags.md` 11, `tempo.md` 12. O `gas.md` tem mais 7 títulos sem `{#id}` (`:339`, `:345`, `:517`, `:522`, `:662`, `:665`, `:670`). Nenhuma página tem dois títulos com o mesmo slug, então nenhum leva `-1`.
  - 4 admonitions, todas com título e corpo recuado em 4 espaços: `dano-escudo-e-morte.md:268-269` (`!!! warning`, "O sistema de habilidades não destrói nada"), `eventos.md:9-15` (`!!! note`, "Em desenvolvimento", corpo de 6 linhas), `gas.md:402-403` (`!!! warning`, "Armadilha: o VFX do `PlayCueAction` não volta") e `gas.md:706-707` (`!!! warning`, "Armadilha: somar floats na fronteira"). Nenhum `???`.
  - Um bloco `mermaid`, em `dano-escudo-e-morte.md:21-27`, com a legenda de uma frase em itálico logo abaixo (`:28`). É o mesmo formato do bloco que o GB1 mostrou desenhado (` ```mermaid ` cercado, `flowchart LR`, recuo de 4 espaços dentro do bloco). Não muda.
  - Nada de `<div>`, `<span>`, `attr_list`, abas (`===`), ícone ou emoji do Material, nota de rodapé, abreviação, `markdown="1"`, `/pages/...` nem link para o `index.md`. O `<T>` dos títulos `eventos.md:32` e `:129` está escapado (`\<T\>`) e fica.
  - 18 blocos `{% code title %}` expandidos pelo GB4 (`eventos.md` 5, `pooling.md` 5, `tags.md` 5, `tempo.md` 3). Ficam como estão. O `gas.md` e o `dano-escudo-e-morte.md` continuam com os blocos C# escritos à mão, na lista `PendingPages` do `ManualCodeExamplesTests.cs:25-30` (EX3 e EX6).
  - Linhas recuadas em 4 espaços fora de bloco de código que não são de admonition: `dano-escudo-e-morte.md:66-67` (sublista de `Mode`) e `eventos.md:565` (continuação de um item de sublista). São Markdown comum de lista e ficam.
  - 2 links absolutos para a API do site antigo: `pooling.md:20-21` ("As assinaturas estão na [página do `SpawnerAsset` na API](https://gabrielctorres.github.io/SpellBook/api/...)") e `tempo.md:14-15` ("A declaração está na [página do `ITimeSource` na API](...)"). A API saiu no GB3. As duas frases saem (exceção ao CA8, acima).
- Regra: a mesma do Detalhe do GB5 (slug do título medido pelo Controlador em 2026-11-05, hint no formato do GB1, título do hint em negrito na primeira linha). O `!!! warning` vira `{% hint style="warning" %}`, e o `!!! note` vira `{% hint style="info" %}`.
- Escopo (as 6 páginas de `Documentation~/manual/modulos/`):
  - Em cada linha de título, sai o ` {#id}` do fim. O texto do título não muda.
  - `gas.md:142`, `:159`, `:247`, `:257` e `:304`: o `#####` vira `####`.
  - `pooling.md:20-21`: sai a frase "As assinaturas estão na [página do `SpawnerAsset` na API](...)." A linha 20 termina em "O `Release` recebe a instância de volta.", e a 21 sai. `tempo.md:14-15`: sai a frase "A declaração está na [página do `ITimeSource` na API](...)." A linha 14 termina em "já com a pausa da fonte aplicada.", e a 15 sai. As outras linhas dos dois parágrafos não mudam.
  - As 4 admonitions viram hint, como no GB5: `{% hint style="..." %}`, o título em negrito, uma linha em branco, o corpo sem o recuo de 4 espaços e `{% endhint %}`. As palavras não mudam.
  - Links com âncora. Hoje saem 110 links com âncora das 6 páginas. Com os títulos sem `{#id}`, 60 continuam certos (o slug já é o `id` antigo, como `#raiseeventaction`, `#eventemitter`, `#cargas`, `#condicoes`, `#eventos-do-gas`). Os 50 que mudam, em 45 linhas, passam a apontar para o slug do título de destino. O caminho e o texto do link não mudam, só o que vem depois do `#`:

    | Origem | Destino (título) | Antes | Depois |
    |---|---|---|---|
    | `eventos.md:7`, `gas.md:7`, `tags.md:8`, `tempo.md:8` | `inicio/conceitos.md:6` (Módulos e assemblies) | `#modulos` | `#modulos-e-assemblies` |
    | `gas.md:31` | `ferramentas/validador.md:44` (Regras da 3.0) | `#regras` | `#regras-da-3.0` |
    | `gas.md:103` | `regras/migracao-2-para-3.md:197` (Valores por nível no GAS) | `#valores-por-nivel` | `#valores-por-nivel-no-gas` |
    | `tags.md:200` | `ferramentas/validador.md:185` (Validar um tipo seu) | `#ivalidatable` | `#validar-um-tipo-seu` |
    | `dano-escudo-e-morte.md:81` | `dano-escudo-e-morte.md:15` (Redirecionar para outro atributo) | `#redirecionar` | `#redirecionar-para-outro-atributo` |
    | `eventos.md:24` | `eventos.md:32` (`EventBus\<T\>`) | `#eventbus` | `#eventbus-less-than-t-greater-than` |
    | `eventos.md:165` | `eventos.md:572` (Segurança do disparo) | `#seguranca` | `#seguranca-do-disparo` |
    | `eventos.md:370` | `eventos.md:399` (Escolher o canal) | `#escolher-canal` | `#escolher-o-canal` |
    | `eventos.md:427` | `eventos.md:433` (SetActive) | `#setactivereaction` | `#setactive` |
    | `eventos.md:428` | `eventos.md:440` (Toggle de componente) | `#togglecomponentreaction` | `#toggle-de-componente` |
    | `eventos.md:411`, `:429`, `:536` | `eventos.md:448` (RaiseChannel) | `#raisechannelreaction` | `#raisechannel` |
    | `eventos.md:430` | `eventos.md:462` (Log) | `#logreaction` | `#log` |
    | `eventos.md:347`, `:411`, `:431`, `:537` | `eventos.md:470` (Invoke com delay) | `#delayedreaction` | `#invoke-com-delay` |
    | `eventos.md:479`, `gas.md:376`, `gas.md:702` | `tempo.md:44` (Grupo pausável) | `#pausabletimesource` | `#grupo-pausavel` |
    | `gas.md:109`, `:149` | `gas.md:55` (Fórmulas de modificadores) | `#formulas` | `#formulas-de-modificadores` |
    | `gas.md:102`, `:103`, `:147`, `:235`, `:382` | `gas.md:85` (Valores por nível (ScalableFloat)) | `#scalablefloat` | `#valores-por-nivel-scalablefloat` |
    | `gas.md:102`, `:103` | `gas.md:116` (Contexto e spec do efeito) | `#effectcontext` | `#contexto-e-spec-do-efeito` |
    | `gas.md:156` | `gas.md:159` (Snapshot ou ao vivo) | `#magnitude-ao-vivo` | `#snapshot-ou-ao-vivo` |
    | `gas.md:91`, `:127`, `:382` | `gas.md:232` (Níveis e tabela de valores) | `#abilityvalues` | `#niveis-e-tabela-de-valores` |
    | `gas.md:330` | `gas.md:515` (4. Ciclo de vida, cooldown e custo) | `#ciclo-de-vida` | `#id-4.-ciclo-de-vida-cooldown-e-custo` |
    | `gas.md:356` | `gas.md:442` (3.2 Targeting) | `#targeting` | `#id-3.2-targeting` |
    | `gas.md:360`, `:398` | `gas.md:386` (Projéteis) | `#projectile` | `#projeteis` |
    | `gas.md:379`, `:433`, `:666` | `gas.md:461` (3.3 Base nova das ações) | `#gameplayaction` | `#id-3.3-base-nova-das-acoes` |
    | `gas.md:365`, `:366`, `:438` | `gas.md:493` (3.4 Alvo de cada ação) | `#alvo-da-acao` | `#id-3.4-alvo-de-cada-acao` |
    | `gas.md:373`, `:600` | `gas.md:684` (4.6 Tempo do ASC) | `#tempo-do-asc` | `#id-4.6-tempo-do-asc` |
    | `tempo.md:77` | `tempo.md:144` (Estado entre sessões de Play) | `#reset` | `#estado-entre-sessoes-de-play` |

    As 4 primeiras linhas são os 7 links que o Detalhe do GB5 deixou para cá. As outras 43 vão para títulos de `modulos/`. Os números de linha são os de hoje; os hints acrescentam linhas e deslocam o que vem depois deles. Os 27 links que chegam a `modulos/` de fora já usam o slug (GB5) e passam a valer quando os `{#id}` saem.
  - Nenhum teste `.cs` lê o texto destas páginas. O `SampleStructureTests` lê só o `exemplos/sandbox.md` (`:32`). O `DocUrlsTests` confere só as constantes. O `ManualCodeExamplesTests` lê os blocos `{% code title %}`, que não mudam. Os comentários `<summary>` dos `*ManualExamples.cs` citam o `--8<--`, mas não são teste, e o texto deles é do GB8 ou do passe. Nenhum `.cs` entra como consequência direta.
- Perguntas respondidas pelo usuário em 2026-11-05:
  1. **As 2 frases com link para a API:** saem inteiras (opção a). Ver a exceção ao CA8, acima.
  2. **Os 5 títulos `#####` de `gas.md`:** sobem para `####` (opção a). No GitBook, o `#####` vira parágrafo em negrito, sem âncora, e sai do índice. Com o `####`, as 5 seções continuam no índice e os 3 links que vão para elas têm destino. Os títulos de cima não sobem, porque o GitBook só tem três níveis com âncora.
- Para o GB7 (não é do GB6): com os títulos sem `{#id}`, 8 `[HelpURL]` para `modulos/` apontam para âncora que não é o slug do título, porque o título não é o nome do tipo: `DocUrls.Gas + "#abilitysystemcomponent"` (→ `id-1.5-abilitysystemcomponent-asc`), `#attributeset` (→ `id-1.2-atributos`), `#gameplayability` (→ `id-1.4-gameplay-abilities-ga`), `#gameplayeffect` (→ `id-1.3-gameplay-effects-ge`), `#projectile` (→ `projeteis`); `DocUrls.Pooling + "#componentpooler"` (→ `componentpooler-e-ipoolable`), `#poolmanager` (→ `poolmanager-pre-aquecimento`); `DocUrls.Time + "#pausabletimesource"` (→ `grupo-pausavel`). O `DocUrls.Gas + "#abilitysystemanchor"` já não tem título hoje. Pelo CA3, o GB7 escolhe entre trocar a âncora no `[HelpURL]` e outro caminho; o GB6 não muda o texto de nenhum título.
- Testes red: nenhum, porque o ticket não tem código. O Codex não roda a Unity; o Examinador roda as guardas na `/revisao`. Guardas `Documentacao` depois do GB6:
  - `ManualLinksTests.EveryHelpUrlHasPageAndAnchor`: segue vermelha, com 35 linhas novas, uma por `[HelpURL]` distinto com âncora em `modulos/` (`Events` 11, `Gas` 13, `Pooling` 4, `Tags` 3, `Time` 4), porque ela procura `{#id}`. O GB7 a reescreve;
  - `ManualLinksTests.EveryDocUrlHasPage`: segue vermelha como hoje. Nenhuma constante do `DocUrls` tem âncora, e as páginas não mudam de nome;
  - `ManualLinksTests.NoStubPages`: verde;
  - `ManualCodeExamplesTests` (4 testes), `ReferenceUpToDateTests` e `DocumentationCategoryTests`: verdes, porque os blocos `{% code title %}` e os blocos à mão do `gas.md` e do `dano-escudo-e-morte.md` não mudam.

  Até o GB7, nenhuma guarda confere link com âncora. Quem confere é o critério de pronto abaixo.
- Critério de pronto (da raiz do repositório, sem Unity; `P` são as 6 páginas de `Documentation~/manual/modulos/`):
  - `git grep -nE '\{#|^\s*(!!!|\?\?\?)|--8<--' -- Documentation~/manual` não acha nada (o manual inteiro, com o GB5);
  - para cada página `f`, `diff <(git show HEAD:"$f" | sed -E '/^#/ s/ \{#[^}]+\}\s*$//') "$f"` mostra só os 4 hints, as 45 linhas da tabela de links (nelas, só muda o que vem depois do `#`), as 2 frases da API que saem (`pooling.md:20-21` e `tempo.md:14-15`) e as 5 linhas de título de `gas.md` que passam de `#####` a `####` (nelas, só muda o nível);
  - `git grep -nE '^#{5,}' -- Documentation~/manual` não acha nada fora de bloco de código (o `awk` que ignora as cercas confirma);
  - cada link `](<caminho>#<âncora>)` e `](#<âncora>)` do manual leva a um título cujo slug é a âncora, pela regra do GB5. A conferência é feita por script descartável na pasta de rascunho, sem Python (CA7), e mostra 148 links, nenhum quebrado (110 saem de `modulos/`);
  - `git grep -c '{% hint style="warning" %}' -- $P` dá 1 em `dano-escudo-e-morte.md` e 2 em `gas.md`; `git grep -c '{% hint style="info" %}' -- $P` dá 1 em `eventos.md`; `git grep -c '{% endhint %}' -- $P` dá 1, 1 e 2 nas mesmas páginas, e nada nas outras;
  - fora de bloco de código, `awk 'FNR==1{b=0} /^\s*```/{b=!b;next} !b && /^    [^ ]/{print FILENAME":"FNR}' $P` mostra só as 3 linhas de sublista (`dano-escudo-e-morte.md:66-67` e a de `eventos.md` que hoje é a `:565`, deslocada pelo hint);
  - `git grep -c '{% code title=' -- $P` continua 5 em `eventos.md`, 5 em `pooling.md`, 5 em `tags.md` e 3 em `tempo.md`;
  - `git grep -c '```mermaid' -- Documentation~/manual` continua 1, em `dano-escudo-e-morte.md`, com a linha de legenda logo abaixo do fechamento;
  - nenhum link absoluto para o site: `git grep -nE 'github\.io/SpellBook|gitbook\.io|\]\(/' -- $P` não acha nada;
  - as buscas da parte 7 do guia: `grep -nE '!\[|<img' $P` não acha nada, e a de sigla em título acha só o que achava antes;
  - `git diff --check` limpo.
- Tamanho: 0 linhas (nenhum arquivo que conta linha) em 6 arquivos, as 6 páginas. Os links corrigidos, os hints, as 2 frases da API e os 5 níveis de título ficam nas mesmas páginas. O `ferramentas/validador.md:84` já usa o slug certo (`#snapshot-ou-ao-vivo`, do GB5) e não entra, então o ticket fica em 6 arquivos, abaixo do teto de 10 da ADR 0042. Exceção de tamanho: nenhuma.
- Commit: `docs(manual): sintaxe do GitBook nas páginas de modulos/`
