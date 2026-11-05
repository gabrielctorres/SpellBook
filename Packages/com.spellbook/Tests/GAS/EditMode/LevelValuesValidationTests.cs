using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>Validação dos valores por nível na habilidade e no efeito (B-007, VN9: CA8, CA10, CA15, CA22, CA33).</summary>
    public class LevelValuesValidationTests
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
        public void UnknownTableName_ErrorOnAbility_Clickable()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Values.Add("Damage", ScalableFloat.Constant(10f));
            GameplayEffect cooldown = CreateNamedEffect("GE_Recarga", EffectDurationPolicy.HasDuration);
            cooldown.Duration = ScalableFloat.FromTable("Cooldown");
            ability.CooldownEffect = cooldown;

            ValidationResult result = Validate(ability);

            ValidationMessage error = SingleError(result);
            Assert.That(error.Context, Is.SameAs(ability), "a mensagem seleciona a habilidade");
            Assert.That(error.Message, Does.Contain("Cooldown").And.Contain("GE_Recarga"));
        }

        [Test]
        public void KnownTableName_NoError()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Values.Add("Cooldown", ScalableFloat.FromArray(6f, 5f));
            GameplayEffect cooldown = CreateNamedEffect("GE_Recarga", EffectDurationPolicy.HasDuration);
            cooldown.Duration = ScalableFloat.FromTable("Cooldown");
            ability.CooldownEffect = cooldown;

            ValidationResult result = Validate(ability);

            Assert.That(Errors(result), Is.Empty);
        }

        [Test]
        public void EffectUsedByAbilityWithoutName_ErrorNamesEffect()
        {
            GameplayEffect cost = CreateNamedEffect("GE_Custo", EffectDurationPolicy.Instant);
            cost.Modifiers.Add(ValueModifier(ScalableFloat.FromTable("ManaCost")));
            GameplayEffect damage = CreateNamedEffect("GE_Dano", EffectDurationPolicy.Instant);
            damage.Modifiers.Add(ValueModifier(ScalableFloat.FromTable("Damage")));
            GameplayEffect burn = CreateNamedEffect("GE_Queimadura", EffectDurationPolicy.HasDuration);
            burn.Period = 1f;
            burn.PeriodicModifiers.Add(ValueModifier(ScalableFloat.FromTable("BurnDamage")));
            var repeat = new RepeatAction { Actions = { new ApplyEffectAction { Effects = new List<GameplayEffect> { damage } } } };
            var projectile = new SpawnProjectileAction
            {
                OnHitActions = { new ApplyEffectAction { Effects = new List<GameplayEffect> { burn } } }
            };
            GameplayAbility ability = _gas.CreateAbility(repeat, projectile);
            ability.CostEffect = cost;

            ValidationResult result = Validate(ability);

            List<ValidationMessage> errors = Errors(result);
            Assert.That(errors, Has.Count.EqualTo(3));
            Assert.That(errors.Any(m => m.Message.Contains("GE_Custo") && m.Message.Contains("ManaCost")), Is.True, "custo");
            Assert.That(errors.Any(m => m.Message.Contains("GE_Dano") && m.Message.Contains("Damage")), Is.True, "RepeatAction");
            Assert.That(errors.Any(m => m.Message.Contains("GE_Queimadura") && m.Message.Contains("BurnDamage")), Is.True, "OnHitActions");
            Assert.That(errors.All(m => m.Context == ability), Is.True);
        }

        [Test]
        public void CallerWithoutDefault_Warning()
        {
            GameplayEffect effect = CreateNamedEffect("GE_Dano", EffectDurationPolicy.Instant);
            effect.Modifiers.Add(CallerModifier("Damage", hasDefault: false));

            ValidationResult result = Validate(effect);

            Assert.That(Errors(result), Is.Empty);
            ValidationMessage warning = result.Messages.Single(m => m.Severity == ValidationSeverity.Warning);
            Assert.That(warning.Message, Does.Contain("Damage"));
            Assert.That(warning.Context, Is.SameAs(effect));
        }

        [Test]
        public void CallerNameNotIdentifier_Error()
        {
            GameplayEffect effect = CreateNamedEffect("GE_Dano", EffectDurationPolicy.Instant);
            effect.Modifiers.Add(CallerModifier("Dano extra", hasDefault: true));
            effect.PeriodicModifiers.Add(CallerModifier(string.Empty, hasDefault: true));

            ValidationResult result = Validate(effect);

            Assert.That(Errors(result), Has.Count.EqualTo(2));
        }

        [Test]
        public void LiveSelfDependency_Error()
        {
            AttributeDefinition attack = _gas.CreateAttribute("Attack");
            GameplayEffect effect = CreateNamedEffect("GE_Furia", EffectDurationPolicy.Infinite);
            effect.Modifiers.Add(ReadAttribute(attack, attack, MagnitudeMode.Live));

            ValidationResult result = Validate(effect);

            Assert.That(SingleError(result).Message, Does.Contain("ciclo"));
        }

        [Test]
        public void LiveCrossPair_Error()
        {
            AttributeDefinition attack = _gas.CreateAttribute("Attack");
            AttributeDefinition defense = _gas.CreateAttribute("Defense");
            GameplayEffect effect = CreateNamedEffect("GE_Postura", EffectDurationPolicy.Infinite);
            effect.Modifiers.Add(ReadAttribute(attack, defense, MagnitudeMode.Live));
            effect.Modifiers.Add(ReadAttribute(defense, attack, MagnitudeMode.Live));

            ValidationResult result = Validate(effect);

            Assert.That(SingleError(result).Message, Does.Contain("ciclo"));
        }

        [Test]
        public void SnapshotCrossPair_NoError()
        {
            AttributeDefinition attack = _gas.CreateAttribute("Attack");
            AttributeDefinition defense = _gas.CreateAttribute("Defense");
            GameplayEffect effect = CreateNamedEffect("GE_Postura", EffectDurationPolicy.Infinite);
            effect.Modifiers.Add(ReadAttribute(attack, defense, MagnitudeMode.Live));
            effect.Modifiers.Add(ReadAttribute(defense, attack, MagnitudeMode.Snapshot));

            ValidationResult result = Validate(effect);

            Assert.That(result.Messages, Is.Empty);
        }

        [Test]
        public void MaxLevelBelow1_Error()
        {
            GameplayAbility ability = _gas.CreateAbility();
            SetMaxLevel(ability, 0);

            ValidationResult result = Validate(ability);

            Assert.That(SingleError(result).PropertyPath, Is.EqualTo("maxLevel"));
        }

        [Test]
        public void LiveOnInstant_Warning()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AttributeDefinition power = _gas.CreateAttribute("Power");
            GameplayEffect effect = CreateNamedEffect("GE_Golpe", EffectDurationPolicy.Instant);
            effect.Modifiers.Add(ReadAttribute(health, power, MagnitudeMode.Live));

            ValidationResult result = Validate(effect);

            Assert.That(Errors(result), Is.Empty);
            Assert.That(result.Messages.Single().Message, Does.Contain("Live"));
        }

        [Test]
        public void LiveOnPeriodic_Warning()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AttributeDefinition power = _gas.CreateAttribute("Power");
            GameplayEffect effect = CreateNamedEffect("GE_Veneno", EffectDurationPolicy.Infinite);
            effect.Period = 1f;
            effect.PeriodicModifiers.Add(ReadAttribute(health, power, MagnitudeMode.Live));

            ValidationResult result = Validate(effect);

            Assert.That(Errors(result), Is.Empty);
            Assert.That(result.Messages.Single().Message, Does.Contain("Live"));
        }

        [Test]
        public void MaxLevelAboveArray_Warning()
        {
            GameplayAbility ability = _gas.CreateAbility();
            SetMaxLevel(ability, 5);
            ability.Values.Add("Damage", ScalableFloat.FromArray(10f, 20f, 30f));
            ability.Values.Add("Range", ScalableFloat.FromArray(1f, 2f, 3f, 4f, 5f));

            ValidationResult result = Validate(ability);

            Assert.That(Errors(result), Is.Empty);
            ValidationMessage warning = result.Messages.Single();
            Assert.That(warning.Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(warning.Message, Does.Contain("Damage"));
        }

        [Test]
        public void EmptyArrayInDuration_Error()
        {
            GameplayEffect effect = CreateNamedEffect("GE_Lento", EffectDurationPolicy.HasDuration);
            effect.Duration = ScalableFloat.FromArray();

            ValidationResult result = Validate(effect);

            Assert.That(SingleError(result).PropertyPath, Is.EqualTo("duration"));
        }

        [Test]
        public void Validate_TableNameInOnEndEffect_IsChecked()
        {
            GameplayEffect onActivateEffect = CreateNamedEffect("GE_Dano", EffectDurationPolicy.Instant);
            onActivateEffect.Modifiers.Add(ValueModifier(ScalableFloat.FromTable("Damage")));
            GameplayEffect onEndEffect = CreateNamedEffect("GE_Dano", EffectDurationPolicy.Instant);
            onEndEffect.Modifiers.Add(ValueModifier(ScalableFloat.FromTable("Damage")));
            GameplayAbility inOnActivate = _gas.CreateAbility(new ApplyEffectAction { Effects = new List<GameplayEffect> { onActivateEffect } });
            GameplayAbility inOnEnd = _gas.CreateAbility();
            inOnEnd.OnEnd.Add(new ApplyEffectAction { Effects = new List<GameplayEffect> { onEndEffect } });

            ValidationMessage expected = SingleError(Validate(inOnActivate));
            ValidationMessage error = SingleError(Validate(inOnEnd));

            Assert.That(error.Message, Is.EqualTo(expected.Message));
            Assert.That(error.PropertyPath, Is.EqualTo(expected.PropertyPath));
            Assert.That(error.Context, Is.SameAs(inOnEnd));
        }

        private GameplayEffect CreateNamedEffect(string name, EffectDurationPolicy policy)
        {
            GameplayEffect effect = _gas.CreateEffect(policy, 0f);
            effect.name = name;
            GasFactory.SetObjectField(effect, "icon", _gas.CreateSprite());
            return effect;
        }

        private static AttributeEffectModifier ValueModifier(ScalableFloat value)
        {
            return new AttributeEffectModifier
            {
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = value }
            };
        }

        private static AttributeEffectModifier CallerModifier(string name, bool hasDefault)
        {
            return new AttributeEffectModifier
            {
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource
                {
                    Type = ModifierValueSource.SourceType.SetByCaller,
                    CallerName = name,
                    CallerHasDefault = hasDefault
                }
            };
        }

        private static AttributeEffectModifier ReadAttribute(AttributeDefinition target, AttributeDefinition source, MagnitudeMode mode)
        {
            return new AttributeEffectModifier
            {
                Attribute = target,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Attribute, Attribute = source },
                Mode = mode
            };
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

        private static ValidationResult Validate(GameplayEffect effect)
        {
            var result = new ValidationResult(effect);
            effect.Validate(result);
            return result;
        }

        private static List<ValidationMessage> Errors(ValidationResult result)
        {
            return result.Messages.Where(m => m.Severity == ValidationSeverity.Error).ToList();
        }

        private static ValidationMessage SingleError(ValidationResult result)
        {
            List<ValidationMessage> errors = Errors(result);
            Assert.That(errors, Has.Count.EqualTo(1), string.Join(" | ", result.Messages.Select(m => m.Message)));
            return errors[0];
        }
    }
}
