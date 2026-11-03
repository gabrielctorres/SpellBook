#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Conditions;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary>Ordem das checagens de ativação, regras de ativação e motivo da falha (AV4).</summary>
    public class ActivationOrderTests
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
        public void Order_TagBeatsCooldownAndCost()
        {
            GameplayTag stunned = _gas.CreateTag("Stunned");
            GameplayAbility ability = BlockedEverywhere(out AbilitySystemComponent asc);
            ability.BlockedTags.Add(stunned);
            asc.AddTag(stunned);

            AbilityActivationResult result = asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure failure);

            Assert.That(result, Is.EqualTo(AbilityActivationResult.BlockedByTags));
            Assert.That(failure.Result, Is.EqualTo(AbilityActivationResult.BlockedByTags));
        }

        [Test]
        public void Order_CooldownBeatsCost()
        {
            GameplayAbility ability = BlockedEverywhere(out AbilitySystemComponent asc);

            AbilityActivationResult result = asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure failure);

            Assert.That(result, Is.EqualTo(AbilityActivationResult.OnCooldown));
            Assert.That(failure.ReasonKey, Is.Null);
            Assert.That(failure.ConditionIndex, Is.EqualTo(-1));
        }

        [Test]
        public void Order_CostBeatsCondition()
        {
            AbilitySystemComponent asc = _gas.CreateAsc((_mana, 10f));
            GameplayAbility ability = _gas.CreateAbility();
            ability.CostEffect = Cost(_mana, 20f);
            ability.ActivationConditions.Add(new ActivationRule(new HasTargetPointCondition()));
            asc.GiveAbility(ability);

            AbilityActivationResult result = asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure failure);

            Assert.That(result, Is.EqualTo(AbilityActivationResult.CannotAffordCost));
            Assert.That(failure.ReasonKey, Is.Null);
            Assert.That(failure.Rule, Is.Null);
        }

        [Test]
        public void Order_ConditionLast_FirstRuleWins()
        {
            GameplayTag noPoint = _gas.CreateTag("Fail.NoPoint");
            GameplayTag lowMana = _gas.CreateTag("Fail.LowMana");
            AbilitySystemComponent asc = _gas.CreateAsc((_mana, 10f));
            GameplayAbility ability = _gas.CreateAbility();
            var first = new ActivationRule(new HasTargetPointCondition(), noPoint, "ui.no_point");
            var second = new ActivationRule(AtLeast(_mana, 20f), lowMana, "ui.low_mana");
            ability.ActivationConditions.Add(first);
            ability.ActivationConditions.Add(second);
            asc.GiveAbility(ability);

            AbilityActivationResult result = asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure failure);

            Assert.That(result, Is.EqualTo(AbilityActivationResult.BlockedByCondition));
            Assert.That(failure.ConditionIndex, Is.EqualTo(0));
            Assert.That(failure.Rule, Is.SameAs(first));
            Assert.That(failure.ReasonTag, Is.EqualTo(noPoint));
            Assert.That(failure.ReasonKey, Is.EqualTo("ui.no_point"));
        }

        [Test]
        public void Rule_Threshold_FailsWithConfiguredTagAndKey()
        {
            GameplayTag lowMana = _gas.CreateTag("Fail.LowMana");
            AbilitySystemComponent poor = _gas.CreateAsc((_mana, 10f));
            AbilitySystemComponent rich = _gas.CreateAsc((_mana, 30f));
            GameplayAbility ability = _gas.CreateAbility();
            var rule = new ActivationRule(AtLeast(_mana, 20f), lowMana, "ui.low_mana");
            ability.ActivationConditions.Add(rule);
            poor.GiveAbility(ability);
            rich.GiveAbility(ability);

            AbilityActivationResult poorResult = poor.TryActivateAbility(ability, AbilityTarget.None);
            poor.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure failure);
            AbilityActivationResult richResult = rich.TryActivateAbility(ability, AbilityTarget.None);

            Assert.That(poorResult, Is.EqualTo(AbilityActivationResult.BlockedByCondition));
            Assert.That(failure.ReasonTag, Is.EqualTo(lowMana));
            Assert.That(failure.ReasonKey, Is.EqualTo("ui.low_mana"));
            Assert.That(failure.Rule, Is.SameAs(rule));
            Assert.That(failure.IsSuccess, Is.False);
            Assert.That(richResult, Is.EqualTo(AbilityActivationResult.Success));
        }

        [Test]
        public void Rule_EmptyKey_UsesConditionDefault()
        {
            AbilitySystemComponent asc = _gas.CreateAsc((_mana, 10f));
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule(AtLeast(_mana, 20f), default, string.Empty));
            asc.GiveAbility(ability);

            asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure failure);

            Assert.That(failure.ReasonKey, Is.EqualTo("gas.activation.attribute_threshold"));
            Assert.That(failure.ReasonTag.IsValid, Is.False);
        }

        [Test]
        public void CustomCondition_EvaluatedInListOrder()
        {
            var log = new List<string>();
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(null);
            ability.ActivationConditions.Add(new ActivationRule(null));
            ability.ActivationConditions.Add(new ActivationRule(new RecordingCondition(log, "A", true)));
            ability.ActivationConditions.Add(new ActivationRule(new RecordingCondition(log, "B", false)));
            ability.ActivationConditions.Add(new ActivationRule(new RecordingCondition(log, "C", false)));
            asc.GiveAbility(ability);

            asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure failure);

            Assert.That(log, Is.EqualTo(new[] { "A", "B" }));
            Assert.That(failure.Result, Is.EqualTo(AbilityActivationResult.BlockedByCondition));
            Assert.That(failure.ConditionIndex, Is.EqualTo(3));
            Assert.That(failure.ReasonKey, Is.EqualTo("test.recording"));
        }

        [Test]
        public void ReasonTag_IsBlockingTag_MissingRequiredTag()
        {
            GameplayTag state = _gas.CreateTag("State");
            GameplayTag stunned = _gas.CreateTag("Stunned", state);
            GameplayTag ready = _gas.CreateTag("Ready");
            GameplayTag armed = _gas.CreateTag("Armed");
            GameplayTag casting = _gas.CreateTag("Ability.Cast");
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility blocked = _gas.CreateAbility();
            blocked.BlockedTags.Add(ready);
            blocked.BlockedTags.Add(state);
            GameplayAbility needy = _gas.CreateAbility();
            needy.RequiredTags.Add(ready);
            needy.RequiredTags.Add(armed);
            GameplayAbility blocker = _gas.CreateAbility(new WaitForeverAction());
            blocker.BlockAbilitiesWithTag.Add(casting);
            GameplayAbility cast = _gas.CreateAbility(casting);
            asc.GiveAbility(blocked);
            asc.GiveAbility(needy);
            asc.GiveAbility(blocker);
            asc.GiveAbility(cast);
            asc.AddTag(stunned);
            asc.AddTag(ready);
            asc.TryActivateAbility(blocker, AbilityTarget.None);

            asc.GetActivationResult(blocked, AbilityTarget.None, out ActivationFailure blockedFailure);
            asc.GetActivationResult(needy, AbilityTarget.None, out ActivationFailure missingFailure);
            asc.GetActivationResult(cast, AbilityTarget.None, out ActivationFailure activeFailure);

            Assert.That(blockedFailure.Result, Is.EqualTo(AbilityActivationResult.BlockedByTags));
            Assert.That(blockedFailure.ReasonTag, Is.EqualTo(ready));
            Assert.That(missingFailure.Result, Is.EqualTo(AbilityActivationResult.MissingRequiredTags));
            Assert.That(missingFailure.ReasonTag, Is.EqualTo(armed));
            Assert.That(activeFailure.Result, Is.EqualTo(AbilityActivationResult.BlockedByActiveAbility));
            Assert.That(activeFailure.ReasonTag, Is.EqualTo(casting));
        }

        [Test]
        public void NoRules_SameAsBefore()
        {
            AbilitySystemComponent asc = _gas.CreateAsc((_mana, 30f));
            GameplayAbility ability = _gas.CreateAbility();
            ability.CostEffect = Cost(_mana, 20f);
            AbilitySpec spec = asc.GiveAbility(ability);

            AbilityActivationResult query = asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure failure);
            AbilityActivationResult legacy = asc.GetActivationResult(ability);
            bool canActivate = ability.CanActivate(asc, out AbilityActivationResult reason);
            AbilityActivationResult first = asc.TryActivateAbility(ability, UnityEngine.Vector3.zero);
            // A execução segue até o próximo frame; cancelar deixa só o custo (Mana 10) barrando a segunda
            asc.CancelAbility(spec);
            AbilityActivationResult second = asc.TryActivateAbility(ability, UnityEngine.Vector3.zero);

            Assert.That(query, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(failure.IsSuccess, Is.True);
            Assert.That(failure.ConditionIndex, Is.EqualTo(-1));
            Assert.That(failure.Rule, Is.Null);
            Assert.That(failure.ReasonKey, Is.Null);
            Assert.That(legacy, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(canActivate, Is.True);
            Assert.That(reason, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(first, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(second, Is.EqualTo(AbilityActivationResult.CannotAffordCost));
        }

        // Concedida, em recarga, sem mana para o custo e com uma regra que falha
        private GameplayAbility BlockedEverywhere(out AbilitySystemComponent asc)
        {
            asc = _gas.CreateAsc((_mana, 10f));
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(10f);
            ability.CostEffect = Cost(_mana, 20f);
            ability.ActivationConditions.Add(new ActivationRule(new HasTargetPointCondition()));
            asc.GiveAbility(ability);
            asc.ApplyEffect(ability.CooldownEffect);
            return ability;
        }

        private GameplayEffect Cost(AttributeDefinition attribute, float amount)
        {
            GameplayEffect cost = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            cost.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(-amount) }
            });
            return cost;
        }

        private static AttributeThresholdCondition AtLeast(AttributeDefinition attribute, float value)
        {
            var condition = new AttributeThresholdCondition
            {
                Attribute = attribute,
                ComparisonType = AttributeThresholdCondition.Comparison.GreaterOrEqual
            };
            condition.Value.Type = ModifierValueSource.SourceType.Value;
            condition.Value.Value = ScalableFloat.Constant(value);
            return condition;
        }

        private sealed class RecordingCondition : Condition<ConditionContext>
        {
            private readonly List<string> _log;
            private readonly string _name;
            private readonly bool _result;

            public RecordingCondition(List<string> log, string name, bool result)
            {
                _log = log;
                _name = name;
                _result = result;
            }

            public override string DefaultFailureKey => "test.recording";

            public override bool Evaluate(in ConditionContext context)
            {
                _log.Add(_name);
                return _result;
            }
        }

        private sealed class WaitForeverAction : GameplayAction
        {
            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                return ActionStatus.Wait(float.MaxValue);
            }
        }
    }
}
#endif
