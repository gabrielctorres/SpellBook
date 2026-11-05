# Design doc: Inspectors da habilidade e do efeito reorganizados
Épico: `epic:inspector-de-habilidade` · Backlog: B-041 · ADRs: nenhuma nova (segue a 0026, o DS `ds/inspector-de-acoes.md` e o DS novo `ds/inspector-de-habilidade.md`) · Status: Aprovada (gate de 2026-11-05)

Referência visual aprovada: [artefato](https://claude.ai/artifact/94QTsdexDe1eAtYyPzdDuA?sk=wUikJrY-YHgCHtBS16TfKw), aba "Inspector da habilidade", layout B, sem o resumo do topo.

## Problema
O Inspector do `GameplayAbility` (`Editor/GAS/GameplayAbilityEditor.cs`) e o do `GameplayEffect` (`Editor/GAS/GameplayEffectEditor.cs`) mostram o Inspector padrão e põem as seções próprias no fim.
- **Habilidade.** As abas de ações aparecem depois de cerca de 30 campos. O cabeçalho "Ativação" junta 12 campos de assuntos diferentes, e os de canalização e de passiva aparecem em qualquer comportamento. O nível de prévia e os valores ficam longe de "Níveis", e a prévia das cargas fica numa segunda seção "Ativação". As seis listas de tags ocupam espaço até vazias.
- **Efeito.** As abas de ações também vêm no fim, depois de duração, período, modificadores, metadados, tags e empilhamento. Os cabeçalhos misturam inglês e português. Os modificadores do tique e a aba "A cada tick" aparecem com `Period` 0, e os campos de pilha aparecem com `CanStack` desligado.

O designer rola o Inspector para achar o campo e não lê o asset de relance.

## Critérios de aceite
1. **CA1.** Abaixo dos avisos e da validação do `SpellBookEditor`, o Inspector da habilidade tem quatro abas: Configuração, Ações, Tags e gatilhos, Níveis e prévia. Cada campo serializado visível fica em uma aba só (tabela no DS). Um campo que o editor não conhece, novo ou de subclasse, aparece no fim de Configuração e nunca some. A aba escolhida é lembrada por asset durante a sessão do Editor.
2. **CA2.** A aba Configuração tem quatro grupos com título, nesta ordem: Identidade, Comportamento, Custo e recarga, Condições de ativação. O resumo das regras (uma linha por regra, com o motivo da falha) fica no grupo Condições de ativação, logo abaixo das regras.
3. **CA3.** Campo que não vale no comportamento atual não aparece: duração e intervalo da canalização só em `Channel`, `reactivatePassive` só em `Passive`, autocast só em `Instant` e `Channel`. A aba de eventos "A cada passo" continua só em `Channel`. Trocar o comportamento não apaga o valor guardado.
4. **CA4.** A aba Níveis e prévia concentra os valores por nível. Mostra o nível máximo, a tabela de valores e uma prévia com uma linha por nível, de 1 ao máximo, com cada valor nomeado e, quando existem, a recarga, o custo e as cargas. A linha do nível de prévia fica marcada, e a prévia da descrição usa esse nível. Nenhuma outra aba mostra valor por nível. A prévia acompanha a edição sem Play.
5. **CA5.** Uma lista de tags vazia ocupa uma linha, com o nome e o botão de adicionar. O grupo de tags mostra no título quantas listas têm itens ("2 de 6 com itens"). Vale para a habilidade e para o efeito.
6. **CA6.** O Inspector do efeito tem quatro abas: Configuração (identidade, metadados, duração, período, empilhamento), Modificadores (persistentes e do tique), Ações (as abas de eventos e, junto das reações, os atributos de dano e de morte) e Tags. Valem as regras do CA1 para campo desconhecido e aba lembrada.
7. **CA7.** No efeito, a duração só aparece com `HasDuration`; os modificadores do tique e a aba de eventos "A cada tick" só com `Period` maior que 0; o máximo de pilhas, a renovação e a multiplicação só com `CanStack`. Trocar a configuração não apaga valor. Uma aba de eventos que some com ações mostra o aviso do `ds/inspector-de-acoes.md` (3.1).
8. **CA8.** O formato serializado e a API pública não mudam. Os assets existentes abrem com os mesmos valores, e os testes do `GameplayAbilityEditor` continuam passando: as constantes públicas (`PreviewLevelName`, `PreviewName`, `ActivationName`, `RuleRowClass`, `ChargesPreviewName`, `DescriptionSectionName`) seguem achando o elemento equivalente. Com vários assets selecionados, os campos das abas editam todos, e a aba Ações segue a regra do DS de ações (seção 9).
9. **CA9.** Tudo em UI Toolkit, pela 0026, sem IMGUI e sem atributo novo no Core. O desenho segue o `ds/inspector-de-habilidade.md`, e as abas de eventos seguem o `ds/inspector-de-acoes.md`.

## Cenário no Sandbox
1. Selecione `Data/Abilities/GA_Fireball`. A validação fica no topo, seguida das quatro abas. Configuração mostra os quatro grupos, e o resumo das regras fica em Condições de ativação (CA1, CA2).
2. Abra Tags e gatilhos. As listas vazias ocupam uma linha cada, e o título diz quantas têm itens. Selecione `Data/Effects/GE_Burn` e volte: a habilidade abre em Tags e gatilhos (CA1, CA5).
3. Duplique `GA_Fireball` (Ctrl+D) e edite a cópia. Troque o comportamento para `Channel`: aparecem os campos da canalização e, em Ações, a aba "A cada passo". Troque para `Passive`: some o autocast e aparece `reactivatePassive`. Volte para `Instant`: os valores digitados continuam lá (CA3).
4. Na cópia, ponha Níveis em 3 e dê três níveis a um valor nomeado. Níveis e prévia mostra três linhas com o valor, a recarga, o custo e as cargas. Mudar o nível de prévia marca outra linha e muda a descrição (CA4).
5. Selecione `GE_Burn`. Aparecem as quatro abas do efeito, e "A cada tick" existe porque o período é 0,5. Ponha `Period` em 0: somem os modificadores do tique e a aba. Desligue `CanStack`: somem os campos de pilha. Desfaça com Ctrl+Z (CA6, CA7).
6. Selecione `GE_Burn` e `GE_Fireball_Cost` juntos e mude um metadado em Configuração: os dois mudam. Ações mostra "Edição múltipla não mostra ações" (CA8).
7. Apague a cópia, entre em Play e conjure a Fireball. O comportamento é o mesmo de antes (CA8).

## Fora do escopo e fronteira
- **Criar habilidade ou efeito pelo Editor.** É o B-019a (2ª aba do artefato, "Criar habilidade ou efeito"), que absorveu essa parte do B-041 em 2026-11-04.
- **Gerenciador do GAS.** É a 3ª aba do artefato e será outro épico; ainda não tem item no backlog.
- O Inspector do `AttributeDefinition`. Pode virar um épico seguinte, com o mesmo DS.
- Mudar campos, nomes serializados ou o modelo de dados da habilidade e do efeito.
- Prévia por nível no efeito. O efeito não tem nível máximo próprio, e os valores em modo Table vêm da habilidade (gate de 2026-11-05).
- Ponto de aviso ou de erro nas abas do topo. As mensagens ficam todas acima das abas, e não há IH7 (gate de 2026-11-05).

## Decisões do épico
- Layout B, abas no topo, sem resumo no topo, com habilidade e efeito juntos (respostas do usuário de 2026-11-05).
- Os campos continuam sendo os `PropertyField` do Inspector padrão. O editor os move para a aba em `BuildAfterDefault`, como o `ActionEventsView.HideDefaultFields` já faz, e o `SpellBookEditor` não muda. Os `[Header]` do runtime que repetem os títulos dos grupos podem sair, porque não mudam o formato serializado.
- A visibilidade por comportamento, período e pilha fica no editor do tipo, sem `[ShowIf]` novo: o autocast depende de dois valores e o `Period > 0` é uma comparação, e o `[ShowIf]` da 0026 só compara com um valor. O `[ShowIf]` que a duração do efeito já tem continua.
- As abas do topo usam `TabView`, que o `ActionEventsView` já usa. A faixa de abas de eventos fica dentro da aba Ações.
- A aba lembrada é por asset, na sessão, como as abas de eventos (DS de ações, 3.1). Um asset sem aba lembrada abre em Configuração.
- O efeito tem as abas Configuração, Modificadores, Ações e Tags, como no CA6 (gate de 2026-11-05).
- O efeito fica sem prévia por nível neste épico (gate de 2026-11-05).
- As abas do topo não ganham ponto de aviso ou de erro. A validação continua só acima das abas (gate de 2026-11-05).

## Tickets
Tamanhos estimados pela 0042 (teto de ~500 linhas e 10 arquivos). A Fábrica confere no Detalhe.

| Ticket | Objetivo | CA | Depende de | Unity |
|---|---|---|---|---|
| IH1 | Quatro abas da habilidade, com um elemento de abas reutilizável no `GAS.Editor`, campos movidos por aba, campo desconhecido no fim de Configuração, aba lembrada e os quatro grupos de Configuração com o resumo das regras junto. ~350 linhas, ~5 arquivos | CA1, CA2, CA8, CA9 | gate e DS; issues abertas que mexem no `GameplayAbilityEditor` | sim |
| IH2 | Campos da habilidade por comportamento. ~150 linhas, ~3 arquivos | CA3, CA8 | IH1 | sim |
| IH3 | Aba Níveis e prévia: prévia por nível com valores, recarga, custo e cargas, linha marcada e descrição no nível de prévia. ~300 linhas, ~4 arquivos | CA4, CA8 | IH2 (mesmo arquivo) | sim |
| IH4 | Listas de tags compactas, num elemento reutilizável, e contador no título do grupo, na habilidade. ~200 linhas, ~4 arquivos | CA5, CA8 | IH1 | sim |
| IH5 | Quatro abas do efeito com o elemento do IH1 e as tags compactas do IH4. ~300 linhas, ~4 arquivos | CA5, CA6, CA8, CA9 | IH1, IH4 | sim |
| IH6 | Campos do efeito por duração, período e pilha, com a aba "A cada tick" escondida e o aviso. ~200 linhas, ~4 arquivos | CA7, CA8 | IH5 | sim |

O IH2, o IH3 e o IH4 mexem no `GameplayAbilityEditor`, então não rodam em paralelo entre si.

## Validação de uso
| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|

## IH1 — Quatro abas do Inspector da habilidade
### Detalhe (2026-11-05)
- CA: CA1, CA2, CA8, CA9. O CA3 (campos por comportamento) é do IH2, a prévia por nível do CA4 é do IH3 e as tags compactas do CA5 são do IH4. Aqui os campos só mudam de lugar.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Editor/GAS/InspectorTabsView.cs` (novo, `internal`): elemento de abas reutilizável, um `TabView` com as abas dadas pelo dono. A aba ativa fica no `SessionState` por asset, com o prefixo `SpellBook.InspectorTabs.Tab.`, separado do prefixo das abas de eventos. Um asset sem aba lembrada abre na primeira. Tem também os grupos com título em negrito, `MoveFields`, que move os `PropertyField:<campo>` do Inspector padrão para um grupo, e `MoveRemaining`, que leva o campo que sobrou para o grupo "Outros" no fim de uma aba (o grupo só existe se sobrar campo).
  - `Editor/GAS/GameplayAbilityEditor.cs:22`: `[CustomEditor(typeof(GameplayAbility), true)]`, para que a subclasse use o mesmo Inspector e o campo dela vá para "Outros". `:39-68` (`BuildAfterDefault`): as quatro abas, pela tabela do DS (seção 3):
    - Configuração: Identidade (`Icon`, `localizedName`, `AbilityTag`, `Description`), Comportamento (`behavior`, `commitMode`, `channelDuration`, `channelTickInterval`, `reactivatePassive`, `autocast`), Custo e recarga (`CostEffect`, `CooldownEffect`, `CooldownReductionAttribute`, `cooldownReductionPolicy`, `charges`) e Condições de ativação (`castRange`, `activationConditions` e, abaixo, as linhas das regras). O grupo Condições de ativação passa a se chamar `ActivationName`.
    - Ações: o `ActionEventsView`, sem mudança.
    - Tags e gatilhos: Tags (as seis listas, sem compactar) e Gatilhos (`triggers`).
    - Níveis e prévia: Níveis (`maxLevel`, `values`) e Prévia (nível de prévia, `PreviewName`, `ChargesPreviewName` e a seção `DescriptionSectionName`). A prévia das cargas sai de "Ativação", porque mostra valor por nível (CA4). As constantes públicas continuam as mesmas (CA8).
  - A linha "Script" (`m_Script`) do Inspector padrão fica acima das abas, como nos outros Inspectors da Unity, e não conta como campo desconhecido. **Decisão a confirmar com o Controlador:** a pergunta não chegou porque o terminal dele mostrava um pedido de permissão. A alternativa é pôr a linha em "Outros".
  - `Runtime/GAS/Abilities/GameplayAbility.cs:39,60,67,102,121,158`: saem os seis `[Header]`, porque os títulos agora vêm dos grupos e o "Ativação" e o "Gatilhos" ficariam no grupo errado (decisões do épico). O formato serializado não muda.
  - `Tests/GAS/EditMode/ActionInspectorTests.cs:51,166`: a busca por `Tab` fica dentro do `ActionEventsView`, porque as abas do topo também são `Tab`. Isso é consequência direta do ticket (0042, item 6), e as asserções continuam as mesmas.
  - Sem `CHANGELOG.md` e sem manual: o ticket não quebra nada, e o manual fica para o passe de documentação.
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`, classe `AbilityInspectorTabsTests`, em `Tests/GAS/EditMode/AbilityInspectorTabsTests.cs`, novo): `Tabs_FourInOrder_BelowValidation`; `Configuration_FourGroupsInOrder_WithDsFields`; `Fields_EachInOneTab_ScriptAboveTabs`; `RuleSummary_InActivationConditions_BelowRules`; `PreviewElements_InLevelsTab`; `UnknownField_InOthersAtEndOfConfiguration`; `ActiveTab_RememberedPerAsset_NewAssetOpensConfiguration`; `MultiSelection_ShowsTabs_ActionsFollowDs`; `RuntimeHeaders_Removed`.
- Critério de pronto: os nove verdes, mais `ActivationInspectorTests`, `DescriptionPreviewTests`, `LevelValuesDrawerTests`, `ActionInspectorTests` e `ThinkerInspectorTests` verdes no filtro, sem mudar asserção. A suíte completa roda na `/revisao`.
- Tamanho: 424 linhas medidas (+394 −30) e 5 arquivos, 2 deles novos, pela ADR 0042. Os `.meta` e este Detalhe não contam. Exceção: nenhuma.
- Commit: `feat(editor): quatro abas do Inspector da habilidade`

