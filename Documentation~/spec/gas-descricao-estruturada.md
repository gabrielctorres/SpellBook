# Spec: Descrição estruturada e texto localizável
PRD: [Documentation~/prd/gas-descricao-estruturada.md](../prd/gas-descricao-estruturada.md) · Backlog: B-008 · ADRs: **0038 (nova, Proposta)**; usa 0003, 0004, 0007, 0009, 0012, 0013, 0017, 0019, 0022, 0023, 0026, 0029, 0030, 0032

Status: Proposta para o gate do Lote E · Data: 2026-10-31 · Autor: Arquiteto

## Contexto do código

Caminhos relativos a `Packages/com.spellbook/`. As linhas são as de hoje; os Lotes A a D mudam parte delas antes deste item.

- `Runtime/GAS/Abilities/AbilityDescription.cs:11-106`: `DescriptionElement.GetText(GameplayAbility, AbilitySystemComponent)` devolve `string`; `TextElement`, `AttributeElement` (cor e `<color>` fixos, `:28-38`), `TemplateElement` (`Replace` por mapeamento, `:65-89`) e `AbilityDescription.GetFullDescription` (`StringBuilder` por chamada, `:99-105`). Nenhum teste usa esses tipos (busca de 2026-10-31).
- `Runtime/GAS/Abilities/GameplayAbility.cs:21-27`: `Icon`, `DisplayName` (`string` público), `Description`; `:99-103` `GetDescription(source)`.
- `Runtime/GAS/Attributes/AttributeDefinition.cs:15-31`: `Icon` e `DisplayName` (`string` público).
- Do que já foi aprovado e este item usa:
  - B-005: `TextRole`/`TextRoleTheme` em `Runtime/Core/Text/` (AR4); `AttributeDisplay.Format`, `GetDeltaRole`, `Hidden` e `AttributeCategory.DisplayName` (AR5); `GameplayEffect.DisplayName` (`string`) e `Icon` (AR7, emenda do Lote E).
  - B-007: `ScalableFloat` (VN1), migração com campo legado e `GasAssetMigration` (VN2, VN3), `AbilityValues` e `GetValue(nome, nível)` (VN4), `EffectContext` (VN5), `ModifierValueSource.GetValue(in EffectContext)` (VN6), `StacksMultiplyMagnitude` (VN7), "nível de prévia" no `GameplayAbilityEditor` (VN10).
  - B-003: `CooldownReductionPolicy` (EA6). B-009: `ChargesConfig`, `ApplyCooldownReduction`, `ActivationFailure`, `ActivationRule` (AV4, AV6). B-012: `GameplayAbilityEditor` com abas (EV17).
- Sample: `Samples~/Sandbox/Data/Abilities/GA_BolaDeFogo.asset:16-20` (`DisplayName` vazio, `Elements: []`) e `Data/Attributes/ATR_Vida.asset`, `ATR_Mana.asset` (`DisplayName` vazio).
- Testes de estrutura: `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:58-77` (runtime sem `defineConstraints`), `:341-355` (lista exata de assemblies).

## Respostas às perguntas técnicas do PRD (seção 8)

- **P1. Onde mora o `ITextProvider`.** No `SpellBook.Core`, em `Runtime/Core/Text/` (ADR 0038, item 1); sem módulo novo. O adaptador é o módulo opcional `SpellBook.Localization` (e o `.Editor`), compilado só com `com.unity.localization` (ADR 0038, item 6).
- **P2. Chaves e campos.** Convenção `<módulo>.<área>.<nome>` (ADR 0038, item 3). Todo texto visível guardado em asset passa a `LocalizedText` (chave opcional + texto padrão). Os quatro `DisplayName` (`GameplayAbility`, `GameplayEffect`, `AttributeDefinition`, `AttributeCategory`) mudam de tipo com campo legado `[FormerlySerializedAs]` convertido no `OnAfterDeserialize`, como o VN2; o menu do VN3 passa a regravar também habilidades, atributos e categorias (DE5). Asset antigo abre com o texto que tinha.
- **P3. Migração do `DescriptionElement`.** Contrato novo: `public abstract void Describe(DescriptionContext context)`, que acrescenta linhas a `context.Lines`. O `GetText` e o `GetFullDescription` somem sem overload (DE8). Migram: os três elementos do pacote (com `[MovedFrom]`, porque mudam de namespace) e o campo `Description` da habilidade. Não há teste nem asset com elemento preenchido para migrar (o Sample tem `Elements: []`); o CA17 não lista nenhum teste trocado.
- **P4. Uma struct de linha.** `DescriptionLine`, uma só, com `Kind` e marcas em `[Flags]` (`Variable`, `Invalid`, `Permanent`). O texto do valor não é guardado na linha: o `DescriptionWriter` o escreve num `TextBuffer` quando alguém pede (formatador do R8). Assim `Describe` não aloca, e a linha continua tendo "texto do valor" para quem chama (CA1).
- **P5. `DescriptionContext`.** Classe reaproveitada por quem chama (`Reset(...)` entre usos), não `struct`: ela é dona da lista de linhas e o elemento precisa acrescentar. Para avaliar valores, monta um `EffectContext` (B-007) com instigador = ASC de origem (pode ser nulo), habilidade e nível; não há contexto paralelo. Ela não tem relação com o `ActionContext` do B-012: descrever não executa ação.
- **P6. Cultura da prévia.** Campo do Inspector (`DescriptionPreviewElement`), padrão `CultureInfo.CurrentCulture` do Editor, guardado em `SessionState`. O Editor monta o `DescriptionContext` com essa cultura e um provedor opcional escolhido no Inspector; o runtime não lê cultura de lugar nenhum.
- **P7. Ordem.** Texto e provedor (DE1–DE4) primeiro, sem dependência de GAS; nomes com `LocalizedText` (DE5, DE6); `EffectDescriber` (DE7); elementos (DE8–DE10); motivo de falha (DE11); validação (DE12); prévia (DE13).

