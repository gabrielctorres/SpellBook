# PRD: UI-3, tooltip e descrição estruturada na tela
Backlog: B-017 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção H.5 (tooltip e `TooltipTrigger`), seção H.6 (passos 6 e 9), seção H.7 (prévia de descrição) e seção E (UI-3). ADRs [0017](../adr/0017-ui-em-tres-camadas.md) (UI em três camadas), [0023](../adr/0023-texto-estruturado-e-itextprovider.md) (texto estruturado e `ITextProvider`), [0012](../adr/0012-mapa-de-modulos-e-asmdefs.md) (módulos), [0013](../adr/0013-serializereference-como-costura-entre-modulos.md) (fontes) e [0026](../adr/0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md) (editor).
Depende de: **B-015** (a base: `IView<T>`, `UISource<T>`, binder, `ActorResolver`, tema, widgets uGUI), **B-008** (`EffectDescriber`, elementos, `DescriptionContext`, `ITextProvider`, que produzem as linhas que o tooltip mostra) e **B-005** (formato, papel e cor do atributo, **e `DisplayName` e `Icon` do `GameplayEffect`**, ticket AR7: decisão do usuário, 2026-10-31). Para o tooltip de efeito ativo, o ícone de buff na lista vem de **B-018** (este item só entrega o gancho); o nome e o ícone **do efeito** vêm do B-005. Para o tooltip de habilidade em slot: **B-016**. Este PRD usa o que eles entregam e não reescreve o escopo deles (seção 6).
**Sample:** o HUD do Sample (S4) pode ganhar tooltip depois, em uGUI e com o Input Manager legado (decisão do usuário, 2026-10-31); este item não altera o Sample.
Este PRD não tem foco em nenhum jogo. O `TooltipViews.Ability` do jogo no plano (procura `UseChargeAction` e fixa pt-BR) é histórico.

---

## 1. Problema (contexto)

O tooltip é onde o jogador lê o que a habilidade faz, e hoje:

- **O texto é uma string pronta.** O jogo do plano monta o tooltip procurando ações no asset e fixa a cultura pt-BR; trocar o idioma ou o balanceamento exige código.
- **Sem dado entre o GAS e a tela.** Não existe um modelo (título, categoria, linhas, custo, recarga, cargas) que uma view desenhe sem conhecer o GAS.
- **Sem gatilho padrão.** Quem passa o mouse ou foca um item e abre o tooltip é código do jogo, repetido por widget (slot, ícone de buff, atributo).
- **Sem prévia.** O designer só vê o tooltip final no Play; não vê como fica no nível 3 sem jogar.
- **Cor e ícone na hora errada.** Hoje o texto sai com cor embutida; a view não escolhe estilo (ADR 0023).

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, **pelo Inspector e sem script** no caso comum:

