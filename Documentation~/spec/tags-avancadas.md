# Spec: Tags avançadas
PRD: [Documentation~/prd/tags-avancadas.md](../prd/tags-avancadas.md) · Backlog: B-006 · ADRs: nenhuma nova (usa 0003, 0004, 0007, 0012, 0026)

Status: Aprovada no gate de 2026-10-31 (TG1–TG5; validação pelo padrão atual da 0026, sem ADR 0029) · Data: 2026-10-31 · Autor: Arquiteto

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`.

- `Runtime/Tags/GameplayTag.cs:6-28`: `struct` serializável com um `[SerializeField] private GameplayTagDefinition definition`. `Matches` (`:16-17`) exige as duas tags válidas e delega para a definição. `GetDefinition()` é `internal` (`:27`). Igualdade por referência da definição (`:19`).
- `Runtime/Tags/GameplayTagDefinition.cs:6-17`: `ScriptableObject` com `[SerializeField] private GameplayTagDefinition parent` e `Matches` recursivo (`this == other` ou o pai casa). Sem acesso público ao pai.
- `Runtime/Tags/SpellBook.Tags.asmdef`: referencia só o Core (0012). Não há `InternalsVisibleTo` no Tags, então o assembly de testes `SpellBook.Tags.Tests.EditMode` só enxerga a API pública (útil para o CA17).
- `Editor/Tags/TagsEditors.cs:7-9`: só o Inspector de uma linha da definição. `Editor/Tags/SpellBook.Tags.Editor.asmdef` existe.
- `Tests/Tags/EditMode/GameplayTagTests.cs`: cria definições com `CreateInstance` e grava o pai por `SerializedObject` (`:134-150`). O asmdef de testes referencia `SpellBook.Tags`, `SpellBook.Time`, `SpellBook.Testing` e o TestRunner; não referencia `SpellBook.Core` nem `SpellBook.Tags.Editor`.
- Validação (0026, item 6): `Runtime/Core/Validation/IValidatable.cs`, `ValidationResult.cs:10-64` (`AddError`/`AddWarning` com `propertyPath`). O padrão para tipos que não são `UnityEngine.Object` é "o dono repassa a chamada" (0026, linhas 243-246), com sobrecarga própria quando o tipo precisa de contexto.
- `[HelpURL]`: `Editor/Core/HelpUrlRule.cs:66-70` só cobra `MonoBehaviour` e `ScriptableObject`; `HelpURLAttribute` só se aplica a classes. O teste da 0007 (CA54 da 0-M) cobra que todo `[HelpURL]` aponte para uma âncora existente do manual.
- Alocação: os testes do pacote usam `Is.Not.AllocatingGCMemory()` (`Tests/Events/EditMode/EventBusTests.cs:121`).
- Manual: `Documentation~/manual/modulos/tags.md` (âncoras `#gameplaytagdefinition`, `#hierarquia`, `#gameplaytag`, `#matches`, `#armadilhas`); `DocUrls.Tags` em `Runtime/Core/DocUrls.cs:18`.

## Respostas às perguntas técnicas do PRD (seção 8)

- **P3. Igualdade do conjunto.** Nenhuma sobrecarga de `==`, `Equals` ou `GetHashCode`: o conjunto é mutável, e hash de objeto mutável quebra dicionários. A comparação de conteúdo que o R7 pede é `a.HasAll(b) && b.HasAll(a)` (hierárquica) ou, exata, `Count` igual e `HasTagExact` de cada uma. Se uma fase futura precisar detectar mudança, a ferramenta certa é um contador de versão, a ser pedido por ela (B-003 ou UI), não igualdade.
- **P4. Representação interna.** `GameplayTagContainer` é uma **classe** `sealed` e `[Serializable]`, com `[SerializeField] private List<GameplayTag> tags`. Motivos: (1) um `struct` com uma `List` dentro compartilha a lista entre cópias e surpreende quem edita uma cópia; (2) `[HelpURL]` só se aplica a classes (R18). A Unity serializa a classe em linha (não é `[SerializeReference]`), então dois campos `GameplayTagContainer` no mesmo componente são independentes, cada um com o seu caminho de propriedade, e copiar e colar o componente copia os valores. A enumeração sem alocação usa um `struct Enumerator` público (padrão `foreach` por duck typing); a interface `IReadOnlyList<GameplayTag>` também é implementada, para indexação e para o gancho abaixo, e só aloca se alguém enumerar **pela interface**.
- **P5. Onde moram as listas da consulta.** Em três `GameplayTagContainer` (`All`, `Any`, `None`). A consulta reaproveita `HasAll`/`HasAny` do conjunto, e o drawer da consulta reaproveita o drawer do conjunto, só trocando o rótulo ("Todas", "Alguma", "Nenhuma").

