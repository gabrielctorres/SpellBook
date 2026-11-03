# Spec: Módulo Teams
PRD: [Documentation~/prd/teams.md](../prd/teams.md) · Backlog: B-011 · ADRs: **0035 (nova, Proposta)**; usa 0003, 0004, 0007, 0012, 0013, 0019, 0026, 0032, 0034

Status: Aprovada no gate de 2026-10-31, Lote D (TM1–TM5; ADR 0035 aceita; Q1: o asset identifica o time; Q2: filtro único, com a emenda no EV4b, no EV8 e no EV16 do B-012) · Emenda de 2026-11-01: uma chave de falha na `TeamRelationCondition` (Design 2, TM5) · Data: 2026-10-31 · Autor: Arquiteto

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`.

- Não existe nada de times no pacote. `SpellBook.Teams` está na tabela da ADR 0012 (referencia só Core). O `SpellBook.GAS.Bridges` pode referenciá-lo "quando o primeiro tipo que o usa entrar no Bridges" (emenda de 2026-10-29 da 0012).
- `Runtime/GAS.Bridges/SpellBook.GAS.Bridges.asmdef:4-10`: hoje Core, Tags, Time, GAS e Events.
- `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs`:
  - `:18-25`: listas `RuntimeModules`, `EditorModules` e `PlayModeModules`;
  - `:245`: referências exatas do Bridges;
  - `:341-355`: `AllExpectedAssembliesExist`.
- `Tests/Core/EditMode/HelpUrlTests.cs:16`: todo `MonoBehaviour`/`ScriptableObject` público e concreto de runtime precisa de `[HelpURL]`. `Runtime/Core/DocUrls.cs:10-22` guarda as constantes, e o `ManualLinksTests` exige a página.
- `Tests/Core/EditMode/Inspector/SpellBookEditorCoverageTests.cs:18-21`: os assemblies cobertos são uma lista explícita. O Teams entra nela no ticket do Inspector.
- `Runtime/GAS/Targeting/TargetingStrategy.cs:49,56`: o targeting acha o ASC por `GetComponentInParent`. O B-012 troca isso pelo `AreaSelector` (EV4b), com o filtro de candidato da ADR 0035 (EV8, emenda).
- Condições: `Condition<ConditionContext>` (B-009, AV1 e AV2; ADR 0032). `AbilityTarget.Unit` chega no EV8 do B-012.

## Respostas às perguntas técnicas do PRD (seção 8)

- **P7. Cache do time do ASC.** **Sem cache.** O Bridges acha o membro subindo a hierarquia com `Transform.TryGetComponent<ITeamMember>` (o objeto do ASC, depois os pais). `TryGetComponent` não aloca nem quando não acha. O `GetComponentInParent` aloca no Editor quando não acha, por causa do objeto nulo falso. O custo é a profundidade da hierarquia, pago só em consultas de time. CA16 mede. Um cache precisaria de estado global (proibido, N3) ou dentro da condição (proibido pela 0032). Se o perfil mostrar custo, um componente de ligação entra como item próprio.
- **P8. Identificador estável.** A **referência de asset** do `TeamDefinition` é o identificador. A Unity a guarda por GUID e `fileID`, que não mudam ao renomear nem ao mover o asset. A matriz referencia os times por asset. Não há id próprio: um `string id` serializado seria uma segunda identidade, e duplicar o asset (Ctrl+D) copiaria o id. O validador acusa time repetido na matriz. *Diverge do texto do R1 ("identificador serializado"): pergunta Q1.*
- **P9. `ITeamMember`.** Só `TeamDefinition Team { get; }` e `TeamRelationMatrix Matrix { get; }`. Não exige `GameObject`. O Bridges só acha membros que são componentes (por `TryGetComponent`). Entidades que não são `MonoBehaviour` usam as consultas estáticas direto.
- **P10. Condição de time no contexto.** Pela ADR 0035, `TeamRelationCondition : Condition<ConditionContext>` compara o time do `Owner` com o da unidade do `Target`. Ativação, ação condicional, seletor de área, impacto do projétil e aura chamam a mesma condição com o mesmo contexto. O Core não conhece Teams.
- **P11. Gancho de filtros.** Não há lista de filtros. O gancho é o campo de filtro de candidato (`Condition<ConditionContext>`) do `AreaSelector`, da aura (pelo `AreaSelector` dela) e do impacto do projétil (ADR 0035). O filtro de tags do alvo é a `TagQueryCondition` com sujeito `TargetUnit`, e os dois se combinam por `And`.
- **P12. Ordem.** Dois blocos. **Parte 1 (TM1 a TM4):** não depende de nenhum item aberto e pode entrar na fila a qualquer momento. **Parte 2 (TM5):** depende do AV2 e do EV8 com a emenda da 0035.
- **Matriz de qual membro na consulta `a → b`:** vale a matriz de `a`. Sem ela, a de `b`. Sem nenhuma, `Neutral` (R16, padrão do PRD P2).

## Design

### 1. Tipos do módulo (`Runtime/Teams/`, namespace `SpellBook.Teams`)

```csharp
public enum TeamRelation { Allied, Hostile, Neutral }

