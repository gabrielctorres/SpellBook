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
- **P7. Ordem.** Texto e provedor (DE1–DE4) primeiro, sem dependência de GAS; nomes com `LocalizedText` (DE5, DE6); `DescriptionLine` e `DescriptionWriter` (DE7a) e `EffectDescriber` (DE7b); elementos (DE8–DE10); motivo de falha (DE11); validação (DE12); prévia (DE13).

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

### Detalhe (2026-11-03)
- CA: CA9 (chave presente, chave ausente e provedor ausente pela tabela) e a parte de N1 (ler chave já resolvida sem alocar). Base do Design 1 (ADR 0038, item 2).
- Escopo (caminhos em `Packages/com.spellbook/`, namespace `SpellBook.Core.Text`):
  - `Runtime/Core/Text/TextTable.cs` (novo): `TextTable : ScriptableObject, IValidatable` com `[CreateAssetMenu("SpellBook/Text/Text Table")]`, `[HelpURL(DocUrls.Text + "#texttable")]`, `cultureName`, `List<Entry>` (`[Serializable] struct Entry`: `key`, `text`); `CultureName`, `Entries`; `Validate`: chave fora de `TextKey`, aviso; chave repetida, erro; com `propertyPath`.
  - `Runtime/Core/Text/TableTextProvider.cs` (novo): `TableTextProvider : TextProviderAsset, IValidatable` com `[CreateAssetMenu("SpellBook/Text/Table Text Provider")]`, `[HelpURL(DocUrls.Text + "#tabletextprovider")]`, `List<TextTable> tables`, `cultureName`; `Culture` (vazia ou desconhecida: invariante); `SetCulture(CultureInfo)` (nula: invariante; dispara `Changed`); `GetText` lê um `Dictionary<string,string>` (ordinal) montado na primeira leitura depois de cada troca: cultura, cultura pai até a invariante, tabelas sem cultura (a primeira chave vence); `OnValidate` invalida e dispara `Changed`; `Validate`: tabela nula, erro; mesma chave com textos diferentes em tabelas da mesma cultura, aviso.
  - `Editor/Core/Inspector/CoreEditors.cs:12` (+2 Inspectors de uma linha: `TextTableEditor`, `TableTextProviderEditor`).
  - `Runtime/Core/DocUrls.cs:24` (+`Text = Base + "modulos/texto/"`) e `Tests/Core/EditMode/DocUrlsTests.cs:35` (+`[TestCase("Text", "modulos/texto/")]`).
  - `Tests/Core/EditMode/Text/TableTextProviderTests.cs` (novo; tabelas montadas por `SerializedObject` numa fábrica do teste, sem API de escrita nova).
  - Sem `texto.md` e sem `CHANGELOG.md`: a página entra no passe de documentação e o ticket só acrescenta tipos (emenda de 2026-11-03 da 0007). O `EveryDocUrlHasPage` (categoria `Documentacao`) fica vermelho para `Text` até o passe, como `Cues` e `Thinkers`.
- Testes red (EditMode, `SpellBook.Core.Tests.EditMode`): `TableTextProviderTests`: `KeyInTable_ReturnsText`, `MissingKey_ReturnsFallback`, `NoProvider_FieldText`, `CultureChain_PtBr_Pt_Neutral_Fallback`, `SetCulture_RaisesChanged_CultureExposed`, `EmptyCulture_Invariant`, `ResolvedKey_10000Reads_DoesNotAllocate`, `Table_DuplicateKey_Error_BadKey_Warning`, `Provider_SameKeyDifferentText_Warning`, `Provider_NullTable_Error`; `DocUrlsTests.DocUrls_PageConstantPointsToPageUnderBase("Text", "modulos/texto/")`.
- Critério de pronto: os testes acima verdes pelo filtro `\.TableTextProviderTests\.;\.DocUrlsTests\.;\.HelpUrlTests\.;\.SpellBookEditorCoverageTests\.`.
- Tamanho: ~465 linhas e 6 arquivos (mais esta página do Spec), pela ADR 0042. Exceção: nenhuma.
- Commit: `feat(core): TextTable e TableTextProvider (#223)`

### DE3 — Adaptador do Unity Localization: código e testes
- Escopo: `Packages/manifest.json` do projeto host (+`com.unity.localization`, só se a Q2 for aprovada) e o `packages-lock.json` que a Unity regrava, `Runtime/Localization/SpellBook.Localization.asmdef`, `Runtime/Localization/LocalizationTextProvider.cs`, `Tests/Localization/EditMode/SpellBook.Localization.Tests.EditMode.asmdef` (mesma constraint), `Tests/Localization/EditMode/LocalizationTextProviderTests.cs`, `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs` (`Localization` em `RuntimeModules`; exceção do módulo opcional, ADR 0038 item 7), `CHANGELOG.md`.
- Testes red: `EntryInStringTable_ReturnsLocalized`; `MissingEntry_ReturnsFallback`; `LocaleChange_RaisesChanged_CultureFollowsLocale`; `ResolvedKey_DoesNotAllocate`; `OptionalModule_OnlyConstraintIsOwnVersionDefine`.
- Critério de pronto: testes verdes com o pacote no host; a API usada foi conferida na versão instalada (registrada no PR).
- Tamanho estimado: ~330 linhas; 8 arquivos. Depende de: DE2, ADR 0038 aceita, Q2.