## Design

### 1. Acesso à definição (R13, R14)

- `GameplayTag.GetDefinition()` passa de `internal` a `public`. Devolve `null` para tag inválida.
- `GameplayTagDefinition` ganha `public GameplayTagDefinition Parent => parent;`. O `Matches` não muda.

### 2. `GameplayTagContainer` (R3 a R8)

```csharp
namespace SpellBook.Tags
{
    [Serializable]
    [HelpURL(DocUrls.Tags + "#gameplaytagcontainer")]
    public sealed class GameplayTagContainer : IReadOnlyList<GameplayTag>
    {
        public GameplayTagContainer();
        public GameplayTagContainer(IEnumerable<GameplayTag> tags);   // R8: ignora repetidas e inválidas
        public GameplayTagContainer(params GameplayTag[] tags);       // R8

        public int Count { get; }
        public bool IsEmpty { get; }
        public GameplayTag this[int index] { get; }

        public bool HasTag(GameplayTag tag);                          // hierárquico (R1)
        public bool HasTagExact(GameplayTag tag);                     // igualdade
        public bool HasAny(IReadOnlyList<GameplayTag> tags);          // null ou vazio: false
        public bool HasAll(IReadOnlyList<GameplayTag> tags);          // null ou vazio: true

        public bool Add(GameplayTag tag);                             // false se inválida ou já presente (exato)
        public bool Remove(GameplayTag tag);                          // exato; false se não existia
        public void Clear();

        public Enumerator GetEnumerator();                            // struct, ordem de inserção
        public struct Enumerator { public GameplayTag Current { get; } public bool MoveNext(); }

        public void Validate(ValidationResult result, string propertyPath);   // ver 4
    }
}
```

- **Regra de casamento (R1, R2):** `HasTag(t)` é "alguma tag `x` do conjunto com `x.Matches(t)`". `HasAny(q)` é "alguma `t` de `q` com `HasTag(t)`". `HasAll(q)` é "toda `t` de `q` com `HasTag(t)`". Como `GameplayTag.Matches` já devolve falso com qualquer lado inválido, tag inválida não casa com nada, inclusive com outra inválida (CA2). `HasTagExact` também devolve falso para tag inválida.
- **Parâmetro `IReadOnlyList<GameplayTag>`:** aceita outro `GameplayTagContainer`, uma `List<GameplayTag>` e um array, sem cópia. É o **gancho para o B-003 e o B-009**: os campos do GAS são `List<GameplayTag>` (P1 do PRD: não migram agora), e o B-003 (R4 dele) precisa perguntar "a consulta casa com as tags concedidas pelo efeito" sem alocar um conjunto por efeito. A implementação percorre por índice (`for`), nunca por `foreach` na interface, para não alocar.
- **Sem alocação (N1, C7):** todas as consultas são laços por índice; `HasTagExact` usa `List<GameplayTag>.Contains`, que com `IEquatable<GameplayTag>` não faz boxing. O `Enumerator` embrulha o `List<GameplayTag>.Enumerator` (lança se o conjunto mudar durante o `foreach`, como a `List`).
- **Invariante "sem repetição":** garantida por `Add` e pelos construtores. Dados serializados editados no Inspector ou no YAML podem ter repetidas ou vazias. Nesse caso o conjunto **não** se corrige sozinho (nada de `OnAfterDeserialize`, que brigaria com o Inspector enquanto o designer edita): as consultas continuam corretas com repetidas, a inválida nunca casa, `Count` conta o que está guardado e o validador aponta (seção 4).
- **Serialização (N3):** campo `tags` inicializado com `new List<GameplayTag>()`; `JsonUtility` e o Inspector usam o mesmo caminho (CA9).

