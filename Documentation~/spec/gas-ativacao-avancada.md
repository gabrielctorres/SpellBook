# Spec: Ativação avançada (condições e cargas)
PRD: [Documentation~/prd/gas-ativacao-avancada.md](../prd/gas-ativacao-avancada.md) · Backlog: B-009 · ADRs: **0032 (nova, Proposta)**; usa 0003, 0004, 0007, 0009, 0012, 0013, 0014, 0019, 0022, 0023, 0026, 0030

Status: Aprovada no gate de 2026-10-31 (AV1–AV9; ADR 0032 aceita; Q1: sim, motivo na `ActivationRule` e `Condition` pura; Q2: sim, `CastRange` na habilidade já no B-009) · Data: 2026-10-31 · Autor: Arquiteto

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`. As linhas são as de hoje; os Lotes A e B mudam parte delas antes deste item.

- `Runtime/GAS/Core/AbilitySystemComponent.cs:165-180`: `GetActivationResult(ability)` testa válida, concedida, ativa, `BlockedTags`, `RequiredTags`, bloqueio por habilidade ativa, recarga e custo, e devolve o enum. `:182-204`: `TryActivateAbility(ability|spec, Vector3)` chama a consulta e dispara `OnAbilityFailed(GameplayAbility, AbilityActivationResult)` (`:37`). `:248-278`: `ActivateSpec` faz o commit (recarga e custo) antes das ações.
- `:216-231`: `CanAffordCost` (o VN5 passa a avaliar com nível e habilidade). `:422-444`: `HasTag`, `HasAllTags`, `HasAnyTag`.
- `Runtime/GAS/Abilities/AbilityActivationResult.cs:6-17`: 9 valores, serializado como inteiro.
- `Runtime/GAS/Abilities/AbilitySpec.cs:10-25`: estado por personagem (`Level`, `IsActive`, `ActiveContext`). O EA5 cria `CooldownTracked`; o VN4 fecha o `set` de `Level`.
- `Runtime/GAS/Abilities/GameplayAbility.cs:75-86`: `GetCooldownDuration` com o piso e o `Clamp01`; o EA6 troca pela `CooldownReductionPolicy` e o VN2/VN5 pela `Duration.Evaluate(nível, tabela)`.
- `Runtime/GAS/Abilities/Actions/FlowActions.cs:75-116`: `ConditionalAction.ConditionType` (fica até o B-012, R8).
- `Runtime/Core/Inspector/TypeCategoryAttribute.cs:8-21`: `Path` e `Summary` (dica do seletor). `Editor/Core/Inspector/TypePickerCatalog.cs:56` lista por `TypeCache.GetTypesDerivedFrom(tipoDoCampo)` e `:133` exclui tipos genéricos.
- `SpellBook.Tags.asmdef` referencia só o Core: o Core não pode usar `GameplayTag` (ADR 0032).
- Do Lote A e B, este item usa: `GameplayTagContainer`/`GameplayTagQuery` (TG2, TG3), `OnAbilityGranted`, `OnCooldownChanged` e `Reduce/Reset/RestartCooldown` (EA5), `CooldownReductionPolicy` (EA6), `ScalableFloat` e `AbilityValues` (VN1, VN4), `EffectContext` e `ModifierValueSource.GetValue(in EffectContext)` (VN5, VN6), `OnAbilityLevelChanged` (VN4), o par `abilityFailed` do relay (RL4), o `GameplayAbilityEditor` (VN10), `validador.md` (AR6, VN9).
- Sample: `Samples~/Sandbox/Scripts/SandboxCaster.cs:40-46` assina `OnAbilityFailed`.

## Respostas às perguntas técnicas do PRD (seção 8)

- **P4. Evento de falha.** **Um só evento**, com a assinatura nova: `event Action<GameplayAbility, ActivationFailure> OnAbilityFailed`. O `ActivationFailure` traz o enum em `Result`, então nada se perde; manter os dois seria um evento paralelo (I1, sem fachada). Quebra listada abaixo. O relay (RL4) acompanha, com o payload ampliado de forma aditiva.
- **P5. Contexto genérico.** ADR 0032: `Condition<TContext>` no Core com as composições como bases genéricas; o GAS fecha as três sobre um contexto único, `ConditionContext`. O motivo de falha fica na regra de ativação (tag e chave), não na condição; o Core só conhece a chave padrão (texto). O `[SerializeReference]` com base genérica fechada já funciona no pacote (o Sample serializa `EventEmission<Void>`); o seletor só oferece tipos concretos não genéricos, por isso as composições são fechadas no GAS.
- **P6. Cargas e controle de recarga.** A recarga de carga é **estado do `AbilitySpec`**, não efeito ativo (ADR 0032, item 6): não concede tag, não aparece em `ActiveEffects` e não dispara `OnCooldownChanged`. `ReduceCooldown`, `ResetCooldown` e `RestartCooldown` agem sobre tudo que barra a habilidade por tempo: o efeito `Cooldown` (como no EA5) **e**, com cargas, a recarga da carga em curso. Regras no Design 5.
- **P7. Filtro do seletor.** É o tipo do campo: `List<ActivationRule>` com `[SerializeReference] Condition<ConditionContext>` só oferece condições do GAS (ADR 0032, item 3). Nada a mais.
- **P8. Condições e custo.** Confirmado: o custo só é **checado** na posição 3 (`CanAffordCost`, sem aplicar); condições nunca consomem (são puras, ADR 0019 e 0032); carga, custo e recarga saem só no commit (`ActivateSpec`), depois de todas as checagens.
- **P9. `%nome` de `MaxCharges`.** O máximo é avaliado (sem alocar: `ScalableFloat` com o cache do VN4) na concessão, a cada mudança de nível (`OnAbilityLevelChanged`) e a cada `Tick` para specs com cargas (cobre a fonte por atributo sem assinatura). O valor fica em `AbilitySpec.MaxCharges`. A consulta lê `min(Charges, MaxCharges)` sem escrever; o limite e o evento saem no `Tick` (Design 5).

## Design

### 1. Base `Condition` no Core (R1 a R3; ADR 0032)

`Runtime/Core/Conditions/Condition.cs`:

```csharp
namespace SpellBook.Core.Conditions
{
    [Serializable]
    public abstract class Condition<TContext> where TContext : struct
    {
        public abstract bool Evaluate(in TContext context);
        public virtual string DefaultFailureKey => null;           // chave de texto (0023); nunca texto pronto
        public virtual void Validate(ValidationResult result, string propertyPath) { }
    }
}
```

`Runtime/Core/Conditions/CompositeConditions.cs`, bases abstratas genéricas com `[SerializeReference] protected List<Condition<TContext>> conditions` (ou `condition`, no Not):
- `AndConditionBase<TContext>`: verdadeira se todas são; vazia é verdadeira; entrada nula é ignorada.
- `OrConditionBase<TContext>`: verdadeira se alguma é; vazia é falsa; `Validate` avisa lista vazia.
- `NotConditionBase<TContext>`: inverte; condição nula é falsa e erro no `Validate`.
- Laços por índice, sem alocação; o `Validate` de cada uma repassa às filhas com o `propertyPath` de cada item.

### 2. Resumo de uma linha (base para o R22 aqui e para o DS do B-012)

- `TypeCategoryAttribute` ganha `public string Line { get; set; }`: modelo do resumo, com `{Campo}` (nome do campo serializado) e `{Campo?texto}` (texto quando o `bool` é verdadeiro). Exemplo: `[TypeCategory("Atributo", Line = "{Attribute} {Comparison} {Value}")]`.
- `Editor/Core/Inspector/SummaryFormatter.cs` (`public static`, no `SpellBook.Core.Editor`, porque o `SpellBook.GAS.Editor` e o `SpellBook.GAS.Bridges.Editor` o chamam): `string Format(SerializedProperty managedReference)`. Por tipo do campo: número com até 2 casas na cultura do Editor; `ScalableFloat` (detectado pelo campo `mode`) mostra a constante, `%nome` no modo `Table` e "por nível" em `Array`/`Curve`; referência de objeto mostra o nome ou "(nenhum)"; enum mostra o nome exibido; lista mostra a contagem; referência gerenciada aninhada usa o `Line` dela (recursivo, até 3 níveis). Sem `Line`, devolve o nome exibido do tipo. Sem reflexão sobre a instância: só `SerializedProperty`.
- Respondido aqui porque as condições do B-009 já precisam (R22); o B-012 usa o mesmo para as ações (P-DS1, ADR 0034, item 9).

### 3. Condições do GAS (R4 a R7)

`Runtime/GAS/Abilities/AbilityTarget.cs`: `public readonly struct AbilityTarget { bool HasPoint; Vector3 Point; static None; static AtPoint(Vector3) }`. O B-012 acrescenta a unidade.

`Runtime/GAS/Conditions/ConditionContext.cs`: `public readonly struct ConditionContext` com `Owner` (ASC), `Ability`, `Level`, `Spec` (pode ser nulo), `Target` (`AbilityTarget`) e `Targets` (`IReadOnlyList<AbilitySystemComponent>`; nunca nulo: na ativação do B-009 é uma lista vazia estática, porque ainda não há unidade-alvo). Somente leitura: as condições não recebem nada que altere o ASC além da referência a ele, e a revisão confere que nenhuma condição do pacote chama método que muda estado (CA5 por teste de estado antes e depois).

`Runtime/GAS/Conditions/GasConditions.cs`: `AndCondition`, `OrCondition`, `NotCondition` (`[Serializable] sealed`, fecham as bases do Core sobre `ConditionContext`, com `[TypeCategory("Lógica", Line = ...)]`).

Condições prontas (`[Serializable] sealed`, `[TypeCategory]` com `Line`, `DefaultFailureKey` no formato `gas.activation.<nome>`; o formato definitivo das chaves é do B-008):
- Emenda de 2026-10-31 (ADR 0035, gate do Lote D): a `TagQueryCondition` ganha `ConditionSubject` (`Owner`, padrão, ou `TargetUnit`) no EV8 do B-012, quando `AbilityTarget.Unit` existe. O AV2 não muda.
- `HasTargetsCondition` (`Targets.Count > 0`, ignorando destruídos).
- `TagQueryCondition`: `GameplayTagQuery query` (TG3); `Owner.HasMatching(query)`.
- `AttributeThresholdCondition`: `AttributeDefinition attribute`, `Comparison comparison` (`Greater`, `GreaterOrEqual`, `Less`, `LessOrEqual`, `Equal`, com tolerância 1e-4 no `Equal`), `ModifierValueSource value` (VN6: `Value` com `ScalableFloat`, `Attribute`, `Formula`; o validador acusa `SetByCaller` e `TargetAttribute`), `bool useRatio` (valor ÷ máximo efetivo do atributo, B-005; máximo ≤ 0 dá falso). O valor é avaliado com um `EffectContext` montado na hora (instigador e alvo = `Owner`, habilidade e nível do contexto), por `ModifierValueSource.GetValue(in EffectContext)`. Atributo ausente no ASC: falso, sem aviso.
- `HasTargetPointCondition`: verdadeira se `Target.HasPoint`. `DefaultFailureKey` = `gas.activation.no_target`. No B-012, com a unidade-alvo, o ponto passa a ser a posição da unidade quando não vem explícito, então a condição continua valendo sem mudar.
- `CastRangeCondition`: mede do `Owner.transform.position` ao `Target.Point` contra `Ability.CastRange` avaliado no `Level` (fonte única do alcance; R29 do B-012 usa o mesmo campo). `CastRange` ≤ 0 é "sem limite" e dá verdadeiro, com ou sem ponto. Com alcance > 0 e sem ponto, dá falso, sem exceção. `DefaultFailureKey` = `gas.activation.cast_range`. Unidade e borda: B-012.
- Emenda de 2026-11-01 (pergunta da Fábrica no AV3): a chave de uma condição é fixa (`DefaultFailureKey` não recebe contexto, ADR 0032, item 4), então a `CastRangeCondition` não pode responder `no_target` numa falha e `cast_range` na outra. A falta de alvo virou a condição própria `HasTargetPointCondition`. Para a falha "sem alvo" ter motivo próprio, o designer põe a regra dela antes da regra de alcance; as regras são avaliadas na ordem e a primeira falha decide (Design 4). Foram descartadas duas saídas: uma chave que depende do contexto (`GetFailureKey(in TContext)`), que poria motivo dentro da avaliação e mudaria a ADR 0032, e `no_target` para qualquer falha de alcance, que mostraria um motivo errado. O CA12 do PRD ("a condição que o exige informa a falha própria") é atendido pela `HasTargetPointCondition`. O usuário aprovou esta saída, a opção (c), em 2026-11-01: duas condições, cada uma com a sua chave de falha. A mesma regra vale para a `TeamRelationCondition` (emenda de 2026-11-01 da Spec de Teams).

`GameplayAbility` ganha `[SerializeField] private ScalableFloat castRange = ScalableFloat.Constant(0)` e `CastRange` (o alcance nasce aqui, e não no B-012, para a condição não ter um campo próprio que viraria legado).

ASC (R7): `bool HasMatching(GameplayTagQuery query)` e `bool HasAll(IReadOnlyList<GameplayTag> tags)`/`HasAny(IReadOnlyList<GameplayTag> tags)` sobre as tags ativas, por laço, sem alocar, com a regra de casamento do B-006 (a tag ativa é a filha). Aceitam `GameplayTagContainer`. Tag inválida não casa.

### 4. Checagem, resultado e consulta (R9 a R13; ADR 0022)

- `AbilityActivationResult` ganha, no fim: `BlockedByCondition`, `NoCharges`.
- `Runtime/GAS/Abilities/ActivationFailure.cs`: `public readonly struct ActivationFailure` com `Result`, `ReasonTag` (`GameplayTag`), `ReasonKey` (`string`), `ConditionIndex` (-1 fora de condição) e `Rule` (a `ActivationRule`, nula fora de condição). `IsSuccess => Result == Success`. Nas falhas de tag, `ReasonTag` é a tag que decidiu: a bloqueadora que o ASC tem, a exigida que falta, ou a `AbilityTag` bloqueada. Nas falhas de cargas, recarga e custo, `ReasonKey` fica nulo: a UI mapeia o `Result` (o texto é do B-008).
- `Runtime/GAS/Abilities/ActivationRule.cs`: `[Serializable] sealed class ActivationRule` com `[SerializeReference] Condition<ConditionContext> condition`, `GameplayTag failureTag`, `string failureKey`. Motivo efetivo: a tag e a chave da regra; chave vazia usa `condition.DefaultFailureKey`.
- `GameplayAbility`: `[SerializeField] private List<ActivationRule> activationConditions = new()` e `ActivationConditions` (somente leitura).
- **Caminho único** (R13): `private bool CheckActivation(AbilitySpec spec, GameplayAbility ability, in AbilityTarget target, out ActivationFailure failure)`, na ordem: 0 pré-requisitos; 1 `BlockedTags`, `RequiredTags`, bloqueio por habilidade ativa; 2 cargas (Design 5) e depois `Cooldown`; 3 custo; 4 regras na ordem da lista (entrada ou condição nula é pulada). Sem escrita em nenhum estado.
- API pública:
  - `AbilityActivationResult GetActivationResult(GameplayAbility ability, in AbilityTarget target, out ActivationFailure failure)`;
  - `AbilityActivationResult GetActivationResult(GameplayAbility ability)` continua, com `AbilityTarget.None` (R11);
  - `AbilityActivationResult TryActivateAbility(GameplayAbility ability, in AbilityTarget target)` e a de `AbilitySpec`; as de `Vector3` continuam e usam `AbilityTarget.AtPoint`.
  - `GameplayAbility.CanActivate(asc, out ...)` passa a repassar para o caminho único.
- `TryActivateAbility` chama `CheckActivation`; falhou: `OnAbilityFailed(ability, failure)`; passou: `ActivateSpec` (commit). O `AbilityContext` recebe o ponto do alvo como hoje.

### 5. Cargas (R14 a R21)

`Runtime/GAS/Abilities/ChargesConfig.cs`, `[Serializable] sealed class ChargesConfig`: `bool enabled` (padrão falso), `ModifierValueSource maxCharges` (padrão `Value` 1), `ScalableFloat rechargeTime` (padrão 1), `ChargeRechargeMode mode` (só `Sequential`; o enum existe para o formato serializado). `GameplayAbility` ganha `[SerializeField] private ChargesConfig charges = new()` e `Charges`.

Conta única da redução: `GameplayAbility.ApplyCooldownReduction(float baseDuration, AbilitySystemComponent asc)` (política do EA6 ou `ApplyDefault`, só com `CooldownReductionAttribute`). `GetCooldownDuration` e a recarga de carga chamam a mesma (R17).

`AbilitySpec` ganha (leitura pública, escrita `internal`): `Charges`, `MaxCharges`, `ChargeRechargeRemaining`, `ChargeRechargeDuration` (a duração aplicada da recarga em curso, para a normalizada).

Regras (todas no ASC; tempo só por `Tick`, ADR 0014):
- **Concessão:** com cargas, avalia `MaxCharges` (inteiro, `floor`, mínimo 0) e começa cheio; recarga parada.
- **Checagem 2:** `min(Charges, MaxCharges) < 1` → `NoCharges`; senão, se há `Cooldown` em curso → `OnCooldown` (P3). Sem cargas: só `OnCooldown`, como hoje.
- **Commit (`ActivateSpec`):** gasta 1 carga junto do custo. Se a recarga estava parada e ficou abaixo do máximo, começa: `ChargeRechargeDuration = ApplyCooldownReduction(rechargeTime.Evaluate(nível, tabela), this)` e `ChargeRechargeRemaining = ChargeRechargeDuration`. `OnChargesChanged(spec, antes, depois)`.
- **`Tick(dt)`:** para cada spec com cargas: reavalia `MaxCharges`; se caiu abaixo de `Charges`, limita (evento) e, se ficou no máximo, para a recarga (R18). Com recarga correndo: `remaining -= dt`; enquanto `remaining ≤ 0` e abaixo do máximo, `Charges++` (evento) e, se ainda abaixo, `remaining += nova duração` (o excesso passa adiante, recarga **sequencial**); no máximo, para. Máximo que sobe não enche (R18): se havia recarga parada e agora está abaixo do máximo, a recarga começa.
- **API (R20):** `GetCharges(spec)`, `GetMaxCharges(spec)`, `GetChargeRechargeRemaining(spec)`, `GetChargeRechargeNormalized(spec)` (0 parada), `bool AddCharges(spec, int n)` (limita ao máximo, n ≤ 0 é falso), `bool RefillCharges(spec)`, `event Action<AbilitySpec, int, int> OnChargesChanged` (só em mudança real). Spec nulo ou sem cargas: 0 ou falso.
- **Controles (R21, P6):** `ReduceCooldown(ability, s)`: além do EA5, subtrai `s` de `ChargeRechargeRemaining` e roda o mesmo laço de reposição do `Tick` (pode devolver mais de uma carga). `ResetCooldown(ability)`: além do EA5, com recarga correndo, devolve 1 carga e recomeça a próxima com a duração cheia (ou para, no máximo). `RestartCooldown(ability)`: além do EA5, com recarga correndo, `remaining = ChargeRechargeDuration`. Devolvem verdadeiro se algo mudou em qualquer das duas partes.
- `AbilityRow` do GAS Debugger (EA7) **não** muda aqui: mostrar cargas no Debugger fica para depois (aditivo).

### 6. Validação (R23) e Inspector (R22)

`GameplayAbility.Validate` acrescenta: regra nula ou com condição nula (erro); `Or` vazio (aviso, pelo Core); `Not` sem condição (erro); `AttributeThresholdCondition` sem atributo (erro) ou com fonte `SetByCaller`/`TargetAttribute` (erro); `CastRangeCondition` com `CastRange` negativo (erro) ou sem nenhum `CastRange` (aviso: sempre passa); `TagQueryCondition` com consulta vazia (aviso) ou tag inválida (pelo `GameplayTagQuery.Validate` do TG4); cargas ligadas com `MaxCharges` constante < 1 (erro), `rechargeTime` constante ≤ 0 (erro), `%nome` inexistente em `maxCharges` ou `rechargeTime` (pela regra do VN9). Mensagens com `propertyPath` (clicáveis).

`GameplayAbilityEditor` (do VN10), seção "Ativação": a lista de regras com uma linha por regra (`SummaryFormatter` da condição, mais "→ motivo: <tag ou chave>"), seletor de tipo do 0026 e composição aninhada pelo `PropertyField`; bloco "Cargas" com a prévia "1 carga a cada X s, máximo Y no nível N" usando o nível de prévia do VN10.

### 7. O que não muda

- `GetActivationResult(ability)` e `TryActivateAbility(ability, Vector3)`: mesmas checagens, mesma ordem para o que já existe (tags → recarga → custo).
- Habilidade sem regras e com cargas desligadas: comportamento idêntico (CA18, CA27).
- `ConditionalAction` e o enum dele: até o B-012.
- O GAS não referencia Events, Pooling, Teams nem UI; `Condition<TContext>` no Core sem domínio (CA23).

## Quebras de compatibilidade

Para o CHANGELOG (ADR 0009):
- **`OnAbilityFailed` muda de assinatura:** `Action<GameplayAbility, AbilityActivationResult>` vira `Action<GameplayAbility, ActivationFailure>`. Migração: `(a, r) => ...` vira `(a, f) => ... f.Result ...`. Testes e scripts que assinam o evento mudam (lista no PR do AV5; no pacote: `SandboxCaster.cs`).
- **`AbilityActivationResult`** ganha `BlockedByCondition` e `NoCharges` no fim (valores antigos iguais).
- **`AbilityEventData`** (Bridges) ganha `FailureTag` e `FailureKey` (aditivo).
- Adicionado: `Condition<TContext>` e composições no Core, `TypeCategory.Line`, `ConditionContext`, `AbilityTarget`, condições do GAS, `ActivationRule`, `ActivationFailure`, `HasMatching`, `CastRange`, cargas e `OnChargesChanged`.

## Tickets

Todos usam a Unity e seguem o WIP 1. **Não há par paralelo sem Editor nesta Spec.** Cada ticket escreve a sua parte de `gas.md` e do `CHANGELOG.md`. Tempo por `asc.Tick` com `ManualTimeSource`; objetos por `GasFactory` (N3); alocação por `Is.Not.AllocatingGCMemory()` com aquecimento. Teto: ~500 linhas e 10 arquivos.

**Conflitos com a fila:** AV2 a AV7 tocam `AbilitySystemComponent.cs` e `GameplayAbility.cs` (todo o Lote B): entram depois do VN10. AV5 toca `AbilitySystemEventRelay.cs` e `GasEventPayloads.cs` (RL2, RL4) e `SandboxCaster.cs` (SB-T2a). AV1b toca `TypeCategoryAttribute.cs` (nenhum ticket da fila). AV8 toca `validador.md` (AR6, VN9, RL5). AV9 toca `GameplayAbilityEditor.cs` (VN10).

### AV1 — dividido em AV1a e AV1b (2026-11-01)

O AV1 ficou pronto com 691 linhas em 8 arquivos, acima do teto. Por decisão do usuário em 2026-11-01, ele se divide em dois tickets pelos arquivos que já existem no working tree. O AV1a fica com a issue #157 e o AV1b ganha issue nova. Os dois arquivos de texto se dividem por trecho: em `conceitos.md`, o parágrafo do `Line` e o exemplo da `LowHealthCondition` (do "Para o Inspector mostrar a condição numa linha" até o fim da seção `#condicoes`) vão para o AV1b, e o resto da seção fica no AV1a. No `CHANGELOG.md`, a entrada "Condições no Core" fica no AV1a e a entrada "Resumo de uma linha no Inspector" vai para o AV1b. Nenhum arquivo de código ou de teste é cortado. As contagens seguem a ADR 0003: os `.meta` não contam e o manual conta só nos arquivos.