## Design

### 1. Texto no Core (R1, R2, R4; ADR 0038)

`Runtime/Core/Text/`, namespace `SpellBook.Core.Text`:

```csharp
public interface ITextProvider
{
    IFormatProvider Culture { get; }                 // nunca nulo; invariante quando vazio
    string GetText(string key, string fallback);     // chave nula, vazia ou ausente: fallback
    event Action Changed;                            // cultura ou tabela trocada
}

public abstract class TextProviderAsset : ScriptableObject, ITextProvider { ... }

[Serializable] public struct LocalizedText
{
    [SerializeField] private string key;   // opcional
    [SerializeField] private string text;  // texto padrão
    public string Key => key; public string Text => text ?? string.Empty;
    public LocalizedText(string key, string text);
    public string Resolve(ITextProvider provider);    // provider nulo: Text
    public bool IsEmpty => string.IsNullOrEmpty(key) && string.IsNullOrEmpty(text);
}

public sealed class TextBuffer
{
    public char[] Chars { get; }  public int Length { get; }
    public void Clear();
    public TextSpan Append(string s);                                   // devolve o trecho escrito
    public TextSpan Append(float value, string format, IFormatProvider culture); // float.TryFormat, sem alocar
    public TextSpan Append(TextSpan other);
    public string ToString(TextSpan span);                               // aloca, só quando pedido
}
public readonly struct TextSpan { public TextBuffer Buffer { get; } public int Start { get; } public int Length { get; } public bool IsEmpty { get; } }

public static class TextKey { public static bool IsValid(string key); }  // ^[a-z0-9_]+(\.[a-z0-9_]+)+$
```

- `TextTable : ScriptableObject` (`Create > SpellBook > Text > Text Table`): `cultureName` e `List<Entry { string key; string text; }>`; `IValidatable`: chave fora de `TextKey`, aviso; chave repetida, erro.
- `TableTextProvider : TextProviderAsset` (`Create > SpellBook > Text > Table Text Provider`): `List<TextTable> tables`, `cultureName`; `SetCulture(CultureInfo)`; dicionário `Dictionary<string, string>` montado na primeira leitura depois de cada troca (ordem do item 2 da ADR 0038); `IValidatable`: tabela nula, erro; mesma chave com textos diferentes em tabelas da mesma cultura, aviso.
- Plural e gênero: fora do contrato (R5).

### 2. Adaptador do Unity Localization (R3; ADR 0038, itens 6 a 8)

- `Runtime/Localization/SpellBook.Localization.asmdef`: referências `SpellBook.Core`, `Unity.Localization`; `versionDefines` `com.unity.localization` → `SPELLBOOK_UNITY_LOCALIZATION`; `defineConstraints` `[SPELLBOOK_UNITY_LOCALIZATION]`; `autoReferenced` true.
- `LocalizationTextProvider : TextProviderAsset`: `TableReference table`; `Culture` = a do `Locale` selecionado; cache `Dictionary<string, string>` limpo na troca de locale (assina o evento de locale do pacote e dispara `Changed`). A API exata (busca síncrona de entrada da tabela e evento de troca) é conferida na versão instalada no DE3 (CLAUDE.md: verificar a API antes de usar).
- `Editor/Localization/SpellBook.Localization.Editor.asmdef` com a mesma constraint e o Inspector de uma linha.

### 3. Nomes exibidos com `LocalizedText` (R4, N4)

- `GameplayAbility.DisplayName`, `AttributeDefinition.DisplayName` (campos públicos `string`) e `GameplayEffect.displayName`, `AttributeCategory.DisplayName` passam a `LocalizedText`. Cada dono ganha `[SerializeField, HideInInspector, FormerlySerializedAs("<nome antigo>")] private string legacyDisplayName` (padrão `null`) e converte em `OnAfterDeserialize` (`ISerializationCallbackReceiver`; o `GameplayEffect` já o tem pelo VN2): legado não nulo vira `new LocalizedText(null, legado)` e volta a `null`.
- `GasAssetMigration` (VN3) passa a carregar e regravar também `GameplayAbility`, `AttributeDefinition` e `AttributeCategory`.
- Os campos legados entram na pendência do B-007 ("remover os campos legados antes da 3.0 final").

### 4. Linhas, contexto e descrição de efeito (R6 a R9)

Em `Runtime/GAS/Description/`, namespace `SpellBook.GAS.Description`:

```csharp
public enum DescriptionLineKind { Text, Attribute, Modifier, Cost, Cooldown, Duration, Charges, Failure }
[Flags] public enum DescriptionLineFlags { None = 0, Variable = 1, Invalid = 2, Permanent = 4 }

public readonly struct DescriptionLine
{
    public DescriptionLineKind Kind { get; }
    public LocalizedText Label { get; }            // rótulo por chave (e padrão)
    public float Value { get; }                    // valor numérico (já com pilhas e nível)
    public float SecondaryValue { get; }           // cargas: intervalo; custo: nada
    public ModifierOperation Operation { get; }    public bool HasOperation { get; }
    public TextRole Role { get; }
    public AttributeDefinition Attribute { get; }  // pode ser nulo
    public GameplayTag Tag { get; }                // motivo de falha por tag
    public DescriptionLineFlags Flags { get; }
    public DescriptionElement Source { get; }      // o elemento que gerou (template, texto)
}

public sealed class DescriptionContext
{
    public GameplayAbility Ability; public GameplayEffect Effect; public int Level = 1; public int Stacks = 1;
    public AbilitySystemComponent Source;          // opcional
    public ITextProvider Text;                     // nulo: textos padrão, cultura invariante
    public ModifierTextFormatter Formatter;        // nulo: DefaultModifierTextFormatter
    public List<DescriptionLine> Lines { get; }    // não é limpa por Describe
    public IFormatProvider Culture => Text?.Culture ?? CultureInfo.InvariantCulture;
    public void Reset(GameplayAbility ability, int level, AbilitySystemComponent source = null);
    public EffectContext EvaluationContext { get; } // instigador = Source, habilidade, nível; sem SetByCaller
}
```

- `EffectDescriber.Describe(GameplayEffect effect, DescriptionContext context)` (`static`): uma linha `Modifier` por item de `Modifiers` e de `PeriodicModifiers` cujo atributo não é `Display.Hidden`; entrada nula pulada. Valor: `ValueSource.GetValue(in EffectContext)` (VN6) multiplicado por `context.Stacks` quando `StacksMultiplyMagnitude` (VN7). `Variable` quando o valor depende do nível (`ScalableFloat` fora de `Constant`), de `SetByCaller`, de atributo do instigador ou do alvo, ou de fórmula; sem instigador, as origens de atributo dão `Value = 0` com `Variable`. Papel: `Attribute.Display.GetDeltaRole(delta)`, com delta = valor (`Add`, `AddFinal`) ou valor ≠ 0 com o sinal da variação (`Multiply`, `MultiplyCompound`); `Override` é `Value`. Rótulo: `Attribute.DisplayName`.
- `ModifierTextFormatter` (`[Serializable] abstract`, `public abstract void WriteValue(in DescriptionLine line, TextBuffer into, IFormatProvider culture)`) e `DefaultModifierTextFormatter`, que chama `AttributeDisplay.Format(TextBuffer, valor, operação, cultura)`, a sobrecarga sem alocação que este item acrescenta ao `AttributeDisplay` do AR5 (tabela estática de formatos, `TextBuffer.Append`). O PRD chama o tipo de `IModifierTextFormatter`: vira classe abstrata pela regra da 0013 (pontos de extensão do Inspector são classes abstratas).
- `DescriptionWriter` (`static`): `WriteValue(in DescriptionLine, DescriptionContext, TextBuffer)` (formatador para `Modifier`; segundos pelo `ValueKind.Seconds` para recarga, duração e cargas) e `WriteLine(in DescriptionLine, DescriptionContext, TextBuffer)` (texto simples da linha: rótulo resolvido pelo provedor e, quando o rótulo tem `{0}` e `{1}`, os valores escritos no lugar, sem `string.Format`). Nenhuma saída tem marcação (CA8). É o que a prévia (DE13) e as fontes de tooltip (B-017) usam.

### 5. Elementos (R10 a R15)

- `DescriptionElement` (`[Serializable] abstract`, `Runtime/GAS/Description/DescriptionElement.cs`): `public abstract void Describe(DescriptionContext context)`, `public virtual void Validate(ValidationResult result, string propertyPath, GameplayAbility owner)`. `AbilityDescription` (mesmo arquivo): `[SerializeReference, TypePicker] List<DescriptionElement> Elements` e `Describe(DescriptionContext)` na ordem, pulando nulos. `GameplayAbility.Describe(DescriptionContext)` repassa; sai o `GetDescription`.
- Prontos, `[Serializable] sealed`, com `[TypeCategory("Descrição/...", Line = ...)]`:
  - `TextElement` (`LocalizedText text`) → linha `Text`;
  - `AttributeElement` (`AttributeDefinition attribute`) → linha `Attribute` com o valor atual do ASC de origem (`TryGetAttributeValue`, EA1); sem ASC, `Variable` e valor 0;
  - `EffectModifiersElement` (`GameplayEffect effect`) → `EffectDescriber.Describe` com `context.Effect = effect` durante a chamada;
  - `CostElement` → uma linha `Cost` por modificador do `CostEffect`, valor absoluto no nível, papel `Value`; sem custo, nada;
  - `CooldownElement` → linha `Cooldown`: `CooldownEffect.Duration.Evaluate(nível, tabela)` e, com ASC de origem, `ApplyCooldownReduction(base, asc)` (AV6, que usa a política do EA6); sem recarga, nada (C6);
  - `DurationElement` (`GameplayEffect effect`) → linha `Duration` no nível; infinito: `Permanent`, rótulo `gas.description.permanent`; instantâneo: nada;
  - `ChargesElement` → linha `Charges` com `Value` = máximo (`floor`, nível) e `SecondaryValue` = recarga no nível (com redução quando há ASC), rótulo `gas.description.charges` ("{0} cargas, uma a cada {1}"); sem cargas, nada;
  - `TemplateElement` (`LocalizedText template`) → linha `Text` com `Source = this`. O texto é expandido pelo `DescriptionWriter`: `{nome}` lê `Ability.TryGetValue(nome, nível)` (VN4); os atalhos reservados `{@cooldown}`, `{@cost}` e `{@charges}` usam a mesma conta dos elementos acima (a lista do R15 fica assim: `{duração}` não entra, porque a habilidade não tem uma duração única). Nome inexistente: a linha sai com `Invalid` e o trecho fica literal. O elemento guarda em cache (`[NonSerialized]`, como o `ScalableFloat` do VN1) os trechos já analisados do template, refeitos quando o texto muda; a comparação de nome usa `AbilityValues.IndexOf(ReadOnlySpan<char>)`, sobrecarga que este item acrescenta.
