# Spec: reorganizar e reescrever o manual (B-035)
PRD: Documentation~/prd/manual-reorganizacao.md · ADRs: 0003 (emenda de 2026-11-01, aceita), 0007 (emenda de 2026-11-01, aceita), 0025, 0027

Estado: aprovada no gate de 2026-11-01. As seis perguntas (Q1 a Q6) saíram como propostas; as respostas estão no fim desta Spec. O PRD foi aprovado no mesmo dia: P1 híbrida em três etapas, P2 molde e regras de escrita no gate da 0007, P3 sem imagens e só diagramas em Mermaid, P4 "Primeira habilidade" depois que o GAS estabilizar.

Esta Spec detalha a etapa 1 (molde, navegação, regras de escrita e a emenda da 0007). As etapas 2 e 3 ficam em esboço, por módulo e ligadas aos lotes. Cada ticket esboçado ganha detalhe numa emenda desta Spec, por gate parcial, antes de entrar na fila.

Caminhos de código seguem a regra de prefixo da 0027: `Runtime/...` e `Tests/...` querem dizer `Packages/com.spellbook/...`.

## Contexto do código

- `Documentation~/mkdocs.yml:1-20`. O site não tem `nav` (o comentário da linha 3 cita o I26), então a barra lateral segue as pastas em ordem alfabética: `exemplos`, `ferramentas`, `inicio`, `modulos`, `regras`. As extensões são `attr_list`, `admonition`, `toc` e `fenced_code`. Sem bloco `validation`, o build `--strict` aceita página fora da navegação e âncora quebrada entre páginas.
- `Documentation~/requirements-docs.txt:3-4` trava `mkdocs==1.6.1` e `mkdocs-material==9.8.0`. O `pymdown-extensions` chega como dependência do Material, sem versão travada.
- `.github/workflows/docs.yml:43-44`. A Action roda `mkdocs build --strict` só no push para a `main`. Dentro do ticket, a Fábrica e o Examinador rodam o build no venv local.
- `Runtime/Core/DocUrls.cs:7-24` tem uma constante por página (`Gas`, `Events`, `Tags`...). Os `[HelpURL]` concatenam a âncora no próprio atributo, como em `[HelpURL(DocUrls.Gas + "#attributedefinition")]` (`Runtime/GAS/Attributes/AttributeDefinition.cs:31`). São 40 tipos de runtime e 5 do Sample nesse formato.
- Dois testes conferem a URL literal: `Tests/GAS.Bridges/EditMode/GasChannelsTests.cs:72` (`DocUrls.Events + "#canais-do-gas"`) e `Tests/GAS.Bridges/EditMode/RaiseEventDrawerTests.cs:97` (`DocUrls.Gas + "#raiseeventaction"`).
- `Tests/Core/EditMode/ManualLinksTests.cs`:
  - `EveryHelpUrlHasPageAndAnchor` (`:29-44`) e `EveryDocUrlHasPage` (`:46-57`) cobram o CA54;
  - `BrokenReason` (`:92-114`) transforma `modulos/gas/#x` em `manual/modulos/gas.md` e procura `{#x}` no texto. Ele não acha uma página em `pasta/index.md`;
  - `NoStubPages` (`:59-79`) cobra o I26: nenhuma página só com título ou "em breve".
- `Documentation~/manual/` tem 14 páginas: `index.md`; `inicio/` (instalacao, conceitos, inspector); `modulos/` (eventos com 569 linhas, gas com 465, pooling, tags, tempo); `ferramentas/` (validador, gas-debugger, event-debugger); `exemplos/sandbox.md`; `regras/migracao-2-para-3.md`.
- Specs aprovadas que criam página e ainda não foram implementadas:
  - `modulos/dano-escudo-e-morte.md` (DM1, Lote 6);
  - `modulos/times.md` (TM1, Lote 9);
  - `modulos/cues.md` (CU2, Lote 10);
  - `modulos/thinkers-e-auras.md` (TK4, Lote 11);
  - `modulos/texto.md` (DE2, Lote 11) e `modulos/descricao.md` (DE8, Lote 12);
  - `ui/*.md` (UI2, UI7, UI10, SL2, SL3, SL10, TT1, BF1; Lotes 13 a 16).

  DM1, TM1, CU2, DE8, UI2 e TT1 já estão em 10 arquivos.
