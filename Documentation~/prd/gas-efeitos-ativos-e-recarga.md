# PRD: Efeitos ativos e recarga controláveis
Backlog: B-003 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção C (itens 3, 11 e 12), seção E (Fase 1) e seção H.7 (GAS Debugger v1).
Depende do Lote A: **B-002** (correções do núcleo: renovação com a duração aplicada, recarga normalizada, casamento hierárquico de tags, remoção idempotente) e **B-006** (`GameplayTagContainer`, `GameplayTagQuery`, `GetDefinition` público). Este PRD usa o que eles entregam e **não reescreve o escopo deles** (ver seção 6).
Este PRD não tem foco em nenhum jogo. As menções a jogos no plano são histórico.

---

## 1. Problema (contexto)

Hoje o `AbilitySystemComponent` (ASC) aplica efeitos e controla recargas, mas esconde o estado. Quem usa o pacote só enxerga o que o ASC decidiu expor:

- **Não há como listar os efeitos ativos.** A lista é privada. Quem precisa de uma lista de buffs, de uma barra de recarga ou de um painel de debug lê o campo por reflexão ou mantém um registro paralelo, que fica fora de sincronia com o ASC.
- **Não há busca nem remoção por tag.** "Remova todos os efeitos de veneno" e "este alvo está sob algum atordoamento?" exigem laço manual sobre uma lista que o desenvolvedor nem consegue ler.
- **A duração de uma instância é imutável.** Não há como estender, reduzir ou reiniciar um efeito ativo (por exemplo, "o próximo acerto reduz 1 s da queimadura"). Quem precisa disso chama `Update` na instância, que não atualiza tags nem modificadores com segurança.
- **Não há eventos.** Para saber que um efeito entrou, saiu ou mudou de pilhas, a UI e o jogo fazem polling.
- **A recarga é uma caixa preta.** Não há "reduzir a recarga em 2 s", "zerar a recarga" ou "reiniciar a recarga". O piso de 0,1 s e o teto da redução (100 %) estão fixos no código.
- **Consultar atributos é verboso.** Ler um atributo que pode não existir passa por `TryGetState` ou por `GetValue`, que registra aviso no Console e devolve 0.
- **A UI não sabe quando uma habilidade foi concedida ou retirada.** Uma barra de habilidades precisa de um evento para se montar e desmontar.
- **Falta uma janela para olhar tudo isso em jogo.** Atributos com modificadores por fonte, tags com contagem, efeitos, habilidades e recarga só aparecem com `Debug.Log`.

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, sem reflexão e sem registro paralelo:

1. ler a lista de efeitos ativos de um ASC e buscar por tag ou por asset;
2. remover efeitos por tag ou por asset, e saber quantas pilhas saíram;
3. estender, reduzir e reiniciar a duração de um efeito ativo, e mudar as pilhas dele, mantendo tags e modificadores consistentes;
4. reagir a "efeito aplicado", "efeito removido (com o motivo)" e "pilhas mudaram" por eventos C# do ASC;
5. reduzir, zerar e reiniciar a recarga de uma habilidade por código;
6. configurar o piso e o teto da redução de recarga por asset, em vez de valores fixos;
7. consultar atributos sem aviso quando eles podem não existir;
8. receber eventos quando uma habilidade é concedida ou retirada;
9. abrir o **GAS Debugger v1** em jogo e ver atributos, tags, efeitos, habilidades e recarga.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Desenvolvedor de gameplay** | API pública para ler e controlar efeitos e recargas, sem reflexão; eventos em vez de polling; sem alocação por consulta |
| **Designer técnico** | Configurar a política de redução de recarga num asset; ver no Debugger o que está ativo e por quê |
| **Desenvolvedor de UI** (consumidor dos itens B-016 a B-018) | Lista de efeitos, duração, pilhas, recarga e concessão de habilidade, tudo por leitura e evento do ASC |
| **Mantenedor do pacote** | Uma única fonte de verdade para efeitos ativos, que a UI, o Debugger e o relay (B-004) leem |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, lista e busca:** o ASC tem veneno (tag `Status.Veneno`) e lentidão (tag `Status.Lentidao`). `ActiveEffects` tem 2 itens. Buscar por `Status` devolve os 2 (hierarquia); buscar por `Status.Veneno` devolve 1; buscar por tag sem efeitos devolve 0.
- **C2, remover por tag:** com 3 pilhas de veneno, remover por `Status.Veneno` devolve 3 (pilhas removidas), a tag some do ASC e os modificadores são devolvidos.
- **C3, estender:** um efeito com 5 s restantes, `ExtendDuration(2)` leva o restante a 7 s; `ReduceDuration(10)` o leva a 0 e o efeito expira normalmente (evento de removido com motivo "expirou").
- **C4, reiniciar:** um efeito com 1 s restantes de 8 s aplicados volta a 8 s com `RestartDuration`.
- **C5, pilhas:** `SetStacks(3)` num efeito com `MaxStacks = 5` leva a 3; `SetStacks(9)` limita em 5; `RemoveStacks(1)` com 1 pilha remove o efeito.
- **C6, eventos:** aplicar um efeito dispara `OnEffectApplied` uma vez; empilhar dispara `OnEffectStacksChanged(instância, 1, 2)`; remover dispara `OnEffectRemoved` com o motivo correto (expirou, removido por código, removido por tag, substituído).
- **C7, recarga:** uma habilidade com 8 s de recarga. `ReduceCooldown(ability, 3)` leva a 5 s; `ResetCooldown` leva a 0 e a habilidade volta a ser ativável; `RestartCooldown` volta ao tempo aplicado cheio.
- **C8, política de redução:** com `MinDuration = 1 s` e `MaxReduction = 50 %`, uma recarga de 8 s com redução de 90 % dura 4 s (teto da redução). Com a redução em 99 % e `MaxReduction = 100 %`, o piso de 1 s vale.
- **C9, leitura sem aviso:** `TryGetValue` de um atributo ausente devolve falso, sem aviso no Console; `GetValueOrDefault(def, 7)` devolve 7.
- **C10, concessão:** `GiveAbility` dispara `OnAbilityGranted(spec)`; `RemoveAbility` dispara `OnAbilityRemoved(spec)`.
- **C11, Debugger (S):** no host, em Play, a janela mostra o ASC selecionado com atributos (base, atual, modificadores por fonte), tags com contagem, efeitos com tempo restante e pilhas, habilidades com recarga.
- **C12, sem lixo:** consultar a lista, buscar por tag em lista reutilizada e ler o tempo de recarga 10 000 vezes não aloca memória gerenciada.

## 4. Requisitos funcionais

### 4.1 Efeitos ativos: leitura

R1. **Lista pública.** O ASC expõe os efeitos ativos como uma lista somente leitura de `ActiveEffectInstance`. A lista é a mesma usada internamente (sem cópia) e a ordem é a de aplicação.

R2. **Dados da instância.** `ActiveEffectInstance` expõe, de forma pública e somente leitura: o `GameplayEffect` de origem, o ASC de origem (quem aplicou), o ASC alvo, o tempo restante, a **duração aplicada** (o total com que a instância foi aplicada, incluindo qualquer redução ou substituição; é o dado guardado pelo B-002, R6 e R7) e as pilhas atuais.

R3. **Busca.** O ASC oferece busca de efeitos ativos por tag (com a regra hierárquica de `GameplayTag.Matches`; tag inválida não casa com nada, B-006 R2) e por `GameplayEffect`. A busca preenche uma lista fornecida por quem chama e devolve a quantidade, **sem alocar**. Existe também a pergunta simples "há algum efeito ativo com esta tag?" sem alocação.

R4. **Busca por conjunto de tags.** Por consequência do Lote A, a busca por tag também aceita um `GameplayTagContainer` ("alguma tag do conjunto") e `GameplayTagQuery` ("a consulta casa com as tags concedidas pelo efeito"). É aqui, e só aqui, que o ASC adota os tipos do B-006 neste item; o `HasMatching` genérico no ASC continua fora (B-006, P2).

### 4.2 Efeitos ativos: controle

R5. **Remoção por tag e por asset.** O ASC remove todos os efeitos ativos que casem com uma tag, ou todos os de um `GameplayEffect`, e devolve o **total de pilhas removidas**. Cada remoção usa o mesmo caminho de `RemoveEffect` (limpeza uma única vez, B-002 R5). Remover por tag com nada ativo devolve 0, sem exceção.

