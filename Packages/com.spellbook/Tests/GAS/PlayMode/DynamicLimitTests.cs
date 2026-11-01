#if UNITY_EDITOR
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.GAS.Tests
{
    /// <summary>Limites vindos de outro atributo e política ao mudar o máximo (B-005, AR1: CA1 a CA9).</summary>
    public class DynamicLimitTests
    {
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
        public void MaxFollowsSource_HealStopsAt150()
        {
            AttributeDefinition maxHealth = CreateAttribute("MaxHealth", 0f, 9999f);
            AttributeDefinition health = CreateAttribute("Health", 0f, 100f);
            BindMax(health, maxHealth);
            AbilitySystemComponent asc = CreateManualAsc((maxHealth, 100f), (health, 100f));
            AttributeState state = GetState(asc, health);

            GetState(asc, maxHealth).AddModifier(new AttributeModifier(50f, ModifierOperation.Add));
            asc.ApplyEffect(CreateInstantEffect(health, 200f));

            Assert.That(state.MaxValue, Is.EqualTo(150f), "o máximo não seguiu a fonte");
            Assert.That(state.BaseValue, Is.EqualTo(150f));
            Assert.That(state.CurrentValue, Is.EqualTo(150f));
        }

        [TestCase(MaxChangePolicy.KeepValue, 80f)]
        [TestCase(MaxChangePolicy.KeepRatio, 120f)]
        [TestCase(MaxChangePolicy.AddDelta, 130f)]
        public void KeepValue80_KeepRatio120_AddDelta130(MaxChangePolicy policy, float expected)
        {
            AttributeDefinition maxHealth = CreateAttribute("MaxHealth", 0f, 9999f);
            AttributeDefinition health = CreateAttribute("Health", 0f, 100f);
            BindMax(health, maxHealth);
            health.OnMaxChange = policy;
            AbilitySystemComponent asc = CreateManualAsc((maxHealth, 100f), (health, 80f));
            AttributeState state = GetState(asc, health);

            GetState(asc, maxHealth).AddModifier(new AttributeModifier(50f, ModifierOperation.Add));

            Assert.That(state.BaseValue, Is.EqualTo(expected).Within(1e-4f));
            Assert.That(state.CurrentValue, Is.EqualTo(expected).Within(1e-4f));
        }

        [TestCase(MaxChangePolicy.KeepValue)]
        [TestCase(MaxChangePolicy.KeepRatio)]
        [TestCase(MaxChangePolicy.AddDelta)]
        public void MaxDropsBelowValue_ClampedAllPolicies_SingleNotification(MaxChangePolicy policy)
        {
            // O máximo constante (reserva) é 100: o valor inicial 120 só cabe porque a fonte vale 150
            AttributeDefinition maxHealth = CreateAttribute("MaxHealth", 0f, 9999f);
            AttributeDefinition health = CreateAttribute("Health", 0f, 100f);
            BindMax(health, maxHealth);
            health.OnMaxChange = policy;
            AbilitySystemComponent asc = CreateManualAsc((maxHealth, 150f), (health, 120f));
            AttributeState state = GetState(asc, health);
            float initial = state.CurrentValue;
            int notifications = 0;
            state.OnValueChanged += (oldValue, newValue) => notifications++;

            GetState(asc, maxHealth).SetBaseValue(100f);

            Assert.That(initial, Is.EqualTo(120f), "o valor inicial foi limitado pela constante e não pela fonte");
            Assert.That(state.BaseValue, Is.EqualTo(100f));
            Assert.That(state.CurrentValue, Is.EqualTo(100f));
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void MinFromAttribute_NeverBelow()
        {
            AttributeDefinition floor = CreateAttribute("Floor", 0f, 9999f);
            AttributeDefinition health = CreateAttribute("Health", 0f, 100f);
            health.MinSource = LimitSource.Attribute;
            health.MinAttribute = floor;
            AbilitySystemComponent asc = CreateManualAsc((floor, 20f), (health, 50f));
            AttributeState state = GetState(asc, health);

            asc.ApplyEffect(CreateInstantEffect(health, -100f));
            float afterDamage = state.CurrentValue;
            GetState(asc, floor).SetBaseValue(30f);

            Assert.That(afterDamage, Is.EqualTo(20f));
            Assert.That(state.MinValue, Is.EqualTo(30f));
            Assert.That(state.BaseValue, Is.EqualTo(30f));
            Assert.That(state.CurrentValue, Is.EqualTo(30f));
        }

        [Test]
        public void ConstantLimits_SameAsBefore()
        {
            AttributeDefinition health = CreateAttribute("Health", 0f, 100f);
            AbilitySystemComponent asc = CreateManualAsc((health, 50f));
            AttributeState state = GetState(asc, health);

            asc.ApplyEffect(CreateInstantEffect(health, 80f));
            float afterHeal = state.CurrentValue;
            asc.ApplyEffect(CreateInstantEffect(health, -500f));

            Assert.That(health.MinSource, Is.EqualTo(LimitSource.Constant));
            Assert.That(health.MaxSource, Is.EqualTo(LimitSource.Constant));
            Assert.That(health.OnMaxChange, Is.EqualTo(MaxChangePolicy.KeepValue));
            Assert.That(state.MinValue, Is.EqualTo(0f));
            Assert.That(state.MaxValue, Is.EqualTo(100f));
            Assert.That(afterHeal, Is.EqualTo(100f));
            Assert.That(state.CurrentValue, Is.EqualTo(0f));
        }

        [Test]
        public void MissingSource_FallsBackToConstant_OneWarning()
        {
            AttributeDefinition maxHealth = CreateAttribute("MaxHealth_Missing", 0f, 9999f);
            AttributeDefinition health = CreateAttribute("Health_Missing", 0f, 100f);
            BindMax(health, maxHealth);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[GAS\] .*'MaxHealth_Missing'.*'Health_Missing'"));

            AbilitySystemComponent asc = CreateManualAsc((health, 80f));
            AttributeState state = GetState(asc, health);
            asc.ApplyEffect(CreateInstantEffect(health, 200f));
            asc.ApplyEffect(CreateInstantEffect(health, 200f));

            Assert.That(state.MaxValue, Is.EqualTo(100f));
            Assert.That(state.CurrentValue, Is.EqualTo(100f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CycleAB_NoInfiniteLoop_OneWarning()
        {
            AttributeDefinition a = CreateAttribute("Cycle_A", 0f, 100f);
            AttributeDefinition b = CreateAttribute("Cycle_B", 0f, 100f);
            BindMax(a, b);
            BindMax(b, a);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[GAS\] Ciclo .*'Cycle_B'"));

            AbilitySystemComponent asc = CreateManualAsc((a, 50f), (b, 50f));
            AttributeState stateA = GetState(asc, a);
            AttributeState stateB = GetState(asc, b);
            stateB.SetBaseValue(90f);
            stateA.SetBaseValue(80f);

            // A aresta B → A fecha o ciclo: B usa a constante e A segue B
            Assert.That(stateB.MaxValue, Is.EqualTo(100f));
            Assert.That(stateA.MaxValue, Is.EqualTo(90f));
            Assert.That(stateA.CurrentValue, Is.EqualTo(80f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void MinAboveMax_UsesMin()
        {
            AttributeDefinition floor = CreateAttribute("Floor", 0f, 9999f);
            AttributeDefinition health = CreateAttribute("Health", 0f, 30f);
            health.MinSource = LimitSource.Attribute;
            health.MinAttribute = floor;
            AbilitySystemComponent asc = CreateManualAsc((floor, 50f), (health, 50f));
            AttributeState state = GetState(asc, health);

            asc.ApplyEffect(CreateInstantEffect(health, -10f));
            float afterDamage = state.CurrentValue;
            asc.ApplyEffect(CreateInstantEffect(health, 10f));

            Assert.That(afterDamage, Is.EqualTo(50f));
            Assert.That(state.BaseValue, Is.EqualTo(50f));
            Assert.That(state.CurrentValue, Is.EqualTo(50f));
        }

        [Test]
        public void SourceChange_UpdatesWithoutTick()
        {
            AttributeDefinition maxHealth = CreateAttribute("MaxHealth", 0f, 9999f);
            AttributeDefinition health = CreateAttribute("Health", 0f, 100f);
            BindMax(health, maxHealth);
            health.OnMaxChange = MaxChangePolicy.KeepRatio;
            AbilitySystemComponent asc = CreateManualAsc((maxHealth, 100f), (health, 100f));
            AttributeState state = GetState(asc, health);

            GetState(asc, maxHealth).SetBaseValue(200f);

            Assert.That(state.MaxValue, Is.EqualTo(200f));
            Assert.That(state.CurrentValue, Is.EqualTo(200f));
        }

        private AttributeDefinition CreateAttribute(string name, float min, float max)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = min;
            definition.MaxValue = max;
            return definition;
        }

        private static void BindMax(AttributeDefinition definition, AttributeDefinition source)
        {
            definition.MaxSource = LimitSource.Attribute;
            definition.MaxAttribute = source;
        }

        private GameplayEffect CreateInstantEffect(AttributeDefinition attribute, float value)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(value) },
            });
            return effect;
        }

        private AbilitySystemComponent CreateManualAsc(params (AttributeDefinition Definition, float InitialValue)[] attributes)
        {
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        private static AttributeState GetState(AbilitySystemComponent asc, AttributeDefinition definition)
        {
            Assert.That(asc.AttributeSet.TryGetState(definition, out AttributeState state), Is.True);
            return state;
        }
    }
}
#endif
