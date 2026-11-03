# Spec: Dano, escudo e morte
PRD: [Documentation~/prd/gas-dano-escudo-e-morte.md](../prd/gas-dano-escudo-e-morte.md) · Backlog: B-010 · ADRs: **0033 (nova, Proposta)**; usa 0003, 0004, 0007, 0009, 0012, 0013, 0016, 0024, 0026, 0029, 0030, 0031

Status: Aprovada no gate de 2026-10-31 (DM1–DM8; ADR 0033 aceita; Q1: sim, processadores só na definição do atributo) · Data: 2026-10-31 · Autor: Arquiteto

Emenda de 2026-11-01: o DM1 foi dividido em DM1a e DM1b por decisão do usuário, depois de uma tentativa reprovada com ~703 linhas e 11 arquivos. O design não muda.

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`. As linhas são as de hoje; os Lotes A e B mudam parte delas antes deste item.

- `Runtime/GAS/Core/AbilitySystemComponent.cs:563-586`: `ApplyModifiersToBase` (instantâneo e tique). O AR3 compõe por atributo e chama `SetBaseValue` uma vez por atributo; o VN5 avalia com o `EffectContext`; o VN6/VN7 multiplicam o tique pelas pilhas. É o **único ponto** onde a base muda por efeito: é onde o pipeline entra.
- `:553-557`: `HandleEffectTick` repassa a instância; o VN5 dá `ActiveEffectInstance.Context`.
- `Runtime/GAS/Attributes/AttributeState.cs:27-35`: `SetBaseValue` grava e recalcula. O NC1 limita a base (`ClampToLimits`); o AR2 cria `OnDepleted`/`OnReachedMax` com os indicadores `_atMin`/`_atMax`.
- `Runtime/GAS/Attributes/AttributeDefinition.cs:15-31`: campos públicos; o AR1 acrescenta fontes de limite, o AR5 `Display`, o AR6 `IValidatable` e o `AttributeDefinitionEditor`.
- `Runtime/GAS/Effects/GameplayEffect.cs`: o AR7 acrescenta metadados, nome e ícone; o VN2 `duration` como `ScalableFloat`.
- `Runtime/GAS.Bridges/Relay/AbilitySystemEventRelay.cs` e `Payloads/GasEventPayloads.cs` (RL2 a RL4): par `attributeDecreased` lê `AttributeState.OnValueChanged`, sem contexto.
- `Tests/Testing/GasFactory.cs` e `ManualTimeSource` para os testes (N4).

## Respostas às perguntas técnicas do PRD (seção 8)

- **P5. Onde moram os processadores.** Só na `AttributeDefinition` (ADR 0033, item 1). A variação por entidade vem de atributos do alvo (resistência, escudo), que os processadores leem. Uma fonte só: não há ordem entre fontes.
- **P6. Meta-atributo.** Atributo comum marcado (`IsMeta`). Precisa estar no `AttributeSet` como qualquer outro. A variação final dele é descartada; a base continua 0 e nada dispara (ADR 0033, item 6).
- **P7. Escopo e composição.** Confirmado: só a base (instantâneo, tique e `ApplyAttributeChange`). Com a 0029, o processador recebe a **variação** já composta daquela aplicação (`nova base - base atual`), não a operação; modificadores persistentes nunca passam (ADR 0033, itens 2 e 3).
- **P8. Instigador destruído.** Referência nula pela comparação da Unity, sem identificador (como a P8 do B-007).
- **P9. Pós-alteração.** Confirmado: não há gancho; `OnValueChanged` e o relatório (P10) cumprem.
- **P10. Resultado do dano.** Sim: `AttributeChangeReport` com pedido, final, aplicado, efeito e contexto, no evento `OnAttributeChangeApplied`. É o que os números flutuantes (B-015) e o `OnTakeDamage` (B-012) usam.
- **P11. `OnDepleted` com processadores.** O processador muda a variação; a base final vai ao `SetBaseValue`, e o `OnDepleted` continua decidido pelo valor atual no `Recalculate` (P7 do B-005). O ASC só acrescenta o contexto (Design 5).
- **P12. Ordem com escudo e vida.** As alterações redirecionadas são aplicadas antes da original (ADR 0033, item 5): o `OnDepleted` do escudo dispara antes do da vida, e o evento de morte do ASC vem depois do `OnDepleted` da vida.

## Design

### 1. Alteração pendente, processador e fila (R1 a R6, R14)

Em `Runtime/GAS/Attributes/Processing/`:

```csharp
public struct AttributeChange                // AttributeChange.cs
{
    public AttributeDefinition Attribute { get; }
    public float Requested { get; }           // variação pedida, antes dos processadores
    public float Delta;                       // variação corrente; zero anula
    public GameplayEffect Effect { get; }     // nulo em alteração por código
    public AttributeDefinition RedirectedFrom { get; }
    public bool HasDamageTag(IReadOnlyList<GameplayTag> filter); // Effect?.DamageTags.HasAny(filter); filtro vazio: verdadeiro
}

