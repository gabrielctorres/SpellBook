# 0025 — Documentação em MkDocs Material ou Zensical, publicada no GitHub Pages

Status: Substituída por [0044](0044-manual-no-gitbook-por-git-sync-sem-referencia-da-api.md) (gate de 2026-11-04)
Data: 2026-10-29

> **Histórico.** Desde 2026-11-04, a regra que vale é a da 0044: manual no GitBook, por Git Sync, sem referência da API gerada. O item 1 (manual em Markdown, numa pasta própria) e a proibição de publicar documento interno continuam na 0044, e a proibição de segunda verdade continua no item 2 dela. Este texto fica só como registro.

## Contexto

O manual do pacote é exigido pelo gate de doc (0007). O `package.json` aponta `documentationUrl` para `https://example.com/`. O estilo pretendido é um manual navegável por módulo, com receitas, referência de componentes e catálogo de ações e condições.

As opções avaliadas no plano (H.8): MkDocs com o tema Material, docs-as-code no próprio repositório; Zensical, sucessor anunciado pelo mantenedor do Material e compatível com o `mkdocs.yml`; GitBook, descartado por hospedar fora do repositório e criar uma segunda verdade. A decisão I.14 do usuário: MkDocs Material ou Zensical no GitHub Pages, com Python só como ferramenta da doc.

## Decisão

1. O conteúdo do manual fica em `Documentation~/manual/`, em Markdown. A configuração do site aponta só para essa pasta.
2. **Ferramenta:** Zensical, se na data do ticket ele já ler a estrutura do `mkdocs.yml` de forma estável; senão, MkDocs com o tema Material em versão travada. A verificação e a escolha ficam registradas na Spec do ticket que cria o esqueleto.
3. Python é dependência **só da ferramenta de documentação**, com versões travadas num arquivo de requisitos próprio. Não entra no pacote nem no projeto Unity.
4. Uma GitHub Action gera o site e publica no GitHub Pages a cada push na `main`.
5. `package.json.documentationUrl` e todo `[HelpURL]` (0007) apontam para o site, pela constante única do Core.
6. A referência de API gerada (DocFX) fica para a Fase 9.
7. A árvore inicial de páginas segue o plano (H.8): Início, Módulos, Ferramentas, Guias, Referência, Regras. Mudanças de estrutura são do Writer/Arquiteto.

## Motivo

- Docs-as-code: a doc muda no mesmo PR que o código e é revisada junto.
- Markdown simples, sem formato proprietário, e um site estático gratuito.

## Consequências

- Uma ferramenta Python passa a existir no repositório (fora do pacote) e precisa ser mantida.
- O site só existe depois do primeiro ticket de esqueleto; até lá, a constante da URL é provisória.
- Se o Zensical mudar de compatibilidade, a troca de ferramenta exige emenda nesta ADR.

## O que os agentes NÃO podem fazer

- Publicar documentos internos (`prd/`, `spec/`, `adr/`, `planos/`, `backlog.md`, `TIME.md`).
- Instalar a ferramenta sem versão travada ou adicionar plugins sem aprovação.
- Escrever documentação fora do repositório (wiki, GitBook) como fonte de verdade.
- Fazer o pacote ou o projeto Unity depender de Python.

## Decisões incorporadas (2026-10-29)

- **Site público** em `https://gabrielctorres.github.io/SpellBook/` (decisão I29), subdomínio padrão do GitHub Pages, publicado a partir do repositório público (decisão I33). É o valor do `documentationUrl` e da constante `DocUrls.Base` (0007).
- **Configuração e requisitos em `Documentation~/`** (decisão técnica): `Documentation~/mkdocs.yml` (ou o arquivo equivalente do Zensical) com `docs_dir: manual`, e `Documentation~/requirements-docs.txt` com versões travadas (`==`). Motivo: a Unity ignora a pasta, nada disso entra no pacote importado, e `docs_dir: manual` garante por construção que só `manual/` vira site (R38; `prd/`, `spec/`, `adr/`, `planos/`, `backlog.md` e `TIME.md` ficam fora do `docs_dir`). `use_directory_urls: true`, para URLs no formato `.../modulos/eventos/#eventlistener`.
- **Ferramenta e Python aprovados** pela decisão I14 (Python só como ferramenta da doc). A escolha entre Zensical e MkDocs Material é feita e registrada no ticket 0M-08: Zensical se ler o `mkdocs.yml` com o tema e a navegação sem ajuste; senão, `mkdocs-material` com versão travada. Nenhum plugin além do tema na 0-M; plugin novo exige aprovação.
- **GitHub Action** (ticket 0M-09): roda num push na `main` que altere `Documentation~/manual/**`, `Documentation~/mkdocs.yml` ou `Documentation~/requirements-docs.txt`, e também por `workflow_dispatch`. Gera com `--strict` (link quebrado falha o build) e publica pelas actions oficiais do Pages (`actions/upload-pages-artifact` e `actions/deploy-pages`), com permissões mínimas (`pages: write`, `id-token: write`, `contents: read`).
- **Odin no CI** (decisão I33): o workflow de testes (não o de doc) baixa o Odin de um local privado por secret do repositório; a URL e a credencial ficam só em secrets (CA56). **Bloqueado pela licença do Odin** (PRD P21, com o usuário): o ticket de CI (0M-11) só entra em implementação depois da resposta. O workflow de doc não depende do Odin e não fica bloqueado.

## Emenda (2026-10-29): CI sem Odin, pela 0026

Esta emenda vale quando a 0026 for aceita.

