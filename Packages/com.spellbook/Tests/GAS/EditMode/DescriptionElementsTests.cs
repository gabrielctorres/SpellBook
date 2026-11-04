using System;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Description;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Contrato novo do <c>DescriptionElement</c>, <c>AbilityDescription</c> e os elementos de texto, atributo e efeito
    /// (Spec do B-008, Design 5; parte do CA14).
    /// </summary>
    public class DescriptionElementsTests
    {
        private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

        private GasFactory _gas;

        [SetUp]
        public void SetUp() => _gas = new GasFactory();

        [TearDown]
        public void TearDown() => _gas.Dispose();

        [Test]
        public void TextElement_KeyAndFallback()
        {
            var element = new TextElement { Text = new LocalizedText("gas.test.hello", "Olá") };
            DescriptionContext withKey = Context(new KeyProvider("gas.test.hello", "Hello"));
            DescriptionContext withoutProvider = Context(null);

            element.Describe(withKey);
            element.Describe(withoutProvider);

            DescriptionLine line = withKey.Lines[0];
            Assert.That(line.Kind, Is.EqualTo(DescriptionLineKind.Text));
            Assert.That(line.Source, Is.SameAs(element));
            Assert.That(line.Role, Is.EqualTo(TextRole.None));
            Assert.That(LineText(line, withKey), Is.EqualTo("Hello"));
            Assert.That(LineText(withoutProvider.Lines[0], withoutProvider), Is.EqualTo("Olá"));
        }

        [Test]
        public void AttributeElement_SourceValue_NoSourceVariable()
        {
            AttributeDefinition power = Attribute("Poder");
            var element = new AttributeElement { Attribute = power };
            DescriptionContext withSource = Context(null, Asc(power, 30f));
            DescriptionContext withoutSource = Context(null);

            element.Describe(withSource);
            element.Describe(withoutSource);

            DescriptionLine line = withSource.Lines[0];
            Assert.That(line.Kind, Is.EqualTo(DescriptionLineKind.Attribute));
            Assert.That(line.Attribute, Is.SameAs(power));
            Assert.That(line.Label.Text, Is.EqualTo("Poder"));
            Assert.That(line.Value, Is.EqualTo(30f));
            Assert.That(line.Role, Is.EqualTo(TextRole.Value));
            Assert.That(line.Flags, Is.EqualTo(DescriptionLineFlags.None));
            Assert.That(line.Source, Is.SameAs(element));
            Assert.That(withoutSource.Lines[0].Value, Is.EqualTo(0f));
            Assert.That(withoutSource.Lines[0].Flags, Is.EqualTo(DescriptionLineFlags.Variable));
        }

        [Test]
        public void EffectModifiersElement_SameLinesAsDescriber()
        {
            AttributeDefinition armor = Attribute("Armadura");
            AttributeDefinition health = Attribute("Vida");
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(Modifier(armor, ModifierOperation.Add, 5f));
            effect.Modifiers.Add(Modifier(health, ModifierOperation.Multiply, 1.2f));
            var element = new EffectModifiersElement { Effect = effect };
            DescriptionContext byElement = Context(null);
            DescriptionContext byDescriber = Context(null);

            element.Describe(byElement);
            byDescriber.Effect = effect;
            EffectDescriber.Describe(effect, byDescriber);

            Assert.That(byElement.Lines, Is.EqualTo(byDescriber.Lines));
            Assert.That(byElement.Lines.Count, Is.EqualTo(2));
            Assert.That(byElement.Effect, Is.Null);
        }

        [Test]
        public void CustomElement_NewContract_AddsLines()
        {
            GameplayAbility ability = AbilityWith(new FixedValueElement(7f));
            DescriptionContext context = Context(null);
            context.Reset(ability, 3);

            ability.Describe(context);

            Assert.That(context.Lines.Count, Is.EqualTo(1));
            Assert.That(context.Lines[0].Value, Is.EqualTo(21f));
            Assert.That(LineText(context.Lines[0], context), Is.EqualTo("Fixo"));
        }

        [Test]
        public void AbilityDescription_InOrder_NullSkipped()
        {
            var first = new TextElement { Text = new LocalizedText(null, "Primeiro") };
            var second = new TextElement { Text = new LocalizedText(null, "Segundo") };
            GameplayAbility ability = AbilityWith(first, null, second);
            DescriptionContext context = Context(null);
            context.Lines.Add(new DescriptionLine(DescriptionLineKind.Text, new LocalizedText(null, "Antes"), 0f, TextRole.None));

            ability.Describe(context);

            Assert.That(context.Lines.Count, Is.EqualTo(3));
            Assert.That(context.Lines[1].Source, Is.SameAs(first));
            Assert.That(context.Lines[2].Source, Is.SameAs(second));
        }

        [Test]
        public void Ability_Describe_NullContextThrows_NoDescriptionNothing()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Description = null;
            DescriptionContext context = Context(null);

            ability.Describe(context);

            Assert.That(context.Lines, Is.Empty);
            Assert.Throws<ArgumentNullException>(() => ability.Describe(null));
            Assert.Throws<ArgumentNullException>(() => new AbilityDescription().Describe(null));
        }

        [TestCase(typeof(TextElement))]
        [TestCase(typeof(AttributeElement))]
        [TestCase(typeof(EffectModifiersElement))]
        public void Elements_HaveTypeCategoryWithSummary(Type type)
        {
            var category = (TypeCategoryAttribute)System.Attribute.GetCustomAttribute(type, typeof(TypeCategoryAttribute), false);

            Assert.That(category, Is.Not.Null);
            Assert.That(category.Path, Does.StartWith("Descrição/"));
            Assert.That(category.Summary, Is.Not.Null.And.Not.Empty);
            Assert.That(type.Namespace, Is.EqualTo("SpellBook.GAS.Description"));
            Assert.That(type.IsSealed, Is.True);
        }

        private GameplayAbility AbilityWith(params DescriptionElement[] elements)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Description = new AbilityDescription();
            ability.Description.Elements.AddRange(elements);
            return ability;
        }

        private AttributeDefinition Attribute(string name)
        {
            AttributeDefinition attribute = _gas.CreateAttribute(name);
            attribute.DisplayName = new LocalizedText(null, name);
            return attribute;
        }

        private AbilitySystemComponent Asc(AttributeDefinition attribute, float value)
        {
            AbilitySystemComponent asc = _gas.CreateAsc((attribute, value));
            GasFactory.SetAutoTick(asc, false);
            GasFactory.SetObjectField(asc, "attributeSet", asc.GetComponent<AttributeSet>());
            return asc;
        }

        private static AttributeEffectModifier Modifier(AttributeDefinition attribute, ModifierOperation op, float value)
        {
            return new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = op,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(value) }
            };
        }

        private static DescriptionContext Context(ITextProvider provider, AbilitySystemComponent source = null)
        {
            var context = new DescriptionContext { Text = provider };
            context.Reset(null, 1, source);
            return context;
        }

        private static string LineText(in DescriptionLine line, DescriptionContext context)
        {
            var buffer = new TextBuffer();
            DescriptionWriter.WriteLine(line, context, buffer);
            return buffer.ToString();
        }

        /// <summary>Elemento próprio: uma linha de texto com o valor vezes o nível.</summary>
        private sealed class FixedValueElement : DescriptionElement
        {
            private readonly float _value;

            public FixedValueElement(float value) => _value = value;

            public override void Describe(DescriptionContext context)
            {
                context.Lines.Add(new DescriptionLine(
                    DescriptionLineKind.Text, new LocalizedText(null, "Fixo"), _value * context.Level, TextRole.Value, source: this));
            }
        }

        /// <summary>Provedor com cultura en-US e uma chave.</summary>
        private sealed class KeyProvider : ITextProvider
        {
            private readonly string _key;
            private readonly string _text;

            public KeyProvider(string key, string text)
            {
                _key = key;
                _text = text;
            }

            public IFormatProvider Culture => EnUs;

            public string GetText(string key, string fallback) => key != null && key == _key ? _text : fallback;

            public event Action Changed { add { } remove { } }
        }
    }
}
