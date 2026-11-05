# 0007 — Documentação como código e gate de doc no `/revisao`

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

O SpellBook entrega sistemas e ferramentas para dev e designer. Sem página, um componente não é usado direito. Hoje o `package.json` tem `documentationUrl` igual a `https://example.com/` e nenhum componente tem `[HelpURL]`. O plano (seção H.7 e H.8) prevê validadores de asset, `[HelpURL]` e um catálogo de ações e condições gerado a partir dos mesmos metadados do Inspector. A decisão I.15 do usuário: o `/revisao` **reprova** ticket sem documentação.

## Decisão

1. O manual público é código: vive em `Documentation~/manual/`, em Markdown, e muda **no mesmo ticket e PR** que muda o comportamento.
2. **Gate no `/revisao`.** O Examinador registra achado (e portanto reprova) quando o ticket:
   - adiciona ou muda um componente (`MonoBehaviour`) ou `ScriptableObject` que o usuário do pacote cria ou configura, sem página nova ou atualizada;
   - adiciona ou muda uma ação, condição, reação ou outro tipo escolhido por `[SerializeReference]`, sem página **ou** sem entrada no catálogo gerado;
   - muda comportamento público sem atualizar a página correspondente.
3. **`[HelpURL]`.** Todo componente e `ScriptableObject` público do pacote tem `[HelpURL]` apontando para a página dele. A URL base é uma constante única no `SpellBook.Core`.
4. **Validadores.** Validadores de asset (0011: `ISelfValidator`), o menu "SpellBook/Validar projeto" e um teste EditMode (0004) que roda todos os validadores e cobra o `[HelpURL]` por reflexão.
5. **Catálogo.** O seletor do Inspector e o catálogo da doc leem os mesmos metadados (`[ActionCategory]`, resumo, XML docs), para nunca divergirem.
6. Quebras e migração: o ticket que quebra algo registra a migração na seção "Quebras de compatibilidade" da Spec; o DevOps a leva ao CHANGELOG (0008).
7. Os documentos internos (`prd/`, `spec/`, `adr/`, `planos/`, `backlog.md`, `TIME.md`) não são publicados.

## Motivo

- Documentação escrita depois não é escrita.
- O `[HelpURL]` leva o usuário do Inspector direto à página.
- Um teste automático cobra a regra sem depender da memória do revisor.

## Consequências

- Tickets ficam um pouco maiores (ver a pergunta em aberto da 0003 sobre a contagem de linhas).
- O gerador do catálogo precisa existir antes que a opção "entrada no catálogo" seja usada; até lá, ações e condições exigem página.
- O primeiro épico cria o esqueleto do manual, a constante da URL e o teste de `[HelpURL]`.

## O que os agentes NÃO podem fazer

- Aprovar ticket sem a página ou a entrada de catálogo exigida.
- Criar componente ou `ScriptableObject` público sem `[HelpURL]`.
- Escrever a URL da doc fora da constante única.
- Publicar documentos internos no site.
- Desligar ou pular o teste de validadores para o ticket passar.

## Decisões incorporadas (2026-10-29)

- **Conteúdo real desde a 0-M, sem stubs** (decisão I26). Uma página publicada nunca é só título ou "em breve"; a árvore do H.8 cresce fase a fase, e a navegação do site lista só páginas que existem. Um teste EditMode cobra que todo `[HelpURL]` aponte para uma página (e âncora) existente em `Documentation~/manual/` (CA54).
- **URL base:** `https://gabrielctorres.github.io/SpellBook/` (decisão I29, site público), na constante `SpellBook.Core.DocUrls.Base`. O `[HelpURL]` usa constantes derivadas dela (`DocUrls.Base + "modulos/eventos/#eventlistener"`), que o C# aceita em atributo por serem `const`. O `SpellBook.Setup.Editor`, que não pode referenciar o Core (0011), lê a URL do `documentationUrl` do `package.json` em vez de repetir a constante.
- **Escopo do `[HelpURL]`** (decisão técnica): todo tipo **público, concreto (não abstrato)** que deriva de `MonoBehaviour` ou `ScriptableObject` nos assemblies **de runtime** do SpellBook. Janelas e tipos de Editor ficam fora, porque o usuário não os adiciona a objetos.
- **Gerador do catálogo** (decisão técnica): fica para a Fase 9 (B-019). Até lá, ações, reações e condições escolhidas por `[SerializeReference]` exigem seção numa página do manual (por exemplo, as reações prontas da 0-M ficam na página Eventos, com uma âncora por reação).