- Specs aprovadas que ainda escrevem em páginas existentes:
  - `gas.md`: lotes 5 a 12;
  - `eventos.md`: AV5 (Lote 5), DM6 (Lote 6) e EV15 (Lote 8);
  - `validador.md`: quase todas, até o Lote 16;
  - `conceitos.md`: AV, CU1 e UI1;
  - `migracao-2-para-3.md`: VN, EV, CU e DE;
  - `sandbox.md`: DM e SL.

  Nenhuma Spec aprovada escreve em `tempo.md`, `tags.md`, `pooling.md` ou `event-debugger.md`.
- `.claude/skills/revisao/SKILL.md:16-20` (os quatro eixos) e `.claude/skills/implementar/SKILL.md:9-22` (passos da Fábrica) não citam molde nem regra de escrita.
- Os papéis do time não têm a ferramenta Skill. A humanizer está em `C:/Users/gabri/.claude/skills/humanizer/SKILL.md`, e quem escreve lê esse arquivo e aplica as regras dele.

## Design

### D1. Navegação: `nav` explícito, com as pastas ligadas ao módulo

A ordem de aprendizado fica no `nav` do `mkdocs.yml`, e a pasta continua dizendo de que parte do pacote a página trata. Assim, nenhuma página muda de endereço na etapa 1 e nenhum `[HelpURL]` muda.

O `nav` tem sete grupos, nesta ordem: Começar; Sistemas de habilidade; Sistemas de apoio; Editor e ferramentas; Exemplos; Guias; Referência. Grupo sem página fica fora do `nav`. Guias ainda não tem página, então o site mostra seis grupos até o primeiro guia (etapa 3).

O `nav` da etapa 1, com as 14 páginas de hoje:

```yaml
nav:
  - Começar:
      - index.md
      - inicio/instalacao.md
  - Sistemas de habilidade:
      - modulos/gas.md
  - Sistemas de apoio:
      - modulos/eventos.md
      - modulos/tags.md
      - modulos/tempo.md
      - modulos/pooling.md
  - Editor e ferramentas:
      - inicio/inspector.md
      - ferramentas/validador.md
      - ferramentas/gas-debugger.md
      - ferramentas/event-debugger.md
  - Exemplos:
      - exemplos/sandbox.md
  - Referência:
      - inicio/conceitos.md
      - regras/migracao-2-para-3.md
```

O rótulo de cada página é o título `#` dela, então o `nav` não repete títulos. `conceitos.md` fica em Referência porque hoje é o mapa de assemblies; a página de entrada "Como os módulos se ligam" chega na etapa 3.

O build confere o I26 nos dois sentidos. Com o bloco `validation` do MkDocs 1.6, o build avisa, e o `--strict` transforma o aviso em falha:

```yaml
validation:
  nav:
    omitted_files: warn     # página que existe e não está no nav
    not_found: warn         # nav aponta para página que não existe
  links:
    not_found: warn
    anchors: warn           # link para âncora que não existe em outra página
```

A Fábrica confere os nomes das chaves na documentação do MkDocs 1.6.1 antes de escrever. O tema ganha `navigation.sections`, que mostra os grupos na barra lateral, e `navigation.footer`, que põe anterior e próximo no rodapé.

O mapa das páginas futuras fica no guia do D2, para quem cria uma página saber onde ela entra:

| Grupo | Páginas que já existem ou que as Specs aprovadas criam |
|---|---|
| Começar | `index.md`, `inicio/instalacao.md`; na etapa 3: primeira habilidade, requisitos mínimos da cena, como os módulos se ligam |
| Sistemas de habilidade | `modulos/gas.md` (visão geral); da divisão (etapa 2): atributos, efeitos, habilidades e ações, ativação, alvos e projéteis; `modulos/dano-escudo-e-morte.md`, `modulos/thinkers-e-auras.md`, `modulos/descricao.md` |
| Sistemas de apoio | `modulos/eventos.md` e as páginas que saem dela (etapa 2); `modulos/tags.md`, `tempo.md`, `pooling.md`, `times.md`, `cues.md`, `texto.md`; `ui/*.md` |
| Editor e ferramentas | `inicio/inspector.md`, `ferramentas/*.md` |
| Exemplos | `exemplos/sandbox.md`; Receitas (B-019f) |
| Guias | `guias/*.md` (etapa 3) |
| Referência | catálogo gerado (B-019g), API em DocFX (B-019g), `inicio/conceitos.md`, `regras/migracao-2-para-3.md`, perguntas frequentes (etapa 3) |

