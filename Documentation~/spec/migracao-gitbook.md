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

## GB7a — URLs e guardas de link
### Detalhe (2026-11-05)
- Divisão (decisão do usuário de 2026-11-05): o GB7 da tabela vira GB7a e GB7b, porque a correção das âncoras de `[HelpURL]` soma 10 `.cs` e o ticket passaria de 13 arquivos. A tabela de tickets é parte fixa. A troca da linha do GB7 pelas linhas do GB7a e do GB7b fica como pedido ao Controlador, para o Desenhista.
- CA: CA5 inteiro (sumário, links relativos, link absoluto para o próprio site, sobra do MkDocs) e a parte do CA3 que é código: `DocUrls`, o `documentationUrl` e a guarda de `[HelpURL]` com a âncora do título. As âncoras dos `[HelpURL]` são do GB7b. O que o site abre de fato, o GB9 confere.
- Escopo do usuário (2026-11-05): o GB7a e o GB7b corrigem só o que o GB1 a GB6 quebraram. Ficam vermelhas, e saem no passe de documentação:
  - em `EveryDocUrlHasPage`, as 15 constantes cuja página ainda não existe: `Teams`, `Cues`, `Thinkers`, `Text`, `Description`, `Ui` e as 9 `Ui*`;
  - em `EveryHelpUrlHasPageAndAnchor`, os `[HelpURL]` dessas páginas e as duas âncoras que já não tinham título antes do GB5: `DocUrls.Concepts + "#ancoras"` (`AnchorRegistrar`, `TransformAnchor`) e `DocUrls.Gas + "#abilitysystemanchor"`. Até o GB7b, também ficam vermelhas as 8 âncoras dele.
- URL, medida pelo Controlador em 2026-11-05 no site de teste publicado: a base é `https://rook-studio.gitbook.io/spellbook`, sem barra no fim. A raiz responde 302 para ela, e `/spellbook/readme` redireciona para ela, então o `README.md` é a própria base. No site de teste (4 spaces e seção padrão), as páginas do space padrão ficam logo abaixo da base (`/spellbook/gas` responde 200), sem `.md` e sem barra no fim. As dos outros spaces ganham o prefixo da seção e do space. Para o layout final, com um space só, a regra é **inferida** e **fica a confirmar no GB9 com o layout final**: página = base + `/` + o caminho do arquivo em `Documentation~/manual/` sem `.md` (`/modulos/gas`, `/inicio/instalacao`), sem barra no fim. A guarda tira o caminho da URL com `Trim('/')`, então ela não depende da barra.
- Medido no manual em 2026-11-05, depois do GB6, por script descartável na pasta de rascunho: 191 links relativos, nenhum quebrado; nenhum link absoluto para `gitbook.io`, para `github.io/SpellBook` ou começando em `/`; nenhuma sobra do MkDocs; um só `#` por página; nenhum título com `_`. O `SUMMARY.md` tem 16 entradas, com todas as páginas, nenhuma repetida e nenhuma sem arquivo. As guardas novas nascem verdes no manual.
- Regra do slug: a do Detalhe do GB5, com os níveis do Detalhe do GB6. O primeiro `#` da página é o título dela e não tem âncora. `##`, `###` e `####` têm âncora. `#####` e `######` não têm. Título dentro de bloco de código não conta. Um link dentro de um título entra só com o texto. Hífens no começo e no fim saem.
- Escopo:
  - `Packages/com.spellbook/Tests/Repository/EditMode/Documentation/ManualMarkdown.cs` (novo, com o `.meta` gerado pela Unity): funções puras sobre o texto das páginas. `Slug(título)`; `Anchors(página)`; `BrokenLinks(caminho, página, base do site, ler página)`, que acusa link relativo para arquivo ou âncora que não existe, link que sai do manual, link que começa em `/` e link absoluto que começa na base do site; `SummaryProblems(sumário, páginas)`; `MkDocsLeftovers(página)`. Os links e as sobras são procurados fora de bloco de código e de código em linha. A exceção é o `--8<--`, procurado também dentro dos blocos, onde o MkDocs o usava.
  - `Tests/Repository/EditMode/Documentation/ManualMarkdownTests.cs` (novo): os testes red abaixo, com páginas pequenas escritas no próprio teste.
  - `Tests/Repository/EditMode/ManualLinksTests.cs:105`: a página inicial passa a ser o `README.md` (decisão 4). `:116-117`: a âncora passa a ser um dos `Anchors` da página, no lugar do `{#id}`. `:14-18`: o resumo da classe. Entram três guardas `Documentacao`: `SummaryListsEveryPageOnce`, `EveryRelativeLinkHasTarget` (inclui o link absoluto para o próprio site) e `NoMkDocsSyntax`.
  - `Tests/Repository/EditMode/Documentation/DocumentationCategoryTests.cs:21-23` (consequência direta, ADR 0042, item 6): as três guardas novas entram na lista.
  - `Packages/com.spellbook/Runtime/Core/DocUrls.cs:9-39`, `Tests/Repository/EditMode/DocUrlsTests.cs:10,14,24-47` e `Packages/com.spellbook/package.json:10`: a base e as 28 constantes no formato acima (`Base + "/modulos/gas"`). Os nomes das constantes não mudam, e nenhum `[HelpURL]` do pacote muda por causa disso.
  - `Tests/Core/EditMode/HelpUrlTests.cs:99` (consequência direta, ADR 0042, item 6): o `[HelpURL(DocUrls.Base + "teste/")]` do asset de teste vira `DocUrls.Base + "/teste"`. Sem a barra no fim da base, ele daria `.../spellbookteste/`.
  - O `README.md` do pacote, o do Sandbox e o `CHANGELOG.md`, que citam o `github.io`, ficam para o GB8 (decisão do usuário de 2026-11-05).
