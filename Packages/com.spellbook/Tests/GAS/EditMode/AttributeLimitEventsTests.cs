using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.Testing;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary><c>OnDepleted</c> e <c>OnReachedMax</c> no <c>AttributeState</c> sem dono (B-005, AR2: CA10 a CA12, CA14, N1).</summary>
    public class AttributeLimitEventsTests
    {
        private GasFactory _gas;
        private int _depleted;
        private int _reachedMax;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _depleted = 0;
            _reachedMax = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
        }

        [Test]
        public void Depleted_OnTransition_NotRepeated_AgainAfterHeal()
        {
            AttributeState health = CreateCountedState(50f);

            health.SetBaseValue(-10f);
            int afterFirstDamage = _depleted;
            health.SetBaseValue(-20f);
            health.Recalculate();
            int afterMoreDamage = _depleted;
            health.SetBaseValue(30f);
            health.SetBaseValue(0f);

            Assert.That(afterFirstDamage, Is.EqualTo(1), "não disparou ao chegar ao mínimo");
            Assert.That(afterMoreDamage, Is.EqualTo(1), "repetiu no mínimo");
            Assert.That(_depleted, Is.EqualTo(2), "não disparou de novo depois de curar e depletar");
            Assert.That(_reachedMax, Is.EqualTo(0));
        }

        [Test]
        public void ReachedMax_OnTransition_NotRepeated()
        {
            AttributeState mana = CreateCountedState(90f);

            mana.SetBaseValue(95f);
            int beforeMax = _reachedMax;
            mana.SetBaseValue(100f);
            mana.SetBaseValue(105f);
            mana.AddModifier(new AttributeModifier(5f, ModifierOperation.Add));

            Assert.That(beforeMax, Is.EqualTo(0));
            Assert.That(_reachedMax, Is.EqualTo(1));
            Assert.That(_depleted, Is.EqualTo(0));
        }

        [TestCase(0f)]
        [TestCase(100f)]
        public void BornAtLimit_DoesNotFire(float initial)
        {
            AttributeState state = CreateCountedState(initial);

            state.Recalculate();
            state.SetBaseValue(initial);

            Assert.That(_depleted, Is.EqualTo(0));
            Assert.That(_reachedMax, Is.EqualTo(0));
        }

        [Test]
        public void Depleted_AfterValueChanged_ListenerChangingValue_NoException()
        {
            AttributeState health = CreateState(50f);
            var calls = new List<string>();
            health.OnValueChanged += (oldValue, newValue) => calls.Add($"changed {newValue}");
            health.OnDepleted += state =>
            {
                calls.Add($"depleted {state.CurrentValue}");
                state.SetBaseValue(40f); // o ouvinte cura dentro do evento
            };

            Assert.DoesNotThrow(() => health.SetBaseValue(0f));
            health.SetBaseValue(0f);

            Assert.That(calls, Is.EqualTo(new[]
            {
                "changed 0", "depleted 0", "changed 40",
                "changed 0", "depleted 0", "changed 40",
            }));
            Assert.That(health.CurrentValue, Is.EqualTo(40f));
        }

        [Test]
        public void LimitEvents_DoNotAllocate()
        {
            AttributeState state = CreateCountedState(50f);
            state.SetBaseValue(0f); // aquecimento dos dois eventos
            state.SetBaseValue(100f);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    state.SetBaseValue(0f);
                    state.SetBaseValue(100f);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(_depleted, Is.EqualTo(1001), "os eventos não dispararam na medição");
            Assert.That(_reachedMax, Is.EqualTo(1001));
        }

        /// <summary>Atributo <c>Primary</c> em [0, 100], sem dono (EditMode, sem cena).</summary>
        private AttributeState CreateState(float baseValue)
        {
            AttributeDefinition definition = _gas.CreateAttribute("Health");
            definition.MinValue = 0f;
            definition.MaxValue = 100f;
            return new AttributeState(definition, baseValue, null);
        }

        /// <summary>Como <see cref="CreateState"/>, com os dois eventos contados nos campos do teste.</summary>
        private AttributeState CreateCountedState(float baseValue)
        {
            AttributeState state = CreateState(baseValue);
            state.OnDepleted += CountDepleted;
            state.OnReachedMax += CountReachedMax;
            return state;
        }

        private void CountDepleted(AttributeState state) => _depleted++;

        private void CountReachedMax(AttributeState state) => _reachedMax++;
    }
}
