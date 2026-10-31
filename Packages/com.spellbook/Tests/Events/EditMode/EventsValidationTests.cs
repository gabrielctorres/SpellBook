using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Tests
{
    /// <summary>
    /// Validação do <see cref="EventListener"/> e das respostas (R35, CA35 parte): resposta sem canal, chamada do
    /// escape sem alvo ou sem método e reação de tipo errado para o payload são erros.
    /// </summary>
    public class EventsValidationTests
    {
        private EventsFactory _events;
        private GameObject _go;
        private EventListener _listener;
        private SerializedObject _serialized;

        [SetUp]
        public void SetUp()
        {
            _events = new EventsFactory();
            _go = new GameObject("Listener");
            _go.SetActive(false);
            _listener = _go.AddComponent<EventListener>();
            _serialized = new SerializedObject(_listener);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            _events.Dispose();
        }

        [Test]
        public void Response_NoChannel_IsError()
        {
            AddIntResponse(null);
            Apply();

            Assert.That(Errors(), Has.Some.Contains("sem canal"));
        }

        [Test]
        public void Escape_CallWithoutTarget_IsError()
        {
            SerializedProperty response = AddIntResponse(_events.CreateChannel<IntEventChannel>());
            AddEscapeCall(response, null, "SetActive");
            Apply();

            Assert.That(Errors(), Has.Some.Contains("sem alvo"));
        }

        [Test]
        public void Escape_CallWithoutMethod_IsError()
        {
            SerializedProperty response = AddIntResponse(_events.CreateChannel<IntEventChannel>());
            AddEscapeCall(response, _go, string.Empty);
            Apply();

            Assert.That(Errors(), Has.Some.Contains("sem método"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Forward_ReactionWrongType_IsError(bool insideDelay)
        {
            SerializedProperty response = AddIntResponse(_events.CreateChannel<IntEventChannel>());
            SerializedProperty reactions = response.FindPropertyRelative("reactions");
            if (insideDelay)
            {
                reactions = AddReaction(reactions, new DelayedReaction()).FindPropertyRelative("reactions");
            }

            AddForward(reactions, _events.CreateChannel<FloatEventChannel>());
            Apply();

            Assert.That(Errors(), Has.Some.Contains("ForwardPayload precisa de um canal de Int32"));
        }

        [Test]
        public void ValidListener_NoIssues()
        {
            SerializedProperty response = AddIntResponse(_events.CreateChannel<IntEventChannel>());
            AddForward(response.FindPropertyRelative("reactions"), _events.CreateChannel<IntEventChannel>());
            AddEscapeCall(response, _go, "SetActive");
            Apply();

            Assert.That(_listener, Is.InstanceOf<IValidatable>());
            Assert.That(Validate().Messages, Is.Empty);
        }

        private SerializedProperty AddIntResponse(EventChannel channel)
        {
            SerializedProperty responses = _serialized.FindProperty("responses");
            responses.arraySize++;
            SerializedProperty response = responses.GetArrayElementAtIndex(responses.arraySize - 1);
            response.managedReferenceValue = new IntEventResponse();
            response.FindPropertyRelative("channel").objectReferenceValue = channel;
            return response;
        }

        private static SerializedProperty AddReaction(SerializedProperty reactions, Reaction reaction)
        {
            reactions.arraySize++;
            SerializedProperty element = reactions.GetArrayElementAtIndex(reactions.arraySize - 1);
            element.managedReferenceValue = reaction;
            return element;
        }

        private static void AddForward(SerializedProperty reactions, EventChannel target)
        {
            SerializedProperty element = AddReaction(reactions, new RaiseChannelReaction());
            element.FindPropertyRelative("mode").intValue = (int)RaiseChannelReaction.Mode.ForwardPayload;
            element.FindPropertyRelative("target").objectReferenceValue = target;
        }

        // Chamada persistente do escape, como o Inspector do UnityEvent grava.
        private static void AddEscapeCall(SerializedProperty response, Object target, string method)
        {
            SerializedProperty calls = response.FindPropertyRelative("escape.Event.m_PersistentCalls.m_Calls");
            calls.arraySize++;
            SerializedProperty call = calls.GetArrayElementAtIndex(calls.arraySize - 1);
            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_MethodName").stringValue = method;
        }

        private void Apply()
        {
            _serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private ValidationResult Validate()
        {
            var result = new ValidationResult(_listener);
            if ((object)_listener is IValidatable validatable)
            {
                validatable.Validate(result);
            }

            return result;
        }

        private string[] Errors()
        {
            return Validate().Messages
                .Where(m => m.Severity == ValidationSeverity.Error)
                .Select(m => m.Message)
                .ToArray();
        }
    }
}