## Emenda (2026-10-29): sem Odin, pela 0026

Esta emenda vale quando a 0026 for aceita.

- **Item 4 (Validadores):** os validadores implementam `SpellBook.Core.Validation.IValidatable` (0026, item 6), não o `ISelfValidator` do Odin. O menu e o teste EditMode continuam como estão.
- **Item 5 (Catálogo):** o metadado de categoria e resumo é o `[TypeCategory("Caminho", Summary = "...")]` do Core (0026, item 4), que vale para ações, reações e condições. O nome `[ActionCategory]` não é criado.
- **URL base no Setup:** o `SpellBook.Setup.Editor` não existe mais (0026). Por isso deixa de valer o trecho que diz que ele lê o `documentationUrl` do `package.json`. A constante `DocUrls.Base` continua sendo a única fonte da URL dentro do código.

## Emenda (2026-11-01): molde de página e regras de escrita no gate

**Status da emenda: Aceita** (gate do B-035, 2026-11-01; Q1 a Q6 da Spec aprovadas como propostas). PRD: `prd/manual-reorganizacao.md`, com a P2 decidida: o molde e as regras de escrita entram no gate. Spec: `spec/manual-reorganizacao.md` (D1 a D6).

Vale para todo ticket que começar depois da aceitação. Um ticket que já está em implementação ou em revisão termina pela regra anterior.

1. **Guia do manual.** O molde de página, as regras de escrita e o mapa de páginas ficam em `Documentation~/guia-do-manual.md`. É um documento interno e entra na lista do item 7 da Decisão: não é publicado.
2. **Gate (item 2 da Decisão).** Além dos casos do item 2, o Examinador registra achado quando o ticket:
   - cria página fora do molde do guia: falta uma seção obrigatória ou a ordem das seções muda;
   - põe sigla num título;
   - usa uma sigla antes do nome por extenso na mesma página. "GAS" é a exceção: pode aparecer depois de "sistema de habilidades" por extenso e no nome das ferramentas que o usam. Nome de tipo em código não é sigla;
   - põe imagem, captura de tela ou gif em `manual/`;
   - põe diagrama que não é Mermaid, ou diagrama sem legenda de uma frase;
   - cria página sem a entrada dela no `nav` do `mkdocs.yml`;
   - escreve à mão uma lista de ações, condições ou reações depois que o catálogo gerado (B-019g) existir;
   - não declara, no relatório da Fábrica, que o texto novo do manual foi escrito e revisado pelas regras da humanizer. Os papéis não têm a ferramenta Skill: a regra é ler `C:/Users/gabri/.claude/skills/humanizer/SKILL.md` e aplicá-lo (Q4 do gate). O Examinador confere a declaração e as regras objetivas do guia, sem reaplicar a humanizer.
3. **Páginas antigas.** Até a reescrita do módulo (etapa 2 da Spec), o ticket que muda uma página antiga:
   - segue as regras de escrita no trecho que escreve;
   - põe o componente ou asset novo num bloco "Campos do Inspector";
   - não reestrutura o resto da página.
4. **Navegação (forma do I26).**
   - A navegação é o `nav` explícito do `mkdocs.yml`, nos sete grupos do guia.
   - O build `--strict` falha:
     - com página fora do `nav`;
     - com entrada do `nav` que aponta para página que não existe;
     - com link para âncora que não existe.
   - Grupo sem página não aparece.
   - O resto do I26 continua: página publicada nunca é só título ou "em breve".
