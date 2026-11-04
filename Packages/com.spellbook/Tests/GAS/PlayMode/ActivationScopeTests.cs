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
    /// <summary>Consulta com escopo: <c>WithoutConditions</c> para antes das regras, na ordem da ADR 0022 (Spec do B-016, P3).</summary>
    public class ActivationScopeTests
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
        public void Scope_WithoutConditions_StopsBeforeRules_SameOrder()
        {
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            GameplayTag stunned = _gas.CreateTag("Stunned");
            AbilitySystemComponent asc = _gas.CreateAsc((mana, 50f));
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(5f);
            ability.CostEffect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            ability.CostEffect.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = mana,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(-30f) }
            });
            ability.BlockedTags.Add(stunned);
            ability.ActivationConditions.Add(new ActivationRule(new HasTargetPointCondition()));
            asc.GiveAbility(ability);

            AbilityActivationResult all = asc.GetActivationResult(ability, AbilityTarget.None, ActivationCheckScope.All, out ActivationFailure allFailure);
            AbilityActivationResult scoped = asc.GetActivationResult(ability, AbilityTarget.None, ActivationCheckScope.WithoutConditions, out _);
            AbilityActivationResult legacy = asc.GetActivationResult(ability, AbilityTarget.None, out _);
            asc.AddTag(stunned);
            AbilityActivationResult blockedAll = asc.GetActivationResult(ability, AbilityTarget.None, ActivationCheckScope.All, out _);
            AbilityActivationResult blockedScoped = asc.GetActivationResult(ability, AbilityTarget.None, ActivationCheckScope.WithoutConditions, out ActivationFailure blockedFailure);
            asc.RemoveTag(stunned);
            asc.TryActivateAbility(ability, Vector3.one);
            AbilityActivationResult cooldown = asc.GetActivationResult(ability, AbilityTarget.None, ActivationCheckScope.WithoutConditions, out _);
            asc.Tick(6f);
            AbilityActivationResult cost = asc.GetActivationResult(ability, AbilityTarget.None, ActivationCheckScope.WithoutConditions, out _);

            Assert.That(all, Is.EqualTo(AbilityActivationResult.BlockedByCondition));
            Assert.That(allFailure.ReasonKey, Is.EqualTo("gas.activation.no_target"));
            Assert.That(legacy, Is.EqualTo(all), "a sobrecarga sem escopo vale All");
            Assert.That(scoped, Is.EqualTo(AbilityActivationResult.Success), "sem as condições, nada falha");
            Assert.That(blockedAll, Is.EqualTo(AbilityActivationResult.BlockedByTags));
            Assert.That(blockedScoped, Is.EqualTo(AbilityActivationResult.BlockedByTags));
            Assert.That(blockedFailure.ReasonTag, Is.EqualTo(stunned));
            Assert.That(cooldown, Is.EqualTo(AbilityActivationResult.OnCooldown));
            Assert.That(cost, Is.EqualTo(AbilityActivationResult.CannotAffordCost), "20 de mana não pagam 30");
            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    asc.GetActivationResult(ability, AbilityTarget.None, ActivationCheckScope.WithoutConditions, out _);
                }
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
#endif
