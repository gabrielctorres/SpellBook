#if UNITY_EDITOR
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary>Renovação pela duração aplicada e reaplicação sem <c>CanStack</c> (B-002, NC4: CA12, CA13, CA25 a CA28).</summary>
    public class EffectRenewalTests
    {
        private const float AssetDuration = 8f;
        private const float AppliedDuration = 6f;
        private const float Elapsed = 4f;

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
        public void StackRefresh_ReturnsToAppliedDuration_NotAsset()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = CreateStackingEffect(maxStacks: 3);
            ActiveEffectInstance instance = asc.ApplyEffect(effect, AppliedDuration);
            asc.Tick(Elapsed);

            ActiveEffectInstance renewed = asc.ApplyEffect(effect, AppliedDuration);

            Assert.That(renewed, Is.SameAs(instance));
            Assert.That(renewed.TimeRemaining, Is.EqualTo(AppliedDuration));
        }

        [Test]
        public void StackRefresh_WithoutOverride_ReturnsToAssetDuration()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = CreateStackingEffect(maxStacks: 3);
            ActiveEffectInstance instance = asc.ApplyEffect(effect);
            asc.Tick(Elapsed);

            ActiveEffectInstance renewed = asc.ApplyEffect(effect);

            Assert.That(renewed, Is.SameAs(instance));
            Assert.That(renewed.TimeRemaining, Is.EqualTo(AssetDuration));
        }

        [Test]
        public void ReapplyWithoutCanStack_KeepsSingleInstance_RenewsToAppliedDuration()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = _gas.CreateDurationEffect(AssetDuration);
            ActiveEffectInstance instance = asc.ApplyEffect(effect, AppliedDuration);
            asc.Tick(Elapsed);

            ActiveEffectInstance reapplied = asc.ApplyEffect(effect, AppliedDuration);

            Assert.That(reapplied, Is.SameAs(instance), "a reaplicação sem CanStack criou outra instância");
            Assert.That(reapplied.TimeRemaining, Is.EqualTo(AppliedDuration));
            Assert.That(reapplied.CurrentStacks, Is.EqualTo(1));
        }

        [Test]
        public void ReapplyWithoutCanStack_TagCountOne_ModifierOnce_RemoveRestoresAll()
        {
            GameplayTag shielded = _gas.CreateTag("State.Shielded");
            AttributeDefinition armor = _gas.CreateAttribute("Armor");
            AbilitySystemComponent asc = CreateManualAsc((armor, 10f));
            GameplayEffect effect = CreateArmorBuff(armor, 5f, shielded);
            ActiveEffectInstance instance = asc.ApplyEffect(effect);

            asc.ApplyEffect(effect);
            float armorWhileActive = asc.GetAttributeValue(armor);
            asc.RemoveEffect(instance);

            Assert.That(armorWhileActive, Is.EqualTo(15f), "o modificador foi aplicado mais de uma vez");
            Assert.That(asc.HasTag(shielded), Is.False, "a tag foi somada na reaplicação");
            Assert.That(asc.GetAttributeValue(armor), Is.EqualTo(10f), "sobrou modificador depois da remoção");
        }

        [Test]
        public void ReapplyInfiniteWithoutCanStack_KeepsSingleInstance()
        {
            GameplayTag aura = _gas.CreateTag("State.Aura");
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f, aura);
            ActiveEffectInstance instance = asc.ApplyEffect(effect);

            ActiveEffectInstance reapplied = asc.ApplyEffect(effect);
            asc.RemoveEffect(instance);

            Assert.That(reapplied, Is.SameAs(instance), "a reaplicação do infinito criou outra instância");
            Assert.That(asc.HasTag(aura), Is.False, "sobrou uma segunda instância concedendo a tag");
        }

        [Test]
        public void ReapplyWithCanStack_StillStacksUpToMax()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = CreateStackingEffect(maxStacks: 3);
            ActiveEffectInstance instance = asc.ApplyEffect(effect);

            for (int i = 0; i < 4; i++) asc.ApplyEffect(effect);

            Assert.That(instance.CurrentStacks, Is.EqualTo(3));
            Assert.That(asc.ApplyEffect(effect), Is.SameAs(instance));
        }

        [Test]
        public void ReapplyWithoutCanStack_DoesNotRunOnApplicationAgain()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            var application = new CountingAction();
            GameplayEffect effect = _gas.CreateDurationEffect(AssetDuration);
            effect.OnApplication.Add(application);
            asc.ApplyEffect(effect);

            asc.ApplyEffect(effect);

            Assert.That(application.Count, Is.EqualTo(1), "a OnApplication rodou de novo na renovação");
        }

        private GameplayEffect CreateStackingEffect(int maxStacks)
        {
            GameplayEffect effect = _gas.CreateDurationEffect(AssetDuration);
            effect.CanStack = true;
            effect.MaxStacks = maxStacks;
            effect.RefreshDurationOnStack = true;
            return effect;
        }

        private GameplayEffect CreateArmorBuff(AttributeDefinition armor, float value, params GameplayTag[] grantedTags)
        {
            GameplayEffect effect = _gas.CreateDurationEffect(AssetDuration, grantedTags);
            effect.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = armor,
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

        /// <summary>Conta quantas vezes rodou.</summary>
        private sealed class CountingAction : EffectAction
        {
            public int Count { get; private set; }

            public override void Execute(AbilitySystemComponent source, AbilitySystemComponent target, ActiveEffectInstance instance = null)
            {
                Count++;
            }
        }
    }
}
#endif
