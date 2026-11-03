# DS: Inspector de ações (abas por evento, resumo e seletor de alvo)
Status: Rascunho · Data: 2026-10-31 · Dono: Desenhista
Origem: [PRD B-012](../prd/gas-evento-acoes-alvos.md) (R31, P5), [ADR 0018](../adr/0018-hierarquia-unica-de-acoes.md) (item 8), [ADR 0026](../adr/0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md) (Inspector em UI Toolkit), [ADR 0013](../adr/0013-serializereference-como-costura-entre-modulos.md) (`[SerializeReference]`).

Este documento define **como o designer vê e edita ações**. Ele não define a API nem a arquitetura: isso é da Spec. Onde uma regra do DS depende de uma decisão técnica, a seção 10 lista a pergunta para o Desenhista.

---

## 1. Escopo

Vale para todo dono que tem listas de ações por evento: `GameplayAbility`, `GameplayEffect`, o projétil (impacto e, na Fase 8, término) e, na Fase 8, o thinker. Vale também para as listas de **gatilhos** e de **condições** quando elas aparecem no mesmo Inspector (a aparência de cada linha segue as regras de linha da seção 4).

Fora do escopo: o desenho do resto do Inspector do `GameplayAbility` e do `GameplayEffect` além do que a seção 8 diz sobre a ordem; widgets de UI de jogo (módulo de UI); o Inspector das ações do B-004 em `GAS.Bridges`, que usa as mesmas regras sem exceção.

## 2. Princípios

1. **Ler antes de abrir.** O designer entende o que uma habilidade faz **só pelas linhas fechadas**, sem expandir nenhuma ação.
2. **Um vocabulário só.** Habilidade, efeito, projétil e thinker usam as mesmas abas, as mesmas linhas e o mesmo seletor. Aprendeu em um, sabe nos outros (ADR 0018).
3. **Evento é lugar, não tipo.** A aba diz **quando** a lista roda; a linha diz **o que** faz e **em quem**.
4. **Sem cor sozinha.** Toda informação dada por cor também aparece por ícone ou texto (seção 7).
5. **Nada se perde em silêncio.** Trocar o tipo de uma ação, apagar ou reordenar tem Undo; tipo ausente não apaga dados (ADR 0026).
6. **Só UI Toolkit.** Sem IMGUI e sem atributo novo no Core fora do critério da ADR 0026, item 5 (seção 10, P-DS3).

## 3. Abas por evento

### 3.1 Estrutura

- O Inspector mostra uma **faixa de abas** (`TabView` ou equivalente) logo abaixo das seções de configuração da seção 8. **Uma aba por evento** do dono. Dentro da aba, a **lista de ações** daquele evento.
- A aba mostra o **nome do evento em português** e, quando há ações, um **contador** (`Ao ativar · 3`). Aba sem ação mostra só o nome, em cor de texto secundária.
- Um **indicador de validação** na aba: ponto de aviso (amarelo) ou de erro (vermelho) com o número de mensagens, quando alguma ação da aba tem mensagem de `IValidatable`. O indicador some quando a lista está válida.
- A aba selecionada é **lembrada por asset** durante a sessão do Editor (ao selecionar outro asset e voltar, abre na mesma aba). Um asset novo abre na primeira aba que tem ação ou, se nenhuma tem, na primeira.
- **Só aparecem as abas que o dono oferece** (e, para a habilidade, as que o comportamento escolhido usa: a aba "A cada passo da canalização" só existe com `Channel`). Mudar o comportamento não apaga ações de uma aba que some: a aba fica **oculta com um aviso** na seção de comportamento ("N ações em 'A cada passo' não vão rodar"), e o validador também acusa.

### 3.2 Abas por dono

Os nomes abaixo são o texto do Inspector. A **lista fechada** de eventos na 3.0 é do Desenhista (PRD B-012, P10); se mudar, muda aqui.

| Dono | Abas (na ordem) |
|---|---|
| `GameplayAbility` | Ao ativar · Ao terminar · Ao ser cancelada · Ao ser concedida · Ao ser retirada · A cada passo da canalização (só `Channel`) |
| `GameplayEffect` | Ao ser aplicado · A cada tick · Ao ser removido · Ao receber dano · Ao morrer · Ao executar habilidade |
| Projétil | No impacto · (Fase 8) Ao terminar |
| Thinker (Fase 8) | Ao criar · A cada pulso · Ao terminar |