5. **Páginas geradas no build.**
   - A página gerada (o catálogo do B-019g) entra no `nav` no mesmo ticket que cria o gerador. Esse ticket também:
     - põe a geração antes do build, na Action de documentação;
     - documenta o comando do build local.
   - Nenhum `[HelpURL]` aponta para página gerada.
   - A referência da API fica fora da pasta do manual e entra no `nav` como link absoluto.
6. **Âncoras (item 3 da Decisão e CA54).**
   - Todo título tem âncora `{#id}`.
   - A seção de um componente ou asset usa o nome do tipo em minúsculas como âncora, e uma reescrita não muda essa âncora.
   - Mover uma seção para outra página exige, no mesmo ticket:
     - atualizar `DocUrls`;
     - atualizar os links das outras páginas.
   - Não há redirecionamento nem página de remendo no endereço antigo.
7. **Constante por tipo** (Q3 do gate da Spec, aprovada).
   - O `[HelpURL]` usa uma constante de `DocUrls` que já tem página e âncora (`[HelpURL(DocUrls.AttributeDefinition)]`) e não concatena âncora no atributo. Assim, mover uma seção muda só o `DocUrls.cs`, e não cada tipo.
   - Código novo já nasce assim.
   - Os `[HelpURL]` que existem migram por módulo, antes da divisão da página daquele módulo.
   - "Escrever a URL da doc fora da constante única", na lista do que os agentes não podem fazer, passa a valer também para a âncora.

**Motivo.** As referências estudadas (TopDown Engine e Edgar) mostram que um molde fixo e uma navegação na ordem de aprendizado fazem o leitor achar o campo e o próximo passo. Faltam 122 tickets, e cada um escreve página pelo gate desta ADR. Pôr o molde no gate agora evita que essas páginas nasçam no formato antigo e sejam refeitas depois.

**Consequências.**
- Todo ticket que cria página toca também o `mkdocs.yml`. Pela Q2, essa entrada conta junto com a página (emenda de 2026-11-01 da 0003, aceita).
- O Examinador ganha conferências por busca, listadas no guia. A conferência da humanizer depende da declaração da Fábrica.
- Os diagramas em Mermaid são desenhados no navegador, com o script que o tema do site carrega. O build confere o Markdown, e o desenho é conferido abrindo o site gerado.

## Emenda (2026-11-03): manual e referência só no passe de documentação

**Status da emenda: Aceita** (decisão do usuário, aprovada pelo usuário em 2026-11-03). Vale para todo ticket que entrar na fila depois do EV10. O EV10, que já está na Fábrica, segue a regra provisória do fim desta emenda.

**Contexto.** Escrever a página e regenerar a referência em todo ticket deixa cada ticket mais lento e mais longo, e foi parte das recontagens dos lotes 7 a 9. O usuário quer acelerar o desenvolvimento e escrever a documentação de uma vez, no fim do projeto, quando ele pedir.

**Regra.**
1. **Item 1 da Decisão.** O manual deixa de mudar no mesmo ticket. Ele muda no **passe de documentação**, que roda quando o usuário pedir. Nenhum ticket de código escreve em `Documentation~/manual/`.
2. **Item 2 da Decisão (gate no `/revisao`).** O Examinador não registra achado por página nova ou atualizada que falte, nem por entrada do catálogo gerado desatualizada. As regras da emenda de 2026-11-01 (molde, siglas, `nav`, humanizer) valem só no passe de documentação.
3. **O que continua em todo ticket**, porque fica no código e é barato:
   - `[HelpURL]` em todo componente e `ScriptableObject` público (item 3), com uma constante de `DocUrls` por tipo (emenda de 2026-11-01, item 7). A constante já nasce com a página e a âncora que o passe vai escrever;
   - `[TypeCategory]` com `Summary` em todo tipo do catálogo (guarda `EveryCatalogTypeHasSummary`);
   - os validadores e o teste deles (item 4);
   - os comentários XML da API pública.
