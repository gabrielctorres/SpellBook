#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Lista pública de efeitos ativos, dados da instância e busca por tag, consulta e asset (B-003, EA2: CA1 a CA4, CA27).</summary>
    public class EffectQueryTests
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
        public void ActiveEffects_InApplicationOrder()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance first = asc.ApplyEffect(_gas.CreateDurationEffect(5f));
            ActiveEffectInstance second = asc.ApplyEffect(_gas.CreateDurationEffect(5f));
            ActiveEffectInstance third = asc.ApplyEffect(_gas.CreateDurationEffect(5f));

            asc.RemoveEffect(second);

            Assert.That(asc.ActiveEffects, Is.EqualTo(new[] { first, third }));
        }

        [Test]
        public void FindWithTag_ParentReturnsChildren_ExactReturnsOne_UnknownReturnsZero()
        {
            GameplayTag status = _gas.CreateTag("Status");
            GameplayTag burn = _gas.CreateTag("Status.Burn", status);
            GameplayTag poison = _gas.CreateTag("Status.Poison", status);
            GameplayTag unknown = _gas.CreateTag("State.Hasted");
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance burning = asc.ApplyEffect(_gas.CreateDurationEffect(5f, burn));
            ActiveEffectInstance poisoned = asc.ApplyEffect(_gas.CreateDurationEffect(5f, poison));
            var results = new List<ActiveEffectInstance> { poisoned };

            int byParent = asc.FindEffectsWithTag(status, results);
            ActiveEffectInstance[] parentResults = results.ToArray();
            int byExact = asc.FindEffectsWithTag(burn, results);
            ActiveEffectInstance[] exactResults = results.ToArray();
            int byUnknown = asc.FindEffectsWithTag(unknown, results);

            Assert.That(byParent, Is.EqualTo(2));
            Assert.That(parentResults, Is.EqualTo(new[] { burning, poisoned }), "a lista não foi limpa antes ou saiu da ordem de aplicação");
            Assert.That(byExact, Is.EqualTo(1));
            Assert.That(exactResults, Is.EqualTo(new[] { burning }));
            Assert.That(byUnknown, Is.EqualTo(0));
            Assert.That(results, Is.Empty);
        }

        [Test]
        public void FindWithTag_InvalidTag_ZeroAndHasEffectFalse()
        {
            GameplayTag burn = _gas.CreateTag("Status.Burn");
            AbilitySystemComponent asc = CreateManualAsc();
            asc.ApplyEffect(_gas.CreateDurationEffect(5f, burn));
            var results = new List<ActiveEffectInstance>();

            int found = asc.FindEffectsWithTag(default, results);

            Assert.That(found, Is.EqualTo(0));
            Assert.That(results, Is.Empty);
            Assert.That(asc.HasEffectWithTag(default), Is.False);
            Assert.That(asc.HasEffectWithTag(burn), Is.True);
        }

        [Test]
        public void FindWithAnyTag_Container()
        {
            GameplayTag burn = _gas.CreateTag("Status.Burn");
            GameplayTag poison = _gas.CreateTag("Status.Poison");
            GameplayTag stun = _gas.CreateTag("Status.Stun");
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance burning = asc.ApplyEffect(_gas.CreateDurationEffect(5f, burn));
            asc.ApplyEffect(_gas.CreateDurationEffect(5f, poison));
            ActiveEffectInstance stunned = asc.ApplyEffect(_gas.CreateDurationEffect(5f, stun));
            var results = new List<ActiveEffectInstance>();

            int found = asc.FindEffectsWithAnyTag(new GameplayTagContainer(stun, burn), results);

            Assert.That(found, Is.EqualTo(2));
            Assert.That(results, Is.EqualTo(new[] { burning, stunned }));
        }

        [Test]
        public void FindMatching_QueryAllAnyNone()
        {
            GameplayTag damage = _gas.CreateTag("Damage");
            GameplayTag fire = _gas.CreateTag("Element.Fire");
            GameplayTag ice = _gas.CreateTag("Element.Ice");
            GameplayTag cleansable = _gas.CreateTag("Cleansable");
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance fireDamage = asc.ApplyEffect(_gas.CreateDurationEffect(5f, damage, fire));
            asc.ApplyEffect(_gas.CreateDurationEffect(5f, fire));
            asc.ApplyEffect(_gas.CreateDurationEffect(5f, damage, ice, cleansable));
            ActiveEffectInstance iceDamage = asc.ApplyEffect(_gas.CreateDurationEffect(5f, damage, ice));
            var query = new GameplayTagQuery();
            query.All.Add(damage);
            query.Any.Add(fire);
            query.Any.Add(ice);
            query.None.Add(cleansable);
            var results = new List<ActiveEffectInstance>();

            int found = asc.FindEffectsMatching(query, results);

            Assert.That(found, Is.EqualTo(2));
            Assert.That(results, Is.EqualTo(new[] { fireDamage, iceDamage }));
        }

        [Test]
        public void FindEffects_ByAsset()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect wanted = _gas.CreateDurationEffect(5f);
            asc.ApplyEffect(_gas.CreateDurationEffect(5f));
            ActiveEffectInstance instance = asc.ApplyEffect(wanted);
            var results = new List<ActiveEffectInstance>();

            int found = asc.FindEffects(wanted, results);
            ActiveEffectInstance[] wantedResults = results.ToArray();
            int foundNull = asc.FindEffects(null, results);

            Assert.That(found, Is.EqualTo(1));
            Assert.That(wantedResults, Is.EqualTo(new[] { instance }));
            Assert.That(foundNull, Is.EqualTo(0));
        }

        [Test]
        public void Find_NullResults_Throws()
        {
            GameplayTag burn = _gas.CreateTag("Status.Burn");
            AbilitySystemComponent asc = CreateManualAsc();

            Assert.Throws<ArgumentNullException>(() => asc.FindEffectsWithTag(burn, null));
            Assert.Throws<ArgumentNullException>(() => asc.FindEffectsWithAnyTag(new GameplayTagContainer(burn), null));
            Assert.Throws<ArgumentNullException>(() => asc.FindEffectsMatching(new GameplayTagQuery(), null));
            Assert.Throws<ArgumentNullException>(() => asc.FindEffects(_gas.CreateDurationEffect(5f), null));
        }

        [Test]
        public void Instance_ExposesEffectSourceTargetRemainingAppliedDurationStacks()
        {
            AbilitySystemComponent source = CreateManualAsc();
            AbilitySystemComponent target = CreateManualAsc();
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            effect.CanStack = true;
            effect.MaxStacks = 3;
            effect.RefreshDurationOnStack = false;
            target.ApplyEffect(effect, source);
            ActiveEffectInstance instance = target.ApplyEffect(effect, source);

            target.Tick(1f);

            Assert.That(instance.Effect, Is.SameAs(effect));
            Assert.That(instance.Source, Is.SameAs(source));
            Assert.That(instance.Target, Is.SameAs(target));
            Assert.That(instance.TimeRemaining, Is.EqualTo(4f));
            Assert.That(instance.AppliedDuration, Is.EqualTo(5f));
            Assert.That(instance.CurrentStacks, Is.EqualTo(2));
        }

        [Test]
        public void AppliedDuration_IsReducedCooldownDuration()
        {
            AttributeDefinition cdr = _gas.CreateAttribute("CooldownReduction");
            AbilitySystemComponent asc = CreateManualAsc((cdr, 0.25f));
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(8f);
            ability.CooldownReductionAttribute = cdr;
            asc.GiveAbility(ability);
            Assert.That(asc.TryActivateAbility(ability, Vector3.zero), Is.EqualTo(AbilityActivationResult.Success));
            var results = new List<ActiveEffectInstance>();

            asc.FindEffects(ability.CooldownEffect, results);

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].AppliedDuration, Is.EqualTo(6f), "a duração aplicada não é a de 8 s reduzida em 25%");
        }

        [Test]
        public void Instance_IsActiveFalseAfterRemoval()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            ActiveEffectInstance instance = asc.ApplyEffect(_gas.CreateDurationEffect(5f));
            bool activeBefore = instance.IsActive;

            asc.RemoveEffect(instance);

            Assert.That(activeBefore, Is.True);
            Assert.That(instance.IsActive, Is.False);
        }

        [Test]
        public void Queries_DoNotAllocate_10000Times()
        {
            GameplayTag status = _gas.CreateTag("Status");
            GameplayTag burn = _gas.CreateTag("Status.Burn", status);
            GameplayTag stun = _gas.CreateTag("Status.Stun", status);
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect burning = _gas.CreateDurationEffect(50f, burn);
            asc.ApplyEffect(burning);
            asc.ApplyEffect(_gas.CreateDurationEffect(50f, stun));
            var container = new GameplayTagContainer(stun);
            var query = new GameplayTagQuery();
            query.All.Add(status);
            var results = new List<ActiveEffectInstance>(4);
            int sum = 0;
            float remaining = 0f;

            void Read()
            {
                IReadOnlyList<ActiveEffectInstance> effects = asc.ActiveEffects;
                for (int e = 0; e < effects.Count; e++) sum += effects[e].CurrentStacks;
                sum += asc.FindEffectsWithTag(status, results);
                sum += asc.FindEffectsWithAnyTag(container, results);
                sum += asc.FindEffectsMatching(query, results);
                sum += asc.FindEffects(burning, results);
                if (asc.HasEffectWithTag(burn)) sum++;
                remaining += asc.GetCooldownTimeRemaining(burning);
            }

            for (int i = 0; i < 10; i++) Read(); // aquecimento
            sum = 0;

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++) Read();
            }, Is.Not.AllocatingGCMemory());
            Assert.That(sum, Is.EqualTo(10000 * 9));
            Assert.That(remaining, Is.GreaterThan(0f));
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
