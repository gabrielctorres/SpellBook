# Design doc: Migração do manual para o GitBook
Épico: `epic:migracao-gitbook` · Backlog: B-042 (com B-019g, B-035 e B-031) · ADRs: 0044 (Aceita) · Status: aprovado no gate de 2026-11-04 (Q1 a Q6)

## Problema
O manual é publicado pelo MkDocs no GitHub Pages, junto com uma referência da API em DocFX. O usuário decidiu (2026-11-04, B-042) publicar o manual no GitBook, pelo Git Sync, tirar a API gerada e deixar os agentes escreverem o manual pelo MCP do GitBook. Quem usa o pacote passa a ter só o manual e os exemplos, no GitBook. Para isso, o repositório e o time precisam:
- de um layout que o Git Sync leia;
- de um sumário (`SUMMARY.md`) no lugar do `nav`;
- de outro jeito de pôr os exemplos dos testes nas páginas, porque o GitBook não inclui arquivo;
- de guardas que façam o papel do `mkdocs build --strict`;
- de um fluxo em que o agente escreve no GitBook sem que o texto entre na `main` por fora do PR e das guardas.

O botão de ajuda do Inspector (`[HelpURL]`) também precisa continuar abrindo a seção certa.

## Critérios de aceite
1. **CA1.** O manual está publicado num espaço público do GitBook, sincronizado com a `main`, no subdomínio `gitbook.io`. O sumário segue os sete grupos do guia do manual, e cada página aparece nele uma vez. Nenhum documento interno (`prd/`, `spec/`, `adr/`, backlog) é publicado.
2. **CA2.** O que o site público mostra é o que está commitado na `main`, na pasta do manual. Nenhum agente escreve no espaço público.
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
10. **CA10.** No passe de documentação, a Fábrica escreve uma página num change request do espaço de rascunho, pelo MCP. Depois do merge, o commit do GitBook está na branch do lote, a geração preenche os blocos C#, as guardas `Documentacao` passam, e a página só chega ao site público pelo PR do lote. A Fábrica tem na lista `tools` só as ferramentas do MCP do item 13 da 0044.

## Cenário no Sandbox
Com o Sandbox importado (`Packages/com.spellbook/Samples~/Sandbox`) e os dois espaços sincronizados:
1. Abra a cena, selecione o objeto do `SandboxDirector` e clique no ícone de ajuda do componente. O navegador abre a página do Sandbox no GitBook, na seção do `SandboxDirector` (CA3).
2. Na mesma página, os blocos C# mostram o código atual de `Samples~/Sandbox/Scripts/`, e o diagrama da cena aparece desenhado (CA4, CA6).
3. Mude uma linha marcada de um exemplo em `Tests/` e rode a suíte `Documentacao`: a guarda do exemplo falha. Rode **Gerar catálogo e exemplos**, e ela volta a passar (CA4).
4. No espaço de rascunho, a Fábrica abre um change request que acrescenta um parágrafo e um marcador com bloco vazio na página do Sandbox, e o junta. O commit aparece na branch do lote, a geração preenche o bloco e as guardas passam. O site público não muda até o merge do PR (CA2, CA10).
5. No site público, percorra o sumário: os sete grupos, nenhuma página repetida, nada de `spec/` ou `adr/` (CA1).

## Fora do escopo
- Reescrever o texto das páginas. A migração mexe só na sintaxe, nos links e nos exemplos (CA8). O texto novo continua nos tickets do passe (MN4 a MN36).
- A conversão das páginas pelo MCP. A migração em si (GB2 a GB7) é feita no repositório, antes de o fluxo do MCP valer.
- Tradução do manual.
- Versões do manual por release do pacote.
- Uso do MCP para estatísticas do site, membros ou plano.

