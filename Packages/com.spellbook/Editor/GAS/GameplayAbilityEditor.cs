using System.Globalization;
using System.Text;
using SpellBook.Core.Editor;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Inspector da <see cref="GameplayAbility"/> (R29). Depois do Inspector padrão: o "nível de prévia", limitado a
    /// [1, MaxLevel], e a prévia nesse nível de cada valor da tabela e da recarga (sem a redução do atributo).
    /// </summary>
    [CustomEditor(typeof(GameplayAbility))]
    [CanEditMultipleObjects]
    public sealed class GameplayAbilityEditor : SpellBookEditor
    {
        public const string PreviewLevelName = "spellbook-ability-preview-level";
        public const string PreviewName = "spellbook-ability-preview";

        private IntegerField _level;
        private Label _preview;

        protected override void BuildAfterDefault(VisualElement root)
        {
            _level = new IntegerField("Nível de prévia") { name = PreviewLevelName, value = 1 };
            _preview = new Label { name = PreviewName };
            root.Add(_level);
            root.Add(_preview);

            _level.RegisterValueChangedCallback(evt => SetPreviewLevel(evt.newValue));
            root.TrackSerializedObjectValue(serializedObject, _ => SetPreviewLevel(_level.value));
            SetPreviewLevel(1);
        }

        /// <summary>Muda o nível de prévia, limitado a [1, MaxLevel], e refaz a prévia.</summary>
        public void SetPreviewLevel(int level)
        {
            if (_level == null || !(target is GameplayAbility ability)) return;

            level = level < 1 ? 1 : level > ability.MaxLevel ? ability.MaxLevel : level;
            _level.SetValueWithoutNotify(level);
            _preview.text = Preview(ability, level);
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
                ScalableFloat duration = cooldown.Duration;
                // Sem o nome na tabela, vale o fallback sem o aviso do Console (o validador acusa)
                float seconds = duration.Mode != ScalableFloat.ScalableMode.Table ? duration.Evaluate(level)
                    : values.TryGetValue(duration.TableName, level, out float named) ? named : duration.Fallback;
                text.Append("Recarga = ").Append(Format(seconds)).Append(" s");
            }

            return text.ToString().TrimEnd('\n');
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.CurrentCulture);
        }
    }
}