R6. **Mutação de duração pelo ASC ou pela instância, sempre consistente.** `ExtendDuration(segundos)`, `ReduceDuration(segundos)` e `RestartDuration()` alteram o tempo restante de uma instância ativa. Segundos negativos ou NaN são rejeitados (sem efeito, com aviso de desenvolvimento). `ReduceDuration` até zero ou menos expira o efeito pelo caminho normal de expiração. `RestartDuration` volta à duração aplicada. Efeitos de duração infinita ignoram as três operações. A duração aplicada **não muda** com Extend ou Reduce: ela só muda quando o efeito é reaplicado (B-002, R6a).

R7. **Mutação de pilhas.** `SetStacks(n)` e `RemoveStacks(n)` mudam as pilhas respeitando `MaxStacks` (limita em vez de falhar). Chegar a 0 pilhas remove o efeito. A mudança recalcula tags e modificadores afetados, e dispara o evento de pilhas.

R8. **`Update` público da instância.** O método `ActiveEffectInstance.Update` deixa de ser a forma de avançar o tempo de fora do ASC. Como o tempo vem do `ITimeSource` (ADR 0014) e do `ASC.Tick`, o método fica **interno** (pela decisão I1 não há `[Obsolete]`).

### 4.3 Eventos

R9. **Eventos C# do ASC**, por instância, sem passar pelo barramento global (ADR 0016):
- `OnEffectApplied(ActiveEffectInstance)`: dispara quando um efeito com duração ou infinito passa a existir. Efeitos instantâneos não geram instância e **não** disparam este evento (o resultado de aplicação é do B-004).
- `OnEffectRemoved(ActiveEffectInstance, EffectRemovalReason)`: com `EffectRemovalReason` em `Expired`, `Removed` (por código, `RemoveEffect`), `RemovedByTag`, `StacksDepleted` (pilhas chegaram a 0) e `Replaced` (substituído por uma reaplicação, se a Spec decidir que isso remove a instância antiga; ver P3).
- `OnEffectStacksChanged(ActiveEffectInstance, int pilhasAntes, int pilhasDepois)`.

R10. **Ordem e segurança.** Quando um efeito é removido, o evento dispara **depois** da limpeza (tags e modificadores já devolvidos), para o ouvinte ver o estado final do ASC. Um ouvinte que remove ou aplica efeitos dentro do evento não corrompe a iteração. Remoção repetida não dispara o evento de novo (B-002, R5).

R11. **Concessão de habilidade.** `OnAbilityGranted(AbilitySpec)` e `OnAbilityRemoved(AbilitySpec)` no ASC, disparados por `GiveAbility` e `RemoveAbility`. Conceder uma habilidade já concedida não dispara de novo. Estes eventos substituem o `AbilitySlotUpdateEvent` do barramento antigo (ADR 0016).

### 4.4 Recarga

R12. **Controle por código.** O ASC oferece, por habilidade: `ReduceCooldown(ability, segundos)`, `ResetCooldown(ability)` (vai a 0 e a habilidade volta a ser ativável) e `RestartCooldown(ability)` (volta ao tempo de recarga aplicado cheio). Todas respeitam o casamento hierárquico do B-002 R8, devolvem se algo mudou e são seguras com a habilidade pronta (nada acontece).

R13. **Evento de recarga.** O ASC avisa quando a recarga de uma habilidade começa, termina e muda por controle de código, para a UI não depender de polling. A forma exata (um evento com o motivo, ou três eventos) é do Arquiteto (P4).

R14. **`CooldownReductionPolicy`.** Um asset (`ScriptableObject`) define como a redução de recarga vira duração: `MinDuration` (piso em segundos), `MaxReduction` (teto da redução, de 0 a 1) e o modo (`Percent`: o atributo é uma porcentagem; `Haste`: o atributo é um multiplicador de velocidade). O padrão, sem asset atribuído, reproduz o comportamento de hoje (piso 0,1 s, redução limitada a 100 %, modo `Percent`). A habilidade escolhe a política por campo opcional no `GameplayAbility`; sem política, vale o padrão. A política tem `[HelpURL]` e validação (piso negativo, teto fora de 0 a 1).