4. **CHANGELOG (item 6).** Em cada ticket, o `CHANGELOG.md` só recebe a entrada de quebra (commit com `!`), com o passo de migração, na seção Quebras e Migração (0008, item 3). As entradas de Adicionado e Corrigido saem dos Conventional Commits no passe de documentação ou no release. Motivo: a quebra precisa do passo de migração, e só quem fez a quebra sabe escrevê-lo. O resto o histórico já guarda.
5. **Guardas de documentação.** As guardas que comparam o código com o manual ou com a referência commitada ficam na categoria de teste `Documentacao` e não rodam na suíte dos tickets (ticket PD1 da Spec `processo-doc-no-fim.md`). Elas não são apagadas nem afrouxadas. Rodam no passe de documentação, e o passe só termina com elas verdes.
6. **Passe de documentação.** Quando o usuário pedir, o Arquiteto detalha o passe em tickets (emenda de 2026-11-03 da 0002). O passe:
   - escreve e atualiza as páginas pelo guia do manual, a partir dos tickets feitos depois desta emenda (escopo e CA na Spec e commits);
   - roda **SpellBook > Documentação > Gerar referência** e commita a saída (0040);
   - registra Adicionado e Corrigido no CHANGELOG;
   - roda a suíte com `-testCategory "Documentacao"` e o `mkdocs build --strict`, os dois verdes.

**Regra provisória até o PD1 entrar.** O ticket não escreve no manual. Ele roda a geração da referência só se uma guarda de `ReferenceUpToDateTests` reprovar na suíte, e commita a saída como antes. Isso acontece em todo ticket que muda `.cs`. Se reprovar uma guarda de `ManualLinksTests` ou de `ManualCodeExamplesTests`, a Fábrica para e devolve ao Controlador. Ela não escreve página só para a guarda passar.

**Consequências.**
- O site publicado fica atrás do código até o passe. O manual e a referência commitados também.
- "Documentação escrita depois não é escrita" (Motivo): o risco é aceito pelo usuário. Ele é reduzido porque o escopo e os CA de cada ticket ficam na Spec, e as quebras ficam no CHANGELOG.

## Emenda (2026-11-03): quem revisa o passe de documentação

