using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Conditions;
using SpellBook.Core.Validation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.Core.Tests.Conditions
{
    /// <summary>Base <c>Condition&lt;TContext&gt;</c> e composições do Core (ADR 0032; Spec do B-009, seção 1; CA2).</summary>
    public class ConditionCompositionTests
    {
        [Test]
        public void And_AllTrue_EmptyTrue_NullIgnored()
        {
            var context = new TestContext(5);

            Assert.That(new TestAnd(Positive(), Const(true)).Evaluate(in context), Is.True);
            Assert.That(new TestAnd(Positive(), Const(false)).Evaluate(in context), Is.False);
            Assert.That(new TestAnd().Evaluate(in context), Is.True);
            Assert.That(new TestAnd(null, Positive(), null).Evaluate(in context), Is.True);
            Assert.That(new TestAnd(null, Const(false)).Evaluate(in context), Is.False);
        }

        [Test]
        public void Or_AnyTrue_EmptyFalse()
        {
            var context = new TestContext(-1);

            Assert.That(new TestOr(Positive(), Const(true)).Evaluate(in context), Is.True);
            Assert.That(new TestOr(Positive(), Const(false)).Evaluate(in context), Is.False);
            Assert.That(new TestOr().Evaluate(in context), Is.False);
            Assert.That(new TestOr(null, Const(true)).Evaluate(in context), Is.True);
        }

        [Test]
        public void Not_Inverts_NullFalse()
        {
            var positive = new TestContext(3);
            var negative = new TestContext(-3);

            Assert.That(new TestNot(Positive()).Evaluate(in positive), Is.False);
            Assert.That(new TestNot(Positive()).Evaluate(in negative), Is.True);
            Assert.That(new TestNot(null).Evaluate(in positive), Is.False);
        }

        [Test]
        public void Composites_Nested_ThreeLevels()
        {
            // (valor > 0 E NÃO (falso OU valor > 0)) é sempre falso; trocar o Or interno por And(falso) inverte.
            var context = new TestContext(2);
            var alwaysFalse = new TestAnd(Positive(), new TestNot(new TestOr(Const(false), Positive())));
            var alwaysTrue = new TestAnd(Positive(), new TestNot(new TestAnd(Const(false), Positive())));

            Assert.That(alwaysFalse.Evaluate(in context), Is.False);
            Assert.That(alwaysTrue.Evaluate(in context), Is.True);
        }

        [Test]
        public void Evaluate_DoesNotAllocate()
        {
            var condition = new TestAnd(Positive(), new TestOr(Const(false), new TestNot(Const(false))), null);
            var context = new TestContext(1);
            bool result = condition.Evaluate(in context);

            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    result &= condition.Evaluate(in context);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(result, Is.True);
        }

        [Test]
        public void Validate_OrEmptyWarning_NotNullError()
        {
            var root = new TestAnd(new TestOr(), new TestNot(null), new TestNot(new TestOr(Positive())));
            var result = new ValidationResult(null);

            root.Validate(result, "rule");

            Assert.That(result.Messages.Select(m => (m.Severity, m.PropertyPath)), Is.EqualTo(new[]
            {
                (ValidationSeverity.Warning, "rule.conditions.Array.data[0]"),
                (ValidationSeverity.Error, "rule.conditions.Array.data[1].condition"),
            }));
            Assert.That(result.Messages.All(m => !string.IsNullOrEmpty(m.Message)), Is.True);
        }

        [Test]
        public void DefaultFailureKey_NullByDefault_OverridableAsTextKey()
        {
            Assert.That(Positive().DefaultFailureKey, Is.Null);
            Assert.That(new KeyedCondition().DefaultFailureKey, Is.EqualTo("test.keyed"));
        }

        private static Condition<TestContext> Positive() => new IsPositive();

        private static Condition<TestContext> Const(bool value) => new ConstCondition(value);

        private readonly struct TestContext
        {
            public TestContext(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        [Serializable]
        private sealed class IsPositive : Condition<TestContext>
        {
            public override bool Evaluate(in TestContext context) => context.Value > 0;
        }

        [Serializable]
        private sealed class ConstCondition : Condition<TestContext>
        {
            private readonly bool _value;

            public ConstCondition(bool value)
            {
                _value = value;
            }

            public override bool Evaluate(in TestContext context) => _value;
        }

        [Serializable]
        private sealed class KeyedCondition : Condition<TestContext>
        {
            public override string DefaultFailureKey => "test.keyed";

            public override bool Evaluate(in TestContext context) => true;
        }

        [Serializable]
        private sealed class TestAnd : AndConditionBase<TestContext>
        {
            public TestAnd(params Condition<TestContext>[] items)
            {
                conditions = new List<Condition<TestContext>>(items);
            }
        }

        [Serializable]
        private sealed class TestOr : OrConditionBase<TestContext>
        {
            public TestOr(params Condition<TestContext>[] items)
            {
                conditions = new List<Condition<TestContext>>(items);
            }
        }

        [Serializable]
        private sealed class TestNot : NotConditionBase<TestContext>
        {
            public TestNot(Condition<TestContext> inner)
            {
                condition = inner;
            }
        }
    }
}