As páginas novas dos sistemas ficam soltas em `modulos/`: `modulos/atributos.md`, e não `modulos/gas/atributos.md`. As Specs aprovadas já usam esse padrão (`dano-escudo-e-morte.md`, `thinkers-e-auras.md`), e ele não pede mudança no `ManualLinksTests`, que não acha `pasta/index.md`.

Páginas geradas só no build (pergunta P1 do PRD para o Arquiteto):

> Revisto em 2026-11-01 pela G1 do gate do B-019g: o catálogo e os YAML da API são gerados na máquina, pela Unity, e commitados, e um teste de guarda falha se estiverem desatualizados. A Action só monta o site, sem Unity. Valem a 0040 revista e a `spec/referencia-catalogo-e-api.md` (D2, D5, D6). Os itens abaixo continuam valendo, menos estes: a pasta `manual/referencia/` não é ignorada pelo git, a geração acontece antes do commit e não na Action, e o build local não precisa mais gerar a página.

- O catálogo do B-019g é gerado na Action e não vai para o repositório (decisão P1 daquele PRD). O gerador escreve em `manual/referencia/`, numa pasta ignorada pelo git.
- O ticket do B-019g que liga o gerador ao site põe no mesmo diff a entrada da página no `nav`, o passo de geração antes do `mkdocs build` no `docs.yml` e o comando para gerar a página antes do build local.
- O I26 vale porque o site publicado tem a página. Sem o gerador, o build local falha no `not_found`; quem quer o build local roda o comando de geração antes.
- Nenhum `[HelpURL]` aponta para página gerada, porque o `ManualLinksTests` lê o disco do repositório, onde ela não existe.
- A API do DocFX sai fora do `docs_dir` e entra no `nav` como link absoluto (`https://gabrielctorres.github.io/SpellBook/api/`). O MkDocs não valida link absoluto, então o `--strict` aceita a entrada.
- Os detalhes estão na Spec do B-019g (`spec/referencia-catalogo-e-api.md`).

### D2. Guia do manual (molde e regras de escrita)

O guia é um documento interno, `Documentation~/guia-do-manual.md`. Ele fica fora de `manual/` e por isso fora do site, e a emenda da 0007 o põe na lista do item 7. Ele tem sete partes.

1. Molde da página de sistema. São nove seções, nesta ordem. As seções 1, 2 e 4 são obrigatórias, e as outras entram quando há o que dizer.

   ```markdown
   # Nome do sistema {#id}
   (1) Uma frase do que a página ensina. Um parágrafo: que problema o sistema resolve e quando usar.
       Linha do módulo: assembly `SpellBook.X`, depende de `SpellBook.Core`.

   ## Como funciona {#como-funciona}
   (2) As peças e como se ligam, na linguagem de quem monta a cena. Sem método, sem alocação, sem ordem interna.
       Diagrama em Mermaid quando ajudar, com legenda de uma frase.

   ## Uso rápido {#uso-rapido}
   (3) Passos numerados no imperativo, com o menu exato (**Create > SpellBook > ...**) e valores concretos.
       Último passo: aperte Play e veja o que acontece.

   ## <Componente ou asset> {#<nometipo>}
   (4) O que é, em uma ou duas frases.
   ### Campos do Inspector {#<nometipo>-campos}
   - `Campo`: uma frase sobre o efeito no jogo.
   - `Modo`: escolha entre
       - `Opção A`: efeito.
       - `Opção B`: efeito.
   (repita para cada componente ou asset)

   ## Prontos para usar {#prontos}
   (5) Os tipos que vêm no pacote, com link para o catálogo gerado. Até o B-019g, a lista que já existe no manual.

   ## Pelo código {#codigo}
   (6) Dois ou três trechos curtos (ler, aplicar, ouvir). Desempenho e ordem interna ficam aqui. Link para a API.

   ## Criar o seu {#criar-o-seu}
   (7) Quando estender vale a pena e um modelo de classe comentado.

   ## Limitações e armadilhas {#armadilhas}
   (8) Regra, exemplo errado, exemplo certo, saída quando a regra não cabe. `!!! note` e `!!! warning`.

   ## Veja também {#veja-tambem}
   (9) Receita, guia, exemplo, ferramenta de depuração e validação ligados ao sistema.
   ```

