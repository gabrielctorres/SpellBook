#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Eventos de efeito do ASC: aplicado, pilhas e removido com motivo (B-003, EA3: CA12 a CA15, R10, N1).</summary>
    public class EffectEventsTests
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
        public void Applied_FiresOnce()
        {
            GameplayTag burn = _gas.CreateTag("Status.Burn");
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = _gas.CreateDurationEffect(5f, burn);
            var applied = new List<ActiveEffectInstance>();
            bool tagGrantedWhenFired = false;
            asc.OnEffectApplied += i =>
            {
                applied.Add(i);
                tagGrantedWhenFired = asc.HasTag(burn);
            };

            ActiveEffectInstance instance = asc.ApplyEffect(effect);
            asc.ApplyEffect(effect); // renovação, sem CanStack

            Assert.That(applied, Is.EqualTo(new[] { instance }), "a renovação disparou OnEffectApplied de novo");
            Assert.That(tagGrantedWhenFired, Is.True, "o evento disparou antes das tags");
        }

        [Test]
        public void Stacked_FiresStacksChanged_1To2()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = CreateStackingEffect(3);
            var changes = new List<(ActiveEffectInstance, int, int)>();
            int appliedCount = 0;
            asc.OnEffectApplied += _ => appliedCount++;
            asc.OnEffectStacksChanged += (i, before, after) => changes.Add((i, before, after));

            ActiveEffectInstance instance = asc.ApplyEffect(effect);
            asc.ApplyEffect(effect);

            Assert.That(changes, Is.EqualTo(new[] { (instance, 1, 2) }));
            Assert.That(appliedCount, Is.EqualTo(1), "empilhar disparou OnEffectApplied");
        }

        [Test]
        public void StackAtMax_DoesNotFire()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = CreateStackingEffect(2);
            asc.ApplyEffect(effect);
            asc.ApplyEffect(effect);
            int fired = 0;
            asc.OnEffectStacksChanged += (_, _, _) => fired++;

            asc.ApplyEffect(effect);

            Assert.That(fired, Is.EqualTo(0));
        }

        [Test]
        public void Removed_Expired()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(_gas.CreateDurationEffect(1f));
            var removed = RecordRemoved(asc);

            asc.Tick(2f);

            Assert.That(removed, Is.EqualTo(new[] { (instance, EffectRemovalReason.Expired) }));
        }

        [Test]
        public void Removed_Removed()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(_gas.CreateDurationEffect(5f));
            var removed = RecordRemoved(asc);

            asc.RemoveEffect(instance);

            Assert.That(removed, Is.EqualTo(new[] { (instance, EffectRemovalReason.Removed) }));
        }

        [Test]
        public void Removed_RemovedByTag()
        {
            GameplayTag burn = _gas.CreateTag("Status.Burn");
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(_gas.CreateDurationEffect(5f, burn));
            var removed = RecordRemoved(asc);

            asc.RemoveEffectsWithTag(burn);

            Assert.That(removed, Is.EqualTo(new[] { (instance, EffectRemovalReason.RemovedByTag) }));
        }

        [Test]
        public void RemovedEvent_TagsAlreadyReturned()
        {
            GameplayTag burn = _gas.CreateTag("Status.Burn");
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(_gas.CreateDurationEffect(5f, burn));
            bool? tagWhenFired = null;
            bool? activeWhenFired = null;
            asc.OnEffectRemoved += (i, _) =>
            {
                tagWhenFired = asc.HasTag(burn);
                activeWhenFired = i.IsActive;
            };

            asc.RemoveEffect(instance);

            Assert.That(tagWhenFired, Is.False, "a tag ainda estava no ASC dentro do evento");
            Assert.That(activeWhenFired, Is.False);
        }

        [Test]
        public void ListenerRemovesOtherEffect_NoExceptionNoSkippedEvent()
        {
            GameplayTag burn = _gas.CreateTag("Status.Burn");
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance first = asc.ApplyEffect(_gas.CreateDurationEffect(5f, burn));
            ActiveEffectInstance second = asc.ApplyEffect(_gas.CreateDurationEffect(5f, burn));
            ActiveEffectInstance third = asc.ApplyEffect(_gas.CreateDurationEffect(5f, burn));
            var removed = RecordRemoved(asc);
            asc.OnEffectRemoved += (i, _) =>
            {
                if (i == third) asc.RemoveEffect(second);
            };

            int count = 0;
            Assert.DoesNotThrow(() => count = asc.RemoveEffectsWithTag(burn));

            Assert.That(removed, Is.EqualTo(new[]
            {
                (third, EffectRemovalReason.RemovedByTag),
                (second, EffectRemovalReason.Removed),
                (first, EffectRemovalReason.RemovedByTag),
            }));
            Assert.That(count, Is.EqualTo(2), "contou a instância que o ouvinte removeu");
            Assert.That(asc.ActiveEffects, Is.Empty);
        }

        [Test]
        public void InstantEffect_DoesNotFireApplied()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            int fired = 0;
            asc.OnEffectApplied += _ => fired++;

            asc.ApplyEffect(_gas.CreateEffect(EffectDurationPolicy.Instant, 0f));

            Assert.That(fired, Is.EqualTo(0));
        }

        [Test]
        public void RemoveTwice_FiresRemovedOnce()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(_gas.CreateDurationEffect(5f));
            var removed = RecordRemoved(asc);

            asc.RemoveEffect(instance);
            asc.RemoveEffect(instance);

            Assert.That(removed.Count, Is.EqualTo(1));
        }

        [Test]
        public void Events_WithoutListeners_DoNotAllocate()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = CreateStackingEffect(int.MaxValue);
            // Sem requisitos nem imunidades: a reaplicação não percorre listas por IEnumerable
            effect.ApplicationRequirements = null;
            effect.ApplicationImmunityTags = null;
            ActiveEffectInstance instance = asc.ApplyEffect(effect);
            for (int i = 0; i < 10; i++) asc.ApplyEffect(effect); // aquecimento

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++) asc.ApplyEffect(effect);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(instance.CurrentStacks, Is.EqualTo(10011), "a reaplicação não passou pelo empilhamento");
        }

        private GameplayEffect CreateStackingEffect(int maxStacks)
        {
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            effect.CanStack = true;
            effect.MaxStacks = maxStacks;
            return effect;
        }

        private static List<(ActiveEffectInstance, EffectRemovalReason)> RecordRemoved(AbilitySystemComponent asc)
        {
            var removed = new List<(ActiveEffectInstance, EffectRemovalReason)>();
            asc.OnEffectRemoved += (i, reason) => removed.Add((i, reason));
            return removed;
        }

        private AbilitySystemComponent CreateManualAsc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }
    }
}
#endif