**Status da emenda: Aceita** (decisão do usuário, aprovada pelo usuário em 2026-11-03, issue #325). Revê o item 2 da emenda de 2026-11-01 e os itens 5 e 6 da emenda "manual e referência só no passe de documentação", acima. Responde ao "Em aberto" da 0002.

**Contexto.** Pela emenda de 2026-11-03 da 0002 ("gate do Examinador só para código"), o Examinador revisa e reprova só pelo código, e o texto de um ticket não gera achado. As páginas do manual são texto. Assim, as conferências da emenda de 2026-11-01 (molde, siglas, `nav`, humanizer, imagens e legenda dos diagramas) ficaram sem quem as fizesse. As guardas da categoria `Documentacao` também ficaram sem dono, e elas precisam da Unity.

**Regra.**
1. **Texto do passe: o Controlador** (item 2 da emenda de 2026-11-01). As conferências que aquele item dava ao Examinador passam ao Controlador, no ticket do passe. Antes do `/commit`, o Controlador:
   - roda `git diff --check` no diff do ticket;
   - roda as conferências por busca da seção 7 do `guia-do-manual.md`;
   - confere o molde, as siglas, o `nav` e as legendas dos diagramas pelas regras do guia;
   - lê o texto novo do manual pelas regras da humanizer (`C:/Users/gabri/.claude/skills/humanizer/SKILL.md`) e confere a declaração da Fábrica no relatório;
   - roda o `mkdocs build --strict` como manda a seção 7 do guia.

   Um problema encontrado volta à Fábrica, e o ticket não segue para o `/commit` até o Controlador repetir a conferência. O usuário lê o texto no PR da branch do lote.
2. **Guardas de documentação: o Examinador** (itens 5 e 6 da emenda "manual e referência só no passe de documentação"). Todo ticket do passe passa pelo `/revisao`, mesmo que seja só de texto. Nele, o Examinador pega a trava de Editor (0005) e roda a suíte com `-testCategory "Documentacao"`.
   - Se uma guarda falhar, ele reprova pela guarda.
   - Ele não registra achado sobre o texto: molde, escrita e humanizer ficam com o Controlador (item 1).
   - Se o ticket do passe tem código (por exemplo, `.cs` dos exemplos incluídos de testes), o Examinador também o revisa pela regra de código da 0002, e roda a suíte completa como em qualquer ticket com código.
   - Se o texto muda depois que as guardas passaram, elas rodam de novo antes do `/commit`.
3. **Ordem e fim do passe.** O ticket do passe só vai ao `/commit` com as duas partes aprovadas para o diff final: a conferência do Controlador (item 1) e o APROVADO do Examinador (item 2). O passe termina quando o último ticket fecha com as guardas verdes e o `mkdocs build --strict` sem erro.
4. **Detalhe do passe** (item 6 da emenda "manual e referência só no passe de documentação"). Onde lá diz "o Arquiteto detalha o passe em tickets", passa a valer o fluxo da 0002: a Fábrica escreve o Detalhe de cada ticket do passe na entrada da fila e o implementa.

**Motivo.**
- O texto fica com dois olhares, como no resto do processo: o Controlador antes do commit e o usuário no PR.
- As guardas precisam da Unity e da trava de Editor, e o Examinador já roda a suíte. Ele fica com o que é verificado por teste, sem voltar a revisar texto.

**Consequências.**
- O ticket do passe só de texto é uma exceção ao item 3 da emenda "gate do Examinador só para código" da 0002: ele passa pelo `/revisao`, mas só para as guardas.
- O Controlador lê páginas do manual. Elas são texto, e os exemplos de código nelas são incluídos de testes e conferidos pelas guardas, então ele continua sem ler código.
- Com esta emenda aceita, um ticket da via rápida alinha ao texto dela: o `AGENTS.md` (seção "Time de desenvolvimento": os papéis do Controlador e do Examinador, o item 5 do Fluxo e "WIP, trava e testes", que hoje tira `Documentacao` da revisão), a skill `/revisao`, o agente `examinador` e o agente `controlador`. A seção 7 do `guia-do-manual.md` já fala de "quem revisa" e não muda.

## Emenda (2026-11-04): manual no GitBook, pela 0044

**Status da emenda: Aceita** (gate de 2026-11-04 da [0044](0044-manual-no-gitbook-por-git-sync-sem-referencia-da-api.md), Q1 a Q6; ticket GB11 de `spec/migracao-gitbook.md`). Vale depois do GB9 do mesmo design doc. Até lá, as emendas acima continuam como estão.

1. **Emenda de 2026-11-01, item 2.** "Cria página sem a entrada dela no `nav` do `mkdocs.yml`" passa a ler "sem a entrada dela no `SUMMARY.md`".
2. **Emenda de 2026-11-01, item 4.** A navegação é o `SUMMARY.md` na raiz do manual, nos sete grupos do guia, com cada página uma vez (0044, item 5). No lugar do build `--strict`, falham as guardas `Documentacao` do item 10 da 0044: página fora do sumário ou repetida nele, entrada sem arquivo, link para arquivo ou âncora que não existe, link absoluto para o próprio site e sobra de sintaxe do MkDocs.
3. **Emenda de 2026-11-01, item 5.** Perde a referência da API, que sai do repositório (0044, item 6). Fica a página gerada do catálogo, no `SUMMARY.md`, sem `[HelpURL]` apontando para ela.
4. **Emenda de 2026-11-01, item 6.** Os títulos não têm `{#id}`. A âncora é a que o GitBook gera a partir do título, e a seção de um componente ou asset tem como título o nome do tipo (0044, item 9). O resto do item continua.
5. **Emenda de 2026-11-01, Consequências.** O Mermaid é desenhado pelo GitBook, e o desenho é conferido na prévia do change request.
6. **Emenda "manual e referência só no passe de documentação", item 6.** O texto do passe é escrito no espaço de rascunho pelo MCP, por change request (0044, item 13). A geração passa a ser **SpellBook > Documentação > Gerar catálogo e exemplos**, rodada no checkout depois do merge do change request. No lugar do `mkdocs build --strict`, valem as guardas `Documentacao`.
7. **Emenda "quem revisa o passe de documentação".** O Controlador faz as conferências de texto (molde, siglas, sumário, legendas e humanizer) lendo o change request pelo MCP, antes do merge dele. Na branch do lote, antes do `/commit`, faz o `git diff --check` e as conferências por busca. Ele não roda mais build. O item 3 termina com as guardas verdes, sem o build. O Examinador roda as guardas depois do merge do change request e da geração; o resto do item 2 não muda.

## Emenda (2026-11-05): quem revisa o passe de documentação, em uma lista

**Status da emenda: Aceita, aprovada pelo usuário em 2026-11-05** (issue #325). Junta numa lista a emenda "quem revisa o passe de documentação" (2026-11-03), o item 7 da emenda de 2026-11-04 e o item 13 da [0044](0044-manual-no-gitbook-por-git-sync-sem-referencia-da-api.md). Não muda nenhuma regra. Aceita esta emenda, a regra de quem revisa o passe se lê aqui, e as duas emendas anteriores ficam como histórico.

1. **Controlador, no change request, antes do merge.** Lê as páginas do change request pelo MCP, só para ler, e confere:
   - o molde do guia: nenhuma seção obrigatória faltando e a ordem das seções;
   - as siglas: nenhuma em título e nenhuma antes do nome por extenso na mesma página, com a exceção do GAS;
   - a entrada de cada página nova no `SUMMARY.md`;
   - os diagramas: só Mermaid, cada um com legenda de uma frase e desenhado na prévia do change request;
   - nenhuma imagem, captura de tela ou gif;
   - nenhuma lista de ações, condições ou reações escrita à mão;
   - o texto novo, lido pelas regras da humanizer (`C:/Users/gabri/.claude/skills/humanizer/SKILL.md`), e a declaração da Fábrica no relatório de que escreveu e revisou o texto por essas regras.

   Um problema volta à Fábrica no mesmo change request, e o Controlador repete a conferência. A Fábrica só junta o change request depois dela.
2. **Controlador, na branch do lote, antes do `/commit`.** Roda o `git diff --check` no diff do ticket e as conferências por busca da seção 7 do `guia-do-manual.md`. Não roda build, porque não há build (0044, item 12).
3. **Examinador, no `/revisao`.** Todo ticket do passe passa pelo `/revisao`, mesmo o que é só de texto, depois do merge do change request e da geração. Sob a trava (0005), o Examinador roda as guardas com `-testCategory "Documentacao"` e reprova se uma falhar.
   - Não registra achado sobre o texto. O texto é do item 1.
   - Se o ticket tem código, revisa o código pela regra da 0002 e roda também a suíte com `-testCategory "!Documentacao"`.
   - Se o texto muda depois das guardas verdes, elas rodam de novo antes do `/commit`. A correção de texto pedida por uma guarda é um change request novo do mesmo ticket, e passa pelo item 1.
4. **Fim.** O ticket só vai ao `/commit` com as duas partes aprovadas para o diff final: a conferência do Controlador (itens 1 e 2) e o APROVADO do Examinador (item 3). O passe termina quando o último ticket fecha com as guardas verdes. O usuário lê o texto no PR da branch do lote.

**Motivo.** A regra estava espalhada: a emenda de 2026-11-03 trazia o `nav` e o `mkdocs build --strict`, e só o item 7 da emenda de 2026-11-04 e a 0044 os tiravam. Quem lia a emenda de 2026-11-03 sozinha seguia uma regra que não vale mais.

**Consequências.** O `AGENTS.md` (seção "Time de desenvolvimento"), os agentes `controlador` e `examinador` e a skill `/revisao` já dizem o mesmo e não mudam.

## Em aberto

Nada.

## Relacionadas

- 0001 (pastas), 0002 (fluxo e papéis), 0003 (tamanho de ticket), 0005 (trava), 0004 (teste de validadores), 0011 (`ISelfValidator`), 0013 (`[SerializeReference]`), 0025 (ferramenta do site, substituída pela 0044), 0044 (manual no GitBook).
