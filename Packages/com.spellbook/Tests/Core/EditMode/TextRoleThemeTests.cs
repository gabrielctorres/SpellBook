using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.Core.Tests
{
    /// <summary><c>TextRole</c> e <c>TextRoleTheme</c> no Core (Spec do B-005, Design 4; R19, R20, CA25, CA26, CA33).</summary>
    public class TextRoleThemeTests
    {
        private static readonly Color Beneficial = new Color(0.2f, 0.8f, 0.3f, 1f);
        private static readonly Color Harmful = new Color(0.9f, 0.25f, 0.25f, 1f);
        private static readonly Color Neutral = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color Value = new Color(1f, 0.85f, 0.3f, 1f);

        private TextRoleTheme _theme;

        [TearDown]
        public void TearDown()
        {
            if (_theme != null) Object.DestroyImmediate(_theme);
        }

        [Test]
        public void GetColor_FourRoles_NoneUsesNeutral()
        {
            _theme = CreateTheme(Beneficial, Harmful, Neutral, Value);

            Assert.That(_theme.GetColor(TextRole.Beneficial), Is.EqualTo(Beneficial));
            Assert.That(_theme.GetColor(TextRole.Harmful), Is.EqualTo(Harmful));
            Assert.That(_theme.GetColor(TextRole.Neutral), Is.EqualTo(Neutral));
            Assert.That(_theme.GetColor(TextRole.Value), Is.EqualTo(Value));
            Assert.That(_theme.GetColor(TextRole.None), Is.EqualTo(Neutral));
        }

        [Test]
        public void ChangingAssetColor_ChangesReadColor()
        {
            _theme = CreateTheme(Beneficial, Harmful, Neutral, Value);
            var changed = new Color(0.1f, 0.2f, 0.9f, 1f);

            SetColor(_theme, "beneficial", changed);

            Assert.That(_theme.GetColor(TextRole.Beneficial), Is.EqualTo(changed));
            Assert.That(_theme.GetColor(TextRole.Harmful), Is.EqualTo(Harmful), "mudar uma cor mexeu em outra");
        }

        [Test]
        public void Default_ExistsWithPackageColors()
        {
            TextRoleTheme theme = TextRoleTheme.Default;

            Assert.That(theme != null, Is.True, "o tema padrão do pacote não existe");
            Assert.That(TextRoleTheme.Default, Is.SameAs(theme), "o tema padrão foi criado de novo");
            Assert.That(theme.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
            Assert.That(theme.GetColor(TextRole.Beneficial), Is.EqualTo(new Color(0.30f, 0.80f, 0.35f, 1f)));
            Assert.That(theme.GetColor(TextRole.Harmful), Is.EqualTo(new Color(0.90f, 0.30f, 0.30f, 1f)));
            Assert.That(theme.GetColor(TextRole.Neutral), Is.EqualTo(new Color(0.85f, 0.85f, 0.85f, 1f)));
            Assert.That(theme.GetColor(TextRole.Value), Is.EqualTo(new Color(1.00f, 0.85f, 0.40f, 1f)));
        }

        [Test]
        public void NewTheme_StartsWithPackageColors()
        {
            _theme = ScriptableObject.CreateInstance<TextRoleTheme>();

            foreach (TextRole role in (TextRole[])Enum.GetValues(typeof(TextRole)))
            {
                Assert.That(_theme.GetColor(role), Is.EqualTo(TextRoleTheme.Default.GetColor(role)), role.ToString());
            }
        }

        [Test]
        public void GetColor_DoesNotAllocate()
        {
            _theme = CreateTheme(Beneficial, Harmful, Neutral, Value);
            Color sum = Color.clear;
            sum += _theme.GetColor(TextRole.None); // aquecimento

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    sum += _theme.GetColor((TextRole)(i % 5));
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(sum.a, Is.GreaterThan(0f));
        }

        [Test]
        public void Core_HasNoGasTypes()
        {
            Assembly core = typeof(TextRoleTheme).Assembly;

            Assert.That(core.GetName().Name, Is.EqualTo("SpellBook.Core"));
            Assert.That(core.GetReferencedAssemblies().Select(a => a.Name),
                Has.None.StartsWith("SpellBook.GAS"));
            Assert.That(core.GetTypes().Select(t => t.Namespace ?? string.Empty),
                Has.None.StartsWith("SpellBook.GAS"));
        }

        private static TextRoleTheme CreateTheme(Color beneficial, Color harmful, Color neutral, Color value)
        {
            var theme = ScriptableObject.CreateInstance<TextRoleTheme>();
            SetColor(theme, "beneficial", beneficial);
            SetColor(theme, "harmful", harmful);
            SetColor(theme, "neutral", neutral);
            SetColor(theme, "value", value);
            return theme;
        }

        private static void SetColor(TextRoleTheme theme, string field, Color color)
        {
            using var serialized = new SerializedObject(theme);
            SerializedProperty property = serialized.FindProperty(field);
            Assert.That(property, Is.Not.Null, $"campo serializado '{field}' não existe");
            property.colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