### AV1a — `Condition<TContext>` e composições no Core
- Escopo: `Runtime/Core/Conditions/Condition.cs` (novo), `Runtime/Core/Conditions/CompositeConditions.cs` (novo), `Tests/Core/EditMode/ConditionCompositionTests.cs` (novo; contexto e condições de teste próprios, fechando as bases), `Documentation~/manual/inicio/conceitos.md` (seção `#condicoes` sem o trecho do `Line`; linha do Core na tabela de módulos), `CHANGELOG.md` (entrada "Condições no Core").
- Testes red: `And_AllTrue_EmptyTrue_NullIgnored`, `Or_AnyTrue_EmptyFalse`, `Not_Inverts_NullFalse` (CA2, parte do Core); `Composites_Nested_ThreeLevels`; `Evaluate_DoesNotAllocate`; `Validate_OrEmptyWarning_NotNullError`; `DefaultFailureKey_NullByDefault_OverridableAsTextKey`; `Core_HasNoTagsReference` (guarda do `AsmdefRulesTests`, verde).
- Critério de pronto: testes verdes sem o AV1b no working tree; CA23 (parte do Core).
- Tamanho estimado: ~342 linhas; 5 arquivos. Depende de: nada do Lote B; pela fila, depois do VN10. Issue #157.

