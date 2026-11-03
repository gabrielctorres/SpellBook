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

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Inspector da <see cref="GameplayAbility"/> (R29). Depois do Inspector padrão: o "nível de prévia", limitado a
    /// [1, MaxLevel], e a prévia nesse nível de cada valor da tabela e da recarga (sem a redução do atributo); a seção
    /// "Ativação", com uma linha por regra (resumo da condição e motivo da falha); a prévia das cargas no mesmo nível; e as
    /// abas de ações por evento, com "A cada passo" só em <see cref="AbilityBehavior.Channel"/>.
    /// </summary>
    [CustomEditor(typeof(GameplayAbility))]
    [CanEditMultipleObjects]
    public sealed class GameplayAbilityEditor : SpellBookEditor
    {
        public const string PreviewLevelName = "spellbook-ability-preview-level";
        public const string PreviewName = "spellbook-ability-preview";
        public const string ActivationName = "spellbook-ability-activation";
        public const string RuleRowClass = "spellbook-ability-rule-row";
        public const string ChargesPreviewName = "spellbook-ability-charges-preview";

        private IntegerField _level;
        private Label _preview;
        private VisualElement _rules;
        private Label _charges;

        protected override void BuildAfterDefault(VisualElement root)
        {
            _level = new IntegerField("Nível de prévia") { name = PreviewLevelName, value = 1 };
            _preview = new Label { name = PreviewName };
            root.Add(_level);
            root.Add(_preview);

            var activation = new VisualElement { name = ActivationName };
            activation.Add(new Label("Ativação") { style = { unityFontStyleAndWeight = UnityEngine.FontStyle.Bold } });
            _rules = new VisualElement();
            activation.Add(_rules);
            _charges = new Label { name = ChargesPreviewName };
            activation.Add(_charges);
            root.Add(activation);

            ActionEventsView.HideDefaultFields(root, typeof(GameplayAbility));
            root.Add(new ActionEventsView(serializedObject,
                "Esta habilidade ainda não faz nada. Adicione uma ação em \"Ao ativar\".",
                field => field != nameof(GameplayAbility.OnChannelTick) || IsChannel()));

            _level.RegisterValueChangedCallback(evt => SetPreviewLevel(evt.newValue));
            root.TrackSerializedObjectValue(serializedObject, _ => SetPreviewLevel(_level.value));
            SetPreviewLevel(1);
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