## IH2 — Campos da habilidade por comportamento
### Detalhe (2026-11-05)
- CA: CA3 e CA8. O aviso de aba de eventos escondida com ações continua onde já está, no `ActionEventsView` da aba Ações. Levá-lo ao grupo Comportamento (DS, seção 7) não está no CA3 e fica como pendência para o Controlador.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Editor/GAS/GameplayAbilityEditor.cs:54-55`: o grupo Comportamento guarda os campos `channelDuration`, `channelTickInterval`, `reactivatePassive` e `autocast` depois de movê-los. Um método `internal` novo, `RefreshBehaviorFields`, mostra ou esconde cada um pelo `style.display`: canalização só em `Channel`, `reactivatePassive` só em `Passive`, autocast só em `Instant` e `Channel`. O valor serializado não muda, e o campo escondido continua no grupo. Com vários assets selecionados, o campo aparece se algum deles usa o comportamento, para que a edição múltipla alcance todos.
  - `:94-97`: o `TrackSerializedObjectValue` que já existe chama também o `RefreshBehaviorFields`, e o método roda uma vez no fim do `BuildAfterDefault`. A aba "A cada passo" já segue o comportamento pelo `ActionEventsView` (IH1) e não muda.
  - `:16-24`: o resumo da classe cita a regra.
  - Sem `[ShowIf]` novo, sem mudança no runtime e sem `CHANGELOG.md` (decisões do épico; nada quebra).
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`, classe `AbilityBehaviorFieldsTests`, em `Tests/GAS/EditMode/AbilityBehaviorFieldsTests.cs`, novo): `BehaviorFields_ShownOnlyForBehavior` (quatro casos: `Instant`, `Passive`, `Toggle`, `Channel`); `BehaviorChange_UpdatesFields_KeepsStoredValues`; `MultiSelection_MixedBehaviors_ShowsFieldsAnyAssetUses`.
- Critério de pronto: os testes acima verdes, mais `AbilityInspectorTabsTests`, `ActionInspectorTests`, `ActivationInspectorTests`, `DescriptionPreviewTests` e `LevelValuesDrawerTests` verdes no filtro, sem mudar asserção. A suíte completa roda na `/revisao`.
- Tamanho: 181 linhas medidas (+178 −3) e 2 arquivos (1 novo), pela ADR 0042. Os `.meta` e este Detalhe não contam. Exceção: nenhuma.
- Commit: `feat(editor): campos da habilidade por comportamento`

