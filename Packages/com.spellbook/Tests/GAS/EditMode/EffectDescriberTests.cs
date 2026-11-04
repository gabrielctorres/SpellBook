using System;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Description;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// <c>DescriptionLine</c>, <c>DescriptionContext</c>, formatador e <c>DescriptionWriter</c> com linhas montadas à mão
    /// (Spec do B-008, Design 4; CA3, CA4 e parte do CA8).
    /// </summary>
    public class EffectDescriberTests
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
        private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

        private GasFactory _gas;

        [SetUp]
        public void SetUp() => _gas = new GasFactory();

        [TearDown]
        public void TearDown() => _gas.Dispose();

        [Test]
        public void Seconds1_5_PtBr_EnUs()
        {
            AttributeDefinition recharge = Attribute("Recarga", ValueKind.Seconds, 1);
            DescriptionLine line = ModifierLine(recharge, 1.5f, ModifierOperation.Override, TextRole.Value);

            Assert.That(ValueText(line, Context(PtBr)), Is.EqualTo("1,5 s"));
            Assert.That(ValueText(line, Context(EnUs)), Is.EqualTo("1.5 s"));
        }

        [Test]
        public void CustomFormatter_ChangesOnlyValueText()
        {
            AttributeDefinition armor = Attribute("Armadura", ValueKind.Percent, 0);
            DescriptionLine line = ModifierLine(armor, 0.2f, ModifierOperation.Add, TextRole.Beneficial);
            DescriptionContext standard = Context(EnUs);
            DescriptionContext custom = Context(EnUs);
            custom.Formatter = new FixedTextFormatter("vinte por cento");

            Assert.That(LineText(line, standard), Is.EqualTo("Armadura +20%"));
            Assert.That(LineText(line, custom), Is.EqualTo("Armadura vinte por cento"));
        }

        [Test]
        public void DefaultFormatter_HasTypeCategoryWithSummary()
        {
            var category = (TypeCategoryAttribute)System.Attribute.GetCustomAttribute(
                typeof(DefaultModifierTextFormatter), typeof(TypeCategoryAttribute), false);

            Assert.That(category, Is.Not.Null);
            Assert.That(category.Summary, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void WriterOutput_HasNoMarkup()
        {
            AttributeDefinition armor = Attribute("Armadura", ValueKind.Percent, 1);
            var label = new LocalizedText("gas.attribute.armor", "Armadura");
            var ops = (ModifierOperation[])Enum.GetValues(typeof(ModifierOperation));
            var lines = new DescriptionLine[ops.Length + 2];
            for (int i = 0; i < ops.Length; i++)
                lines[i] = DescriptionLine.ForModifier(DescriptionLineKind.Modifier, armor, label, -0.25f, ops[i], TextRole.Harmful);
            lines[ops.Length] = new DescriptionLine(DescriptionLineKind.Attribute, label, 0.25f, TextRole.Value, attribute: armor);
            lines[ops.Length + 1] = new DescriptionLine(DescriptionLineKind.Cost, label, 0.1f, TextRole.Harmful, attribute: armor);
            DescriptionContext context = Context(PtBr, "gas.attribute.armor", "Blindagem");

            foreach (DescriptionLine line in lines)
            {
                string text = LineText(line, context);
                Assert.That(text, Does.StartWith("Blindagem "));
                Assert.That(text.IndexOfAny(new[] { '<', '>' }), Is.EqualTo(-1), text);
            }
        }

        [Test]
        public void AttributeDisplay_FormatToBuffer_SameAsString()
        {
            var buffer = new TextBuffer(4);
            foreach (ValueKind kind in (ValueKind[])Enum.GetValues(typeof(ValueKind)))
            foreach (ModifierOperation op in (ModifierOperation[])Enum.GetValues(typeof(ModifierOperation)))
            foreach (CultureInfo culture in new[] { PtBr, EnUs, null })
            {
                var display = new AttributeDisplay { Kind = kind, Decimals = 2, Prefix = "P ", Suffix = " S" };

                buffer.Clear();
                buffer.Append("|");
                display.Format(buffer, -1.25f, op, culture);
                buffer.Append("|");
                display.Format(buffer, 0.5f, culture);

                Assert.That(buffer.ToString(),
                    Is.EqualTo("|" + display.Format(-1.25f, op, culture) + "|" + display.Format(0.5f, culture)),
                    $"{kind} {op} {culture}");
            }
        }

        private AttributeDefinition Attribute(string name, ValueKind kind, int decimals)
        {
            AttributeDefinition attribute = _gas.CreateAttribute(name);
            attribute.DisplayName = new LocalizedText(null, name);
            attribute.Display.Kind = kind;
            attribute.Display.Decimals = decimals;
            return attribute;
        }

        private static DescriptionLine ModifierLine(AttributeDefinition attribute, float value, ModifierOperation op, TextRole role)
        {
            return DescriptionLine.ForModifier(DescriptionLineKind.Modifier, attribute, attribute.DisplayName, value, op, role);
        }

        private static DescriptionContext Context(CultureInfo culture, string key = null, string text = null)
        {
            var context = new DescriptionContext { Text = new FixedCultureProvider(culture, key, text) };
            context.Reset(null, 1);
            return context;
        }

        private static string ValueText(in DescriptionLine line, DescriptionContext context)
        {
            var buffer = new TextBuffer();
            DescriptionWriter.WriteValue(line, context, buffer);
            return buffer.ToString();
        }

        private static string LineText(in DescriptionLine line, DescriptionContext context)
        {
            var buffer = new TextBuffer();
            DescriptionWriter.WriteLine(line, context, buffer);
            return buffer.ToString();
        }

        /// <summary>Provedor com cultura fixa e, no máximo, uma chave.</summary>
        private sealed class FixedCultureProvider : ITextProvider
        {
            private readonly string _key;
            private readonly string _text;

            public FixedCultureProvider(IFormatProvider culture, string key, string text)
            {
                Culture = culture;
                _key = key;
                _text = text;
            }

            public IFormatProvider Culture { get; }

            public string GetText(string key, string fallback) => key != null && key == _key ? _text : fallback;

            public event Action Changed { add { } remove { } }
        }

        /// <summary>Formatador próprio que escreve sempre o mesmo texto.</summary>
        private sealed class FixedTextFormatter : ModifierTextFormatter
        {
            private readonly string _text;

            public FixedTextFormatter(string text) => _text = text;

            public override void WriteValue(in DescriptionLine line, TextBuffer into, IFormatProvider culture) => into.Append(_text);
        }
    }
}