### 4.5 Leitura de atributos

R15. **Consulta segura.** O `AttributeSet` oferece `TryGetValue(def, out valor)`, `GetValueOrDefault(def, padrão)`, `HasAttribute(def)` e `GetBaseValue`, sem aviso no Console para atributo ausente. O ASC tem atalhos equivalentes. `GetValue` continua como está (avisa e devolve 0). Nenhum deles aloca.

### 4.6 GAS Debugger v1

R16. **Janela de debug em jogo.** Uma janela de Editor em UI Toolkit (ADR 0026) mostra, para o ASC escolhido (por seleção do objeto ou por seletor na janela), em Play:
- **Atributos:** base, valor atual, mínimo e máximo, e os modificadores com a fonte (nome do efeito e operação);
- **Tags:** cada tag com a contagem de referências;
- **Efeitos:** asset, origem, tempo restante, duração aplicada, pilhas;
- **Habilidades:** nível, ativa ou não, recarga restante e normalizada.

R17. **Ações no Debugger.** Botões para aplicar um efeito (escolhido por seletor de asset), remover um efeito ativo, conceder e retirar uma habilidade, e zerar a recarga. Um log curto dos últimos eventos do ASC (R9, R11), com limite de itens.

R18. **Só leitura do estado real.** O Debugger usa a API pública deste PRD (R1, R2, R12, R15). Ele **não** usa reflexão e **não** altera o comportamento do ASC. Fora do Play, mostra uma mensagem de "entre em Play". Ele não existe em build de player.

### 4.7 Validação e documentação

R19. **Validação.** `CooldownReductionPolicy` implementa `IValidatable` (ADR 0026) e a janela do validador do projeto a lista, com o objeto clicável.

R20. **Manual.** A página do manual do GAS sobre efeitos descreve R1 a R12, e uma página nova descreve o Debugger. As classes e assets novos têm `[HelpURL]` (decisão I15). Sem as páginas o ticket não passa na revisão.

R21. **CHANGELOG.** A entrada da pré-release cita: `ActiveEffectInstance.Update` interno, eventos novos, `CooldownReductionPolicy`.

## 5. Requisitos não funcionais

N1. **Alocação:** consultas, buscas em lista reutilizada, leitura de recarga e `TryGetValue` não alocam depois do aquecimento (C12). Disparar eventos sem ouvintes não aloca.
N2. **Determinismo:** os testes usam `ManualTimeSource`, nunca o relógio real (ADR 0014).
N3. **Módulos:** tudo em `SpellBook.GAS`, que referencia só Core, Tags e Time. Os eventos são C# no ASC; **o GAS não referencia `SpellBook.Events`** (ADR 0012, 0016). O Debugger fica em `SpellBook.GAS.Editor`.
N4. **Sem regressão:** a suíte do GAS existente (incluindo o que o B-002 entrega) passa sem edição.
N5. **Serialização:** o campo novo de política em `GameplayAbility` usa serialização nativa; assets existentes abrem sem perda de dados (campo nulo = comportamento de hoje).
N6. **Testes:** efeitos e habilidades de teste por factory (`GasFactory`), sem assets fixos.

## 6. Fora do escopo

- **Já entregue pelo Lote A (B-002), apenas usado aqui:** remoção idempotente, renovação com a duração aplicada, recarga normalizada, casamento hierárquico de tags na recarga e reaplicação sem `CanStack`. Este PRD só **expõe** a duração aplicada (R2).
- **`GameplayTagContainer` e `GameplayTagQuery` (B-006):** só são consumidos, no R4.
- `TryApplyEffect`, `EffectApplicationResult`, `OnEffectBlocked`, relay e canais do GAS: **B-004**. Efeitos instantâneos não disparam `OnEffectApplied` por isso.
- Metadados `IsBuff`/`IsDebuff`/`IsHidden`/`IsPurgable` e purge por categoria: **B-005**. A busca por categoria entra depois.
- Magnitude ao vivo, pilhas que multiplicam o valor, nível do efeito: **B-007**. `SetStacks` aqui muda só a contagem e dispara o recálculo já existente.
- Cargas de habilidade e recarga sequencial: B-009.
- Widgets e fontes de UI que consomem estes dados (`ActiveEffectsSource`, slot de habilidade): B-015 a B-018.
- Overlay de debug em build, simulador de efeito e tabela de balanceamento: B-019.
- Sobrecarga `HasMatching(GameplayTagQuery)` genérica no ASC (B-006, P2): só a **busca de efeitos** por conjunto e consulta (R4) entra aqui.
- Salvar e carregar efeitos ativos; replicação em rede.

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