## IH3 — Aba Níveis e prévia
### Detalhe (2026-11-05)
- CA: CA4 e CA8. O aviso de aba de eventos escondida com ações continua no `ActionEventsView` da aba Ações. Levá-lo ao grupo Comportamento (DS, seção 7) não está no CA4 nem no CA8 e segue como pendência para o Controlador, como no IH2.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Editor/GAS/GameplayAbilityEditor.cs:90-101` (grupo Prévia): entre o "Nível de prévia" e os rótulos de hoje, entra a tabela por nível (`LevelTableName`, `internal`). Ela tem uma linha de cabeçalho e uma linha por nível, de 1 ao `MaxLevel`, com uma coluna por valor nomeado e, quando existem, Recarga (efeito de recarga com duração, sem a redução do atributo), Custo (um item por modificador com atributo do efeito de custo, em valor absoluto, ou o nome da fonte quando não é valor) e Cargas (máximo e tempo de recarga, só com as cargas ligadas). A linha do nível de prévia ganha a classe `LevelRowSelectedClass`, com o fundo de seleção, e o marcador `◂` no número do nível. Valores com até três casas, na cultura atual.
  - `:151-164` (`SetPreviewLevel`): refaz a tabela com a linha marcada. Como o `TrackSerializedObjectValue` que já existe chama esse método, a tabela acompanha a edição sem Play. A prévia da descrição já usa o mesmo nível.
  - `:193-208` (`ChargesPreview`, `Preview`): o texto da fonte do máximo vai para um método que a coluna de cargas e a de custo também usam, e a duração da recarga e os nomes da tabela vão para métodos que a tabela e o `Preview` dividem. O texto dos rótulos não muda.
  - Os rótulos `PreviewName` e `ChargesPreviewName` continuam abaixo da tabela, com o texto do nível de prévia de hoje (CA8: os testes do editor seguem sem mudar asserção). Com vários assets, a tabela usa o primeiro (DS, seção 7).
  - `:17-26`: o resumo da classe cita a tabela.
  - Sem mudança no runtime, sem `CHANGELOG.md` e sem manual: nada quebra, e o manual fica para o passe de documentação.
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`, classe `AbilityLevelPreviewTests`, em `Tests/GAS/EditMode/AbilityLevelPreviewTests.cs`, novo): `LevelTable_OneRowPerLevel_NamedValuesAndCooldown`; `LevelTable_CostAndCharges_WhenPresent`; `PreviewLevel_MarksRow_DescriptionUsesSameLevel`; `Edit_RebuildsTable_WithoutPlay`; `LevelValues_OnlyInLevelsTab`; `MultiSelection_TableUsesFirstAsset`.
- Critério de pronto: os seis verdes, mais `AbilityInspectorTabsTests`, `AbilityBehaviorFieldsTests`, `ActivationInspectorTests`, `DescriptionPreviewTests` e `LevelValuesDrawerTests` verdes no filtro, sem mudar asserção. A suíte completa roda na `/revisao`.
- Tamanho: 392 linhas medidas (+376 −16) e 2 arquivos (1 novo), pela ADR 0042. Os `.meta` e este Detalhe não contam. Exceção: nenhuma.
- Commit: `feat(editor): prévia por nível da habilidade`