### AV1b — Resumo de uma linha no Inspector (`TypeCategory.Line` e `SummaryFormatter`)
- Escopo: `Runtime/Core/Inspector/TypeCategoryAttribute.cs` (`Line`), `Editor/Core/Inspector/SummaryFormatter.cs` (novo), `Tests/Core/EditMode/Inspector/SummaryFormatterTests.cs` (novo), `Documentation~/manual/inicio/conceitos.md` (parágrafo do `Line` e exemplo da `LowHealthCondition`), `CHANGELOG.md` (entrada "Resumo de uma linha no Inspector").
- Testes red: `Summary_Fields_Bool_ObjectName_ListCount_NoLineUsesTypeName`; `Summary_NestedReference_UsesChildLine`.
- Critério de pronto: testes verdes; a suíte do AV1a continua verde.
- Tamanho estimado: ~350 linhas; 5 arquivos. Depende de: AV1a. O código não usa nada do AV1a; a dependência vem do exemplo do manual, que herda de `Condition<MyContext>`, e da fila.

### AV2 — Contexto do GAS, composições fechadas, `HasMatching`, tag e alvos
- Escopo: `Runtime/GAS/Abilities/AbilityTarget.cs` (novo), `Runtime/GAS/Conditions/ConditionContext.cs` (novo), `Runtime/GAS/Conditions/GasConditions.cs` (novo), `Runtime/GAS/Conditions/TagQueryCondition.cs` (novo), `Runtime/GAS/Conditions/HasTargetsCondition.cs` (novo), `AbilitySystemComponent.cs` (`HasMatching`, `HasAll`, `HasAny`), `Tests/GAS/PlayMode/GasConditionsTests.cs` (novo), `gas.md` (`#condicoes`), `CHANGELOG.md`.
- Testes red: `HasMatching_Query_AllAnyNone`, `HasAllHasAny_Container_InvalidTagFalse` (CA6); `TagQuery_PassAndFail_UsesAllAnyNone` (CA3, parte); `HasTargets_PassAndFail_IgnoresDestroyed`; `OrTagQueryHasTargets_NotHasTargets_AndRequiresAll` (C2, CA2 completo); `Conditions_DoNotChangeAsc` (CA5); `HasMatching_DoesNotAllocate`.
- Critério de pronto: CA2, CA5, CA6 e a parte de tag do CA3 verdes.
- Tamanho estimado: ~430 linhas; 9 arquivos. Depende de: AV1b (as condições do GAS usam o `Line` e a base do AV1a), TG3, VN10 (fila).

