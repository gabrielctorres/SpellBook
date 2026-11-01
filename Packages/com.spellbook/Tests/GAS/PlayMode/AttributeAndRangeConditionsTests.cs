#if UNITY_EDITOR
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.GAS.Tests
{
    /// <summary>Condição de atributo, ponto-alvo e alcance da habilidade sobre o <see cref="ConditionContext"/> (AV3).</summary>
    public class AttributeAndRangeConditionsTests
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
        public void Threshold_Mana30Passes_10Fails()
        {
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            AttributeThresholdCondition condition = Threshold(mana, AttributeThresholdCondition.Comparison.GreaterOrEqual, ScalableFloat.Constant(20f));
            AbilitySystemComponent rich = _gas.CreateAsc((mana, 30f));
            AbilitySystemComponent poor = _gas.CreateAsc((mana, 10f));

            bool passes = condition.Evaluate(Context(rich));
            bool fails = condition.Evaluate(Context(poor));

            Assert.That(passes, Is.True);
            Assert.That(fails, Is.False);
            Assert.That(condition.DefaultFailureKey, Is.EqualTo("gas.activation.attribute_threshold"));
        }

        [TestCase(AttributeThresholdCondition.Comparison.Greater, 30f, false)]
        [TestCase(AttributeThresholdCondition.Comparison.Greater, 29f, true)]
        [TestCase(AttributeThresholdCondition.Comparison.GreaterOrEqual, 30f, true)]
        [TestCase(AttributeThresholdCondition.Comparison.Less, 30f, false)]
        [TestCase(AttributeThresholdCondition.Comparison.Less, 31f, true)]
        [TestCase(AttributeThresholdCondition.Comparison.LessOrEqual, 30f, true)]
        [TestCase(AttributeThresholdCondition.Comparison.Equal, 30.00005f, true)]
        [TestCase(AttributeThresholdCondition.Comparison.Equal, 30.01f, false)]
        public void Threshold_Comparisons_EqualWithTolerance(AttributeThresholdCondition.Comparison comparison, float value, bool expected)
        {
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            AttributeThresholdCondition condition = Threshold(mana, comparison, ScalableFloat.Constant(value));
            AbilitySystemComponent asc = _gas.CreateAsc((mana, 30f));

            bool result = condition.Evaluate(Context(asc));

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Threshold_Ratio_UsesEffectiveMax()
        {
            AttributeDefinition maxHealth = _gas.CreateAttribute("MaxHealth");
            AttributeDefinition health = _gas.CreateAttribute("Health");
            health.MaxValue = 100f;
            health.MaxSource = LimitSource.Attribute;
            health.MaxAttribute = maxHealth;
            AttributeThresholdCondition condition = Threshold(health, AttributeThresholdCondition.Comparison.Less, ScalableFloat.Constant(0.3f));
            condition.UseRatio = true;
            AbilitySystemComponent asc = _gas.CreateAsc((maxHealth, 200f), (health, 50f));

            // 50 / 200 = 0,25 pelo máximo efetivo; pela constante da definição seria 0,5
            bool belowThirty = condition.Evaluate(Context(asc));
            Assert.That(asc.AttributeSet.TryGetState(maxHealth, out AttributeState maxState), Is.True);
            maxState.SetBaseValue(0f);
            bool zeroMax = condition.Evaluate(Context(asc));

            Assert.That(belowThirty, Is.True);
            Assert.That(zeroMax, Is.False, "máximo ≤ 0 dá falso");
        }

        [Test]
        public void Threshold_TableValueByLevel()
        {
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            GameplayAbility ability = _gas.CreateAbility();
            ability.Values.Add("minMana", ScalableFloat.FromArray(10f, 40f));
            AttributeThresholdCondition condition = Threshold(mana, AttributeThresholdCondition.Comparison.GreaterOrEqual, ScalableFloat.FromTable("minMana"));
            AbilitySystemComponent asc = _gas.CreateAsc((mana, 30f));

            bool levelOne = condition.Evaluate(Context(asc, ability, 1));
            bool levelTwo = condition.Evaluate(Context(asc, ability, 2));

            Assert.That(levelOne, Is.True);
            Assert.That(levelTwo, Is.False);
        }

        [Test]
        public void Threshold_MissingAttribute_False()
        {
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AttributeThresholdCondition missing = Threshold(mana, AttributeThresholdCondition.Comparison.GreaterOrEqual, ScalableFloat.Constant(-1000f));
            AttributeThresholdCondition noAttribute = Threshold(null, AttributeThresholdCondition.Comparison.GreaterOrEqual, ScalableFloat.Constant(-1000f));
            AbilitySystemComponent asc = _gas.CreateAsc((health, 30f));

            bool absent = missing.Evaluate(Context(asc));
            bool unset = noAttribute.Evaluate(Context(asc));
            bool noOwner = missing.Evaluate(Context(null));

            Assert.That(absent, Is.False);
            Assert.That(unset, Is.False);
            Assert.That(noOwner, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CastRange_3Passes_8Fails()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.CastRange = ScalableFloat.Constant(5f);
            AbilitySystemComponent asc = _gas.CreateAsc();
            asc.transform.position = new Vector3(1f, 0f, 1f);
            var condition = new CastRangeCondition();

            bool near = condition.Evaluate(Context(asc, ability, 1, AbilityTarget.AtPoint(new Vector3(4f, 0f, 1f))));
            bool far = condition.Evaluate(Context(asc, ability, 1, AbilityTarget.AtPoint(new Vector3(1f, 0f, 9f))));

            Assert.That(near, Is.True);
            Assert.That(far, Is.False);
            Assert.That(condition.DefaultFailureKey, Is.EqualTo("gas.activation.cast_range"));
        }

        [Test]
        public void CastRange_EvaluatedAtContextLevel()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.CastRange = ScalableFloat.FromArray(2f, 6f);
            AbilitySystemComponent asc = _gas.CreateAsc();
            AbilityTarget target = AbilityTarget.AtPoint(new Vector3(4f, 0f, 0f));
            var condition = new CastRangeCondition();

            bool levelOne = condition.Evaluate(Context(asc, ability, 1, target));
            bool levelTwo = condition.Evaluate(Context(asc, ability, 2, target));

            Assert.That(levelOne, Is.False);
            Assert.That(levelTwo, Is.True);
        }

        [Test]
        public void HasTargetPoint_NoPoint_FalseWithNoTargetKey()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            var condition = new HasTargetPointCondition();

            bool withoutPoint = condition.Evaluate(Context(asc));
            bool withPoint = condition.Evaluate(Context(asc, null, 1, AbilityTarget.AtPoint(Vector3.zero)));

            Assert.That(withoutPoint, Is.False);
            Assert.That(withPoint, Is.True);
            Assert.That(condition.DefaultFailureKey, Is.EqualTo("gas.activation.no_target"));
        }

        [Test]
        public void CastRange_NoPoint_False_NoException_CastRangeKey()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.CastRange = ScalableFloat.Constant(5f);
            AbilitySystemComponent asc = _gas.CreateAsc();
            var condition = new CastRangeCondition();
            bool result = true;

            Assert.That(() => result = condition.Evaluate(Context(asc, ability, 1)), Throws.Nothing);
            Assert.That(result, Is.False);
            Assert.That(condition.DefaultFailureKey, Is.EqualTo("gas.activation.cast_range"));
        }

        [Test]
        public void CastRangeZero_Unlimited_EvenWithoutPoint()
        {
            GameplayAbility ability = _gas.CreateAbility();
            AbilitySystemComponent asc = _gas.CreateAsc();
            var condition = new CastRangeCondition();
            float defaultRange = ability.CastRange.Evaluate(1);

            bool defaultWithoutPoint = condition.Evaluate(Context(asc, ability, 1));
            bool defaultFarPoint = condition.Evaluate(Context(asc, ability, 1, AbilityTarget.AtPoint(new Vector3(1000f, 0f, 0f))));
            ability.CastRange = ScalableFloat.Constant(-1f);
            bool negativeWithoutPoint = condition.Evaluate(Context(asc, ability, 1));

            Assert.That(defaultRange, Is.EqualTo(0f), "o alcance nasce 0");
            Assert.That(defaultWithoutPoint, Is.True);
            Assert.That(defaultFarPoint, Is.True);
            Assert.That(negativeWithoutPoint, Is.True);
        }

        private static AttributeThresholdCondition Threshold(
            AttributeDefinition attribute, AttributeThresholdCondition.Comparison comparison, ScalableFloat value)
        {
            var condition = new AttributeThresholdCondition
            {
                Attribute = attribute,
                ComparisonType = comparison
            };
            condition.Value.Type = ModifierValueSource.SourceType.Value;
            condition.Value.Value = value;
            return condition;
        }

        private static ConditionContext Context(
            AbilitySystemComponent owner, GameplayAbility ability = null, int level = 1, AbilityTarget target = default)
        {
            return new ConditionContext(owner, ability, level, null, target, null);
        }
    }
}
#endif
