# PRD: Reorganizar e reescrever o manual publicado (B-035)
Backlog: B-035 · Issues: (criadas depois do gate) · Status: Aprovado · Data: 2026-11-01 (gate decidido pelo usuário em 2026-11-01)

Origem: pedido do usuário em 2026-11-01. Estudo das referências, com a comparação com o manual atual: `scratchpad/estudo-docs-referencias.md` (TopDown Engine e Edgar). ADRs [0007](../adr/0007-documentacao-como-codigo-e-gate-de-doc.md) (gate de doc, I26, CA54), [0025](../adr/0025-documentacao-em-mkdocs-ou-zensical-no-github-pages.md) (ferramenta do site) e [0003](../adr/0003-um-ticket-uma-issue-um-pr.md) (tamanho de ticket).
Relaciona-se com: [B-019f, Receitas](samples-receitas.md), [B-019g, catálogo e API](referencia-catalogo-e-api.md) e [B-032, Sandbox](sample-sandbox.md). Este PRD não reescreve o escopo de nenhum deles.
Sem foco em nenhum jogo.

---

## 1. Problema

O manual (cerca de 2.900 linhas, sem imagem) explica o pacote na ordem do código, não na ordem em que o leitor aprende:

- Não há página de "primeira habilidade" nem de requisitos mínimos da cena. O passo a passo de habilidade vem depois de 230 linhas de detalhe de atributos em `gas.md`.
- `gas.md` (465 linhas) e `eventos.md` (569) juntam conceito, tutorial, campos, catálogo e API de código.
- Detalhe de implementação ("não aloca", ordem interna de eventos) aparece na visão geral.
- Siglas aparecem até nos títulos (GE, GA, ASC), antes de o leitor conhecê-las.
- Não há receitas, guias por tarefa, perguntas frequentes nem solução de problemas. Teams, Cues e UI não têm página.
- A navegação não tem grupos na ordem de aprendizado (ADR 0007, I26: lista só o que existe).

Restam 122 tickets em 13 lotes (5 a 17, de B-007 até a UI, B-018). Cada um muda ou cria páginas pelo gate da ADR 0007. O momento da reforma decide se o time escreve a mesma página uma, duas ou três vezes.

## 2. Objetivo

Quem chega ao SpellBook (designer ou programador) segue um caminho de aprendizado: entende o que é, instala, faz a primeira habilidade em cerca de 10 minutos, aprofunda cada sistema e consulta a referência. O manual é bem explicado, com o mesmo molde em todas as páginas.

**Princípio (modular, sem remendos).** Um molde único de página e uma navegação única, valendo para toda página nova. Nenhuma página nasce no formato antigo para ser refeita depois, e a lista de tipos vem do catálogo gerado, não de texto à mão.

## 3. Usuários e cenários

| Usuário | O que espera |
|---|---|
| **Designer** | Chegar a um resultado na tela sem ler código; achar o campo do Inspector pelo nome |
| **Programador** | Achar a API, o jeito de estender e as armadilhas |
| **Mantenedor** | Saber onde uma página nova entra e que molde segue |

- **C1, primeiro resultado:** da instalação à habilidade que causa dano, seguindo uma página só, em cerca de 10 minutos.
- **C2, conceito antes de detalhe:** a visão geral de cada sistema mostra o quadro e como as peças se ligam, antes de qualquer campo ou método.
- **C3, achar o campo:** cada componente e asset tem um bloco "Campos do Inspector" de formato fixo (nome, uma frase, opções em sub-lista).
- **C4, tarefa pontual:** "como faço X" está em Guias ou Receitas, não dentro da página do sistema.
- **C5, texto sem jargão:** nenhuma sigla nova no título nem antes do nome por extenso.

## 4. Requisitos funcionais

