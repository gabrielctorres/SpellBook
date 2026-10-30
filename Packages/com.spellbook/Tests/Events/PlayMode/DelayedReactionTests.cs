using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Tests
{
    /// <summary>
    /// Invoke com delay (<see cref="DelayedReaction"/>): roda a lista interna uma vez, na ordem, quando a fonte de
    /// tempo avança o atraso (CA51); o ouvinte desabilitado na espera cancela (CA59); sem fonte, conta pelo tempo
    /// escalado (CA60). O ouvinte é montado pelo formato serializado, como o Inspector faz.
    /// </summary>
    public class DelayedReactionTests
    {
        private EventsFactory _events;
        private TimeFactory _time;
        private float _timeScale;
        private readonly List<Object> _objects = new List<Object>();
        private Recorder _recorder;

        [SetUp]
        public void SetUp()
        {
            _events = new EventsFactory();
            _time = new TimeFactory();
            _timeScale = UnityEngine.Time.timeScale;
            var go = new GameObject("Recorder");
            _objects.Add(go);
            _recorder = go.AddComponent<Recorder>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Time.timeScale = _timeScale;
            foreach (Object obj in _objects)
            {
                if (obj != null) Object.Destroy(obj);
            }

            _objects.Clear();
            _time.Dispose();
            _events.Dispose();
        }

        [UnityTest]
        public IEnumerator Delayed_1s_NotAt0_9_OnceAt1_1_InOrder()
        {
            var channel = _events.CreateChannel<FloatEventChannel>();
            ManualTimeSource source = _time.CreateManual();
            CreateListener(channel, 1f, source);

            channel.Raise(3.5f);
            source.Advance(0.9f);
            yield return null;
            Assert.That(_recorder.Ids, Is.Empty, "rodou antes do atraso");

            source.Advance(0.2f);
            yield return null;
            source.Advance(5f);
            yield return null;

            Assert.That(_recorder.Ids, Is.EqualTo(new[] { "first", "second" }));
            Assert.That(_recorder.Payloads, Is.EqualTo(new object[] { 3.5f, 3.5f }));
        }

        [UnityTest]
        public IEnumerator Delayed_ListenerDisabledDuringWait_NeverRuns()
        {
            var channel = _events.CreateChannel<FloatEventChannel>();
            ManualTimeSource source = _time.CreateManual();
            EventListener listener = CreateListener(channel, 1f, source);

            channel.Raise(1f);
            listener.enabled = false;
            source.Advance(2f);
            yield return null;
            listener.enabled = true;
            source.Advance(2f);
            yield return null;
            Assert.That(_recorder.Ids, Is.Empty, "a espera cancelada rodou");

            // Reabilitado, um disparo novo roda uma vez só, com o payload dele: a espera antiga não volta.
            channel.Raise(2f);
            source.Advance(1f);
            yield return null;
            Assert.That(_recorder.Payloads, Is.EqualTo(new object[] { 2f, 2f }));
        }

        [UnityTest]
        public IEnumerator Delayed_NoAsset_TimeScaleZero_DoesNotRun()
        {
            var channel = _events.CreateChannel<FloatEventChannel>();
            CreateListener(channel, 0.05f, null);

            UnityEngine.Time.timeScale = 0f;
            channel.Raise(1f);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(_recorder.Ids, Is.Empty, "rodou com o tempo escalado parado");

            // A espera continua pendente e vence quando o tempo escalado volta a andar.
            UnityEngine.Time.timeScale = 1f;
            yield return new WaitForSeconds(0.2f);
            Assert.That(_recorder.Ids, Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void Delayed_NoReactions_IsValidationError()
        {
            var result = new ValidationResult(null);

            new DelayedReaction().Validate(result);

            Assert.That(result.HasErrors, Is.True);
        }

        // Uma resposta Float com um Invoke com delay que contém duas reações que gravam id e payload.
        private EventListener CreateListener(FloatEventChannel channel, float delay, TimeSourceAsset source)
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
            reactions.arraySize = 1;
            SerializedProperty delayed = reactions.GetArrayElementAtIndex(0);
            delayed.managedReferenceValue = new DelayedReaction();
            delayed.FindPropertyRelative("delay").floatValue = delay;
            delayed.FindPropertyRelative("timeSource").objectReferenceValue = source;

            SerializedProperty inner = delayed.FindPropertyRelative("reactions");
            inner.arraySize = 2;
            inner.GetArrayElementAtIndex(0).managedReferenceValue = new RecordReaction("first", _recorder);
            inner.GetArrayElementAtIndex(1).managedReferenceValue = new RecordReaction("second", _recorder);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
            return listener;
        }

        public sealed class Recorder : MonoBehaviour
        {
            public readonly List<string> Ids = new List<string>();
            public readonly List<object> Payloads = new List<object>();
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
                recorder.Ids.Add(id);
                recorder.Payloads.Add(payload);
            }
        }
    }
}