### 3. `GameplayTagQuery` (R9 a R12)

```csharp
namespace SpellBook.Tags
{
    [Serializable]
    [HelpURL(DocUrls.Tags + "#gameplaytagquery")]
    public sealed class GameplayTagQuery
    {
        public GameplayTagContainer All { get; }    // campo [SerializeField] private all = new()
        public GameplayTagContainer Any { get; }
        public GameplayTagContainer None { get; }

        public bool IsEmpty { get; }                              // as três vazias
        public bool Matches(IReadOnlyList<GameplayTag> tags);     // conjunto, lista ou array; null = vazio
        public bool Matches(GameplayTag tag);                     // R11, sem alocação

        public void Validate(ValidationResult result, string propertyPath);   // ver 4
    }
}
```

- `Matches(tags)` = `Covers(tags, All)` e (`Any.IsEmpty` ou `Intersects(tags, Any)`) e não `Intersects(tags, None)`, com `Covers` e `Intersects` sendo `HasAll` e `HasAny` aplicados ao lado de `tags` (a tag de `tags` é a filha: `t.Matches(condição)`). Lista vazia não restringe (R10).
- `Matches(GameplayTag tag)` aplica as mesmas três regras com um único elemento, sem montar conjunto: toda `c` de `All` com `tag.Matches(c)`; `Any` vazia ou alguma `c` com `tag.Matches(c)`; nenhuma `c` de `None` com `tag.Matches(c)`. O CA14 compara com `Matches(new GameplayTagContainer(tag))`.
- Montagem em código: `query.All.Add(tag)`; as três propriedades nunca são nulas em objeto criado por código. A consulta é só dados, sem texto nem expressão (R12).
- Para `Matches` aceitar um `GameplayTagContainer` sem ambiguidade, a sobrecarga com `IReadOnlyList<GameplayTag>` basta (o conjunto implementa a interface). Chamadas com `List<GameplayTag>` também resolvem para ela.

### 4. Validação (R16)

Segue o padrão da 0026 (item 6): o conjunto e a consulta **não** são `UnityEngine.Object`, então expõem `Validate(ValidationResult result, string propertyPath)` e o **dono** (um `MonoBehaviour` ou `ScriptableObject` que implemente `IValidatable`) repassa a chamada com o nome do campo. As mensagens saem com o `Context` do dono (o objeto clicável na janela) e o `propertyPath` do campo.

| Regra | Severidade | Texto (exemplo) |
|---|---|---|
| Tag vazia no conjunto | Erro | `Tag vazia em {campo} (posição {i}). Escolha uma tag ou remova a linha.` |
| Tag repetida no conjunto | Aviso | `Tag '{nome}' repetida em {campo}. Remova a repetição.` |
| Mesma tag em duas listas da consulta | Erro | `Tag '{nome}' está em {lista A} e {lista B} de {campo}.` |
| Consulta totalmente vazia | Aviso | `A consulta {campo} está vazia e casa com qualquer conjunto.` |

A consulta valida as suas três listas pelo `Validate` do conjunto (com `propertyPath` + `.all`, `.any`, `.none`) e depois a regra entre listas e a de vazia. "Mesma tag" é igualdade exata; um pai em "Todas" e um filho em "Nenhuma" é legítimo e não é apontado. `Validate` não muda estado, não escreve no Console e não usa `UnityEditor` (0026).

