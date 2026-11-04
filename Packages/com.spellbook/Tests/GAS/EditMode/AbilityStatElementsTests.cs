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
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Elementos de custo, recarga, duração e cargas e os rótulos com <c>{0}</c>/<c>{1}</c> do <c>DescriptionWriter</c>
    /// (Spec do B-008, Design 5; CA5, CA6 e parte do CA8).
    /// </summary>
    public class AbilityStatElementsTests
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
        private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

        private GasFactory _gas;
        private CooldownReductionPolicy _policy;

        [SetUp]
        public void SetUp() => _gas = new GasFactory();

        [TearDown]
        public void TearDown()
        {
            if (_policy != null) Object.DestroyImmediate(_policy);
            _gas.Dispose();
        }

        [Test]
        public void CostCooldownDurationCharges_OneLineEach_AtLevel3()
        {
            AttributeDefinition mana = Attribute("Mana");
            GameplayAbility ability = _gas.CreateAbility();
            ability.CostEffect = Effect(EffectDurationPolicy.Instant, ScalableFloat.Constant(0f));
            ability.CostEffect.Modifiers.Add(Modifier(mana, ScalableFloat.FromArray(-10f, -20f, -30f)));
            ability.CooldownEffect = Effect(EffectDurationPolicy.HasDuration, ScalableFloat.FromArray(8f, 7f, 6f));
            ability.Charges.Enabled = true;
            ability.Charges.MaxCharges = new ModifierValueSource { Value = ScalableFloat.Constant(3f) };
            ability.Charges.RechargeTime = ScalableFloat.Constant(5f);
            GameplayEffect buff = Effect(EffectDurationPolicy.HasDuration, ScalableFloat.FromArray(2f, 3f, 4f));
            SetElements(ability, new CostElement(), new CooldownElement(), new DurationElement { Effect = buff }, new ChargesElement());
            DescriptionContext context = Context(EnUs);
            context.Reset(ability, 3);

            ability.Describe(context);

            Assert.That(context.Lines.Count, Is.EqualTo(4));
            DescriptionLine cost = context.Lines[0];
            Assert.That(cost.Kind, Is.EqualTo(DescriptionLineKind.Cost));
            Assert.That(cost.Attribute, Is.SameAs(mana));
            Assert.That(cost.Value, Is.EqualTo(30f));
            Assert.That(cost.Role, Is.EqualTo(TextRole.Value));
            Assert.That(cost.Flags, Is.EqualTo(DescriptionLineFlags.Variable));
            Assert.That(context.Lines[1].Kind, Is.EqualTo(DescriptionLineKind.Cooldown));
            Assert.That(context.Lines[1].Value, Is.EqualTo(6f));
            Assert.That(context.Lines[1].Flags, Is.EqualTo(DescriptionLineFlags.Variable));
            Assert.That(context.Lines[2].Kind, Is.EqualTo(DescriptionLineKind.Duration));
            Assert.That(context.Lines[2].Value, Is.EqualTo(4f));
            Assert.That(context.Lines[3].Kind, Is.EqualTo(DescriptionLineKind.Charges));
            Assert.That(context.Lines[3].Value, Is.EqualTo(3f));
            Assert.That(context.Lines[3].SecondaryValue, Is.EqualTo(5f));
            Assert.That(context.Lines[3].Flags, Is.EqualTo(DescriptionLineFlags.None));
            Assert.That(LineText(context.Lines[0], context), Is.EqualTo("Mana 30"));
            Assert.That(LineText(context.Lines[1], context), Is.EqualTo("Recarga 6 s"));
            Assert.That(LineText(context.Lines[2], context), Is.EqualTo("Duração 4 s"));
            Assert.That(LineText(context.Lines[3], context), Is.EqualTo("3 cargas, uma a cada 5 s"));
        }

        [Test]
        public void NoCost_NoCooldown_NoCharges_NoLine()
        {
            GameplayAbility ability = _gas.CreateAbility();
            SetElements(ability, new CostElement(), new CooldownElement(), new ChargesElement(), new DurationElement());
            DescriptionContext context = Context(null);
            context.Reset(ability, 1);

            ability.Describe(context);
            context.Ability = null;
            new CostElement().Describe(context);
            new CooldownElement().Describe(context);
            new ChargesElement().Describe(context);

            Assert.That(context.Lines, Is.Empty);
        }

        [Test]
        public void Infinite_PermanentKey_Instant_NoLine()
        {
            var infinite = new DurationElement { Effect = Effect(EffectDurationPolicy.Infinite, ScalableFloat.Constant(9f)) };
            var instant = new DurationElement { Effect = Effect(EffectDurationPolicy.Instant, ScalableFloat.Constant(9f)) };
            DescriptionContext context = Context(null);

            infinite.Describe(context);
            instant.Describe(context);

            Assert.That(context.Lines.Count, Is.EqualTo(1));
            DescriptionLine line = context.Lines[0];
            Assert.That(line.Kind, Is.EqualTo(DescriptionLineKind.Duration));
            Assert.That(line.Flags, Is.EqualTo(DescriptionLineFlags.Permanent));
            Assert.That(line.Label.Key, Is.EqualTo("gas.description.permanent"));
            Assert.That(line.Source, Is.SameAs(infinite));
            Assert.That(LineText(line, context), Is.EqualTo("Permanente"));
        }

        [Test]
        public void Cooldown_WithAsc_AfterPolicy_WithoutAsc_Base()
        {
            AttributeDefinition cdr = Attribute("Redução");
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(10f);
            ability.CooldownReductionAttribute = cdr;
            _policy = Policy(maxReduction: 0.3f);
            GasFactory.SetObjectField(ability, "cooldownReductionPolicy", _policy);
            AbilitySystemComponent asc = Asc(cdr, 0.5f);
            DescriptionContext withAsc = Context(null);
            withAsc.Reset(ability, 1, asc);
            DescriptionContext withoutAsc = Context(null);
            withoutAsc.Reset(ability, 1);

            new CooldownElement().Describe(withAsc);
            new CooldownElement().Describe(withoutAsc);

            Assert.That(withAsc.Lines[0].Value, Is.EqualTo(7f).Within(1e-5f));
            Assert.That(withAsc.Lines[0].Value, Is.EqualTo(ability.GetCooldownDuration(asc)));
            Assert.That(withoutAsc.Lines[0].Value, Is.EqualTo(10f));
            Assert.That(withAsc.Lines[0].Flags, Is.EqualTo(DescriptionLineFlags.Variable));
            Assert.That(withoutAsc.Lines[0].Flags, Is.EqualTo(DescriptionLineFlags.Variable));
        }

        [Test]
        public void ConstantRecharge_VariableOnlyWithReductionAttribute()
        {
            GameplayAbility cooldown = _gas.CreateAbilityWithCooldown(10f);
            GameplayAbility charges = _gas.CreateAbility();
            charges.Charges.Enabled = true;
            charges.Charges.MaxCharges = new ModifierValueSource { Value = ScalableFloat.Constant(2f) };
            charges.Charges.RechargeTime = ScalableFloat.Constant(10f);
            DescriptionLineFlags cooldownPlain = Describe(new CooldownElement(), cooldown);
            DescriptionLineFlags chargesPlain = Describe(new ChargesElement(), charges);
            cooldown.CooldownReductionAttribute = Attribute("Redução");
            charges.CooldownReductionAttribute = cooldown.CooldownReductionAttribute;

            DescriptionLineFlags cooldownReduced = Describe(new CooldownElement(), cooldown);
            DescriptionLineFlags chargesReduced = Describe(new ChargesElement(), charges);

            Assert.That(cooldownPlain, Is.EqualTo(DescriptionLineFlags.None));
            Assert.That(chargesPlain, Is.EqualTo(DescriptionLineFlags.None));
            Assert.That(cooldownReduced, Is.EqualTo(DescriptionLineFlags.Variable));
            Assert.That(chargesReduced, Is.EqualTo(DescriptionLineFlags.Variable));
        }

        [Test]
        public void Charges_MaxFromTable_RechargeWithReduction()
        {
            AttributeDefinition cdr = Attribute("Redução");
            GameplayAbility ability = _gas.CreateAbility();
            ability.CooldownReductionAttribute = cdr;
            ability.Values.Add("cargas", ScalableFloat.FromArray(2f, 3f, 4.7f));
            ability.Charges.Enabled = true;
            ability.Charges.MaxCharges = new ModifierValueSource { Value = ScalableFloat.FromTable("cargas") };
            ability.Charges.RechargeTime = ScalableFloat.Constant(10f);
            DescriptionContext withAsc = Context(null);
            withAsc.Reset(ability, 3, Asc(cdr, 0.25f));
            DescriptionContext withoutAsc = Context(null);
            withoutAsc.Reset(ability, 2);

            new ChargesElement().Describe(withAsc);
            new ChargesElement().Describe(withoutAsc);

            Assert.That(withAsc.Lines[0].Value, Is.EqualTo(4f));
            Assert.That(withAsc.Lines[0].SecondaryValue, Is.EqualTo(7.5f).Within(1e-5f));
            Assert.That(withAsc.Lines[0].Flags, Is.EqualTo(DescriptionLineFlags.Variable));
            Assert.That(withAsc.Lines[0].Label.Key, Is.EqualTo("gas.description.charges"));
            Assert.That(withoutAsc.Lines[0].Value, Is.EqualTo(3f));
            Assert.That(withoutAsc.Lines[0].SecondaryValue, Is.EqualTo(10f));
        }

        [Test]
        public void Writer_ChargesLabel_SubstitutesValues_NoMarkup()
        {
            var line = new DescriptionLine(
                DescriptionLineKind.Charges, new LocalizedText("gas.description.charges", "{0} cargas, uma a cada {1}"), 3f,
                TextRole.Value, secondaryValue: 1.5f);
            DescriptionContext ptBr = Context(PtBr);
            DescriptionContext translated = Context(EnUs, "gas.description.charges", "{0} charges, one every {1}.");

            string pt = LineText(line, ptBr);
            string en = LineText(line, translated);

            Assert.That(pt, Is.EqualTo("3 cargas, uma a cada 1,5 s"));
            Assert.That(en, Is.EqualTo("3 charges, one every 1.5 s."));
            Assert.That(pt + en, Does.Not.Contain("<").And.Not.Contain("{"));
        }

        [Test]
        public void Writer_Seconds_IntegerWithoutDecimals()
        {
            DescriptionContext context = Context(PtBr);

            Assert.That(LineText(Cooldown(6f), context), Is.EqualTo("Recarga 6 s"));
            Assert.That(LineText(Cooldown(10f * 0.7f), context), Is.EqualTo("Recarga 7 s"));
            Assert.That(LineText(Cooldown(6.4f), context), Is.EqualTo("Recarga 6,4 s"));
        }

        [TestCase(typeof(CostElement))]
        [TestCase(typeof(CooldownElement))]
        [TestCase(typeof(DurationElement))]
        [TestCase(typeof(ChargesElement))]
        public void Elements_HaveTypeCategoryWithSummary(Type type)
        {
            var category = (TypeCategoryAttribute)System.Attribute.GetCustomAttribute(type, typeof(TypeCategoryAttribute), false);

            Assert.That(category, Is.Not.Null);
            Assert.That(category.Path, Does.StartWith("Descrição/"));
            Assert.That(category.Summary, Is.Not.Null.And.Not.Empty);
            Assert.That(type.IsSealed, Is.True);
            Assert.That(typeof(DescriptionElement).IsAssignableFrom(type), Is.True);
        }

        private static DescriptionLineFlags Describe(DescriptionElement element, GameplayAbility ability)
        {
            DescriptionContext context = Context(null);
            context.Reset(ability, 1);
            element.Describe(context);
            Assert.That(context.Lines.Count, Is.EqualTo(1));
            return context.Lines[0].Flags;
        }

        private static DescriptionLine Cooldown(float seconds)
        {
            return new DescriptionLine(DescriptionLineKind.Cooldown, new LocalizedText(null, "Recarga"), seconds, TextRole.Value);
        }

        private static void SetElements(GameplayAbility ability, params DescriptionElement[] elements)
        {
            ability.Description = new AbilityDescription();
            ability.Description.Elements.AddRange(elements);
        }

        private GameplayEffect Effect(EffectDurationPolicy policy, ScalableFloat duration)
        {
            GameplayEffect effect = _gas.CreateEffect(policy, 0f);
            effect.Duration = duration;
            return effect;
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

        private static CooldownReductionPolicy Policy(float maxReduction)
        {
            var policy = ScriptableObject.CreateInstance<CooldownReductionPolicy>();
            using (var serialized = new SerializedObject(policy))
            {
                serialized.FindProperty("maxReduction").floatValue = maxReduction;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return policy;
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
            var context = new DescriptionContext { Text = culture != null ? new KeyProvider(culture, key, text) : null };
            context.Reset(null, 1);
            return context;
        }

        private static string LineText(in DescriptionLine line, DescriptionContext context)
        {
            var buffer = new TextBuffer();
            DescriptionWriter.WriteLine(line, context, buffer);
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
