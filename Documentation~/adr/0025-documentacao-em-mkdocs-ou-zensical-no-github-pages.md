# 0025 — Documentação em MkDocs Material ou Zensical, publicada no GitHub Pages

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

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

## Em aberto

- PRD P21: a licença do Odin permite baixá-lo de um local privado no CI de um repositório público? Se não, qual o plano B (por exemplo, runner self-hosted com o Odin instalado)?

## Relacionadas

- 0001 (pastas), 0007 (gate de doc e `[HelpURL]`), 0008 (release).
