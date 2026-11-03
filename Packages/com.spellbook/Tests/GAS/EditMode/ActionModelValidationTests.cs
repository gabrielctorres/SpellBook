using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>Validação do modelo evento → ações → alvos (EV16a: percurso das listas, regras das ações e do efeito; CA14 e CA32).</summary>
    public class ActionModelValidationTests
    {
        private const string Action0 = "OnActivate.Array.data[0]";

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
        public void NullAction_Nested_Error()
        {
            GameplayAbility ability = _gas.CreateAbility(new ForEachTargetAction { Actions = new List<GameplayAction> { null } });

            AssertSingle(ability, ValidationSeverity.Error, Action0 + ".actions.Array.data[0]");
        }

        [Test]
        public void EventTargetsSelector_InEventWithoutTargets_Error()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.OnGranted.Add(new WaitAction { Target = new EventTargetsSelector() });

            AssertSingle(ability, ValidationSeverity.Error, "OnGranted.Array.data[0].target");
        }

        [Test]
        public void ForEachTarget_Empty_Warns()
        {
            GameplayAbility ability = _gas.CreateAbility(new ForEachTargetAction());

            AssertSingle(ability, ValidationSeverity.Warning, Action0 + ".actions");
        }

        [Test]
        public void Delayed_NegativeConstant_Error()
        {
            var delayed = new DelayedAction { Delay = ScalableFloat.Constant(-1f) };
            GameplayAbility ability = _gas.CreateAbility(new ChanceAction { Actions = new List<GameplayAction> { delayed } });

            AssertSingle(ability, ValidationSeverity.Error, Action0 + ".actions.Array.data[0].delay");
        }

        [TestCase(-0.1f)]
        [TestCase(1.5f)]
        public void Chance_OutOfRange_Error(float probability)
        {
            GameplayAbility ability = _gas.CreateAbility(new ChanceAction { Probability = ScalableFloat.Constant(probability) });

            AssertSingle(ability, ValidationSeverity.Error, Action0 + ".probability");
        }

        [Test]
        public void RemoveEffect_Empty_Error()
        {
            GameplayAbility ability = _gas.CreateAbility(new RemoveEffectAction());

            AssertSingle(ability, ValidationSeverity.Error, Action0 + ".effect");
        }

        [Test]
        public void OnDeath_WithoutDeathAttribute_Error()
        {
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            effect.OnDeath.Add(new WaitAction());

            AssertSingle(effect, ValidationSeverity.Error, "deathAttribute");
        }

        [Test]
        public void Reaction_InInstantEffect_Warns()
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.OnTakeDamage.Add(new WaitAction());

            AssertSingle(effect, ValidationSeverity.Warning, nameof(GameplayEffect.OnTakeDamage));
        }

        [Test]
        public void Nesting_DeeperThanFour_Warns()
        {
            GameplayAction deepest = new WaitAction();
            string path = string.Empty;
            for (int level = 0; level < 4; level++)
            {
                deepest = new ParallelAction { Actions = new List<GameplayAction> { deepest } };
                path += ".Actions.Array.data[0]";
            }

            GameplayAbility ability = _gas.CreateAbility(deepest);

            AssertSingle(ability, ValidationSeverity.Warning, Action0 + path);
        }

        [Test]
        public void ProjectileOnHit_ValidatedAsOwnEventWithTargets()
        {
            var projectile = new SpawnProjectileAction
            {
                OnHitActions = new List<GameplayAction> { null, new WaitAction { Target = new EventTargetsSelector() } }
            };
            GameplayAbility ability = _gas.CreateAbility();
            ability.OnGranted.Add(projectile);

            AssertSingle(ability, ValidationSeverity.Error, "OnGranted.Array.data[0].OnHitActions.Array.data[0]");
        }

        [Test]
        public void ConfiguredModel_HasNoMessages()
        {
            GameplayTag hit = _gas.CreateTag("Event.Hit");
            var delayed = new DelayedAction { Delay = ScalableFloat.Constant(0.5f), Actions = new List<GameplayAction> { new WaitAction() } };
            var forEach = new ForEachTargetAction { Actions = new List<GameplayAction> { delayed } };
            var chance = new ChanceAction { Probability = ScalableFloat.Constant(1f), Actions = new List<GameplayAction> { new RemoveEffectAction { Tag = hit } } };
            GameplayAbility ability = _gas.CreateAbility(forEach, chance);
            ability.OnGranted.Add(new SpawnProjectileAction
            {
                OnHitActions = new List<GameplayAction> { new WaitAction { Target = new EventTargetsSelector() } }
            });
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            effect.OnTakeDamage.Add(new RemoveEffectAction { Tag = hit });

            Assert.That(Messages(ability), Is.Empty);
            Assert.That(Messages(effect), Is.Empty);
        }

        private static List<string> Messages(Object asset)
        {
            var result = new ValidationResult(asset);
            ((IValidatable)asset).Validate(result);
            return result.Messages.Select(m => $"{m.Severity}: {m.Message} ({m.PropertyPath})").ToList();
        }

        // Uma mensagem só, no objeto validado, com um caminho que o Inspector acha: a mensagem é clicável
        private static ValidationMessage AssertSingle(Object asset, ValidationSeverity severity, string propertyPath)
        {
            var result = new ValidationResult(asset);
            ((IValidatable)asset).Validate(result);

            Assert.That(result.Messages.Select(m => $"{m.Severity}: {m.Message} ({m.PropertyPath})").ToList(), Has.Count.EqualTo(1));
            ValidationMessage message = result.Messages[0];
            Assert.That(message.Severity, Is.EqualTo(severity), message.Message);
            Assert.That(message.Context, Is.SameAs(asset));
            Assert.That(message.PropertyPath, Is.EqualTo(propertyPath));
            Assert.That(new SerializedObject(asset).FindProperty(propertyPath), Is.Not.Null, "o campo existe no asset");
            return message;
        }
    }
}