### Detalhe (2026-11-03)
- CA: nenhum CA numerado fechado aqui; o ticket entrega o adaptador do Design 2 (R3) compilado e testado no host (Q2), com a exceção do módulo opcional na regra de asmdefs (ADR 0038, itens 6 a 8). O CA10 fecha no DE4.
- API conferida no `com.unity.localization` 1.5.13 instalado (dependências resolvidas: Addressables 2.9.1, Newtonsoft 3.2.2, SBP 2.6.1, Profiling Core 1.0.3): `LocalizationSettings.StringDatabase.GetTable(TableReference)` (síncrono, por `WaitForCompletion`), `StringTable.GetEntry(key)` e `StringTableEntry.GetLocalizedString()`, `LocalizationSettings.SelectedLocale`, evento estático `LocalizationSettings.SelectedLocaleChanged`, `Locale.Identifier.CultureInfo`. Nos testes: setter `LocalizationSettings.Instance`, `SetAvailableLocales(ILocalesProvider)`, `SetStringDatabase` com `LocalizedStringDatabase.TableProvider` (`ITableProvider`), como o exemplo `TableProviderSamples.cs` do pacote.
- Escopo:
  - `Packages/manifest.json:3` (+`com.unity.localization` 1.5.13) e `Packages/packages-lock.json` (regravado pela Unity). O `package.json` do SpellBook fica como está.
  - `Packages/com.spellbook/Runtime/Localization/SpellBook.Localization.asmdef` (novo): `SpellBook.Core` (GUID) e `Unity.Localization` (por nome, legível sem o pacote); `versionDefines` `com.unity.localization` → `SPELLBOOK_UNITY_LOCALIZATION`, a mesma como única `defineConstraints`; `autoReferenced` true.
  - `Packages/com.spellbook/Runtime/Localization/LocalizationTextProvider.cs` (novo): `TextProviderAsset` com `[HelpURL(DocUrls.Text + "#localizationtextprovider")]`, `[CreateAssetMenu("SpellBook/Text/Localization Text Provider")]` e `TableReference table`; `Culture` = cultura do `Locale` selecionado (sem locale ou sem cultura: invariante); `GetText` com cache `Dictionary<string,string>` ordinal, que guarda também a chave ausente; tabela vazia, entrada ausente ou texto vazio devolvem o fallback; assina `SelectedLocaleChanged` no `OnEnable` (sai no `OnDisable`), e a troca limpa o cache, atualiza a cultura e dispara `Changed`; `OnValidate` limpa e dispara `Changed`.
  - `Packages/com.spellbook/Tests/Localization/EditMode/SpellBook.Localization.Tests.EditMode.asmdef` (novo; `UNITY_INCLUDE_TESTS` e a mesma constraint e `versionDefines`; referencia também `Unity.Localization`, `Unity.Addressables` e `Unity.ResourceManager` para montar as tabelas de teste).
  - `Packages/com.spellbook/Tests/Localization/EditMode/LocalizationTextProviderTests.cs` (novo; `LocalizationSettings` de teste com locales e tabelas em memória, restaurado no `TearDown`).
  - `Packages/com.spellbook/Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:18-21` (`Localization` em `RuntimeModules`; lista `OptionalModules`), `:70` (exceção do módulo opcional na regra "runtime sem `defineConstraints`") e dois testes novos depois de `:77`.
  - Sem `CHANGELOG.md`, `texto.md` e `DocUrls`: o ticket só acrescenta tipos (emenda de 2026-11-03 da 0007); a âncora `#localizationtextprovider` entra com a seção do adaptador no passe. Inspector, `EditorModules`, `SpellBookEditorCoverageTests` e as guardas de isolamento ficam no DE4.
- Testes red (EditMode): `LocalizationTextProviderTests` (`SpellBook.Localization.Tests.EditMode`): `EntryInStringTable_ReturnsLocalized`, `MissingEntry_ReturnsFallback`, `LocaleChange_RaisesChanged_CultureFollowsLocale`, `ResolvedKey_DoesNotAllocate`; `AsmdefRulesTests` (`SpellBook.Core.Tests.EditMode`): `OptionalModule_OnlyConstraintIsOwnVersionDefine`, `LocalizationAsmdef_ReferencesCoreAndUnityLocalization`, e `AllExpectedAssembliesExist` e `EachRuntimeModuleHasEditModeTests` com `Localization` na lista.
- Critério de pronto: os testes acima verdes pelo filtro `\.AsmdefRulesTests\.;\.LocalizationTextProviderTests\.;\.HelpUrlTests\.;\.RuntimeReferencesTests\.`, com o pacote no host; nenhum asset gerado em `Assets/` nem em `ProjectSettings/`.
- Tamanho: ~483 linhas e 7 arquivos (mais esta página do Spec), pela ADR 0042; o `packages-lock.json` conta 77. Exceção: nenhuma.
- Commit: `feat(localization): adaptador LocalizationTextProvider (#224)`

### DE4 — Adaptador: Inspector, guardas de isolamento e página
- Escopo: `Editor/Localization/SpellBook.Localization.Editor.asmdef`, `Editor/Localization/LocalizationEditors.cs` (novos), `AsmdefRulesTests.cs` (`Localization` em `EditorModules`; ninguém fora do adaptador referencia `Unity.Localization` nem os asmdefs dele), `Tests/Core/EditMode/Structure/PackageJsonTests.cs` (novo ou caso novo no teste existente de `package.json`: não declara `com.unity.localization`), `SpellBookEditorCoverageTests.cs` (+`SpellBook.Localization`), `texto.md` (seção do adaptador), `CHANGELOG.md`.
- Testes red: `NoAsmdefOutsideAdapter_ReferencesUnityLocalization` (CA10); `PackageJson_DoesNotDeclareLocalization` (CA10); `LocalizationEditor_ConstraintMatchesRuntime`.
- Critério de pronto: CA10 (E) verde.
- Tamanho estimado: ~220 linhas; 7 arquivos. Depende de: DE3.