1. mostrar um **tooltip** de **habilidade**, de **efeito** e de **atributo** a partir do asset (título, categoria, ícone, linhas de descrição, custo, recarga, duração e cargas), no **nível** certo;
2. ligar o tooltip a qualquer widget com um **`TooltipTrigger`** (mouse sobre o item, foco por navegação, ou chamada por código);
3. ver o texto **na cultura do jogador**, com **cor e ícone escolhidos pela view** (papel `Beneficial`, `Harmful`, `Neutral`, `Value` do `TextRoleTheme`), sem rich text vindo do GAS;
4. **ver o tooltip em edição** (prévia por nível), sem Play;
5. usar uma **view própria** (`IView<TooltipModel>`) sem tocar na fonte, e um **provedor de texto próprio**;
6. ter só **um tooltip aberto** por vez, que **acompanha** o item (posição, nível, valores vivos) e fecha ao sair.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Designer técnico / de UI** | Pôr um `TooltipTrigger` num slot ou ícone, escolher a fonte, ver a prévia do tooltip por nível |
| **Desenvolvedor de gameplay** | Abrir e fechar o tooltip por código; fonte própria de tooltip (um item, um NPC) por subclasse |
| **Quem localiza** | Todo texto do tooltip por chave, na cultura injetada |
| **Mantenedor do pacote** | A view recebe um modelo de linhas e papéis, sem conhecer o GAS |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, tooltip de habilidade:** `AbilityTooltipSource` para `Fireball` no nível 3 produz um `TooltipModel` com título (por chave), ícone, categoria, as linhas dos elementos de descrição (B-008) no nível 3, e as linhas de custo, recarga e cargas. Sem alterar o estado do ASC.
- **C2, valores vivos:** com o ASC do jogador, a recarga mostrada é a real (após redução, B-008 P3) e o custo o do nível atual; quando o atributo de redução muda com o tooltip aberto, o texto **atualiza**.
- **C3, nível:** o mesmo asset no nível 1 e no nível 3 produz linhas com valores diferentes (tabela do B-007). Mudar o nível do `AbilitySpec` com o tooltip aberto atualiza as linhas.
- **C4, tooltip de efeito:** `EffectTooltipSource` para um efeito ativo mostra título, ícone, linhas de modificador (`EffectDescriber`), pilhas atuais e **tempo restante**; o tempo restante corre por tick do driver só enquanto o tooltip está aberto.
- **C5, tooltip de atributo:** `AttributeTooltipSource` mostra o nome do atributo, o valor atual com o formato dele e, quando houver, a decomposição (base e modificadores) como linhas (P3).
- **C6, trigger por ponteiro e foco:** `TooltipTrigger` abre o tooltip ao ponteiro entrar no item (com atraso configurável) e fecha ao sair; abre também por foco de navegação (teclado, controle) e fecha ao perder o foco. A chamada por código `Show` e `Hide` funciona igual.
- **C7, um por vez:** abrir um tooltip fecha o anterior; o mesmo trigger reabrir não duplica; destruir ou desativar o item fecha o tooltip sem exceção.
- **C8, posição:** o tooltip aparece perto do item e **não sai da tela** (inverte de lado ao encostar na borda); a regra de posição é da view (P4).
- **C9, cultura e provedor:** com a cultura `en-US` o número decimal sai com ponto; um `ITextProvider` de teste troca as chaves de rótulo; **nenhuma** saída do GAS tem rich text; a cor sai do papel da linha, escolhida pela view e pelo `TextRoleTheme`.
- **C10, dono:** o tooltip lê o ASC pelo `ActorResolver` do trigger (âncora do jogador); dono não encontrado mostra só o que o asset sabe (sem valores vivos), sem exceção.
- **C11, prévia (S):** no host, o `TooltipTrigger`/fonte tem uma prévia do tooltip por nível, com um controle de nível, sem Play.
- **C12, view própria:** uma `IView<TooltipModel>` do teste recebe o modelo sem conhecer o GAS.
- **C13, sem lixo em regime:** com o tooltip **aberto**, 10 000 atualizações de valor vivo (recarga, tempo restante) alocam 0 bytes gerenciados; **abrir** o tooltip pode alocar uma vez, para montar o texto final (P4).
- **C14, Inspector (S):** no host, a fonte do tooltip aparece com seletor de tipo, resumo de uma linha e validação no topo.

## 4. Requisitos funcionais

### 4.1 Modelo

R1. **`TooltipModel`** (struct ou struct com buffer de linhas reaproveitado, em `SpellBook.UI`): **título** (por chave e texto padrão), **subtítulo ou categoria**, **ícone**, **linhas** (lista fornecida pelo binder, reaproveitada; cada linha traz rótulo, valor, papel e atributo, como as `DescriptionLine` do B-008), **bloco de custo e recarga** (linhas próprias do B-008), **pilhas** e **tempo restante** (efeito), e **rodapé** opcional (por chave). Não guarda cor nem rich text (ADR 0023). A forma exata do buffer de linhas é do Arquiteto (P1 do Arquiteto).

R2. **Cultura e provedor.** A cultura e o `ITextProvider` chegam por **configuração do binder ou do tema** (ADR 0023, item 2), nunca fixos no runtime. Sem provedor, vale o texto padrão do campo.

### 4.2 Fontes (`SpellBook.GAS.UI`)

R3. **`AbilityTooltipSource`.** Para uma habilidade (e o ASC dono, opcional), produz o `TooltipModel` usando os elementos de descrição e o `EffectDescriber` do B-008, no **nível** do `AbilitySpec` do ASC dono (ou o nível configurado, quando não há dono, P2). Atualiza **por evento** (mudança de nível, de atributo que afeta custo ou recarga, de cargas) e só pede tick enquanto o tooltip está aberto e há valor que corre no tempo (ADR 0017, item 1). Não altera o ASC.

