#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.GAS.Tests
{
    /// <summary>Mutação de duração e de pilhas de efeitos ativos (B-003, EA4: CA7 a CA11 e StacksDepleted do CA12).</summary>
    public class EffectMutationTests
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
        public void Extend2_From5_Is7()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(_gas.CreateDurationEffect(5f));

            bool changed = asc.ExtendEffectDuration(instance, 2f);

            Assert.That(changed, Is.True);
            Assert.That(instance.TimeRemaining, Is.EqualTo(7f).Within(1e-4f));
            Assert.That(instance.AppliedDuration, Is.EqualTo(5f), "estender mudou a duração aplicada");
        }

        [Test]
        public void Reduce10_Expires_WithReasonExpired()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(_gas.CreateDurationEffect(5f));
            List<(ActiveEffectInstance, EffectRemovalReason)> removed = RecordRemoved(asc);

            bool changed = asc.ReduceEffectDuration(instance, 10f);

            Assert.That(changed, Is.True);
            Assert.That(instance.IsActive, Is.False, "não expirou no mesmo quadro");
            Assert.That(asc.ActiveEffects, Is.Empty);
            Assert.That(removed, Is.EqualTo(new[] { (instance, EffectRemovalReason.Expired) }));
        }

        [Test]
        public void NegativeOrNaN_ExtendReduce_NoChange()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(_gas.CreateDurationEffect(5f));
            var warning = new Regex(@"\[GAS\]");
            for (int i = 0; i < 4; i++) LogAssert.Expect(LogType.Warning, warning);

            bool extendNegative = asc.ExtendEffectDuration(instance, -1f);
            bool extendNaN = asc.ExtendEffectDuration(instance, float.NaN);
            bool reduceNegative = asc.ReduceEffectDuration(instance, -1f);
            bool reduceNaN = asc.ReduceEffectDuration(instance, float.NaN);

            Assert.That(new[] { extendNegative, extendNaN, reduceNegative, reduceNaN }, Is.All.False);
            Assert.That(instance.TimeRemaining, Is.EqualTo(5f));
            Assert.That(instance.IsActive, Is.True);
        }

        [Test]
        public void Restart_ReturnsToAppliedDuration_NotAsset()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(_gas.CreateDurationEffect(5f), 3f);
            asc.Tick(2f);

            bool changed = asc.RestartEffectDuration(instance);

            Assert.That(changed, Is.True);
            Assert.That(instance.TimeRemaining, Is.EqualTo(3f), "voltou à Duration do asset, não à aplicada");
            Assert.That(instance.AppliedDuration, Is.EqualTo(3f));
        }

        [Test]
        public void Infinite_IgnoresExtendReduceRestart()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(_gas.CreateEffect(EffectDurationPolicy.Infinite, 0f));
            float before = instance.TimeRemaining;

            bool extended = asc.ExtendEffectDuration(instance, 2f);
            bool reduced = asc.ReduceEffectDuration(instance, 2f);
            bool restarted = asc.RestartEffectDuration(instance);

            Assert.That(new[] { extended, reduced, restarted }, Is.All.False);
            Assert.That(instance.TimeRemaining, Is.EqualTo(before));
            Assert.That(instance.IsActive, Is.True);
        }

        [Test]
        public void SetStacks3_Of5_Is3()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = ApplyStacks(asc, CreateStackingEffect(5), 5);
            var changes = RecordStacks(asc);

            bool changed = asc.SetEffectStacks(instance, 3);
            bool repeated = asc.SetEffectStacks(instance, 3);

            Assert.That(changed, Is.True);
            Assert.That(repeated, Is.False, "a mesma contagem contou como mudança");
            Assert.That(instance.CurrentStacks, Is.EqualTo(3));
            Assert.That(changes, Is.EqualTo(new[] { (instance, 5, 3) }));
        }

        [Test]
        public void SetStacks9_ClampsTo5()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(CreateStackingEffect(5));
            var changes = RecordStacks(asc);

            bool changed = asc.SetEffectStacks(instance, 9);

            Assert.That(changed, Is.True);
            Assert.That(instance.CurrentStacks, Is.EqualTo(5));
            Assert.That(changes, Is.EqualTo(new[] { (instance, 1, 5) }));
        }

        [Test]
        public void RemoveStacks_ToZero_RemovesWithStacksDepleted()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = ApplyStacks(asc, CreateStackingEffect(5), 2);
            List<(ActiveEffectInstance, EffectRemovalReason)> removed = RecordRemoved(asc);

            bool partial = asc.RemoveEffectStacks(instance, 1);
            bool zeroCount = asc.RemoveEffectStacks(instance, 0);
            bool depleted = asc.RemoveEffectStacks(instance, 5);

            Assert.That(partial, Is.True);
            Assert.That(zeroCount, Is.False);
            Assert.That(depleted, Is.True);
            Assert.That(instance.IsActive, Is.False);
            Assert.That(asc.ActiveEffects, Is.Empty);
            Assert.That(removed, Is.EqualTo(new[] { (instance, EffectRemovalReason.StacksDepleted) }));
        }

        [Test]
        public void Depletion_EventOrder_StacksChangedThenRemoved()
        {
            var burn = _gas.CreateTag("Status.Burn");
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = CreateStackingEffect(5);
            effect.GrantedTags.Add(burn);
            ActiveEffectInstance instance = ApplyStacks(asc, effect, 3);
            var order = new List<string>();
            asc.OnEffectStacksChanged += (i, before, after) => order.Add($"stacks {before}->{after} tag={asc.HasTag(burn)}");
            asc.OnEffectRemoved += (i, reason) => order.Add($"removed {reason}");

            asc.SetEffectStacks(instance, 0);

            Assert.That(order, Is.EqualTo(new[] { "stacks 3->0 tag=False", "removed StacksDepleted" }));
        }

        [Test]
        public void InstanceShortcuts_DelegateToAsc()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(CreateStackingEffect(5), 4f);
            var changes = RecordStacks(asc);

            Assert.That(instance.ExtendDuration(2f), Is.True);
            Assert.That(instance.TimeRemaining, Is.EqualTo(6f).Within(1e-4f));
            Assert.That(instance.ReduceDuration(1f), Is.True);
            Assert.That(instance.TimeRemaining, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(instance.RestartDuration(), Is.True);
            Assert.That(instance.TimeRemaining, Is.EqualTo(4f));
            Assert.That(instance.SetStacks(4), Is.True);
            Assert.That(instance.RemoveStacks(1), Is.True);
            Assert.That(changes, Is.EqualTo(new[] { (instance, 1, 4), (instance, 4, 3) }));
        }

        [Test]
        public void ShortcutOnRemovedInstance_False()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            AbilitySystemComponent other = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(CreateStackingEffect(5));
            asc.RemoveEffect(instance);

            Assert.That(instance.ExtendDuration(1f), Is.False);
            Assert.That(instance.ReduceDuration(1f), Is.False);
            Assert.That(instance.RestartDuration(), Is.False);
            Assert.That(instance.SetStacks(2), Is.False);
            Assert.That(instance.RemoveStacks(1), Is.False);

            ActiveEffectInstance foreign = asc.ApplyEffect(CreateStackingEffect(5));
            Assert.That(other.ExtendEffectDuration(foreign, 1f), Is.False, "outro ASC mudou a instância");
            Assert.That(other.SetEffectStacks(foreign, 2), Is.False, "outro ASC mudou a instância");
            Assert.That(other.SetEffectStacks(null, 2), Is.False);
            Assert.That(foreign.CurrentStacks, Is.EqualTo(1));
        }

        private ActiveEffectInstance ApplyStacks(AbilitySystemComponent asc, GameplayEffect effect, int stacks)
        {
            ActiveEffectInstance instance = asc.ApplyEffect(effect);
            for (int i = 1; i < stacks; i++) asc.ApplyEffect(effect);
            Assert.That(instance.CurrentStacks, Is.EqualTo(stacks), "preparo: pilhas");
            return instance;
        }

        private GameplayEffect CreateStackingEffect(int maxStacks)
        {
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            effect.CanStack = true;
            effect.MaxStacks = maxStacks;
            return effect;
        }

        private static List<(ActiveEffectInstance, int, int)> RecordStacks(AbilitySystemComponent asc)
        {
            var changes = new List<(ActiveEffectInstance, int, int)>();
            asc.OnEffectStacksChanged += (i, before, after) => changes.Add((i, before, after));
            return changes;
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
