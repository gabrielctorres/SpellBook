using System;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Conditions;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Description;
using SpellBook.GAS.Text;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Motivo de falha de ativação como linha de descrição (Spec do B-008, Design 6; CA11).</summary>
    public class ActivationFailureDescriberTests
    {
        private GasFactory _gas;

        [SetUp]
        public void SetUp() => _gas = new GasFactory();

        [TearDown]
        public void TearDown() => _gas.Dispose();

        [Test]
        public void RuleFailure_KeyAndTag_Harmful()
        {
            GameplayTag tooFar = _gas.CreateTag("Failure.TooFar");
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule(new HasTargetPointCondition(), tooFar, "game.failure.too_far"));
            asc.GiveAbility(ability);
            asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure failure);
            DescriptionContext context = Context();

            ActivationFailureDescriber.Describe(failure, context);

            Assert.That(context.Lines.Count, Is.EqualTo(1));
            DescriptionLine line = context.Lines[0];
            Assert.That(line.Kind, Is.EqualTo(DescriptionLineKind.Failure));
            Assert.That(line.Label.Key, Is.EqualTo("game.failure.too_far"));
            Assert.That(line.Tag, Is.EqualTo(tooFar));
            Assert.That(line.Role, Is.EqualTo(TextRole.Harmful));
            Assert.That(line.Flags, Is.EqualTo(DescriptionLineFlags.None));
        }

        [Test]
        public void ResultWithoutKey_MapsToPackageKey()
        {
            GameplayTag stunned = _gas.CreateTag("State.Stunned");
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility ability = _gas.CreateAbility();
            ability.BlockedTags.Add(stunned);
            asc.GiveAbility(ability);
            asc.AddTag(stunned);
            asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure blocked);
            asc.GetActivationResult(_gas.CreateAbility(), AbilityTarget.None, out ActivationFailure notGranted);
            DescriptionContext context = Context();

            ActivationFailureDescriber.Describe(blocked, context);
            ActivationFailureDescriber.Describe(notGranted, context);

            Assert.That(context.Lines.Count, Is.EqualTo(2), "acrescenta sem limpar");
            Assert.That(context.Lines[0].Label.Key, Is.EqualTo("gas.activation.blocked_by_tags"));
            Assert.That(context.Lines[0].Tag, Is.EqualTo(stunned));
            Assert.That(context.Lines[1].Label.Key, Is.EqualTo("gas.activation.not_granted"));
            Assert.That(context.Lines[1].Tag.IsValid, Is.False);
            Assert.That(context.Lines[1].Role, Is.EqualTo(TextRole.Harmful));
        }

        [Test]
        public void RuleWithoutKey_BlockedByConditionKey()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule(new NeverCondition()));
            asc.GiveAbility(ability);
            asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure failure);
            DescriptionContext context = Context();

            ActivationFailureDescriber.Describe(failure, context);

            Assert.That(failure.ReasonKey, Is.Null, "a condição de teste não tem chave padrão");
            Assert.That(context.Lines[0].Label.Key, Is.EqualTo(GasTextKeys.ActivationBlockedByCondition));
        }

        [Test]
        public void Success_NoLine()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility ability = _gas.CreateAbility();
            asc.GiveAbility(ability);
            asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure success);
            DescriptionContext context = Context();

            ActivationFailureDescriber.Describe(success, context);
            ActivationFailureDescriber.Describe(default, context);

            Assert.That(success.IsSuccess, Is.True);
            Assert.That(context.Lines, Is.Empty);
        }

        [TestCase(AbilityActivationResult.InvalidAbility, "gas.activation.invalid")]
        [TestCase(AbilityActivationResult.NotGranted, "gas.activation.not_granted")]
        [TestCase(AbilityActivationResult.AlreadyActive, "gas.activation.already_active")]
        [TestCase(AbilityActivationResult.BlockedByTags, "gas.activation.blocked_by_tags")]
        [TestCase(AbilityActivationResult.MissingRequiredTags, "gas.activation.missing_required_tags")]
        [TestCase(AbilityActivationResult.BlockedByActiveAbility, "gas.activation.blocked_by_active_ability")]
        [TestCase(AbilityActivationResult.OnCooldown, "gas.activation.on_cooldown")]
        [TestCase(AbilityActivationResult.CannotAffordCost, "gas.activation.cannot_afford_cost")]
        [TestCase(AbilityActivationResult.BlockedByCondition, "gas.activation.blocked_by_condition")]
        [TestCase(AbilityActivationResult.NoCharges, "gas.activation.no_charges")]
        [TestCase(AbilityActivationResult.Success, null)]
        public void ForResult_EveryFailureHasKey_SuccessNull(AbilityActivationResult result, string expected)
        {
            Assert.That(GasTextKeys.ForResult(result), Is.EqualTo(expected));
        }

        [Test]
        public void ForResult_CoversEveryEnumValue()
        {
            foreach (AbilityActivationResult result in Enum.GetValues(typeof(AbilityActivationResult)))
            {
                if (result == AbilityActivationResult.Success) continue;
                Assert.That(GasTextKeys.ForResult(result), Is.Not.Null.And.Not.Empty, result.ToString());
            }
        }

        [Test]
        public void Writer_FailureLine_ProviderTextOrKey()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            asc.GetActivationResult(_gas.CreateAbility(), AbilityTarget.None, out ActivationFailure notGranted);
            DescriptionContext withoutProvider = Context();
            DescriptionContext withProvider = Context();
            withProvider.Text = new KeyProvider(GasTextKeys.ActivationNotGranted, "Habilidade não aprendida");
            ActivationFailureDescriber.Describe(notGranted, withoutProvider);
            ActivationFailureDescriber.Describe(notGranted, withProvider);
            var buffer = new TextBuffer();

            DescriptionWriter.WriteLine(withoutProvider.Lines[0], withoutProvider, buffer);
            string keyText = buffer.ToString();
            buffer.Clear();
            DescriptionWriter.WriteLine(withProvider.Lines[0], withProvider, buffer);

            Assert.That(keyText, Is.EqualTo("gas.activation.not_granted"), "sem provedor, a chave (Q5)");
            Assert.That(buffer.ToString(), Is.EqualTo("Habilidade não aprendida"));
        }

        [Test]
        public void Describe_DoesNotAllocate()
        {
            GameplayTag tooFar = _gas.CreateTag("Failure.TooFar");
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule(new HasTargetPointCondition(), tooFar));
            asc.GiveAbility(ability);
            asc.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure rule);
            asc.GetActivationResult(_gas.CreateAbility(), AbilityTarget.None, out ActivationFailure notGranted);
            DescriptionContext context = Context();
            TestDelegate describe10000 = () =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    context.Reset(ability, 1);
                    ActivationFailureDescriber.Describe(rule, context);
                    ActivationFailureDescriber.Describe(notGranted, context);
                }
            };
            describe10000(); // aquecimento: capacidade da lista

            Assert.That(describe10000, Is.Not.AllocatingGCMemory());
            Assert.That(context.Lines.Count, Is.EqualTo(2));
        }

        [Test]
        public void Describe_NullContext_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ActivationFailureDescriber.Describe(default, null));
        }

        private static DescriptionContext Context()
        {
            var context = new DescriptionContext();
            context.Reset(null, 1);
            return context;
        }

        /// <summary>Condição que sempre falha e não tem chave padrão.</summary>
        [Serializable]
        private sealed class NeverCondition : Condition<ConditionContext>
        {
            public override bool Evaluate(in ConditionContext context) => false;
        }

        /// <summary>Provedor com uma chave, na cultura invariante.</summary>
        private sealed class KeyProvider : ITextProvider
        {
            private readonly string _key;
            private readonly string _text;

            public KeyProvider(string key, string text)
            {
                _key = key;
                _text = text;
            }

            public IFormatProvider Culture => CultureInfo.InvariantCulture;

            public event Action Changed
            {
                add { }
                remove { }
            }

            public string GetText(string key, string fallback) => key == _key ? _text : fallback;
        }
    }
}
