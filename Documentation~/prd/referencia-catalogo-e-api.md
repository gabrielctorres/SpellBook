# PRD: Catálogo gerado e referência da API em DocFX (B-019g)
Backlog: B-019g (parte do B-019) · Issues: (criadas depois do gate) · Status: Aprovado (gate de 2026-11-01); emenda de 2026-11-01 (extras) aprovada · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), H.8 e Fase 9. ADRs [0007](../adr/0007-documentacao-como-codigo-e-gate-de-doc.md) (item 5: catálogo gerado na Fase 9), [0025](../adr/0025-documentacao-em-mkdocs-ou-zensical-no-github-pages.md) (site e DocFX na Fase 9) e [0026](../adr/0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md) (item 4).
Depende de: o `[TypeCategory]` existente e da Action de documentação. Não espera o Lote B a D. Este PRD não reescreve o escopo de nenhum outro item.
Sem foco em nenhum jogo.

## 1. Problema

O manual explica o uso, mas falta a lista completa dos tipos públicos e o catálogo de ações, condições e reações escolhidas por `[SerializeReference]`. A ADR 0007 deixou o gerador do catálogo para esta fase. Até lá, toda ação nova exige uma página escrita à mão.

## 2. Objetivo

Quem usa o pacote consulta, no mesmo site do manual, (1) o catálogo de ações, condições e reações, gerado do código, e (2) a referência da API pública de todos os módulos, em DocFX.

Princípio (modular, sem remendos): o catálogo e o seletor do Inspector leem o mesmo metadado (`[TypeCategory]`). O DocFX é ferramenta só da documentação, no mesmo site do manual (decisão do usuário, P3).

Geração local (gate de 2026-11-01, G1): o catálogo e a referência da API são gerados na máquina do mantenedor, com a Unity, e commitados. A Action de documentação só monta o site a partir do que está no repositório.

## 3. Usuários e cenários

| Usuário | O que espera |
|---|---|
| Quem adota o pacote | Achar um tipo ou uma ação na referência |
| Mantenedor | Adicionar uma ação sem escrever página à mão |

- C1, catálogo gerado: lista toda ação, condição e reação com `[TypeCategory]` (caminho, resumo, página), a partir do código. Um tipo novo entra sem editar página.
- C2, referência da API: o site tem a seção `api/` com os tipos públicos de todos os módulos e os comentários XML.
- C3, sem divergência: catálogo e seletor do Inspector listam os mesmos tipos (teste compara as duas listas).
- C4, build estrito: link quebrado quebra o build. Tipo público sem comentário só avisa, exceto os tipos do catálogo, que exigem resumo (R7).
- C5, revisão de PR: o PR que mexe na documentação mostra se o site monta, sem publicar nada.

## 4. Requisitos funcionais

R1. Catálogo gerado. O gerador roda localmente, com a Unity, lê `[TypeCategory]` e os comentários XML e escreve a página de catálogo (ações, condições, reações e demais tipos `[SerializeReference]`) em `Documentation~/manual/referencia/`. O arquivo gerado é commitado. Cumpre a "entrada no catálogo" prometida pela ADR 0007.
R2. Mesmo metadado do Inspector (C3).
R3. Referência da API em DocFX. API pública de todos os módulos, gerada localmente e commitada, sob uma seção do mesmo site. A Action monta o site com ela. DocFX com versão travada, fora do pacote e do projeto Unity.
R4. O `--strict` do site continua (C4).
R5. O gate de doc continua (ADR 0007): o teste de `[HelpURL]` e página existente passa. O catálogo não o substitui para tipos que exigem página própria.
R6. Sem edição manual do arquivo gerado. O cabeçalho avisa e a mudança é feita na fonte.
R7. Resumo obrigatório (G2). Todo tipo que entra no catálogo tem resumo (`<summary>`). Tipo sem resumo reprova o gerador ou o teste.
R8. Build de documentação em PR (G3). A Action monta o site (com `--strict`) também em pull request, sem publicar. A publicação continua restrita ao fluxo atual de release/`main`.

## 5. Requisitos não funcionais

N1. A Action de doc só monta o site (ADR 0025). Nenhuma dependência nova no pacote nem no projeto Unity. O gerador e o DocFX de metadados rodam localmente.
N2. Saída determinística: mesma fonte, mesma página.
N3. Pacote em UPM continua sem dependência de terceiros.
N4. A saída gerada e commitada (catálogo e referência da API) não conta no teto de linhas do ticket (G4). O código do gerador e os testes contam normalmente.

## 6. Fora do escopo

- Manual de cada ferramenta (cada PRD B-019a a e cuida da sua página).
- Traduzir, guias por gênero, FAQ, migração 2.x para 3.0.
- Trocar a ferramenta do site (MkDocs/Zensical, ADR 0025).
- Versionar a referência por release.
- Rodar a Unity na Action (decidido em G1).

## 7. Critérios de aceite

