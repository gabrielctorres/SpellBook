using System.Collections.Generic;
using System.Globalization;
using SpellBook.Core.Editor;
using SpellBook.Core.Editor.Inspector;
using SpellBook.GAS.Attributes;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Inspector do <see cref="AttributeDefinition"/> (R25). Depois do Inspector padrão: um valor de exemplo editável e
    /// a prévia do <see cref="AttributeDisplay.Format(float, System.IFormatProvider)"/> dele na cultura atual, refeita ao
    /// editar o exemplo ou o asset. Com um asset selecionado, vem depois a seção "Dano": uma linha de resumo por
    /// processador e a prévia "dano → processadores → alterações" do <see cref="DamagePreviewModel"/>, com um campo de
    /// valor por atributo. O ASC temporário da prévia é destruído quando o Inspector fecha.
    /// </summary>
    [CustomEditor(typeof(AttributeDefinition))]
    [CanEditMultipleObjects]
    public sealed class AttributeDefinitionEditor : SpellBookEditor
    {
        public const string SampleFieldName = "spellbook-attribute-sample";
        public const string PreviewLabelName = "spellbook-attribute-preview";
        public const float DefaultSample = 1f;
        public const string DamageSectionName = "spellbook-attribute-damage";
        public const string ProcessorRowClass = "spellbook-attribute-processor-row";
        public const string DamageSampleFieldName = "spellbook-attribute-damage-sample";
        public const string PreviewRowClass = "spellbook-attribute-damage-row";
        public const float DefaultDamage = 10f;

        private DamagePreviewModel _damagePreview;

        protected override void BuildAfterDefault(VisualElement root)
        {
            var sample = new FloatField("Valor de exemplo") { name = SampleFieldName, value = DefaultSample };
            var preview = new Label { name = PreviewLabelName };
            root.Add(sample);
            root.Add(preview);

            void Refresh()
            {
                if (target is AttributeDefinition definition)
                {
                    preview.text = definition.Display.Format(sample.value, CultureInfo.CurrentCulture);
                }
            }

            Refresh();
            sample.RegisterValueChangedCallback(_ => Refresh());
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());

            if (targets.Length == 1 && target is AttributeDefinition single)
            {
                BuildDamage(root, single);
            }
        }

        private void OnDisable()
        {
            _damagePreview?.Dispose();
            _damagePreview = null;
        }

        private void BuildDamage(VisualElement root, AttributeDefinition definition)
        {
            _damagePreview?.Dispose();
            _damagePreview = new DamagePreviewModel(definition) { SampleDamage = DefaultDamage };
            DamagePreviewModel model = _damagePreview;

            var section = new VisualElement { name = DamageSectionName };
            section.Add(new Label("Dano") { style = { unityFontStyleAndWeight = UnityEngine.FontStyle.Bold } });
            var processorRows = new VisualElement();
            section.Add(processorRows);
            var damage = new FloatField("Dano de exemplo") { name = DamageSampleFieldName, value = DefaultDamage };
            section.Add(damage);
            var values = new VisualElement();
            section.Add(values);
            var changes = new VisualElement();
            section.Add(changes);
            root.Add(section);

            void RefreshChanges()
            {
                changes.Clear();
                model.SampleDamage = damage.value;
                IReadOnlyList<DamagePreviewRow> rows = model.Compute();
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = new Label(FormatChange(rows[i]));
                    row.AddToClassList(PreviewRowClass);
                    changes.Add(row);
                }
            }

            void RebuildValues()
            {
                values.Clear();
                IReadOnlyList<AttributeDefinition> attributes = model.Attributes;
                for (int i = 0; i < attributes.Count; i++)
                {
                    AttributeDefinition attribute = attributes[i];
                    var field = new FloatField(attribute.name) { value = model.GetValue(attribute) };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        model.SetValue(attribute, evt.newValue);
                        RefreshChanges();
                    });
                    values.Add(field);
                }
            }

            void RefreshAll()
            {
                processorRows.Clear();
                serializedObject.Update();
                SerializedProperty processors = serializedObject.FindProperty("processors");
                for (int i = 0; processors != null && i < processors.arraySize; i++)
                {
                    var row = new Label($"{i + 1}. {SummaryFormatter.Format(processors.GetArrayElementAtIndex(i))}");
                    row.AddToClassList(ProcessorRowClass);
                    processorRows.Add(row);
                }

                if (model.RefreshAttributes() || values.childCount == 0)
                {
                    RebuildValues();
                }

                RefreshChanges();
            }

            RefreshAll();
            damage.RegisterValueChangedCallback(_ => RefreshChanges());
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshAll());
        }

        /// <summary>"Atributo: antes → depois (variação)", com "via" e o atributo de origem numa alteração redirecionada.</summary>
        private static string FormatChange(in DamagePreviewRow row)
        {
            string name = row.Attribute != null ? row.Attribute.name : SummaryFormatter.None;
            string text = $"{name}: {Number(row.Before)} → {Number(row.After)} ({Number(row.Applied, true)})";
            return row.RedirectedFrom != null ? $"{text} via {row.RedirectedFrom.name}" : text;
        }

        private static string Number(float value, bool signed = false)
        {
            string format = signed ? "+0.##;-0.##;0" : "0.##";
            return value.ToString(format, CultureInfo.CurrentCulture);
        }
    }
}
