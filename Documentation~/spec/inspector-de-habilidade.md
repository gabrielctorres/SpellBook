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
