using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Events;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>
    /// Payload dinâmico da <see cref="RaiseEventAction"/>: o valor sai do <see cref="AbilityContext"/> na hora do disparo,
    /// e o payload fixo continua igual (CA23).
    /// </summary>
    public class RaiseEventPayloadTests
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
        public void Float_AttributeValue_ReadsCaster()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AbilitySystemComponent caster = _gas.CreateAsc((health, 80f));
            AbilitySystemComponent target = _gas.CreateAsc((health, 30f));
            var channel = _events.CreateChannel<FloatEventChannel>();
            RaiseEventAction action = CreateAction(channel, GasPayloadSource.AttributeValue, health);

            List<float> received = RunAndCollect(channel, action, GasFactory.CreateAbilityContext(caster, Vector3.zero, target));

            Assert.That(received, Is.EqualTo(new[] { 80f }));
        }

        [UnityTest]
        public IEnumerator String_AbilityName()
        {
            var channel = _events.CreateChannel<StringEventChannel>();
            GameplayAbility ability = _gas.CreateAbility(new RaiseEventAction());
            ability.name = "Bola de fogo";
            Configure(ability, channel, GasPayloadSource.AbilityName, null);
            AbilitySystemComponent caster = _gas.CreateAsc();
            caster.GiveAbility(ability);
            var received = new List<string>();
            Action<string> onRaised = received.Add;
            channel.Raised += onRaised;

            caster.TryActivateAbility(ability, Vector3.zero);
            yield return null;
            channel.Raised -= onRaised;

            Assert.That(received, Is.EqualTo(new[] { "Bola de fogo" }));
        }

        [Test]
        public void GameObject_SourceAndFirstValidTarget()
        {
            AbilitySystemComponent caster = _gas.CreateAsc();
            AbilitySystemComponent destroyed = _gas.CreateAsc();
            AbilitySystemComponent target = _gas.CreateAsc();
            Object.DestroyImmediate(destroyed.gameObject);
            var channel = _events.CreateChannel<GameObjectEventChannel>();
            RaiseEventAction sourceAction = CreateAction(channel, GasPayloadSource.SourceObject, null);
            RaiseEventAction targetAction = CreateAction(channel, GasPayloadSource.TargetObject, null);

            List<GameObject> fromSource = RunAndCollect(channel, sourceAction,
                GasFactory.CreateAbilityContext(caster, Vector3.zero, destroyed, target));
            List<GameObject> fromTarget = RunAndCollect(channel, targetAction,
                GasFactory.CreateAbilityContext(caster, Vector3.zero, destroyed, target));

            Assert.That(fromSource, Is.EqualTo(new[] { caster.gameObject }), "SourceObject não é o conjurador");
            Assert.That(fromTarget, Is.EqualTo(new[] { target.gameObject }), "TargetObject não é o primeiro alvo válido");
        }

        [Test]
        public void TargetObject_NoValidTarget_RaisesNothing()
        {
            AbilitySystemComponent caster = _gas.CreateAsc();
            var channel = _events.CreateChannel<GameObjectEventChannel>();
            RaiseEventAction action = CreateAction(channel, GasPayloadSource.TargetObject, null);

            List<GameObject> received = RunAndCollect(channel, action, GasFactory.CreateAbilityContext(caster, Vector3.zero));

            Assert.That(received, Is.Empty);
        }

        [UnityTest]
        public IEnumerator AbilityAction_EventData_FillsAbilityEventData()
        {
            var channel = _events.CreateChannel<AbilityEventChannel>();
            GameplayAbility ability = _gas.CreateAbility(new RaiseEventAction());
            Configure(ability, channel, GasPayloadSource.EventData, null);
            AbilitySystemComponent caster = _gas.CreateAsc();
            caster.GiveAbility(ability);
            var received = new List<AbilityEventData>();
            Action<AbilityEventData> onRaised = received.Add;
            channel.Raised += onRaised;

            caster.TryActivateAbility(ability, Vector3.zero);
            yield return null;
            channel.Raised -= onRaised;

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Owner, Is.SameAs(caster));
            Assert.That(received[0].Ability, Is.SameAs(ability));
            Assert.That(received[0].Level, Is.EqualTo(1));
            Assert.That(received[0].Cancelled, Is.False);
        }

        [Test]
        public void FixedPayload_StillWorks()
        {
            var channel = _events.CreateChannel<IntEventChannel>();
            RaiseEventAction action = CreateAction(channel, GasPayloadSource.Fixed, null, p => p.intValue = 7);

            List<int> received = RunAndCollect(channel, action, GasFactory.CreateAbilityContext(_gas.CreateAsc(), Vector3.zero));

            Assert.That(received, Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void UnsupportedSource_AtRuntime_RaisesNothing_NoException()
        {
            var channel = _events.CreateChannel<IntEventChannel>();
            RaiseEventAction action = CreateAction(channel, GasPayloadSource.AbilityName, null, p => p.intValue = 7);
            AbilityContext context = GasFactory.CreateAbilityContext(_gas.CreateAsc(), Vector3.zero);

            List<int> received = null;
            Assert.DoesNotThrow(() => received = RunAndCollect(channel, action, context));

            Assert.That(received, Is.Empty, "fonte incompatível disparou o canal");
        }

        private static List<T> RunAndCollect<T>(EventChannel<T> channel, RaiseEventAction action, AbilityContext context)
        {
            var received = new List<T>();
            Action<T> onRaised = received.Add;
            channel.Raised += onRaised;
            try
            {
                IEnumerator run = action.Execute(context);
                while (run.MoveNext()) { }
            }
            finally
            {
                channel.Raised -= onRaised;
            }

            return received;
        }

        private RaiseEventAction CreateAction(EventChannel channel, GasPayloadSource source, AttributeDefinition attribute,
            Action<SerializedProperty> configurePayload = null)
        {
            GameplayAbility ability = _gas.CreateAbility(new RaiseEventAction());
            Configure(ability, channel, source, attribute, configurePayload);
            return (RaiseEventAction)ability.Actions[0];
        }

        // Configura a primeira ação da habilidade pelo SerializedProperty, como o Inspector faz.
        private static void Configure(GameplayAbility ability, EventChannel channel, GasPayloadSource source,
            AttributeDefinition attribute, Action<SerializedProperty> configurePayload = null)
        {
            using (var serialized = new SerializedObject(ability))
            {
                SerializedProperty action = serialized.FindProperty(nameof(GameplayAbility.Actions)).GetArrayElementAtIndex(0);
                SerializedProperty emission = action.FindPropertyRelative("emission");
                emission.managedReferenceValue = EventEmission.CreateFor(channel);
                configurePayload?.Invoke(emission.FindPropertyRelative("payload"));
                action.FindPropertyRelative("payloadSource").enumValueIndex = (int)source;
                action.FindPropertyRelative("attribute").objectReferenceValue = attribute;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