## IH4 — Listas de tags compactas
### Detalhe (2026-11-05)
- CA: CA5 (na habilidade; o efeito é do IH5) e CA8. O texto do título do grupo continua "Tags", e o contador fica numa etiqueta ao lado, na mesma linha. Assim o `AbilityInspectorTabsTests` segue sem mudar asserção.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Editor/GAS/CompactTagList.cs` (novo, `internal`): elemento reutilizável que recebe o `PropertyField` de uma lista e a `SerializedProperty` dela. Com a lista vazia, mostra uma linha com o nome (`displayName`, em texto secundário, com o `[Tooltip]` do campo) e o botão "+ adicionar" à direita, e esconde o `PropertyField`. Com itens, mostra o `PropertyField` de sempre: nome, itens e botão no rodapé (DS, seção 6). O botão cria um item pela `SerializedProperty`, com desfazer. Com vários assets, a lista só fica compacta se estiver vazia em todos. Tem também `WrapGroup`, que embrulha as listas de um grupo e põe ao lado do título a etiqueta "N de M com itens" (`CountName`), e `RefreshAll`, que atualiza as listas e o contador. O IH5 reusa os dois no efeito.
  - `Editor/GAS/GameplayAbilityEditor.cs:92-97`: o grupo Tags embrulha as seis listas no `CompactTagList` e ganha o contador. `:122-128`: o `TrackSerializedObjectValue` que já existe chama o `RefreshTagLists` (`internal`, novo). Ao montar, o `WrapGroup` já deixa as listas e o contador atualizados. `:19-29`: o resumo da classe cita as listas compactas.
  - Sem mudança no runtime, sem `CHANGELOG.md` e sem manual.
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`, classe `AbilityCompactTagListsTests`, em `Tests/GAS/EditMode/AbilityCompactTagListsTests.cs`, novo): `EmptyList_OneRow_NameTooltipAndAddButton`; `ListWithItems_ShowsFullField`; `AddButton_AddsItem_ShowsFullField`; `TagsGroup_CountsListsWithItems`; `MultiSelection_ItemsInAnyAsset_ShowsFullField`.
- Critério de pronto: os cinco verdes, mais `AbilityInspectorTabsTests`, `AbilityBehaviorFieldsTests`, `AbilityLevelPreviewTests`, `ActivationInspectorTests` e `DescriptionPreviewTests` verdes no filtro, sem mudar asserção. A suíte completa roda na `/revisao`.
- Tamanho: 305 linhas medidas (+302 −3) e 3 arquivos (2 novos), pela ADR 0042. Os `.meta` e este Detalhe não contam. Exceção: nenhuma.
- Commit: `feat(editor): listas de tags compactas na habilidade`