- Testes red (EditMode, `SpellBook.Repository.Tests.EditMode`, classe `ManualMarkdownTests`; os casos de uma mesma conferência são `TestCase` com `TestName`):
  - slug, 12 casos com os exemplos medidos do GB5: `Slug_LowercasesAndJoinsWordsWithHyphen`, `Slug_DropsAccentsAndCedilla`, `Slug_SpellsAmpersand`, `Slug_SpellsAngleBrackets`, `Slug_DropsPunctuationAndCollapsesHyphens`, `Slug_KeepsDotBetweenDigits`, `Slug_DropsDotAfterLetter`, `Slug_KeepsDotAfterDigit`, `Slug_StartingWithDigit_GetsIdPrefix`, `Slug_KeepsOnlyTextOfInlineCode`, `Slug_KeepsOnlyTextOfEmphasis` e `Slug_KeepsOnlyTextOfLink`;
  - âncoras: `Anchors_SkipsPageTitle`, `Anchors_SkipsFifthAndSixthLevels`, `Anchors_NumbersRepeatedTitles` e `Anchors_IgnoresTitlesInsideCodeBlocks`;
  - links: `BrokenLinks_AcceptsExistingPageAndAnchor`, `BrokenLinks_MissingFile_Reports`, `BrokenLinks_MissingAnchorInOtherPage_Reports`, `BrokenLinks_MissingAnchorInSamePage_Reports`, `BrokenLinks_OutsideManual_Reports`, `BrokenLinks_RootRelative_Reports`, `BrokenLinks_AbsoluteToOwnSite_Reports`, `BrokenLinks_AbsoluteToOwnSiteAnyCase_Reports`, `BrokenLinks_AutoLinkToOwnSite_Reports`, `BrokenLinks_IgnoresExternalLinks` e `BrokenLinks_IgnoresLinksInCode`;
  - sumário: `SummaryProblems_AcceptsEveryPageOnce`, `SummaryProblems_PageOutsideSummary_Reports`, `SummaryProblems_RepeatedEntry_Reports` e `SummaryProblems_EntryWithoutFile_Reports`;
  - sobras: `MkDocsLeftovers_ReportsEachSyntax` (4 casos: `{#`, `!!!`, `???` e `--8<--`), `MkDocsLeftovers_IgnoresGitBookBlocks` e `MkDocsLeftovers_IgnoresSyntaxInsideCodeBlocks`;
  - `DocUrlsTests.DocUrls_BaseMatchesSite` e `DocUrls_PageConstantPointsToPageUnderBase` (24 casos), com a URL medida.
- Critério de pronto:
  - os testes red passam, com o `total` do XML conferido;
  - `SummaryListsEveryPageOnce`, `EveryRelativeLinkHasTarget`, `NoMkDocsSyntax`, `NoStubPages`, `DocumentationCategoryTests`, `DocUrlsTests`, `HelpUrlTests` e `SampleStructureTests` passam;
  - `EveryDocUrlHasPage` falha só com as 15 constantes do escopo do usuário;
  - `EveryHelpUrlHasPageAndAnchor` falha só com os `[HelpURL]` das páginas que faltam, as duas âncoras antigas e as 8 do GB7b. Nenhuma linha vem do `index.md` nem do `{#id}`;
  - `git grep -n "index.md\|{#" -- Packages/com.spellbook/Tests/Repository/EditMode/ManualLinksTests.cs` não acha nada;
  - `git diff --check` limpo.
- Tamanho, medido: 645 linhas (adicionadas mais removidas) em 8 arquivos: `ManualMarkdown.cs` 266, `ManualMarkdownTests.cs` 184, `ManualLinksTests.cs` 72, `DocUrls.cs` 64, `DocUrlsTests.cs` 52, `DocumentationCategoryTests.cs` 3, `package.json` 2 e `HelpUrlTests.cs` 2. Exceção: 645 linhas, +29% sobre o teto de 500 e dentro dos +30% da ADR 0042. Motivo: o CA5 tem quatro guardas, e cada caso de falha tem o seu teste. Além disso, a URL do GitBook não tem barra no fim, então as 28 constantes e os 24 casos do teste mudam juntos. A Spec estimou ~5 arquivos. O helper e os testes dele ficam em arquivos próprios, para não misturar as guardas do manual com os testes da regra, e o `HelpUrlTests.cs` entra por consequência direta.
- Commit: `feat(docs): URLs e guardas de link no formato do GitBook`

## GB7b — Âncoras de HelpURL pelo título
### Detalhe (2026-11-05)
- CA: CA3, na parte das âncoras. Depende do GB7a, cuja guarda aponta as 8 âncoras.
- Escopo: em cada `[HelpURL]`, a âncora passa a ser o slug do título da seção do tipo. O título não muda (CA8). 8 âncoras em 10 arquivos:
  - `Runtime/GAS/Core/AbilitySystemComponent.cs:24`: `#abilitysystemcomponent` → `#id-1.5-abilitysystemcomponent-asc`;
  - `Runtime/GAS/Attributes/AttributeSet.cs:8`: `#attributeset` → `#id-1.2-atributos`;
  - `Runtime/GAS/Abilities/GameplayAbility.cs:36`: `#gameplayability` → `#id-1.4-gameplay-abilities-ga`;
  - `Runtime/GAS/Effects/GameplayEffect.cs:47`: `#gameplayeffect` → `#id-1.3-gameplay-effects-ge`;
  - `Runtime/GAS/Abilities/Projectiles/Projectile.cs:22`: `#projectile` → `#projeteis`;
  - `Runtime/Pooling/ComponentPooler.cs:11` e `PoolableObject.cs:9`: `#componentpooler` → `#componentpooler-e-ipoolable`;
  - `Runtime/Pooling/PoolManager.cs:11` e `PoolConfiguration.cs:10`: `#poolmanager` → `#poolmanager-pre-aquecimento`;
  - `Runtime/Time/PausableTimeSource.cs:11`: `#pausabletimesource` → `#grupo-pausavel`.

  Os slugs são os do Detalhe do GB6. Na implementação, a Fábrica os confere com o `ManualMarkdown.Slug` do GB7a.
- Conferido em 2026-11-05, depois do GB7a (8aa3afa): as linhas, os títulos e os slugs continuam os de cima. Nada mudou no Detalhe.
- Testes red: nenhum teste novo. Antes da mudança, o `ManualLinksTests.EveryHelpUrlHasPageAndAnchor` do GB7a falha com as 8 âncoras. Depois dela, ele falha só com o escopo do usuário do GB7a.
- Critério de pronto: a guarda fica nesse estado; `HelpUrlTests` passa; `git diff --check` limpo.
- Tamanho: ~20 linhas em 10 arquivos. Exceção: nenhuma.
- Commit: `fix(docs): âncoras de HelpURL pelo título no GitBook`