- "Ao receber dano", "Ao morrer" e "Ao executar habilidade" do efeito agrupam-se visualmente numa **segunda faixa** chamada "Reações" (mesmo componente, rótulo de grupo), para as três primeiras abas do efeito (ciclo de vida) não ficarem misturadas com as de reação.
- Cada aba tem um **subtítulo** de uma linha, em texto secundário, que diz **quem é o alvo padrão** daquele evento (PRD B-012, R9 e C3): "Alvo padrão: alvo da habilidade", "Alvo padrão: dono do efeito", "Alvo padrão: quem foi atingido". O subtítulo é a resposta ao "em quem age uma ação sem seletor?".

### 3.3 Aba vazia

Mostra uma linha de texto secundário ("Nenhuma ação neste evento.") e o botão **Adicionar ação**. Nada de caixa grande.

## 4. A lista e a linha de ação

### 4.1 Lista

- Lista reordenável (`ListView` com `reorderable`), uma **linha por ação**, com o botão **+ Adicionar ação** no rodapé e o contador "N ações".
- Cabeçalho da lista (acima da primeira linha), com dois botões pequenos: **Expandir tudo** e **Recolher tudo**.
- Por padrão, **toda ação aparece recolhida**. Uma ação recém-adicionada aparece **expandida** e com o foco no primeiro campo.
- **Reordenar:** arrastar pela **alça** (ícone de seis pontos, à esquerda). A posição de soltura é mostrada por uma linha horizontal. A ordem da lista é a ordem de execução, e o texto de ajuda da aba diz isso ("Roda de cima para baixo").
- **Menu de contexto** (botão direito e botão de três pontos da linha): Duplicar, Mover para cima, Mover para baixo, Mover para o topo, Mover para o fim, Remover. **Duplicar** cria uma **instância nova** com os dados copiados (nunca a mesma referência, ADR 0013/0026), logo abaixo da original.
- **Teclado:** `Delete` remove a linha com foco; `Ctrl+D` duplica; `Alt+↑`/`Alt+↓` move; `Enter` expande/recolhe; `↑`/`↓` navega entre linhas.
- **Remover** pede confirmação só quando a ação tem filhas (ações de fluxo com lista dentro). Há Undo em tudo.

### 4.2 Anatomia da linha recolhida

Da esquerda para a direita, numa linha só (altura de uma linha de texto, com respiro):

```
⠿  [ícone cat.]  Aplicar efeito  Dano de fogo   → Alvos do evento   · se Mana ≥ 20   ⚠   ⋯
```

1. **Alça** de arrastar.
2. **Ícone da categoria** (seção 6), com a **barra de cor** da categoria na borda esquerda da linha (4 px).
3. **Verbo da ação** (o nome do tipo, em negrito), seguido do **objeto principal** em texto normal (o asset, a tag, o valor mais importante). Exemplo: `Aplicar efeito Dano de fogo`, `Esperar 2 s`, `Chance 30 %`.
4. **Seletor de alvo** como "chip" (seção 5): `→ Alvos do evento`. Ação que não usa alvo não mostra o chip.
5. **Condição**, quando existe: `· se <resumo>` em texto secundário, truncado com reticências; a dica completa no tooltip.
6. **Ícone de validação**, só quando há mensagem: ⚠ amarelo (aviso) ou ✖ vermelho (erro), com a mensagem no tooltip.
7. **Menu** (três pontos).

Regras do resumo:
- **Cabe em uma linha.** O que não cabe é truncado **do fim**, e o tooltip da linha mostra o resumo inteiro, sem truncar.
- Os valores vêm do próprio asset, em português: `Esperar 2 s`, `Repetir 3× a cada 0,5 s`, `Atrasar 2 s (continua se cancelar)`.
- Valor por nível aparece com o **nome**, não o número: `Esperar %tempo` (o número muda por nível).
- **Sem texto de erro no resumo.** O erro é o ícone da validação; o resumo continua descrevendo a configuração, mesmo errada.
- O texto do resumo é gerado pelo **tipo da ação**, que informa o verbo e o objeto (o mesmo metadado do catálogo, `[TypeCategory]` + resumo; seção 10, P-DS1). Uma ação própria do projeto sem resumo mostra só o nome do tipo.
- **Ação nula** (`None`) mostra "(Escolha uma ação)" em texto secundário e o botão de escolher tipo, sem ícone de categoria.

### 4.3 Ação expandida