- CA1 (T). C1, C3: o gerador lista todo tipo com `[TypeCategory]`. O teste compara com a lista do seletor e as duas são iguais.
- CA2 (T). N2: mesma fonte gera a mesma página.
- CA3 (E). C2, R3: a Action monta a API junto com o manual a partir do commitado, com versão travada do DocFX.
- CA4 (E). C4, R4: o build `--strict` passa, e um link quebrado o derruba.
- CA5 (E). R5: o teste de `[HelpURL]` passa. O CHANGELOG cita o catálogo e a API.
- CA6 (T). R7: um tipo do catálogo sem resumo faz o gerador ou o teste falhar, e a mensagem cita o tipo.
- CA7 (E). R8, C5: um PR que altera `Documentation~` roda o build do site e não publica.
- CA8 (E). R1, R3: a saída gerada está commitada, e a Action não invoca a Unity.

## 8. Perguntas em aberto

### Com o usuário
- P1. Catálogo commitado ou só no build. RESPONDIDA (2026-11-01, G1): o catálogo e a referência da API são gerados localmente, com a Unity, e commitados. A decisão de 2026-10-31 (catálogo só no build, página fora do repositório) foi substituída.

### Com o Arquiteto
- P1. Como o gerador roda localmente (Unity em batchmode, menu do Editor ou teste) e como o DocFX de metadados consome a saída. A parte "na Action em batchmode" da versão anterior caiu com G1.
- P2. Integração do DocFX com o `--strict` e com a navegação do site.
- P3. Como o seletor do Inspector e o gerador compartilham a leitura do `[TypeCategory]`.
- P4. Como evitar que a saída commitada fique velha, já que a Action não tem a Unity. Opções a avaliar: teste local, hook de pré-commit, checagem na revisão.

## 9. Ajustes de playtest

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | Bloco G do B-019, PRD próprio (P1 do usuário) | Todo o documento | Rascunho |
| 2026-10-31 | usuário (via Controlador) P3: DocFX só como ferramenta de docs, mesma Action e mesmo site. P6: catálogo gerado incluído | R1, R3, N1 | Aplicado |
| 2026-11-01 | Gate aprovado. G1: catálogo e API gerados localmente com a Unity e commitados, Action só monta o site | P1, R1, R3, N1, CA3, CA8 | Aplicado |
| 2026-11-01 | G2: resumo obrigatório em todo tipo do catálogo | R7, CA6 | Aplicado |
| 2026-11-01 | G3: documentação também montada em PR, sem publicar | R8, CA7 | Aplicado |
| 2026-11-01 | G4: saída gerada commitada fora do teto do ticket | N4 | Aplicado |
| 2026-11-01 | Extras do usuário: E1 (links para a documentação da Unity) e E2 (exemplos de código reais) | Seção 10 | Aplicado |
| 2026-11-01 | Gate dos extras aprovado. X1: links para a documentação mais recente da Unity. X2: sem link para o .NET. X3: mapa gerado por nós. X4: exemplos só de testes EditMode. X5: pymdownx.snippets com versão travada | R9, R10, CA9, CA10, Q1 | Aplicado |
| 2026-11-02 | Usuário, revendo o X2: na API, os tipos `System.*` ganham link para learn.microsoft.com, gravado pelo DocFX 2.81 sem download (build sem rede). O texto do manual segue sem link para o .NET escrito à mão | R9, CA9, Q1 | Aplicado |

## 10. Emenda de 2026-11-01 (aprovada)

Dois pedidos do usuário entraram como requisitos novos, aprovados no gate dos extras de 2026-11-01. Issues: XR1, XR2 e EX1 a EX5 (#294 a #300).

- R9 (E1). Links para a documentação da Unity. Na referência da API, os tipos da Unity que aparecem nas assinaturas viram links para a documentação mais recente da Unity (X1). O mapa de referências cruzadas é gerado por nós (X3), não escrito à mão. Os tipos do .NET (`System.*`) ganham link para learn.microsoft.com, gravado pelo próprio DocFX 2.81 sem download, então o build segue sem depender de rede (X2, emenda de 2026-11-02). O texto do manual fora da API continua sem link para o .NET escrito à mão.
- R10 (E2). Exemplos de código reais. Os exemplos de código do manual vêm só de testes EditMode (X4), para que compilem junto com o projeto e não envelheçam no texto. A página do manual inclui o trecho a partir do arquivo-fonte com `pymdownx.snippets`, com a versão travada (X5).

Critérios de aceite da emenda:

- CA9 (E). R9: um tipo da Unity numa assinatura da API (por exemplo `GameObject`) aparece como link para a página correspondente da documentação mais recente da Unity, um tipo `System.*` (por exemplo `string`) aparece como link para learn.microsoft.com, gravado pelo DocFX sem download e sem rede no build, e o `--strict` valida o mapa gerado. Nenhum link para o .NET é escrito à mão no texto do manual.
- CA10 (E). R10: todo exemplo de código do manual é incluído de um arquivo de testes EditMode. Quebrar a compilação desse arquivo quebra o exemplo, e nenhum bloco de código C# do manual fica escrito à mão.

Perguntas do gate dos extras:

- Q1 (E1). Fechada em 2026-11-01: links para a documentação mais recente da Unity (X1), mapa gerado por nós (X3). O X2 (.NET) foi revisto em 2026-11-02: os tipos `System.*` da API ganham link para learn.microsoft.com, gravado pelo DocFX 2.81 sem download; o texto do manual segue sem link escrito à mão.
- Q2 (E2). Respondida em parte por X4: os exemplos de código vêm só de testes EditMode. Em aberto: exemplos de configuração (assets e Inspector), que seguem como texto e imagem.