public readonly struct AttributeChangeReport  // mesmo arquivo
{   Attribute, Requested, Final, Applied, Effect, Context (EffectContext), RedirectedFrom, IsValid }

[Serializable] public abstract class AttributeChangeProcessor   // AttributeChangeProcessor.cs
{
    public abstract void Process(ref AttributeChange change, in EffectContext context, AttributeChangeQueue queue);
    public virtual void Validate(ValidationResult result, string propertyPath, AttributeDefinition owner) { }
}

public sealed class AttributeChangeQueue      // AttributeChangeQueue.cs; uma por ASC
{
    public AbilitySystemComponent Owner { get; }
    public bool TryGetState(AttributeDefinition def, out AttributeState state);
    public void Redirect(AttributeDefinition target, float delta);   // gera alteração adicional
}
```

- `AttributeDefinition` ganha `[SerializeReference, TypePicker] private List<AttributeChangeProcessor> processors = new()` e `Processors` (leitura).
- ASC: `public float ApplyAttributeChange(AttributeDefinition attribute, float delta, in EffectContext context = default, GameplayEffect effect = null)` (devolve o aplicado no atributo original) e `event AttributeChangeHandler OnAttributeChangeApplied` (`delegate void AttributeChangeHandler(in AttributeChangeReport report)`).
- `ApplyModifiersToBase` (depois do AR3, VN5 e VN7): no lugar de `state.SetBaseValue(nova)`, chama o núcleo `ProcessChange(state, nova - state.BaseValue, effect, in context)`.
- **Núcleo:** monta a alteração; se a definição não tem processadores e não é meta, aplica direto (sem trabalho extra, R5, C4). Senão roda os processadores em ordem (entrada nula pulada). As alterações redirecionadas ficam na fila do ASC (lista reutilizada de `struct`) e, depois do último processador, cada uma passa pelo núcleo do atributo de destino (recursão com profundidade máxima 8 e um conjunto fixo dos atributos na cadeia; repetido ou fundo demais: aplicado direto, com aviso único por definição). Depois aplica a original. Cada aplicação: `final == 0` não chama `SetBaseValue`; senão `SetBaseValue(base + final)`; `Applied = base depois - base antes`; dispara `OnAttributeChangeApplied`.
- **Alteração em curso:** durante a aplicação, o ASC guarda o efeito e o contexto da alteração (`bool TryGetActiveChangeSource(out GameplayEffect effect, out EffectContext context)`), para o evento de morte e o relay. Fora da aplicação devolve falso.
- Sem efeito (código): contexto `default` e efeito nulo; nenhum processador do pacote lança com isso (CA6).
- Sem alocação: fila e conjunto da cadeia pré-alocados por ASC (crescem uma vez), laços por índice (N1).

### 2. Tipos de dano e processadores prontos (R9, R12, R13)

- `GameplayEffect` ganha `[SerializeField] private GameplayTagContainer damageTags = new()` e `DamageTags` (TG2). Tipo de dano é sempre tag; o casamento é o hierárquico (`Dano.Fogo` casa com o filtro `Dano`).
- Em `Runtime/GAS/Attributes/Processing/Processors/`, todos `[Serializable] sealed`, com `[TypeCategory("Dano/...", Line = ...)]`, `[HelpURL]` para a página nova:
  - `ReduceByAttribute`: `AttributeDefinition sourceAttribute` (do dono), `ReductionMode mode` (`Percent`: reduz `|Delta| × clamp(valor, 0, 1)`; `Flat`: reduz até `valor`, sem inverter o sinal), `GameplayTagContainer damageTags` (vazio: qualquer), `bool onlyNegative = true`.
  - `ImmuneToTags`: `GameplayTagContainer damageTags`; `Delta = 0` quando o efeito tem alguma (C8). O efeito continua aplicado (tags, outros modificadores).
  - `AbsorbWithAttribute`: `AttributeDefinition shield`, `GameplayTagContainer damageTags` (vazio: todas), `float fraction = 1` (0 a 1). Só para `Delta < 0`: `absorvível = |Delta| × fraction`; `consumido = min(absorvível, escudo - mínimo do escudo)`; `queue.Redirect(shield, -consumido)`; `Delta += consumido`. Escudo ausente no ASC: não faz nada.

### 3. Meta-atributo (R7, R8)

`AttributeDefinition` ganha `[SerializeField] private bool isMeta` e `IsMeta`. No núcleo, depois dos processadores, a original de um meta não é aplicada: o relatório sai com `Final` e `Applied = 0`. Sem processadores, o meta não afeta ninguém (o validador avisa, CA9). O cálculo com origem e alvo do R8 já é o `ModifierValueSource` do B-007 (atributo do instigador, `TargetAttribute`); nada novo aqui.

### 4. Ordem e reentrada (C9, N7)

- A ordem da lista é a ordem de execução; a mesma lista dá sempre o mesmo resultado (C9).
- Um ouvinte (de `OnValueChanged`, `OnDepleted`, `OnAttributeChangeApplied` ou do evento de morte) que aplica efeitos ou chama `ApplyAttributeChange` dentro do evento roda um pipeline aninhado; a fila guarda o início do trecho de cada nível, então o nível de fora continua com as suas alterações. Profundidade de aninhamento acima de 8: a alteração é aplicada direto, com aviso único (N7, CA20).

### 5. Morte como sinal (R15 a R18)

- `AttributeState` ganha `IsAtMin`, `IsAtMax` (leitura dos indicadores do AR2) e `event Action<AttributeState> OnLeftMin` e `OnLeftMax` (verdadeiro → falso, disparados no mesmo ponto e depois de `OnDepleted`/`OnReachedMax`).
- ASC, depois do `EnsureInitialized` (EA1), assina o `OnDepleted`, o `OnReachedMax` e o `OnLeftMin` de cada estado de `AttributeSet.States` (uma alocação por estado, na inicialização).
- `event AttributeLimitHandler OnAttributeDepleted` e `OnAttributeReachedMax` (`delegate void AttributeLimitHandler(AttributeState state, in AttributeChangeReport cause)`). `cause` é o relatório da alteração em curso; sem alteração em curso (limite mudou, modificador persistente), `cause.IsValid` é falso e o contexto é vazio. O evento dispara depois do `OnDepleted` do estado (R15). Instigador destruído chega nulo (R16).
- `AttributeDefinition` ganha `[SerializeField] private GameplayTag stateTagWhileDepleted` e `StateTagWhileDepleted` (padrão inválida = desligado). Ao chegar ao mínimo, `AddTag`; ao sair (`OnLeftMin`), `RemoveTag`. Estado que nasce no mínimo (construtor do AR2 não dispara): o ASC confere `IsAtMin` na inicialização e concede a tag, para a tag refletir o estado (C11). A tag usa a contagem do ASC, então `BlockedTags` e `RequiredTags` funcionam (CA18).
- Nenhum tipo do pipeline destrói, desabilita ou usa nome de gênero (R18, N3).

### 6. Relay (R20, R21; Bridges)

- `GasEventPayloads.cs`: `AttributeLimitEventData` (`Owner`, `Attribute`, `Value`, `Instigator`, `Effect`) com `AttributeLimitEventChannel` e `AttributeLimitEventResponse` (padrão do RL2). `AttributeChangedData` ganha `Instigator` e `Effect` (aditivo), preenchidos por `TryGetActiveChangeSource` quando a mudança vem do pipeline; as tags de dano são `Effect.DamageTags` (CA22).
- `AbilitySystemEventRelay`: pares `attributeDepleted` e `attributeReachedMax` (grupo "Atributos"; Float `Value`, String nome do atributo, GameObject dono), assinando os eventos do ASC do Design 5.

### 7. Validação e Inspector (R22, R23)

- `AttributeDefinition.Validate` (AR6) acrescenta: processador nulo (erro); `AbsorbWithAttribute` sem escudo ou com escudo igual ao próprio atributo (erro), `fraction` fora de 0 a 1 (erro); `ReduceByAttribute` sem atributo (erro); meta sem processadores (aviso); meta cujo processador redireciona para ele mesmo (erro: ciclo visível no asset); `stateTagWhileDepleted` com definição nula mas campo preenchido (erro).
- `GameplayEffect.Validate` acrescenta: tag de dano inválida (pelo `GameplayTagContainer.Validate`, TG4); meta-atributo em `Modifiers` de efeito com duração ou infinito (erro).
- `AbilitySystemComponent.Validate` (AR7) acrescenta: processador de um atributo do `AttributeSet` que referencia atributo ausente do mesmo `AttributeSet` (aviso; no runtime o processador não faz nada).
- `AttributeDefinitionEditor` (AR6): seção "Dano" com a lista de processadores (linha pelo `SummaryFormatter` do AV1), `IsMeta` e `StateTagWhileDepleted`; **prévia** "dano → processadores → alterações": campo "dano de exemplo", um campo de valor por atributo citado pelos processadores, e a lista de alterações resultantes, calculada por `DamagePreviewModel` (Editor), que roda o mesmo núcleo num ASC temporário oculto (`HideFlags.HideAndDontSave`, criado ao abrir e destruído ao fechar o Inspector).

## Quebras de compatibilidade

Nenhuma de comportamento: sem processadores, o resultado é idêntico (CA1). Adicionado (CHANGELOG, R25): `AttributeChangeProcessor`, `AttributeChange`, `AttributeChangeReport`, `ApplyAttributeChange`, `OnAttributeChangeApplied`, `AbsorbWithAttribute`, `ReduceByAttribute`, `ImmuneToTags`, `IsMeta`, `DamageTags`, `OnAttributeDepleted`/`OnAttributeReachedMax`, `IsAtMin`/`IsAtMax`/`OnLeftMin`/`OnLeftMax`, `StateTagWhileDepleted`, pares do relay e `AttributeLimitEventData`.

## Tickets

Todos usam a Unity e seguem o WIP 1. **Não há par paralelo sem Editor nesta Spec.** Cada ticket escreve a sua parte da página nova `Documentation~/manual/modulos/dano-escudo-e-morte.md` (criada no DM1a, com a entrada no `Documentation~/mkdocs.yml`) e do `Packages/com.spellbook/CHANGELOG.md`. Testes com `GasFactory` e `asc.Tick`; nomes de exemplo ("Vida", "Escudo") só nos testes (N3). Teto: ~500 linhas e 10 arquivos.

**Conflitos com a fila:** DM1a, DM4, DM5 e DM7 tocam `AttributeDefinition.cs` (AR1, AR5, AR6); DM1a, DM1b e DM5 tocam `AbilitySystemComponent.cs` (todo o Lote B e o B-009); DM5 toca `AttributeState.cs` (AR1 a AR3); DM2 e DM7 tocam `GameplayEffect.cs` (AR7, VN*); DM6 toca o relay e os payloads (RL2 a RL4, AV5); DM7 toca `validador.md`; DM8 toca `AttributeDefinitionEditor.cs` (AR6). Este item entra depois do B-009 na fila do Unity, ou entre o AV9 e o B-012, à escolha do Controlador; não há dependência de código com o B-009.

**Reaproveitamento do DM1 reprovado.** A Fábrica parte dos arquivos guardados da primeira tentativa (fora do repositório, sem commit) e corta cada um entre o DM1a e o DM1b como abaixo. Dois erros de compilação daquela tentativa ficam registrados para não voltarem:

- **CS8329 em `AttributePipelineTests.cs:189`.** Num lambda de um parâmetro, `(in AttributeChangeReport _) => ...` declara um parâmetro de verdade chamado `_`, não um descarte. O `out _` dentro do corpo passa a apontar para esse parâmetro `in`, que é só leitura. Quando o corpo usa descarte, o parâmetro do lambda tem nome (`report`).
- **CS1061 em `:281` (checagem de alocação).** `AllocatingGCMemory()` vem do Unity Test Framework, não do NUnit, e o `Is.Not.AllocatingGCMemory()` depende do método de extensão `ConstraintExtensions.AllocatingGCMemory(this ConstraintExpression)`, que fica no namespace `UnityEngine.TestTools.Constraints`. O arquivo tinha só o alias `using Is = UnityEngine.TestTools.Constraints.Is;`, sem importar o namespace. O meio a usar é o mesmo dos outros testes do pacote: as duas diretivas `using UnityEngine.TestTools.Constraints;` e `using Is = UnityEngine.TestTools.Constraints.Is;` (`Tests/GAS/PlayMode/ChargesTests.cs:14-15`, `Tests/GAS/PlayMode/AttributeAllocationTests.cs:8-9`) e `Assert.That(() => { laço }, Is.Not.AllocatingGCMemory())` (`ChargesTests.cs:295`), com aquecimento antes do laço medido. Nenhum teste deste item mede com `GC.GetAllocatedBytesForCurrentThread` nem com o Profiler.

A classe de testes do pipeline é `partial` desde o DM1a. O DM1b acrescenta um segundo arquivo da mesma classe e usa os mesmos auxiliares (`CreateAttribute`, `SetProcessors`, `Instant`, `Modifier`, `CreateManualAsc`) e processadores de teste (`AddDelta`, `ScaleDelta`, `Annul`) sem copiá-los.

### DM1a — Pipeline de pré-alteração e relatório
- Escopo: `Runtime/GAS/Attributes/Processing/AttributeChange.cs` (novo: `AttributeChange` sem `RedirectedFrom`, `AttributeChangeReport` sem `RedirectedFrom`, `AttributeChangeHandler`), `AttributeChangeProcessor.cs` (novo), `AttributeChangeQueue.cs` (novo, só `Owner`, `TryGetState` e o construtor interno), `AttributeDefinition.cs` (`processors`, `Processors`), `AbilitySystemComponent.cs` (núcleo sem redirecionamento: `ProcessChange` monta a alteração, roda os processadores por índice pulando nulos, aplica, gera o relatório e dispara `OnAttributeChangeApplied`; `ApplyAttributeChange`; `ApplyModifiersToBase` chama o núcleo), `Tests/GAS/PlayMode/AttributePipelineTests.cs` (novo, `partial`; auxiliares e os processadores de teste `AddDelta`, `ScaleDelta`, `Annul`), `Documentation~/manual/modulos/dano-escudo-e-morte.md` (nova: o que o pipeline faz, a ordem da lista, como escrever um processador, `ApplyAttributeChange` e o relatório), `Documentation~/mkdocs.yml` (entrada da página), `CHANGELOG.md`.
- Testes red: `NoProcessors_SameValuesAsBefore` (CA1, C4, com os casos do B-002); `Order_AB_vs_BA_DiffersAsExpected_Deterministic` (CA2, C9); `Processor_Reduce_Increase_Annul` e `FinalZero_NoValueChanged` (CA3, sem o redirecionamento); `DurationModifiers_NotProcessed` (CA5); `Report_RequestedFinalApplied_EffectAndContext` (uma alteração sem redirecionamento: `Requested`, `Final`, `Applied` limitado ao mínimo, `Effect`, `Context.Target`).
- Critério de pronto: CA1, CA2, CA5 e a parte do CA3 sem redirecionamento verdes; suíte do GAS sem edição (CA28). A guarda de profundidade (Design 1 e 4) e a alteração em curso ficam para o DM1b.
- Tamanho estimado: ~480 linhas; 9 arquivos. Depende de: AR3, VN7, EA1.

### DM1b — Redirecionamento, alteração em curso e alocação
- Escopo: `AttributeChangeQueue.cs` (`Entry`, lista pré-alocada, `Count`, `Redirect`, indexador, `TrimTo`), `AttributeChange.cs` (`RedirectedFrom` na alteração e no relatório), `AbilitySystemComponent.cs` (redirecionadas aplicadas antes da original, cadeia fixa de 8 atributos, profundidade máxima 8 também para pipelines aninhados, aviso único por definição; `TryGetActiveChangeSource` com o efeito e o contexto guardados e restaurados em cada nível), `Tests/GAS/PlayMode/AttributePipelineRedirectTests.cs` (novo, segundo arquivo da classe `partial`; processadores de teste `RedirectFraction` e `ReadsSource`), página de dano (redirecionamento, ordem das alterações e alteração em curso), `Runtime/Core/DocUrls.cs` (`Damage`), `Tests/Core/EditMode/DocUrlsTests.cs` (caso novo), `CHANGELOG.md`.
- Testes red: `Processor_Redirect_AppliedBeforeOriginal_WithReport` (CA3, parte do redirecionamento: valores, ordem dos relatórios, `RedirectedFrom`); `Redirect_RunsTargetProcessors_RepeatGuarded` (aviso único por definição); `Redirect_DeeperThanEight_AppliedDirectWithWarning`; `CodeChange_EmptyContext_NoException` (CA6); `ActiveChangeSource_OnlyDuringApplication`; `ThreeProcessors_10000Changes_DoNotAllocate` (CA24, parte; meio de medida na nota acima); `DocUrls_PageConstant("Damage", "modulos/dano-escudo-e-morte/")`.
- Critério de pronto: CA3 completo e CA6 verdes, a parte do DM1 do CA24 verde; suíte do GAS sem edição (CA28).
- Tamanho estimado: ~320 linhas; 8 arquivos. Depende de: DM1a.

### DM2 — Tags de dano, resistência e imunidade
- Escopo: `GameplayEffect.cs` (`damageTags`), `Runtime/GAS/Attributes/Processing/Processors/ReduceByAttribute.cs`, `ImmuneToTags.cs` (novos), `Tests/GAS/PlayMode/DamageTypeTests.cs` (novo), página de dano, `CHANGELOG.md`.
- Testes red: `Fire50pct_Fire100Reduces50_Poison100Reduces100` (CA11, C6); `FireAndMagic_ParentDamageMatchesBoth` (CA12, C7); `Immune_AnnulsOnlyDamage_EffectStillApplied` (CA13, C8); `InvalidDamageTag_NoException` (CA14, parte de runtime); `Flat_DoesNotInvertSign`.
- Critério de pronto: CA11 a CA13 e a parte de runtime do CA14 verdes.
- Tamanho estimado: ~360 linhas; 6 arquivos. Depende de: DM1b (o `[HelpURL]` usa `DocUrls.Damage`), TG2.

### DM3 — Escudo (`AbsorbWithAttribute`) e ordem escudo → vida
- Escopo: `Processors/AbsorbWithAttribute.cs` (novo), `Tests/GAS/PlayMode/ShieldTests.cs` (novo), página de dano (exemplo do escudo), `CHANGELOG.md`.
- Testes red: `Shield50_Damage30_Shield20_Health100` (C1), `Shield20_Damage30_Shield0_HealthMinus10` (C2), `ShieldBreaks_DepletedOnce_ThenDamageGoesToHealth` (C3, CA7); `OnlyConfiguredTags_AndFraction` (CA8); `ShieldDepletedBeforeHealthDepleted` (P12); `CustomProcessor_InPickerAndWorks` (CA4, parte de runtime).
- Critério de pronto: CA4 (runtime), CA7 e CA8 verdes.
- Tamanho estimado: ~320 linhas; 4 arquivos. Depende de: DM2, AR2.

### DM4 — Meta-atributo
- Escopo: `AttributeDefinition.cs` (`isMeta`), `AbilitySystemComponent.cs` (descarte do meta), `Tests/GAS/PlayMode/MetaAttributeTests.cs` (novo), página de dano, `CHANGELOG.md`.
- Testes red: `Meta40_ConvertedToShieldAndHealth_StaysZero_NoValueEvents` (CA9, C5); `MetaWithoutProcessors_AffectsNobody`; `Meta_ReportHasFinalAndZeroApplied`.
- Critério de pronto: CA9 (runtime) verde.
- Tamanho estimado: ~260 linhas; 5 arquivos. Depende de: DM3.

### DM5 — Morte: evento com causa e tag de estado
- Escopo: `AttributeState.cs` (`IsAtMin`, `IsAtMax`, `OnLeftMin`, `OnLeftMax`), `AttributeDefinition.cs` (`stateTagWhileDepleted`), `AbilitySystemComponent.cs` (assinaturas, `OnAttributeDepleted`, `OnAttributeReachedMax`, tag de estado), `Tests/GAS/EditMode/AttributeLeftLimitTests.cs` (novo), `Tests/GAS/PlayMode/DeathSignalTests.cs` (novo), página de dano (exemplo "morto"), `CHANGELOG.md`.
- Testes red: `Depleted_WithInstigatorAndEffect_OnceAtMin_AgainAfterHeal` (CA15, C10); `DestroyedInstigator_NullNoException` (CA16); `StateTag_GrantedAtMin_ReturnedOnHeal_EmptyFieldNothing`, `BornAtMin_HasStateTag` (CA17, C11); `StateTag_BlocksAbilityViaBlockedTags` (CA18); `HealAfterZeroBase_RaisesHealth` (CA10, C12); `ListenerHealsInsideDeath_NoExceptionNoSkippedEvents` (CA20); `LimitChangeDepletion_CauseInvalid`; `Death1000_WithStateTag_DoNotAllocate` (CA24 completo).
- Critério de pronto: CA10, CA15 a CA18, CA20 e CA24 verdes; CA19 (E) conferido pelo Examinador.
- Tamanho estimado: ~460 linhas; 7 arquivos. Depende de: DM4, AR2.

### DM6 — Relay de mínimo, máximo e dano com efeito
- Escopo: `Runtime/GAS.Bridges/Payloads/GasEventPayloads.cs`, `Runtime/GAS.Bridges/Channels/AttributeLimitEventChannel.cs` (novo), `Runtime/GAS.Bridges/Channels/GasEventResponses.cs`, `Runtime/GAS.Bridges/Relay/AbilitySystemEventRelay.cs`, `Tests/GAS.Bridges/PlayMode/RelayLimitsTests.cs` (novo), `Tests/GAS.Bridges/EditMode/GasChannelsTests.cs` (caso do canal novo), `eventos.md` (pares novos), `CHANGELOG.md`.
- Testes red: `Depleted_And_ReachedMax_RaiseChannelWithInstigatorAndEffect` (CA21, C13); `DisableRelay_Unsubscribes` (CA21); `Decreased_IncludesEffectWithDamageTags` (CA22); `AttributeLimitChannel_HasMenuAndHelpUrl`; `AsmdefRules` verde (CA23).
- Critério de pronto: CA21 a CA23 verdes; C13 (S) conferido no host.
- Tamanho estimado: ~400 linhas; 8 arquivos. Depende de: DM5, RL4, AV5.

### DM7 — Validação do pipeline
- Escopo: `AttributeDefinition.cs` (`Validate`), `GameplayEffect.cs` (`Validate`), `AbsorbWithAttribute.cs`, `ReduceByAttribute.cs`, `AbilitySystemComponent.cs` (`Validate`), `Tests/GAS/EditMode/DamageValidationTests.cs` (novo), `manual/ferramentas/validador.md`, `CHANGELOG.md`.
- Testes red: um por regra do Design 7 (CA26), com `propertyPath`; `InvalidDamageTag_ValidatorPoints` (CA14 completo); `MetaWithoutProcessors_Warning` (CA9 completo).
- Critério de pronto: CA9, CA14 e CA26 completos.
- Tamanho estimado: ~380 linhas; 8 arquivos. Depende de: DM5, AR6, AR7.

### DM8 — Inspector e prévia do dano
- Escopo: `Editor/GAS/AttributeDefinitionEditor.cs` (seção "Dano"), `Editor/GAS/DamagePreviewModel.cs` (novo), `Tests/GAS/EditMode/DamagePreviewModelTests.cs` (novo), página de dano (três exemplos completos, o que o GAS não faz), `gas.md` (links nas páginas de Atributos e Efeitos), `manual/exemplos/sandbox.md` (nota: o Sample pode ganhar a morte depois, R19), `CHANGELOG.md` (R25 completo).
- Testes red: `Preview_Shield50Damage30_ListsShieldMinus30HealthZero`; `Preview_DestroysTemporaryAsc`; `Preview_ReorderChangesResult`.
- Critério de pronto: testes verdes; CA25 (S) no host; CA27 e CA29 (E) conferidos pelo Examinador.
- Tamanho estimado: ~420 linhas; 7 arquivos. Depende de: DM7, AV1 (`SummaryFormatter`), AR6.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| DM1a | sim | AR3, VN7, EA1 | ~480 / 9 |
| DM1b | sim | DM1a | ~320 / 8 |
| DM2 | sim | DM1b, TG2 | ~360 / 6 |
| DM3 | sim | DM2, AR2 | ~320 / 4 |
| DM4 | sim | DM3 | ~260 / 5 |
| DM5 | sim | DM4, AR2 | ~460 / 7 |
| DM6 | sim | DM5, RL4, AV5 | ~400 / 8 |
| DM7 | sim | DM5, AR6, AR7 | ~380 / 8 |
| DM8 | sim | DM7, AV1, AR6 | ~420 / 7 |

Fila: DM1a, DM1b e DM2 a DM8 em série. O DM1a fica com a issue #166; o DM1b ganha issue nova. CA23 e CA28 rodam em todos.

### Emenda de 2026-11-02: lote 8 e saída gerada

Estado: aprovada no gate parcial de 2026-11-02, junto com a emenda de 2026-11-02 do lote 8 da `referencia-catalogo-e-api.md`, com L1, L2 e L3 como propostos (respostas lá). Os exemplos completos que o DM8 escreve à mão na página de dano são migrados pelo EX6, no lote 9.

- O DM7 e o DM8 entram no lote 8, nesta ordem, na fila descrita na Spec `referencia-catalogo-e-api.md` (seção "Lote 8 e pares do WIP 2").
- Saída gerada: o DM7 e o DM8 mudam `.cs` do pacote. Os dois regeneram e commitam `Documentation~/manual/referencia/catalogo.md`, `Documentation~/api/reference/**` e `Documentation~/api/reference.sha256` (D6 da `referencia-catalogo-e-api.md`). Esses caminhos ficam no escopo dos dois como saída gerada, fora da contagem (emenda "saída de gerador commitada" da 0003). A geração passou a existir depois desta Spec, e é por isso que os escopos acima não a citam.
- Exemplos do manual: o EX1 entra antes do DM8. A partir dele, a guarda `NoHandWrittenCSharpBlocks` aceita bloco C# escrito à mão só nas páginas da lista de pendências. A página de dano e o `gas.md` estão nela, então os exemplos completos do DM8 podem ser escritos à mão, e o EX6 os migra. O `exemplos/sandbox.md` não está na lista: a nota do DM8 nessa página não pode ter bloco C#.
- O DM7 vem antes do EX2, porque os dois escrevem `manual/ferramentas/validador.md`.

| Ticket | Par com Unity (WIP 2) | Por quê |
|---|---|---|
| DM7 | nenhum | o único EX que poderia rodar junto é o EX1, que também escreve o `CHANGELOG.md`; o EX2 escreve `validador.md`, como o DM7 |
| DM8 | EX2 | nenhum arquivo em comum, e nenhum dos dois depende do outro (lista de arquivos na `referencia-catalogo-e-api.md`) |

### Emenda de 2026-11-03: documentação no fim e detalhe na entrada

Estado: decisão do usuário, aprovada pelo usuário em 2026-11-03 (emendas de 2026-11-03 da 0002, da 0003, da 0007 e da 0040; Spec `processo-doc-no-fim.md`).

- A regra "regeneram e commitam" da emenda anterior vale só para o que já foi feito. Daqui em diante, nenhum ticket desta Spec regenera a saída gerada nem escreve no manual. As duas coisas ficam para o passe de documentação. O CHANGELOG fica só no ticket que quebra algo.
- Os tickets desta Spec que ainda não entraram na fila mantêm o objetivo, os CA e as dependências. O escopo, os testes e o tamanho escritos são rascunho, e o Arquiteto os refaz na entrada (emenda de 2026-11-03 da 0002).

## Perguntas ao usuário

- **Q1. Processadores só na definição do atributo (ADR 0033, item 1).** Proposta: a lista mora só na `AttributeDefinition`; o que muda por entidade vem de atributos (a resistência de um chefe é um atributo dele). Alternativa: lista também no `AttributeSet` (por entidade), com ordem fixa definição → entidade, o que dá duas fontes para ler e validar.
- A ADR 0033 passa pelo gate junto com esta Spec.