### Detalhe (2026-11-03)
- CA: CA10 (E): o adaptador compila só com o pacote (os dois asmdefs com a constraint do próprio `versionDefines`), o `package.json` não o declara e nenhum asmdef fora do adaptador o referencia (ADR 0038, itens 6 e 7).
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Editor/Localization/SpellBook.Localization.Editor.asmdef` (novo): referências `SpellBook.Core`, `SpellBook.Localization`, `SpellBook.Core.Editor` (por GUID), `includePlatforms` `Editor`, `autoReferenced` true, mesmo `versionDefines` e mesma `defineConstraints` do runtime.
  - `Editor/Localization/LocalizationEditors.cs` (novo): `LocalizationTextProviderEditor : SpellBookEditor` de uma linha.
  - `Tests/Core/EditMode/Structure/AsmdefRulesTests.cs:24` (`SpellBook.Localization.Editor` em `OptionalModules`), `:26` (`Localization` em `EditorModules`) e três testes novos depois de `:113`. O adaptador, para a guarda de isolamento, é o runtime, o Editor e o asmdef de testes dele (`SpellBook.Localization.Tests.EditMode`, que precisa de `Unity.Localization` para montar as tabelas; pendência do DE3).
  - `Tests/Core/EditMode/Inspector/SpellBookEditorCoverageTests.cs:21` (+`SpellBook.Localization`; a varredura é por nome, sem referência ao asmdef).
  - Sem `PackageJsonTests.cs` novo: o caso entra no `AsmdefRulesTests`, junto da guarda. Sem `texto.md` e sem `CHANGELOG.md`: a página entra no passe de documentação e o ticket só acrescenta tipos (emenda de 2026-11-03 da 0007).
- Testes red (EditMode, `SpellBook.Core.Tests.EditMode`): `AsmdefRulesTests`: `NoAsmdefOutsideAdapter_ReferencesUnityLocalization` (também cobra que a varredura acha os três asmdefs do adaptador), `PackageJson_DoesNotDeclareLocalization`, `LocalizationEditor_ConstraintMatchesRuntime`, e `OptionalModule_OnlyConstraintIsOwnVersionDefine` e `AllExpectedAssembliesExist` com o Editor na lista; `SpellBookEditorCoverageTests.EveryPublicConcreteMbAndSo_InRuntimeAssemblies_UsesSpellBookEditor` com `SpellBook.Localization`.
- Critério de pronto: os testes acima verdes pelo filtro `\.AsmdefRulesTests\.;\.SpellBookEditorCoverageTests\.;\.LocalizationTextProviderTests\.`; suíte EditMode `!Documentacao` verde.
- Tamanho: ~110 linhas e 4 arquivos (mais esta página do Spec), pela ADR 0042. Exceção: nenhuma.
- Commit: `feat(localization): Inspector e guardas de isolamento do adaptador (#225)`

### DE5 — Nomes exibidos com `LocalizedText` (quebra com migração)
- Escopo: `Runtime/GAS/Abilities/GameplayAbility.cs`, `Runtime/GAS/Effects/GameplayEffect.cs`, `Runtime/GAS/Attributes/AttributeDefinition.cs`, `Runtime/GAS/Attributes/AttributeCategory.cs` (campo novo, legado, conversão), `Editor/GAS/GasAssetMigration.cs`, `Tests/GAS/EditMode/DisplayNameMigrationTests.cs` (novo), testes que escrevem `DisplayName` (lista no PR), `gas.md`, `CHANGELOG.md` (quebra).
- Testes red: `LegacyYaml_AbilityDisplayName_LoadsAsText` e o mesmo para atributo, efeito e categoria (YAML 2.x temporário em `Assets/`, como o VN2); `NewAsset_SavesWithoutLegacy`; `Migrate_RewritesAbilitiesAttributesCategories_Idempotent`; `DisplayName_Resolve_KeyThroughProvider`.
- Critério de pronto: testes verdes; suíte do GAS verde com os testes editados listados no PR.
- Tamanho estimado: ~420 linhas; até 10 arquivos. Depende de: DE1, AR5, AR7, VN3 (fila: depois do EV21, do CU9 e do TK8).

### Detalhe (2026-11-03)
- CA: nenhum CA numerado fecha aqui; o ticket entrega o Design 3 (R4, N4): os quatro nomes exibidos em `LocalizedText`, asset antigo abre com o texto que tinha (Design 8) e o menu do VN3 regrava os quatro tipos. Base da linha `Attribute`/`Modifier` do DE7 (rótulo `Attribute.DisplayName`).
- Nome serializado: o campo novo é `localizedName` nos quatro donos, porque o `FormerlySerializedAs` do legado precisa de um nome que o campo novo não use (o efeito já gravava `displayName`, os outros três `DisplayName`). `DisplayName` passa a propriedade `LocalizedText` (com `set` na habilidade, no atributo e na categoria; só leitura no efeito, como hoje), então `x.DisplayName = new LocalizedText(null, "x")` compila, como a seção de quebras prevê.
- Escopo (caminhos em `Packages/com.spellbook/`):
  - `Runtime/GAS/Abilities/GameplayAbility.cs:35-40` (`ISerializationCallbackReceiver`; `localizedName`, `legacyDisplayName` com `FormerlySerializedAs("DisplayName")`, propriedade `DisplayName`; conversão no `OnAfterDeserialize`).
  - `Runtime/GAS/Effects/GameplayEffect.cs:48-57` (o mesmo, com `FormerlySerializedAs("displayName")`) e `:411-416` (a conversão junto da do `legacyDuration`).
  - `Runtime/GAS/Attributes/AttributeDefinition.cs:33-37` e `Runtime/GAS/Attributes/AttributeCategory.cs:9-11` (o mesmo que a habilidade).
  - `Runtime/GAS/Attributes/AttributeState.cs:81` (consequência direta: o aviso interpola `DisplayName.Text`, senão sairia o nome do tipo).
  - `Editor/GAS/GasAssetMigration.cs:10-39` (+`AttributeDefinition` e `AttributeCategory` no menu e no `MigrateAll`; mensagem e resumo).
  - `Tests/GAS/EditMode/DisplayNameMigrationTests.cs` (novo; YAML 2.x temporário em `Assets/__DisplayNameMigrationTests`, como o VN2).
  - Testes que escrevem ou leem `DisplayName`: `Tests/GAS/EditMode/EffectMetadataTests.cs:69,71,84,95,169` (`localizedName.text` e `.Text`) e `Tests/GAS/EditMode/AttributeDisplayTests.cs:153` (`new LocalizedText(null, "Ofensivo")`).
  - `CHANGELOG.md` (quebra com a migração). Sem `gas.md`: a página entra no passe de documentação (emenda de 2026-11-03 da 0007).
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`): `DisplayNameMigrationTests`: `LegacyYaml_AbilityDisplayName_LoadsAsText`, `LegacyYaml_AttributeDisplayName_LoadsAsText`, `LegacyYaml_EffectDisplayName_LoadsAsText`, `LegacyYaml_CategoryDisplayName_LoadsAsText`, `NewAsset_SavesWithoutLegacy`, `Migrate_RewritesAbilitiesAttributesCategories_Idempotent`, `DisplayName_Resolve_KeyThroughProvider`.
- Critério de pronto: os testes acima, `EffectMetadataTests`, `AttributeDisplayTests`, `GasAssetMigrationTests` e `ScalableMigrationTests` verdes pelo filtro `\.DisplayNameMigrationTests\.;\.EffectMetadataTests\.;\.AttributeDisplayTests\.;\.GasAssetMigrationTests\.;\.ScalableMigrationTests\.`; suíte EditMode `!Documentacao` verde.
- Tamanho: ~352 linhas e 10 arquivos (mais esta página do Spec), pela ADR 0042. Exceção: nenhuma.
- Commit: `feat(gas)!: DisplayName como LocalizedText, com migração (#226)`

### DE6 — Sample no formato novo
- Escopo: `Samples~/Sandbox/Data/Abilities/GA_BolaDeFogo.asset`, `Data/Attributes/ATR_Vida.asset`, `ATR_Mana.asset` (regravados pelo menu, com a pasta renomeada pela ADR 0028), `Documentation~/manual/regras/migracao-2-para-3.md` (nomes exibidos), `CHANGELOG.md`.
- Testes red: nenhum novo; as guardas do SB-T1 seguem verdes.
- Critério de pronto: os três assets abrem sem "Missing" e sem legado no YAML (o Examinador confere).
- Tamanho estimado: 0 linhas de código; 5 arquivos (3 fixtures). Depende de: DE5, EV21.

### Detalhe (2026-11-04)
- CA: nenhum CA numerado fecha aqui; o ticket entrega a parte do Sample do Design 3 e do Design 8 (assets do Sandbox no formato do DE5, sem `string` legado no YAML).
- Escopo (caminhos em `Packages/com.spellbook/Samples~/Sandbox/Data/`, regravados por `GasAssetMigration.MigrateAll` com a pasta renomeada pela ADR 0028, sem YAML à mão):
  - `Abilities/GA_BolaDeFogo.asset:16`, `Attributes/ATR_Vida.asset:16`, `Attributes/ATR_Mana.asset:16` (`DisplayName:` legado vira `localizedName`).
  - `Effects/GE_BolaDeFogo_Custo.asset:15`, `GE_BolaDeFogo_Dano.asset:15`, `GE_BolaDeFogo_Recarga.asset:15`, `GE_Queimadura.asset:15` (consequência direta, ADR 0042 item 6: o DE5 também converteu o `displayName` do efeito, e o menu regrava os quatro tipos; sem eles o Sample ficaria com legado).
  - Fora: `manual/regras/migracao-2-para-3.md` (só no passe de documentação, emenda de 2026-11-03 da 0007) e `CHANGELOG.md` (o ticket não quebra nada; a entrada da quebra com a migração já entrou no DE5).
- Testes red: nenhum novo. O red é o critério verificável: `grep -rnE "^  (DisplayName|displayName): " Samples~/Sandbox/Data` acha 7 linhas antes da regravação.
- Critério de pronto: a busca acima não acha nada; os 7 assets têm `localizedName` com `key` e `text` vazios e `legacyDisplayName` vazio (nulo, como o `NewAsset_SavesWithoutLegacy` do DE5); a regravação também escreve nos dois atributos os campos 3.0 que o YAML ainda não tinha (valores padrão); com a pasta renomeada, a Unity importa o Sample sem erro de console; `Samples/` e `Samples.meta` não sobram (ADR 0028); `SampleStructureTests` e suíte EditMode `!Documentacao` verdes com `Samples~`.
- Tamanho: ~0 linhas e 7 arquivos (fixtures), pela ADR 0042. Exceção: nenhuma.
- Commit: `chore(sample): Sample com nomes exibidos no formato novo (#227)`

### DE7a — `DescriptionLine`, `DescriptionContext`, formatador e `DescriptionWriter` (#228)
- Escopo: `Runtime/GAS/Description/DescriptionLine.cs`, `DescriptionContext.cs`, `ModifierTextFormatter.cs` (com o padrão), `DescriptionWriter.cs` (novos), `Runtime/GAS/Attributes/AttributeDisplay.cs` (`Format` em `TextBuffer`), `Tests/GAS/EditMode/EffectDescriberTests.cs` (novo, com linhas montadas à mão).
- Testes red: `Seconds1_5_PtBr_EnUs` (CA3, C3); `CustomFormatter_ChangesOnlyValueText` (CA4, C4); `WriterOutput_HasNoMarkup` (CA8, parte); `AttributeDisplay_FormatToBuffer_SameAsString`.
- Critério de pronto: CA3 e CA4 verdes; parte do CA8.
- Tamanho estimado: ~470 linhas; 6 arquivos. Depende de: DE1, AR5. Nasceu da divisão do DE7 (ADR 0042, acima de +30%).

### Detalhe (2026-11-04)
- CA: CA3, CA4; parte do CA8 (saída do `DescriptionWriter` sem marcação). As linhas dos testes são montadas à mão; o `EffectDescriber`, o CA1, o CA2 e a parte do CA12 ficam no DE7b. Dependências conferidas no código: AR5 (`AttributeDisplay.Format`, `GetDeltaRole`, `Hidden`).
- Escopo (caminhos em `Packages/com.spellbook/`, namespace `SpellBook.GAS.Description`):
  - `Runtime/GAS/Description/DescriptionLine.cs` (novo): `DescriptionLineKind`, `DescriptionLineFlags`, `DescriptionLine` com construtor público e `DescriptionLine.ForModifier(...)` (único caminho com `HasOperation`). O `Source` (`DescriptionElement`) entra no DE8, com o tipo novo; aqui ele ainda está em `SpellBook.GAS.Abilities` com o contrato antigo.
  - `Runtime/GAS/Description/DescriptionContext.cs` (novo): como o Design 4; `Reset` limpa `Lines`, `Effect` e `Stacks` e mantém `Text` e `Formatter`; `EvaluationContext` usa o construtor `internal` do `EffectContext` (mesmo assembly), então o `EffectContext.cs` não muda.
  - `Runtime/GAS/Description/ModifierTextFormatter.cs` (novo): `ModifierTextFormatter` e `DefaultModifierTextFormatter`.
  - `Runtime/GAS/Description/DescriptionWriter.cs` (novo): `WriteValue` para as linhas com atributo (`Modifier`, `Attribute`, `Cost`) pelo formatador; `WriteLine` = rótulo resolvido, espaço e valor. Os segundos de `Cooldown`/`Duration`/`Charges` e os rótulos com `{0}`/`{1}` ficam no DE9, junto dos elementos que geram essas linhas e do CA5 que os testa.
  - `Runtime/GAS/Attributes/AttributeDisplay.cs:51-123`: `Format(TextBuffer, valor, cultura)` e `Format(TextBuffer, valor, operação, cultura)`; o `Compose` passa a escrever no `TextBuffer`, e as duas sobrecargas de `string` usam um buffer `[ThreadStatic]` (uma só regra de formato; o texto de saída não muda, os `AttributeDisplayTests` guardam).
  - `Tests/GAS/EditMode/EffectDescriberTests.cs` (novo): os 4 testes com linhas montadas à mão; o DE7b acrescenta os do `EffectDescriber` no mesmo arquivo.
  - Sem `EffectContext.cs` (o construtor `internal` basta) e sem `CHANGELOG.md` (só acrescenta tipos; emenda de 2026-11-03 da 0007).
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`, `EffectDescriberTests`): `Seconds1_5_PtBr_EnUs`, `CustomFormatter_ChangesOnlyValueText`, `WriterOutput_HasNoMarkup` (linhas de modificador com cada operação, de atributo e de custo), `AttributeDisplay_FormatToBuffer_SameAsString`, `DefaultFormatter_HasTypeCategoryWithSummary` (metadados do tipo concreto pela emenda da 0013; achado da revisão).
- Critério de pronto: os 5 testes verdes e os `AttributeDisplayTests` verdes pelo filtro `\.EffectDescriberTests\.;\.AttributeDisplayTests\.`; suíte EditMode `!Documentacao` verde.
- Tamanho: medido 497 linhas e 6 arquivos, pela ADR 0042 (`AttributeDisplay.cs` 65, `DescriptionLine.cs` 122, `DescriptionContext.cs` 60, `ModifierTextFormatter.cs` 34, `DescriptionWriter.cs` 45, `EffectDescriberTests.cs` 171). Exceção: nenhuma.
- Commit: `feat(gas): DescriptionLine, DescriptionWriter e formatador (#228)`

### DE7b — `EffectDescriber` (#330)
- Escopo: `Runtime/GAS/Description/EffectDescriber.cs` (novo; como o Design 4: `PeriodicModifiers` só com `Period > 0`; entrada nula ou sem atributo, pulada; `SetByCaller` vale o `CallerDefault` ou 0 sem chamar o `GetValue`; sem instigador, atributo e fórmula valem 0; atributo do alvo vale 0; os três com `Variable`), `Tests/GAS/EditMode/EffectDescriberTests.cs` (acrescenta os testes do `EffectDescriber`).
- Testes red: `Armor20Percent_LineAttributeValueOpRoleBeneficial_ValueText` (CA1, C1); `Stacks3Level2_ValueComposed_VariableWhenLevelOrCaller` (CA2, C2); `HiddenAttribute_Skipped`; `Describe_AppendsWithoutClearing`; `Describe10000_ReusedContext_DoesNotAllocate` (CA12, parte); `AttributeSource_NoInstigator_ZeroVariable_WithInstigator_Value`; `PeriodicModifiers_OnlyWithPeriod`.
- Critério de pronto: CA1 e CA2 verdes; parte do CA12.
- Tamanho estimado: ~290 linhas; 2 arquivos. Depende de: DE7a, VN6, VN7.

### Detalhe (2026-11-04)
- CA: CA1, CA2; parte do CA12 (descrever um efeito com o contexto reaproveitado não aloca). Devolve ao `Seconds1_5_PtBr_EnUs` a checagem de que o `Override` sai com papel `Value`, perdida no DE7a por depender do `EffectDescriber`. Dependências conferidas no código: VN6 (`ModifierValueSource.GetValue(in EffectContext)`), VN7 (`GameplayEffect.StacksMultiplyMagnitude`), DE7a (`DescriptionLine.ForModifier`, `DescriptionContext.EvaluationContext`).
- Escopo (caminhos em `Packages/com.spellbook/`, namespace `SpellBook.GAS.Description`):
  - `Runtime/GAS/Description/EffectDescriber.cs` (novo): `static Describe(GameplayEffect, DescriptionContext)`; efeito nulo, nada; contexto nulo, `ArgumentNullException`. Uma linha `Modifier` por item de `Modifiers` e, com `Period > 0`, de `PeriodicModifiers`; entrada nula, sem atributo ou com `Display.Hidden`, pulada. Valor: `GetValue(in context.EvaluationContext)` vezes `Stacks` quando `StacksMultiplyMagnitude`; `SetByCaller` vale `CallerDefault` (ou 0) sem chamar o `GetValue`, para não avisar; atributo e fórmula valem 0 sem instigador; atributo do alvo vale 0 (o contexto não tem alvo). `Variable` para `ScalableFloat` fora de `Constant`, `SetByCaller`, atributo, fórmula e atributo do alvo. Papel: `Override` dá `Value`; as outras operações, `Display.GetDeltaRole(valor)`. Rótulo: `Attribute.DisplayName`.
  - `Tests/GAS/EditMode/EffectDescriberTests.cs:1-20` (usings e resumo), `:39-47` (`Seconds1_5_PtBr_EnUs` monta a linha pelo `EffectDescriber`), os 7 testes novos depois de `:113` e as fábricas `Effect`, `Asc` e `Modifier` depois de `Attribute`.
  - Sem `CHANGELOG.md` (só acrescenta um tipo; emenda de 2026-11-03 da 0007).
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`, `EffectDescriberTests`): `Armor20Percent_LineAttributeValueOpRoleBeneficial_ValueText`, `Stacks3Level2_ValueComposed_VariableWhenLevelOrCaller`, `HiddenAttribute_Skipped`, `Describe_AppendsWithoutClearing`, `Describe10000_ReusedContext_DoesNotAllocate`, `AttributeSource_NoInstigator_ZeroVariable_WithInstigator_Value`, `PeriodicModifiers_OnlyWithPeriod`, e `Seconds1_5_PtBr_EnUs` com o papel do `Override`.
- Critério de pronto: os 12 testes de `EffectDescriberTests` verdes pelo filtro `\.EffectDescriberTests\.`; suíte EditMode `!Documentacao` verde.
- Tamanho: medido 277 linhas e 2 arquivos (mais esta página do Spec), pela ADR 0042 (`EffectDescriber.cs` 87, `EffectDescriberTests.cs` 190). Exceção: nenhuma.
- Commit: `feat(gas): EffectDescriber (#330)`

### DE8 — Contrato novo do `DescriptionElement` e elementos de texto, atributo e efeito
- Escopo: `Runtime/GAS/Abilities/AbilityDescription.cs` (apagado, com substituto), `Runtime/GAS/Description/DescriptionElement.cs` (base e `AbilityDescription`), `Runtime/GAS/Description/DescriptionElements.cs` (`TextElement`, `AttributeElement`, `EffectModifiersElement`, `[MovedFrom]`), `GameplayAbility.cs` (`Describe`; sai `GetDescription`), `Runtime/Core/DocUrls.cs` (`Description = Base + "modulos/descricao/"`), `DocUrlsTests.cs`, `Documentation~/manual/modulos/descricao.md` (nova), `Tests/GAS/EditMode/DescriptionElementsTests.cs` (novo), `Tests/GAS/EditMode/GasTextScanTests.cs` (novo; guarda textual do CA15), `CHANGELOG.md` (quebra).
- Testes red: `TextElement_KeyAndFallback`; `AttributeElement_SourceValue_NoSourceVariable`; `EffectModifiersElement_SameLinesAsDescriber`; `CustomElement_NewContract_AddsLines` (CA14, parte); `AbilityDescription_InOrder_NullSkipped`; `OldGetTextGone` (guarda textual); `GasRuntime_HasNoRichTextNorFixedCulture` (CA15: nenhum `.cs` de `Runtime/GAS/**` com `<color`, `<b>`, `<i>`, `<size` ou `new CultureInfo(`; `InvariantCulture` só no fallback do `AttributeDisplay` (Q3 do B-005) e no `ScalableFloat.TryParse` do Editor; red forçado).
- Critério de pronto: CA15 e parte do CA14 verdes; CA17 (suíte sem edição, nenhum teste trocado).
- Tamanho estimado: ~480 linhas; 10 arquivos. Depende de: DE7b, DE5.

### Detalhe (2026-11-04)
- CA: CA15 (busca de código no runtime do GAS), parte do CA14 (elemento próprio no contrato novo; o validador fica no DE12) e CA17 (nenhum teste existente editado). Dependências conferidas no código: DE7b (`EffectDescriber`), DE5 (`DisplayName` como `LocalizedText`), EA1 (`TryGetAttributeValue`).
- Escopo (caminhos em `Packages/com.spellbook/`, namespace `SpellBook.GAS.Description`):
  - `Runtime/GAS/Abilities/AbilityDescription.cs:1-107` (apagado; substituído pelos dois arquivos abaixo). O `TemplateElement` sai junto e volta no DE10, que o reescreve no contrato novo (o Design 5 o põe lá); nenhum asset nem teste o usa (o Sample tem `Elements: []`).
  - `Runtime/GAS/Description/DescriptionElement.cs` (novo): `DescriptionElement` (`[Serializable] abstract`, `public abstract void Describe(DescriptionContext)`) e `AbilityDescription` (`[SerializeReference, TypePicker] List<DescriptionElement> Elements`; `Describe` na ordem, pulando nulos; contexto nulo, `ArgumentNullException`). O `Validate` do Design 5 fica no DE12, que o usa.
  - `Runtime/GAS/Description/DescriptionElements.cs` (novo): `TextElement` (`LocalizedText`, linha `Text` com papel `None` e `Source = this`), `AttributeElement` (linha `Attribute`, rótulo `DisplayName`, papel `Value`, valor por `TryGetAttributeValue`; sem ASC ou sem o atributo, 0 com `Variable`; sem atributo, nada) e `EffectModifiersElement` (`EffectDescriber.Describe` com `context.Effect` trocado durante a chamada e restaurado depois), `[Serializable] sealed`, `[TypeCategory("Descrição/...", Summary, Line)]`, campos `[SerializeField] private` com propriedade. `[MovedFrom(false, sourceNamespace: "SpellBook.GAS.Abilities")]` no `TextElement` e no `AttributeElement`; `[FormerlySerializedAs("Attribute")]` no campo do atributo.
  - `Runtime/GAS/Description/DescriptionLine.cs:45-80` (`Source`, parâmetro opcional `source` no construtor público; `ForModifier` sai sem fonte).
  - `Runtime/GAS/Abilities/GameplayAbility.cs:57` (`Description` passa ao tipo novo pelo `using`) e `:608-612` (`GetDescription` vira `Describe(DescriptionContext)`; contexto nulo, `ArgumentNullException`; sem descrição, nada).
  - `Runtime/Core/DocUrls.cs:24` (+`Description = Base + "modulos/descricao/"`) e `Tests/Core/EditMode/DocUrlsTests.cs:33` (+`[TestCase("Description", "modulos/descricao/")]`). O `EveryDocUrlHasPage` (categoria `Documentacao`) fica vermelho para `Description` até o passe, como `Text` (DE2).
  - `Tests/GAS/EditMode/DescriptionElementsTests.cs` (novo).
  - `Tests/GAS/EditMode/GasSourceScanTests.cs` (dois testes novos depois de `GasHasNoVfxOrCueActions`, reaproveitando `Scan` e `PackageRoot`; no lugar do `GasTextScanTests.cs` novo do escopo, que duplicaria os dois auxiliares).
  - `CHANGELOG.md` (quebra com a migração). Sem `descricao.md`: a página entra no passe de documentação (emenda de 2026-11-03 da 0007).
- Exceções da guarda do CA15, com o motivo no teste: `<color` em linha de `Debug.Log` (o `DebugLogAction` colore a saída do Console, que não é texto exibido ao jogador; mudar o campo `LogColor` quebraria API e formato que o design doc não prevê); `InvariantCulture` só em `AttributeDisplay.cs` e `DescriptionContext.cs` (fallback sem cultura, Q3 do B-005 e Design 4), `ScalableFloat.cs` (`TryParse` do texto colado) e `Formulas/ExpressionFormula.cs` e `Formulas/Utils/ExpressionParser.cs` (leitura de fórmula, não texto exibido). O Design 5 citava só os dois primeiros casos de `InvariantCulture`; os outros já estavam no código e não exibem texto.
- Testes red (EditMode, `SpellBook.GAS.Tests.EditMode`): `DescriptionElementsTests`: `TextElement_KeyAndFallback`, `AttributeElement_SourceValue_NoSourceVariable`, `EffectModifiersElement_SameLinesAsDescriber`, `CustomElement_NewContract_AddsLines`, `AbilityDescription_InOrder_NullSkipped`, `Ability_Describe_NullContextThrows_NoDescriptionNothing`, `Elements_HaveTypeCategoryWithSummary`; `GasSourceScanTests`: `OldGetTextGone`, `GasRuntime_HasNoRichTextNorFixedCulture`; `DocUrlsTests.DocUrls_PageConstantPointsToPageUnderBase("Description", "modulos/descricao/")`.
- Critério de pronto: os testes acima verdes pelo filtro `\.DescriptionElementsTests\.;\.GasSourceScanTests\.;\.DocUrlsTests\.;\.EffectDescriberTests\.;\.TypeCatalogSummaryTests\.`; suítes EditMode e PlayMode `!Documentacao` verdes sem editar teste existente (CA17).
- Tamanho: medido 563 linhas e 10 arquivos (mais esta página do Spec), pela ADR 0042 (`AbilityDescription.cs` 107 removidas, `DescriptionElement.cs` 40, `DescriptionElements.cs` 94, `DescriptionLine.cs` 14, `GameplayAbility.cs` 12, `DocUrls.cs` 1, `DocUrlsTests.cs` 1, `DescriptionElementsTests.cs` 233, `GasSourceScanTests.cs` 61, `CHANGELOG.md` só arquivo). Exceção: +13% nas linhas (até +30%, ADR 0042). Motivo: o arquivo apagado conta 107 linhas por ter substituto no mesmo ticket, e os testes dos elementos precisam de um elemento próprio e de um provedor de teste para cobrir o contrato novo (CA14) e a chave.
- Commit: `feat(gas)!: contrato novo do DescriptionElement e elementos (#229)`

### DE9 — Custo, recarga, duração e cargas
- Escopo: `Runtime/GAS/Description/AbilityStatElements.cs` (novo: `CostElement`, `CooldownElement`, `DurationElement`, `ChargesElement`), `DescriptionWriter.cs` (rótulos com `{0}`/`{1}`), `Tests/GAS/PlayMode/AbilityStatElementsTests.cs` (novo), `descricao.md`, `CHANGELOG.md`.
- Testes red: `CostCooldownDurationCharges_OneLineEach_AtLevel3` (CA5, C5); `NoCost_NoCooldown_NoCharges_NoLine`; `Infinite_PermanentKey_Instant_NoLine`; `Cooldown_WithAsc_AfterPolicy_WithoutAsc_Base` (CA6, C6); `Charges_MaxFromTable_RechargeWithReduction`; `Writer_ChargesLabel_SubstitutesValues_NoMarkup`.
- Critério de pronto: CA5 e CA6 verdes.
- Tamanho estimado: ~440 linhas; 5 arquivos. Depende de: DE8, AV7, EA6.

### Detalhe (2026-11-04)
- CA: CA5, CA6; parte do CA8 (linhas de recarga, duração e cargas sem marcação). Dependências conferidas no código: DE8 (`DescriptionElement`, `DescriptionContext`), AV7 (`ChargesConfig.MaxCharges`/`RechargeTime`), EA6/AV6 (`GameplayAbility.ApplyCooldownReduction`, `internal`, mesmo assembly).
- Escopo (caminhos em `Packages/com.spellbook/`, namespace `SpellBook.GAS.Description`):
  - `Runtime/GAS/Description/AbilityStatElements.cs` (novo): `CostElement`, `CooldownElement`, `ChargesElement` (leem `context.Ability`; sem habilidade, nada) e `DurationElement` (`GameplayEffect effect`), `[Serializable] sealed`, `[TypeCategory("Descrição/...", Summary, Line)]`, como o Design 5. Custo: uma linha `Cost` por item de `CostEffect.Modifiers` (nulo, sem atributo ou oculto, pulado), valor absoluto no nível pela mesma avaliação do `EffectDescriber`, papel `Value`, rótulo `DisplayName`. Recarga: `CooldownEffect.Duration.Evaluate(nível, Values)` e `ApplyCooldownReduction(base, Source)` (sem ASC, a base). Duração: `HasDuration` no nível; `Infinite` com `Permanent`, valor 0 e rótulo `gas.description.permanent`; `Instant` ou nulo, nada. Cargas: `Value` = `floor` do máximo no nível (mínimo 0), `SecondaryValue` = recarga com redução, rótulo `gas.description.charges`. `Variable` quando o valor depende do nível ou de outra fonte, como no `EffectDescriber`; na recarga e na recarga das cargas, também quando a habilidade tem `CooldownReductionAttribute` (o atributo de quem lança muda o valor, com ou sem ASC). Rótulos com chave e texto padrão em pt-BR (`gas.description.cooldown` "Recarga", `gas.description.duration` "Duração", `gas.description.permanent` "Permanente", `gas.description.charges` "{0} cargas, uma a cada {1}"); as constantes passam ao `GasTextKeys` no DE11.
  - `Runtime/GAS/Description/DescriptionWriter.cs:12-47`: `WriteValue` também escreve `Cooldown` e `Duration` em segundos (`ValueKind.Seconds`; 0 casas quando o valor arredondado a uma casa é inteiro, senão 1; `Permanent`, nada) e `Charges` como inteiro; `WriteLine` troca `{0}` pelo texto do valor e `{1}` pelo do segundo valor (segundos, nas cargas) quando o rótulo os tem, e então não acrescenta o valor no fim. Sem `string.Format`, sem alocar.
  - `Runtime/GAS/Description/EffectDescriber.cs:59` (`Evaluate` passa de `private` a `internal`, para o custo e as cargas usarem a mesma avaliação; consequência direta, ADR 0042 item 6).
  - `Runtime/Core/Text/TextBuffer.cs` (+`Append(string text, int start, int length)`, para copiar os trechos do rótulo entre os `{n}` sem alocar) e `Tests/Core/EditMode/Text/TextBufferTests.cs` (+`Append_Substring_OnlyThatRange`).
  - `Tests/GAS/EditMode/AbilityStatElementsTests.cs` (novo). EditMode e não PlayMode como no escopo: o ASC de teste já roda em EditMode pelo `GasFactory` (como nos `DescriptionElementsTests`), e nenhum teste precisa de frame.
  - Sem `descricao.md` e sem `CHANGELOG.md`: o ticket só acrescenta tipos, e a página entra no passe de documentação (emenda de 2026-11-03 da 0007).
- Testes red (EditMode): `SpellBook.GAS.Tests.EditMode`, `AbilityStatElementsTests`: `CostCooldownDurationCharges_OneLineEach_AtLevel3`, `NoCost_NoCooldown_NoCharges_NoLine`, `Infinite_PermanentKey_Instant_NoLine`, `Cooldown_WithAsc_AfterPolicy_WithoutAsc_Base`, `ConstantRecharge_VariableOnlyWithReductionAttribute`, `Charges_MaxFromTable_RechargeWithReduction`, `Writer_ChargesLabel_SubstitutesValues_NoMarkup`, `Writer_Seconds_IntegerWithoutDecimals`, `Elements_HaveTypeCategoryWithSummary`; `SpellBook.Core.Tests.EditMode`, `TextBufferTests.Append_Substring_OnlyThatRange`.
- Critério de pronto: os testes acima verdes pelo filtro `\.AbilityStatElementsTests\.;\.TextBufferTests\.;\.EffectDescriberTests\.;\.DescriptionElementsTests\.;\.TypeCatalogSummaryTests\.`; suíte EditMode `!Documentacao` verde na revisão.
- Tamanho: medido 619 linhas e 6 arquivos (mais esta página do Spec), pela ADR 0042 (`AbilityStatElements.cs` 150, `DescriptionWriter.cs` 109, `EffectDescriber.cs` 2, `TextBuffer.cs` 15, `TextBufferTests.cs` 14, `AbilityStatElementsTests.cs` 329), já com a correção da revisão (flag `Variable` pela redução de recarga). Exceção: +24% nas linhas (até +30%, ADR 0042). Motivo: os testes montam habilidade, efeitos, tabela, política de redução e ASC para cada um dos quatro elementos e precisam de um provedor de teste com cultura para cobrir o rótulo traduzido das cargas.
- Commit: `feat(gas): elementos de custo, recarga, duração e cargas (#230)`

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
| DE7a | sim | DE1, AR5 | ~470 / 6 |
| DE7b | sim | DE7a, VN6, VN7 | ~290 / 2 |
| DE8 | sim | DE7b, DE5 | ~480 / 10 |
| DE9 | sim | DE8, AV7, EA6 | ~440 / 5 |
| DE10 | sim | DE9, VN4 | ~420 / 6 |
| DE11 | sim | DE10, DE2, AV5 | ~320 / 8 |
| DE12 | sim | DE11, AV8 | ~380 / 7 |
| DE13 | sim | DE12, EV17 | ~360 / 5 |

Fila: DE1 → DE4 podem entrar cedo (só Core e o adaptador); DE5 → DE13 em série depois dos Lotes C e D. O B-015 precisa só do DE1 e do DE7b; o B-016 precisa do DE11; o B-017 precisa do DE13. CA15 roda em todos depois do DE8.

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
