using System.Globalization;
using SpellBook.Core.Editor;
using SpellBook.GAS.Attributes;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Inspector do <see cref="AttributeDefinition"/> (R25). Depois do Inspector padrão: um valor de exemplo editável e
    /// a prévia do <see cref="AttributeDisplay.Format(float, System.IFormatProvider)"/> dele na cultura atual, refeita ao
    /// editar o exemplo ou o asset.
    /// </summary>
    [CustomEditor(typeof(AttributeDefinition))]
    [CanEditMultipleObjects]
    public sealed class AttributeDefinitionEditor : SpellBookEditor
    {
        public const string SampleFieldName = "spellbook-attribute-sample";
        public const string PreviewLabelName = "spellbook-attribute-preview";
        public const float DefaultSample = 1f;

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
        }
    }
}
