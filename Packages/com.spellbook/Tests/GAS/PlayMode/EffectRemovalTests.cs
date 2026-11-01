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
    /// <summary>Remoção idempotente de efeitos ativos (B-002, NC3: CA9 a CA11).</summary>
    public class EffectRemovalTests
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
        public void RemoveTwice_RunsOnRemovalOnce_TagKeptBySecondEffect()
        {
            GameplayTag burning = _gas.CreateTag("State.Burning");
            AbilitySystemComponent asc = CreateManualAsc();
            var removal = new CountingAction();
            GameplayEffect first = _gas.CreateDurationEffect(10f, burning);
            first.OnRemoval.Add(removal);
            GameplayEffect second = _gas.CreateDurationEffect(10f, burning);
            ActiveEffectInstance firstInstance = asc.ApplyEffect(first);
            ActiveEffectInstance secondInstance = asc.ApplyEffect(second);

            asc.RemoveEffect(firstInstance);
            asc.RemoveEffect(firstInstance);
            bool tagWhileSecondActive = asc.HasTag(burning);
            asc.RemoveEffect(secondInstance);

            Assert.That(removal.Count, Is.EqualTo(1), "a OnRemoval rodou mais de uma vez");
            Assert.That(tagWhileSecondActive, Is.True, "a segunda remoção devolveu a tag do outro efeito");
            Assert.That(asc.HasTag(burning), Is.False, "a contagem da tag ficou errada");
        }

        [Test]
        public void RemoveNull_DoesNothing()
        {
            GameplayTag burning = _gas.CreateTag("State.Burning");
            AttributeDefinition armor = _gas.CreateAttribute("Armor");
            AbilitySystemComponent asc = CreateManualAsc((armor, 10f));
            asc.ApplyEffect(CreateArmorBuff(armor, 5f, burning));

            Assert.DoesNotThrow(() => asc.RemoveEffect(null));

            Assert.That(asc.HasTag(burning), Is.True);
            Assert.That(asc.GetAttributeValue(armor), Is.EqualTo(15f));
        }

        [Test]
        public void RemoveInstanceFromOtherAsc_DoesNothing()
        {
            GameplayTag burning = _gas.CreateTag("State.Burning");
            AttributeDefinition armor = _gas.CreateAttribute("Armor");
            AbilitySystemComponent asc = CreateManualAsc((armor, 10f));
            AbilitySystemComponent other = CreateManualAsc((armor, 10f));
            var removal = new CountingAction();
            GameplayEffect effect = CreateArmorBuff(armor, 5f, burning);
            effect.OnRemoval.Add(removal);
            asc.ApplyEffect(effect);
            ActiveEffectInstance otherInstance = other.ApplyEffect(effect);

            Assert.DoesNotThrow(() => asc.RemoveEffect(otherInstance));

            Assert.That(removal.Count, Is.EqualTo(0), "a OnRemoval rodou para uma instância de outro ASC");
            Assert.That(asc.HasTag(burning), Is.True);
            Assert.That(asc.GetAttributeValue(armor), Is.EqualTo(15f));
            Assert.That(other.HasTag(burning), Is.True);
            Assert.That(other.GetAttributeValue(armor), Is.EqualTo(15f));
        }

        [Test]
        public void RemoveInSameFrameAsExpiry_CleansUpOnce([Values] bool removeBeforeTick)
        {
            GameplayTag burning = _gas.CreateTag("State.Burning");
            AbilitySystemComponent asc = CreateManualAsc();
            var removal = new CountingAction();
            GameplayEffect effect = _gas.CreateDurationEffect(1f, burning);
            effect.OnRemoval.Add(removal);
            ActiveEffectInstance instance = asc.ApplyEffect(effect);
            asc.AddTag(burning); // outra fonte da mesma tag: uma devolução a mais apareceria aqui

            if (removeBeforeTick)
            {
                asc.RemoveEffect(instance);
                asc.Tick(2f);
            }
            else
            {
                asc.Tick(2f);
                asc.RemoveEffect(instance);
            }

            Assert.That(removal.Count, Is.EqualTo(1), "a limpeza rodou mais de uma vez");
            Assert.That(asc.HasTag(burning), Is.True, "a tag foi devolvida duas vezes");
        }

        [Test]
        public void OnRemovalThatRemovesSameInstance_DoesNotReenter()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            var removal = new RemoveSelfAction();
            GameplayEffect effect = _gas.CreateDurationEffect(10f);
            effect.OnRemoval.Add(removal);
            ActiveEffectInstance instance = asc.ApplyEffect(effect);

            asc.RemoveEffect(instance);

            Assert.That(removal.Count, Is.EqualTo(1), "a remoção reentrou na mesma instância");
        }

        private GameplayEffect CreateArmorBuff(AttributeDefinition armor, float value, params GameplayTag[] grantedTags)
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

        /// <summary>Conta quantas vezes rodou.</summary>
        private sealed class CountingAction : EffectAction
        {
            public int Count { get; private set; }

            public override void Execute(AbilitySystemComponent source, AbilitySystemComponent target, ActiveEffectInstance instance = null)
            {
                Count++;
            }
        }

        /// <summary>
        /// Remove a própria instância ao rodar. Para de chamar a remoção depois da segunda execução,
        /// para que a reentrância apareça como contagem e não como estouro de pilha.
        /// </summary>
        private sealed class RemoveSelfAction : EffectAction
        {
            private const int ReentryLimit = 2;

            public int Count { get; private set; }

            public override void Execute(AbilitySystemComponent source, AbilitySystemComponent target, ActiveEffectInstance instance = null)
            {
                Count++;
                if (Count < ReentryLimit) target.RemoveEffect(instance);
            }
        }
    }
}
#endif