- O clique na linha (fora dos controles) ou `Enter` expande/recolhe. Há uma **seta de dobra** à esquerda do ícone.
- Expandida, a linha vira **cabeçalho** (mantém verbo, chip de alvo e validação) e **abaixo** aparecem os campos da ação, um `PropertyField` por campo, na ordem do código, com os rótulos do `[Tooltip]`/`[Header]` nativos (ADR 0026).
- **Trocar o tipo:** o cabeçalho tem o botão **Trocar tipo**, que abre o popup de tipo (ADR 0026, item 4). Os dados da ação anterior são descartados, com **Undo** e **aviso** ("Os dados desta ação serão descartados") quando a ação tinha algum campo diferente do padrão.
- **Condição:** quando a ação aceita condição (`ConditionalAction` ou o campo de condição de uma ação), o campo "Condição" é uma lista com o mesmo seletor de tipo e o resumo de uma linha da hierarquia `Condition` (And/Or/Not aninhados seguem a seção 4.4).

### 4.4 Ações com filhas (fluxo)

`Sequence`, `Parallel`, `Repeat`, `Chance`, `Delayed`, `ForEachTarget` e `Conditional` têm uma **lista de ações filhas**.

- As filhas aparecem **dentro** da ação expandida, **recuadas** em 16 px, com uma **barra vertical** na cor da categoria "Fluxo" à esquerda, e usam **a mesma linha e a mesma lista** (reordenar, duplicar, remover, resumo).
- Recolhida, a linha da ação de fluxo mostra no resumo **quantas filhas** tem (`Repetir 3× a cada 0,5 s · 2 ações`).
- O recuo visual vai até **3 níveis**. Do 4.º em diante o recuo **não aumenta** e a linha mostra um indicador `⋯ nível 4` (a profundidade não some, só deixa de estreitar a tela). O validador **avisa** profundidade acima de 4 ("considere extrair para outra habilidade ou efeito").
- **Arrastar** entre listas diferentes da mesma aba (da lista principal para dentro de um `Repeat`, por exemplo) é permitido. Arrastar entre **abas** não é (o designer usa Duplicar e cola em outra aba: seção 4.5).

### 4.5 Copiar e colar

- **Copiar** e **Colar** no menu de contexto, para a ação **e suas filhas**. Colar numa aba ou numa lista diferente é permitido e cria instâncias novas.
- O conteúdo copiado é um texto serializado por `EditorJsonUtility` (o mesmo método da ADR 0026 para a cópia de uma referência duplicada). Colar uma ação de outro tipo de dono é permitido; se ela não vale no evento de destino, entra e o **validador acusa** (nada é descartado em silêncio).

## 5. Seletor de alvo inline

### 5.1 O chip

Toda ação que age sobre alvos mostra, no cabeçalho da linha, um **chip** com o seletor atual:

| Estado | Texto do chip | Aparência |
|---|---|---|
| Sem seletor | `→ Padrão do evento` | Texto secundário, contorno tracejado. O tooltip explica o alvo padrão da aba (seção 3.2) |
| Quem lançou | `→ Quem lançou` | Contorno cheio, ícone de pessoa |
| Alvos do evento | `→ Alvos do evento` | Contorno cheio, ícone de alvos |
| Ponto | `→ Ponto` | Contorno cheio, ícone de ponto |
| Área | `→ Área 5 m · 2 alvos · Inimigos` | Contorno cheio, ícone de área; resumo do raio, do máximo e dos filtros mais importantes |
| Seletor próprio do projeto | `→ <nome do tipo>` | Contorno cheio, ícone genérico |

- O texto do chip de **Área** cita, na ordem: raio (ou "forma"), máximo de alvos (`N alvos`, ou nada se ilimitado) e, quando houver, o **filtro de time** (`Inimigos`, vindo do Bridges) ou a contagem de tags (`2 tags`). Cabe em uma linha, truncado do fim.
- O chip só aparece quando a ação usa alvos; para as que não usam (`Wait`, `DebugLog`), não existe.

### 5.2 Editar sem sair da linha

- **Clique no chip** abre o popup de tipo do SpellBook (ADR 0026, item 4) com os seletores disponíveis (Quem lançou, Alvos do evento, Ponto, Área e os do projeto), com "(Padrão do evento)" no lugar de "(Nenhum)". Escolher um tipo troca o seletor; o texto do chip atualiza **na hora**.
- **Seletor com campos** (Área e os do projeto): ao escolher, a ação **expande** e os campos do seletor aparecem num **bloco "Alvo"**, logo abaixo do cabeçalho, **antes** dos campos da ação. O bloco tem a mesma barra de cor do chip. Os campos da Área, na ordem: forma (2D ou 3D) e raio · filtro de tags (exigidas e ignoradas, com a consulta do B-006) · incluir o dono · máximo de alvos · ordem (mais perto primeiro ou aleatório) · camadas de física. O **filtro de time** aparece nesse bloco quando o Bridges está no projeto.
- **Seletor sem campos** (Quem lançou, Alvos do evento, Ponto): **não expande**. Basta o chip.
- **Validação do chip:** seletor incompatível com o evento (por exemplo, "alvo do impacto" onde não há projétil) pinta o chip de **vermelho** com o ícone ✖ e a mensagem no tooltip; o seletor **não é bloqueado** (o designer pode estar no meio da edição).
- **Alvo padrão visível.** Ao passar o mouse no chip `→ Padrão do evento`, o tooltip diz o alvo padrão da aba **atual** ("Neste evento, o padrão é o alvo da habilidade").

