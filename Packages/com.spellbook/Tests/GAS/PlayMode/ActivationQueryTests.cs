#if UNITY_EDITOR
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Consulta de ativação com alvo: sem efeito colateral, igual à tentativa e sem alocar (AV4).</summary>
    public class ActivationQueryTests
    {
        private GasFactory _gas;
        private AttributeDefinition _mana;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _mana = _gas.CreateAttribute("Mana");
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
        }

        [Test]
        public void Query_NoSideEffects_SameAsTry()
        {
            GameplayTag casting = _gas.CreateTag("State.Casting");
            AbilitySystemComponent asc = _gas.CreateAsc((_mana, 50f));
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(5f);
            ability.CostEffect = Cost(10f);
            ability.ActivationOwnedTags.Add(casting);
            ability.ActivationConditions.Add(new ActivationRule(new HasTargetPointCondition()));
            AbilitySpec spec = asc.GiveAbility(ability);
            int events = 0;
            asc.OnAbilityActivated += _ => events++;
            asc.OnAbilityFailed += (_, _) => events++;
            asc.OnEffectApplied += _ => events++;
            asc.OnTagChanged += (_, _) => events++;
            asc.OnCooldownChanged += (_, _) => events++;

            AbilityActivationResult blocked = asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure failure);
            AbilityActivationResult passes = asc.GetActivationResult(ability, AbilityTarget.AtPoint(Vector3.one), out _);

            Assert.That(blocked, Is.EqualTo(AbilityActivationResult.BlockedByCondition));
            Assert.That(failure.ReasonKey, Is.EqualTo("gas.activation.no_target"));
            Assert.That(passes, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(events, Is.EqualTo(0));
            Assert.That(asc.GetAttributeValue(_mana), Is.EqualTo(50f));
            Assert.That(asc.ActiveEffects.Count, Is.EqualTo(0));
            Assert.That(asc.TagCounts.Count, Is.EqualTo(0));
            Assert.That(spec.IsActive, Is.False);

            AbilityActivationResult tryBlocked = asc.TryActivateAbility(ability, AbilityTarget.None);
            AbilityActivationResult tryPasses = asc.TryActivateAbility(ability, AbilityTarget.AtPoint(Vector3.one));

            Assert.That(tryBlocked, Is.EqualTo(blocked));
            Assert.That(tryPasses, Is.EqualTo(passes));
            Assert.That(asc.GetAttributeValue(_mana), Is.EqualTo(40f));
        }

        [Test]
        public void Query_WithPoint_CastRange()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility ability = _gas.CreateAbility();
            ability.CastRange = ScalableFloat.Constant(5f);
            ability.ActivationConditions.Add(new ActivationRule(new CastRangeCondition()));
            asc.GiveAbility(ability);

            AbilityActivationResult near = asc.GetActivationResult(ability, AbilityTarget.AtPoint(new Vector3(3f, 0f, 0f)), out _);
            AbilityActivationResult far = asc.GetActivationResult(ability, AbilityTarget.AtPoint(new Vector3(8f, 0f, 0f)), out ActivationFailure failure);
            AbilityActivationResult legacyFar = asc.TryActivateAbility(ability, new Vector3(8f, 0f, 0f));

            Assert.That(near, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(far, Is.EqualTo(AbilityActivationResult.BlockedByCondition));
            Assert.That(failure.ReasonKey, Is.EqualTo("gas.activation.cast_range"));
            Assert.That(legacyFar, Is.EqualTo(AbilityActivationResult.BlockedByCondition));
        }

        [Test]
        public void Query4Conditions_DoesNotAllocate_10000Times()
        {
            GameplayTag ready = _gas.CreateTag("Ready");
            GameplayTag stunned = _gas.CreateTag("Stunned");
            AbilitySystemComponent asc = _gas.CreateAsc((_mana, 50f));
            asc.AddTag(ready);
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(5f);
            ability.CostEffect = Cost(10f);
            ability.BlockedTags.Add(stunned);
            ability.RequiredTags.Add(ready);
            ability.CastRange = ScalableFloat.Constant(10f);
            var threshold = new AttributeThresholdCondition { Attribute = _mana };
            threshold.Value.Value = ScalableFloat.Constant(20f);
            var tagQuery = new TagQueryCondition();
            tagQuery.Query.All.Add(ready);
            ability.ActivationConditions.Add(new ActivationRule(threshold));
            ability.ActivationConditions.Add(new ActivationRule(tagQuery));
            ability.ActivationConditions.Add(new ActivationRule(new HasTargetPointCondition()));
            ability.ActivationConditions.Add(new ActivationRule(new CastRangeCondition()));
            asc.GiveAbility(ability);
            AbilityTarget target = AbilityTarget.AtPoint(new Vector3(3f, 0f, 0f));
            AbilityActivationResult warmUp = asc.GetActivationResult(ability, target, out _);

            Assert.That(warmUp, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    asc.GetActivationResult(ability, target, out _);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        private GameplayEffect Cost(float amount)
        {
            GameplayEffect cost = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            cost.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = _mana,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(-amount) }
            });
            return cost;
        }
    }
}
#endif