## IH5 — Quatro abas do Inspector do efeito
### Detalhe (2026-11-05)
- CA: CA5 (no efeito), CA6, CA8 e CA9. A visibilidade por duração, período e pilha (CA7) é do IH6. Aqui os campos só mudam de lugar, e a aba "A cada tick" continua como está.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Editor/GAS/GameplayEffectEditor.cs:9-24`: `[CustomEditor(typeof(GameplayEffect), true)]`, para que a subclasse use o mesmo Inspector e o campo dela vá para "Outros". O `BuildAfterDefault` monta as quatro abas com o `InspectorTabsView`, pela tabela do DS (seção 4):
    - Configuração: Identidade (`localizedName`, `icon`, `isBuff`, `isDebuff`, `isHidden`, `isPurgable`), Duração (`DurationPolicy`, `duration`, `defaultLevel`, `Period`) e Empilhamento (`CanStack`, `MaxStacks`, `RefreshDurationOnStack`, `stacksMultiplyMagnitude`). O campo desconhecido vai para "Outros", no fim desta aba, e a linha "Script" fica acima das abas, como na habilidade.
    - Modificadores: Persistentes (`Modifiers`) e Do tique (`PeriodicModifiers`).
    - Ações: o `ActionEventsView` de hoje, com as reações agrupadas e, abaixo dele, o grupo "Reações" com `damageAttributes` e `deathAttribute`.
    - Tags: o grupo Tags com `damageTags`, `GrantedTags`, `ApplicationRequirements` e `ApplicationImmunityTags`, embrulhadas pelo `CompactTagList.WrapGroup` ("N de 4 com itens"). O `TrackSerializedObjectValue` chama o `RefreshAll` do grupo, como na habilidade.
    - O resumo da classe cita as abas.
  - `Editor/GAS/CompactTagList.cs:28-46,81-94`: o `damageTags` é um `GameplayTagContainer`, não uma lista. O `WrapGroup` passa a aceitar o conjunto: conta e acrescenta itens pela lista `tags` de dentro dele, e o nome e o tooltip da linha compacta vêm do campo do efeito. A lista comum não muda.
  - `Runtime/GAS/Effects/GameplayEffect.cs:50,67,90,94,97,101,119,175,186`: saem os nove `[Header]`, porque os títulos agora vêm dos grupos e o "Actions" e o "Gameplay Tags" ficariam no lugar errado (decisões do épico). O formato serializado não muda.
  - `Tests/GAS/EditMode/ActionInspectorTests.cs:71`: a busca por `Tab` fica dentro do `ActionEventsView`, porque as abas do topo do efeito também são `Tab`, como o IH1 fez na habilidade. Consequência direta do ticket (0042, item 6); as asserções continuam as mesmas.
  - Sem `CHANGELOG.md` e sem manual: nada quebra, e o manual fica para o passe de documentação. O efeito fica sem prévia por nível e sem ponto de aviso nas abas (gate de 2026-11-05).
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`, classe `EffectInspectorTabsTests`, em `Tests/GAS/EditMode/EffectInspectorTabsTests.cs`, novo): `Tabs_FourInOrder_BelowValidation`; `Configuration_ThreeGroupsInOrder_WithDsFields`; `Modifiers_PersistentAndTickGroups`; `Actions_EventTabs_ThenReactionAttributes`; `Fields_EachInOneTab_ScriptAboveTabs`; `TagsGroup_FourCompactLists_CountsListsWithItems`; `DamageTags_Empty_OneRowWithFieldName_AddButtonAddsTag`; `UnknownField_InOthersAtEndOfConfiguration`; `ActiveTab_RememberedPerAsset_NewAssetOpensConfiguration`; `MultiSelection_FieldsEditAll_ActionsFollowDs`; `RuntimeHeaders_Removed`.
- Critério de pronto: os onze verdes, mais `ActionInspectorTests`, `AbilityCompactTagListsTests`, `AbilityInspectorTabsTests`, `LevelValuesDrawerTests` e `ThinkerInspectorTests` verdes no filtro, sem mudar asserção. A suíte completa roda na `/revisao`.
- Tamanho: 338 linhas medidas (+320 −18) e 5 arquivos (1 novo), pela ADR 0042. Os `.meta` e este Detalhe não contam. Exceção: nenhuma.
- Commit: `feat(editor): quatro abas do Inspector do efeito`

