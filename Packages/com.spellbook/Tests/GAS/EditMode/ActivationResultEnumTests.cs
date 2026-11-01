using NUnit.Framework;
using SpellBook.GAS.Abilities;

namespace SpellBook.GAS.Tests
{
    /// <summary>Valores serializados do <see cref="AbilityActivationResult"/> (AV4).</summary>
    public class ActivationResultEnumTests
    {
        [Test]
        public void Enum_NewValuesAtEnd_OldIntSame()
        {
            Assert.That((int)AbilityActivationResult.Success, Is.EqualTo(0));
            Assert.That((int)AbilityActivationResult.InvalidAbility, Is.EqualTo(1));
            Assert.That((int)AbilityActivationResult.NotGranted, Is.EqualTo(2));
            Assert.That((int)AbilityActivationResult.AlreadyActive, Is.EqualTo(3));
            Assert.That((int)AbilityActivationResult.BlockedByTags, Is.EqualTo(4));
            Assert.That((int)AbilityActivationResult.MissingRequiredTags, Is.EqualTo(5));
            Assert.That((int)AbilityActivationResult.BlockedByActiveAbility, Is.EqualTo(6));
            Assert.That((int)AbilityActivationResult.OnCooldown, Is.EqualTo(7));
            Assert.That((int)AbilityActivationResult.CannotAffordCost, Is.EqualTo(8));
            Assert.That((int)AbilityActivationResult.BlockedByCondition, Is.EqualTo(9));
            Assert.That((int)AbilityActivationResult.NoCharges, Is.EqualTo(10));
        }
    }
}