2. Moldes curtos para os outros tipos de página:
   - ferramenta: o que é, como abrir, como ler a janela, exemplo;
   - guia: objetivo em uma frase, plano em passos, uma seção por peça, caso de borda;
   - exemplo: o que mostra, como importar, passo a passo por cena, onde ficam os arquivos;
   - entrada: para quem é, o que fazer, próximo passo.
3. Regras de escrita:
   - Leia `C:/Users/gabri/.claude/skills/humanizer/SKILL.md` e aplique as regras dele a todo texto novo, ao escrever e ao revisar. Os papéis não têm a ferramenta Skill, então ninguém chama `/humanizer`: a regra é ler o arquivo e aplicá-lo.
   - Escreva em segunda pessoa, com uma ideia por parágrafo e verbo no imperativo nos passos.
   - Escreva o nome de campo como aparece no Inspector, em código.
   - Ponha o caminho de menu em negrito.
   - Não ponha sigla em título. Fora do título, a sigla só aparece depois do nome por extenso na mesma página. "GAS" é a exceção: pode aparecer depois de "sistema de habilidades" por extenso na página e no nome das ferramentas que o usam (GAS Debugger). Nome de tipo em código (`AbilitySystemComponent`) não é sigla.
   - Não use imagem, captura de tela nem gif. O diagrama é Mermaid, tem legenda de uma frase, diz que parte do pacote representa e usa os nomes do Inspector.