- Elemento próprio do projeto: subclasse de `DescriptionElement` que acrescenta linhas (C14).

### 6. Motivo de falha (R16)

`ActivationFailureDescriber.Describe(in ActivationFailure failure, DescriptionContext context)` acrescenta uma linha `Failure`, papel `Harmful`, `Tag = failure.ReasonTag`, rótulo `failure.ReasonKey` ou, quando nulo, a chave do `Result` em `GasTextKeys` (`gas.activation.on_cooldown`, `no_charges`, `cannot_afford_cost`, `blocked_by_tags`, `missing_required_tags`, `blocked_by_active_ability`, `already_active`, `not_granted`, `invalid`, `blocked_by_condition`). As chaves do pacote (estas e `gas.description.*`) têm texto nas tabelas `Runtime/GAS/Text/GasTexts.pt-BR.asset` e `GasTexts.en.asset`.

### 7. Validação e prévia (R17, R18)

- `GameplayAbility.Validate` repassa a `Description.Elements`: elemento nulo, erro; `TemplateElement` com `{nome}` fora da tabela, erro; `CostElement` sem `CostEffect`, `CooldownElement` sem `CooldownEffect`, `ChargesElement` sem cargas, aviso; `TextElement` vazio, erro; a mesma chave com textos padrão diferentes entre elementos da habilidade, aviso. Mensagens com `propertyPath`.
- `Editor/GAS/DescriptionPreviewElement.cs` (`VisualElement`): nível (o "nível de prévia" do VN10), cultura (campo, padrão a do Editor) e provedor (`ObjectField` opcional de `TextProviderAsset`); mostra uma linha de texto simples por `DescriptionLine`, refeita no `TrackSerializedObjectValue` e ao trocar nível, cultura ou provedor. Entra numa seção "Descrição" do `GameplayAbilityEditor`.

### 8. O que não muda

- Formato do `AttributeDisplay` (AR5) e a regra de cultura nula do B-005 (Q3).
- O GAS não referencia UI, Events, Pooling, Teams, Cues nem `Unity.Localization`.
- Assets antigos abrem com o texto que tinham.

## Quebras de compatibilidade

Para o CHANGELOG e `manual/regras/migracao-2-para-3.md` (ADR 0009):
- **`DescriptionElement` muda de contrato:** `GetText(ability, source)` vira `Describe(DescriptionContext)`; `AbilityDescription.GetFullDescription` e `GameplayAbility.GetDescription` saem. Para texto, use `DescriptionWriter.WriteLine`. Elementos próprios são reescritos (exemplo na página).
- **Elementos mudam de namespace** (`SpellBook.GAS.Abilities` → `SpellBook.GAS.Description`), com `[MovedFrom]`; `AttributeElement` perde `Color`, `Format` e `Placeholder` (a view escolhe); `TemplateElement` perde `Mappings` (usa a tabela `%nome`).
- **`DisplayName` vira `LocalizedText`** em habilidade, efeito, atributo e categoria; assets convertem ao carregar; rode `SpellBook > Migrar assets do GAS (3.0)`. Código: `ability.DisplayName = "x"` vira `new LocalizedText(null, "x")`; ler o texto: `.Resolve(provedor)` ou `.Text`.
- Adicionado: `ITextProvider`, `TextProviderAsset`, `LocalizedText`, `TextBuffer`, `TextSpan`, `TextKey`, `TextTable`, `TableTextProvider`, adaptador `SpellBook.Localization`, `DescriptionLine`, `DescriptionContext`, `EffectDescriber`, `ModifierTextFormatter`, `DescriptionWriter`, elementos novos, `ActivationFailureDescriber`, tabelas de texto do pacote.

## Tickets

Todos usam a Unity e seguem o WIP 1. **Não há par paralelo sem Editor.** Cada ticket escreve a sua parte da página e do `CHANGELOG.md`. Cultura sempre injetada nos testes (`CultureInfo.GetCultureInfo("pt-BR")`, `"en-US"`); objetos por `GasFactory`; alocação por `Is.Not.AllocatingGCMemory()` com aquecimento. Teto: ~500 linhas e 10 arquivos (0003).

**Conflitos com a fila:** DE1 a DE4 tocam só Core, `Localization` e testes de estrutura (`AsmdefRulesTests.cs`, `SpellBookEditorCoverageTests.cs`, `DocUrls.cs`, também tocados por CU2, CU7, TM* e pelos tickets de UI) e podem entrar quando a fila do Unity tiver folga, depois do AR4 (mesma pasta). DE5 a DE13 tocam `GameplayAbility.cs`, `GameplayEffect.cs`, `AttributeDefinition.cs`, `AttributeDisplay.cs`, `GameplayAbilityEditor.cs` e `validador.md` (Lotes B a D): entram depois do EV21, do CU9 e do TK8. DE6 toca assets do Sample (VN3, EV21).