### AV3 — Condição de atributo, ponto-alvo, alcance e `CastRange`
- Escopo: `Runtime/GAS/Conditions/AttributeThresholdCondition.cs` (novo), `Runtime/GAS/Conditions/HasTargetPointCondition.cs` (novo; emenda de 2026-11-01), `Runtime/GAS/Conditions/CastRangeCondition.cs` (novo), `GameplayAbility.cs` (`castRange`), `Tests/GAS/PlayMode/AttributeAndRangeConditionsTests.cs` (novo), `gas.md`, `CHANGELOG.md`.
- Testes red: `Threshold_Mana30Passes_10Fails` (C1, parte); `Threshold_Ratio_UsesEffectiveMax`, `Threshold_TableValueByLevel`, `Threshold_MissingAttribute_False` (CA3); `CastRange_3Passes_8Fails` (C5); `HasTargetPoint_NoPoint_FalseWithNoTargetKey` (CA12); `CastRange_NoPoint_False_NoException_CastRangeKey` (CA12, parte do alcance); `CastRangeZero_Unlimited_EvenWithoutPoint`. O `CastRange_NoPoint_FalseWithOwnKey_NoException` da versão anterior deixa de existir: as duas chaves vêm de condições diferentes.
- Critério de pronto: CA3 e CA12 verdes (pela ativação, C5 fica completo no AV4a e C1 no AV4b).
- Tamanho estimado: ~420 linhas; 7 arquivos. Depende de: AV2, VN6.

