#if UNITY_EDITOR
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary><see cref="AbilitySystemComponent.OnAbilityFailed"/> leva a <see cref="ActivationFailure"/> da checagem (AV5, CA11).</summary>
    public class AbilityFailedEventTests
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
        public void Failed_CarriesAbilityResultAndReason_SameAsQuery()
        {
            GameplayTag reason = _gas.CreateTag("Falha.SemAlvo");
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule(new HasTargetPointCondition(), reason, "ui.sem_alvo"));
            asc.GiveAbility(ability);
            GameplayAbility raisedAbility = null;
            ActivationFailure raised = default;
            int calls = 0;
            asc.OnAbilityFailed += (a, f) =>
            {
                raisedAbility = a;
                raised = f;
                calls++;
            };
            asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure queried);

            AbilityActivationResult result = asc.TryActivateAbility(ability, AbilityTarget.None);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(raisedAbility, Is.SameAs(ability));
            Assert.That(result, Is.EqualTo(AbilityActivationResult.BlockedByCondition));
            Assert.That(raised.Result, Is.EqualTo(queried.Result));
            Assert.That(raised.ReasonTag, Is.EqualTo(reason));
            Assert.That(raised.ReasonKey, Is.EqualTo("ui.sem_alvo"));
            Assert.That(raised.ConditionIndex, Is.EqualTo(queried.ConditionIndex).And.EqualTo(0));
            Assert.That(raised.Rule, Is.SameAs(queried.Rule).And.SameAs(ability.ActivationConditions[0]));
        }

        [Test]
        public void Failed_SpecOfAnotherAsc_CarriesNotGranted()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            AbilitySystemComponent other = _gas.CreateAsc();
            AbilitySpec foreign = other.GiveAbility(_gas.CreateAbility());
            ActivationFailure raised = default;
            asc.OnAbilityFailed += (_, f) => raised = f;

            asc.TryActivateAbility(foreign, Vector3.zero);

            Assert.That(raised.Result, Is.EqualTo(AbilityActivationResult.NotGranted));
            Assert.That(raised.ReasonKey, Is.Null);
            Assert.That(raised.ConditionIndex, Is.EqualTo(-1));
        }

        [Test]
        public void Failed_NotRaisedOnQuery()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule(new HasTargetPointCondition()));
            asc.GiveAbility(ability);
            int calls = 0;
            asc.OnAbilityFailed += (_, _) => calls++;

            AbilityActivationResult result = asc.GetActivationResult(ability, AbilityTarget.None, out _);

            Assert.That(result, Is.EqualTo(AbilityActivationResult.BlockedByCondition));
            Assert.That(calls, Is.EqualTo(0));
        }
    }
}
#endif