## IH6 — Campos do efeito por duração, período e pilha
### Detalhe (2026-11-05)
- CA: CA7 e CA8. A duração já some fora de `HasDuration` pelo `[ShowIf]` que o campo tem, e ele continua (decisões do épico). O ticket acrescenta os outros campos, a aba "A cada tick" e o aviso.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Editor/GAS/GameplayEffectEditor.cs:45-55`: o editor guarda o grupo "Do tique" e os campos `MaxStacks`, `RefreshDurationOnStack` e `stacksMultiplyMagnitude` depois de movê-los. Um método `internal` novo, `RefreshConfigFields`, mostra ou esconde pelo `style.display`: o grupo "Do tique" inteiro (título e `PeriodicModifiers`) só com `Period` maior que 0, e os três campos de pilha só com `CanStack`. O valor serializado não muda, e o campo escondido continua no grupo. Com vários assets, o campo aparece se algum deles o usa, como no IH2.
  - `:59-60`: o `ActionEventsView` do efeito recebe a regra da aba (`OnTick` só com `Period` maior que 0) e o motivo "o período atual" para o texto do aviso. O aviso de aba escondida com ações sai da faixa de eventos e vai para o grupo Duração (DS do Inspector da habilidade, seção 7), logo abaixo do `Period`.
  - `:70`: o `TrackSerializedObjectValue` que já existe chama também o `RefreshConfigFields`, que roda uma vez no fim do `BuildAfterDefault`. `:10-17`: o resumo da classe cita a regra.
  - `Editor/GAS/Actions/ActionEventsView.cs:45-49,67-68,128-131`: parâmetro opcional `hiddenReason` (padrão "o comportamento atual", o texto de hoje) e a propriedade `internal HiddenWarning`, para o dono pôr o aviso no grupo que causou. Na habilidade nada muda.
  - `Tests/GAS/EditMode/ActionInspectorTests.cs:67`: o `EffectTabs_ReactionsGrouped` monta o efeito com `Period` 0,5, porque com 0 a aba "A cada tick" agora some (CA7). O teste é sobre o grupo "Reações", e as asserções não mudam. É consequência direta do ticket (0042, item 6).
  - Sem `[ShowIf]` novo, sem mudança no runtime e sem `CHANGELOG.md` (decisões do épico; nada quebra).
  - **Pendência para o Controlador (vinda do IH2 e do IH3):** na habilidade, o aviso continua no `ActionEventsView` da aba Ações. Levá-lo ao grupo Comportamento fica em duas linhas com a `HiddenWarning` deste ticket, mas não está no CA7 nem no CA8 (é do CA3) e mudaria o lugar que o `ActionInspectorTests.ChannelTab_OnlyWithChannel_HiddenWithWarning` procura. Não foi feito.
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`, classe `EffectConfigFieldsTests`, em `Tests/GAS/EditMode/EffectConfigFieldsTests.cs`, novo): `Duration_ShownOnlyWithHasDuration` (três casos, pela regra do `[ShowIf]`); `TickGroup_ShownOnlyWithPeriod`; `StackFields_ShownOnlyWithCanStack`; `ConfigChange_UpdatesFields_KeepsStoredValues`; `TickTab_HiddenWithActions_WarningInDurationGroup`; `MultiSelection_ShowsFieldsAnyAssetUses`.
- Critério de pronto: os testes acima verdes, mais `EffectInspectorTabsTests`, `ActionInspectorTests` e `AbilityBehaviorFieldsTests` verdes no filtro, sem mudar asserção. A suíte completa roda na `/revisao`.
- Tamanho: 293 linhas medidas (+281 −12) e 4 arquivos (1 novo), pela ADR 0042. Os `.meta` e este Detalhe não contam. Exceção: nenhuma.
- Commit: `feat(editor): campos do efeito por duração, período e pilha`