Consequência: um campo `GameplayTagContainer` num componente do usuário só é validado se o componente implementar `IValidatable` e repassar. Nenhum tipo do pacote tem esses campos ainda (P1 do PRD). O manual mostra o repasse em três linhas. Ver a pergunta Q1 ao usuário para a alternativa genérica.

Referência nova: `SpellBook.Tags` já referencia o Core, então usa `SpellBook.Core.Validation` sem mudar asmdef.

### 5. Drawers (R15)

No `SpellBook.Tags.Editor`, dois `PropertyDrawer` em UI Toolkit (`CreatePropertyGUI`), sem IMGUI (I46):

- **`GameplayTagContainerDrawer`**: um cabeçalho com o rótulo do campo, a contagem e um botão de ajuda que abre o `[HelpURL]` do tipo; abaixo, um `ListView` ligado a `tags`, reordenável, com cada item como `ObjectField` de `GameplayTagDefinition` ligado ao `definition` do elemento (em vez do foldout padrão do `struct`). O "+" acrescenta uma **linha vazia**, não uma cópia da última (comportamento padrão de array, que criaria repetida). A API exata do `ListView` para trocar o "+" é verificada pela Fábrica na 6000.0 antes de usar.
- **`GameplayTagQueryDrawer`**: um `Foldout` com o rótulo do campo e o botão de ajuda; dentro, três `PropertyField` para `all`, `any` e `none`, com os rótulos "Todas", "Alguma" e "Nenhuma", desenhados pelo drawer do conjunto.

Os drawers não validam nem colorem (0026, "O que os agentes NÃO podem fazer": validar no Inspector em vez de `IValidatable`). Se o drawer falhar, a Unity mostra os campos serializados padrão (`tags`, `all`, `any`, `none`), sem perda de dados.

### 6. Ganchos para o Lote B

- **B-003 (R3, R4 dele):** busca de efeitos por conjunto e por consulta sobre `GameplayEffect.GrantedTags` (`List<GameplayTag>`) sem alocar, pelas sobrecargas `IReadOnlyList<GameplayTag>`. O `HasMatching` no ASC continua fora (P2 do PRD).
- **B-004:** usa `GameplayTag` e `GameplayTagContainer` como estão; nada a mais.
- **B-005 e B-007:** não usam os tipos novos. Nenhum gancho.
- **B-009 (condições, fora do Lote B):** `TagQueryCondition` monta sobre `GameplayTagQuery.Matches(IReadOnlyList<GameplayTag>)` e sobre a regra de validação do item 4.

### 7. O que não muda

- `GameplayTag.Matches`, a igualdade e o formato serializado de `GameplayTag` e de `GameplayTagDefinition`.
- Nenhum campo do GAS troca de tipo (P1 do PRD). O ASC não ganha sobrecarga (P2).
- `SpellBook.Tags` continua referenciando só o Core (R17, CA21).

## Quebras de compatibilidade

Nenhuma. Só acréscimos: `GetDefinition()` público, `Parent`, `GameplayTagContainer`, `GameplayTagQuery`. Entram no CHANGELOG em "Adicionado".

## Tickets

Todos usam a Unity (testes EditMode no host) e seguem o WIP 1. Não há par paralelo sem Editor nesta Spec. Cada ticket atualiza `Documentation~/manual/modulos/tags.md` (com a âncora do `[HelpURL]` que criar, para o teste de âncoras da 0007 passar) e o `CHANGELOG.md`; os dois contam só no limite de arquivos (0003).

Testes de tags criam definições por factory (`CreateInstance`, N5). Para não repetir o `CreateDefinition` privado de `GameplayTagTests.cs:134-150` em cada classe, o TG2 extrai um `TagTestFactory` interno (`Tests/Tags/EditMode/TagTestFactory.cs`, `IDisposable`, com `Create(string name, GameplayTagDefinition parent = null)`), usado pelos testes novos. O `GameplayTagTests` antigo não é editado.