R4. **`EffectTooltipSource`.** Para um efeito (asset ou instância ativa), produz título, ícone, linhas de modificador, **pilhas** e **tempo restante** (instância ativa). Os campos de nome e ícone do **efeito** vêm do `GameplayEffect`, entregues pelo **B-005** (ticket AR7); este item só os lê, sem tipo próprio nem cópia.

R5. **`AttributeTooltipSource`.** Para um atributo do ASC, produz nome, valor atual formatado pelo `AttributeDisplay` (B-005) e as linhas "base" e "modificadores por fonte" quando a decomposição é pedida (P3).

R6. **Fontes próprias.** O ponto de extensão `UISource<TooltipModel>` é `[SerializeReference]` (ADR 0013): o projeto cria a fonte de um item, de um NPC ou de um objeto do mundo por subclasse, sem tocar no módulo.

### 4.3 Gatilho

R7. **`TooltipTrigger`.** Um componente que, num item (slot, ícone, texto de atributo, qualquer objeto da UI), escolhe a **fonte**, o **`ActorResolver`** (B-015) e o **modo de abertura**: ponteiro (com atraso), foco ou só por código. Chama o `TooltipBinder` para abrir e fechar. Sem `Find` e sem singleton: o binder de tooltip chega por campo serializado ou por `RuntimeAnchor` (B-015). O modo ponteiro precisa do `EventSystem` da uGUI; o modo foco usa a navegação da uGUI (P5 do Arquiteto).

R8. **`TooltipBinder`.** Um componente que mantém **um tooltip aberto por vez** (R9), liga a fonte do trigger ativo, entrega o `TooltipModel` à view, **atualiza** enquanto aberto e fecha ao sair, ao desativar ou ao destruir o item. Não mantém referência forte a item destruído.

R9. **Um por vez e acompanhamento.** Abrir fecha o anterior. O tooltip **acompanha** o item enquanto aberto: o texto vivo (recarga, tempo restante, pilhas) atualiza e o tooltip se reposiciona se o item se move.

### 4.4 View padrão (`SpellBook.UI.UGUI`)

R10. **View do tooltip (uGUI + TextMeshPro).** O pacote entrega uma view e um **prefab**: painel com título, ícone, linhas (rótulo e valor com a cor do papel, lida do `TextRoleTheme` pelo `UITheme`), bloco de custo e recarga, e rodapé. Posição perto do item, com inversão de borda. É de protótipo e Sample (ADR 0017, consequências); não cobre todo estilo visual.

R11. **Montagem do texto.** A view escolhe cor, ícone e rich text **na ponta**, a partir do papel e do `TextRoleTheme`; nenhum rich text vem do GAS. O texto final é montado só quando o tooltip **abre** ou o valor vivo muda; mantê-lo aberto sem mudança não aloca.

### 4.5 Prévia em edição

R12. **Prévia.** A fonte de tooltip tem um `PreviewModel` e, no Inspector do `TooltipTrigger` e do asset de habilidade e efeito, uma **prévia do tooltip por nível** com controle de nível, sem Play (H.7 do plano; reaproveita a prévia de descrição de B-008). A prévia roda em UI Toolkit no Editor (ADR 0026), com o texto simples e o papel como cor de referência; a view uGUI não é instanciada no Editor.

### 4.6 Editor, validação e documentação

R13. **Inspector** (UI Toolkit, ADR 0026, `SpellBookEditor`): `TooltipTrigger` com fonte (seletor de tipo), resolvedor, modo de abertura e atraso; `TooltipBinder` com a view e o tema; resumo de uma linha ("Tooltip · Habilidade Fireball · ponteiro, 0,3 s").

R14. **Validação** (`IValidatable`): trigger sem fonte ou sem binder alcançável; fonte de habilidade sem habilidade; `TooltipBinder` sem view; binder duplicado na mesma cena (aviso); atraso negativo; modo ponteiro sem `EventSystem` na cena (aviso, em cena aberta); item sem `Graphic` para receber o ponteiro (aviso). Mensagens clicáveis.

