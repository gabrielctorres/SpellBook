using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Events.Editor;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Tests
{
    /// <summary>Nome legível de um ouvinte de canal (Spec 0M-35, emenda de 2026-10-31; CA23).</summary>
    public class ListenerLabelTests
    {
        private EventsFactory _events;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _events = new EventsFactory();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }

            _events.Dispose();
        }

        [Test]
        public void Describe_ChannelListenerFromEventListener_ShowsOwnerNameAndType()
        {
            var channel = _events.CreateChannel<VoidEventChannel>();
            _go = new GameObject("Painel");
            var listener = _go.AddComponent<EventListener>();
            EventResponse response = AddVoidResponse(listener, channel);
            response.Subscribe(listener);
            var targets = new List<object>();
            channel.GetListeners(targets);

            string label = ListenerLabel.Describe(targets[0]);

            response.Unsubscribe();
            Assert.That(targets.Count, Is.EqualTo(1));
            Assert.That(label, Is.EqualTo("Painel (EventListener)"));
        }

        [Test]
        public void Describe_ResponseWithoutOwner_ShowsResponseTypeName()
        {
            _go = new GameObject("Painel");
            var listener = _go.AddComponent<EventListener>();
            EventResponse response = AddVoidResponse(listener, _events.CreateChannel<VoidEventChannel>());

            string label = ListenerLabel.Describe(response);

            Assert.That(label, Is.EqualTo(nameof(VoidEventResponse)));
        }

        [Test]
        public void Describe_DestroyedObject_ShowsDestroyed()
        {
            var destroyed = new GameObject("Painel");
            Object.DestroyImmediate(destroyed);

            string label = ListenerLabel.Describe(destroyed);

            Assert.That(label, Is.EqualTo("(destruído)"));
        }

        [Test]
        public void Describe_Null_ShowsNull()
        {
            Assert.That(ListenerLabel.Describe(null), Is.EqualTo("(nulo)"));
        }

        [Test]
        public void Describe_UnityObject_ShowsNameAndType()
        {
            _go = new GameObject("Botao");

            Assert.That(ListenerLabel.Describe(_go), Is.EqualTo("Botao (GameObject)"));
        }

        [Test]
        public void Describe_OtherTarget_ShowsToString()
        {
            Assert.That(ListenerLabel.Describe(7), Is.EqualTo("7"));
        }

        private static EventResponse AddVoidResponse(EventListener listener, VoidEventChannel channel)
        {
            var serialized = new SerializedObject(listener);
            SerializedProperty responses = serialized.FindProperty("responses");
            responses.arraySize = 1;
            SerializedProperty response = responses.GetArrayElementAtIndex(0);
            response.managedReferenceValue = new VoidEventResponse();
            response.FindPropertyRelative("channel").objectReferenceValue = channel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();
            return (EventResponse)serialized.FindProperty("responses").GetArrayElementAtIndex(0).managedReferenceValue;
        }
    }
}