### DE1 — `TextBuffer`, `LocalizedText` e `ITextProvider`
- Escopo: `Runtime/Core/Text/TextBuffer.cs` (com `TextSpan`), `LocalizedText.cs`, `TextProvider.cs` (`ITextProvider`, `TextProviderAsset`, `TextKey`) (novos), `Tests/Core/EditMode/Text/TextBufferTests.cs`, `Tests/Core/EditMode/Text/LocalizedTextTests.cs` (novos), `CHANGELOG.md`.
- Testes red: `Append_Float_F1_PtBrComma_EnUsDot`; `Append_10000Floats_ReusedBuffer_DoesNotAllocate`; `Span_ToString_OnlyThatSpan`; `Grow_KeepsContent`; `LocalizedText_NullProvider_ReturnsText`; `LocalizedText_KeyFound_ProviderText_KeyMissing_Fallback` (provedor de teste); `TextKey_Valid_gas_activation_no_charges_Invalid_Upper_Space_SingleSegment`.
- Critério de pronto: testes verdes; `AsmdefRules_CoreDoesNotReferenceGas` verde.
- Tamanho estimado: ~360 linhas; 6 arquivos. Depende de: nada (fila: depois do AR4).

### Detalhe (2026-11-03)
- CA: CA3 (parte: número com cultura injetada no `TextBuffer`), CA9 (parte: chave presente, ausente e provedor ausente no `LocalizedText`), CA12 (parte: `Append` de número sem alocar depois do aquecimento). Base do Design 1 (ADR 0038, itens 1, 3 e 5).
- Escopo (caminhos em `Packages/com.spellbook/`, namespace `SpellBook.Core.Text`, ao lado de `Runtime/Core/Text/TextRoleTheme.cs`, que fica como está):
  - `Runtime/Core/Text/TextBuffer.cs` (novo): `TextBuffer` (`char[]` que dobra ao crescer, `Chars`, `Length`, `Clear`, `Append(string)`, `Append(float, string, IFormatProvider)` por `float.TryFormat` num `Span<char>` do próprio array, `Append(TextSpan)`, `ToString(TextSpan)` e `ToString()`) e `TextSpan` (`readonly struct`: `Buffer`, `Start`, `Length`, `IsEmpty`). Cultura nula: invariante (mesma regra do `AttributeDisplay`, Q3 do B-005).
  - `Runtime/Core/Text/LocalizedText.cs` (novo): `[Serializable] struct` com `key`, `text`, `Key`, `Text`, `IsEmpty`, construtor e `Resolve(ITextProvider)`.
  - `Runtime/Core/Text/TextProvider.cs` (novo): `ITextProvider`; `TextProviderAsset` (abstrata, `Culture` e `GetText` abstratos, `Changed` com `protected void RaiseChanged()`; sem `[HelpURL]`, que o `HelpUrlRule` pede só a tipo concreto); `TextKey.IsValid` por varredura de caracteres, sem `Regex`.
  - `Tests/Core/EditMode/Text/TextBufferTests.cs` e `Tests/Core/EditMode/Text/LocalizedTextTests.cs` (novos; o segundo com um `TestTextProvider` de dicionário e os testes do `TextKey`).
  - Sem `CHANGELOG.md`: o ticket só acrescenta tipos e não quebra nada (emenda de 2026-11-03 da 0007); os tipos novos entram no CHANGELOG da quebra do DE5/DE8. Sem `DocUrls.Text` (DE2).
- Testes red (EditMode, `SpellBook.Core.Tests.EditMode`): `TextBufferTests`: `Append_Float_F1_PtBrComma_EnUsDot`, `Append_10000Floats_ReusedBuffer_DoesNotAllocate` (aquece com o mesmo laço: o formatador do runtime inicializa tabelas na primeira chamada), `Span_ToString_OnlyThatSpan`, `Grow_KeepsContent`, mais `Append_NullCulture_UsesInvariant`, `Append_SpanFromOtherBuffer_CopiesChars`, `Clear_ResetsLength_KeepsCapacity`; `LocalizedTextTests`: `LocalizedText_NullProvider_ReturnsText`, `LocalizedText_KeyFound_ProviderText_KeyMissing_Fallback`, `TextKey_Valid_gas_activation_no_charges_Invalid_Upper_Space_SingleSegment`, mais `LocalizedText_IsEmpty_OnlyWithoutKeyAndText`.
- Critério de pronto: os 11 testes verdes pelo filtro `\.TextBufferTests\.;\.LocalizedTextTests\.`; `AsmdefRules_CoreDoesNotReferenceGas` e `HelpUrlTests` verdes.
- Tamanho: ~460 linhas e 5 arquivos (mais esta página do Spec), pela ADR 0042. Exceção: nenhuma.
- Commit: `feat(core): TextBuffer, LocalizedText e ITextProvider (#222)`

### DE2 — `TextTable` e `TableTextProvider`
- Escopo: `Runtime/Core/Text/TextTable.cs`, `TableTextProvider.cs` (novos), `Editor/Core/Inspector/CoreEditors.cs` (duas linhas), `Runtime/Core/DocUrls.cs` (`Text = Base + "modulos/texto/"`), `Tests/Core/EditMode/DocUrlsTests.cs`, `Tests/Core/EditMode/Text/TableTextProviderTests.cs` (novo), `Documentation~/manual/modulos/texto.md` (nova: provedor, tabela, convenção de chaves), `CHANGELOG.md`.
- Testes red: `KeyInTable_ReturnsText` (CA9); `MissingKey_ReturnsFallback`, `NoProvider_FieldText` (CA9); `CultureChain_PtBr_Pt_Neutral_Fallback`; `SetCulture_RaisesChanged_CultureExposed`; `EmptyCulture_Invariant`; `ResolvedKey_10000Reads_DoesNotAllocate` (N1); `Table_DuplicateKey_Error_BadKey_Warning`; `Provider_SameKeyDifferentText_Warning`; `DocUrls_PageConstantPointsToPageUnderBase("Text", "modulos/texto/")`.
- Critério de pronto: CA9 verde; `HelpUrlTests`, `SpellBookEditorCoverageTests`, `ManualLinksTests` verdes.
- Tamanho estimado: ~430 linhas; 8 arquivos. Depende de: DE1.