## Manual do Inspector
### Detalhe (2026-11-05)
- CA: CA1 a CA7, na descrição do manual (issue #408). O ticket não muda código. O rascunho do GitBook (GB9) ainda não existe, então a edição dos `.md` sai direto no repositório (decisão do usuário).
- Escopo (caminhos em `Documentation~/manual/`):
  - `modulos/gas.md:99-115` (1.3): seção nova "Inspector do efeito", depois da lista do efeito, com as quatro abas, os grupos, os campos que só aparecem com `HasDuration`, `Period` maior que 0 e `CanStack`, o aviso no grupo Duração, as tags compactas e a aba lembrada. `:107`: os metadados ficam no grupo **Identidade**, e não mais em **Metadados**.
  - `modulos/gas.md:234-237` (1.4): seção nova "Inspector da habilidade", com as quatro abas, os grupos de Configuração, os campos por comportamento, as tags compactas com o contador, o grupo "Outros" e a aba lembrada. `:277`: a prévia sai de "depois dos campos" e vai para a tabela por nível da aba **Níveis e prévia**.
  - `modulos/gas.md:351-355` (passo a passo): a lista "Ao ativar" fica na aba **Ações**. `:566`: o resumo das regras fica no grupo **Condições de ativação**. `:605`, `:640` e `:664`: o lugar das cargas, a prévia das cargas na aba **Níveis e prévia** e os gatilhos na aba **Tags e gatilhos**.
  - `regras/migracao-2-para-3.md:25-29`: o GAS não usa mais o Inspector padrão com `[Header]`; tem abas e grupos próprios.
  - `modulos/tags.md:153-169`: uma frase sobre a lista compacta na habilidade e no efeito.
  - Nenhum marcador de exemplo muda, então a geração não roda. Sem `CHANGELOG.md`: nada quebra.
- Testes red: nenhum, ticket só de texto do passe. As guardas `Documentacao` rodam sob a trava, com as 2 vermelhas herdadas da `main` (`EveryDocUrlHasPage` e `EveryHelpUrlHasPageAndAnchor`).
- Critério de pronto: a busca por "depois dos campos", "Abaixo dos campos da habilidade", "seção **Ativação**" e "Inspector padrão da Unity com `[Header]`" não acha nada no manual; as conferências da seção 7 do guia passam; as guardas `Documentacao` passam, menos as 2 herdadas.
- Tamanho: 0 linhas contadas (só `.md`) e 3 arquivos, pela ADR 0042. Este Detalhe não conta. Exceção: nenhuma.
- Commit: `docs(manual): Inspector da habilidade e do efeito com abas`