[Flags] public enum TeamRelationMask { None = 0, Self = 1, Allied = 2, Hostile = 4, Neutral = 8 }

public static class TeamRelationMaskExtensions
{
    // Self casa só quando isSelf; senão casa a relação. Máscara vazia não casa com nada.
    public static bool Matches(this TeamRelationMask mask, TeamRelation relation, bool isSelf);
}

[CreateAssetMenu(menuName = "SpellBook/Teams/Team")] [HelpURL(DocUrls.Teams + "#teamdefinition")]
public sealed class TeamDefinition : ScriptableObject, IValidatable
{   // displayName, color
}

[CreateAssetMenu(menuName = "SpellBook/Teams/Relation Matrix")] [HelpURL(DocUrls.Teams + "#teamrelationmatrix")]
public sealed class TeamRelationMatrix : ScriptableObject, IValidatable
{
    [SerializeField] List<TeamDefinition> teams;
    [SerializeField] List<TeamRelationEntry> entries;   // (from, to, relation), par ordenado, esparso
    [SerializeField] TeamRelation defaultRelation = Neutral;     // par sem célula
    [SerializeField] TeamRelation sameTeamRelation = Allied;     // time consigo mesmo sem célula
    [SerializeField] TeamRelation noTeamRelation = Neutral;      // um dos lados sem time
    public TeamRelation GetRelation(TeamDefinition from, TeamDefinition to);
}
```

- **Consulta sem alocação:**
  - na primeira consulta (e no `OnValidate`, no Editor), a matriz monta um índice `Dictionary<TeamDefinition, int>` e um vetor `TeamRelation[n*n]`, uma vez só;
  - `GetRelation` faz duas buscas no dicionário e lê o vetor;
  - célula ausente: `sameTeamRelation` para o mesmo time, `defaultRelation` para os demais;
  - `null` ou time fora da lista: `noTeamRelation`;
  - a matriz não é alterada em runtime (P5): não há API pública de escrita.
- **Membros:**
  - `ITeamMember { TeamDefinition Team { get; } TeamRelationMatrix Matrix { get; } }`;
  - `TeamMember : MonoBehaviour, ITeamMember, IValidatable` (`[HelpURL]`): `SetTeam(TeamDefinition)`, `event Action<TeamMember, TeamDefinition, TeamDefinition> OnTeamChanged`. Só dispara em mudança real. O time é gravado antes de chamar os ouvintes, então um ouvinte que troca de novo dispara outro evento, sem estado intermediário (R9).
- **`TeamQueries`** (estáticas e de extensão sobre `ITeamMember`):
  - `GetRelation(a, b)`: membro nulo conta como "sem time"; `a == b` devolve a relação do time consigo mesmo (P4);
  - `IsAllied`, `IsHostile`, `IsNeutral`;
  - `Matches(TeamRelationMask mask, ITeamMember a, ITeamMember b)`, com `Self` quando `ReferenceEquals(a, b)`.

### 2. Integração no `SpellBook.GAS.Bridges` (parte 2)

- O asmdef do Bridges passa a referenciar `SpellBook.Teams`. O GAS e o Teams não se referenciam.
- `AscTeams.GetMember(AbilitySystemComponent asc)` (estática, `public`): sobe de `asc.transform` pelos pais com `TryGetComponent<ITeamMember>`. Devolve `null` se não achar ou se `asc` for nulo.
- `TeamRelationCondition : Condition<ConditionContext>` (`[Serializable] sealed`, `[TypeCategory("Time", Line = "Relação com o alvo: {Mask}")]`):
  - campo `TeamRelationMask mask = Hostile`;
  - `Evaluate`: `a = AscTeams.GetMember(context.Owner)`, `b = AscTeams.GetMember(context.Target.Unit)`, `isSelf = context.Owner == context.Target.Unit`, e então `mask.Matches(TeamQueries.GetRelation(a, b), isSelf)`;
  - sem unidade no `Target`: falso, sem exceção;
  - `DefaultFailureKey` = `teams.condition.relation`, a única chave da condição, inclusive quando falta a unidade;
  - `Validate`: máscara vazia gera aviso.
- Emenda de 2026-11-01 (mesma regra da opção (c) do AV3, aprovada pelo usuário em 2026-11-01): a versão anterior dava duas chaves à condição, `teams.condition.no_target` sem unidade e `teams.condition.relation` nas outras falhas. A chave de uma condição é fixa (`DefaultFailureKey` não recebe contexto, ADR 0032, item 4), então a chave `no_target` sai. Para a falta de alvo ter motivo próprio numa regra de ativação, o designer põe antes a regra da `HasTargetPointCondition` (AV3, chave `gas.activation.no_target`); a primeira regra que falha decide. Um alvo que é só um ponto, sem unidade, passa nessa regra e falha a de time com `teams.condition.relation`, como a `TagQueryCondition` com sujeito `TargetUnit` (ADR 0035, item 2). No filtro de candidato o alvo é sempre `AtUnit`, então o caso sem unidade não aparece lá. Nenhum tipo novo entra.
- Usos sem código novo (ADR 0035): regra de ativação, `ConditionalAction`, filtro do `AreaSelector`, impacto do projétil e aura do B-014.

### 3. Validação (R5, R17) e Inspector (R18)

- `TeamDefinition.Validate`: nome de exibição vazio (aviso).
- `TeamRelationMatrix.Validate`:
  - time nulo na lista (erro);
  - time repetido (erro);
  - entrada com time fora da lista (erro);
  - par repetido (erro, a segunda vence);
  - lista vazia (aviso).
- `TeamMember.Validate`: sem matriz (aviso); sem time (aviso); time fora da matriz (aviso).
- Mensagens com `propertyPath`.
- `Editor/Teams/` (`SpellBook.Teams.Editor`: Core, Teams, Core.Editor), herdando de `SpellBookEditor`:
  - `TeamDefinition` com a cor;
  - `TeamRelationMatrix` com a tabela (linhas = de, colunas = para, `EnumField` por célula, célula vazia mostra o padrão em cinza) e o botão "Espelhar" por célula e para a matriz inteira;
  - `TeamMember` com o time e a matriz;
  - `PropertyDrawer` de `TeamRelationMask` com quatro `Toggle` (Self, Aliado, Hostil, Neutro). Vale também nos campos do Bridges, porque a Unity aplica o drawer pelo tipo.
- A lógica de edição fica em `TeamMatrixEditing` (estática, testável):
  - `Set(matrix, from, to, relation)`;
  - `Mirror(matrix, from, to)`;
  - `MirrorAll(matrix)`;
  - `Clear(matrix, from, to)`.

  Todas com `Undo`.

### 4. O que não muda

- O GAS não referencia Teams. O Teams não referencia GAS, Tags, Time, Events, Cues nem UI (CA10, CA15).
- O seletor de área, o projétil e a aura não ganham campo de time: o time é uma condição (ADR 0035).

## Quebras de compatibilidade

Nenhuma. Só acréscimos (módulo novo e uma condição nova no Bridges).

## Tickets

Todos usam a Unity e seguem o WIP 1. TM1 a TM4 não dependem de item aberto. Testes EditMode, com factory de times e sem assets fixos. Cada ticket escreve a sua parte da página `manual/modulos/times.md` e do `CHANGELOG.md`. Teto: ~500 linhas e 10 arquivos (ADR 0003).

**Conflitos com a fila:**
- `AsmdefRulesTests.cs`: TM1, TM4 e TM5, e qualquer ticket de asmdef do B-013, que não pode rodar junto.
- `DocUrls.cs`: TM1, CU2 e TK4.
- `SpellBook.Testing.asmdef`: TM2 e CU3.
- `SpellBook.GAS.Bridges.asmdef`: TM5 e CU8.
- `SpellBookEditorCoverageTests.cs`: TM4 e CU7.

### TM1 — Módulo, times, matriz e consulta
- Escopo: `Runtime/Teams/SpellBook.Teams.asmdef` (novo; Core), `TeamRelation.cs` (enum, máscara e `Matches`), `TeamDefinition.cs`, `TeamRelationMatrix.cs` (novos), `Tests/Teams/EditMode/SpellBook.Teams.Tests.EditMode.asmdef` (novo), `Tests/Teams/EditMode/TeamRelationMatrixTests.cs` (novo; cria os assets com `ScriptableObject.CreateInstance` e destrói no `TearDown`), `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs` (módulo `Teams` em `RuntimeModules`; teste de referências: só Core), `Runtime/Core/DocUrls.cs` (`Teams = Base + "modulos/times/"`), `Documentation~/manual/modulos/times.md` (nova: times, matriz, padrões), `CHANGELOG.md`.
- Testes red:
  - `Matrix_Hostile_Allied_Neutral_PerCell` (CA1, C1);
  - `Matrix_SameTeamWithoutCell_IsAllied_DefaultChangeable`;
  - `Matrix_MissingPair_UsesDefault` (CA4, C4);
  - `Matrix_IsDirectional` (CA5, C5);
  - `Matrix_NullOrUnknownTeam_UsesNoTeamRelation` (CA3, parte);
  - `Mask_EachCombination_SelfOnlyWhenSame_EmptyMatchesNothing` (CA7);
  - `GetRelation_DoesNotAllocate_AfterFirstCall` (CA16, parte);
  - `TeamsAsmdef_ReferencesOnlyCore` (CA10).
- Critério de pronto: testes verdes; `HelpUrlTests` e `ManualLinksTests` verdes.
- Tamanho estimado: ~430 linhas; 10 arquivos (1 página).

### Detalhe (2026-11-03)
- CA: CA1, CA3 (parte: time nulo ou fora da matriz), CA4, CA5, CA7, CA10, CA16 (parte: `GetRelation`).
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/Teams/SpellBook.Teams.asmdef` (novo; só Core);
  - `Runtime/Teams/TeamRelation.cs` (novo: `TeamRelation`, `TeamRelationMask`, `TeamRelationMaskExtensions.Matches`);
  - `Runtime/Teams/TeamDefinition.cs` (novo: `displayName`, `color`, `[HelpURL]`, `[CreateAssetMenu]`);
  - `Runtime/Teams/TeamRelationMatrix.cs` (novo: listas `teams` e `entries`, os três padrões, `GetRelation` com índice preguiçoso `Dictionary<TeamDefinition, int>` + `TeamRelation[n*n]`, refeito no `OnValidate`; `TeamRelationEntry` serializável interno no mesmo arquivo; par repetido: a última entrada vence);
  - `Runtime/Core/DocUrls.cs:20` (+`Teams = Base + "modulos/times/"`);
  - `Tests/Teams/EditMode/SpellBook.Teams.Tests.EditMode.asmdef` (novo; Core, Teams, Time, Testing e o runner, como o dos Tags);
  - `Tests/Teams/EditMode/TeamRelationMatrixTests.cs` (novo; times e matriz por `ScriptableObject.CreateInstance` e células por `SerializedObject`, destruídos no `TearDown`);
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:18-21` (`Teams` em `RuntimeModules`) e teste novo `TeamsAsmdef_ReferencesOnlyCore` depois de `:245`.
  - Fora, pela emenda de 2026-11-03 da 0007: a página `manual/modulos/times.md` e o `CHANGELOG.md` (sem quebra) ficam para o passe de documentação. `IValidatable` e `Validate` entram no TM3.
- Testes red (EditMode): `TeamRelationMatrixTests.Matrix_Hostile_Allied_Neutral_PerCell`, `Matrix_SameTeamWithoutCell_IsAllied_DefaultChangeable`, `Matrix_MissingPair_UsesDefault`, `Matrix_IsDirectional`, `Matrix_NullOrUnknownTeam_UsesNoTeamRelation`, `Mask_EachCombination_SelfOnlyWhenSame_EmptyMatchesNothing`, `GetRelation_DoesNotAllocate_AfterFirstCall`; `AsmdefRulesTests.TeamsAsmdef_ReferencesOnlyCore` (e `AllExpectedAssembliesExist`, `EachRuntimeModuleHasEditModeTests` com o Teams).
- Critério de pronto: os testes acima verdes com filtro, e `HelpUrlTests` verde. `ManualLinksTests` (categoria `Documentacao`) fica para o passe de documentação, junto com a página.
- Tamanho: ~410 linhas e 8 arquivos, pela ADR 0042. Exceção: nenhuma.
- Commit: `feat(teams): módulo Teams com times, matriz e consulta`

### TM2 — Membros, consultas e troca em runtime
- Escopo: `Runtime/Teams/ITeamMember.cs`, `TeamMember.cs`, `TeamQueries.cs` (novos), `Tests/Teams/EditMode/TeamMemberTests.cs` (novo), `Tests/Testing/TeamsFactory.cs` (novo: times, matriz com células, objeto com `TeamMember`), `Tests/Testing/SpellBook.Testing.asmdef` (+Teams), `times.md` (membro, consultas, componente próprio), `CHANGELOG.md`.
- Testes red:
  - `IsAllied_IsHostile_IsNeutral_BetweenMembers` (CA2, C2);
  - `SameMember_ReturnsSameTeamRelation`;
  - `NoMemberOrNoTeam_UsesNoTeamRelation_NoException` (CA3, C3);
  - `MatrixFromAsker_ThenOther_ThenNeutral`;
  - `SetTeam_FiresOnce_SameTeamDoesNot_ListenerChangingAgainIsSafe` (CA6, C6);
  - `CustomITeamMember_ParticipatesInQueries` (CA8);
  - `Queries_DoNotAllocate` (CA16, parte);
  - `TeamsSources_NoSingletonOrFind` (CA19, guarda textual sobre `Runtime/Teams/**` no padrão do `GasSourceScanTests`).
- Critério de pronto: CA2, CA3, CA6, CA8 e CA19 verdes.
- Tamanho estimado: ~420 linhas; 8 arquivos. Depende de: TM1.

### Detalhe (2026-11-03)
- CA: CA2, CA3, CA6, CA8, CA16 (parte: consultas entre membros), CA19.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/Teams/ITeamMember.cs` (novo: `Team` e `Matrix`);
  - `Runtime/Teams/TeamMember.cs` (novo: `[SerializeField]` `team` e `matrix`, `[HelpURL(DocUrls.Teams + "#teammember")]`, `SetTeam`, `OnTeamChanged(membro, antes, depois)` disparado só em mudança real e com o time já gravado);
  - `Runtime/Teams/TeamQueries.cs` (novo: `GetRelation(this ITeamMember a, ITeamMember b)` com a matriz de `a`, depois a de `b`, depois `Neutral`; membro nulo conta como sem time; `IsAllied`, `IsHostile`, `IsNeutral`; `Matches(mask, a, b)` com `Self` quando `a` não é nulo e `ReferenceEquals(a, b)`);
  - `Tests/Testing/TeamsFactory.cs` (novo, `#if UNITY_EDITOR` como o `EventsFactory`: time, matriz com padrão, célula, objeto com `TeamMember`; tudo destruído no `Dispose`);
  - `Tests/Testing/SpellBook.Testing.asmdef:4-9` (+`SpellBook.Teams`);
  - `Tests/Teams/EditMode/TeamMemberTests.cs` (novo; inclui a guarda textual de CA19 sobre `Runtime/Teams/**`, no padrão do `GasSourceScanTests`).
  - Fora, pela emenda de 2026-11-03 da 0007: `times.md` e `CHANGELOG.md` (sem quebra). `IValidatable` do `TeamMember` entra no TM3. O `TeamRelationMatrixTests` do TM1 não passa a usar a factory (fora do ticket).
- Testes red (EditMode, `TeamMemberTests`): `IsAllied_IsHostile_IsNeutral_BetweenMembers`, `SameMember_ReturnsSameTeamRelation`, `NoMemberOrNoTeam_UsesNoTeamRelation_NoException`, `MatrixFromAsker_ThenOther_ThenNeutral`, `SetTeam_FiresOnce_SameTeamDoesNot_ListenerChangingAgainIsSafe`, `CustomITeamMember_ParticipatesInQueries`, `Queries_DoNotAllocate`, `TeamsSources_NoSingletonOrFind`.
- Critério de pronto: `TeamMemberTests` (8) e `TeamRelationMatrixTests` verdes com filtro; `HelpUrlTests` e `AsmdefRulesTests` verdes (`TeamMember` novo e `Testing` com Teams).
- Tamanho: ~430 linhas e 6 arquivos, pela ADR 0042. Exceção: nenhuma.
- Commit: `feat(teams): membros, consultas e troca de time em runtime`

### TM3 — Validação
- Escopo: `TeamDefinition.cs`, `TeamRelationMatrix.cs`, `TeamMember.cs` (`Validate`), `Tests/Teams/EditMode/TeamsValidationTests.cs` (novo), `Documentation~/manual/ferramentas/validador.md`, `CHANGELOG.md`.
- Testes red: `Matrix_NullDuplicateUnknownTeam_RepeatedPair_EmptyList` (CA9); `Member_NoMatrix_NoTeam_TeamOutsideMatrix` (CA9); `ProjectValidator_ReportsTeamsAssetsClickable` (C8).
- Critério de pronto: CA9 verde.
- Tamanho estimado: ~300 linhas; 6 arquivos. Depende de: TM2.

### Detalhe (2026-11-03)
- CA: CA9 (a parte de máscara vazia fica com a `TeamRelationCondition`, no TM5); Design 3, validação.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/Teams/TeamDefinition.cs:9` (+`IValidatable`: nome de exibição vazio, aviso em `displayName`);
  - `Runtime/Teams/TeamRelationMatrix.cs:14` (+`IValidatable`: time nulo em `teams.Array.data[i]` (erro); time repetido, na repetição (erro); célula com time nulo ou fora da lista, em `entries.Array.data[i].from`/`.to` (erro); par repetido, na célula seguinte (erro: a última vence); lista vazia em `teams` (aviso); `internal bool Contains(TeamDefinition)` lendo a lista, sem montar o índice);
  - `Runtime/Teams/TeamMember.cs:9` (+`IValidatable`: sem matriz em `matrix` (aviso); sem time em `team` (aviso); time fora da matriz em `team` (aviso));
  - `Tests/Teams/EditMode/SpellBook.Teams.Tests.EditMode.asmdef:4-11` (+`SpellBook.Core.Editor`, para o `ProjectValidator`, como o dos Tags);
  - `Tests/Teams/EditMode/TeamsValidationTests.cs` (novo; usa a `TeamsFactory`).
  - Fora, pela emenda de 2026-11-03 da 0007: `manual/ferramentas/validador.md` e `CHANGELOG.md` (sem quebra).
- Testes red (EditMode, `TeamsValidationTests`): `Matrix_NullDuplicateUnknownTeam_RepeatedPair_EmptyList`, `Member_NoMatrix_NoTeam_TeamOutsideMatrix`, `Matrix_CellWithoutTeam_IsError`, `Definition_EmptyDisplayName_IsWarning`, `ValidSetup_HasNoMessages`, `ProjectValidator_ReportsTeamsAssetsClickable`.
- Critério de pronto: `TeamsValidationTests` (6), `TeamMemberTests` e `TeamRelationMatrixTests` verdes com filtro; `HelpUrlTests` verde.
- Tamanho: ~330 linhas e 5 arquivos, pela ADR 0042. Exceção: nenhuma.
- Commit: `feat(teams): validação de times, matriz e membros`

### TM4 — Inspector: tabela, "Espelhar" e máscara
- Escopo: `Editor/Teams/SpellBook.Teams.Editor.asmdef` (novo; Core, Teams, Core.Editor), `Editor/Teams/TeamsEditors.cs` (Inspectors do `TeamDefinition` e do `TeamMember`), `TeamRelationMatrixEditor.cs`, `TeamRelationMaskDrawer.cs`, `TeamMatrixEditing.cs` (novos), `Tests/Teams/EditMode/SpellBook.Teams.Tests.EditMode.asmdef` (+Teams.Editor, como no TG5), `Tests/Teams/EditMode/TeamMatrixEditingTests.cs` (novo), `AsmdefRulesTests.cs` (`Teams` em `EditorModules`; referências exatas), `SpellBookEditorCoverageTests.cs` (+`SpellBook.Teams`), `CHANGELOG.md`.
- Testes red: `Mirror_CopiesAtoBIntoBtoA_Undoable`; `MirrorAll_MakesMatrixSymmetric`; `Clear_FallsBackToDefault`; `TeamsEditorAsmdef_References`; coverage com o Teams.
- Critério de pronto: testes verdes; CA17 (S) conferido pelo Examinador no host: a tabela, "Espelhar", quatro caixas na máscara e o `TeamMember` com time e matriz.
- Tamanho estimado: ~480 linhas; 10 arquivos. Depende de: TM3.

### Detalhe (2026-11-03) — TM4a (#201): tabela, "Espelhar" e editores
- Divisão de 2026-11-03: o TM4 passou de +30% (~697 linhas) e se divide em TM4a (aqui) e TM4b (a máscara, abaixo).
- CA: CA17 (S), a parte da tabela, do "Espelhar" e do `TeamMember` com time e matriz (a conferência no host fica com o Examinador); Design 3, Inspector e `TeamMatrixEditing`.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Editor/Teams/SpellBook.Teams.Editor.asmdef` (novo; Core, Teams e Core.Editor por GUID, como o dos Tags);
  - `Editor/Teams/TeamsEditors.cs` (novo: `TeamDefinitionEditor` com a faixa da cor antes do padrão e `TeamMemberEditor`, os dois `internal` derivados de `SpellBookEditor`);
  - `Editor/Teams/TeamMatrixEditing.cs` (novo, `public static`: `TryGet`, `Set`, `Mirror`, `MirrorAll`, `Clear` por `SerializedObject`, cada um num grupo de `Undo`; `Set` grava todas as células do par; `Clear` remove todas; `Mirror` copia `a → b` em `b → a` e limpa `b → a` se `a → b` não tem célula; `MirrorAll` copia, para cada par da lista, a célula do time que vem antes e, se ela não existe, a do outro). Os campos da matriz continuam privados; nada muda no runtime;
  - `Editor/Teams/TeamRelationMatrixEditor.cs` (novo: tabela depois do padrão, linhas = de e colunas = para, `EnumField` por célula, célula vazia mostra o padrão em cinza, botões "Espelhar" e "Limpar" por célula e "Espelhar tudo"; a tabela se refaz quando o objeto muda, inclusive no Undo);
  - `Tests/Teams/EditMode/SpellBook.Teams.Tests.EditMode.asmdef:8` (+`SpellBook.Teams.Editor`);
  - `Tests/Teams/EditMode/TeamMatrixEditingTests.cs` (novo; usa a `TeamsFactory`);
  - `Tests/Teams/EditMode/TeamsInspectorTests.cs` (novo: a tabela do editor da matriz; o TM4b soma os testes da máscara);
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:23` (`Teams` em `EditorModules`) e teste novo `TeamsEditorAsmdef_References` depois de `:315`;
  - `Tests/Core/EditMode/Inspector/SpellBookEditorCoverageTests.cs:21` (+`SpellBook.Teams`).
  - Fora, pela emenda de 2026-11-03 da 0007: `CHANGELOG.md` (sem quebra).
- Testes red (EditMode): `TeamMatrixEditingTests.Set_CreatesThenUpdatesCell`, `Mirror_CopiesAtoBIntoBtoA_Undoable`, `Mirror_EmptySource_ClearsTarget`, `MirrorAll_MakesMatrixSymmetric`, `Clear_FallsBackToDefault`; `TeamsInspectorTests.MatrixEditor_TableHasCellPerPair_EmptyCellShowsDefault`; `AsmdefRulesTests.TeamsEditorAsmdef_References` (e `AllExpectedAssembliesExist` com o `Teams.Editor`); `SpellBookEditorCoverageTests` com o Teams.
- Critério de pronto: os testes acima verdes com filtro, mais `TeamsValidationTests`, `TeamMemberTests`, `TeamRelationMatrixTests` e `AsmdefRulesTests` verdes; CA17 (tabela, "Espelhar" e `TeamMember`) conferido pelo Examinador no host.
- Tamanho: ~525 linhas e 9 arquivos, pela ADR 0042. Exceção: linha de 2026-11-03 do TM4a no `decisoes.md`.
- Commit: `feat(teams): Inspector da matriz com tabela e Espelhar`

### Detalhe (2026-11-03) — TM4b (#315): máscara (rascunho)
- Objetivo: drawer de todo `TeamRelationMask` com quatro caixas (Self, Aliado, Hostil e Neutro), Undo, multisseleção com valor misto e o fallback IMGUI de uma linha. Fecha o CA17 (quatro caixas na máscara).
- Rascunho do escopo: `Editor/Teams/TeamRelationMaskDrawer.cs` (novo, `public`) e `Tests/Teams/EditMode/TeamsInspectorTests.cs` (+`MaskDrawer_FourToggles_WriteFlags`, `MaskDrawer_AfterUndo_TogglesFollowMask`, `MaskDrawer_MultipleTargets_ShowsMixed_WritesOnlyTheFlag` e o `MaskHost` de teste). ~230 linhas e 2 arquivos. Depende de: TM4a. O Detalhe final sai quando o ticket entrar na fila.
- Commit: `feat(teams): drawer da máscara de relação`

### TM5 — Times no GAS: `TeamRelationCondition` e filtro de área
- Escopo: `Runtime/GAS.Bridges/SpellBook.GAS.Bridges.asmdef` (+Teams), `Runtime/GAS.Bridges/Teams/AscTeams.cs`, `TeamRelationCondition.cs` (novos), `Tests/GAS.Bridges/PlayMode/SpellBook.GAS.Bridges.Tests.PlayMode.asmdef` (+Teams), `Tests/GAS.Bridges/PlayMode/TeamRelationConditionTests.cs`, `TeamAreaFilterTests.cs` (novos), `AsmdefRulesTests.cs` (referências do Bridges e do teste), `times.md` (seção "Times no GAS", com "só inimigos" e "só aliados", e a regra da `HasTargetPointCondition` antes da de time quando o motivo "sem alvo" importa), `CHANGELOG.md`.
- Testes red:
  - `Member_OnSameObjectAndParent_Found_NoneIsNoTeam` (CA11);
  - `Condition_PassesAndFailsByMask_ReasonKey` (CA12, C9, como regra de ativação);
  - `Condition_NoTargetUnit_False_NoException_RelationKey` (emenda de 2026-11-01);
  - `Rules_HasTargetPointBeforeTeam_NoTargetFailsWithNoTargetKey` (emenda de 2026-11-01: sem alvo, a primeira regra decide com `gas.activation.no_target`);
  - `AreaFilter_OnlyHostiles_AlliesPlusSelf` (CA13, C10, com `AreaSelector` e filtro de candidato);
  - `NoTeamOnBothSides_UsesNoTeamRelation` (CA14);
  - `BridgesReferencesTeams_GasDoesNot` (CA15);
  - `Lookup_10000_Condition_10000_DoNotAllocate` (CA16, C11).
- Critério de pronto: CA11 a CA16 verdes; CA18 (E) conferido: página com o exemplo, `[HelpURL]` nos tipos, CHANGELOG com o R20.
- Tamanho estimado: ~480 linhas; 9 arquivos (a emenda de 2026-11-01 soma dois testes e nenhum arquivo). Depende de: TM2, AV2, AV3 (a `HasTargetPointCondition` do teste de regras), EV8 (com a emenda da ADR 0035).

### Detalhe (2026-11-03)
- CA: CA11, CA12, CA13, CA14, CA15, CA16 (parte: busca do membro e condição); CA9 (parte: máscara vazia, deixada pelo TM3); emenda de 2026-11-01.
- Escopo (caminhos relativos a `Packages/com.spellbook/`):
  - `Runtime/GAS.Bridges/SpellBook.GAS.Bridges.asmdef:4-10` (+GUID do `SpellBook.Teams`);
  - `Runtime/GAS.Bridges/Teams/AscTeams.cs` (novo, namespace `SpellBook.GAS.Bridges`: `GetMember(asc)` sobe de `asc.transform` pelos pais com `TryGetComponent<ITeamMember>`; nulo sem membro ou sem ASC);
  - `Runtime/GAS.Bridges/Teams/TeamRelationCondition.cs` (novo: `[Serializable] sealed`, `[TypeCategory("Time", Summary = ..., Line = "Relação com o alvo: {mask}")]`; o `Line` usa o nome do campo serializado, como os outros tipos; campo `mask = Hostile` com propriedade `Mask`; `Evaluate` como no Design 2; sem `Target.Unit`, falso; `DefaultFailureKey` = `teams.condition.relation`; `Validate`: máscara vazia é aviso em `<caminho>.mask`);
  - `Tests/Testing/TeamsFactory.cs:73` (+`AddMember(GameObject, team, matrix)`, para pôr o membro no objeto do ASC ou num pai; consequência direta: o `CreateMember` só cria objeto novo);
  - `Tests/GAS.Bridges/PlayMode/SpellBook.GAS.Bridges.Tests.PlayMode.asmdef:4-12` (+`SpellBook.Teams`);
  - `Tests/GAS.Bridges/PlayMode/TeamRelationConditionTests.cs` (novo, `#if UNITY_EDITOR` como os outros PlayMode com factory);
  - `Tests/GAS.Bridges/PlayMode/TeamAreaFilterTests.cs` (novo; `AreaSelector` com física 3D, no padrão do `AreaSelectorTests`);
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:245` (referências exatas do Bridges com `SpellBook.Teams`) e teste novo `BridgesReferencesTeams_GasDoesNot` depois de `:261`.
  - Fora, pela emenda de 2026-11-03 da 0007: `times.md` ("Times no GAS") e `CHANGELOG.md` (sem quebra). O CA18 (página e CHANGELOG com o R20) fica para o passe de documentação.
- Testes red:
  - PlayMode, `TeamRelationConditionTests`: `Member_OnSameObjectAndParent_Found_NoneIsNoTeam`, `Condition_PassesAndFailsByMask_ReasonKey`, `Condition_NoTargetUnit_False_NoException_RelationKey`, `Rules_HasTargetPointBeforeTeam_NoTargetFailsWithNoTargetKey`, `NoTeamOnBothSides_UsesNoTeamRelation`, `Validate_EmptyMask_Warns`, `Lookup_10000_Condition_10000_DoNotAllocate`;
  - PlayMode, `TeamAreaFilterTests`: `AreaFilter_OnlyHostiles_AlliesPlusSelf`;
  - EditMode, `AsmdefRulesTests`: `BridgesReferencesTeams_GasDoesNot` e `AsmdefRules_BridgesDoesNotReferencePooling` (lista com o Teams).
- Critério de pronto: os testes acima verdes com filtro (total do XML igual ao número de testes); `AsmdefRulesTests` inteiro verde (mapa de dependências com o Bridges → Teams).
- Tamanho: ~455 linhas e 8 arquivos, pela ADR 0042. Exceção: nenhuma.
- Commit: `feat(gas): TeamRelationCondition e filtro de área por time`

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| TM1 | sim | — | ~430 / 10 |
| TM2 | sim | TM1 | ~420 / 8 |
| TM3 | sim | TM2 | ~300 / 6 |
| TM4 | sim | TM3 | ~480 / 10 |
| TM5 | sim | TM2, AV2, AV3, EV8 (emenda 0035) | ~480 / 9 |

Fila: TM1 → TM4 a qualquer momento (não esperam o Lote B nem o C). TM5 depois do EV8. CA10 e CA15 rodam em todos.

## Perguntas ao usuário

- **Q1. Identificador do time (R1, N4).** Proposta: o asset é o identificador, sem id próprio. A referência da Unity não muda ao renomear nem ao mover, e o validador acusa time repetido. Alternativa: um `string id` serializado, como o R1 descreve, com o validador acusando id duplicado (Ctrl+D copia o id). Seriam duas identidades para o mesmo time.
- **Q2. Filtro de candidato único (ADR 0035).** Proposta: o time entra como `TeamRelationCondition` no filtro de candidato (`Condition<ConditionContext>`) do seletor de área, do impacto do projétil e da aura. A `TagQueryCondition` ganha o sujeito "alvo", e o `AreaSelector` perde o `GameplayTagQuery` próprio. Isso emenda o EV4b e o EV8 da Spec aprovada do B-012. Alternativa: manter o `GameplayTagQuery` no seletor e acrescentar uma lista de filtros própria do seletor para o time. Seriam dois mecanismos de filtro no mesmo seletor, e o projétil e a aura precisariam de outro gancho.
- A ADR 0035 passa pelo gate junto com esta Spec.
