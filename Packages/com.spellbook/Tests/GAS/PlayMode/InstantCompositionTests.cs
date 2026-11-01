#if UNITY_EDITOR
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary>Instantâneo e tique usam a fórmula única, uma vez por atributo, sobre a base (B-005, AR3, ADR 0029: CA19, P5).</summary>
    public class InstantCompositionTests
    {
        private const float Tolerance = 1e-4f;

        private GasFactory _gas;
        private TimeFactory _time;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Instant_TwoMultiply_Sum_NotCompose()
        {
            AttributeDefinition power = CreateAttribute("Power");
            AbilitySystemComponent asc = CreateManualAsc((power, 100f));
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(Modifier(power, ModifierOperation.Multiply, 0.5f));
            effect.Modifiers.Add(Modifier(power, ModifierOperation.Multiply, 0.5f));

            asc.ApplyEffect(effect);

            Assert.That(asc.GetAttributeBaseValue(power), Is.EqualTo(200f).Within(Tolerance)); // 225 em sequência
        }

        [Test]
        public void Instant_MultiplyThenAdd_UsesFormula()
        {
            AttributeDefinition power = CreateAttribute("Power");
            AbilitySystemComponent asc = CreateManualAsc((power, 100f));
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(Modifier(power, ModifierOperation.Multiply, 0.5f));
            effect.Modifiers.Add(Modifier(power, ModifierOperation.Add, 10f));

            asc.ApplyEffect(effect);

            Assert.That(asc.GetAttributeBaseValue(power), Is.EqualTo(165f).Within(Tolerance)); // 160 em sequência
        }

        [Test]
        public void Instant_OverrideThenAdd_OverrideWins()
        {
            AttributeDefinition power = CreateAttribute("Power");
            AbilitySystemComponent asc = CreateManualAsc((power, 100f));
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(Modifier(power, ModifierOperation.Override, 50f));
            effect.Modifiers.Add(Modifier(power, ModifierOperation.Add, 10f));

            asc.ApplyEffect(effect);

            Assert.That(asc.GetAttributeBaseValue(power), Is.EqualTo(50f)); // 60 em sequência
        }

        [Test]
        public void Instant_InterleavedAttributes_EachUsesOwnModifiers()
        {
            AttributeDefinition power = CreateAttribute("Power");
            AttributeDefinition mana = CreateAttribute("Mana");
            AbilitySystemComponent asc = CreateManualAsc((power, 100f), (mana, 100f));
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(Modifier(power, ModifierOperation.Multiply, 0.5f));
            effect.Modifiers.Add(Modifier(mana, ModifierOperation.Add, 5f));
            effect.Modifiers.Add(Modifier(power, ModifierOperation.Add, 10f));
            effect.Modifiers.Add(Modifier(mana, ModifierOperation.MultiplyCompound, 1f));

            asc.ApplyEffect(effect);

            Assert.That(asc.GetAttributeBaseValue(power), Is.EqualTo(165f).Within(Tolerance), "Power");
            Assert.That(asc.GetAttributeBaseValue(mana), Is.EqualTo(210f).Within(Tolerance), "Mana"); // (100 + 5) x 2
        }

        [Test]
        public void Tick_UsesFormula()
        {
            AttributeDefinition power = CreateAttribute("Power");
            AbilitySystemComponent asc = CreateManualAsc((power, 100f));
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            effect.Period = 1f;
            effect.PeriodicModifiers.Add(Modifier(power, ModifierOperation.Multiply, 0.5f));
            effect.PeriodicModifiers.Add(Modifier(power, ModifierOperation.Add, 10f));
            effect.PeriodicModifiers.Add(Modifier(power, ModifierOperation.AddFinal, 1f));
            asc.ApplyEffect(effect);

            asc.Tick(1f);

            Assert.That(asc.GetAttributeBaseValue(power), Is.EqualTo(166f).Within(Tolerance)); // (100 + 10) x 1,5 + 1
        }

        [Test]
        public void TwoInstantApplications_Compose()
        {
            AttributeDefinition power = CreateAttribute("Power");
            AbilitySystemComponent asc = CreateManualAsc((power, 100f));
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(Modifier(power, ModifierOperation.Multiply, 0.5f));

            asc.ApplyEffect(effect);
            asc.ApplyEffect(effect);

            Assert.That(asc.GetAttributeBaseValue(power), Is.EqualTo(225f).Within(Tolerance)); // cada uma parte da base anterior
        }

        /// <summary>Atributo em [0, 1000], para os exemplos com base 100 não baterem no limite.</summary>
        private AttributeDefinition CreateAttribute(string name)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = 0f;
            definition.MaxValue = 1000f;
            return definition;
        }

        private static AttributeEffectModifier Modifier(AttributeDefinition attribute, ModifierOperation operation, float value)
        {
            return new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = operation,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(value) },
            };
        }

        private AbilitySystemComponent CreateManualAsc(params (AttributeDefinition Definition, float InitialValue)[] attributes)
        {
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }
    }
}
#endif