**Leitura**
- CA1 (T). Cenário C1: `ActiveEffects` lista os efeitos na ordem de aplicação; a busca por tag pai devolve os filhos, por tag exata devolve um, por tag sem efeitos devolve 0.
- CA2 (T). Uma tag inválida (sem definição) devolve 0 na busca e `false` na pergunta "há efeito com esta tag?", sem exceção.
- CA3 (T). A busca por `GameplayTagContainer` e por `GameplayTagQuery` devolve os efeitos esperados (alguma tag; consulta com todas/alguma/nenhuma).
- CA4 (T). `ActiveEffectInstance` expõe efeito, origem, alvo, tempo restante, duração aplicada e pilhas; a duração aplicada é a reduzida quando houve redução de recarga (continuidade do B-002).

**Controle de efeitos**
- CA5 (T). Cenário C2: remover por tag devolve as pilhas removidas, devolve tags e modificadores, e uma segunda chamada devolve 0.
- CA6 (T). Remover por `GameplayEffect` remove só as instâncias daquele asset.
- CA7 (T). Cenário C3: estender, reduzir e reduzir além do restante; o último expira com motivo `Expired`.
- CA8 (T). Segundos negativos ou NaN em Extend e Reduce não alteram o efeito.
- CA9 (T). Cenário C4: `RestartDuration` volta à duração aplicada (e não à duração do asset quando as duas diferem).
- CA10 (T). Efeito infinito ignora Extend, Reduce e Restart.
- CA11 (T). Cenário C5: `SetStacks` limita em `MaxStacks`; `RemoveStacks` até zero remove o efeito com motivo `StacksDepleted`; tags e modificadores acompanham a contagem.

**Eventos**
- CA12 (T). Cenário C6: `OnEffectApplied`, `OnEffectStacksChanged` (valores antes e depois) e `OnEffectRemoved` (um teste por motivo) disparam exatamente uma vez cada.
- CA13 (T). Dentro de `OnEffectRemoved`, as tags do efeito já foram devolvidas (`HasTag` é falso).
- CA14 (T). Um ouvinte que remove outro efeito dentro do evento não lança exceção nem pula eventos.
- CA15 (T). Efeito instantâneo não dispara `OnEffectApplied`.
- CA16 (T). Cenário C10: `OnAbilityGranted` e `OnAbilityRemoved` disparam uma vez; conceder duas vezes a mesma habilidade dispara uma só.

**Recarga**
- CA17 (T). Cenário C7: `ReduceCooldown`, `ResetCooldown` e `RestartCooldown` dão os tempos esperados e a habilidade volta a ser ativável após `ResetCooldown`.
- CA18 (T). Os três métodos com a habilidade pronta devolvem `false` e não lançam exceção.
- CA19 (T). Os três respeitam a recarga concedida por tag filha (B-002, CA16).
- CA20 (T). Cenário C8: `CooldownReductionPolicy` aplica piso e teto; sem política, o resultado é idêntico ao de hoje (piso 0,1 s, `Clamp01`).
- CA21 (T). A política com piso negativo ou teto fora de 0 a 1 é apontada pelo validador, com o objeto clicável.
- CA22 (T). O evento de recarga (R13) dispara ao começar, terminar e ao mudar por controle de código.

**Atributos**
- CA23 (T). Cenário C9: `TryGetValue`, `GetValueOrDefault`, `HasAttribute` e `GetBaseValue` para atributo existente e ausente; nenhum registra aviso no Console.