### 5.3 Ações com seletor próprio de filhas

Em `ForEachTarget`, o chip do **conjunto** (sobre quem repete) fica no cabeçalho dele; as filhas têm **o seu** chip, que por padrão significa "o alvo corrente da repetição". O tooltip das filhas diz isso ("Dentro do ForEach, o padrão é o alvo corrente").

## 6. Categorias, ícones e cores

### 6.1 Categorias

As categorias são as do `[TypeCategory]` (ADR 0026), com a **raiz** do caminho como categoria visual. O DS fixa as raízes abaixo para as ações do pacote; o projeto pode usar outras (recebem o ícone e a cor "Outro").

| Categoria | Ações do pacote | Ícone (nome lógico) |
|---|---|---|
| **Fluxo** | Sequence, Parallel, Repeat, Conditional, Chance, Delayed, ForEachTarget, Wait | `flow` |
| **Efeito** | ApplyEffect, RemoveEffect, Commit | `effect` |
| **Alvo** | Targeting | `target` |
| **Mundo** | SpawnProjectile, CreateThinker (Fase 8) | `world` |
| **Apresentação** | Animation, ExecuteCue, AddCue (B-013, Bridges) | `present` |
| **Evento** | SendGameplayEvent, RaiseEvent, RaiseEventEffect (Bridges) | `event` |
| **Depuração** | DebugLog | `debug` |
| **Outro** | Tipos sem categoria ou de outra raiz | `other` |

- A ordem das categorias no popup de tipo é a da tabela (primeiro as categorias, depois os tipos, em ordem alfabética, ADR 0026).
- Os ícones usam nomes **lógicos**; os arquivos (USS/imagens) ficam em `Editor/GAS/` e são decididos na Spec. Sem ícone, a linha usa só a letra inicial da categoria numa pastilha.

### 6.2 Cores

- A cor de categoria é um **token USS** (`--spellbook-cat-flow`, `--spellbook-cat-effect`, `--spellbook-cat-target`, `--spellbook-cat-world`, `--spellbook-cat-present`, `--spellbook-cat-event`, `--spellbook-cat-debug`, `--spellbook-cat-other`), definido uma vez, com **valor para o tema claro e para o escuro** do Editor. O DS não fixa o valor hexadecimal; fixa que são **distinguíveis entre si** e com **contraste mínimo 3:1** contra o fundo da linha, nos dois temas.
- A cor aparece **só** na barra esquerda de 4 px e no ícone. **Nunca** colore o texto do resumo nem o fundo da linha inteira.
- Validação usa outras duas cores, também em token: `--spellbook-warning` e `--spellbook-error`, sempre **junto** com ícone e tooltip.

## 7. Acessibilidade e legibilidade

- **Cor nunca é a única pista:** categoria = barra + ícone + (no popup) caminho de texto; validação = ícone + texto no tooltip + contador na aba.
- **Foco visível** em toda linha, chip e botão (contorno de 2 px). Tudo que tem ícone **sem texto** tem tooltip e rótulo.
- **Teclado completo:** tudo da seção 4.1 e do chip (`Enter` no chip abre o popup; `Esc` fecha).
- **Tamanho:** o Inspector respeita o zoom do Editor; nenhum texto menor que o da fonte padrão do Inspector. O resumo trunca por reticências, nunca por corte de pixel.
- **Desempenho:** abas e listas com `ListView` virtualizado; uma lista com 50 ações abre sem engasgo (seção 10, P-DS4).

## 8. Onde as abas entram no Inspector

O `GameplayAbility` e o `GameplayEffect` ganham a ordem de seções abaixo. O DS só fixa a posição das abas: o conteúdo de cada seção anterior é do desenho do respectivo Inspector.

