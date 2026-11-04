using System;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Description;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// <c>DescriptionLine</c>, <c>DescriptionContext</c>, <c>EffectDescriber</c>, formatador e <c>DescriptionWriter</c>
    /// (Spec do B-008, Design 4; CA1 a CA4 e partes do CA8 e do CA12).
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
            DescriptionContext described = Context(EnUs);
            EffectDescriber.Describe(Effect(Modifier(recharge, ModifierOperation.Override, ScalableFloat.Constant(1.5f))), described);
            DescriptionLine line = described.Lines[0];

            Assert.That(line.Role, Is.EqualTo(TextRole.Value), "Override mostra um valor, não uma variação");
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

        [Test]
        public void Armor20Percent_LineAttributeValueOpRoleBeneficial_ValueText()
        {
            AttributeDefinition armor = Attribute("Armadura", ValueKind.Percent, 0);
            GameplayEffect effect = Effect(Modifier(armor, ModifierOperation.Add, ScalableFloat.Constant(0.2f)));
            DescriptionContext context = Context(EnUs);

            EffectDescriber.Describe(effect, context);

            Assert.That(context.Lines.Count, Is.EqualTo(1));
            DescriptionLine line = context.Lines[0];
            Assert.That(line.Kind, Is.EqualTo(DescriptionLineKind.Modifier));
            Assert.That(line.Attribute, Is.SameAs(armor));
            Assert.That(line.Label.Text, Is.EqualTo("Armadura"));
            Assert.That(line.Value, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(line.HasOperation, Is.True);
            Assert.That(line.Operation, Is.EqualTo(ModifierOperation.Add));
            Assert.That(line.Role, Is.EqualTo(TextRole.Beneficial));
            Assert.That(line.Flags, Is.EqualTo(DescriptionLineFlags.None));
            Assert.That(ValueText(line, context), Is.EqualTo("+20%"));
        }

        [Test]
        public void Stacks3Level2_ValueComposed_VariableWhenLevelOrCaller()
        {
            AttributeDefinition damage = Attribute("Dano", ValueKind.Flat, 0);
            var caller = new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.SetByCaller, CallerName = "dano", CallerHasDefault = true, CallerDefault = 5f
            };
            GameplayEffect effect = Effect(
                Modifier(damage, ModifierOperation.Add, ScalableFloat.FromArray(10f, 20f)),
                Modifier(damage, ModifierOperation.Add, caller),
                Modifier(damage, ModifierOperation.Add, ScalableFloat.Constant(4f)));
            DescriptionContext context = Context(EnUs);
            context.Level = 2;
            context.Stacks = 3;

            EffectDescriber.Describe(effect, context);
            effect.StacksMultiplyMagnitude = false;
            EffectDescriber.Describe(effect, context);

            Assert.That(context.Lines[0].Value, Is.EqualTo(60f));
            Assert.That(context.Lines[0].Flags, Is.EqualTo(DescriptionLineFlags.Variable));
            Assert.That(context.Lines[1].Value, Is.EqualTo(15f));
            Assert.That(context.Lines[1].Flags, Is.EqualTo(DescriptionLineFlags.Variable));
            Assert.That(context.Lines[2].Value, Is.EqualTo(12f));
            Assert.That(context.Lines[2].Flags, Is.EqualTo(DescriptionLineFlags.None));
            Assert.That(context.Lines[3].Value, Is.EqualTo(20f), "sem StacksMultiplyMagnitude, as pilhas não multiplicam");
        }

        [Test]
        public void HiddenAttribute_Skipped()
        {
            AttributeDefinition visible = Attribute("Vida", ValueKind.Flat, 0);
            AttributeDefinition hidden = Attribute("Interno", ValueKind.Flat, 0);
            hidden.Display.Hidden = true;
            GameplayEffect effect = Effect(
                Modifier(hidden, ModifierOperation.Add, ScalableFloat.Constant(1f)),
                null,
                Modifier(visible, ModifierOperation.Add, ScalableFloat.Constant(2f)));
            DescriptionContext context = Context(EnUs);

            EffectDescriber.Describe(effect, context);

            Assert.That(context.Lines.Count, Is.EqualTo(1));
            Assert.That(context.Lines[0].Attribute, Is.SameAs(visible));
        }

        [Test]
        public void Describe_AppendsWithoutClearing()
        {
            AttributeDefinition health = Attribute("Vida", ValueKind.Flat, 0);
            GameplayEffect effect = Effect(Modifier(health, ModifierOperation.Add, ScalableFloat.Constant(-3f)));
            DescriptionContext context = Context(EnUs);
            var existing = new DescriptionLine(DescriptionLineKind.Text, new LocalizedText(null, "Antes"), 0f, TextRole.None);
            context.Lines.Add(existing);

            EffectDescriber.Describe(effect, context);

            Assert.That(context.Lines.Count, Is.EqualTo(2));
            Assert.That(context.Lines[0], Is.EqualTo(existing));
            Assert.That(context.Lines[1].Role, Is.EqualTo(TextRole.Harmful));
        }

        [Test]
        public void Describe10000_ReusedContext_DoesNotAllocate()
        {
            AttributeDefinition armor = Attribute("Armadura", ValueKind.Percent, 1);
            AttributeDefinition damage = Attribute("Dano", ValueKind.Flat, 0);
            GameplayEffect effect = Effect(
                Modifier(armor, ModifierOperation.Multiply, ScalableFloat.Constant(0.1f)),
                Modifier(damage, ModifierOperation.Add, ScalableFloat.FromArray(10f, 20f, 30f)));
            DescriptionContext context = Context(PtBr);
            var buffer = new TextBuffer();
            int total = 0;
            TestDelegate describe10000 = () =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    context.Lines.Clear();
                    context.Level = 1 + i % 3;
                    EffectDescriber.Describe(effect, context);
                    buffer.Clear();
                    for (int l = 0; l < context.Lines.Count; l++) DescriptionWriter.WriteLine(context.Lines[l], context, buffer);
                    total += buffer.Length;
                }
            };
            describe10000(); // aquecimento: capacidade da lista e do buffer, tabelas do formatador do runtime

            Assert.That(describe10000, Is.Not.AllocatingGCMemory());
            Assert.That(total, Is.GreaterThan(0));
        }

        [Test]
        public void AttributeSource_NoInstigator_ZeroVariable_WithInstigator_Value()
        {
            AttributeDefinition strength = Attribute("Força", ValueKind.Flat, 0);
            AttributeDefinition damage = Attribute("Dano", ValueKind.Flat, 0);
            var source = new ModifierValueSource { Type = ModifierValueSource.SourceType.Attribute, Attribute = strength };
            GameplayEffect effect = Effect(Modifier(damage, ModifierOperation.Add, source));
            DescriptionContext context = Context(EnUs);

            EffectDescriber.Describe(effect, context);
            context.Source = Asc(strength, 30f);
            EffectDescriber.Describe(effect, context);

            Assert.That(context.Lines[0].Value, Is.EqualTo(0f));
            Assert.That(context.Lines[0].Flags, Is.EqualTo(DescriptionLineFlags.Variable));
            Assert.That(context.Lines[1].Value, Is.EqualTo(30f));
            Assert.That(context.Lines[1].Flags, Is.EqualTo(DescriptionLineFlags.Variable));
        }

        [Test]
        public void PeriodicModifiers_OnlyWithPeriod()
        {
            AttributeDefinition health = Attribute("Vida", ValueKind.Flat, 0);
            GameplayEffect effect = Effect();
            effect.PeriodicModifiers.Add(Modifier(health, ModifierOperation.Add, ScalableFloat.Constant(-2f)));
            DescriptionContext context = Context(EnUs);

            EffectDescriber.Describe(effect, context);
            int withoutPeriod = context.Lines.Count;
            effect.Period = 1f;
            EffectDescriber.Describe(effect, context);

            Assert.That(withoutPeriod, Is.EqualTo(0));
            Assert.That(context.Lines.Count, Is.EqualTo(1));
            Assert.That(context.Lines[0].Value, Is.EqualTo(-2f));
        }

        private AttributeDefinition Attribute(string name, ValueKind kind, int decimals)
        {
            AttributeDefinition attribute = _gas.CreateAttribute(name);
            attribute.DisplayName = new LocalizedText(null, name);
            attribute.Display.Kind = kind;
            attribute.Display.Decimals = decimals;
            return attribute;
        }

        private GameplayEffect Effect(params AttributeEffectModifier[] modifiers)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.AddRange(modifiers);
            return effect;
        }

        private AbilitySystemComponent Asc(AttributeDefinition attribute, float value)
        {
            AbilitySystemComponent asc = _gas.CreateAsc((attribute, value));
            GasFactory.SetAutoTick(asc, false);
            GasFactory.SetObjectField(asc, "attributeSet", asc.GetComponent<AttributeSet>());
            return asc;
        }

        private static AttributeEffectModifier Modifier(AttributeDefinition attribute, ModifierOperation op, ScalableFloat value)
        {
            return Modifier(attribute, op, new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = value });
        }

        private static AttributeEffectModifier Modifier(AttributeDefinition attribute, ModifierOperation op, ModifierValueSource source)
        {
            return new AttributeEffectModifier { Attribute = attribute, Operation = op, ValueSource = source };
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
