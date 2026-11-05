using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SpellBook.Core.Editor;
using SpellBook.Core.Editor.Inspector;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Inspector da <see cref="GameplayAbility"/> (R29; DS do Inspector da habilidade, seção 3). Abaixo dos avisos e da
    /// validação, quatro abas com os campos do Inspector padrão em grupos: Configuração (Identidade, Comportamento,
    /// Custo e recarga e Condições de ativação, com uma linha por regra: resumo da condição e motivo da falha), Ações
    /// (as abas de ações por evento, com "A cada passo" só em <see cref="AbilityBehavior.Channel"/>), Tags e gatilhos, e
    /// Níveis e prévia (o "nível de prévia", limitado a [1, MaxLevel], uma tabela com uma linha por nível, com os valores
    /// nomeados, a recarga sem a redução do atributo, o custo e as cargas, a linha desse nível marcada, a prévia em texto
    /// nesse nível e a <see cref="DescriptionPreviewElement"/> no mesmo nível). Um
    /// campo que o editor não conhece vai para "Outros", no fim de Configuração. Em Comportamento, os campos da
    /// canalização, o <c>reactivatePassive</c> e o autocast só aparecem nos comportamentos que os usam.
    /// </summary>
    [CustomEditor(typeof(GameplayAbility), true)]
    [CanEditMultipleObjects]
    public sealed class GameplayAbilityEditor : SpellBookEditor
    {
        public const string PreviewLevelName = "spellbook-ability-preview-level";
        public const string PreviewName = "spellbook-ability-preview";
        public const string ActivationName = "spellbook-ability-activation";
        public const string RuleRowClass = "spellbook-ability-rule-row";
        public const string ChargesPreviewName = "spellbook-ability-charges-preview";
        public const string DescriptionSectionName = "spellbook-ability-description";

        private IntegerField _level;
        private Label _preview;
        private VisualElement _rules;
        private Label _charges;
        private VisualElement _table;
        private DescriptionPreviewElement _description;
        private PropertyField _channelDuration;
        private PropertyField _channelTickInterval;
        private PropertyField _reactivatePassive;
        private PropertyField _autocast;

        internal const string LevelTableName = "spellbook-ability-level-table";
        internal const string LevelHeaderClass = "spellbook-ability-level-header";
        internal const string LevelRowClass = "spellbook-ability-level-row";
        internal const string LevelRowSelectedClass = "spellbook-ability-level-row--selected";
        private const string LevelMarker = " ◂";
        private static readonly Color SelectionColor = new Color(0.24f, 0.37f, 0.59f, 0.45f);

        internal const string ConfigurationTab = "Configuração";
        internal const string ActionsTab = "Ações";
        internal const string TagsTab = "Tags e gatilhos";
        internal const string LevelsTab = "Níveis e prévia";

        protected override void BuildAfterDefault(VisualElement root)
        {
            var tabs = new InspectorTabsView(serializedObject.targetObject, ConfigurationTab, ActionsTab, TagsTab, LevelsTab);

            VisualElement configuration = tabs.Content(ConfigurationTab);
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(configuration, "Identidade"),
                nameof(GameplayAbility.Icon), "localizedName", nameof(GameplayAbility.AbilityTag), nameof(GameplayAbility.Description));
            VisualElement behavior = InspectorTabsView.AddGroup(configuration, "Comportamento");
            InspectorTabsView.MoveFields(root, behavior,
                "behavior", "commitMode", "channelDuration", "channelTickInterval", "reactivatePassive", "autocast");
            _channelDuration = behavior.Q<PropertyField>("PropertyField:channelDuration");
            _channelTickInterval = behavior.Q<PropertyField>("PropertyField:channelTickInterval");
            _reactivatePassive = behavior.Q<PropertyField>("PropertyField:reactivatePassive");
            _autocast = behavior.Q<PropertyField>("PropertyField:autocast");
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(configuration, "Custo e recarga"),
                nameof(GameplayAbility.CostEffect), nameof(GameplayAbility.CooldownEffect),
                nameof(GameplayAbility.CooldownReductionAttribute), "cooldownReductionPolicy", "charges");
            VisualElement activation = InspectorTabsView.AddGroup(configuration, "Condições de ativação");
            activation.name = ActivationName;
            InspectorTabsView.MoveFields(root, activation, "castRange", "activationConditions");
            _rules = new VisualElement();
            activation.Add(_rules);

            ActionEventsView.HideDefaultFields(root, typeof(GameplayAbility));
            tabs.Content(ActionsTab).Add(new ActionEventsView(serializedObject,
                "Esta habilidade ainda não faz nada. Adicione uma ação em \"Ao ativar\".",
                field => field != nameof(GameplayAbility.OnChannelTick) || IsChannel()));

            VisualElement tags = tabs.Content(TagsTab);
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(tags, "Tags"),
                nameof(GameplayAbility.BlockedTags), nameof(GameplayAbility.RequiredTags),
                nameof(GameplayAbility.ActivationOwnedTags), nameof(GameplayAbility.CancelAbilitiesWithTag),
                nameof(GameplayAbility.BlockAbilitiesWithTag), nameof(GameplayAbility.CancelledByTags));
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(tags, "Gatilhos"), "triggers");

            VisualElement levels = tabs.Content(LevelsTab);
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(levels, "Níveis"), "maxLevel", "values");
            VisualElement preview = InspectorTabsView.AddGroup(levels, "Prévia");
            _level = new IntegerField("Nível de prévia") { name = PreviewLevelName, value = 1 };
            _preview = new Label { name = PreviewName };
            _charges = new Label { name = ChargesPreviewName };
            _table = new VisualElement { name = LevelTableName };
            _table.style.marginTop = 2;
            _table.style.marginBottom = 4;
            preview.Add(_level);
            preview.Add(_table);
            preview.Add(_preview);
            preview.Add(_charges);
            var description = new VisualElement { name = DescriptionSectionName };
            description.Add(new Label("Descrição"));
            _description = new DescriptionPreviewElement(serializedObject);
            description.Add(_description);
            preview.Add(description);

            InspectorTabsView.MoveRemaining(root, configuration, "m_Script");
            root.Add(tabs);

            _level.RegisterValueChangedCallback(evt => SetPreviewLevel(evt.newValue));
            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                RefreshBehaviorFields();
                SetPreviewLevel(_level.value);
            });
            RefreshBehaviorFields();
            SetPreviewLevel(1);
        }

        /// <summary>
        /// Mostra só os campos que valem no comportamento (CA3): canalização em <see cref="AbilityBehavior.Channel"/>,
        /// <c>reactivatePassive</c> em <see cref="AbilityBehavior.Passive"/> e autocast em
        /// <see cref="AbilityBehavior.Instant"/> e <see cref="AbilityBehavior.Channel"/>. O campo escondido guarda o
        /// valor. Com vários assets, o campo aparece se algum deles o usa.
        /// </summary>
        internal void RefreshBehaviorFields()
        {
            bool channel = AnyBehavior(AbilityBehavior.Channel);
            Show(_channelDuration, channel);
            Show(_channelTickInterval, channel);
            Show(_reactivatePassive, AnyBehavior(AbilityBehavior.Passive));
            Show(_autocast, channel || AnyBehavior(AbilityBehavior.Instant));
        }

        private bool AnyBehavior(AbilityBehavior behavior)
        {
            foreach (Object selected in targets)
            {
                if (selected is GameplayAbility ability && ability.Behavior == behavior) return true;
            }

            return false;
        }

        private static void Show(VisualElement field, bool visible)
        {
            if (field != null) field.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private bool IsChannel()
        {
            return target is GameplayAbility ability && ability.Behavior == AbilityBehavior.Channel;
        }

        /// <summary>Muda o nível de prévia, limitado a [1, MaxLevel], e refaz a prévia.</summary>
        public void SetPreviewLevel(int level)
        {
            if (_level == null || !(target is GameplayAbility ability)) return;

            level = level < 1 ? 1 : level > ability.MaxLevel ? ability.MaxLevel : level;
            _level.SetValueWithoutNotify(level);
            _preview.text = Preview(ability, level);
            _description.SetLevel(level);
            RefreshTable(ability, level);
            RefreshRules(ability);
            string charges = ChargesPreview(ability, level);
            _charges.text = charges ?? string.Empty;
            _charges.style.display = charges != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Uma linha por regra: "n. resumo da condição → motivo: tag ou chave".</summary>
        private void RefreshRules(GameplayAbility ability)
        {
            _rules.Clear();
            using (var serialized = new SerializedObject(ability))
            {
                SerializedProperty rules = serialized.FindProperty("activationConditions");
                for (int i = 0; i < rules.arraySize; i++)
                {
                    SerializedProperty rule = rules.GetArrayElementAtIndex(i);
                    string summary = SummaryFormatter.Format(rule.FindPropertyRelative("condition"));
                    string reason = Reason(i < ability.ActivationConditions.Count ? ability.ActivationConditions[i] : null);
                    var row = new Label($"{i + 1}. {summary} → motivo: {reason}");
                    row.AddToClassList(RuleRowClass);
                    _rules.Add(row);
                }
            }
        }

        private static string Reason(ActivationRule rule)
        {
            if (rule == null) return SummaryFormatter.None;
            if (rule.FailureTag.IsValid) return rule.FailureTag.Name;
            string key = rule.EffectiveFailureKey;
            return string.IsNullOrEmpty(key) ? SummaryFormatter.None : key;
        }

        /// <summary>
        /// "1 carga a cada X s, máximo Y no nível N", sem a redução do atributo; nula com as cargas desligadas. O máximo
        /// por atributo ou fórmula depende do ASC e aparece pelo nome da fonte.
        /// </summary>
        private static string ChargesPreview(GameplayAbility ability, int level)
        {
            ChargesConfig charges = ability.Charges;
            if (!charges.Enabled) return null;

            float seconds = Evaluate(charges.RechargeTime, level, ability.Values);
            return $"1 carga a cada {Format(seconds)} s, máximo {MaxCharges(ability, level)} no nível {level}";
        }

        /// <summary>Máximo de cargas no nível; de atributo ou fórmula, o nome da fonte.</summary>
        private static string MaxCharges(GameplayAbility ability, int level)
        {
            ModifierValueSource source = ability.Charges.MaxCharges;
            return source.Type == ModifierValueSource.SourceType.Value
                ? System.Math.Max(0, (int)System.Math.Floor(Evaluate(source.Value, level, ability.Values))).ToString(CultureInfo.CurrentCulture)
                : SourceName(source);
        }

        private static string SourceName(ModifierValueSource source)
        {
            return ObjectNames.NicifyVariableName(source.Type.ToString()).ToLowerInvariant();
        }

        /// <summary>
        /// Tabela da prévia por nível (CA4; DS, seção 5): cabeçalho e uma linha por nível, de 1 ao máximo, com os valores
        /// nomeados e, quando existem, a recarga, o custo e as cargas. A linha de <paramref name="selected"/> tem o fundo de
        /// seleção e o marcador no número, para não depender só de cor.
        /// </summary>
        private void RefreshTable(GameplayAbility ability, int selected)
        {
            _table.Clear();
            List<string> names = ValueNames(ability);
            ScalableFloat cooldown = CooldownDuration(ability);
            GameplayEffect cost = ability.CostEffect;
            bool hasCost = cost != null && cost.Modifiers != null && cost.Modifiers.Exists(m => m?.Attribute != null);
            bool hasCharges = ability.Charges.Enabled;

            var header = new List<string> { "Nível" };
            header.AddRange(names);
            if (cooldown != null) header.Add("Recarga");
            if (hasCost) header.Add("Custo");
            if (hasCharges) header.Add("Cargas");
            VisualElement headerRow = AddRow(header);
            headerRow.AddToClassList(LevelHeaderClass);
            headerRow.style.unityFontStyleAndWeight = FontStyle.Bold;

            for (int level = 1; level <= ability.MaxLevel; level++)
            {
                string number = level.ToString(CultureInfo.CurrentCulture);
                var cells = new List<string> { level == selected ? number + LevelMarker : number };
                foreach (string name in names)
                {
                    cells.Add(ability.Values.TryGetValue(name, level, out float value) ? Format(value) : SummaryFormatter.None);
                }

                if (cooldown != null) cells.Add(Format(Evaluate(cooldown, level, ability.Values)) + " s");
                if (hasCost) cells.Add(Cost(ability, cost, level));
                if (hasCharges)
                {
                    float recharge = Evaluate(ability.Charges.RechargeTime, level, ability.Values);
                    cells.Add($"{MaxCharges(ability, level)} ({Format(recharge)} s)");
                }

                VisualElement row = AddRow(cells);
                row.AddToClassList(LevelRowClass);
                if (level != selected) continue;
                row.AddToClassList(LevelRowSelectedClass);
                row.style.backgroundColor = SelectionColor;
            }
        }

        /// <summary>Nomes da tabela de valores, na ordem do asset.</summary>
        private static List<string> ValueNames(GameplayAbility ability)
        {
            var names = new List<string>();
            using (var serialized = new SerializedObject(ability))
            {
                SerializedProperty entries = serialized.FindProperty("values.entries");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    names.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);
                }
            }

            return names;
        }

        private VisualElement AddRow(List<string> cells)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            foreach (string text in cells)
            {
                var cell = new Label(text);
                cell.style.flexGrow = 1;
                cell.style.flexBasis = 0;
                cell.style.overflow = Overflow.Hidden;
                row.Add(cell);
            }

            _table.Add(row);
            return row;
        }

        /// <summary>Um item por modificador com atributo: "Mana 20", em valor absoluto, ou o nome da fonte.</summary>
        private static string Cost(GameplayAbility ability, GameplayEffect cost, int level)
        {
            var items = new List<string>();
            foreach (AttributeEffectModifier modifier in cost.Modifiers)
            {
                AttributeDefinition attribute = modifier?.Attribute;
                if (attribute == null) continue;
                string name = string.IsNullOrEmpty(attribute.DisplayName.Text) ? attribute.name : attribute.DisplayName.Text;
                ModifierValueSource source = modifier.ValueSource;
                string amount = source == null ? SummaryFormatter.None
                    : source.Type == ModifierValueSource.SourceType.Value
                        ? Format(System.Math.Abs(Evaluate(source.Value, level, ability.Values)))
                        : SourceName(source);
                items.Add($"{name} {amount}");
            }

            return string.Join(", ", items);
        }

        /// <summary>Duração do efeito de recarga; nula sem efeito de recarga com duração.</summary>
        private static ScalableFloat CooldownDuration(GameplayAbility ability)
        {
            GameplayEffect cooldown = ability.CooldownEffect;
            return cooldown != null && cooldown.DurationPolicy == EffectDurationPolicy.HasDuration ? cooldown.Duration : null;
        }

        private static string Preview(GameplayAbility ability, int level)
        {
            var text = new StringBuilder();
            AbilityValues values = ability.Values;
            foreach (string name in ValueNames(ability))
            {
                if (values.TryGetValue(name, level, out float value))
                {
                    text.Append(name).Append(" = ").Append(Format(value)).Append('\n');
                }
            }

            ScalableFloat cooldown = CooldownDuration(ability);
            if (cooldown != null)
            {
                float seconds = Evaluate(cooldown, level, values);
                text.Append("Recarga = ").Append(Format(seconds)).Append(" s");
            }

            return text.ToString().TrimEnd('\n');
        }

        /// <summary>Valor no nível; sem o nome na tabela, vale o fallback sem o aviso do Console (o validador acusa).</summary>
        private static float Evaluate(ScalableFloat value, int level, AbilityValues values)
        {
            if (value == null) return 0f;
            return value.Mode != ScalableFloat.ScalableMode.Table ? value.Evaluate(level)
                : values.TryGetValue(value.TableName, level, out float named) ? named : value.Fallback;
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.CurrentCulture);
        }
    }
}