### DE3 — Adaptador do Unity Localization: código e testes
- Escopo: `Packages/manifest.json` do projeto host (+`com.unity.localization`, só se a Q2 for aprovada) e o `packages-lock.json` que a Unity regrava, `Runtime/Localization/SpellBook.Localization.asmdef`, `Runtime/Localization/LocalizationTextProvider.cs`, `Tests/Localization/EditMode/SpellBook.Localization.Tests.EditMode.asmdef` (mesma constraint), `Tests/Localization/EditMode/LocalizationTextProviderTests.cs`, `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs` (`Localization` em `RuntimeModules`; exceção do módulo opcional, ADR 0038 item 7), `CHANGELOG.md`.
- Testes red: `EntryInStringTable_ReturnsLocalized`; `MissingEntry_ReturnsFallback`; `LocaleChange_RaisesChanged_CultureFollowsLocale`; `ResolvedKey_DoesNotAllocate`; `OptionalModule_OnlyConstraintIsOwnVersionDefine`.
- Critério de pronto: testes verdes com o pacote no host; a API usada foi conferida na versão instalada (registrada no PR).
- Tamanho estimado: ~330 linhas; 8 arquivos. Depende de: DE2, ADR 0038 aceita, Q2.

### DE4 — Adaptador: Inspector, guardas de isolamento e página
- Escopo: `Editor/Localization/SpellBook.Localization.Editor.asmdef`, `Editor/Localization/LocalizationEditors.cs` (novos), `AsmdefRulesTests.cs` (`Localization` em `EditorModules`; ninguém fora do adaptador referencia `Unity.Localization` nem os asmdefs dele), `Tests/Core/EditMode/Structure/PackageJsonTests.cs` (novo ou caso novo no teste existente de `package.json`: não declara `com.unity.localization`), `SpellBookEditorCoverageTests.cs` (+`SpellBook.Localization`), `texto.md` (seção do adaptador), `CHANGELOG.md`.
- Testes red: `NoAsmdefOutsideAdapter_ReferencesUnityLocalization` (CA10); `PackageJson_DoesNotDeclareLocalization` (CA10); `LocalizationEditor_ConstraintMatchesRuntime`.
- Critério de pronto: CA10 (E) verde.
- Tamanho estimado: ~220 linhas; 7 arquivos. Depende de: DE3.

### DE5 — Nomes exibidos com `LocalizedText` (quebra com migração)
- Escopo: `Runtime/GAS/Abilities/GameplayAbility.cs`, `Runtime/GAS/Effects/GameplayEffect.cs`, `Runtime/GAS/Attributes/AttributeDefinition.cs`, `Runtime/GAS/Attributes/AttributeCategory.cs` (campo novo, legado, conversão), `Editor/GAS/GasAssetMigration.cs`, `Tests/GAS/EditMode/DisplayNameMigrationTests.cs` (novo), testes que escrevem `DisplayName` (lista no PR), `gas.md`, `CHANGELOG.md` (quebra).
- Testes red: `LegacyYaml_AbilityDisplayName_LoadsAsText` e o mesmo para atributo, efeito e categoria (YAML 2.x temporário em `Assets/`, como o VN2); `NewAsset_SavesWithoutLegacy`; `Migrate_RewritesAbilitiesAttributesCategories_Idempotent`; `DisplayName_Resolve_KeyThroughProvider`.
- Critério de pronto: testes verdes; suíte do GAS verde com os testes editados listados no PR.
- Tamanho estimado: ~420 linhas; até 10 arquivos. Depende de: DE1, AR5, AR7, VN3 (fila: depois do EV21, do CU9 e do TK8).

### DE6 — Sample no formato novo
- Escopo: `Samples~/Sandbox/Data/Abilities/GA_BolaDeFogo.asset`, `Data/Attributes/ATR_Vida.asset`, `ATR_Mana.asset` (regravados pelo menu, com a pasta renomeada pela ADR 0028), `Documentation~/manual/regras/migracao-2-para-3.md` (nomes exibidos), `CHANGELOG.md`.
- Testes red: nenhum novo; as guardas do SB-T1 seguem verdes.
- Critério de pronto: os três assets abrem sem "Missing" e sem legado no YAML (o Examinador confere).
- Tamanho estimado: 0 linhas de código; 5 arquivos (3 fixtures). Depende de: DE5, EV21.

