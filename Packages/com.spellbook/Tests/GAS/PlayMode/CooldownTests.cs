#if UNITY_EDITOR
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>Recarga normalizada pela duração aplicada e casamento hierárquico de tags (B-002, NC5: CA14 a CA17).</summary>
    public class CooldownTests
    {
        private const float AssetCooldown = 8f;
        private const float Reduction = 0.25f;
        private const float ReducedCooldown = 6f;

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
        public void Normalized_WithReduction_StartsAtOne_HalfAtHalf_ZeroWhenReady()
        {
            AttributeDefinition cdr = _gas.CreateAttribute("CooldownReduction");
            AbilitySystemComponent asc = CreateManualAsc((cdr, Reduction));
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(AssetCooldown);
            ability.CooldownReductionAttribute = cdr;
            asc.GiveAbility(ability);
            Assert.That(asc.TryActivateAbility(ability, Vector3.zero), Is.EqualTo(AbilityActivationResult.Success));

            float atStart = asc.GetCooldownNormalized(ability.CooldownEffect);
            asc.Tick(ReducedCooldown / 2f);
            float atHalf = asc.GetCooldownNormalized(ability.CooldownEffect);
            asc.Tick(ReducedCooldown / 2f);
            float whenReady = asc.GetCooldownNormalized(ability.CooldownEffect);

            Assert.That(atStart, Is.EqualTo(1f), "logo após ativar com redução, o normalizado não é 1");
            Assert.That(atHalf, Is.EqualTo(0.5f));
            Assert.That(whenReady, Is.EqualTo(0f));
            Assert.That(asc.IsOnCooldown(ability.CooldownEffect), Is.False, "a recarga não terminou nos 6 s aplicados");
        }

        [Test]
        public void Normalized_ZeroAppliedDuration_ReturnsZero()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect cooldown = _gas.CreateDurationEffect(AssetCooldown);
            asc.ApplyEffect(cooldown, 0f);

            float normalized = asc.GetCooldownNormalized(cooldown);

            Assert.That(normalized, Is.EqualTo(0f));
        }

        [Test]
        public void Remaining_CountsChildTagOfCooldownTag_NotUnrelatedTag()
        {
            GameplayTag cooldownTag = _gas.CreateTag("Cooldown");
            GameplayTag fire = _gas.CreateTag("Cooldown.Fire", cooldownTag);
            GameplayTag unrelated = _gas.CreateTag("State.Hasted");
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect cooldown = _gas.CreateDurationEffect(AssetCooldown, cooldownTag);
            asc.ApplyEffect(_gas.CreateDurationEffect(5f, fire));
            asc.ApplyEffect(_gas.CreateDurationEffect(9f, unrelated));

            float remaining = asc.GetCooldownTimeRemaining(cooldown);

            Assert.That(remaining, Is.EqualTo(5f), "a tag filha não contou ou a tag sem parentesco contou");
            Assert.That(asc.IsOnCooldown(cooldown), Is.True);
        }

        [Test]
        public void Remaining_ParentTagGrantedByOther_DoesNotCount()
        {
            GameplayTag cooldownTag = _gas.CreateTag("Cooldown");
            GameplayTag fire = _gas.CreateTag("Cooldown.Fire", cooldownTag);
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect cooldown = _gas.CreateDurationEffect(AssetCooldown, fire);
            asc.ApplyEffect(_gas.CreateDurationEffect(5f, cooldownTag));

            float remaining = asc.GetCooldownTimeRemaining(cooldown);

            Assert.That(remaining, Is.EqualTo(0f), "a tag pai concedida por outro efeito contou como recarga");
        }

        [Test]
        public void Remaining_TwoMatches_ReturnsLongest()
        {
            GameplayTag cooldownTag = _gas.CreateTag("Cooldown");
            GameplayTag fire = _gas.CreateTag("Cooldown.Fire", cooldownTag);
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect cooldown = _gas.CreateDurationEffect(AssetCooldown, cooldownTag);
            asc.ApplyEffect(cooldown, 3f);
            asc.ApplyEffect(_gas.CreateDurationEffect(7f, fire));

            float remaining = asc.GetCooldownTimeRemaining(cooldown);

            Assert.That(remaining, Is.EqualTo(7f));
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