## GB8 — Processo sem MkDocs
### Detalhe (2026-11-05)
- CA: CA9, na parte que não é o MCP: o `AGENTS.md`, o guia do manual, as skills `/implementar` e `/revisao` e os agentes `controlador` e `examinador` descrevem o passe sem `mkdocs` nem `docfx`, com a geração e as guardas. O fluxo do change request, a skill `/commit` e o agente `fabrica` são do GB10 (lista no fim deste Detalhe).
- Base, lida em 2026-11-05 depois do GB7b (`13b7535`): itens 8, 10, 12 e 14 da 0044; emenda de 2026-11-04 da 0007 (itens 1 a 7), da 0040 e da 0042 (item 3). O GB11 está commitado (`da876f4`, `f1ee15a`). As emendas da 0002, da 0007 e da 0042 dizem que valem depois do GB9. O GB8 já escreve a regra nova porque o MkDocs saiu no GB2 e o passe só volta depois do GB9, então não há passe no meio com a regra antiga.
- Nomes conferidos no código: menu `SpellBook/Documentação/Gerar catálogo e exemplos` (`ReferenceGenerator.cs:20`); batchmode `-executeMethod SpellBook.Core.Editor.Documentation.ReferenceGenerator.GenerateFromCommandLine`, que sai com 0 ou com 1 e lista os marcadores quebrados no log (`:43-56`, `:72-75`); guardas `Documentacao` da lista do `DocumentationCategoryTests.cs:17-26` mais o `ManualCodeExamplesTests.ExpandedBlocksMatchSections` (`ManualCodeExamplesTests.cs:110`, categoria pela classe); `ManualMarkdown.Slug` (`Tests/Repository/EditMode/Documentation/ManualMarkdown.cs:28`), com a regra do Detalhe do GB5 e os níveis do GB7a; marcador do exemplo `{% code title="<caminho>:<seção>,<seção>" %}`, cerca ` ```csharp `, `{% endcode %}` (`ReferenceGenerator.cs:22-24`); seção no arquivo de exemplos continua marcada por `// --8<-- [start:<nome>]` e `// --8<-- [end:<nome>]` (`:210`); hints usados no manual: `{% hint style="info" %}` e `{% hint style="warning" %}`, fechados por `{% endhint %}`; URL `https://rook-studio.gitbook.io/spellbook` (`DocUrls.cs:14`, `package.json:10`).
- Escopo: os 8 arquivos da tabela e 4 por decisões do usuário de 2026-11-05 (3 no Detalhe do GB7a e 1 na resposta à pendência deste Detalhe). Troca exata por arquivo:
  - `AGENTS.md`:
    - `:114` (Controlador): "confere molde, siglas, `nav` e legendas" → "confere molde, siglas, `SUMMARY.md` e legendas"; sai ", e roda `mkdocs build --strict` conforme a seção 7" e entra, depois de "confere a declaração da Fábrica no relatório": "Não roda build: as guardas `Documentacao`, que o Examinador roda pelo `/revisao` depois da geração, fazem esse papel."
    - `:128` (Fluxo, item 5): "No passe de documentação, todo ticket passa pelo `/revisao`" → "No passe de documentação, a Fábrica roda **SpellBook > Documentação > Gerar catálogo e exemplos**, e todo ticket passa pelo `/revisao`"; "confere molde, siglas, `nav`, legendas, regras e declaração da humanizer, e roda `mkdocs build --strict`." → "confere molde, siglas, `SUMMARY.md`, legendas, regras e declaração da humanizer. Não há build."
    - `:135` (Tamanho): "saída de gerador com guarda (0040)" → "saída de gerador com guarda (a página do catálogo e os blocos C# expandidos, 0044)"; "a entrada do `nav` junto da página" → "a entrada do `SUMMARY.md` junto da página".
    - `:144`: "Manual, catálogo e referência da API só no passe de documentação (0007 e 0040)." → "Manual, catálogo e exemplos expandidos só no passe de documentação (0007, 0040 e 0044)."
  - `Documentation~/guia-do-manual.md`:
    - `:3`: "Vale pela emenda de 2026-11-01 da ADR 0007" → "Vale pela emenda de 2026-11-01 da ADR 0007 e pela ADR 0044".
    - Parte 1, o molde (`:10-43`, `:52`, `:55`; o Detalhe do GB5 a deixou para o GB8): sai o ` {#...}` do fim de cada título. `:35`: sai "Link para a referência da API.". `:41`: "Use `!!! note` e `!!! warning`." → "Use `{% hint style="info" %}` e `{% hint style="warning" %}`, fechados por `{% endhint %}`."
    - Parte 4:
      - `:89` → "1. O título não tem âncora escrita à mão. A âncora é a que o GitBook gera do título, pela regra do `ManualMarkdown.Slug`: letras minúsculas; acento e cedilha viram a letra base; código e ênfase entram só com o texto; espaço vira hífen, e hífens seguidos viram um; `&`, `<` e `>` viram `and`, `less-than` e `greater-than`; a pontuação some, menos o ponto depois de dígito; slug que começa com dígito ganha `id-`; título repetido na página ganha `-1`, `-2`. O título `#` da página não tem âncora, nem os de `#####` e `######`."
      - `:90` → "2. A seção de um componente ou asset tem como título o nome do tipo, então a âncora é o nome em minúsculas (`attributedefinition`). Uma reescrita não muda esse título."
      - `:101` → "5. Nenhum `[HelpURL]` aponta para a página do catálogo, que é gerada (0007, emenda de 2026-11-04, item 3)."
    - Parte 6:
      - `:113`: título "Mapa de páginas e `nav`" → "Mapa de páginas e `SUMMARY.md`".
      - `:115` → "O `SUMMARY.md` da raiz do manual (`manual/SUMMARY.md`) tem como títulos de grupo (`## Grupo`) os sete grupos, nesta ordem: Começar; Sistemas de habilidade; Sistemas de apoio; Editor e ferramentas; Exemplos; Guias; Referência. Grupo sem página fica fora do sumário. O rótulo de cada página é o título `#` dela, e cada página aparece uma vez."
      - `:117` → "Toda página nova entra no `SUMMARY.md` no mesmo ticket que a cria. Sem isso, a guarda `SummaryListsEveryPageOnce` falha."
      - `:121`: `index.md` → `README.md`.
      - `:127`: sai "referência da API (B-019g), ".
      - `:131`: "porque o `ManualLinksTests` não acha `pasta/index.md`" → "porque o `ManualLinksTests` procura a página de cada URL em `<caminho>.md` e só usa o `README.md` na raiz do manual".
      - `:132` → "O catálogo é gerado na máquina e commitado (0040 e 0044, item 7)."
    - Parte 7:
      - `:141` → "- Guardas: no lugar do build, as guardas `Documentacao` da parte 8 rodam na Unity, sob a trava, com `-testCategory "Documentacao"`. Quem não roda a Unity confere o resultado na `/revisao`."
      - `:142` (Diagrama novo, "abra a página no site gerado") sai. A conferência do desenho na prévia do change request é do GB10.
    - Parte 8:
      - `:144`: título "Catálogo e referência da API gerados" → "Catálogo e exemplos gerados".
      - `:146` → "O catálogo (`manual/referencia/catalogo.md`) e o corpo dos blocos C# marcados das páginas (parte 9) saem do código, pela Unity, e vão para o repositório (0040 e 0044, itens 7 e 8). Não edite esses trechos à mão."
      - `:148` (mapa da Unity, .NET, `docfx`) sai.
      - `:150` → "As guardas que comparam o código com o manual, e as que fazem o papel do build, ficam na categoria `Documentacao` do NUnit: o `ReferenceUpToDateTests` (catálogo), o `ManualCodeExamplesTests` (exemplos, parte 9) e seis testes do `ManualLinksTests`: `EveryHelpUrlHasPageAndAnchor`, `EveryDocUrlHasPage`, `NoStubPages`, `SummaryListsEveryPageOnce` (página fora do sumário ou repetida nele, entrada sem arquivo), `EveryRelativeLinkHasTarget` (link relativo para arquivo ou âncora que não existe, link que sai do manual, que começa em `/` ou que aponta para o próprio site) e `NoMkDocsSyntax` (sobra de `{#`, `!!!`, `???` ou `--8<--` na página). Quando cada suíte roda, e o que um ticket de código deixa para o passe de documentação, está no `AGENTS.md`, seção "Time de desenvolvimento"."
      - `:154` → "1. Rode **SpellBook > Documentação > Gerar catálogo e exemplos** na Unity, sob a trava. Em batchmode, a mesma geração roda com `-executeMethod SpellBook.Core.Editor.Documentation.ReferenceGenerator.GenerateFromCommandLine` e sai com 0 se deu certo ou com 1 se falhou. Um marcador quebrado não para os outros: a geração expande o resto e lista os quebrados no log."
      - `:155` → "2. O catálogo e as páginas que a geração mudou entram no mesmo diff. Sem isso, o `ReferenceUpToDateTests` ou o `ExpandedBlocksMatchSections` falha."
      - `:156` → "3. Rode a suíte com `-testCategory "Documentacao"`. Se o ticket tem código, a revisão roda também a suíte com `-testCategory "!Documentacao"`." (alinha com `AGENTS.md:143`, que já não manda rodar sem filtro).
      - `:158-165` (site sem a Unity, `mkdocs`, `dotnet docfx`, Action `docs`, versão do DocFX) sai, e entra um parágrafo: "Não há build local nem Action de documentação (0044, item 12). O GitBook publica o space público a cada push na `main` que muda a pasta do manual, e as guardas acima são a única validação."
    - Parte 9:
      - `:169`, segunda frase → "A página não tem código escrito à mão: antes do bloco vai um marcador com o arquivo e as seções, e a geração (parte 8) escreve o trecho no bloco." A citação passa a "(Spec `referencia-catalogo-e-api.md`, D9; ADR 0041 e 0044, item 8)".
      - `:173`, no fim: "O comentário guarda o formato antigo, `--8<--`, que a geração lê no arquivo de exemplos. Na página ele não entra." O exemplo `:175-187` fica.
      - `:189-195` → "Na página, escreva o marcador e um bloco C# vazio. O título do marcador é o caminho a partir da raiz do repositório, dois-pontos e as seções do mesmo arquivo, separadas por vírgula, na ordem em que entram no bloco. Depois rode a geração, que preenche o bloco:", com o exemplo `{% code title="Packages/com.spellbook/Tests/Core/EditMode/Examples/CoreManualExamples.cs:mathtools-using,mathtools-map" %}`, ` ```csharp `, ` ``` `, `{% endcode %}`, dentro da mesma cerca de quatro crases de hoje (o marcador real de `inicio/conceitos.md:133`).
      - `:201`, última frase → "O título do marcador lista então duas seções, a do `using` primeiro e a do trecho depois."
      - `:202` → "O recuo sai sozinho: a geração tira o recuo comum das linhas de cada seção e põe o recuo da cerca. Um membro de classe (um campo, um método) ou uma linha dentro de um método aparece na página a partir da coluna zero."
      - `:203` → "Um bloco junta seções do mesmo arquivo, na ordem do título. Seções de arquivos diferentes ficam em blocos diferentes."
      - `:205` → "A geração falha se o arquivo ou a seção não existe. O `ManualCodeExamplesTests` confere, na Unity:"
      - `:207` → "`NoHandWrittenCSharpBlocks`: todo bloco ` ```csharp `, ` ```cs ` ou ` ```c# ` tem o marcador logo antes, fora as páginas da lista de pendências. A lista só diminui, e a página que sai dela não volta;"
      - `:208` → "`SnippetsPointToExistingSections`: cada marcador vem logo antes de um bloco C#, tem `{% endcode %}` logo depois do bloco e aponta para um arquivo que existe e para seções com `start` e `end`;"
      - `:209` → "`SnippetSourcesAreCompiledCode`: o arquivo do marcador fica em `Packages/com.spellbook/Tests/`, ou em `Samples~/Sandbox/Scripts/` só no Sandbox;" e entra "- `ExpandedBlocksMatchSections`: cada bloco marcado é igual ao que a geração escreveria. Se não for, rode a geração."
      - `:211`: "vira texto ou um link para a API, e não" → "vira texto, e não".
      - `:213`: sai ", como o mapa da parte 8"; a última frase ("Quem quiser o link encontra na página do tipo na API, que o DocFX liga ao learn.microsoft.com.") sai.
      - `:215` → "Mudar um arquivo de exemplos deixa o bloco da página atrás do código até a próxima geração, no passe. O `ExpandedBlocksMatchSections` acusa a diferença."
  - `Documentation~/README.md:14`: "Site MkDocs (GitHub Pages), no estilo TopDown Engine" → "Site no GitBook (`https://rook-studio.gitbook.io/spellbook`), publicado pelo Git Sync a partir da `main`, no estilo TopDown Engine".
  - `README.md:18`: "[gabrielctorres.github.io/SpellBook](https://gabrielctorres.github.io/SpellBook/)" → "[rook-studio.gitbook.io/spellbook](https://rook-studio.gitbook.io/spellbook)".
  - `.claude/agents/controlador.md`:
    - `:3` (description): "no passe de documentação também faz as conferências do guia, humanizer e mkdocs build --strict" → "no passe de documentação também faz as conferências do guia e a humanizer, sem build".
    - `:14`: "confere molde, siglas, `nav` e legendas" → "confere molde, siglas, `SUMMARY.md` e legendas"; "; roda `mkdocs build --strict` conforme a seção 7" → ". Não roda build: as guardas `Documentacao` do Examinador fazem esse papel".
  - `.claude/agents/examinador.md:19`: "catálogo ou referência desatualizados" → "catálogo ou exemplos desatualizados"; "o Controlador faz as buscas da seção 7, confere regras do guia e humanizer, e roda `mkdocs build --strict`." → "o Controlador faz as buscas da seção 7 e confere as regras do guia e a humanizer. As guardas `Documentacao` que você roda fazem o papel do build."
  - `.claude/skills/implementar/SKILL.md`:
    - `:13`: "não rode a geração da referência" → "não rode **Gerar catálogo e exemplos**".
    - `:17`: "No passe de documentação, rode também as buscas da parte 7 do `guia-do-manual.md`, o `mkdocs build --strict` e a suíte com `-testCategory "Documentacao"`." → "O ticket do passe de documentação usa a Unity: sob a trava, rode a geração (**SpellBook > Documentação > Gerar catálogo e exemplos**, ou em batchmode `"$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD" -executeMethod SpellBook.Core.Editor.Documentation.ReferenceGenerator.GenerateFromCommandLine -logFile "$PWD/Logs/unity-generate.log"`, que sai com 1 se um marcador estiver quebrado), as buscas da parte 7 do `guia-do-manual.md` e a suíte com `-testCategory "Documentacao"`." Sem `-quit`: o método fecha a Unity com o código de saída.
  - `.claude/skills/revisao/SKILL.md`:
    - `:9`: "o Controlador revisa o texto conforme a seção 7 do guia e a humanizer, e roda `mkdocs build --strict`;" → "o Controlador revisa o texto conforme a seção 7 do guia e a humanizer, sem build;".
    - `:14`: "saída gerada com guarda (0040)" → "saída gerada com guarda (a página do catálogo e os blocos C# expandidos, 0044)"; "a entrada do `nav` da página" → "a entrada do `SUMMARY.md` da página".
    - `:21`: "nem por catálogo ou referência desatualizados" → "nem por catálogo ou exemplos desatualizados"; "a humanizer e o `mkdocs build --strict`." → "e a humanizer."
  - Por decisão do usuário (Detalhe do GB7a), os outros textos que citam o `github.io`:
    - `Packages/com.spellbook/README.md:20`: "https://gabrielctorres.github.io/SpellBook/" → "https://rook-studio.gitbook.io/spellbook".
    - `Packages/com.spellbook/Samples~/Sandbox/README.md:61`: "<https://gabrielctorres.github.io/SpellBook/exemplos/sandbox/>" → "<https://rook-studio.gitbook.io/spellbook/exemplos/sandbox>" (`DocUrls.Samples`).
    - `Packages/com.spellbook/CHANGELOG.md:88` ([Não lançado], Adicionado): "https://gabrielctorres.github.io/SpellBook/modulos/dano-escudo-e-morte/" → "https://rook-studio.gitbook.io/spellbook/modulos/dano-escudo-e-morte" (`DocUrls.Damage`).
  - O formato da URL de página é o inferido no Detalhe do GB7a, que o GB9 confere no site final. A troca segue o `DocUrls`, para os dois não divergirem.
  - Por decisão do usuário de 2026-11-05 (resposta à pendência deste Detalhe):
    - `Packages/com.spellbook/CHANGELOG.md:34` ([Não lançado], Quebras): "**SpellBook > Documentação > Gerar referência** escreve só a página do catálogo" → "**SpellBook > Documentação > Gerar catálogo e exemplos** escreve a página do catálogo". O nome é o do `MenuPath` (`ReferenceGenerator.cs:20`). O "só" sai porque o menu também expande os exemplos desde o GB4. O resto da entrada não muda. É o mesmo arquivo do item do `:88`, então não soma arquivo.
    - `Packages/com.spellbook/Tests/GAS/EditMode/GasSourceScanTests.cs:112-113`, só o comentário `<summary>` do `NoProtectedInternal_InActions`. O teste não muda. "e o docfx, que compila o pacote como / um assembly só, recusa a forma que a Unity pede no Bridges." → "então a assinatura do / override muda conforme o assembly." (a barra marca a quebra de linha). O comentário continua dizendo o que a regra evita, mas sem citar a ferramenta que saiu.
    - `CHANGELOG.md:106-110` ([Não lançado], Documentação), pela segunda resposta do usuário no mesmo dia. A referência da API entrou e saiu antes de qualquer versão, então quem atualiza o pacote nunca a viu. A forma mais simples é tirar o que fala dela e manter o resto da seção no formato de hoje:
      - `:107` (Catálogo de tipos) fica, porque o catálogo continua. "pelo menu **SpellBook > Documentação > Gerar referência**, e cada tipo leva à página dele na API." → "pelo menu **SpellBook > Documentação > Gerar catálogo e exemplos**." O resto da linha não muda.
      - `:108` (Referência da API) sai.
      - `:109` (Links da API para a Unity) sai também. O pedido citava só as linhas 107 e 108, mas esta descreve o mapa da Unity e o link do .NET, que eram da mesma API e saíram com ela no GB3, então contradiz a linha 34 do mesmo jeito.
      - `:110` (Exemplos do manual tirados de testes) fica. A regra continua, e só o mecanismo mudou.
      - A linha 34 (Quebras) não muda além da troca do menu, descrita acima.