### DE7 — `DescriptionLine`, `DescriptionContext`, `EffectDescriber` e formatador
- Escopo: `Runtime/GAS/Description/DescriptionLine.cs`, `DescriptionContext.cs`, `EffectDescriber.cs`, `ModifierTextFormatter.cs` (com o padrão), `DescriptionWriter.cs` (novos), `Runtime/GAS/Attributes/AttributeDisplay.cs` (`Format` em `TextBuffer`), `Runtime/GAS/Effects/EffectContext.cs` (fábrica `ForDescription(instigador, habilidade, nível)`, só se o VN5 não tiver construtor público), `Tests/GAS/EditMode/EffectDescriberTests.cs` (novo), `CHANGELOG.md`.
- Testes red: `Armor20Percent_LineAttributeValueOpRoleBeneficial_ValueText` (CA1, C1); `Stacks3Level2_ValueComposed_VariableWhenLevelOrCaller` (CA2, C2); `Seconds1_5_PtBr_EnUs` (CA3, C3); `CustomFormatter_ChangesOnlyValueText` (CA4, C4); `HiddenAttribute_Skipped`; `Describe_AppendsWithoutClearing`; `WriterOutput_HasNoMarkup` (CA8, parte); `Describe10000_ReusedContext_DoesNotAllocate` (CA12, parte); `AttributeDisplay_FormatToBuffer_SameAsString`.
- Critério de pronto: CA1 a CA4 verdes; partes do CA8 e do CA12.
- Tamanho estimado: ~490 linhas; 9 arquivos. Depende de: DE1, AR5, VN6, VN7.

### DE8 — Contrato novo do `DescriptionElement` e elementos de texto, atributo e efeito
- Escopo: `Runtime/GAS/Abilities/AbilityDescription.cs` (apagado, com substituto), `Runtime/GAS/Description/DescriptionElement.cs` (base e `AbilityDescription`), `Runtime/GAS/Description/DescriptionElements.cs` (`TextElement`, `AttributeElement`, `EffectModifiersElement`, `[MovedFrom]`), `GameplayAbility.cs` (`Describe`; sai `GetDescription`), `Runtime/Core/DocUrls.cs` (`Description = Base + "modulos/descricao/"`), `DocUrlsTests.cs`, `Documentation~/manual/modulos/descricao.md` (nova), `Tests/GAS/EditMode/DescriptionElementsTests.cs` (novo), `Tests/GAS/EditMode/GasTextScanTests.cs` (novo; guarda textual do CA15), `CHANGELOG.md` (quebra).
- Testes red: `TextElement_KeyAndFallback`; `AttributeElement_SourceValue_NoSourceVariable`; `EffectModifiersElement_SameLinesAsDescriber`; `CustomElement_NewContract_AddsLines` (CA14, parte); `AbilityDescription_InOrder_NullSkipped`; `OldGetTextGone` (guarda textual); `GasRuntime_HasNoRichTextNorFixedCulture` (CA15: nenhum `.cs` de `Runtime/GAS/**` com `<color`, `<b>`, `<i>`, `<size` ou `new CultureInfo(`; `InvariantCulture` só no fallback do `AttributeDisplay` (Q3 do B-005) e no `ScalableFloat.TryParse` do Editor; red forçado).
- Critério de pronto: CA15 e parte do CA14 verdes; CA17 (suíte sem edição, nenhum teste trocado).
- Tamanho estimado: ~480 linhas; 10 arquivos. Depende de: DE7, DE5.

### DE9 — Custo, recarga, duração e cargas
- Escopo: `Runtime/GAS/Description/AbilityStatElements.cs` (novo: `CostElement`, `CooldownElement`, `DurationElement`, `ChargesElement`), `DescriptionWriter.cs` (rótulos com `{0}`/`{1}`), `Tests/GAS/PlayMode/AbilityStatElementsTests.cs` (novo), `descricao.md`, `CHANGELOG.md`.
- Testes red: `CostCooldownDurationCharges_OneLineEach_AtLevel3` (CA5, C5); `NoCost_NoCooldown_NoCharges_NoLine`; `Infinite_PermanentKey_Instant_NoLine`; `Cooldown_WithAsc_AfterPolicy_WithoutAsc_Base` (CA6, C6); `Charges_MaxFromTable_RechargeWithReduction`; `Writer_ChargesLabel_SubstitutesValues_NoMarkup`.
- Critério de pronto: CA5 e CA6 verdes.
- Tamanho estimado: ~440 linhas; 5 arquivos. Depende de: DE8, AV7, EA6.

### DE10 — `TemplateElement` por nível e atalhos
- Escopo: `DescriptionElements.cs` (`TemplateElement` com cache de trechos), `Runtime/GAS/Values/AbilityValues.cs` (`IndexOf(ReadOnlySpan<char>)`), `DescriptionWriter.cs` (expansão), `Tests/GAS/EditMode/TemplateElementTests.cs` (novo), `descricao.md`, `CHANGELOG.md`.
- Testes red: `Template_Dano_50_75_100_Level1Is50_Level3Is100` (CA7, C7); `UnknownName_InvalidLine_LiteralKept_NoException` (CA7); `Shortcuts_Cooldown_Cost_Charges_SameAsElements`; `TemplateEdited_CacheRebuilt`; `Template10000_DoesNotAllocate` (CA12, parte).
- Critério de pronto: CA7 verde; CA12 completo.
- Tamanho estimado: ~420 linhas; 6 arquivos. Depende de: DE9, VN4.

