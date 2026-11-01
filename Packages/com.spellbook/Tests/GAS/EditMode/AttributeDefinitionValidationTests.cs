using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.Attributes;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Validação do <see cref="AttributeDefinition"/> (B-005, AR6: R26, CA7, CA8, CA32).</summary>
    public class AttributeDefinitionValidationTests
    {
        private GasFactory _gas;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void Defaults_NoMessages()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");

            Assert.That(Validate(health), Is.Empty);
        }

        [Test]
        public void MinAboveMax_Error()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            health.MinValue = 10f;
            health.MaxValue = 5f;

            ValidationMessage message = Validate(health).Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.PropertyPath, Is.EqualTo(nameof(AttributeDefinition.MinValue)));
        }

        [Test]
        public void MinAboveMax_WithAttributeSource_NoConstantError()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AttributeDefinition maxHealth = _gas.CreateAttribute("MaxHealth");
            health.MinValue = 10f;
            health.MaxValue = 5f;
            health.MaxSource = LimitSource.Attribute;
            health.MaxAttribute = maxHealth;

            Assert.That(Validate(health), Is.Empty);
        }

        [Test]
        public void SelfSource_Error()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            health.MaxSource = LimitSource.Attribute;
            health.MaxAttribute = health;

            ValidationMessage message = Validate(health).Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.PropertyPath, Is.EqualTo(nameof(AttributeDefinition.MaxAttribute)));
        }

        [Test]
        public void MissingSourceAttribute_Error()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            health.MinSource = LimitSource.Attribute;

            ValidationMessage message = Validate(health).Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.PropertyPath, Is.EqualTo(nameof(AttributeDefinition.MinAttribute)));
        }

        [Test]
        public void CycleAB_Error()
        {
            AttributeDefinition a = _gas.CreateAttribute("A");
            AttributeDefinition b = _gas.CreateAttribute("B");
            a.MaxSource = LimitSource.Attribute;
            a.MaxAttribute = b;
            b.MinSource = LimitSource.Attribute;
            b.MinAttribute = a;

            ValidationMessage message = Validate(a).Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.PropertyPath, Is.EqualTo(nameof(AttributeDefinition.MaxAttribute)));
            StringAssert.Contains("B", message.Message);
        }

        [Test]
        public void SourceCycleNotThroughThis_NoError()
        {
            AttributeDefinition a = _gas.CreateAttribute("A");
            AttributeDefinition b = _gas.CreateAttribute("B");
            AttributeDefinition c = _gas.CreateAttribute("C");
            a.MaxSource = LimitSource.Attribute;
            a.MaxAttribute = b;
            b.MaxSource = LimitSource.Attribute;
            b.MaxAttribute = c;
            c.MaxSource = LimitSource.Attribute;
            c.MaxAttribute = b;

            Assert.That(Validate(a), Is.Empty);
        }

        [Test]
        public void DerivedSourceDependingOnThis_Error()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AttributeDefinition maxHealth = _gas.CreateAttribute("MaxHealth");
            maxHealth.Type = AttributeType.Derived;
            maxHealth.Formula = CreateFormulaOf(health);
            health.MaxSource = LimitSource.Attribute;
            health.MaxAttribute = maxHealth;

            ValidationMessage message = Validate(health).Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.PropertyPath, Is.EqualTo(nameof(AttributeDefinition.MaxAttribute)));
        }

        [Test]
        public void NegativeDecimals_Error()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            health.Display.Decimals = -1;

            ValidationMessage message = Validate(health).Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.PropertyPath, Is.EqualTo("Display.Decimals"));
        }

        [Test]
        public void HiddenWithoutCategory_Warning()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            health.Display.Hidden = true;

            ValidationMessage message = Validate(health).Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(message.PropertyPath, Is.EqualTo("Display.Category"));
        }

        [Test]
        public void HiddenWithCategory_NoMessages()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            var category = ScriptableObject.CreateInstance<AttributeCategory>();
            _created.Add(category);
            health.Display.Hidden = true;
            health.Display.Category = category;

            Assert.That(Validate(health), Is.Empty);
        }

        [Test]
        public void ProjectValidator_ReportsDefinitionAsContext()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            health.MinValue = 10f;
            health.MaxValue = 5f;
            var messages = new List<ValidationMessage>();

            ProjectValidator.Validate(new Object[] { health }, messages);

            ValidationMessage message = messages.Single();
            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.Context, Is.SameAs(health));
            Assert.That(message.PropertyPath, Is.EqualTo(nameof(AttributeDefinition.MinValue)));
        }

        private SimpleAttributeFormula CreateFormulaOf(AttributeDefinition attribute)
        {
            var formula = ScriptableObject.CreateInstance<SimpleAttributeFormula>();
            _created.Add(formula);
            var so = new SerializedObject(formula);
            SerializedProperty attributes = so.FindProperty("attributes");
            attributes.arraySize = 1;
            SerializedProperty entry = attributes.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative(nameof(SimpleAttributeFormula.AttributeComponent.Attribute)).objectReferenceValue = attribute;
            entry.FindPropertyRelative(nameof(SimpleAttributeFormula.AttributeComponent.Multiplier)).floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return formula;
        }

        private static IReadOnlyList<ValidationMessage> Validate(Object target)
        {
            return SpellBookEditor.CollectMessages(new[] { target });
        }
    }
}
