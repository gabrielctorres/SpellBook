using System;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
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
    /// <c>TemplateElement</c>: <c>{nome}</c> pela tabela de valores no nível, atalhos <c>{@cooldown}</c>, <c>{@cost}</c> e
    /// <c>{@charges}</c>, linha inválida sem exceção e sem alocação (Spec do B-008, Design 5; CA7 e CA12).
    /// </summary>
    public class TemplateElementTests
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
        private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

        private GasFactory _gas;

        [SetUp]
        public void SetUp() => _gas = new GasFactory();

        [TearDown]
        public void TearDown() => _gas.Dispose();

        [Test]
        public void Template_Dano_50_75_100_Level1Is50_Level3Is100()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Values.Add("dano", ScalableFloat.FromArray(50f, 75f, 100f));
            ability.Values.Add("coef", ScalableFloat.Constant(0.25f));
            TemplateElement element = Template("Causa {dano} de dano ({coef})");
            DescriptionContext context = Context(PtBr);

            context.Reset(ability, 1);
            element.Describe(context);
            DescriptionLine level1 = context.Lines[0];
            string level1Text = LineText(level1, context);
            context.Reset(ability, 3);
            element.Describe(context);
            string level3Text = LineText(context.Lines[0], context);

            Assert.That(level1.Kind, Is.EqualTo(DescriptionLineKind.Text));
            Assert.That(level1.Source, Is.SameAs(element));
            Assert.That(level1.Label.Text, Is.EqualTo("Causa {dano} de dano ({coef})"));
            Assert.That(level1.Flags, Is.EqualTo(DescriptionLineFlags.Variable));
            Assert.That(level1Text, Is.EqualTo("Causa 50 de dano (0,25)"));
            Assert.That(level3Text, Is.EqualTo("Causa 100 de dano (0,25)"));
        }

        [Test]
        public void UnknownName_InvalidLine_LiteralKept_NoException()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Values.Add("dano", ScalableFloat.Constant(50f));
            TemplateElement element = Template("Causa {danno} e {dano}");
            DescriptionContext context = Context(PtBr);
            context.Reset(ability, 1);
            DescriptionContext noAbility = Context(PtBr);

            Assert.DoesNotThrow(() => element.Describe(context));
            Assert.DoesNotThrow(() => element.Describe(noAbility));

            Assert.That(context.Lines[0].Flags & DescriptionLineFlags.Invalid, Is.EqualTo(DescriptionLineFlags.Invalid));
            Assert.That(LineText(context.Lines[0], context), Is.EqualTo("Causa {danno} e 50"));
            Assert.That(noAbility.Lines[0].Flags & DescriptionLineFlags.Invalid, Is.EqualTo(DescriptionLineFlags.Invalid));
            Assert.That(LineText(noAbility.Lines[0], noAbility), Is.EqualTo("Causa {danno} e {dano}"));
        }

        [Test]
        public void Shortcuts_Cooldown_Cost_Charges_SameAsElements()
        {
            AttributeDefinition cdr = Attribute("Redução");
            GameplayAbility ability = FullAbility(cdr);
            AbilitySystemComponent asc = Asc(cdr, 0.25f);
            TemplateElement element = Template("Recarga {@cooldown}, custo {@cost}, cargas {@charges}");
            DescriptionContext context = Context(PtBr);
            context.Reset(ability, 3, asc);

            element.Describe(context);
            new CooldownElement().Describe(context);
            new CostElement().Describe(context);
            new ChargesElement().Describe(context);

            string expected = "Recarga " + ValueText(context.Lines[1], context)
                + ", custo " + ValueText(context.Lines[2], context) + ", " + ValueText(context.Lines[3], context)
                + ", cargas " + ValueText(context.Lines[4], context);
            Assert.That(LineText(context.Lines[0], context), Is.EqualTo(expected));
            Assert.That(expected, Is.EqualTo("Recarga 4,5 s, custo 30, 5, cargas 3"));
            Assert.That(context.Lines[0].Flags, Is.EqualTo(DescriptionLineFlags.Variable));
        }

        [Test]
        public void Shortcut_WithoutData_InvalidLiteral()
        {
            GameplayAbility ability = _gas.CreateAbility();
            const string text = "{@cooldown} {@cost} {@charges} {@foo}";
            TemplateElement element = Template(text);
            DescriptionContext context = Context(PtBr);
            context.Reset(ability, 1);

            element.Describe(context);

            Assert.That(context.Lines[0].Flags & DescriptionLineFlags.Invalid, Is.EqualTo(DescriptionLineFlags.Invalid));
            Assert.That(LineText(context.Lines[0], context), Is.EqualTo(text));
        }

        [Test]
        public void TemplateEdited_CacheRebuilt()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Values.Add("dano", ScalableFloat.Constant(50f));
            TemplateElement element = Template("A {dano}");
            DescriptionContext context = Context(PtBr);
            context.Reset(ability, 1);
            element.Describe(context);
            string before = LineText(context.Lines[0], context);

            element.Template = new LocalizedText(null, "B {dano} {dano}");
            ability.Values.Set("dano", ScalableFloat.Constant(60f));
            context.Reset(ability, 1);
            element.Describe(context);

            Assert.That(before, Is.EqualTo("A 50"));
            Assert.That(LineText(context.Lines[0], context), Is.EqualTo("B 60 60"));
            Assert.That(context.Lines[0].Label.Text, Is.EqualTo("B {dano} {dano}"));
        }

        [Test]
        public void Template_TranslatedByProvider_UsesProviderText()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Values.Add("dano", ScalableFloat.Constant(12.5f));
            var element = new TemplateElement { Template = new LocalizedText("skill.fireball.desc", "Causa {dano}") };
            DescriptionContext portuguese = Context(PtBr);
            portuguese.Reset(ability, 1);
            DescriptionContext english = Context(EnUs, "skill.fireball.desc", "Deals {dano} damage");
            english.Reset(ability, 1);

            element.Describe(portuguese);
            element.Describe(english);

            Assert.That(LineText(portuguese.Lines[0], portuguese), Is.EqualTo("Causa 12,5"));
            Assert.That(LineText(english.Lines[0], english), Is.EqualTo("Deals 12.5 damage"));
            Assert.That(LineText(portuguese.Lines[0], portuguese), Is.EqualTo("Causa 12,5"));
        }

        [Test]
        public void NonNameBraces_StayLiteral()
        {
            GameplayAbility ability = _gas.CreateAbility();
            const string text = "Use { x }, {a-b}, {}, {@} e {";
            TemplateElement element = Template(text);
            DescriptionContext context = Context(PtBr);
            context.Reset(ability, 1);

            element.Describe(context);

            Assert.That(context.Lines[0].Flags, Is.EqualTo(DescriptionLineFlags.None));
            Assert.That(LineText(context.Lines[0], context), Is.EqualTo(text));
        }

        [Test]
        public void AbilityValues_IndexOfSpan_SameAsString()
        {
            var values = new AbilityValues();
            values.Add("dano", ScalableFloat.Constant(1f));
            values.Add("raio", ScalableFloat.Constant(2f));

            Assert.That(values.IndexOf("xx raio yy".AsSpan(3, 4)), Is.EqualTo(values.IndexOf("raio")).And.EqualTo(1));
            Assert.That(values.IndexOf("dan".AsSpan()), Is.EqualTo(-1));
            Assert.That(values.IndexOf("Dano".AsSpan()), Is.EqualTo(-1));
            Assert.That(values.IndexOf(ReadOnlySpan<char>.Empty), Is.EqualTo(-1));
        }

        [Test]
        public void Template10000_DoesNotAllocate()
        {
            AttributeDefinition cdr = Attribute("Redução");
            AttributeDefinition life = Attribute("Vida");
            GameplayAbility ability = FullAbility(cdr);
            GameplayEffect buff = _gas.CreateEffect(EffectDurationPolicy.HasDuration, 4f);
            buff.Modifiers.Add(Modifier(life, ScalableFloat.FromArray(10f, 20f, 30f)));
            ability.Description = new AbilityDescription();
            ability.Description.Elements.AddRange(new DescriptionElement[]
            {
                new TextElement { Text = new LocalizedText(null, "Bola de fogo") },
                new AttributeElement { Attribute = cdr },
                new EffectModifiersElement { Effect = buff },
                new CostElement(),
                new CooldownElement(),
                new DurationElement { Effect = buff },
                new ChargesElement(),
                Template("Causa {dano}, recarga {@cooldown}, custo {@cost}, cargas {@charges}, {nada}")
            });
            AbilitySystemComponent asc = Asc(cdr, 0.25f);
            DescriptionContext context = Context(PtBr);
            var buffer = new TextBuffer();
            int total = 0;
            TestDelegate describe10000 = () =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    context.Reset(ability, 1 + i % 3, asc);
                    ability.Describe(context);
                    buffer.Clear();
                    for (int l = 0; l < context.Lines.Count; l++) DescriptionWriter.WriteLine(context.Lines[l], context, buffer);
                    total += buffer.Length;
                }
            };
            describe10000(); // aquecimento: capacidade da lista, do buffer e do cache do template

            Assert.That(describe10000, Is.Not.AllocatingGCMemory());
            Assert.That(context.Lines.Count, Is.EqualTo(9));
            Assert.That(total, Is.GreaterThan(0));
        }

        [Test]
        public void Element_HasTypeCategoryWithSummary()
        {
            var category = (TypeCategoryAttribute)System.Attribute.GetCustomAttribute(
                typeof(TemplateElement), typeof(TypeCategoryAttribute), false);

            Assert.That(category, Is.Not.Null);
            Assert.That(category.Path, Does.StartWith("Descrição/"));
            Assert.That(category.Summary, Is.Not.Null.And.Not.Empty);
            Assert.That(typeof(TemplateElement).IsSealed, Is.True);
        }

        /// <summary>Dano 50/75/100, custo de mana 10/20/30 e de vida 5, recarga 8/7/6 com redução, 3 cargas a cada 5 s.</summary>
        private GameplayAbility FullAbility(AttributeDefinition cdr)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Values.Add("dano", ScalableFloat.FromArray(50f, 75f, 100f));
            ability.CooldownReductionAttribute = cdr;
            ability.CostEffect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            ability.CostEffect.Modifiers.Add(Modifier(Attribute("Mana"), ScalableFloat.FromArray(-10f, -20f, -30f)));
            ability.CostEffect.Modifiers.Add(Modifier(Attribute("Vida"), ScalableFloat.Constant(-5f)));
            ability.CooldownEffect = _gas.CreateEffect(EffectDurationPolicy.HasDuration, 0f);
            ability.CooldownEffect.Duration = ScalableFloat.FromArray(8f, 7f, 6f);
            ability.Charges.Enabled = true;
            ability.Charges.MaxCharges = new ModifierValueSource { Value = ScalableFloat.Constant(3f) };
            ability.Charges.RechargeTime = ScalableFloat.Constant(5f);
            return ability;
        }

        private static TemplateElement Template(string text) => new TemplateElement { Template = new LocalizedText(null, text) };

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

        private static AttributeEffectModifier Modifier(AttributeDefinition attribute, ScalableFloat value)
        {
            return new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = value }
            };
        }

        private static DescriptionContext Context(CultureInfo culture, string key = null, string text = null)
        {
            var context = new DescriptionContext { Text = new KeyProvider(culture, key, text) };
            context.Reset(null, 1);
            return context;
        }

        private static string LineText(in DescriptionLine line, DescriptionContext context)
        {
            var buffer = new TextBuffer();
            DescriptionWriter.WriteLine(line, context, buffer);
            return buffer.ToString();
        }

        private static string ValueText(in DescriptionLine line, DescriptionContext context)
        {
            var buffer = new TextBuffer();
            DescriptionWriter.WriteValue(line, context, buffer);
            return buffer.ToString();
        }

        /// <summary>Provedor com uma cultura e, opcionalmente, uma chave.</summary>
        private sealed class KeyProvider : ITextProvider
        {
            private readonly string _key;
            private readonly string _text;

            public KeyProvider(CultureInfo culture, string key, string text)
            {
                Culture = culture;
                _key = key;
                _text = text;
            }

            public IFormatProvider Culture { get; }

            public string GetText(string key, string fallback) => key != null && key == _key ? _text : fallback;

            public event Action Changed { add { } remove { } }
        }
    }
}