### DE11 — Motivo de falha e tabelas de texto do pacote
- Escopo: `Runtime/GAS/Description/ActivationFailureDescriber.cs`, `Runtime/GAS/Text/GasTextKeys.cs` (novos), `Runtime/GAS/Text/GasTexts.pt-BR.asset`, `GasTexts.en.asset` (fixtures), `Tests/GAS/EditMode/ActivationFailureDescriberTests.cs`, `Tests/GAS/EditMode/GasTextTablesTests.cs` (novos), `texto.md` (chaves do pacote), `CHANGELOG.md`.
- Testes red: `RuleFailure_KeyAndTag_Harmful` (CA11, C11); `ResultWithoutKey_MapsToPackageKey`; `EveryGasTextKey_HasTextInPtBrAndEn`; `GasTextKeys_FollowConvention`.
- Critério de pronto: CA11 verde.
- Tamanho estimado: ~320 linhas; 8 arquivos (2 fixtures). Depende de: DE10, DE2, AV5.

### DE12 — Validação da descrição
- Escopo: `DescriptionElement.cs`, `DescriptionElements.cs`, `AbilityStatElements.cs` (`Validate`), `GameplayAbility.cs` (repasse), `Tests/GAS/EditMode/DescriptionValidationTests.cs` (novo), `manual/ferramentas/validador.md`, `CHANGELOG.md`.
- Testes red: um por regra do Design 7, com `propertyPath` (CA14, validador); `ProjectValidator_ReportsAbilityAsContext`.
- Critério de pronto: CA14 completo.
- Tamanho estimado: ~380 linhas; 7 arquivos. Depende de: DE11, AV8.

### DE13 — Prévia por nível no Inspector e fechamento da documentação
- Escopo: `Editor/GAS/DescriptionPreviewElement.cs` (novo), `Editor/GAS/GameplayAbilityEditor.cs` (seção "Descrição"), `Tests/GAS/EditMode/DescriptionPreviewTests.cs` (novo), `descricao.md` (prévia, elemento e formatador próprios), `CHANGELOG.md` (R19 completo).
- Testes red: `Preview_LevelChange_UpdatesLines`; `Preview_AssetEdit_Updates`; `Preview_CultureField_DefaultIsEditorCulture`; `Preview_PlainText_NoMarkup`.
- Critério de pronto: testes verdes; CA13 (S) no host; CA16 (E) conferido.
- Tamanho estimado: ~360 linhas; 5 arquivos. Depende de: DE12, EV17.

### Ordem e resumo

| Ticket | Unity | Depende de | Tamanho |
|---|---|---|---|
| DE1 | sim | — (fila: depois do AR4) | ~360 / 6 |
| DE2 | sim | DE1 | ~430 / 8 |
| DE3 | sim | DE2, ADR 0038, Q2 | ~330 / 8 |
| DE4 | sim | DE3 | ~220 / 7 |
| DE5 | sim | DE1, AR5, AR7, VN3 (fila: depois do EV21, CU9, TK8) | ~420 / ≤10 |
| DE6 | sim | DE5, EV21 | ~0 / 5 |
| DE7 | sim | DE1, AR5, VN6, VN7 | ~490 / 9 |
| DE8 | sim | DE7, DE5 | ~480 / 10 |
| DE9 | sim | DE8, AV7, EA6 | ~440 / 5 |
| DE10 | sim | DE9, VN4 | ~420 / 6 |
| DE11 | sim | DE10, DE2, AV5 | ~320 / 8 |
| DE12 | sim | DE11, AV8 | ~380 / 7 |
| DE13 | sim | DE12, EV17 | ~360 / 5 |

Fila: DE1 → DE4 podem entrar cedo (só Core e o adaptador); DE5 → DE13 em série depois dos Lotes C e D. O B-015 precisa só do DE1 e do DE7; o B-016 precisa do DE11; o B-017 precisa do DE13. CA15 roda em todos depois do DE8.

## Depois deste item

- Os campos `legacyDisplayName` entram na pendência do B-007 (remover os campos legados antes da 3.0 final, depois do migrador).

## Perguntas ao usuário

- **Q2. `com.unity.localization` no projeto host.** Proposta: instalar só no `Packages/manifest.json` do projeto host (o `package.json` do SpellBook continua sem ele), para o adaptador ser compilado e testado no DE3. Alternativa: não instalar; o adaptador entra sem compilação verificada no repositório, só com a checagem estrutural do CA10.
- **Q4. Nomes exibidos como `LocalizedText`.** Proposta: os quatro `DisplayName` mudam de tipo, com campo legado convertido ao carregar e o menu de migração (padrão do VN2), removido junto da pendência do B-007. Alternativa: manter `string` e acrescentar um campo de chave ao lado de cada nome (sem migração, mas com dois campos para o mesmo texto e uma forma diferente da dos elementos).
- **Q5. Textos das chaves do pacote.** Proposta: duas tabelas no pacote (`pt-BR` e `en`) que o provedor do projeto lista; sem provedor, a linha mostra a chave. Alternativa: só a tabela `pt-BR`.
- A ADR 0038 passa pelo gate junto com esta Spec (e com as Specs de UI, que a usam).

## Respostas do gate (2026-11-01)

Spec aprovada no gate do Lote E, com a ADR 0038 (Aceita).
- Q2: aprovada como proposta. O `com.unity.localization` entra só no `Packages/manifest.json` do projeto host; o `package.json` do SpellBook não o declara. O DE3 instala o pacote no host.
- Q4: aprovada como proposta. Os quatro `DisplayName` viram `LocalizedText`, com campo legado convertido ao carregar e menu de migração (DE5); os `legacyDisplayName` entram na pendência do B-007.
- Q5: aprovada como proposta. Tabelas `pt-BR` e `en` no pacote (DE11).
- As respostas Q1, Q3 e Q6 do mesmo gate estão nas Specs do B-015, do B-016 e do B-018.