### TG1 — `GetDefinition` público e `Parent`
- Escopo: `Runtime/Tags/GameplayTag.cs`, `Runtime/Tags/GameplayTagDefinition.cs`, `Tests/Tags/EditMode/TagDefinitionAccessTests.cs` (novo), `Documentation~/manual/modulos/tags.md` (seção "Ferramentas próprias", com o exemplo do C6), `CHANGELOG.md`.
- Testes red (o arquivo de teste não compila antes: é o red da 0004 para API nova):
  - `GetDefinition_ReturnsDefinitionUsedToCreate` e `GetDefinition_InvalidTag_ReturnsNull` (CA16);
  - `Parent_RootIsNull`, `Parent_WalkChainUsingPublicApiOnly` (CA17; o assembly de testes não tem `InternalsVisibleTo`, então compila só se a API for pública).
- Critério de pronto: CA16 e CA17 verdes; `GameplayTagTests` sem edição; CA21 (assemblies) verde.
- Tamanho estimado: ~90 linhas; 5 arquivos.
- Depende de: nada.

### TG2 — `GameplayTagContainer`
- Escopo: `Runtime/Tags/GameplayTagContainer.cs` (novo), `Tests/Tags/EditMode/TagTestFactory.cs` (novo), `Tests/Tags/EditMode/GameplayTagContainerTests.cs` (novo), `Documentation~/manual/modulos/tags.md` (seção `#gameplaytagcontainer`, com a regra do R1), `CHANGELOG.md`.
- Testes red:
  - `HasTag_Hierarchical` (CA1, C1) e `HasTagExact_NoHierarchy` (CA5, C2);
  - `InvalidTag_NeverMatches_InAnyMethod` (CA2, parte do conjunto);
  - `Add_DuplicateOrInvalid_ReturnsFalse_Unchanged`, `Add_New_ReturnsTrue` (CA3);
  - `Remove_OnlyIfPresent`, `RemoveParent_KeepsChildren` (CA4);
  - `HasAny_HasAll_FourCasesOfC3`, `HasAll_Empty_IsTrue_HasAny_Empty_IsFalse` (CA6);
  - `HasAll_HasAny_IgnoreOrder` (CA7);
  - `Foreach_InsertionOrder` (CA8);
  - `JsonRoundTrip_SameTagsSameOrder` (CA9; via um `[Serializable]` de teste que segura o conjunto, porque `JsonUtility` não serializa referência a `ScriptableObject` fora de objeto da Unity: a Fábrica usa `EditorJsonUtility` se o `JsonUtility` não aceitar a referência, e registra a troca no PR);
  - `FromList_DropsDuplicatesAndInvalid` (CA10);
  - `Queries_DoNotAllocate_10000Times` e `Foreach_DoesNotAllocate` (CA22, parte do conjunto);
  - `AcceptsListAndArray_AsArgument` (gancho do B-003).
- Critério de pronto: CA1 a CA10 verdes; CA22 verde para o conjunto; `[HelpURL]` com âncora existente (teste da 0007 verde).
- Tamanho estimado: ~430 linhas (tipo ~150, testes ~280); 5 arquivos.
- Depende de: nada (TG1 antes só pela fila).

### TG3 — `GameplayTagQuery`
- Escopo: `Runtime/Tags/GameplayTagQuery.cs` (novo), `Tests/Tags/EditMode/GameplayTagQueryTests.cs` (novo), `Documentation~/manual/modulos/tags.md` (seção `#gameplaytagquery` com o exemplo C4), `CHANGELOG.md`.
- Testes red:
  - `ExampleC4_ThreeContainers` (CA11);
  - `EmptyQuery_MatchesEmptyAndAny_IsEmptyTrue` (CA12, C5);
  - `OnlyAll`, `OnlyAny`, `OnlyNone` (CA13);
  - `MatchesTag_EqualsMatchesSingleContainer` (CA14, com vários casos);
  - `Hierarchy_ChildSatisfiesAllAndAny_BlockedByNoneViaParent` (CA15);
  - `InvalidTag_NeverMatches_InQuery` (CA2, parte da consulta);
  - `Matches_DoesNotAllocate_10000Times` (CA22, parte da consulta).