**Debugger**
- CA24 (S). Cenário C11: a janela abre pelo menu, mostra os quatro blocos do R16 para o ASC selecionado e atualiza durante o Play.
- CA25 (S). Os botões do R17 aplicam, removem, concedem e zeram a recarga, e cada ação aparece no log.
- CA26 (E). O código do Debugger não usa reflexão nem `System.Reflection` para ler o ASC (revisão estrutural) e não é compilado em build de player.

**Desempenho, módulos e documentação**
- CA27 (T). Cenário C12: 10 000 chamadas de leitura de lista, busca em lista reutilizada, tempo de recarga e `TryGetValue` alocam 0 bytes gerenciados depois do aquecimento.
- CA28 (T). O teste de assemblies da 0-M continua passando: o GAS não referencia o Events.
- CA29 (E). O CHANGELOG cita os itens do R21 e o manual do GAS descreve R1 a R12, mais a página do Debugger; os tipos novos têm `[HelpURL]`.
- CA30 (T). A suíte do GAS existente passa sem edição de teste antigo (N4).

## 8. Perguntas em aberto

### Com o usuário

- **P1. Tamanho do item.** *Respondida (usuário, 2026-10-31): o PRD fica inteiro; a Spec divide em tickets, com o GAS Debugger como último ticket, para a UI-2 (B-016) já poder usar R1 a R15 antes dele.*
- **P2. Remoção de efeito por tag e a "pureza" do ASC.** *Respondida (usuário, 2026-10-31): aceita. O ASC só oferece as operações do R5 por tag e por asset; "remover por categoria" (buff, debuff, purgável) fica para depois do B-005.*

### Com o Arquiteto (decisão técnica da Spec)

- **P3. `Replaced`.** Reaplicar sem `CanStack` **renova** a instância (B-002, R6a), então "substituído" talvez nunca ocorra. Confirmar se `EffectRemovalReason.Replaced` é necessário ou se o enum fica sem ele.
- **P4. Forma do evento de recarga (R13).** Um evento com o motivo ou três eventos; e se a recarga continua sendo "efeito de recarga ativo" (então já está coberta por R9) ou ganha estado próprio.
- **P5. Onde mora a duração aplicada** na instância (continuação da P3 do B-002) e como as mutações de R6 e R7 passam pelo ASC para manter tags e modificadores consistentes quando chamadas pela instância.
- **P6. Ordem entre eventos** quando uma mutação dispara vários (por exemplo, `RemoveStacks` até zero: pilhas mudaram e efeito removido).
- **P7. Como o Debugger lê os modificadores por fonte** (R16) sem API nova pesada: o `AttributeState` precisa expor a fonte de cada modificador de forma pública e somente leitura?

## 9. Diário de decisões e ajustes

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Origem | Decisão ou observação | Efeito neste PRD | Status |
|---|---|---|---|---|
| 2026-10-31 | Controlador | PRD escrito para o Lote B do backlog, a partir do texto de B-003, do plano e das ADRs 0012, 0014 e 0016 | Todo o documento | Rascunho |
| 2026-10-31 | Controlador | Lote A vem antes: B-002 entrega duração aplicada, remoção idempotente e casamento de tags na recarga; B-006 entrega `GameplayTagContainer`/`GameplayTagQuery`. Este PRD só os consome | R2, R4, R12, seção 6 | Aplicado |
| 2026-10-31 | usuário (via Controlador) | B-006 P2: sem `HasMatching` no ASC agora; fica para B-003/B-009 | R4 (só busca de efeitos, não `HasMatching`) | Aplicado |
| 2026-10-29 | usuário | I23: os eventos C# do ASC continuam; o relay vem no B-004 | R9, seção 6 | Aplicado |
| 2026-10-29 | usuário | I1: 3.0 com quebras livres, sem fachada de compatibilidade | R8, R21 | Aplicado |
| 2026-10-31 | usuário (via Controlador) | P1: PRD inteiro; a Spec divide em tickets e o GAS Debugger (R16 a R18) é o último ticket | Seção 8 (P1), R16 a R18 | Aplicado |
| 2026-10-31 | usuário (via Controlador) | P2: remoção por categoria fica para depois do B-005; o ASC só remove por tag e por asset | R5, seção 6 (sem mudança de texto: já descrevia isso) | Aplicado |
