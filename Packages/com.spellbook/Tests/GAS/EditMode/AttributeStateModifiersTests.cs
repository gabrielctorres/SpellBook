using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary>Lista pública de modificadores do atributo (B-003, EA1; P7 da Spec).</summary>
    public class AttributeStateModifiersTests
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
        public void Modifiers_ReflectAddAndRemove()
        {
            AttributeDefinition definition = _gas.CreateAttribute("Health");
            definition.MinValue = 0f;
            definition.MaxValue = 100f;
            var state = new AttributeState(definition, 10f, null);
            var add = new AttributeModifier(5f, ModifierOperation.Add);
            var multiply = new AttributeModifier(0.5f, ModifierOperation.Multiply);

            state.AddModifier(add);
            state.AddModifier(multiply);
            int countAfterAdd = state.Modifiers.Count;
            state.RemoveModifier(add);

            Assert.That(countAfterAdd, Is.EqualTo(2));
            Assert.That(state.Modifiers.Count, Is.EqualTo(1));
            Assert.That(state.Modifiers[0], Is.EqualTo(multiply));
        }
    }
}
