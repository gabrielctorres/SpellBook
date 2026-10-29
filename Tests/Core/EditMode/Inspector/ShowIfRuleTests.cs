using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.Core.Tests.Inspector
{
    /// <summary>Regra de visibilidade do <c>[ShowIf]</c> (ADR 0026, item 5; Spec, seção 4.10).</summary>
    public class ShowIfRuleTests
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
        public void EnumSibling_Equal_Visible_Different_Hidden()
        {
            SerializedObject so = Holder(out ShowIfHolder holder);
            var attr = new ShowIfAttribute(nameof(ShowIfHolder.mode), ShowIfMode.Second);

            holder.mode = ShowIfMode.Second;
            so.Update();
            bool equal = ShowIfRule.IsVisible(so.FindProperty(nameof(ShowIfHolder.value)), attr);
            holder.mode = ShowIfMode.Third;
            so.Update();
            bool different = ShowIfRule.IsVisible(so.FindProperty(nameof(ShowIfHolder.value)), attr);

            Assert.That(equal, Is.True);
            Assert.That(different, Is.False);
        }

        [Test]
        public void BoolSibling_True_Visible_False_Hidden()
        {
            SerializedObject so = Holder(out ShowIfHolder holder);
            var attr = new ShowIfAttribute(nameof(ShowIfHolder.flag), true);

            holder.flag = true;
            so.Update();
            bool whenTrue = ShowIfRule.IsVisible(so.FindProperty(nameof(ShowIfHolder.value)), attr);
            holder.flag = false;
            so.Update();
            bool whenFalse = ShowIfRule.IsVisible(so.FindProperty(nameof(ShowIfHolder.value)), attr);

            Assert.That(whenTrue, Is.True);
            Assert.That(whenFalse, Is.False);
        }

        [Test]
        public void MissingSibling_Visible_AndWarnsOnce()
        {
            SerializedObject so = Holder(out _);
            SerializedProperty field = so.FindProperty(nameof(ShowIfHolder.value));
            var attr = new ShowIfAttribute("naoExiste", true);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[ShowIf\].*naoExiste"));

            bool first = ShowIfRule.IsVisible(field, attr);
            bool second = ShowIfRule.IsVisible(field, attr);

            Assert.That(first, Is.True);
            Assert.That(second, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NestedStructInList_UsesSiblingInSameElement()
        {
            var effect = ScriptableObject.CreateInstance<GameplayEffect>();
            _created.Add(effect);
            effect.Modifiers = new List<AttributeEffectModifier>
            {
                new AttributeEffectModifier { ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Constant } },
                new AttributeEffectModifier { ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Attribute } },
            };
            var so = new SerializedObject(effect);
            ShowIfAttribute constantIf = GasShowIf(nameof(ModifierValueSource.ConstantValue));
            ShowIfAttribute attributeIf = GasShowIf(nameof(ModifierValueSource.Attribute));

            bool constantInFirst = ShowIfRule.IsVisible(ValueSourceField(so, 0, nameof(ModifierValueSource.ConstantValue)), constantIf);
            bool attributeInFirst = ShowIfRule.IsVisible(ValueSourceField(so, 0, nameof(ModifierValueSource.Attribute)), attributeIf);
            bool constantInSecond = ShowIfRule.IsVisible(ValueSourceField(so, 1, nameof(ModifierValueSource.ConstantValue)), constantIf);
            bool attributeInSecond = ShowIfRule.IsVisible(ValueSourceField(so, 1, nameof(ModifierValueSource.Attribute)), attributeIf);

            Assert.That(new[] { constantInFirst, attributeInFirst, constantInSecond, attributeInSecond },
                Is.EqualTo(new[] { true, false, false, true }));
        }

        private SerializedObject Holder(out ShowIfHolder holder)
        {
            holder = ScriptableObject.CreateInstance<ShowIfHolder>();
            _created.Add(holder);
            return new SerializedObject(holder);
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

    internal enum ShowIfMode
    {
        First,
        Second,
        Third,
    }

    /// <summary>Dono dos campos dos testes do <c>[ShowIf]</c>; criado só em memória.</summary>
    internal class ShowIfHolder : ScriptableObject
    {
        [SerializeField] internal ShowIfMode mode;
        [SerializeField] internal bool flag;
        [SerializeField] internal float value;
    }
}
