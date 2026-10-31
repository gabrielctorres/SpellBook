using NUnit.Framework;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Tests
{
    /// <summary>
    /// <see cref="EventEmission{T}"/> é um genérico fechado num <c>[SerializeReference]</c>, criado por código (ADR 0013,
    /// risco 16). Se a ida e volta falhar depois da implementação, vale o plano B da 0013.
    /// </summary>
    public class EmissionSerializationTests
    {
        private EventsFactory _events;
        private EmissionHolder _source;
        private EmissionHolder _copy;

        [SetUp]
        public void SetUp()
        {
            _events = new EventsFactory();
            _source = ScriptableObject.CreateInstance<EmissionHolder>();
            _copy = ScriptableObject.CreateInstance<EmissionHolder>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_source);
            Object.DestroyImmediate(_copy);
            _events.Dispose();
        }

        // O canal de teste não é asset, e o EditorJsonUtility grava a referência a ele como instanceID 0. A ida e
        // volta confere o risco 16 (tipo genérico fechado e payload); o canal fica nos testes de CreateFor e do emissor.
        [Test]
        public void Emission_GenericRoundTrip_Float_AndCustomStruct()
        {
            var channel = _events.CreateChannel<FloatEventChannel>();
            var serialized = new SerializedObject(_source);
            SerializedProperty emission = serialized.FindProperty("emission");

            emission.managedReferenceValue = EventEmission.CreateFor(channel);
            emission.FindPropertyRelative("payload").floatValue = 2.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(_source), _copy);

            Assert.That(_copy.emission, Is.TypeOf<EventEmission<float>>());
            Assert.That(new SerializedObject(_copy).FindProperty("emission.payload").floatValue, Is.EqualTo(2.5f));

            emission.managedReferenceValue = new EventEmission<EmissionTestPayload>();
            emission.FindPropertyRelative("payload.Amount").intValue = 7;
            emission.FindPropertyRelative("payload.Label").stringValue = "dano";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(_source), _copy);

            Assert.That(_copy.emission, Is.TypeOf<EventEmission<EmissionTestPayload>>());
            var copied = new SerializedObject(_copy).FindProperty("emission.payload");
            Assert.That(copied.FindPropertyRelative("Amount").intValue, Is.EqualTo(7));
            Assert.That(copied.FindPropertyRelative("Label").stringValue, Is.EqualTo("dano"));
        }

        [Test]
        public void Emission_CreateFor_NullChannel_ReturnsNull()
        {
            Assert.That(EventEmission.CreateFor(null), Is.Null);
        }

        [Test]
        public void Emission_CreateFor_Channel_TypedByPayloadWithChannel()
        {
            var channel = _events.CreateChannel<VoidEventChannel>();

            EventEmission emission = EventEmission.CreateFor(channel);

            Assert.That(emission, Is.TypeOf<EventEmission<SpellBook.Core.Void>>());
            Assert.That(emission.Channel, Is.SameAs(channel));
            Assert.That(emission.PayloadType, Is.EqualTo(typeof(SpellBook.Core.Void)));
        }
    }
}