- **"Odin no CI"** (Decisões incorporadas) deixa de valer. O workflow de testes não baixa o Odin e não usa nenhum secret do Odin. O ticket 0M-11 deixa de estar bloqueado.
- A PRD P21 perde o objeto. O item dela em "Em aberto" fica resolvido pela decisão I45.

## Emenda (2026-11-01): catálogo e API pela 0040

**Status da emenda: Aceita** (gate da Spec `referencia-catalogo-e-api.md`, B-019g, aprovada em 2026-11-01, junto com a 0040).

Revista em 2026-11-01 pela G1 do gate: a Unity não entra na Action.

- **Item 6 (DocFX na Fase 9):** a referência da API sai em DocFX, como a 0040 descreve, em `api/` no mesmo site. O catálogo e os YAML do DocFX são gerados na máquina, pela Unity, e commitados.
- **GitHub Action (Decisões incorporadas):** a Action continua sem Unity e sem secrets. O job `build` passa a rodar também o `docfx build --warningsAsErrors`, a partir dos YAML commitados, e a conferência dos links do catálogo para a API. Os gatilhos ganham `Documentation~/api/**` e `Documentation~/.config/**`. A Action roda também nos pull requests, sem publicar; o deploy continua só no push para a `main`, com as mesmas permissões.
- **Ferramentas:** o DocFX e o SDK do .NET entram como ferramentas só da documentação, com versão travada, como o Python do item 3. Não são plugins do MkDocs.

O resto desta ADR não muda.

## Emenda (2026-11-04): GitBook reavaliado (B-042)

**Status da emenda: Recusada** (decisão do usuário, 2026-11-04). A proposta desta emenda era manter o MkDocs. O usuário a recusou e decidiu migrar o manual para o GitBook, tirar a referência da API por inteiro (gate de 2026-11-04, Q5) e deixar os agentes editarem o manual pelo MCP do GitBook (Q3). A decisão nova está na [0044](0044-manual-no-gitbook-por-git-sync-sem-referencia-da-api.md) (Aceita no gate de 2026-11-04), que substitui esta ADR. O status desta ADR muda para "Substituída por 0044" no ticket GB11 de `spec/migracao-gitbook.md`, junto com a migração. O texto abaixo fica só como registro da avaliação e não vale como decisão.

O usuário pediu que o GitBook fosse reavaliado, com o [Gasify](https://feliperoddd.gitbook.io/gasify) como referência. O GitBook não era só um site hospedado: ele sincroniza com o repositório (Git Sync), tem API e CLI para integrações e oferece um servidor MCP. Esta emenda registra a reavaliação contra o que o manual usa hoje. As capacidades do GitBook vêm da documentação pública dele e não foram conferidas nesta sessão, sem acesso à web. Antes do gate, conferir a seção "Capacidades a conferir".

**O que hoje depende do MkDocs** (conferido no repositório em 2026-11-04):
- **Exemplos de código incluídos de testes.** O `pymdownx.snippets` faz isso, pela 0041, e a guarda `ManualCodeExamplesTests` confere. O GitBook não inclui trecho de arquivo do repositório no Markdown sincronizado, então os exemplos voltariam a ser escritos à mão, e a garantia da 0041 se perderia.
- **Referência da API no mesmo site** (`/api/`). O HTML do `docfx build` é gerado na Action (0040). O GitBook não hospeda HTML gerado, então a API ficaria num segundo site, no GitHub Pages.
- **Build estrito.** O `mkdocs build --strict` e a validação do `nav` nos dois sentidos fazem parte do passe de documentação (`AGENTS.md`, 0007). O GitBook não falha o build por link ou página fora do sumário, então essas conferências precisariam de uma ferramenta nova.
- **Âncoras dos `[HelpURL]`.** O `DocUrls` e as âncoras `{#tipo}` do `attr_list` seguem o formato do MkDocs. A troca pediria migrar todas as URLs e as âncoras.
- **Sintaxe.** O manual tem 99 blocos de admonition, Mermaid ou inclusão em 11 páginas, todos na sintaxe do MkDocs. O GitBook usa outra sintaxe (`{% hint %}` e blocos próprios).

**Capacidades a conferir no GitBook antes do gate (em aberto em 2026-11-04; a emenda só vai ao gate depois desta conferência na web):** se o Git Sync preserva a pasta `Documentation~/manual/` como única fonte (sem segunda verdade, o motivo da recusa original); se o servidor MCP faz edição e reestruturação ou só leitura do site publicado; qual plano permite repositório público, domínio próprio e mais de um editor; e se o build pode rodar numa Action com falha por link quebrado.

**Proposta (recusada pelo usuário em 2026-11-04):** manter o MkDocs Material, com a troca para o Zensical prevista no item 2. O que o usuário gostou no Gasify (visual, navegação e busca) pode ser trazido por recursos do tema Material, sem plugin novo, num ticket do passe de documentação. A troca para o GitBook só aconteceria se o usuário aceitar perder os exemplos incluídos (0041) e ter a API num segundo site, e pediria uma ADR que substitui esta, com as emendas da 0007, da 0040 e da 0041.

## Em aberto

- ~~PRD P21: a licença do Odin permite baixá-lo de um local privado no CI de um repositório público? Se não, qual o plano B (por exemplo, runner self-hosted com o Odin instalado)?~~ Resolvida pela decisão I45 (emenda acima, 0026).

## Relacionadas

- 0001 (pastas), 0007 (gate de doc e `[HelpURL]`), 0008 (release), 0044 (substitui esta ADR).