R1. **Navegação em grupos, na ordem de aprendizado** (proposta do estudo, seção 5): Começar; Sistemas de habilidade; Sistemas de apoio; Editor e ferramentas; Exemplos; Guias; Referência. Lista só páginas que existem (ADR 0007, I26). A forma (`nav` explícito ou prefixo de pasta) é do Arquiteto.
R2. **Molde de página de sistema** (estudo, seção 6): escopo e porquê; como funciona; uso rápido; campos do Inspector por componente; prontos para usar; pelo código; criar o seu; limitações e armadilhas; veja também. Só as seções 1, 2 e 4 são obrigatórias.
R3. **Páginas novas de entrada:** "O que é o SpellBook", "Primeira habilidade", "Requisitos mínimos da cena" e "Como os módulos se ligam" (com diagrama).
R4. **Dividir** as páginas acima de cerca de 250 linhas (`gas.md`, `eventos.md`) no molde, sem quebrar nenhum `[HelpURL]`: o mesmo ticket que move uma âncora atualiza a constante em `DocUrls`.
R5. **Detalhe de desempenho e de ordem interna** vai para "Pelo código" ou para a API, não para "Como funciona".
R6. **Guias e FAQ:** uma seção de Guias (problema e solução) e uma de perguntas frequentes e solução de problemas, nascendo com conteúdo real (I26), sem página de "em breve".
R7. **Diagramas, sem imagens (decisão P3):** o manual não tem prints, capturas de tela nem gifs. Só diagramas em Mermaid, escritos no próprio texto da página. Todo diagrama tem legenda de uma frase, diz qual parte do pacote representa (módulo, fluxo ou ordem) e usa os mesmos nomes de classe, campo e asset do Inspector. Precisa renderizar no build `--strict` do site (a forma de habilitar o Mermaid é do Arquiteto, sem dependência nova no pacote nem no projeto Unity). Onde um print ajudaria, a página descreve o campo pelo nome, no bloco "Campos do Inspector" (C3).
R8. **Regras de escrita (pedido do usuário):**
   - Todo texto do manual é escrito e revisado com a skill humanizer (`/humanizer`).
   - Evite siglas. Use sigla só quando for necessário, e depois do nome por extenso; nunca no título. GAS pode aparecer às vezes.
   - Segunda pessoa, uma ideia por parágrafo, verbos no imperativo nos passos.
   - Nomes de campo como aparecem no Inspector, em código.
R9. **Âncoras estáveis** (`{#id}`) em todo título; o teste de `[HelpURL]` (ADR 0007, CA54) continua passando a cada ticket.
R10. **Ligação com B-019f e B-019g:** Receitas (B-019f) viram o grupo Exemplos; o catálogo e a API gerados (B-019g) viram o grupo Referência. Esta reforma **não escreve à mão** lista de ações, condições ou reações que o B-019g vai gerar.

## 5. Requisitos não funcionais

N1. Sem dependência nova no pacote nem no projeto Unity.
N2. O build `--strict` do site continua passando a cada ticket.
N3. Documentos internos (`prd/`, `spec/`, `adr/`) não entram no site (ADR 0007, item 7).
N4. Páginas de módulos que ainda não existem (Teams, Cues, UI) só entram quando o módulo sair.

## 6. Fora do escopo

- Trocar a ferramenta do site (ADR 0025).
- Traduzir o manual; versionar a documentação por release.
- Escrever Spec ou código; mudar comportamento do pacote.
- Escrever o catálogo e a API (B-019g) e as cenas das Receitas (B-019f).
- Guias por gênero de jogo.
- Imagens, prints, capturas de tela e gifs (decisão P3).

## 7. Quando implementar

Quatro momentos possíveis, com o que cada um custa:

| Momento | Prós | Contras |
|---|---|---|
| **A. Antes dos lotes** | Os 122 tickets já escrevem no molde e na navegação certos. Manual melhor logo. | O conteúdo muda lote a lote (valores por nível B-007, relay, dano, thinkers); reescrever agora é reescrever de novo. "Primeira habilidade" mudaria quando o B-007 sair. Atrasa os lotes (WIP de Unity não muda, mas o time de docs disputa a mesma fila de revisão). |
| **B. No meio** (por exemplo, depois do Lote 9) | Parte do GAS já estável. | Os lotes 5 a 9 já escreveram páginas no formato antigo; sem critério claro de corte. Pior dos dois mundos. |
| **C. Depois de tudo** | Conteúdo final, imagens finais, um passe só. | Cerca de 100 tickets escrevem página no formato atual, que depois é refeito. Manual fraco por meses. Reescrita enorme, difícil de revisar (ADR 0003). |
| **D. Por módulo, quando fecha** | Cada reescrita vê o conteúdo final do módulo. Tickets pequenos. | Sem molde e navegação antes, os módulos saem diferentes entre si. "Primeira habilidade" só no fim do GAS. |

**Proposta (híbrida, em três etapas):**