- Fica fora do GB8 e vai para o GB10 (fluxo do MCP): no `AGENTS.md`, o "um ticket = um commit" salvo no passe (0042, emenda de 2026-11-04, item 1), o commit do change request (0002, emenda de 2026-11-04) e a conferência do Controlador no change request antes do merge, pelo MCP; no guia, a escrita pelo MCP, o `SUMMARY.md` ajustado pela estrutura do GitBook, a conferência do diagrama na prévia do change request (substitui a linha `:142` que sai) e a ordem merge → avanço rápido → geração; no `/implementar`, o change request e essa ordem; no `controlador.md`, a leitura pelo MCP; o `fabrica.md` inteiro, inclusive `:45` ("nem regenera a referência da API"), e a skill `/commit`.
- Testes red: nenhum. O único código é um comentário, que não muda comportamento. O red é o critério de pronto falhando antes da mudança. Medido em 2026-11-05: a busca 1 acha 29 linhas, a busca 2 acha 17, a busca 3 acha 12 e a busca 8 acha 5 (1 no `.cs` e 4 no `CHANGELOG.md`: as linhas 34, 107, 108 e 109).
- Revisão: o ticket tem um `.cs`, então passa pelo `/revisao` do Examinador, com a suíte completa (`-testCategory "!Documentacao"`) e as guardas `Documentacao`. O `GasSourceScanTests` precisa continuar verde. O Codex não compila o C#, então diz no resultado que isso não foi verificado.
- Critério de pronto (da raiz do repositório, sem Unity), com `F="AGENTS.md README.md Documentation~/README.md Documentation~/guia-do-manual.md .claude/agents/controlador.md .claude/agents/examinador.md .claude/skills/implementar/SKILL.md .claude/skills/revisao/SKILL.md Packages/com.spellbook/README.md Packages/com.spellbook/Samples~/Sandbox/README.md"`:
  1. `git grep -n -i -E "mkdocs|docfx|dotnet|pymdownx|dedent_subsections|check_paths|github\.io|workflow_dispatch|api/" -- $F | grep -v NoMkDocsSyntax` não acha nada;
  2. `` git grep -n -E '`nav`|index\.md|referência da API|!!! (note|warning)|Gerar referência' -- AGENTS.md Documentation~/guia-do-manual.md .claude/agents/controlador.md .claude/agents/examinador.md .claude/skills/implementar/SKILL.md .claude/skills/revisao/SKILL.md `` não acha nada;
  3. `grep -nE '^ *#{1,6} .*\{#' Documentation~/guia-do-manual.md` não acha nada;
  4. `git grep -n "github.io" -- Packages/com.spellbook/CHANGELOG.md` não acha nada;
  5. cada nome que o texto novo cita existe no código: `for t in SummaryListsEveryPageOnce EveryRelativeLinkHasTarget NoMkDocsSyntax EveryHelpUrlHasPageAndAnchor EveryDocUrlHasPage NoStubPages NoHandWrittenCSharpBlocks SnippetsPointToExistingSections SnippetSourcesAreCompiledCode ExpandedBlocksMatchSections CommittedCatalog_MatchesGenerated GenerateFromCommandLine; do git grep -q "void $t(" -- Packages || echo "falta $t"; done` não imprime nada, e `git grep -n "Gerar catálogo e exemplos" -- Packages/com.spellbook/Editor/Core/Documentation/ReferenceGenerator.cs` acha o `MenuPath`;
  6. o texto novo segue a humanizer (o Codex declara no resultado);
  7. `git diff --check` limpo;
  8. `git grep -n -i -e "docfx" -- Packages/com.spellbook/Tests/GAS/EditMode/GasSourceScanTests.cs` não acha nada. Na seção [Não lançado] do `CHANGELOG.md`, `sed -n '/^## \[Não lançado\]/,/^## \[2\.0\.0\]/p' Packages/com.spellbook/CHANGELOG.md | grep -nE "Gerar referência|Referência da API|Links da API|página dele na API"` também não acha nada;
  9. no `.cs`, o diff só toca linhas que começam com `///`: `git diff -U0 -- Packages/com.spellbook/Tests/GAS/EditMode/GasSourceScanTests.cs | grep -E '^[-+][^-+]' | grep -vE '^[-+]\s*///'` não acha nada;
  10. o Examinador roda a suíte completa e as guardas `Documentacao`, e as duas ficam como estavam antes do ticket.
