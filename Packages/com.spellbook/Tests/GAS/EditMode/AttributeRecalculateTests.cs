using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.Testing;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Recálculo do atributo: valores (guardas do CA6) e ausência de alocação (CA7). B-002, NC2.</summary>
    public class AttributeRecalculateTests
    {
        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
        }

        [Test]
        public void AddOnly_SumsOnBase()
        {
            AttributeState state = CreateState(10f);

            state.AddModifier(new AttributeModifier(5f, ModifierOperation.Add));
            state.AddModifier(new AttributeModifier(-2f, ModifierOperation.Add));

            Assert.That(state.CurrentValue, Is.EqualTo(13f));
        }

        [Test]
        public void AddAndMultiply_AddsFirstThenMultiplies()
        {
            AttributeState state = CreateState(10f);

            state.AddModifier(new AttributeModifier(0.5f, ModifierOperation.Multiply));
            state.AddModifier(new AttributeModifier(10f, ModifierOperation.Add));

            Assert.That(state.CurrentValue, Is.EqualTo(30f)); // (10 + 10) x 1,5
        }

        [Test]
        public void TwoMultiply_AreSummedNotCompounded()
        {
            AttributeState state = CreateState(10f);

            state.AddModifier(new AttributeModifier(0.5f, ModifierOperation.Multiply));
            state.AddModifier(new AttributeModifier(0.25f, ModifierOperation.Multiply));

            Assert.That(state.CurrentValue, Is.EqualTo(17.5f)); // 10 x (1 + 0,5 + 0,25)
        }

        [Test]
        public void TwoOverride_LastWins()
        {
            AttributeState state = CreateState(10f);

            state.AddModifier(new AttributeModifier(40f, ModifierOperation.Override));
            state.AddModifier(new AttributeModifier(5f, ModifierOperation.Add));
            state.AddModifier(new AttributeModifier(25f, ModifierOperation.Override));

            Assert.That(state.CurrentValue, Is.EqualTo(25f));
        }

        [Test]
        public void ResultAboveMax_IsClampedToMax()
        {
            AttributeState state = CreateState(80f);

            state.AddModifier(new AttributeModifier(1f, ModifierOperation.Multiply));

            Assert.That(state.CurrentValue, Is.EqualTo(100f)); // 160 sem limite
            Assert.That(state.BaseValue, Is.EqualTo(80f));
        }

        [Test]
        public void Recalculate_DoesNotAllocate_AfterWarmup()
        {
            AttributeState state = CreateStateWithAddAndMultiply();
            state.AddModifier(new AttributeModifier(50f, ModifierOperation.Override));
            state.Recalculate(); // aquecimento

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++) state.Recalculate();
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void AddRemoveModifier_DoesNotAllocate_AfterWarmup()
        {
            AttributeState state = CreateStateWithAddAndMultiply();
            var modifier = new AttributeModifier(3f, ModifierOperation.Add);
            state.AddModifier(modifier); // aquecimento: a lista cresce aqui, não na medição
            state.RemoveModifier(modifier);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    state.AddModifier(modifier);
                    state.RemoveModifier(modifier);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void RemoveModifiersFromSource_DoesNotAllocate()
        {
            AttributeState state = CreateStateWithAddAndMultiply();
            var source = new object();
            var first = new AttributeModifier(1f, ModifierOperation.Add, source);
            var second = new AttributeModifier(0.5f, ModifierOperation.Multiply, source);
            state.AddModifier(first);
            state.AddModifier(second);
            state.RemoveModifiersFromSource(source); // aquecimento
            state.AddModifier(first);
            state.AddModifier(second);

            Assert.That(() => state.RemoveModifiersFromSource(source), Is.Not.AllocatingGCMemory());
            Assert.That(state.CurrentValue, Is.EqualTo(32f), "os modificadores da fonte não saíram"); // (10 + 6) x 2
        }

        /// <summary>Atributo <c>Primary</c> em [0, 100], sem dono (EditMode, sem cena).</summary>
        private AttributeState CreateState(float baseValue)
        {
            AttributeDefinition definition = _gas.CreateAttribute("Health");
            definition.MinValue = 0f;
            definition.MaxValue = 100f;
            return new AttributeState(definition, baseValue, null);
        }

        /// <summary>Base 10 com um <c>Add</c> e um <c>Multiply</c>: (10 + 6) x 2 = 32.</summary>
        private AttributeState CreateStateWithAddAndMultiply()
        {
            AttributeState state = CreateState(10f);
            state.AddModifier(new AttributeModifier(6f, ModifierOperation.Add));
            state.AddModifier(new AttributeModifier(1f, ModifierOperation.Multiply));
            return state;
        }
    }
}