1. **Agora, antes do Lote 5, pequeno e sem reescrever conteúdo:** molde (R2), navegação (R1), regras de escrita (R8) e esqueleto de pastas, mais a emenda da ADR 0007 que torna o molde e as regras parte do gate (o Examinador reprova ticket que escreve fora do molde ou com sigla nova no título). Os 122 tickets já escrevem no molde certo. Também define a forma de mover âncoras sem quebrar `[HelpURL]` (R4, R9).
2. **Por módulo, quando o último ticket dele fecha (D):** a reescrita no molde, com humanizer. Ordem proposta: Eventos, Tags, Tempo, Pooling (módulos estáveis; podem ir antes do GAS e em paralelo, sem Editor); GAS dividido em páginas quando B-007 a B-010 fecharem; Teams, Cues e UI cada um no seu lote.
3. **No fim, depois do Lote F:** "Primeira habilidade", "Requisitos mínimos", "Como os módulos se ligam", Guias, FAQ e os diagramas, porque dependem do GAS final, dos templates (B-019a) e das cenas das Receitas (B-019f).

**B-019g (catálogo e API) deve vir cedo.** Ele não espera os Lotes B a D e remove da reescrita as listas de ações e reações. Na etapa 2, as páginas já apontam para o catálogo em vez de listar tipos.

**Ligação com a ADR 0007.** O item 1 (manual muda no mesmo ticket) e o item 2 (gate) continuam valendo e fazem a etapa 1 funcionar: cada ticket já escreve no molde. O item 2 trocou "página ou catálogo" por "página até o gerador existir"; o B-019g encerra esse período. A navegação mexe no I26, e mover âncora mexe no CA54. A emenda nova entra pelo Arquiteto.

## 8. Critérios de aceite

- CA1 (E). R1, R2: o site tem os sete grupos, só com páginas existentes, e toda página nova segue o molde.
- CA2 (E). C1, R3: um leitor novo chega da instalação à habilidade com dano na tela só pela página "Primeira habilidade", em cerca de 10 minutos (conferência manual no Editor).
- CA3 (E). C3: cada componente e asset de sistema tem bloco "Campos do Inspector" no formato fixo.
- CA4 (T). R4, R9: o teste de `[HelpURL]` passa e o `--strict` passa depois de cada divisão de página.
- CA5 (E). R8, C5: nenhum título tem sigla; nenhuma sigla aparece antes do nome por extenso (conferência por busca); todo texto novo passou pelo humanizer.
- CA6 (E). R7: o manual não contém imagem nem captura de tela (conferência por busca em `manual/`); todo diagrama é Mermaid, tem legenda e renderiza no build `--strict`.
- CA7 (E). R10: nenhuma lista de ações, condições ou reações escrita à mão sobra depois do B-019g.
- CA8 (E). Existem páginas de Guias e de FAQ com conteúdo real, e `gas.md` e `eventos.md` ficam abaixo de cerca de 250 linhas por página.

## 9. Perguntas em aberto

### Com o usuário (respondidas no gate de 2026-11-01)
- **P1. Quando fazer a reforma.** Decidido: híbrida em três etapas (molde e navegação agora, reescrita por módulo quando fecha, entrada e diagramas depois do Lote F). Seção 7 vale como escrita.
- **P2. O molde e as regras de escrita entram no gate da ADR 0007?** Decidido: sim. O molde e as regras de escrita viram regra da ADR 0007 (emenda pelo Arquiteto) e o Examinador reprova ticket com sigla nova no título ou página fora do molde.
- **P3. Quem captura as imagens.** Decidido pela alternativa: sem imagens nem capturas de tela; só diagramas em Mermaid. Efeito aplicado: R7, CA6, seção 6, etapa 3 da seção 7 e R10 ajustados.
- **P4. A página "Primeira habilidade" sai antes do B-007?** Decidido: não, espera o GAS estabilizar (valores por nível mudam o passo a passo).

### Com o Arquiteto
- **P1.** Forma da navegação (`nav` explícito ou prefixo) e como lista páginas geradas só no build (B-019g) sem ferir o I26.
- **P2.** Como mover âncora e página sem quebrar `[HelpURL]` e o CA54 (redirecionamento ou atualização de `DocUrls` no mesmo ticket).
- **P3.** Emenda da ADR 0007 (molde e regras de escrita no gate), e onde a regra do humanizer fica registrada.
- **P4.** Tamanho dos tickets de reescrita (um por página ou um por módulo) e ordem dentro dos lotes.

## 10. Ajustes de playtest

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-11-01 | usuário: reorganizar e reescrever o manual, com humanizer, sem siglas desnecessárias; time diz quando implementar | Todo o documento | Aprovado |
| 2026-11-01 | gate: P1, P2 e P4 como propostas; P3 pela alternativa (sem imagens, só diagramas Mermaid) | R7, CA6, seções 6, 7 e 9 | Aplicado |