- Tamanho: 4 linhas de `.cs` (as 2 linhas do comentário, removidas e escritas de novo; a quebra cai no meio da frase, então não dá para mudar só uma linha) em 12 arquivos. Exceção: 12 arquivos, +20% sobre o teto de 10, dentro dos +30% da ADR 0042. Motivo: a tabela conta os 8 arquivos do processo. A decisão do usuário de 2026-11-05 no Detalhe do GB7a soma o `README.md` do pacote, o do Sandbox e o `CHANGELOG.md`, que citam o `github.io`. A decisão do mesmo dia sobre a pendência deste Detalhe soma o `GasSourceScanTests.cs`, e o `CHANGELOG.md:34` cai num arquivo que já estava na conta.
- Commit: `docs(processo): processo do manual sem MkDocs nem DocFX`

## GB10 — Fluxo do MCP
### Detalhe (2026-11-05)
- CA: CA9, na parte do MCP que o GB8 deixou para cá (lista no fim do Detalhe do GB8), e CA10, na parte que vive no repositório: a lista `tools` da Fábrica e o passo que traz o commit do change request. O percurso de ponta a ponta do CA10 (cenário, passo 4) é do GB9.
- Base, lida em 2026-11-05 depois do GB8 (`17bff5f`): 0044, itens 8, 13 e 14, e a emenda de 2026-11-05 (pontos 6 e 7, decisão 2); 0002, emenda de 2026-11-04; 0042, emenda de 2026-11-04 (itens 1, 2 e 4); 0007, emenda de 2026-11-04 (itens 5 a 7). Commit do spike `934c949`: autor é a conta do usuário, mensagem `GITBOOK-1: <título do change request>`, e ele mexe em `Documentation~/manual/`, no `AGENTS.md` e no `CLAUDE.md` (o bloco aceito na decisão 2). O GB8 já tirou o `mkdocs` e o `docfx` e escreveu a geração e as guardas; este Detalhe não repete nenhuma troca dele.
- Ferramentas do MCP (item 13 da 0044). O servidor se chama `gitbook` nas instruções do MCP desta sessão (2026-11-05), então o nome completo é `mcp__gitbook__<ferramenta>`. Entram as sete do item 13: `create_change_request`, `invoke_operation`, `submit_or_merge_change_request`, `list_sites`, `get_site_structure`, `get_page` e `get_usage_guide`. O GB1 citou `update_page` e `insert_page` como passos do fluxo, mas não como ferramentas com nome conferido, e o CA10 pede só as do item 13; criar e mudar página é feito pelo `invoke_operation` com `updateChangeRequestContent`. O `search` e o `report_tool_feedback` do servidor ficam fora. Não conferido aqui, porque a Fábrica ainda não tem o MCP: se a sessão mostra esses nomes e se a autenticação do usuário vale no terminal do Orca. O GB9 confere no cenário, passo 4. Se lá faltar uma ferramenta, a lista volta ao gate (muda o CA10).
- Onde fica o passo de trazer o commit. O `ticket.sh` está fora do repositório (`spec/processo-enxuto.md`, "Script da fila"), e o `/commit` já é o "passo do script da fila" que usa git. O passo entra na skill `/commit` como um modo próprio, `/commit <design doc> <ticket> change-request`, chamado depois do merge do change request e antes da geração. Assim nenhum papel ganha git que altera estado.
- Escopo, com a troca por arquivo:
  - `.claude/agents/fabrica.md`:
    - `:4` (`tools`): no fim da lista, antes do `Agent(...)`, entram `mcp__gitbook__create_change_request, mcp__gitbook__invoke_operation, mcp__gitbook__submit_or_merge_change_request, mcp__gitbook__list_sites, mcp__gitbook__get_site_structure, mcp__gitbook__get_page, mcp__gitbook__get_usage_guide`.
    - `:3` (description), depois de "testá-lo (/implementar).": "No passe de documentação, escreve o texto no space de rascunho do GitBook pelo MCP, num change request do ticket."
    - "Faz", item novo depois de `:15`: "- No passe de documentação, é o único papel que escreve pelo MCP do GitBook, e só no space de rascunho (0044, item 13). Abre um change request por ticket, com o número do ticket no título, escreve só as páginas do Detalhe e o junta depois da conferência do Controlador."
    - "Não faz", item novo depois de `:34`: "- Não escreve no space público nem junta change request nele. Pelo `invoke_operation`, usa só a operação `updateChangeRequestContent`; nada de publicar, configurar site ou space. Se o fluxo pedir outra operação, pare e devolva ao Controlador."
    - `:33`: no fim, entra "Juntar o change request no rascunho não é comando `git`: o commit é do GitBook, e o `/commit <design doc> <ticket> change-request` o traz ao checkout."
    - `:45` → "- Não escreve no manual nem roda **Gerar catálogo e exemplos**, salvo no passe de documentação (emenda de 2026-11-03 da 0007). No passe, siga `Documentation~/guia-do-manual.md` (partes 8 e 10): o texto vai pelo MCP, e no checkout só a geração escreve em `Documentation~/manual/`. No CHANGELOG, escreva só a quebra com a migração."
  - `.claude/agents/controlador.md`:
    - `:14` → "- No passe de documentação, confere o texto no change request, antes do merge: lê pelo MCP as páginas do change request, confere molde, siglas, `SUMMARY.md` e legendas pelo guia, abre a prévia para ver os diagramas desenhados, lê o texto novo pelas regras de `C:/Users/gabri/.claude/skills/humanizer/SKILL.md` e confere a declaração da Fábrica. Problema volta à Fábrica, no mesmo change request, e as conferências se repetem. Com o texto aprovado, libera o merge à Fábrica. Depois do merge, roda `/commit <design doc> <ticket> change-request` pelo script da fila. Na branch do lote, antes do `/commit`, roda `git diff --check` e as conferências por busca da seção 7 do guia. Não roda build: as guardas `Documentacao` do Examinador fazem esse papel. O usuário lê o texto no PR."
    - "Não faz", item novo depois de `:19`: "- Usa o MCP do GitBook só para ler. Não cria, não edita e não junta change request (0044, item 13)."
    - `:3` (description): "no passe de documentação também faz as conferências do guia e a humanizer, sem build" → "no passe de documentação também confere o change request pelo MCP, só lendo, e faz as conferências do guia e a humanizer, sem build".
  - `.claude/skills/commit/SKILL.md`:
    - `:3` (description), no fim: "No passe de documentação, também traz ao checkout do ticket o commit do change request, só por avanço rápido."
    - `:4`: `<design doc> <ticket> [release]` → `<design doc> <ticket> [change-request|release]`.
    - Seção nova antes do item 1, "**Com `change-request`** (passe de documentação, depois do merge do change request no rascunho; 0044, item 13). Faça só estes passos e pare:":
      1. Pré-condição: a Fábrica avisou o merge do change request do ticket; não há `.claude/editor.lock` na raiz do checkout; `git status --porcelain -- AGENTS.md CLAUDE.md` está vazio. Se faltar algo, pare.
      2. Na correção (um change request novo do mesmo ticket), o checkout pode ter a saída da geração anterior em `Documentation~/manual/`. Ela é refeita pela geração, então descarte-a: `git restore --staged --worktree -- Documentation~/manual`. Fora desse caso, `git status --porcelain -- Documentation~/manual` precisa estar vazio; se não estiver, pare.
      3. `git fetch origin <branch do lote>`.
      4. Confira o que chega: `git log --format='%h %an %s' HEAD..origin/<branch do lote>`. Para cada commit cujo assunto começa por `GITBOOK-`, o assunto cita o ticket, `git show --format= --name-only <commit>` lista só caminhos em `Documentation~/manual/`, mais o `AGENTS.md` e o `CLAUDE.md` (bloco da decisão 2 da emenda de 2026-11-05 da 0044), e `git show --check --format= <commit>` não acusa nada. Se algo falhar, pare e devolva ao Controlador com a saída.
      5. Avanço rápido, sem merge nem rebase: `git merge --ff-only origin/<branch do lote>`. Num worktree (ADR 0005), faça o mesmo também no checkout da branch do lote, para o cherry-pick do item 2 cair em cima do commit do GitBook. Se o avanço rápido não der, pare e devolva ao Controlador.
      6. Relate os commits trazidos (hash, autor e assunto) e os caminhos de cada um. Não commite: o próximo passo é a geração, pela Fábrica.
    - `:12` (Pré-condição), no fim: "No passe de documentação, o commit do change request do ticket já chegou ao checkout pelo modo `change-request`."
    - `:13` (Branch), no fim: "Antes do cherry-pick, a branch do lote precisa estar em dia com `origin` por avanço rápido (`git merge --ff-only origin/<branch do lote>`); se não der, pare."
    - `:14` (Stage seletivo), depois da primeira frase: "No passe, entram também as páginas e o catálogo que a geração mudou em `Documentation~/manual/`."
  - `.claude/skills/implementar/SKILL.md`:
    - `:13`, a frase "No passe, leia também `Documentation~/guia-do-manual.md`." → "No passe, leia também `Documentation~/guia-do-manual.md`; o texto vai pelo MCP, num change request do space de rascunho (parte 10 do guia), e no checkout só a geração escreve em `Documentation~/manual/`."
    - Passo novo entre o 1 e o 2, e os passos 2 a 7 passam a 3 a 8 (as citações "passo 2" e "passo 5" do texto acompanham): "2. **Change request** (só no passe de documentação; 0044, item 13). Pelo MCP, abra no space de rascunho um change request com o número do ticket no título e escreva nele só as páginas do Detalhe, pela parte 10 do guia. Aplique a humanizer ao texto novo. Avise o Controlador, que confere o texto no change request. Corrija no mesmo change request o que ele devolver. Com o texto aprovado, junte o change request e avise o Controlador, que roda `/commit <design doc> <ticket> change-request`. Se o GitBook acusar conflito no merge, pare e devolva ao Controlador. A geração (passo 6) só roda depois que esse passo trouxe o commit ao checkout."
    - `:17` (passo 5, que vira 6): "No passe de documentação, a Fábrica usa a Unity: sob a trava, rode a geração" → "No passe de documentação, a Fábrica usa a Unity: depois que o commit do change request chegou ao checkout, sob a trava, rode a geração".
    - `:28` (Relatório, que vira passo 8), depois de "e liste as páginas": ", com o título do change request e o hash do commit `GITBOOK-` que chegou ao checkout. Se uma guarda reprovar o texto, a correção é um change request novo do mesmo ticket, pelo passo 2".
  - `.claude/skills/revisao/SKILL.md:11` (consequência direta da 0042, emenda de 2026-11-04, item 2): depois da primeira frase, entra "No passe de documentação, a base é o commit anterior ao primeiro commit `GITBOOK-` do ticket, para o diff somar os change requests e o que o `/commit` vai commitar." Sem isso, o Examinador conta só a geração e o código e não vê os arquivos do change request no teto.
  - `AGENTS.md`:
    - `:114` (Controlador): o trecho "No passe de documentação, antes do `/commit`, roda `git diff --check`, as conferências por busca da seção 7 de `Documentation~/guia-do-manual.md`, confere molde, siglas, `SUMMARY.md` e legendas de diagramas pelo guia, lê o texto novo do manual pelas regras da humanizer (`C:/Users/gabri/.claude/skills/humanizer/SKILL.md`) e confere a declaração da Fábrica no relatório." → "No passe de documentação, antes do merge do change request, lê o change request pelo MCP, só para ler, e confere molde, siglas, `SUMMARY.md`, legendas e diagramas na prévia, o texto novo pelas regras da humanizer (`C:/Users/gabri/.claude/skills/humanizer/SKILL.md`) e a declaração da Fábrica. Antes do `/commit`, na branch do lote, roda `git diff --check` e as conferências por busca da seção 7 de `Documentation~/guia-do-manual.md`." O resto da linha fica.
    - `:115` (Fábrica), no fim: "No passe de documentação, escreve o texto no space de rascunho do GitBook pelo MCP, num change request do ticket, e o junta depois da conferência do Controlador."
    - Item novo depois de `:120`: "- **MCP do GitBook** (0044, item 13): só a Fábrica do passe escreve, e só no space de rascunho, ligado à branch do lote. O Controlador só lê. O Examinador, o Desenhista, o Codex e o Antigravity não o usam. Nenhum agente escreve no space público."
    - `:121`, no fim: "No passe de documentação, o merge do change request no rascunho cria um commit do GitBook na branch do lote, o único fora do `/commit` (0002, emenda de 2026-11-04). O `/commit <design doc> <ticket> change-request` o traz ao checkout do ticket, só por avanço rápido."
    - `:128` (Fluxo, item 5): "No passe de documentação, a Fábrica roda **SpellBook > Documentação > Gerar catálogo e exemplos**, e todo ticket passa pelo `/revisao`, que roda as guardas `Documentacao` sob a trava; em paralelo, antes do `/commit`, o Controlador faz `git diff --check`, as conferências da seção 7 do guia, confere molde, siglas, `SUMMARY.md`, legendas, regras e declaração da humanizer. Não há build." → "No passe de documentação, a Fábrica escreve o texto num change request do space de rascunho, pelo MCP. O Controlador confere molde, siglas, `SUMMARY.md`, legendas, diagramas na prévia, regras e declaração da humanizer no change request, e a Fábrica o junta. O `/commit <design doc> <ticket> change-request` traz o commit do GitBook ao checkout por avanço rápido, a Fábrica roda **SpellBook > Documentação > Gerar catálogo e exemplos**, e todo ticket passa pelo `/revisao`, que roda as guardas `Documentacao` sob a trava. Antes do `/commit`, o Controlador faz `git diff --check` e as conferências da seção 7 do guia. Se uma guarda reprovar o texto, a correção é um change request novo do mesmo ticket. Não há build." O resto do item fica.
    - `:133`: "- Um ticket = uma issue = um commit." → "- Um ticket = uma issue = um commit. No passe de documentação, o ticket tem também o commit de cada change request, e o teto soma todos (0042, emenda de 2026-11-04)."
    - `:144`, no fim: "No checkout, ninguém edita à mão a pasta do manual: só a geração escreve nela, depois do merge do change request. Uma página fica em um change request aberto por vez (0044, item 13)."
  - `Documentation~/guia-do-manual.md`:
    - `:117` (parte 6) → "Toda página nova entra no `SUMMARY.md` no mesmo ticket que a cria. No passe, a Fábrica cria a página pelo MCP, e o GitBook escreve a entrada no sumário; ela confere, no mesmo change request, o grupo e o rótulo da entrada. Sem a entrada, a guarda `SummaryListsEveryPageOnce` falha."
    - Parte 7, item novo depois de `:139` (Diagrama sem legenda): "- Diagrama desenhado: o Controlador abre a prévia do change request e confere que cada diagrama aparece desenhado."
    - Parte 8, `:151`: o passo 1 começa por "Depois que o commit do change request chegou ao checkout (parte 10), rode" no lugar de "Rode".
    - Parte 10, nova, no fim do arquivo, "## 10. Escrita pelo MCP" (0044, item 13):
      - "O texto do manual é escrito no space de rascunho do GitBook, ligado à branch do lote, e não no checkout. Só a Fábrica do ticket escreve, num change request com o número do ticket no título. O space público só recebe da `main`."
      - "No change request, a Fábrica escreve só as páginas do Detalhe. Não toca na página do catálogo nem no corpo de um bloco C#: para um exemplo novo, escreve o marcador da parte 9 e um bloco C# vazio. Sem imagem e sem bloco de desenho do GitBook; diagrama só em Mermaid, com legenda."
      - "Uma página fica em um change request aberto por vez. Se o GitBook acusar conflito no merge, a Fábrica para e devolve ao Controlador. No conflito, vence o repositório para o que é gerado (blocos C# e catálogo) e o change request para o texto."
      - "A ordem é: o Controlador confere o texto no change request; a Fábrica o junta, e o GitBook faz o commit `GITBOOK-<n>` na branch do lote; o `/commit <design doc> <ticket> change-request` traz o commit ao checkout por avanço rápido; a geração (parte 8) preenche os blocos; as guardas rodam. Se uma guarda reprovar o texto, a correção é um change request novo do mesmo ticket."
