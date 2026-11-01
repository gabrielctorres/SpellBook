using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.Tags;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary>As sobrecargas de <see cref="GasFactory.CreateAbility(AbilityAction[])"/> com e sem tag (RP-04).</summary>
    public class GasFactoryTests
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
        public void CreateAbility_ActionsOnly_HasNoTagAndKeepsActions()
        {
            var a = new WaitAction();
            var b = new WaitAction();

            GameplayAbility ability = _gas.CreateAbility(a, b);

            Assert.That(ability.AbilityTag.IsValid, Is.False);
            Assert.That(ability.Actions, Is.EqualTo(new AbilityAction[] { a, b }));
        }

        [Test]
        public void CreateAbility_WithTag_KeepsTagAndActions()
        {
            GameplayTag tag = _gas.CreateTag("Ability.Test");
            var a = new WaitAction();

            GameplayAbility ability = _gas.CreateAbility(tag, a);

            Assert.That(ability.AbilityTag, Is.EqualTo(tag));
            Assert.That(ability.Actions, Is.EqualTo(new AbilityAction[] { a }));
        }
    }
}
