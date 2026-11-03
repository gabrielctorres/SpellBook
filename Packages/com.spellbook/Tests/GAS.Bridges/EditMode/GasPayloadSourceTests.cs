using System;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.Events;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
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
                GasPayloadSource.SourceObject, GasPayloadSource.TargetObject, GasPayloadSource.EventData,
                GasPayloadSource.ModifierMagnitude, GasPayloadSource.Stacks, GasPayloadSource.EffectName
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

        [TestCase(GasPayloadSource.ModifierMagnitude, typeof(float), true, true)]
        [TestCase(GasPayloadSource.ModifierMagnitude, typeof(float), false, false)]
        [TestCase(GasPayloadSource.ModifierMagnitude, typeof(string), true, false)]
        [TestCase(GasPayloadSource.Stacks, typeof(float), true, true)]
        [TestCase(GasPayloadSource.Stacks, typeof(float), false, false)]
        [TestCase(GasPayloadSource.EffectName, typeof(string), true, true)]
        [TestCase(GasPayloadSource.EffectName, typeof(string), false, false)]
        [TestCase(GasPayloadSource.AbilityName, typeof(string), true, false)]
        [TestCase(GasPayloadSource.AbilityName, typeof(string), false, true)]
        [TestCase(GasPayloadSource.EventData, typeof(EffectEventData), true, true)]
        [TestCase(GasPayloadSource.EventData, typeof(AbilityEventData), true, false)]
        [TestCase(GasPayloadSource.EventData, typeof(AbilityEventData), false, true)]
        [TestCase(GasPayloadSource.AttributeValue, typeof(float), true, true)]
        [TestCase(GasPayloadSource.TargetObject, typeof(GameObject), true, true)]
        [TestCase(GasPayloadSource.Fixed, typeof(int), true, true)]
        public void Supports_EffectList_Table(GasPayloadSource source, Type payloadType, bool inEffectList, bool expected)
        {
            Assert.That(GasPayloadSourceUtility.Supports(source, payloadType, inEffectList), Is.EqualTo(expected));
        }

        [Test]
        public void ModifierMagnitude_OnlyInEffectContext_ValidatorWhenNotSupported()
        {
            var channel = _events.CreateChannel<FloatEventChannel>();
            GameplayAbility ability = _gas.CreateAbility(new RaiseEventAction());
            Configure(ability, nameof(GameplayAbility.OnActivate), channel, GasPayloadSource.ModifierMagnitude, null);
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            effect.OnTick.Add(new RaiseEventAction());
            Configure(effect, nameof(GameplayEffect.OnTick), channel, GasPayloadSource.ModifierMagnitude, null);
            var inAbility = new ValidationResult(ability);
            var inEffect = new ValidationResult(effect);

            ability.Validate(inAbility);
            effect.OnTick[0].Validate(inEffect);

            Assert.That(inAbility.HasErrors, Is.True, "ModifierMagnitude numa lista da habilidade não deu erro");
            Assert.That(inEffect.Messages, Is.Empty, "ModifierMagnitude numa lista do efeito deu mensagem");
        }

        [Test]
        public void NegativeModifierIndex_ValidatorError()
        {
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            effect.OnTick.Add(new RaiseEventAction());
            Configure(effect, nameof(GameplayEffect.OnTick), _events.CreateChannel<FloatEventChannel>(),
                GasPayloadSource.ModifierMagnitude, null, modifierIndex: -1);
            var result = new ValidationResult(effect);

            effect.OnTick[0].Validate(result);

            Assert.That(result.HasErrors, Is.True, "modifierIndex negativo não deu erro");
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
            Configure(ability, nameof(GameplayAbility.OnActivate), channel, source, attribute);
            return (RaiseEventAction)ability.OnActivate[0];
        }

        // Configura a última ação da lista do asset pelo SerializedProperty.
        private static void Configure(Object asset, string list, EventChannel channel, GasPayloadSource source,
            AttributeDefinition attribute, int modifierIndex = 0)
        {
            using (var serialized = new SerializedObject(asset))
            {
                SerializedProperty actions = serialized.FindProperty(list);
                SerializedProperty action = actions.GetArrayElementAtIndex(actions.arraySize - 1);
                action.FindPropertyRelative("emission").managedReferenceValue = EventEmission.CreateFor(channel);
                action.FindPropertyRelative("payloadSource").enumValueIndex = (int)source;
                action.FindPropertyRelative("attribute").objectReferenceValue = attribute;
                action.FindPropertyRelative("modifierIndex").intValue = modifierIndex;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
