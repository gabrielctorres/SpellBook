using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.GAS.Values;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>ScalableFloat em constante, array e curva (B-007, VN1: CA1 a CA6 e parte do CA26).</summary>
    public class ScalableFloatTests
    {
        [Test]
        public void Array_10_8_6_4_Levels1_3_4()
        {
            ScalableFloat value = ScalableFloat.FromArray(10f, 8f, 6f, 4f);

            Assert.That(value.Evaluate(1), Is.EqualTo(10f));
            Assert.That(value.Evaluate(3), Is.EqualTo(6f));
            Assert.That(value.Evaluate(4), Is.EqualTo(4f));
        }

        [Test]
        public void AboveSize_RepeatsLast_ZeroOrNegative_IsLevel1()
        {
            ScalableFloat value = ScalableFloat.FromArray(10f, 8f, 6f, 4f);

            Assert.That(value.Evaluate(6), Is.EqualTo(4f));
            Assert.That(value.Evaluate(0), Is.EqualTo(10f));
            Assert.That(value.Evaluate(-3), Is.EqualTo(10f));
        }

        [Test]
        public void Curve_EvaluatedAtIntegerLevel()
        {
            var curve = new AnimationCurve(new Keyframe(1f, 10f), new Keyframe(3f, 50f), new Keyframe(5f, 20f));
            ScalableFloat value = ScalableFloat.FromCurve(curve);

            Assert.That(value.Evaluate(2), Is.EqualTo(curve.Evaluate(2f)));
            Assert.That(value.Evaluate(3), Is.EqualTo(50f));
            Assert.That(value.Evaluate(0), Is.EqualTo(10f)); // nível 0 vale como 1
        }

        [Test]
        public void Curve_Null_IsZero()
        {
            ScalableFloat value = ScalableFloat.FromCurve(null);

            Assert.That(value.Evaluate(2), Is.EqualTo(0f));
        }

        [Test]
        public void Constant_SameAtAnyLevel()
        {
            ScalableFloat value = ScalableFloat.Constant(7.5f);

            Assert.That(value.Mode, Is.EqualTo(ScalableFloat.ScalableMode.Constant));
            Assert.That(value.Evaluate(1), Is.EqualTo(7.5f));
            Assert.That(value.Evaluate(5), Is.EqualTo(7.5f));
            Assert.That(value.Evaluate(-1), Is.EqualTo(7.5f));
        }

        [Test]
        public void TryParse_SpaceCommaSemicolon_Invariant()
        {
            bool ok = ScalableFloat.TryParse(" 10 8,6;4  1.5 ;; ", out float[] values, out string error);

            Assert.That(ok, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(values, Is.EqualTo(new[] { 10f, 8f, 6f, 4f, 1.5f }));
        }

        [Test]
        public void TryParse_Invalid_ReturnsError()
        {
            bool ok = ScalableFloat.TryParse("10 abc 6", out float[] values, out string error);

            Assert.That(ok, Is.False);
            Assert.That(values, Is.Null);
            Assert.That(error, Does.Contain("abc"));
        }

        [Test]
        public void EmptyArray_Zero_ValidateError()
        {
            ScalableFloat value = ScalableFloat.FromArray();
            var result = new ValidationResult(null);

            value.Validate(result, "duration");

            Assert.That(value.Evaluate(1), Is.EqualTo(0f));
            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Messages[0].PropertyPath, Is.EqualTo("duration"));
        }

        [Test]
        public void NullCurve_ValidateError()
        {
            ScalableFloat value = ScalableFloat.FromCurve(null);
            var result = new ValidationResult(null);

            value.Validate(result, "duration");

            Assert.That(result.HasErrors, Is.True);
        }

        [Test]
        public void ValidValues_ValidateNoMessages()
        {
            var result = new ValidationResult(null);

            ScalableFloat.Constant(3f).Validate(result, "a");
            ScalableFloat.FromArray(1f, 2f).Validate(result, "b");
            ScalableFloat.FromCurve(AnimationCurve.Linear(1f, 1f, 5f, 5f)).Validate(result, "c");

            Assert.That(result.Messages, Is.Empty);
        }

        [Test]
        public void Evaluate_DoesNotAllocate_10000Times()
        {
            ScalableFloat constant = ScalableFloat.Constant(3f);
            ScalableFloat array = ScalableFloat.FromArray(10f, 8f, 6f, 4f);
            ScalableFloat curve = ScalableFloat.FromCurve(AnimationCurve.Linear(1f, 1f, 5f, 5f));
            float sum = constant.Evaluate(1) + array.Evaluate(1) + curve.Evaluate(1); // aquecimento

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    int level = i % 6;
                    sum += constant.Evaluate(level) + array.Evaluate(level) + curve.Evaluate(level);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(sum, Is.GreaterThan(0f));
        }
    }
}