- Critério de pronto: CA11 a CA15 verdes; CA2 e CA22 completos; `[HelpURL]` com âncora existente.
- Tamanho estimado: ~330 linhas; 4 arquivos.
- Depende de: TG2.

### TG4 — Validação do conjunto e da consulta
- Escopo: `Runtime/Tags/GameplayTagContainer.cs`, `Runtime/Tags/GameplayTagQuery.cs`, `Tests/Tags/EditMode/TagValidationTests.cs` (novo; um `ScriptableObject` de teste com um conjunto e uma consulta que implementa `IValidatable` e repassa), `Tests/Tags/EditMode/SpellBook.Tags.Tests.EditMode.asmdef` (acrescenta `SpellBook.Core`; conferir a lista permitida do `AsmdefRulesTests`), `Documentation~/manual/modulos/tags.md` (seção "Validação", com o repasse em três linhas) e `Documentation~/manual/ferramentas/validador.md` (as regras novas), `CHANGELOG.md`.
- Testes red:
  - `EmptyTagInContainer_IsError_WithPropertyPath`;
  - `DuplicateTagInContainer_IsWarning`;
  - `SameTagInTwoQueryLists_IsError`, `ParentInAllChildInNone_IsNotReported`;
  - `EmptyQuery_IsWarning`;
  - `ProjectValidator_ReportsOwnerAsContext` (CA19: `ProjectValidator.Validate` sobre o holder; o `Context` da mensagem é o holder).
- Critério de pronto: CA19 verde; `Validate` sem `UnityEditor` e sem Console (revisão).
- Tamanho estimado: ~260 linhas; 7 arquivos.
- Depende de: TG3.

### TG5 — Drawers em UI Toolkit
- Escopo: `Editor/Tags/GameplayTagContainerDrawer.cs` (novo), `Editor/Tags/GameplayTagQueryDrawer.cs` (novo), `Tests/Tags/EditMode/TagDrawerTests.cs` (novo), `Tests/Tags/EditMode/SpellBook.Tags.Tests.EditMode.asmdef` (acrescenta `SpellBook.Tags.Editor`; se o `AsmdefRulesTests` exigir assembly de teste de editor separado, a Fábrica para e devolve), `Documentation~/manual/modulos/tags.md` (seção "No Inspector", com imagem opcional), `CHANGELOG.md`.
- Testes red:
  - `ContainerDrawer_BuildsListBoundToTags` (o `CreatePropertyGUI` sobre um `SerializedProperty` de um holder de teste devolve um `ListView` com `bindingPath` em `tags`);
  - `ContainerDrawer_AddCreatesEmptyRow_NotCopy`;
  - `QueryDrawer_ShowsLabelsTodasAlgumaNenhuma`;
  - `Drawers_HelpButtonUsesTypeHelpUrl`.
- Critério de pronto: testes verdes; CA18 (S) conferido pelo Examinador no host com um componente temporário com dois `GameplayTagContainer` e um `GameplayTagQuery` (script de rascunho fora do diff, apagado depois): adicionar, remover, reordenar, rótulos, dois campos independentes, copiar e colar o componente, recarregar scripts sem perder dados (N3). CA20 (E): os dois tipos com `[HelpURL]` e a página com conteúdo real.
- Tamanho estimado: ~380 linhas; 6 arquivos.
- Depende de: TG3 (TG4 antes só pela fila; os dois tocam o mesmo asmdef de teste, então não andam juntos).

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| TG1 | sim | — | ~90 / 5 |
| TG2 | sim | — | ~430 / 5 |
| TG3 | sim | TG2 | ~330 / 4 |
| TG4 | sim | TG3 | ~260 / 7 |
| TG5 | sim | TG3 | ~380 / 6 |

Fila proposta: TG1 → TG2 → TG3 → TG4 → TG5. As duas Specs do Lote A não dependem uma da outra; a fila entre elas é do Controlador. CA21 (assemblies) roda em todos os tickets.