### AV4 — dividido em AV4a e AV4b (2026-11-01)

O AV4 foi implementado e aprovado em tudo menos no tamanho: 711 linhas em 10 arquivos, 42% acima do teto. Ele entrou na branch por engano (f1717ed) e saiu pelo revert 31f4a36. Por decisão do usuário em 2026-11-01, ele se divide em dois tickets, e a Fábrica remonta cada um a partir do f1717ed, arquivo por arquivo, sem reescrever. O AV4a fica com a issue #160 e o AV4b ganha issue nova.

A divisão segue os arquivos e nenhum é cortado. Todo o código de produção vai para o AV4a, porque o caminho de checagem, as regras e a consulta dependem uns dos outros. O AV4a leva junto os testes de consulta e do enum, que passam pelo mesmo `CheckActivation` (`Query_NoSideEffects_SameAsTry` compara a consulta com a tentativa). O AV4b leva só o `ActivationOrderTests.cs`. Assim o AV4b não tem teste red: os testes chegam verdes, porque o código já entrou no AV4a. Antes de abrir o AV4a, a Fábrica confere com `git show f1717ed` que o `ActivationQueryTests.cs` não usa nenhum tipo declarado no `ActivationOrderTests.cs`. Se usar, ela para e devolve ao Controlador, sem mover o tipo por conta própria. As contagens seguem a ADR 0003: os `.meta` não contam e o manual conta só nos arquivos.