4. Âncoras e constante por tipo (D3).
5. Páginas antigas durante a transição. Até o módulo ser reescrito (etapa 2), quem muda uma página antiga segue as regras de escrita no trecho que escreve e põe o componente novo num bloco "Campos do Inspector", sem reestruturar o resto da página. Página nova nasce no molde.
6. O mapa do D1 e onde cada página nova entra no `nav`.
7. Conferências por busca, que a Fábrica e o Examinador rodam:
   - imagem: `grep -rnE '!\[|<img' Documentation~/manual` não acha nada;
   - sigla em título: `grep -rnE '^#{1,6} .*\b[A-Z]{2,}\b' Documentation~/manual` só acha nomes de tipo em código, GAS (pela exceção acima) e nomes de produto (Unity, JSON);
   - diagrama sem legenda: todo bloco ```` ```mermaid ```` tem uma linha de legenda logo abaixo;
   - build: `mkdocs build --strict -f Documentation~/mkdocs.yml`.

### D3. Mover âncora sem quebrar o `[HelpURL]` (pergunta P2 do PRD para o Arquiteto)

1. Reescrita na mesma página (Tempo, Tags, Pooling, ferramentas). A âncora `{#id}` de todo componente e asset fica como está. A seção do componente usa o nome do tipo em minúsculas como âncora, que é o padrão de hoje. Nenhum arquivo de código muda e o ticket não usa a Unity.
2. Divisão de página (GAS, Eventos). A seção vai para a página nova, e o mesmo ticket:
   - atualiza `DocUrls`;
   - atualiza os links das outras páginas, porque o `validation.links.anchors` derruba o `--strict` em link quebrado;
   - roda o `ManualLinksTests`.

   O ticket usa a Unity. Não há redirecionamento, que pediria um plugin novo, nem página de remendo no endereço antigo.
3. Constante por tipo (Q3, aprovada). O `[HelpURL]` usa uma constante de `DocUrls` que já tem página e âncora:

   ```csharp
   public const string AttributeDefinition = GasAttributes + "#attributedefinition";
   [HelpURL(DocUrls.AttributeDefinition)]
   ```

   Os testes que conferem a URL usam a mesma constante. Mover uma seção passa a mudar só `DocUrls.cs`, a página nova, a página de origem, os links e o `nav`, cerca de 5 arquivos. Pela regra antiga, mover a seção de atributos tocaria 5 ou 6 arquivos de código (`AttributeDefinition`, `AttributeSet`, `AttributeCategory`, as duas fórmulas e talvez `TextRoleTheme`), mais `DocUrls.cs`, duas páginas e o `mkdocs.yml`, e passaria de 10. O `EveryDocUrlHasPage` já cobra página e âncora das constantes (`BrokenReason`, `:107-113`), então a regra não pede teste novo.

   Os `[HelpURL]` que existem migram por módulo, em tickets mecânicos com Unity, antes da divisão daquele módulo. Código novo já nasce com a constante, pela emenda da 0007.

### D4. Mermaid sem dependência nova

O Material desenha Mermaid com a extensão `pymdownx.superfences`, que já vem instalada com ele:

```yaml
markdown_extensions:
  - attr_list
  - admonition
  - toc
  - pymdownx.superfences:
      custom_fences:
        - name: mermaid
          class: mermaid
          format: !!python/name:pymdownx.superfences.fence_code_format
```

O `superfences` substitui o `fenced_code`, que sai. O `requirements-docs.txt` trava a versão do `pymdown-extensions` que o `pip install` resolve hoje com o Material 9.8.0 (Q5, aprovada). O pacote já está instalado, e a trava só fixa a versão. O pacote do SpellBook e o projeto Unity não mudam (N1).

O `--strict` confere o Markdown, mas o diagrama é desenhado no navegador, pelo script do Mermaid que o Material carrega. Por isso a Fábrica confere todo diagrama novo abrindo a página no site gerado, sem Unity.

### D5. Onde fica a regra da humanizer (pergunta P3 do PRD para o Arquiteto)

A regra é a mesma nos três lugares onde aparece: ler `C:/Users/gabri/.claude/skills/humanizer/SKILL.md` e aplicar as regras dele ao texto novo do manual.
- A emenda da 0007 a põe no gate.
- O guia do manual a dá a quem escreve.
- O `/implementar` a cobra no passo 7, o relatório: a Fábrica declara "texto do manual revisado pelas regras da humanizer (SKILL.md)" e lista as páginas.

O `/revisao` ganha, no eixo ADR, a conferência do molde, das buscas do D2 e da declaração. Pela Q4, a Fábrica aplica as regras e declara isso no relatório, e o Examinador confere a declaração e as regras objetivas do guia (molde, siglas, imagens, legenda dos diagramas, `nav` e build). O Examinador não reaplica a humanizer ao texto.

### D6. Tamanho dos tickets de reescrita (pergunta P4 do PRD para o Arquiteto)

Cada ticket cobre uma página de destino, nunca um módulo inteiro.
- Página do manual conta só no limite de arquivos (I35), então o teto que pesa é o de 10 arquivos.
- Uma página de destino junta a página nova, a página de origem, `DocUrls.cs`, `DocUrlsTests.cs` (o caso da constante nova), os links de outras páginas e o `mkdocs.yml`: entre 5 e 8 arquivos.
- A reescrita na mesma página é um ticket por página, com 1 ou 2 arquivos.
- A migração das constantes (D3) é um ticket por módulo, ou por metade de módulo quando passa de 10 arquivos. Cada arquivo muda uma linha.

A reescrita sem Unity anda em paralelo, numa cópia de trabalho própria, e não ocupa lugar no lote. A migração e a divisão usam a Unity e formam um lote próprio entre dois lotes da fila (etapa 2, abaixo).

Todo ticket que cria página põe a entrada dela no `mkdocs.yml`, inclusive os seis que já estão em 10 arquivos (DM1, TM1, CU2, DE8, UI2, TT1). Pela Q2, essa entrada conta junto com a página e não soma arquivo (emenda de 2026-11-01 da 0003, aceita).

## Quebras de compatibilidade

Etapa 1: nenhuma. Nenhuma página muda de endereço, nenhum `[HelpURL]` muda e nenhuma API muda.

Etapas 2 e 3 (esboço): a divisão do GAS e dos Eventos muda o endereço de seções do site. Um link salvo fora do site para `modulos/gas/#atributos`, ou para outra seção movida, deixa de levar à seção, embora a página `modulos/gas/` continue existindo. Na migração das constantes (D3), `DocUrls` ganha constantes públicas e nenhuma sai. Cada ticket registra a quebra de endereço no CHANGELOG, em "Documentação", com a lista dos endereços novos.

## Tickets

### Resumo da etapa 1

| Ticket | Título da issue | Unity? | Paralelo sem Editor? | Depende de | Tamanho |
|---|---|---|---|---|---|
| MN1 | `MN1: docs(manual): navegação em sete grupos, validação do nav e Mermaid` | não | sim | gate | 2 arquivos; ~45 linhas de configuração |
| MN2 | `MN2: docs(manual): guia do manual com molde de página e regras de escrita` | não | sim | gate | 1 arquivo; 0 linhas de código |
| MN3 | `MN3: docs(processo): molde e regras de escrita no /implementar e no /revisao` | não | sim | MN2 | 4 arquivos; 0 linhas de código |

Ordem e paralelo, pela emenda de 2026-10-29 da 0003:
- MN1 e MN2 não tocam nenhum arquivo em comum, nem entre si nem com os tickets dos Lotes 5 a 7. Podem andar juntos e em paralelo ao Lote 5, cada um numa cópia de trabalho própria, sem trocar a branch do checkout do ticket com Unity.
- MN3 espera o MN2, porque aponta para o guia, e não toca arquivo de nenhum ticket com Unity.
- Os três precisam estar na `main` antes do primeiro ticket que cria página (DM1, Lote 6). Até lá, os tickets do Lote 5 seguem a regra de páginas antigas do D2, que vale desde a aceitação da emenda.
- A branch é `fix/manual-mnN`, como nos tickets de docs dos rápidos.

### MN1 — `docs(manual): navegação em sete grupos, validação do nav e Mermaid`

- Escopo: `Documentation~/mkdocs.yml`, `Documentation~/requirements-docs.txt`.
  - `nav` com os grupos e as 14 páginas do D1, nesta ordem.
  - Bloco `validation` do D1.
  - `theme.features`: `navigation.sections`, `navigation.footer`.
  - Mermaid pelo `pymdownx.superfences` do D4, sem o `fenced_code`.
  - O comentário da linha 3 passa a dizer: "Com nav explícito: o build falha se uma página ficar fora do nav ou se o nav apontar para página que não existe (ADR 0007, I26, emenda de 2026-11-01)".
  - `requirements-docs.txt`: `pymdown-extensions==<versão que o pip resolve hoje com o Material 9.8.0>` (Q5).
- Testes red (critério verificável, sem Unity, no venv do manual):
  - antes do ticket, uma página de rascunho fora do `nav` (`manual/rascunho.md`, que não é commitada) passa no `mkdocs build --strict`; depois, o build falha com "not included in the nav";
  - antes, não há `nav` onde pôr `modulos/nao-existe.md`; depois, um `nav` com essa entrada falha com "not found";
  - antes, um link de rascunho para `modulos/gas.md#ancora-que-nao-existe` passa; depois, falha;
  - antes, um bloco ```` ```mermaid ```` aparece como código; depois, aparece como diagrama ao abrir o site gerado no navegador.

  O rascunho é apagado antes do diff.
- Critério de pronto:
  - `mkdocs build --strict -f Documentation~/mkdocs.yml` passa com as 14 páginas;
  - a barra lateral mostra seis grupos, na ordem do D1;
  - os quatro casos red falham ou passam como descrito.

  Se o `anchors: warn` acusar um link quebrado que já existe hoje, o ticket corrige o link na página, e cada página corrigida conta 1 arquivo. Acima de 10 arquivos, a Fábrica para e devolve.
- Tamanho estimado: 2 arquivos; ~45 linhas de configuração.

### MN2 — `docs(manual): guia do manual com molde de página e regras de escrita`

- Escopo: `Documentation~/guia-do-manual.md` (novo), com as sete partes do D2:
  - o molde da página de sistema;
  - os moldes curtos;
  - as regras de escrita, com a leitura do `SKILL.md` da humanizer;
  - a regra de âncoras e a constante por tipo do D3 (Q3);
  - a regra das páginas antigas;
  - o mapa do D1;
  - as conferências por busca.

  O guia traz um exemplo curto de bloco "Campos do Inspector" e um de diagrama com legenda, ambos com nomes reais do pacote (`AttributeDefinition`, `GameplayEffect`). A Fábrica lê `C:/Users/gabri/.claude/skills/humanizer/SKILL.md` e aplica as regras dele ao texto do guia.
- Testes red (critério verificável):
  - `test -f Documentation~/guia-do-manual.md` falha antes do ticket e passa depois;
  - `grep -c '^## ' Documentation~/guia-do-manual.md` acha as sete partes;
  - o guia não aparece no site: o `mkdocs build --strict` não gera `guia-do-manual/`.
- Critério de pronto:
  - o guia cobre o molde de nove seções, com as obrigatórias marcadas;
  - não usa sigla sem o nome por extenso;
  - não contradiz a emenda da 0007 nem o D1 a D6;
  - a regra de escrita manda ler e aplicar o `SKILL.md` da humanizer e não cita a ferramenta Skill nem o comando `/humanizer`;
  - o relatório da Fábrica declara que o texto passou pelas regras da humanizer.
- Tamanho estimado: 1 arquivo; 0 linhas de código (~180 linhas de texto).

### MN3 — `docs(processo): molde e regras de escrita no /implementar e no /revisao`

- Escopo:
  - `.claude/skills/implementar/SKILL.md`:
    - no passo 1, a Fábrica lê o `guia-do-manual.md` quando o ticket toca `manual/`;
    - no passo 5, roda as buscas do guia e o `--strict`;
    - no passo 7, declara que leu `C:/Users/gabri/.claude/skills/humanizer/SKILL.md` e aplicou as regras dele ao texto novo, e lista as páginas.
  - `.claude/skills/revisao/SKILL.md`: no eixo ADR, uma frase que manda conferir a emenda de 2026-11-01 da 0007 com as buscas do guia e a declaração da humanizer. O Examinador não reaplica a humanizer (Q4).
  - `.claude/agents/fabrica.md` e `.claude/agents/examinador.md`: uma linha cada que aponta para o guia.
- Testes red (critério verificável):
  - `grep -n "guia-do-manual" .claude/skills/implementar/SKILL.md .claude/skills/revisao/SKILL.md .claude/agents/fabrica.md .claude/agents/examinador.md` acha os quatro arquivos;
  - `grep -n "humanizer/SKILL.md" .claude/skills/implementar/SKILL.md` acha o passo 7.

  Nenhum dos dois acha nada antes do ticket.
- Critério de pronto: os greps passam; ``grep -rnE '(^|[[:space:]`(])/humanizer' .claude/skills .claude/agents`` não acha nada; o critério de nota do `/revisao` fica como está; o texto novo não contradiz a 0003, a 0005 nem o `TIME.md`.
  - Emenda de 2026-11-01 (revisão do MN3, issue #285): a busca antiga, `grep -rn "/humanizer"`, achava o caminho `C:/Users/gabri/.claude/skills/humanizer/SKILL.md` que o passo 7 manda citar, e o critério contradizia o próprio ticket. A busca nova proíbe só o comando `/humanizer`, isto é, `/humanizer` no começo da linha ou depois de espaço, crase ou parêntese. O caminho do arquivo continua permitido. A forma `(^|[^/])/humanizer` não serve, porque o `s` de `skills/humanizer` satisfaz `[^/]`.
- Tamanho estimado: 4 arquivos; 0 linhas de código (~15 linhas de texto).

### Etapa 2 (esboço): reescrita por módulo, quando o módulo fecha

Um módulo fecha quando nenhum ticket aprovado ainda escreve na página dele. Todo ticket abaixo segue o molde e as regras da humanizer. "Sem Unity" quer dizer reescrita na mesma página, com as âncoras do D3.1.

| Ticket | Página | Quando | Unity? |
|---|---|---|---|
| MN4 | Tempo (`modulos/tempo.md`), na mesma página | em paralelo ao Lote 5 | não |
| MN5 | Tags (`modulos/tags.md`), na mesma página | em paralelo ao Lote 5 | não |
| MN6 | Pooling (`modulos/pooling.md`), na mesma página | em paralelo ao Lote 5 ou 6 | não |
| MN7 | Event Debugger (`ferramentas/event-debugger.md`), na mesma página | em paralelo ao Lote 6 | não |
| MN8 a MN11 | Constantes por tipo (D3.3): Core, Tempo e Tags; Pooling e Sample; Eventos e pontes do GAS; GAS (duas metades) | lote próprio entre o Lote 7 e o Lote 8, antes da divisão | sim |
| MN12 a MN17 | Divisão do GAS: atributos, efeitos, habilidades e ações, ativação (custo, recarga, condições), alvos e projéteis; por último, `gas.md` reescrita como visão geral | mesmo lote próprio, logo depois das constantes; B-007, B-009 e B-010 fecham no Lote 7 (DM8) | sim |
| MN18 a MN20 | Divisão dos Eventos: canais, ouvinte e emissor (`EventListener`, `EventEmitter`), eventos do GAS (canais do GAS, relay, `RaiseEventAction`); `eventos.md` vira visão geral | entre o Lote 8 e o Lote 9; o último ticket que escreve em `eventos.md` é o EV15 (Lote 8) | sim |
| MN21 | Inspector (`inicio/inspector.md`), na mesma página | em paralelo ao Lote 10, depois das abas do Inspector do B-012 (Lote 9) | não |
| MN22 | GAS Debugger (`ferramentas/gas-debugger.md`), na mesma página | em paralelo ao Lote 10 | não |
| MN23 | Sandbox (`exemplos/sandbox.md`), na mesma página | depois do Lote 15 (SB-T8, SL13) | não |
| MN24 | Validador (`ferramentas/validador.md`), na mesma página | depois do Lote 17 | não |
| MN25 | Migração 2.x para 3.0 (`regras/migracao-2-para-3.md`), na mesma página | depois do Lote 17 | não |

- As constantes e a divisão do GAS somam cerca de 10 tickets, o tamanho de um lote de PR. Se passar de 10, o Controlador leva o resto para o começo do Lote 8.
- Teams, Cues, Thinkers, Dano, Texto, Descrição e a interface não precisam de reescrita, porque as páginas deles nascem no molde, nos próprios tickets, pela emenda da 0007.
- Depois da divisão do GAS, os tickets dos Lotes 8 a 12 que ainda citam `gas.md` escrevem na página que o mapa do guia indica. A emenda desta Spec, no gate parcial do MN12, lista essa troca ticket por ticket.
- O B-019g entra antes da divisão (Q6), então a seção "Prontos para usar" das páginas divididas do GAS e dos Eventos já nasce como link para o catálogo gerado, em vez de mover a lista escrita à mão (R10, CA7).

### Etapa 3 (esboço): páginas de entrada, guias e diagramas, depois do Lote F

Os tickets dependem do GAS final (Lote 12), dos templates (B-019a) e das cenas das Receitas (B-019f). Nenhum usa a Unity, salvo onde está marcado.

| Ticket | Página | Unity? |
|---|---|---|
| MN26 | "O que é o SpellBook" (`index.md`): propósito, para quem, módulos e limitações | não |
| MN27 | "Requisitos mínimos da cena" (`inicio/requisitos-da-cena.md`) | não |
| MN28 | "Primeira habilidade" (`inicio/primeira-habilidade.md`), do zero ao dano na tela; o usuário confere o CA2 à mão no Editor | não (conferência no Editor pelo usuário) |
| MN29 | "Como os módulos se ligam" (`inicio/como-os-modulos-se-ligam.md`), com diagrama; o mapa de assemblies de `conceitos.md` vai para Referência; se `DocUrls.Concepts` mudar, o ticket usa a Unity | depende |
| MN30, MN31 | Guias (`guias/`): os primeiros dois ou três, como ligar o GAS a eventos, barra de vida e pausar só parte do jogo; o sétimo grupo aparece aqui | não |
| MN32 | Perguntas frequentes e solução de problemas (Referência) | não |
| MN33 a MN35 | Diagramas que faltam nas páginas de sistema, um ticket por grupo | não |
| MN36 | Conferência final: CA1, CA3, CA5, CA6, CA7 e CA8 por busca e pelo build | não |

## Decisões do gate (2026-11-01)

O usuário aprovou as seis perguntas como propostas.

- Q1. Forma da navegação: `nav` explícito no `mkdocs.yml`, com `validation` nos dois sentidos e as pastas ligadas ao módulo, sem mudar endereço (D1). A alternativa, prefixo numérico nas pastas, mudaria o endereço de todas as páginas e todos os `[HelpURL]`.
- Q2. Entrada do `nav` no limite de arquivos: a mudança do `mkdocs.yml` que só adiciona ou tira a entrada de uma página criada ou apagada no mesmo ticket conta junto com a página e não soma arquivo. A emenda de 2026-11-01 da 0003 passou a Aceita, e DM1, TM1, CU2, DE8, UI2 e TT1 não precisam ser divididos.
- Q3. Troca do `[HelpURL]` na divisão: constante por tipo em `DocUrls`, migrada pelos MN8 a MN11 antes da divisão (D3.3). A emenda da 0007 (item 7) passou a Aceita.
- Q4. Quem aplica a humanizer: a Fábrica aplica e declara no relatório; o Examinador confere a declaração e as regras objetivas do guia. Como os papéis não têm a ferramenta Skill, a regra é ler e aplicar `C:/Users/gabri/.claude/skills/humanizer/SKILL.md` (D2, D5, MN2, MN3).
- Q5. `pymdown-extensions` travado no `requirements-docs.txt`, na versão que o Material 9.8.0 instala hoje (MN1).
- Q6. O B-019g vem cedo: a Spec dele foi escrita logo depois deste gate (`spec/referencia-catalogo-e-api.md`), e os tickets entram na fila com Unity depois do Lote 6, antes da divisão do GAS.
