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
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Inspector da <see cref="GameplayAbility"/> (R29; DS do Inspector da habilidade, seção 3). Abaixo dos avisos e da
    /// validação, quatro abas com os campos do Inspector padrão em grupos: Configuração (Identidade, Comportamento,
    /// Custo e recarga e Condições de ativação, com uma linha por regra: resumo da condição e motivo da falha), Ações
    /// (as abas de ações por evento, com "A cada passo" só em <see cref="AbilityBehavior.Channel"/>), Tags e gatilhos, e
    /// Níveis e prévia (o "nível de prévia", limitado a [1, MaxLevel], a prévia nesse nível de cada valor da tabela, da
    /// recarga sem a redução do atributo e das cargas, e a <see cref="DescriptionPreviewElement"/> no mesmo nível). Um
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
        private DescriptionPreviewElement _description;
        private PropertyField _channelDuration;
        private PropertyField _channelTickInterval;
        private PropertyField _reactivatePassive;
        private PropertyField _autocast;

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
            preview.Add(_level);
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
            ModifierValueSource source = charges.MaxCharges;
            string max = source.Type == ModifierValueSource.SourceType.Value
                ? System.Math.Max(0, (int)System.Math.Floor(Evaluate(source.Value, level, ability.Values))).ToString(CultureInfo.CurrentCulture)
                : ObjectNames.NicifyVariableName(source.Type.ToString()).ToLowerInvariant();
            return $"1 carga a cada {Format(seconds)} s, máximo {max} no nível {level}";
        }

        private static string Preview(GameplayAbility ability, int level)
        {
            var text = new StringBuilder();
            AbilityValues values = ability.Values;
            using (var serialized = new SerializedObject(ability))
            {
                SerializedProperty entries = serialized.FindProperty("values.entries");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    string name = entries.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                    if (values.TryGetValue(name, level, out float value))
                    {
                        text.Append(name).Append(" = ").Append(Format(value)).Append('\n');
                    }
                }
            }

            GameplayEffect cooldown = ability.CooldownEffect;
            if (cooldown != null && cooldown.DurationPolicy == EffectDurationPolicy.HasDuration && cooldown.Duration != null)
            {
                float seconds = Evaluate(cooldown.Duration, level, values);
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