### AV4a — `ActivationFailure`, regras, caminho único de checagem e consulta
- Escopo (inteiros, do f1717ed): `Runtime/GAS/Abilities/AbilityActivationResult.cs`, `Runtime/GAS/Abilities/ActivationFailure.cs` (novo), `Runtime/GAS/Abilities/ActivationRule.cs` (novo), `GameplayAbility.cs` (`activationConditions`, `CanActivate`), `AbilitySystemComponent.cs` (`CheckActivation`, sobrecargas com `AbilityTarget`), `Tests/GAS/PlayMode/ActivationQueryTests.cs` (novo), `Tests/GAS/EditMode/ActivationResultEnumTests.cs` (novo), `gas.md` (`#gameplayability`, ordem das checagens), `CHANGELOG.md`.
- Testes red: `Query_NoSideEffects_SameAsTry` (CA8, C4: estado antes e depois, nenhum evento); `Query_WithPoint_CastRange` (CA9, C5); `Query4Conditions_DoesNotAllocate_10000Times` (CA22, parte da consulta); `Enum_NewValuesAtEnd_OldIntSame` (CA10). O conjunto exato é o que está nos dois arquivos de teste do f1717ed.
- Critério de pronto: CA8 a CA10 verdes e a parte de consulta do CA22, sem o `ActivationOrderTests.cs` no working tree; a suíte do Lote A e do AV1a ao AV3 continua verde.
- Tamanho estimado: ~440 linhas; 9 arquivos. Depende de: AV3, EA5. Issue #160.