- Fica fora, com o dono:
  - o `ticket.sh`, fora do repositório: passa a chamar `/commit <design doc> <ticket> change-request` depois do aviso de merge da Fábrica. É do usuário, como no PS1;
  - o bloco que o GitBook injeta no `AGENTS.md` e no `CLAUDE.md` (decisão 2) manda rodar `npx skills add gitbookio/gitbook-skills`, o que bate com "Do not add new dependencies without explicit approval". O GB10 não mexe nisso; fica como pendência ao Controlador;
  - `examinador.md` não muda: o Examinador não usa o MCP, e o que ele roda no passe já está no GB8;
  - o percurso do cenário, a autenticação e os nomes na sessão são do GB9.
- Testes red: nenhum, porque o ticket não tem código (só `.md` de agentes, skills, guia e `AGENTS.md`). O red é o critério de pronto falhando antes da mudança. Medido em 2026-11-05: a busca 1 acha 0 nomes `mcp__`; a busca 2 aponta os 7 arquivos; a busca 3 acha `fabrica.md:45` ("referência da API"); a busca 4 não acha `ff-only`.
- Revisão: ticket só de texto, fora do passe. Não passa por `/revisao`; o Controlador roda `git diff --check` e confere o diff contra o objetivo e os CA da issue #365.
- Critério de pronto (da raiz, sem Unity), com `F="AGENTS.md Documentation~/guia-do-manual.md .claude/skills/implementar/SKILL.md .claude/skills/commit/SKILL.md .claude/skills/revisao/SKILL.md .claude/agents/fabrica.md .claude/agents/controlador.md"`:
  1. `grep '^tools:' .claude/agents/fabrica.md | grep -o 'mcp__[a-z_]*' | sort | tr '\n' ' '` imprime exatamente `mcp__gitbook__create_change_request mcp__gitbook__get_page mcp__gitbook__get_site_structure mcp__gitbook__get_usage_guide mcp__gitbook__invoke_operation mcp__gitbook__list_sites mcp__gitbook__submit_or_merge_change_request `, e `git grep -n "mcp__" -- .claude/agents/controlador.md .claude/agents/examinador.md` não acha nada;
  2. `for f in $F; do git grep -q "change request" -- "$f" || echo "falta $f"; done` não imprime nada;
  3. `git grep -n -i -E "mkdocs|docfx|referência da API" -- $F | grep -v NoMkDocsSyntax` não acha nada;
  4. `git grep -n "ff-only" -- .claude/skills/commit/SKILL.md` acha os itens 5 e `:13`, e `git grep -n -E "rebase|reset|--force|git pull|merge origin|merge --no-ff" -- .claude/skills/commit/SKILL.md` só acha a frase "sem merge nem rebase" do item 5;
  5. `git grep -n "change-request" -- .claude/skills/commit/SKILL.md .claude/skills/implementar/SKILL.md .claude/agents/controlador.md AGENTS.md Documentation~/guia-do-manual.md` acha cada um dos cinco arquivos;
  6. `grep -c "^[0-9]\. \*\*" .claude/skills/implementar/SKILL.md` imprime 8, e as citações de passo do texto batem com a numeração nova;
  7. `git diff --name-only` só lista os 7 arquivos de `F` e este design doc;
  8. o texto novo segue a humanizer (o Codex declara no resultado);
  9. `git diff --check` limpo.
- Tamanho: 0 linhas que contam (só `.md`) em 7 arquivos: `AGENTS.md`, `guia-do-manual.md`, `/implementar`, `/commit`, `/revisao`, `fabrica.md` e `controlador.md`. A tabela estimava ~6; o `/revisao` entra como consequência direta da emenda de 2026-11-04 da 0042, item 2. Exceção: nenhuma.
- Commit: `docs(processo): fluxo do change request do GitBook pelo MCP`
