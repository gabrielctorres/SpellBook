using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Conditions;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>Validação das regras de ativação e das cargas (B-009, AV8: CA25).</summary>
    public class ActivationValidationTests
    {
        private const string Rule0 = "activationConditions.Array.data[0]";
        private const string Condition0 = Rule0 + ".condition";

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
        public void NullRule_Error()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(null);

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, Rule0);
        }

        [Test]
        public void RuleWithoutCondition_Error()
        {
            GameplayAbility ability = WithRule(null);

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, Condition0);
        }

        [Test]
        public void EmptyOr_Warns()
        {
            GameplayAbility ability = WithRule(new OrCondition());

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Warning, Condition0);
        }

        [Test]
        public void NotWithoutCondition_Error()
        {
            GameplayAbility ability = WithRule(new NotCondition());

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, Condition0 + ".condition");
        }

        [Test]
        public void AttributeThreshold_WithoutAttribute_Error()
        {
            GameplayAbility ability = WithRule(new AttributeThresholdCondition());

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, Condition0 + ".attribute");
        }

        [TestCase(ModifierValueSource.SourceType.SetByCaller)]
        [TestCase(ModifierValueSource.SourceType.TargetAttribute)]
        public void AttributeThreshold_CallerOrTargetSource_Error(ModifierValueSource.SourceType type)
        {
            var condition = new AttributeThresholdCondition { Attribute = _gas.CreateAttribute("Mana") };
            condition.Value.Type = type;
            GameplayAbility ability = WithRule(condition);

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, Condition0 + ".value");
            Assert.That(message.Message, Does.Contain(type.ToString()));
        }

        [Test]
        public void CastRange_Negative_Error()
        {
            GameplayAbility ability = WithRule(new CastRangeCondition());
            ability.CastRange = ScalableFloat.Constant(-2f);

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, "castRange");
        }

        [Test]
        public void CastRange_NegativeOnlyAtMaxLevel_Error()
        {
            GameplayAbility ability = WithRule(new CastRangeCondition());
            SetMaxLevel(ability, 2);
            ability.CastRange = ScalableFloat.FromArray(5f, -1f);

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, "castRange");
        }

        [Test]
        public void CastRange_Unset_WarnsAlwaysPasses()
        {
            var nested = new AndCondition();
            nested.Conditions.Add(new NotCondition { Condition = new CastRangeCondition() });
            GameplayAbility ability = WithRule(nested);

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Warning, "castRange");
        }

        [Test]
        public void CastRange_WithoutCondition_NotChecked()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.CastRange = ScalableFloat.Constant(-2f);

            Assert.That(Validate(ability).Messages, Is.Empty);
        }

        [Test]
        public void TagQuery_Empty_Warns()
        {
            GameplayAbility ability = WithRule(new TagQueryCondition());

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Warning, Condition0 + ".query");
        }

        [Test]
        public void TagQuery_TagInTwoLists_Error()
        {
            GameplayTag stunned = _gas.CreateTag("State.Stunned");
            var condition = new TagQueryCondition();
            condition.Query.All.Add(stunned);
            condition.Query.None.Add(stunned);
            GameplayAbility ability = WithRule(condition);

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, Condition0 + ".query");
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        public void Charges_ConstantMaxBelowOne_Error(float max)
        {
            GameplayAbility ability = WithCharges();
            ability.Charges.MaxCharges = new ModifierValueSource { Value = ScalableFloat.Constant(max) };

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, "charges.maxCharges");
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Charges_ConstantRechargeTimeNotPositive_Error(float seconds)
        {
            GameplayAbility ability = WithCharges();
            ability.Charges.RechargeTime = ScalableFloat.Constant(seconds);

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, "charges.rechargeTime");
        }

        [Test]
        public void Charges_UnknownMaxTableName_Error()
        {
            GameplayAbility ability = WithCharges();
            ability.Charges.MaxCharges = new ModifierValueSource { Value = ScalableFloat.FromTable("Charges") };

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, "charges.maxCharges");
            Assert.That(message.Message, Does.Contain("Charges"));
        }

        [Test]
        public void Charges_UnknownRechargeTableName_Error()
        {
            GameplayAbility ability = WithCharges();
            ability.Charges.RechargeTime = ScalableFloat.FromTable("Recharge");

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, "charges.rechargeTime");
            Assert.That(message.Message, Does.Contain("Recharge"));
        }

        [Test]
        public void Charges_KnownTableNames_NoMessages()
        {
            GameplayAbility ability = WithCharges();
            ability.Values.Add("Charges", ScalableFloat.Constant(2f));
            ability.Values.Add("Recharge", ScalableFloat.Constant(3f));
            ability.Charges.MaxCharges = new ModifierValueSource { Value = ScalableFloat.FromTable("Charges") };
            ability.Charges.RechargeTime = ScalableFloat.FromTable("Recharge");

            Assert.That(Validate(ability).Messages, Is.Empty);
        }

        [Test]
        public void Charges_Disabled_NotChecked()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Charges.MaxCharges = new ModifierValueSource { Value = ScalableFloat.Constant(0f) };
            ability.Charges.RechargeTime = ScalableFloat.Constant(0f);

            Assert.That(Validate(ability).Messages, Is.Empty);
        }

        [Test]
        public void ValidRulesAndCharges_NoMessages()
        {
            var threshold = new AttributeThresholdCondition { Attribute = _gas.CreateAttribute("Mana") };
            GameplayAbility ability = WithRule(threshold);
            ability.ActivationConditions.Add(new ActivationRule(new CastRangeCondition()));
            ability.CastRange = ScalableFloat.Constant(8f);
            ability.Charges.Enabled = true;

            Assert.That(Validate(ability).Messages, Is.Empty);
        }

        [Test]
        public void ProjectValidator_ReportsAbilityAsContext()
        {
            GameplayAbility ability = WithRule(new AttributeThresholdCondition());
            var messages = new List<ValidationMessage>();

            ProjectValidator.Validate(new Object[] { ability }, messages);

            ValidationMessage message = messages.Single();
            Assert.That(message.Context, Is.SameAs(ability));
            Assert.That(message.PropertyPath, Is.EqualTo(Condition0 + ".attribute"));
        }

        private GameplayAbility WithRule(Condition<ConditionContext> condition)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule(condition));
            return ability;
        }

        private GameplayAbility WithCharges()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Charges.Enabled = true;
            return ability;
        }

        private static void SetMaxLevel(GameplayAbility ability, int maxLevel)
        {
            var serialized = new SerializedObject(ability);
            serialized.FindProperty("maxLevel").intValue = maxLevel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ValidationResult Validate(GameplayAbility ability)
        {
            var result = new ValidationResult(ability);
            ability.Validate(result);
            return result;
        }

        private static ValidationMessage Single(GameplayAbility ability)
        {
            IReadOnlyList<ValidationMessage> messages = Validate(ability).Messages;
            Assert.That(messages.Select(m => $"{m.Severity}: {m.Message} ({m.PropertyPath})").ToList(), Has.Count.EqualTo(1));
            return messages[0];
        }

        // O caminho leva ao campo no Inspector da habilidade: a mensagem é clicável
        private static void AssertClickable(GameplayAbility ability, ValidationMessage message, ValidationSeverity severity,
            string propertyPath)
        {
            Assert.That(message.Severity, Is.EqualTo(severity), message.Message);
            Assert.That(message.Context, Is.SameAs(ability));
            Assert.That(message.PropertyPath, Is.EqualTo(propertyPath));
            Assert.That(new SerializedObject(ability).FindProperty(propertyPath), Is.Not.Null, "o campo existe na habilidade");
        }
    }
}
