using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Piso, teto e modos da política de redução de recarga e a validação dela (B-003, EA6: CA20, CA21, C8).</summary>
    public class CooldownReductionPolicyTests
    {
        private const float BaseDuration = 8f;

        private GasFactory _gas;
        private CooldownReductionPolicy _policy;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _policy = ScriptableObject.CreateInstance<CooldownReductionPolicy>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_policy);
            _gas.Dispose();
        }

        [Test]
        public void Percent_Min1_Max50_8sAt90_Is4()
        {
            Configure(1f, 0.5f, CooldownReductionMode.Percent);

            float duration = _policy.Apply(BaseDuration, 0.9f);

            Assert.That(duration, Is.EqualTo(4f).Within(1e-5f));
        }

        [Test]
        public void Percent_Max100_At99_FloorIs1()
        {
            Configure(1f, 1f, CooldownReductionMode.Percent);

            float duration = _policy.Apply(BaseDuration, 0.99f);

            Assert.That(duration, Is.EqualTo(1f));
        }

        [Test]
        public void Haste_DoublesSpeed_HalvesDuration()
        {
            Configure(0.1f, 1f, CooldownReductionMode.Haste);

            float duration = _policy.Apply(BaseDuration, 1f);

            Assert.That(duration, Is.EqualTo(4f).Within(1e-5f));
        }

        [Test]
        public void Haste_CappedByMaxReduction()
        {
            Configure(0.1f, 0.25f, CooldownReductionMode.Haste);

            float duration = _policy.Apply(BaseDuration, 3f);

            Assert.That(duration, Is.EqualTo(6f).Within(1e-5f), "haste 3 reduz 75 %, mas o teto é 25 %");
        }

        [Test]
        public void NoPolicy_SameAsToday_Floor01_Clamp01()
        {
            float quarter = CooldownReductionPolicy.ApplyDefault(BaseDuration, 0.25f);
            float overFull = CooldownReductionPolicy.ApplyDefault(BaseDuration, 1.5f);
            float negative = CooldownReductionPolicy.ApplyDefault(BaseDuration, -0.5f);

            Assert.That(quarter, Is.EqualTo(6f));
            Assert.That(overFull, Is.EqualTo(0.1f), "redução acima de 1 não fica no piso de 0,1 s");
            Assert.That(negative, Is.EqualTo(BaseDuration), "redução negativa não é limitada a 0");
        }

        [Test]
        public void Validate_NegativeMin_Error()
        {
            Configure(-1f, 0.5f, CooldownReductionMode.Percent);

            IReadOnlyList<ValidationMessage> messages = Validate(_policy);

            ValidationMessage message = messages.Single();
            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.PropertyPath, Is.EqualTo("minDuration"));
        }

        [TestCase(-0.1f)]
        [TestCase(1.5f)]
        public void Validate_MaxOutOfRange_Error(float maxReduction)
        {
            Configure(0.1f, maxReduction, CooldownReductionMode.Percent);

            IReadOnlyList<ValidationMessage> messages = Validate(_policy);

            ValidationMessage message = messages.Single();
            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.PropertyPath, Is.EqualTo("maxReduction"));
        }

        [Test]
        public void Validate_Defaults_NoMessages()
        {
            IReadOnlyList<ValidationMessage> messages = Validate(_policy);

            Assert.That(messages, Is.Empty);
        }

        [Test]
        public void ProjectValidator_ReportsPolicyAsContext()
        {
            Configure(-1f, 0.5f, CooldownReductionMode.Percent);
            var messages = new List<ValidationMessage>();

            ProjectValidator.Validate(new Object[] { _policy }, messages);

            ValidationMessage message = messages.Single();
            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.Context, Is.SameAs(_policy));
            Assert.That(message.PropertyPath, Is.EqualTo("minDuration"));
        }

        [Test]
        public void Ability_PolicyWithoutReductionAttribute_Warns()
        {
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(BaseDuration);
            GasFactory.SetObjectField(ability, "cooldownReductionPolicy", _policy);

            IReadOnlyList<ValidationMessage> messages = Validate(ability);

            ValidationMessage message = messages.Single();
            Assert.That(ability.CooldownReductionPolicy, Is.SameAs(_policy));
            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(message.PropertyPath, Is.EqualTo("cooldownReductionPolicy"));
        }

        private static IReadOnlyList<ValidationMessage> Validate(Object target)
        {
            var result = new ValidationResult(target);
            ((IValidatable)target).Validate(result);
            return result.Messages;
        }

        // Os campos são privados serializados: grava como o Inspector.
        private void Configure(float minDuration, float maxReduction, CooldownReductionMode mode)
        {
            using (var serialized = new SerializedObject(_policy))
            {
                serialized.FindProperty("minDuration").floatValue = minDuration;
                serialized.FindProperty("maxReduction").floatValue = maxReduction;
                serialized.FindProperty("mode").enumValueIndex = (int)mode;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
