using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Tests
{
    /// <summary>
    /// Reações prontas disparadas por um canal através de um <see cref="EventListener"/> montado pelo formato
    /// serializado, como o Inspector faz: SetActive (CA47), RaiseChannel (CA48, CA58), Log (CA49) e Toggle (CA50).
    /// </summary>
    public class ReactionTests
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
        public void SetActive_ActivatesInactive_AndDeactivates()
        {
            var activate = _events.CreateChannel<VoidEventChannel>();
            var deactivate = _events.CreateChannel<VoidEventChannel>();
            GameObject target = CreateObject("Target");
            target.SetActive(false);
            CreateListener<VoidEventResponse>(activate, new SetActiveReaction(), r =>
            {
                r.FindPropertyRelative("target").objectReferenceValue = target;
                r.FindPropertyRelative("active").boolValue = true;
            });
            CreateListener<VoidEventResponse>(deactivate, new SetActiveReaction(), r =>
            {
                r.FindPropertyRelative("target").objectReferenceValue = target;
                r.FindPropertyRelative("active").boolValue = false;
            });

            activate.Raise();
            Assert.That(target.activeSelf, Is.True, "não ativou o objeto inativo");

            deactivate.Raise();
            Assert.That(target.activeSelf, Is.False, "não desativou o objeto ativo");
        }

        [Test]
        public void RaiseVoid_RaisesTargetOnce_ListenerReceives()
        {
            var source = _events.CreateChannel<VoidEventChannel>();
            var target = _events.CreateChannel<VoidEventChannel>();
            int received = 0;
            Action<SpellBook.Core.Void> onTarget = _ => received++;
            target.Raised += onTarget;
            CreateListener<VoidEventResponse>(source, new RaiseChannelReaction(), r =>
            {
                r.FindPropertyRelative("mode").intValue = (int)RaiseChannelReaction.Mode.RaiseVoid;
                r.FindPropertyRelative("target").objectReferenceValue = target;
            });

            source.Raise();
            target.Raised -= onTarget;

            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void Forward_RaisesSameTypeWithPayload_3_5()
        {
            var source = _events.CreateChannel<FloatEventChannel>();
            var target = _events.CreateChannel<FloatEventChannel>();
            var received = new List<float>();
            Action<float> onTarget = received.Add;
            target.Raised += onTarget;
            CreateListener<FloatEventResponse>(source, new RaiseChannelReaction(), r =>
            {
                r.FindPropertyRelative("mode").intValue = (int)RaiseChannelReaction.Mode.ForwardPayload;
                r.FindPropertyRelative("target").objectReferenceValue = target;
            });

            source.Raise(3.5f);
            target.Raised -= onTarget;

            Assert.That(received, Is.EqualTo(new[] { 3.5f }));
        }

        [Test]
        public void Forward_DifferentType_DoesNotRaise()
        {
            var source = _events.CreateChannel<FloatEventChannel>();
            var target = _events.CreateChannel<IntEventChannel>();
            int received = 0;
            Action<int> onTarget = _ => received++;
            target.Raised += onTarget;
            CreateListener<FloatEventResponse>(source, new RaiseChannelReaction(), r =>
            {
                r.FindPropertyRelative("mode").intValue = (int)RaiseChannelReaction.Mode.ForwardPayload;
                r.FindPropertyRelative("target").objectReferenceValue = target;
            });

            source.Raise(3.5f);
            target.Raised -= onTarget;

            Assert.That(received, Is.Zero);
        }

        [Test]
        public void Log_WritesExactMessage()
        {
            var channel = _events.CreateChannel<VoidEventChannel>();
            CreateListener<VoidEventResponse>(channel, new LogReaction(), r =>
            {
                r.FindPropertyRelative("message").stringValue = "Porta aberta";
                r.FindPropertyRelative("type").intValue = (int)LogType.Warning;
            });
            LogAssert.Expect(LogType.Warning, "Porta aberta");

            channel.Raise();

            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(true, ToggleComponentReaction.Mode.Disable, false)]
        [TestCase(false, ToggleComponentReaction.Mode.Enable, true)]
        [TestCase(true, ToggleComponentReaction.Mode.Toggle, false)]
        [TestCase(false, ToggleComponentReaction.Mode.Toggle, true)]
        public void Toggle_SetsEnabled(bool initial, ToggleComponentReaction.Mode mode, bool expected)
        {
            var channel = _events.CreateChannel<VoidEventChannel>();
            var target = CreateObject("Target").AddComponent<Dummy>();
            target.enabled = initial;
            CreateListener<VoidEventResponse>(channel, new ToggleComponentReaction(), r =>
            {
                r.FindPropertyRelative("target").objectReferenceValue = target;
                r.FindPropertyRelative("mode").intValue = (int)mode;
            });

            channel.Raise();

            Assert.That(target.enabled, Is.EqualTo(expected));
        }

        private GameObject CreateObject(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        // Uma resposta com uma única reação, configurada pelo SerializedProperty dela.
        private void CreateListener<TResponse>(EventChannel channel, Reaction reaction, Action<SerializedProperty> configure)
            where TResponse : EventResponse, new()
        {
            GameObject go = CreateObject("Listener");
            go.SetActive(false);
            var listener = go.AddComponent<EventListener>();

            var serialized = new SerializedObject(listener);
            SerializedProperty responses = serialized.FindProperty("responses");
            responses.arraySize = 1;
            SerializedProperty response = responses.GetArrayElementAtIndex(0);
            response.managedReferenceValue = new TResponse();
            response.FindPropertyRelative("channel").objectReferenceValue = channel;

            SerializedProperty reactions = response.FindPropertyRelative("reactions");
            reactions.arraySize = 1;
            SerializedProperty element = reactions.GetArrayElementAtIndex(0);
            element.managedReferenceValue = reaction;
            configure(element);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
        }

        public sealed class Dummy : MonoBehaviour
        {
        }
    }
}
