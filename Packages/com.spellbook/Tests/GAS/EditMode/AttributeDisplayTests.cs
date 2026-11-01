using System.Globalization;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.GAS.Attributes;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// <c>AttributeDisplay</c>, <c>Format</c>, <c>IsBeneficial</c> e <c>AttributeCategory</c>
    /// (Spec do B-005, Design 5; R15 a R18, CA21 a CA24 e CA26, parte do GAS).
    /// </summary>
    public class AttributeDisplayTests
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
        private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

        private AttributeDefinition _definition;
        private AttributeCategory _category;

        [TearDown]
        public void TearDown()
        {
            if (_definition != null) Object.DestroyImmediate(_definition);
            if (_category != null) Object.DestroyImmediate(_category);
        }

        [Test]
        public void Format_Percent0Dec_25pct()
        {
            AttributeDisplay display = Display(ValueKind.Percent, 0);

            Assert.That(display.Format(0.25f, EnUs), Is.EqualTo("25%"));
        }

        [Test]
        public void Format_Seconds1Dec_PtBr_EnUs()
        {
            AttributeDisplay display = Display(ValueKind.Seconds, 1);

            Assert.That(display.Format(1.5f, PtBr), Is.EqualTo("1,5 s"));
            Assert.That(display.Format(1.5f, EnUs), Is.EqualTo("1.5 s"));
        }

        [Test]
        public void Format_Multiplier_x2()
        {
            AttributeDisplay display = Display(ValueKind.Multiplier, 0);

            Assert.That(display.Format(2f, EnUs), Is.EqualTo("x2"));
        }

        [Test]
        public void Format_Flat_PrefixSuffixDecimals()
        {
            AttributeDisplay display = Display(ValueKind.Flat, 2);
            display.Prefix = "PV ";
            display.Suffix = " pts";

            Assert.That(display.Format(1234.5f, PtBr), Is.EqualTo("PV 1234,50 pts"));
            Assert.That(display.Format(1234.5f, EnUs), Is.EqualTo("PV 1234.50 pts"));
        }

        [Test]
        public void Format_PercentPoints()
        {
            AttributeDisplay display = Display(ValueKind.PercentPoints, 1);

            Assert.That(display.Format(12.5f, PtBr), Is.EqualTo("12,5%"));
        }

        [Test]
        public void Format_DecimalsAboveSix_UsesSix()
        {
            AttributeDisplay display = Display(ValueKind.Flat, 9);

            Assert.That(display.Format(0.5f, EnUs), Is.EqualTo("0.500000"));
        }

        [Test]
        public void Format_Op_AddSigned_MultiplyAsPercent()
        {
            AttributeDisplay display = Display(ValueKind.Flat, 0);

            Assert.That(display.Format(10f, ModifierOperation.Add, EnUs), Is.EqualTo("+10"));
            Assert.That(display.Format(-5f, ModifierOperation.AddFinal, EnUs), Is.EqualTo("-5"));
            Assert.That(display.Format(0.5f, ModifierOperation.Multiply, EnUs), Is.EqualTo("+50%"));
            Assert.That(display.Format(-0.25f, ModifierOperation.MultiplyCompound, EnUs), Is.EqualTo("-25%"));
            Assert.That(display.Format(50f, ModifierOperation.Override, EnUs), Is.EqualTo("50"));
        }

        [Test]
        public void Format_NullCulture_Invariant()
        {
            AttributeDisplay display = Display(ValueKind.Seconds, 1);
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = PtBr;
            try
            {
                Assert.That(display.Format(1.5f), Is.EqualTo("1.5 s"));
                Assert.That(display.Format(0.5f, ModifierOperation.Add), Is.EqualTo("+0.5 s"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void IsBeneficial_HigherAndLowerIsBetter_PosNegZero()
        {
            AttributeDisplay higher = Display(ValueKind.Flat, 0);
            AttributeDisplay lower = Display(ValueKind.Seconds, 1);
            lower.LowerIsBetter = true;

            Assert.That(higher.HigherIsBetter, Is.True);
            Assert.That(higher.IsBeneficial(10f, ModifierOperation.Add), Is.True);
            Assert.That(higher.IsBeneficial(-10f, ModifierOperation.Add), Is.False);
            Assert.That(higher.IsBeneficial(0f, ModifierOperation.Add), Is.False);
            Assert.That(higher.GetDeltaRole(10f), Is.EqualTo(TextRole.Beneficial));
            Assert.That(higher.GetDeltaRole(-10f), Is.EqualTo(TextRole.Harmful));
            Assert.That(higher.GetDeltaRole(0f), Is.EqualTo(TextRole.Neutral));

            Assert.That(lower.HigherIsBetter, Is.False);
            Assert.That(lower.IsBeneficial(-10f, ModifierOperation.Add), Is.True, "C10: recarga menor é boa");
            Assert.That(lower.IsBeneficial(10f, ModifierOperation.Multiply), Is.False);
            Assert.That(lower.IsBeneficial(0f, ModifierOperation.Add), Is.False);
            Assert.That(lower.GetDeltaRole(-10f), Is.EqualTo(TextRole.Beneficial));
            Assert.That(lower.GetDeltaRole(10f), Is.EqualTo(TextRole.Harmful));
            Assert.That(lower.GetDeltaRole(0f), Is.EqualTo(TextRole.Neutral));
        }

        [Test]
        public void OldDefinition_FlatZeroDecimalsVisible_ReadOnlyApi()
        {
            _definition = ScriptableObject.CreateInstance<AttributeDefinition>();

            AttributeDisplay display = _definition.Display;
            Assert.That(display.Kind, Is.EqualTo(ValueKind.Flat));
            Assert.That(display.Decimals, Is.EqualTo(0));
            Assert.That(display.Hidden, Is.False);
            Assert.That(display.HigherIsBetter, Is.True);
            Assert.That(display.Category, Is.Null);
            Assert.That(display.Role, Is.EqualTo(TextRole.None));
            Assert.That(_definition.Category, Is.Null);

            _category = ScriptableObject.CreateInstance<AttributeCategory>();
            _category.DisplayName = "Ofensivo";
            _definition.Display.Category = _category;

            Assert.That(_definition.Category, Is.SameAs(_category));
            PropertyInfo property = typeof(AttributeDefinition).GetProperty(nameof(AttributeDefinition.Category));
            Assert.That(property, Is.Not.Null);
            Assert.That(property.CanWrite, Is.False, "Category é somente leitura no AttributeDefinition");
        }

        [Test]
        public void IsBeneficial_DoesNotAllocate()
        {
            AttributeDisplay display = Display(ValueKind.Flat, 0);
            int beneficial = 0;
            int harmful = 0;
            if (display.IsBeneficial(1f, ModifierOperation.Add)) beneficial++; // aquecimento
            display.GetDeltaRole(1f);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    float delta = (i % 3) - 1f;
                    if (display.IsBeneficial(delta, (ModifierOperation)(i % 5))) beneficial++;
                    if (display.GetDeltaRole(delta) == TextRole.Harmful) harmful++;
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(beneficial, Is.EqualTo(334));
            Assert.That(harmful, Is.EqualTo(334));
        }

        private static AttributeDisplay Display(ValueKind kind, int decimals)
        {
            return new AttributeDisplay { Kind = kind, Decimals = decimals };
        }
    }
}
