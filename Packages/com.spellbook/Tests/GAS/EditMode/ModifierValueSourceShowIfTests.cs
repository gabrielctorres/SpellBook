using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>Regra do <c>[ShowIf]</c> nos campos do <c>ModifierValueSource</c> (ADR 0026, item 5; Spec, seção 4.10).</summary>
    public class ModifierValueSourceShowIfTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ShowIf_ValueVisibleOnlyForValueType()
        {
            var effect = ScriptableObject.CreateInstance<GameplayEffect>();
            _created.Add(effect);
            effect.Modifiers = new List<AttributeEffectModifier>
            {
                new AttributeEffectModifier { ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value } },
                new AttributeEffectModifier { ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Attribute } },
            };
            var so = new SerializedObject(effect);
            ShowIfAttribute valueIf = GasShowIf(nameof(ModifierValueSource.Value));
            ShowIfAttribute attributeIf = GasShowIf(nameof(ModifierValueSource.Attribute));

            bool valueInFirst = ShowIfRule.IsVisible(ValueSourceField(so, 0, nameof(ModifierValueSource.Value)), valueIf);
            bool attributeInFirst = ShowIfRule.IsVisible(ValueSourceField(so, 0, nameof(ModifierValueSource.Attribute)), attributeIf);
            bool valueInSecond = ShowIfRule.IsVisible(ValueSourceField(so, 1, nameof(ModifierValueSource.Value)), valueIf);
            bool attributeInSecond = ShowIfRule.IsVisible(ValueSourceField(so, 1, nameof(ModifierValueSource.Attribute)), attributeIf);

            Assert.That(new[] { valueInFirst, attributeInFirst, valueInSecond, attributeInSecond },
                Is.EqualTo(new[] { true, false, false, true }));
        }

        private static ShowIfAttribute GasShowIf(string fieldName)
        {
            ShowIfAttribute attr = typeof(ModifierValueSource).GetField(fieldName).GetCustomAttribute<ShowIfAttribute>();
            Assert.That(attr, Is.Not.Null, $"ModifierValueSource.{fieldName} sem [ShowIf]");
            return attr;
        }

        private static SerializedProperty ValueSourceField(SerializedObject so, int index, string fieldName)
        {
            return so.FindProperty($"{nameof(GameplayEffect.Modifiers)}.Array.data[{index}].{nameof(AttributeEffectModifier.ValueSource)}.{fieldName}");
        }
    }
}