## Decisões do épico
- **Respostas do gate de 2026-11-04 (aprovadas).** Q1: o catálogo fica, gerado e sem link para a API. Q2: a pasta continua `Documentation~/manual/` se o GB1 mostrar que o Git Sync aceita o til; se não aceitar, vai para `docs/` na raiz. Q4: subdomínio `gitbook.io`. Q5: remoção completa do DocFX, de `api/`, do mapa da Unity, da impressão digital, do `docs.yml`, do `mkdocs.yml` e do Python. Q3: os agentes editam pelo MCP. Q6: dois espaços, o público ligado à `main`, sem agente escrevendo, e o de rascunho ligado à branch do lote, onde a Fábrica edita por change request (0044, itens 3 e 13).
- **O spike vem primeiro.** Os pontos não confirmados (ADR 0044, "Em aberto") são verificados no GB1. Até lá, eles não são tratados como fato. Os GB2, GB4, GB5, GB6 e GB10 esperam o resultado.
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
| GB2 | Layout e sumário: arquivo de configuração do GitBook na raiz, conforme o GB1, e `SUMMARY.md` tirado do `nav`. Remove o `mkdocs.yml`, o `requirements-docs.txt` e o `docs.yml` (remoção pura). Se o GB1 recusar o til, move a pasta para `docs/` (movimento puro, `R100`). ~3 arquivos | CA1, CA7 | GB1 | não (Codex) |
| GB3 | Tirar a API: apaga `api/`, `.config/` e o `DocfxConfig`, o `ReferenceFingerprint` e o `UnityXrefMap`, com os testes deles (remoção pura). O `ReferenceGenerator` passa a gerar só o catálogo, a página do catálogo perde o link para a API e o `{#id}`, e o `ReferenceUpToDateTests` e o `DocumentationCategoryTests` ficam só com o catálogo. ~200 linhas, ~6 arquivos | CA7 | — | sim |
| GB4 | Exemplos expandidos: o expansor de marcadores, a entrada **Gerar catálogo e exemplos**, o `ManualCodeExamplesTests` reescrito para o marcador e a expansão nas 7 páginas que já usam inclusão. ~350 linhas e ~11 arquivos: 4 de código e as 7 páginas, porque trocar as linhas `--8<--` pelo marcador é escrito à mão e conta. O código expandido não conta. Fica dentro da exceção de +30%, com o motivo no Detalhe. Se passar disso, a troca das páginas vira um ticket próprio | CA4 | GB1, GB3 | sim |
| GB5 | Sintaxe das páginas, parte 1: `index.md`, `inicio/` (3), `ferramentas/` (3), `exemplos/sandbox.md` e `regras/`. Títulos sem `{#id}`, hints, Mermaid e links pelo resultado do GB1. 9 arquivos | CA6, CA8 | GB2, GB4 | não (Codex) |
| GB6 | Sintaxe das páginas, parte 2: as 6 páginas de `modulos/`. 6 arquivos | CA6, CA8 | GB5 | não (Codex) |
| GB7 | URLs e guardas de link: `DocUrls.Base` e as constantes no formato de URL do GitBook, o `documentationUrl` do `package.json` e o `ManualLinksTests` com a âncora do título. Inclui a guarda nova do sumário, dos links relativos, do link absoluto para o próprio site, das âncoras e da sintaxe do MkDocs. ~350 linhas, ~5 arquivos | CA3, CA5 | GB6 | sim |
| GB8 | Processo sem MkDocs: `AGENTS.md`, `guia-do-manual.md` (partes 4, 6, 7, 8 e 9), `Documentation~/README.md`, o `README.md` da raiz (link do manual), as skills `/implementar` e `/revisao` e os agentes `controlador` e `examinador`. Tira `mkdocs` e `docfx` e põe a geração e as guardas. 8 arquivos | CA9 | GB7, GB11 | não (Codex) |
| GB11 | Emendas das ADRs (Desenhista, só texto): emenda curta em cada ADR afetada, apontando para a 0044. **0042:** "um ticket, um commit" continua, salvo no passe de documentação, em que o ticket tem o commit do change request (e o de cada correção) e o do `/commit`, e o teto soma os dois; "a entrada do `nav`" vira "a entrada do `SUMMARY.md`"; a saída de gerador com guarda passa a ser a página do catálogo e os blocos C# expandidos. **0002:** a Fábrica do passe cria o commit do change request no rascunho; o `/commit` continua o único passo que commita no checkout, e o merge na `main` continua do usuário. **0007, 0040 e 0041:** as mudanças listadas em "Consequências" da 0044. **0025:** status "Substituída por 0044". Mais a linha de cada uma no `adr/README.md`. 7 arquivos | CA9 | — | não (Desenhista) |
| GB10 | Fluxo do MCP: a lista `tools` do agente `fabrica` com as ferramentas do item 13 da 0044, pelo nome que o GB1 levantou; o passo do script da fila que traz o commit do change request ao checkout do ticket, só por avanço rápido (skill `/commit` ou a que o Detalhe indicar); e o fluxo do change request no `AGENTS.md`, no guia do manual, no `/implementar` e nos agentes `fabrica` e `controlador`. ~6 arquivos | CA9, CA10 | GB8, GB1 | não (Codex) |
| GB9 | Ligar (passo do usuário): depois do merge do lote, ligar o espaço público à `main` e travar o conteúdo dele, se der; criar o espaço de rascunho, privado, e ligá-lo à branch do próximo lote com passe; desligar o GitHub Pages e percorrer o cenário. Sem diff | CA1, CA2, CA3, CA6, CA10 | GB10 e merge do lote | não |

O GB11 entra no começo do lote, antes do GB8 e do GB10, porque o `AGENTS.md` segue as ADRs ("se divergir, vale a ADR"). As emendas descrevem o fluxo que passa a valer depois do GB9.

## Perguntas ao usuário
Nenhuma em aberto. Q1 a Q6 respondidas no gate de 2026-11-04 (ver "Decisões do épico").

## Resultado do spike
<Preenchido depois do GB1, um ponto do "Em aberto" da 0044 por linha.>

## Validação de uso
| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
