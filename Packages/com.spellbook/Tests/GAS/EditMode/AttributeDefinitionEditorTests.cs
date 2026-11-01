using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Editor;
using SpellBook.Testing;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Inspector do <see cref="AttributeDefinition"/>: prévia do <c>Format</c> (B-005, AR6: R25).</summary>
    public class AttributeDefinitionEditorTests
    {
        private GasFactory _gas;
        private UnityEditor.Editor _editor;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
            }

            _gas.Dispose();
        }

        [Test]
        public void Editor_PreviewShowsFormattedSample()
        {
            AttributeDefinition critChance = _gas.CreateAttribute("CritChance");
            critChance.Display.Kind = ValueKind.Percent;
            critChance.Display.Decimals = 0;
            _editor = UnityEditor.Editor.CreateEditor(critChance);

            VisualElement root = _editor.CreateInspectorGUI();

            var sample = root.Q<FloatField>(AttributeDefinitionEditor.SampleFieldName);
            var preview = root.Q<Label>(AttributeDefinitionEditor.PreviewLabelName);
            Assert.That(_editor, Is.InstanceOf<AttributeDefinitionEditor>());
            Assert.That(sample, Is.Not.Null);
            Assert.That(sample.value, Is.EqualTo(AttributeDefinitionEditor.DefaultSample));
            Assert.That(preview.text, Is.EqualTo("100%"));
        }
    }
}
