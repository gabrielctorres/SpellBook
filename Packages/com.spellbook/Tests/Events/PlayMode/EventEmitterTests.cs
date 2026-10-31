using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Tests
{
    /// <summary>
    /// <see cref="EventEmitter"/> dispara o canal com o payload da emissão (CA20), também com um payload próprio de
    /// ponta a ponta (CA22). Emissor sem emissão ou sem canal é erro de validação (CA35, parte).
    /// </summary>
    public class EventEmitterTests
    {
        private EventsFactory _events;
        private readonly List<Object> _objects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _events = new EventsFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _objects)
            {
                if (obj != null) Object.Destroy(obj);
            }

            _objects.Clear();
            _events.Dispose();
        }

        [Test]
        public void Emitter_Raise_RaisesConfiguredPayload()
        {
            var channel = _events.CreateChannel<FloatEventChannel>();
            EventEmitter emitter = CreateEmitter(channel, p => p.floatValue = 2.5f);
            var received = new List<float>();
            Action<float> onRaised = received.Add;
            channel.Raised += onRaised;

            emitter.Raise();
            channel.Raised -= onRaised;

            Assert.That(received, Is.EqualTo(new[] { 2.5f }));
        }

        [Test]
        public void CustomPayload_EndToEnd_EmitterChannelListenerReaction()
        {
            var source = _events.CreateChannel<TestPayloadEventChannel>();
            var forwarded = _events.CreateChannel<TestPayloadEventChannel>();
            EventEmitter emitter = CreateEmitter(source, p =>
            {
                p.FindPropertyRelative("Amount").intValue = 10;
                p.FindPropertyRelative("Label").stringValue = "fogo";
            });
            CreateForwardingListener(source, forwarded);
            var received = new List<TestPayload>();
            Action<TestPayload> onForwarded = received.Add;
            forwarded.Raised += onForwarded;

            emitter.Raise();
            forwarded.Raised -= onForwarded;

            Assert.That(received.Count, Is.EqualTo(1), "a reação não repassou o disparo uma vez");
            Assert.That(received[0].Amount, Is.EqualTo(10));
            Assert.That(received[0].Label, Is.EqualTo("fogo"));
        }

        [Test]
        public void Emitter_NoEmissionOrNoChannel_IsValidationError()
        {
            var withoutEmission = CreateObject("Emitter").AddComponent<EventEmitter>();
            var withoutChannel = CreateObject("Emitter").AddComponent<EventEmitter>();
            var serialized = new SerializedObject(withoutChannel);
            serialized.FindProperty("emission").managedReferenceValue = new EventEmission<float>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var noEmission = new ValidationResult(withoutEmission);
            var noChannel = new ValidationResult(withoutChannel);

            withoutEmission.Validate(noEmission);
            withoutChannel.Validate(noChannel);

            Assert.That(noEmission.HasErrors, Is.True, "emissor sem emissão não deu erro");
            Assert.That(noChannel.HasErrors, Is.True, "emissão sem canal não deu erro");
        }

        [Test]
        public void Emitter_WithChannel_HasNoValidationIssues()
        {
            EventEmitter emitter = CreateEmitter(_events.CreateChannel<VoidEventChannel>(), _ => { });
            var result = new ValidationResult(emitter);

            emitter.Validate(result);

            Assert.That(result.Messages, Is.Empty);
        }

        private GameObject CreateObject(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        // Emissor com a emissão criada a partir do canal e o payload configurado pelo SerializedProperty, como o
        // Inspector faz.
        private EventEmitter CreateEmitter(EventChannel channel, Action<SerializedProperty> configurePayload)
        {
            var emitter = CreateObject("Emitter").AddComponent<EventEmitter>();
            var serialized = new SerializedObject(emitter);
            SerializedProperty emission = serialized.FindProperty("emission");
            emission.managedReferenceValue = EventEmission.CreateFor(channel);
            configurePayload(emission.FindPropertyRelative("payload"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return emitter;
        }

        // Ouvinte de payload próprio com um RaiseChannel em ForwardPayload para outro canal do mesmo tipo.
        private void CreateForwardingListener(EventChannel source, EventChannel target)
        {
            GameObject go = CreateObject("Listener");
            go.SetActive(false);
            var listener = go.AddComponent<EventListener>();

            var serialized = new SerializedObject(listener);
            SerializedProperty responses = serialized.FindProperty("responses");
            responses.arraySize = 1;
            SerializedProperty response = responses.GetArrayElementAtIndex(0);
            response.managedReferenceValue = new TestPayloadEventResponse();
            response.FindPropertyRelative("channel").objectReferenceValue = source;

            SerializedProperty reactions = response.FindPropertyRelative("reactions");
            reactions.arraySize = 1;
            SerializedProperty reaction = reactions.GetArrayElementAtIndex(0);
            reaction.managedReferenceValue = new RaiseChannelReaction();
            reaction.FindPropertyRelative("mode").intValue = (int)RaiseChannelReaction.Mode.ForwardPayload;
            reaction.FindPropertyRelative("target").objectReferenceValue = target;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
        }
    }
}
