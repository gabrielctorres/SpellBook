using System;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.Events;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Void = SpellBook.Core.Void;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>
    /// Tabela de <see cref="GasPayloadSource"/> por tipo de canal e validação da fonte na <see cref="RaiseEventAction"/>
    /// (CA23, parte).
    /// </summary>
    public class GasPayloadSourceTests
    {
        private GasFactory _gas;
        private EventsFactory _events;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _events = new EventsFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _events.Dispose();
        }

        [Test]
        public void Enum_Order()
        {
            var values = (GasPayloadSource[])Enum.GetValues(typeof(GasPayloadSource));

            Assert.That(values, Is.EqualTo(new[]
            {
                GasPayloadSource.Fixed, GasPayloadSource.AttributeValue, GasPayloadSource.AbilityName,
                GasPayloadSource.SourceObject, GasPayloadSource.TargetObject, GasPayloadSource.EventData
            }));
            Assert.That((int)GasPayloadSource.Fixed, Is.EqualTo(0), "Fixed precisa ser o padrão serializado");
        }

        [TestCase(GasPayloadSource.Fixed, typeof(int), true)]
        [TestCase(GasPayloadSource.Fixed, typeof(Void), true)]
        [TestCase(GasPayloadSource.AttributeValue, typeof(float), true)]
        [TestCase(GasPayloadSource.AttributeValue, typeof(int), false)]
        [TestCase(GasPayloadSource.AbilityName, typeof(string), true)]
        [TestCase(GasPayloadSource.AbilityName, typeof(float), false)]
        [TestCase(GasPayloadSource.SourceObject, typeof(GameObject), true)]
        [TestCase(GasPayloadSource.SourceObject, typeof(string), false)]
        [TestCase(GasPayloadSource.TargetObject, typeof(GameObject), true)]
        [TestCase(GasPayloadSource.TargetObject, typeof(Void), false)]
        [TestCase(GasPayloadSource.EventData, typeof(AbilityEventData), true)]
        [TestCase(GasPayloadSource.EventData, typeof(EffectEventData), false)]
        [TestCase(GasPayloadSource.EventData, typeof(GameObject), false)]
        public void Supports_Table(GasPayloadSource source, Type payloadType, bool expected)
        {
            Assert.That(GasPayloadSourceUtility.Supports(source, payloadType), Is.EqualTo(expected));
        }

        [Test]
        public void Supports_NullType_OnlyFixed()
        {
            Assert.That(GasPayloadSourceUtility.Supports(GasPayloadSource.Fixed, null), Is.True);
            Assert.That(GasPayloadSourceUtility.Supports(GasPayloadSource.AbilityName, null), Is.False);
        }

        [Test]
        public void UnsupportedSourceForChannel_ValidatorError()
        {
            RaiseEventAction action = CreateAction(_events.CreateChannel<IntEventChannel>(), GasPayloadSource.AbilityName, null);
            var result = new ValidationResult(null);

            action.Validate(result);

            Assert.That(result.HasErrors, Is.True, "fonte AbilityName num canal Int não deu erro");
        }

        [Test]
        public void AttributeValueWithoutAttribute_ValidatorError()
        {
            RaiseEventAction action = CreateAction(_events.CreateChannel<FloatEventChannel>(), GasPayloadSource.AttributeValue, null);
            var result = new ValidationResult(null);

            action.Validate(result);

            Assert.That(result.HasErrors, Is.True, "AttributeValue sem atributo não deu erro");
        }

        [Test]
        public void AttributeValueWithAttribute_FloatChannel_NoIssues()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            RaiseEventAction action = CreateAction(_events.CreateChannel<FloatEventChannel>(), GasPayloadSource.AttributeValue, health);
            var result = new ValidationResult(null);

            action.Validate(result);

            Assert.That(result.Messages, Is.Empty);
        }

        // Ação dentro de uma habilidade, configurada pelo SerializedProperty, como o Inspector faz.
        private RaiseEventAction CreateAction(EventChannel channel, GasPayloadSource source, AttributeDefinition attribute)
        {
            GameplayAbility ability = _gas.CreateAbility(new RaiseEventAction());
            using (var serialized = new SerializedObject(ability))
            {
                SerializedProperty action = serialized.FindProperty(nameof(GameplayAbility.OnActivate)).GetArrayElementAtIndex(0);
                action.FindPropertyRelative("emission").managedReferenceValue = EventEmission.CreateFor(channel);
                action.FindPropertyRelative("payloadSource").enumValueIndex = (int)source;
                action.FindPropertyRelative("attribute").objectReferenceValue = attribute;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return (RaiseEventAction)ability.OnActivate[0];
        }
    }
}