**`GameplayAbility`:**
1. Validação (`SpellBookEditor`, no topo, ADR 0026).
2. **Identidade:** tag, nome, ícone, descrição.
3. **Comportamento:** `AbilityBehavior`, `CommitMode`, duração do `Channel`, **Autocast** (`Permitir autocast` e `Começa ligado`; só aparece com `Instant` ou `Channel`, e a mensagem do validador cobre os outros).
4. **Ativação:** tags, custo, recarga, cargas, condições de ativação, alcance de cast.
5. **Gatilhos:** lista (cada linha segue a seção 4.2, com a opção "só com autocast" como um interruptor pequeno na linha).
6. **Eventos e ações:** a faixa de abas desta DS, **a parte maior do Inspector**.

**`GameplayEffect`:** validação · identidade · duração, pilhas e modificadores · tags e requisitos · **abas de ações** (ciclo de vida e reações).

## 9. Estados especiais

| Estado | Comportamento |
|---|---|
| Dono sem nenhuma ação | A faixa de abas aparece com todas as abas vazias e um texto único acima: "Esta habilidade ainda não faz nada. Adicione uma ação em 'Ao ativar'." |
| Tipo de ação ausente (renomeado ou removido) | Segue a ADR 0026: erro no topo do Inspector com o botão "Limpar referência" (com confirmação); a linha mostra "(Tipo não encontrado)" e **não** é apagada sozinha |
| Várias seleções | Edição de vários assets ao mesmo tempo mostra só "Edição múltipla não mostra ações" e a faixa fica oculta |
| Modo Play | Os campos seguem editáveis como no Inspector padrão; a linha da ação **em execução** pode ganhar um pequeno indicador de "rodando" (decisão da Spec, P-DS5; não é requisito) |
| Ação própria do projeto | Aparece em "Outro" (ou na categoria que o projeto declarar) com ícone genérico e resumo pelo metadado do tipo |

## 10. Perguntas em aberto (com o Desenhista)

- **P-DS1. Resumo da linha.** A linha precisa que cada tipo de ação informe verbo, objeto principal e condição. A proposta é um **método virtual no tipo da ação** (puro, sem alocação em runtime, `#if UNITY_EDITOR` ou assembly Editor) que devolve o resumo; a alternativa é o catálogo `[TypeCategory(Summary=...)]` só estático (sem valores). O Criativo prefere o método, para o resumo trazer valores ("Esperar 2 s").
- **P-DS2. Abas.** A proposta é `TabView` do UI Toolkit (Unity 6000.0). Confirmar que existe e serve na 6000.0 (ADR 0010); senão, uma barra de botões própria.
- **P-DS3. Atributo novo.** Abas e linhas resumidas são do `CustomEditor` do GAS (em `SpellBook.GAS.Editor`), sem atributo novo no Core. Se um campo precisar combinar `[TypePicker]` com outro atributo, vale o "limite conhecido" da ADR 0026 (emenda).
- **P-DS4. Desempenho do Inspector.** Confirmar que 50 ações recolhidas com resumo abrem sem engasgo, e se o resumo é cacheado até o `SerializedObject` mudar.
- **P-DS5. Indicador de "rodando" em Play.** Opcional: vale um ticket pequeno ou fica fora? (Não é requisito do PRD.)
- **P-DS6. Reordenar entre listas** (4.4) e **copiar/colar** (4.5) são requisitos de usabilidade deste DS. A Spec confirma o custo; se o Desenhista os considerar grandes, viram ticket à parte, e o DS continua a referência.

## 11. Verificação (para o `/revisao`)

- **S.** No host, uma habilidade com ações em três eventos mostra: as abas com contador e indicador de validação; cada ação recolhida legível em uma linha; o chip de alvo clicável; o bloco "Alvo" com os campos da Área; arrastar e Duplicar criando instância nova; o mesmo aspecto num efeito (CA31 do PRD B-012).
- **E.** O Inspector está em UI Toolkit, sem `OnGUI` fora do fallback da ADR 0026 e sem Odin; as cores vêm de tokens USS com valor para tema claro e escuro.
- **S.** A tab do `Channel` só aparece com o comportamento `Channel`; o autocast só aparece com `Instant` ou `Channel`.
- **S.** Tipo de ação ausente mostra o erro no topo e a linha "(Tipo não encontrado)" sem apagar dados.

## 12. Histórico

| Data | Mudança |
|---|---|
| 2026-10-31 | Primeira versão, a pedido do usuário (PRD B-012, P5), antes da Spec, para entrar no gate único |
| 2026-10-31 | Gate do Lote D: PlayCue e SpawnVFXEffect saem das categorias; entram ExecuteCue e AddCue (Apresentação); cues só por ações |
