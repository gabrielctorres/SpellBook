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
    /// <summary>Remoção de efeitos ativos por tag e por asset (B-003, EA3: CA5 e CA6).</summary>
    public class EffectRemoveByTagTests
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
        public void RemoveByTag_ReturnsStacks_ReturnsTagsAndModifiers_SecondCallZero()
        {
            GameplayTag status = _gas.CreateTag("Status");
            GameplayTag poison = _gas.CreateTag("Status.Poison", status);
            AttributeDefinition armor = _gas.CreateAttribute("Armor");
            AbilitySystemComponent asc = CreateManualAsc((armor, 10f));
            GameplayEffect poisoned = CreateArmorEffect(armor, -2f, poison);
            poisoned.CanStack = true;
            poisoned.MaxStacks = 5;
            for (int i = 0; i < 3; i++) asc.ApplyEffect(poisoned);

            int removed = asc.RemoveEffectsWithTag(poison);
            int removedAgain = asc.RemoveEffectsWithTag(poison);

            Assert.That(removed, Is.EqualTo(3), "devolveu as instâncias, não as pilhas removidas");
            Assert.That(removedAgain, Is.EqualTo(0));
            Assert.That(asc.HasTag(poison), Is.False, "a tag do efeito ficou no ASC");
            Assert.That(asc.GetAttributeValue(armor), Is.EqualTo(10f), "o modificador do efeito não foi devolvido");
            Assert.That(asc.ActiveEffects, Is.Empty);
        }

        [Test]
        public void RemoveByTag_ParentRemovesChildren_KeepsOthers()
        {
            GameplayTag status = _gas.CreateTag("Status");
            GameplayTag burn = _gas.CreateTag("Status.Burn", status);
            GameplayTag poison = _gas.CreateTag("Status.Poison", status);
            GameplayTag hasted = _gas.CreateTag("State.Hasted");
            AbilitySystemComponent asc = CreateManualAsc();
            asc.ApplyEffect(_gas.CreateDurationEffect(5f, burn));
            asc.ApplyEffect(_gas.CreateDurationEffect(5f, poison));
            ActiveEffectInstance haste = asc.ApplyEffect(_gas.CreateDurationEffect(5f, hasted));

            int removed = asc.RemoveEffectsWithTag(status);

            Assert.That(removed, Is.EqualTo(2));
            Assert.That(asc.ActiveEffects, Is.EqualTo(new[] { haste }));
        }

        [Test]
        public void RemoveByTag_InvalidTag_Zero()
        {
            GameplayTag burn = _gas.CreateTag("Status.Burn");
            AbilitySystemComponent asc = CreateManualAsc();
            asc.ApplyEffect(_gas.CreateDurationEffect(5f, burn));

            int removed = asc.RemoveEffectsWithTag(default);

            Assert.That(removed, Is.EqualTo(0));
            Assert.That(asc.ActiveEffects.Count, Is.EqualTo(1));
        }

        [Test]
        public void RemoveByEffect_OnlyThatAsset()
        {
            GameplayTag burn = _gas.CreateTag("Status.Burn");
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect target = _gas.CreateDurationEffect(5f, burn);
            target.CanStack = true;
            target.MaxStacks = 3;
            ActiveEffectInstance other = asc.ApplyEffect(_gas.CreateDurationEffect(5f, burn));
            asc.ApplyEffect(target);
            asc.ApplyEffect(target);

            int removed = asc.RemoveEffects(target);
            int removedNull = asc.RemoveEffects(null);

            Assert.That(removed, Is.EqualTo(2), "devolveu outra contagem que não as pilhas do asset");
            Assert.That(removedNull, Is.EqualTo(0));
            Assert.That(asc.ActiveEffects, Is.EqualTo(new[] { other }), "removeu um efeito de outro asset com a mesma tag");
            Assert.That(asc.HasTag(burn), Is.True, "a tag do outro efeito foi devolvida junto");
        }

        private GameplayEffect CreateArmorEffect(AttributeDefinition armor, float value, params GameplayTag[] grantedTags)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f, grantedTags);
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
    }
}
#endif