### AV4b — Testes da ordem das checagens e das regras de ativação
- Escopo: `Tests/GAS/PlayMode/ActivationOrderTests.cs` (novo, inteiro, do f1717ed). Sem código de produção, sem `gas.md` e sem `CHANGELOG.md`.
- Testes (nascem verdes): `Order_TagBeatsCooldownAndCost`, `Order_CooldownBeatsCost`, `Order_CostBeatsCondition`, `Order_ConditionLast_FirstRuleWins` (CA7, C3); `Rule_Threshold_FailsWithConfiguredTagAndKey` (CA1, C1); `Rule_EmptyKey_UsesConditionDefault`; `CustomCondition_EvaluatedInListOrder` (CA4, parte de runtime); `ReasonTag_IsBlockingTag_MissingRequiredTag`; `NoRules_SameAsBefore`.
- Critério de pronto: CA1, CA4 (runtime) e CA7 verdes. Como não há red, o Examinador confere que os testes de ordem falham quando duas checagens trocam de lugar no `CheckActivation`, e desfaz a troca depois.
- Tamanho estimado: ~291 linhas; 1 arquivo. Depende de: AV4a.

### AV5 — Evento de falha único com `ActivationFailure` (quebra)
- Escopo: `AbilitySystemComponent.cs` (`OnAbilityFailed`), `Runtime/GAS.Bridges/Relay/AbilitySystemEventRelay.cs` (handler do par `abilityFailed`), `Runtime/GAS.Bridges/Payloads/GasEventPayloads.cs` (`AbilityEventData.FailureTag`, `FailureKey`), `Samples~/Sandbox/Scripts/SandboxCaster.cs` (assinatura), `Tests/GAS/PlayMode/AbilityFailedEventTests.cs` (novo), `Tests/GAS.Bridges/PlayMode/EventRelayFailureTests.cs` (novo), testes do Lote A/B que assinam `OnAbilityFailed` (lista no PR), `gas.md`, `eventos.md` (payload), `CHANGELOG.md` (quebra).
- Testes red: `Failed_CarriesAbilityResultAndReason_SameAsQuery` (CA11); `Failed_NotRaisedOnQuery`; `Relay_AbilityFailed_HasTagAndKey`; os testes antigos do relay passam sem edição.
- Critério de pronto: CA11 verde; CA28 (S) conferido no host pelo Examinador (o `SandboxCaster` mostra no Console a chave ou a tag da primeira falha); suíte sem edição fora da lista.
- Tamanho estimado: ~300 linhas; até 10 arquivos (8 mais os testes listados). Depende de: AV4b, RL4, SB-T2a. Será refeito: a primeira versão saiu pelo revert e94e659.

### AV6 — Cargas: configuração, gasto, recarga sequencial e API
- Escopo: `Runtime/GAS/Abilities/ChargesConfig.cs` (novo), `AbilitySpec.cs`, `GameplayAbility.cs` (`charges`, `ApplyCooldownReduction`), `AbilitySystemComponent.cs` (concessão, checagem 2, commit, `Tick`, API, `OnChargesChanged`), `Tests/GAS/PlayMode/ChargesTests.cs` (novo), `gas.md` (`#cargas`), `CHANGELOG.md`.
- Testes red: `Three_Spend_FourthNoCharges_SequentialRecharge` (CA13, C6); `OneOfThree_RechargeRunning_Activates` (CA14, C7); `AddRespectsMax_RefillFills_ChangedOnlyOnRealChange` (CA16, C9); `RechargeUsesPolicyFloorAndCap` (CA17, C10); `BornFull_DisabledSameAsToday` (CA18); `WithCooldown_BlocksBetweenUses_WithoutOnlyCharges` (CA19); `ManualTimeSource_Only` (CA21); `BigTick_RestoresSeveral_CarriesExcess`; `Query_DoesNotSpendCharge` (CA8, parte de cargas); `Ticks1000_DoNotAllocate` (CA22, parte de cargas).
- Critério de pronto: CA13, CA14, CA16 a CA19, CA21 e o CA22 completo verdes.
- Tamanho estimado: 579 linhas; 7 arquivos (estimativa original ~480; exceção única pela margem de ~30%, emenda de 2026-11-01 da ADR 0003). Depende de: AV4b (pela fila), VN4, EA6.

