using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Abilities.Triggers;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>Validação do modelo evento → ações → alvos (EV16: CA14, CA32 e CA41).</summary>
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
        public void ChannelTick_OutsideChannel_Warns()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.OnChannelTick.Add(new WaitAction());

            ValidationMessage message = AssertSingle(ability, ValidationSeverity.Warning, nameof(GameplayAbility.OnChannelTick));
            StringAssert.Contains("1 ações", message.Message);
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
        public void GameplayEventTrigger_InvalidTag_Error()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Triggers.Add(new GameplayEventTrigger());

            AssertSingle(ability, ValidationSeverity.Error, "triggers.Array.data[0].tag");
        }

        [Test]
        public void Trigger_TagSentToOwnerBySelf_Warns()
        {
            GameplayTag hit = _gas.CreateTag("Event.Hit");
            GameplayAbility ability = _gas.CreateAbility(new SendGameplayEventAction { Tag = hit, Target = new CasterSelector() });
            ability.Triggers.Add(new GameplayEventTrigger { Tag = hit });

            AssertSingle(ability, ValidationSeverity.Warning, "triggers.Array.data[0].tag");
        }

        [TestCase(true, nameof(GameplayAbility.CostEffect))]
        [TestCase(false, nameof(GameplayAbility.CooldownEffect))]
        public void Passive_WithCostOrCooldown_Warns(bool cost, string path)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Behavior = AbilityBehavior.Passive;
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            if (cost) ability.CostEffect = effect;
            else ability.CooldownEffect = effect;

            AssertSingle(ability, ValidationSeverity.Warning, path);
        }

        [Test]
        public void Channel_WithoutDurationNorCancel_Warns()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Behavior = AbilityBehavior.Channel;

            AssertSingle(ability, ValidationSeverity.Warning, "channelDuration");
        }

        [Test]
        public void Manual_WithoutCommit_Error()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.CommitMode = CommitMode.Manual;

            AssertSingle(ability, ValidationSeverity.Error, "commitMode");
        }

        [Test]
        public void Commit_WithOnStart_Warns()
        {
            var parallel = new ParallelAction { Actions = new List<GameplayAction> { new CommitAction() } };
            GameplayAbility ability = _gas.CreateAbility(parallel);

            AssertSingle(ability, ValidationSeverity.Warning, Action0 + ".Actions.Array.data[0]");
        }

        [Test]
        public void RemoveEffect_Empty_Error()
        {
            GameplayAbility ability = _gas.CreateAbility(new RemoveEffectAction());

            AssertSingle(ability, ValidationSeverity.Error, Action0 + ".effect");
        }

        [TestCase(AbilityBehavior.Passive)]
        [TestCase(AbilityBehavior.Toggle)]
        public void Autocast_InPassiveOrToggle_Error(AbilityBehavior behavior)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Behavior = behavior;
            ability.Autocast.Allowed = true;
            ability.Triggers.Add(new WhenReadyTrigger());

            AssertSingle(ability, ValidationSeverity.Error, "autocast.allowed");
        }

        [Test]
        public void Autocast_WithoutTrigger_Warns()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Autocast.Allowed = true;
            ability.Triggers.Add(new GameplayEventTrigger { Tag = _gas.CreateTag("Event.Hit") });

            AssertSingle(ability, ValidationSeverity.Warning, "autocast.allowed");
        }

        [Test]
        public void WhenReady_SelectorWithoutCastRange_Warns()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Triggers.Add(new WhenReadyTrigger { Selector = new CasterSelector() });

            AssertSingle(ability, ValidationSeverity.Warning, "triggers.Array.data[0].selector");
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

        [TestCase(true, "activationConditions.Array.data[0].condition")]
        [TestCase(false, Action0 + ".Condition")]
        public void TagQueryTargetUnit_OutsideCandidateFilter_Warns(bool inActivationRule, string path)
        {
            var query = new TagQueryCondition { Subject = ConditionSubject.TargetUnit };
            query.Query.All.Add(_gas.CreateTag("State.Burning"));
            GameplayAbility ability;
            if (inActivationRule)
            {
                ability = _gas.CreateAbility();
                ability.ActivationConditions.Add(new ActivationRule(new NotCondition { Condition = query }));
            }
            else
            {
                ability = _gas.CreateAbility(new ConditionalAction { Condition = query });
            }

            AssertSingle(ability, ValidationSeverity.Warning, path);
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
            GameplayTag ping = _gas.CreateTag("Event.Ping");
            var delayed = new DelayedAction { Delay = ScalableFloat.Constant(0.5f), Actions = new List<GameplayAction> { new CommitAction() } };
            var forEach = new ForEachTargetAction { Actions = new List<GameplayAction> { delayed } };
            var chance = new ChanceAction { Probability = ScalableFloat.Constant(1f), Actions = new List<GameplayAction> { new RemoveEffectAction { Tag = hit } } };
            GameplayAbility ability = _gas.CreateAbility(forEach, chance, new SendGameplayEventAction { Tag = ping, Target = new CasterSelector() });
            ability.OnGranted.Add(new SpawnProjectileAction
            {
                OnHitActions = new List<GameplayAction> { new WaitAction { Target = new EventTargetsSelector() } }
            });
            ability.CommitMode = CommitMode.Manual;
            ability.Behavior = AbilityBehavior.Channel;
            ability.ChannelDuration = ScalableFloat.Constant(2f);
            ability.OnChannelTick.Add(new WaitAction());
            ability.CastRange = ScalableFloat.Constant(5f);
            ability.Autocast.Allowed = true;
            ability.Triggers.Add(new GameplayEventTrigger { Tag = hit });
            ability.Triggers.Add(new WhenReadyTrigger { Selector = new CasterSelector() });
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
