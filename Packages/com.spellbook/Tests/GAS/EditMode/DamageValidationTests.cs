using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Validação do pipeline de dano (B-010, DM7: R23, CA9, CA14, CA26).</summary>
    public class DamageValidationTests
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
        public void ValidPipeline_NoMessages()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AttributeDefinition shield = _gas.CreateAttribute("Shield");
            AttributeDefinition armor = _gas.CreateAttribute("Armor");
            SetProcessors(health, new AbsorbWithAttribute { Shield = shield }, new ReduceByAttribute { SourceAttribute = armor });

            Assert.That(Validate(health), Is.Empty);
        }

        [Test]
        public void NullProcessor_Error()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            SetProcessors(health, new ImmuneToTags(), null);

            AssertSingle(Validate(health), ValidationSeverity.Error, "processors.Array.data[1]");
        }

        [Test]
        public void Absorb_WithoutShield_Error()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            SetProcessors(health, new AbsorbWithAttribute());

            AssertSingle(Validate(health), ValidationSeverity.Error, "processors.Array.data[0].shield");
        }

        [Test]
        public void Absorb_ShieldIsOwner_Error()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            SetProcessors(health, new AbsorbWithAttribute { Shield = health });

            AssertSingle(Validate(health), ValidationSeverity.Error, "processors.Array.data[0].shield");
        }

        [TestCase(-0.1f)]
        [TestCase(1.5f)]
        public void Absorb_FractionOutOfRange_Error(float fraction)
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AttributeDefinition shield = _gas.CreateAttribute("Shield");
            SetProcessors(health, new AbsorbWithAttribute { Shield = shield, Fraction = fraction });

            AssertSingle(Validate(health), ValidationSeverity.Error, "processors.Array.data[0].fraction");
        }

        [Test]
        public void Reduce_WithoutAttribute_Error()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            SetProcessors(health, new ReduceByAttribute());

            AssertSingle(Validate(health), ValidationSeverity.Error, "processors.Array.data[0].sourceAttribute");
        }

        [Test]
        public void MetaWithoutProcessors_Warning()
        {
            AttributeDefinition damage = _gas.CreateAttribute("Damage");
            SetMeta(damage);

            AssertSingle(Validate(damage), ValidationSeverity.Warning, "isMeta");
        }

        [Test]
        public void Meta_RedirectsToItself_CycleError()
        {
            AttributeDefinition damage = _gas.CreateAttribute("Damage");
            SetMeta(damage);
            SetProcessors(damage, new AbsorbWithAttribute { Shield = damage });

            ValidationMessage message = AssertSingle(Validate(damage), ValidationSeverity.Error, "processors.Array.data[0].shield");
            StringAssert.Contains("ciclo", message.Message);
        }

        [Test]
        public void StateTag_MissingDefinition_Error()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            GameplayTag dead = _gas.CreateTag("Dead");
            GameplayTagDefinition definition = dead.GetDefinition();
            using (var so = new SerializedObject(health))
            {
                so.FindProperty("stateTagWhileDepleted.definition").objectReferenceValue = definition;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Object.DestroyImmediate(definition);

            AssertSingle(Validate(health), ValidationSeverity.Error, "stateTagWhileDepleted");
        }

        [Test]
        public void StateTag_Empty_NoMessages()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");

            Assert.That(health.StateTagWhileDepleted.IsValid, Is.False);
            Assert.That(Validate(health), Is.Empty);
        }

        [Test]
        public void InvalidDamageTag_ValidatorPoints()
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            using (var so = new SerializedObject(effect))
            {
                SerializedProperty tags = so.FindProperty("damageTags.tags");
                tags.arraySize = 1;
                tags.GetArrayElementAtIndex(0).FindPropertyRelative("definition").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AssertSingle(Validate(effect), ValidationSeverity.Error, "damageTags.tags.Array.data[0]");
        }

        [TestCase(EffectDurationPolicy.HasDuration)]
        [TestCase(EffectDurationPolicy.Infinite)]
        public void MetaInPersistentModifiers_Error(EffectDurationPolicy policy)
        {
            AttributeDefinition damage = _gas.CreateAttribute("Damage");
            SetMeta(damage);
            GameplayEffect effect = CreateEffectModifying(damage, policy);

            AssertSingle(Validate(effect), ValidationSeverity.Error, "Modifiers.Array.data[0].Attribute");
        }

        [Test]
        public void MetaInInstantModifiers_NoMessages()
        {
            AttributeDefinition damage = _gas.CreateAttribute("Damage");
            SetMeta(damage);
            GameplayEffect effect = CreateEffectModifying(damage, EffectDurationPolicy.Instant);

            Assert.That(Validate(effect), Is.Empty);
        }

        [Test]
        public void Asc_ProcessorReferencesAbsentAttribute_Warning()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AttributeDefinition shield = _gas.CreateAttribute("Shield");
            SetProcessors(health, new AbsorbWithAttribute { Shield = shield });
            AbilitySystemComponent asc = _gas.CreateAsc((health, 100f));

            ValidationMessage message = AssertSingle(Validate(asc), ValidationSeverity.Warning, "attributeSet");
            StringAssert.Contains(shield.name, message.Message);
        }

        [Test]
        public void Asc_ProcessorReferencesPresentAttribute_NoMessages()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AttributeDefinition armor = _gas.CreateAttribute("Armor");
            SetProcessors(health, new ReduceByAttribute { SourceAttribute = armor });
            AbilitySystemComponent asc = _gas.CreateAsc((health, 100f), (armor, 0.5f));

            Assert.That(Validate(asc), Is.Empty);
        }

        private GameplayEffect CreateEffectModifying(AttributeDefinition attribute, EffectDurationPolicy policy)
        {
            GameplayEffect effect = _gas.CreateEffect(policy, 1f);
            effect.Modifiers = new List<AttributeEffectModifier>
            {
                new AttributeEffectModifier { Attribute = attribute, Operation = ModifierOperation.Add }
            };
            return effect;
        }

        private static ValidationMessage AssertSingle(IReadOnlyList<ValidationMessage> messages, ValidationSeverity severity, string path)
        {
            Assert.That(messages.Select(m => $"{m.Severity} {m.PropertyPath}"), Is.EqualTo(new[] { $"{severity} {path}" }));
            return messages[0];
        }

        private static IReadOnlyList<ValidationMessage> Validate(Object target)
        {
            var result = new ValidationResult(target);
            ((IValidatable)target).Validate(result);
            return result.Messages;
        }

        private static void SetMeta(AttributeDefinition definition)
        {
            using (var so = new SerializedObject(definition))
            {
                so.FindProperty("isMeta").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetProcessors(AttributeDefinition definition, params AttributeChangeProcessor[] processors)
        {
            using (var so = new SerializedObject(definition))
            {
                SerializedProperty list = so.FindProperty("processors");
                list.arraySize = processors.Length;
                for (int i = 0; i < processors.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).managedReferenceValue = processors[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