R15. **Documentação.** Páginas **Tooltip** (fonte, trigger, binder, view, prévia, como criar uma fonte própria e uma view própria) e a seção "Tooltip em slot e em buff" nas páginas de B-016 e B-018 (após esses itens). Os tipos novos têm `[HelpURL]` (decisão I15). O CHANGELOG cita `TooltipModel`, as três fontes, `TooltipTrigger` e `TooltipBinder`.

### 4.7 Dependência com o Sample

R16. **O Sample pode ganhar tooltip depois.** O Sample Sandbox (B-032) **não** é alterado por este item. Quando o HUD (S4) existir (depois de B-015 e B-016, em uGUI e com o Input Manager legado, decisão do usuário, 2026-10-31), o tooltip é um complemento dele, com PRD ou emenda do Sample.

## 5. Requisitos não funcionais

N1. **Alocação:** atualizar valor vivo com o tooltip aberto não aloca depois do aquecimento (C13). **Abrir** o tooltip pode alocar uma vez (a string final, P4); isso é do caminho de abertura, não de regime.
N2. **Módulos (ADR 0012, 0017):** `TooltipModel`, binder e contratos em `SpellBook.UI`; fontes em `SpellBook.GAS.UI` (GAS, UI, Tags, Time; **não** Bridges); view e trigger de ponteiro em `SpellBook.UI.UGUI`. A view não conhece o GAS. O GAS não referencia UI. O teste de assemblies cobre.
N3. **Sem texto fixo:** nenhuma cultura, cor nem rich text vindo do GAS; todo texto por chave (ADR 0023).
N4. **Tempo:** só `ITimeSource` pelo driver; testes com `ManualTimeSource`.
N5. **Estado:** a fonte guarda **configuração**; o tooltip aberto é estado do binder (ADR 0013).
N6. **Determinismo e serialização:** testes com factories e cultura injetada, sem Canvas real nos testes de lógica; a view uGUI tem teste PlayMode no host; serialização nativa.
N7. **Asmdefs (ADR 0012, emenda 0026):** a **Spec** cria os asmdefs, **não** este PRD.

## 6. Fora do escopo

- **A base de UI e os widgets de atributo:** **B-015**. **Slot e barra de habilidades:** **B-016**. **Lista de buffs e o ícone dela:** **B-018**. **Nome e ícone do `GameplayEffect`:** **B-005**.
- **`EffectDescriber`, elementos de descrição, `ITextProvider`, adaptador do Unity Localization, prévia de descrição:** **B-008**. Aqui só são **usados**.
- **Formato, papel e cor do atributo, metadados do efeito:** **B-005**.
- **Tooltip de item de inventário, de unidade no mundo e de cenário:** fora; o projeto cria a fonte (R6).
- **Comparação entre níveis ou "próximo nível":** fora (P1).
- **Tooltip fixo (travado com clique), tooltip aninhado (tooltip dentro de tooltip) e glossário:** fora.
- **Animações e áudio do tooltip:** do kit do projeto.
- **UI Toolkit em runtime.**
- **Rede e replicação.**

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

- CA1 (T). Cenário C1: a fonte de habilidade produz título, ícone, linhas de descrição, custo, recarga e cargas no nível, sem alterar o ASC.
- CA2 (T). Cenário C2: valores vivos (recarga real, custo) aparecem e atualizam com o tooltip aberto.
- CA3 (T). Cenário C3: o nível muda as linhas e atualiza com o tooltip aberto.
- CA4 (T). Cenário C4: o tooltip de efeito mostra pilhas e tempo restante, pelo `ManualTimeSource`.
- CA5 (T). Cenário C5: o tooltip de atributo mostra o valor formatado e, quando pedida, a decomposição.
- CA6 (T). Cenário C6: ponteiro (com atraso), foco e chamada por código abrem e fecham.
- CA7 (T). Cenário C7: um tooltip por vez; item destruído ou desativado fecha sem exceção.
- CA8 (S). Cenário C8: o tooltip não sai da tela e inverte de lado na borda.
- CA9 (T). Cenário C9: cultura injetada, provedor de teste e ausência de rich text no GAS; a cor vem do papel pela view.
- CA10 (T). Cenário C10: dono ausente mostra só o que o asset sabe, sem exceção.
- CA11 (S). Cenário C11: a prévia por nível aparece em edição.
- CA12 (T). Cenário C12: uma `IView<TooltipModel>` própria recebe o modelo sem conhecer o GAS.
- CA13 (T). Cenário C13: 10 000 atualizações com o tooltip aberto alocam 0 bytes gerenciados depois do aquecimento.
- CA14 (S). Cenário C14: no host, o Inspector da fonte e do trigger mostra o seletor, o resumo e a validação.
- CA15 (T). O teste de assemblies da 0-M passa (N2); nenhum tipo do módulo usa singleton, `Find`, `FindObjectOfType`, `Time.time` nem corrotina da Unity (busca de código).
- CA16 (T). O validador cobre os casos do R14, com o objeto clicável.
- CA17 (E). O CHANGELOG cita os itens do R15; a página existe; os tipos novos têm `[HelpURL]`.
- CA18 (E). O PRD do Sample (B-032) fica sem alteração neste item; a nota do R16 está registrada.

