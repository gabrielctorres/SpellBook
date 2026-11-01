using System;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Ordem única de composição no recálculo (B-005, AR3, ADR 0029: CA15 a CA18 e CA20).</summary>
    public class ModifierCompositionTests
    {
        private const float Tolerance = 1e-4f;

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
        public void ExampleC7_Is242_6()
        {
            AttributeState state = CreateState(100f);

            state.AddModifier(new AttributeModifier(20f, ModifierOperation.Add));
            state.AddModifier(new AttributeModifier(0.5f, ModifierOperation.Multiply));
            state.AddModifier(new AttributeModifier(0.1f, ModifierOperation.MultiplyCompound));
            state.AddModifier(new AttributeModifier(0.2f, ModifierOperation.MultiplyCompound));
            state.AddModifier(new AttributeModifier(5f, ModifierOperation.AddFinal));

            Assert.That(state.CurrentValue, Is.EqualTo(242.6f).Within(Tolerance)); // ((100 + 20) x 1,5) x 1,1 x 1,2 + 5
        }

        [Test]
        public void OverrideAfterAddFinal_ThenLimit()
        {
            AttributeState state = CreateState(100f);
            state.AddModifier(new AttributeModifier(20f, ModifierOperation.Add));
            state.AddModifier(new AttributeModifier(0.1f, ModifierOperation.MultiplyCompound));
            state.AddModifier(new AttributeModifier(5f, ModifierOperation.AddFinal));

            state.AddModifier(new AttributeModifier(50f, ModifierOperation.Override));
            float withOverride = state.CurrentValue;
            state.AddModifier(new AttributeModifier(5000f, ModifierOperation.Override));

            Assert.That(withOverride, Is.EqualTo(50f), "o Override não venceu o AddFinal");
            Assert.That(state.CurrentValue, Is.EqualTo(1000f), "o limite não veio depois do Override");
        }

        [Test]
        public void WithoutNewOperations_MatchesB002SequenceBitForBit()
        {
            // Valores em que a ordem das somas muda o resultado em ponto flutuante
            const float baseValue = 0.1f;
            float[] adds = { 0.1f, 0.7f, 0.3f };
            float[] multiplies = { 0.1f, 0.2f, 0.3f };
            AttributeState state = CreateState(baseValue);
            for (int i = 0; i < adds.Length; i++)
            {
                state.AddModifier(new AttributeModifier(adds[i], ModifierOperation.Add));
                state.AddModifier(new AttributeModifier(multiplies[i], ModifierOperation.Multiply));
            }

            // A sequência do B-002: base somada na ordem da lista e multiplicador partindo de 1
            float expected = baseValue;
            float multiplierSum = 1f;
            for (int i = 0; i < adds.Length; i++)
            {
                expected += adds[i];
                multiplierSum += multiplies[i];
            }
            expected *= multiplierSum;

            Assert.That(state.CurrentValue, Is.EqualTo(expected));
        }

        [Test]
        public void Enum_NewValuesAtEnd()
        {
            Assert.That((int)ModifierOperation.Add, Is.EqualTo(0));
            Assert.That((int)ModifierOperation.Multiply, Is.EqualTo(1));
            Assert.That((int)ModifierOperation.Override, Is.EqualTo(2));
            Assert.That((int)ModifierOperation.MultiplyCompound, Is.EqualTo(3));
            Assert.That((int)ModifierOperation.AddFinal, Is.EqualTo(4));
            Assert.That(Enum.GetValues(typeof(ModifierOperation)).Length, Is.EqualTo(5));
        }

        [TestCase(0, ModifierOperation.Add)]
        [TestCase(1, ModifierOperation.Multiply)]
        [TestCase(2, ModifierOperation.Override)]
        public void SerializedOldOperation_SameValue(int serialized, ModifierOperation expected)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);

            JsonUtility.FromJsonOverwrite("{\"Modifiers\":[{\"Operation\":" + serialized + "}]}", effect);

            Assert.That(effect.Modifiers[0].Operation, Is.EqualTo(expected));
        }

        [Test]
        public void Recalculate_FourOperations_DoesNotAllocate_1000Times()
        {
            AttributeState state = CreateState(100f);
            state.AddModifier(new AttributeModifier(20f, ModifierOperation.Add));
            state.AddModifier(new AttributeModifier(0.5f, ModifierOperation.Multiply));
            state.AddModifier(new AttributeModifier(0.1f, ModifierOperation.MultiplyCompound));
            state.AddModifier(new AttributeModifier(5f, ModifierOperation.AddFinal));
            state.Recalculate(); // aquecimento

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++) state.Recalculate();
            }, Is.Not.AllocatingGCMemory());
            Assert.That(state.CurrentValue, Is.EqualTo(203f).Within(Tolerance), "o recálculo não usou as quatro operações");
        }

        /// <summary>Atributo <c>Primary</c> em [0, 1000], sem dono (EditMode, sem cena).</summary>
        private AttributeState CreateState(float baseValue)
        {
            AttributeDefinition definition = _gas.CreateAttribute("Damage");
            definition.MinValue = 0f;
            definition.MaxValue = 1000f;
            return new AttributeState(definition, baseValue, null);
        }
    }
}