### AV7 — Máximo variável de cargas e controle de recarga
- Escopo: `AbilitySystemComponent.cs` (reavaliação do máximo, `OnAbilityLevelChanged`, controles), `AbilitySpec.cs`, `Tests/GAS/PlayMode/ChargesMaxAndControlTests.cs` (novo), `gas.md` (`#cargas`, controles), `CHANGELOG.md`.
- Testes red: `MaxByTableName_Level1Is2_Level3Is4` e `MaxByAttribute_Follows` (CA15, C8); `MaxUp_DoesNotFill_RechargeStarts`, `MaxDown_Clamps_StopsRechargeAtMax` (R18); `Reduce_ShortensRecharge_CanRestoreSeveral`, `Reset_ReturnsOneCharge_RestartsNext`, `Restart_BackToFullAppliedDuration` (CA20); `Controls_AlsoActOnCooldownEffect` (P6).
- Critério de pronto: CA15 e CA20 verdes.
- Tamanho estimado: ~380 linhas; 5 arquivos. Depende de: AV6, EA5.

### AV8 — Validação de condições e cargas
- Escopo: `GameplayAbility.cs` (`Validate`), `ActivationRule.cs`, `AttributeThresholdCondition.cs`, `CastRangeCondition.cs`, `TagQueryCondition.cs`, `ChargesConfig.cs` (`Validate`), `Tests/GAS/EditMode/ActivationValidationTests.cs` (novo), `manual/ferramentas/validador.md`, `CHANGELOG.md`.
- Testes red: um por regra do Design 6 (CA25), com `propertyPath` conferido; `ProjectValidator_ReportsAbilityAsContext`.
- Critério de pronto: CA25 verde.
- Tamanho estimado: ~380 linhas; 9 arquivos. Depende de: AV7, AR6, VN9.

### AV9 — Inspector de condições e cargas, e documentação
- Escopo: `Editor/GAS/GameplayAbilityEditor.cs` (seção "Ativação": regras com resumo e motivo, bloco de cargas com prévia), `Tests/GAS/EditMode/ActivationInspectorTests.cs` (novo), `gas.md` (ordem das checagens, condições prontas com exemplo, motivo para a UI, "como criar uma condição própria" com a subclasse mínima), `CHANGELOG.md` (R25 completo).
- Testes red: `Picker_ListsGasConditionsAndCustom_NotCoreBases` (CA4, parte do seletor); `RuleRow_ShowsSummaryAndReason`; `ChargesPreview_UsesPreviewLevel`.
- Critério de pronto: testes verdes; CA24 (S) no host; CA26 (E) conferido pelo Examinador.
- Tamanho estimado: ~380 linhas; 4 arquivos. Depende de: AV8, VN10.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| AV1a | sim | — (fila: depois do VN10) | ~342 / 5 |
| AV1b | sim | AV1a | ~350 / 5 |
| AV2 | sim | AV1b, TG3 | ~430 / 9 |
| AV3 | sim | AV2, VN6 | ~420 / 7 |
| AV4a | sim | AV3, EA5 | ~440 / 9 |
| AV4b | sim | AV4a | ~291 / 1 |
| AV5 | sim | AV4b, RL4, SB-T2a | ~300 / ≤10 |
| AV6 | sim | AV4b (fila), VN4, EA6 | 579 / 7 (exceção, ADR 0003) |
| AV7 | sim | AV6, EA5 | ~380 / 5 |
| AV8 | sim | AV7, AR6, VN9 | ~380 / 9 |
| AV9 | sim | AV8, VN10 | ~380 / 4 |

Fila: AV1a, AV1b, AV2, AV3, AV4a, AV4b e depois AV5 a AV9, em série. AV5 pode trocar de lugar com AV6/AV7 se o relay ainda não tiver entrado. CA23 e CA27 rodam em todos.

## Perguntas ao usuário

- **Q1. Motivo da falha na regra, não na condição (ADR 0032, item 4).** Proposta: cada entrada da lista de ativação é uma `ActivationRule` (condição + tag + chave), e a condição fica um predicado puro, reutilizável em ações. Alternativa: a condição guarda o próprio motivo (como o PRD R3 descreve), o que obriga a base do GAS a ter tag e duplica o campo nas composições.
- **Q2. Alcance de cast na habilidade já no B-009.** Proposta: `GameplayAbility.CastRange` nasce aqui e a `CastRangeCondition` o lê (fonte única; o B-012 só acrescenta unidade e borda). Alternativa: a condição tem alcance próprio no B-009 e o B-012 cria o campo da habilidade, deixando dois lugares para o mesmo número.
- A ADR 0032 passa pelo gate junto com esta Spec.