## 8. Perguntas em aberto

### Com o usuário

- **P1. Próximo nível no tooltip.** *Respondida (usuário, 2026-10-31): aceita. Só o nível atual; sem comparação com o próximo (seção 6).*
- **P2. Tooltip sem dono.** *Respondida (usuário, 2026-10-31): aceita. O tooltip de habilidade funciona sem ASC, com nível configurado na fonte e valores do asset (R3, C10).*
- **P3. Decomposição do atributo.** *Respondida (usuário, 2026-10-31): aceita. Base e modificadores por fonte como linhas, opcional na fonte, desligada por padrão (R5).*
- **P4. Alocação ao abrir.** *Respondida (usuário, 2026-10-31): aceita. Uma alocação ao abrir; zero enquanto aberto (R11, N1).*

### Com o Arquiteto (decisão técnica da Spec)

- **P1. `TooltipModel` e o buffer de linhas:** struct com lista reaproveitada fornecida pelo binder, ou o binder dono de um buffer que a view lê; como isso convive com `IView<T>` por valor (ADR 0017, item 5).
- **P2. Fontes por `[SerializeReference]` com genéricos** (ADR 0013): `UISource<TooltipModel>` ou base fechada; como o `TooltipTrigger` guarda a fonte sem estado de ligação.
- **P3. Atualização por evento** (nível, atributo de custo e de recarga, cargas) e o tick enquanto o tooltip está aberto: como o binder pede o tick ao `UIUpdateDriver` só nesse período.
- **P4. Posicionamento** (R9, C8): regra da view uGUI (pivô e inversão) e se o cálculo é uma função pura testável sem Canvas.
- **P5. Entrada do `TooltipTrigger`:** ponteiro e foco pela `EventSystem` da uGUI (`IPointerEnterHandler`, `ISelectHandler`), sem depender do Input System nem do Input Manager.
- **P6. Prévia no Editor** (R12): reaproveitar a prévia de descrição do B-008 e o `PreviewModel`; onde mora o código de Editor, sem referenciar o `.Editor` de outro módulo (ADR 0026).
- **P7. Ordem entre tickets:** modelo, binder e view por uma fonte de habilidade primeiro (depende de B-008 e B-015); `TooltipTrigger` com ponteiro e foco; fontes de efeito e atributo; prévia por último.

## 9. Ajustes de playtest

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | PRD escrito pelo Criativo a partir do B-017, do plano (H.5, H.6, H.7, UI-3) e das ADRs 0017, 0023, 0012, 0013 e 0026; o Controlador pediu o PRD inteiro e a Spec divide em tickets | Todo o documento | Rascunho |
| 2026-10-31 | usuário (via Controlador): o HUD do Sample (S4) usa uGUI e o Input Manager legado; o tooltip do Sample é complemento posterior | R16 | Aplicado |
| 2026-10-31 | Dependências registradas sem puxar escopo: B-015 (base), B-008 (descrição), B-005 (formato e papel), B-016 e B-018 (onde o tooltip se liga) | Seção 6 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P1 a P4: propostas aceitas (só nível atual; sem dono; decomposição opcional; uma alocação ao abrir). Critério de design: sistema modular, sem remendos | Seção 8 (sem mudança de texto nos requisitos) | Aplicado |
| 2026-10-31 | usuário (via Controlador) B-018 P5: nome e ícone do `GameplayEffect` entram no B-005 (AR7); o B-017 os consome de lá | Cabeçalho, R4, seção 6 | Aplicado |
