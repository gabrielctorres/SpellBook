using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Tests
{
    /// <summary>
    /// <see cref="EventListener"/> ligado a um canal pelo Inspector: payload exato, reações na ordem da lista
    /// e depois o escape (CA13); desabilitado não recebe (CA14). O ouvinte é montado pelo formato serializado,
    /// como o Inspector faz.
    /// </summary>
    public class EventListenerTests
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
        public void Listener_ReceivesExactFloat_ReactionsInOrderThenUnityEvent()
        {
            var channel = _events.CreateChannel<FloatEventChannel>();
            Recorder recorder = CreateRecorder();
            CreateListener(channel, recorder, withEscape: true);

            channel.Raise(3.5f);

            Assert.That(recorder.Ids, Is.EqualTo(new[] { "first", "second", "escape" }));
            Assert.That(recorder.Payloads, Is.EqualTo(new object[] { 3.5f, 3.5f, 3.5f }));
        }

        [Test]
        public void Listener_Disabled_DoesNotReceive_ReenabledReceives()
        {
            var channel = _events.CreateChannel<FloatEventChannel>();
            Recorder recorder = CreateRecorder();
            EventListener listener = CreateListener(channel, recorder, withEscape: false);

            listener.enabled = false;
            channel.Raise(1f);
            Assert.That(recorder.Ids, Is.Empty, "desabilitado recebeu o disparo");

            listener.enabled = true;
            channel.Raise(2f);
            Assert.That(recorder.Ids, Is.EqualTo(new[] { "first", "second" }));
            Assert.That(recorder.Payloads, Is.EqualTo(new object[] { 2f, 2f }));
        }

        private Recorder CreateRecorder()
        {
            var go = new GameObject("Recorder");
            _objects.Add(go);
            return go.AddComponent<Recorder>();
        }

        private EventListener CreateListener(FloatEventChannel channel, Recorder recorder, bool withEscape)
        {
            var go = new GameObject("Listener");
            _objects.Add(go);
            go.SetActive(false);
            var listener = go.AddComponent<EventListener>();

            var serialized = new SerializedObject(listener);
            SerializedProperty responses = serialized.FindProperty("responses");
            responses.arraySize = 1;
            SerializedProperty response = responses.GetArrayElementAtIndex(0);
            response.managedReferenceValue = new FloatEventResponse();
            response.FindPropertyRelative("channel").objectReferenceValue = channel;

            SerializedProperty reactions = response.FindPropertyRelative("reactions");
            reactions.arraySize = 2;
            reactions.GetArrayElementAtIndex(0).managedReferenceValue = new RecordReaction("first", recorder);
            reactions.GetArrayElementAtIndex(1).managedReferenceValue = new RecordReaction("second", recorder);

            if (withEscape)
            {
                AddPersistentCall(response.FindPropertyRelative("escape.Event"), recorder, nameof(Recorder.RecordEscape));
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
            return listener;
        }

        // Mesmo formato que o drawer de UnityEvent grava quando o designer liga um método no Inspector.
        private static void AddPersistentCall(SerializedProperty unityEvent, Object target, string method)
        {
            SerializedProperty calls = unityEvent.FindPropertyRelative("m_PersistentCalls.m_Calls");
            calls.arraySize = 1;
            SerializedProperty call = calls.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = method;
            call.FindPropertyRelative("m_Mode").intValue = (int)PersistentListenerMode.EventDefined;
            call.FindPropertyRelative("m_CallState").intValue = (int)UnityEventCallState.RuntimeOnly;
        }

        public sealed class Recorder : MonoBehaviour
        {
            public readonly List<string> Ids = new List<string>();
            public readonly List<object> Payloads = new List<object>();

            public void Record(string id, object payload)
            {
                Ids.Add(id);
                Payloads.Add(payload);
            }

            public void RecordEscape(float payload)
            {
                Record("escape", payload);
            }
        }

        [Serializable]
        private sealed class RecordReaction : Reaction
        {
            [SerializeField] private string id;
            [SerializeField] private Recorder recorder;

            public RecordReaction(string id, Recorder recorder)
            {
                this.id = id;
                this.recorder = recorder;
            }

            public override void Execute<T>(EventListener listener, in T payload)
            {
                recorder.Record(id, payload);
            }
        }
    }
}
