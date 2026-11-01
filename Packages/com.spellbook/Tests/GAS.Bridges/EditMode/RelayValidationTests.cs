using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.Events;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>
    /// Validação do <see cref="AbilitySystemEventRelay"/> (Spec do relay, RL5; CA26): relay sem ASC, canal de valor de
    /// tipo fora da lista e <c>UnityEvent</c> com chamada sem alvo são erros.
    /// </summary>
    public class RelayValidationTests
    {
        private EventsFactory _events;
        private GameObject _go;
        private AbilitySystemEventRelay _relay;

        [SetUp]
        public void SetUp()
        {
            _events = new EventsFactory();
            _go = new GameObject("Relay");
            _go.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            _events.Dispose();
        }

        [Test]
        public void NoAsc_Error()
        {
            _relay = _go.AddComponent<AbilitySystemEventRelay>();

            ValidationMessage message = Validate().Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.Message, Does.Contain("sem ASC"));
            Assert.That(message.PropertyPath, Is.EqualTo("abilitySystem"));
        }

        [Test]
        public void AscOnSameObject_NoIssues()
        {
            _go.AddComponent<AbilitySystemComponent>();
            _relay = _go.AddComponent<AbilitySystemEventRelay>();
            SetValueChannel("tagAdded", _events.CreateChannel<FloatEventChannel>());

            Assert.That(Validate(), Is.Empty);
        }

        [Test]
        public void ValueChannelOfUnsupportedType_Error()
        {
            _go.AddComponent<AbilitySystemComponent>();
            _relay = _go.AddComponent<AbilitySystemEventRelay>();
            SetValueChannel("effectApplied", _events.CreateChannel<IntEventChannel>());

            ValidationMessage message = Validate().Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.Message, Does.Contain("Int32"));
            Assert.That(message.PropertyPath, Is.EqualTo("effectApplied.valueChannel"));
        }

        [TestCase(true, "sem alvo")]
        [TestCase(false, "sem método")]
        public void UnityEventWithoutTarget_Error(bool missingTarget, string expected)
        {
            _go.AddComponent<AbilitySystemComponent>();
            _relay = _go.AddComponent<AbilitySystemEventRelay>();
            AddResponseCall("blocked", missingTarget ? null : _go, missingTarget ? "SetActive" : string.Empty);

            ValidationMessage message = Validate().Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.Message, Does.Contain(expected));
            Assert.That(message.PropertyPath, Is.EqualTo("blocked.response"));
        }

        [Test]
        public void ProjectValidator_ReportsRelayAsContext()
        {
            _relay = _go.AddComponent<AbilitySystemEventRelay>();
            var messages = new List<ValidationMessage>();

            ProjectValidator.Validate(new Object[] { _go }, messages);

            ValidationMessage message = messages.Single(m => m.Context == _relay);
            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.PropertyPath, Is.EqualTo("abilitySystem"));
        }

        private void SetValueChannel(string pair, EventChannel channel)
        {
            var serialized = new SerializedObject(_relay);
            serialized.FindProperty(pair + ".valueChannel").objectReferenceValue = channel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Chamada persistente, como o Inspector do UnityEvent grava.
        private void AddResponseCall(string pair, Object target, string method)
        {
            var serialized = new SerializedObject(_relay);
            SerializedProperty calls = serialized.FindProperty(pair + ".response.m_PersistentCalls.m_Calls");
            calls.arraySize++;
            SerializedProperty call = calls.GetArrayElementAtIndex(calls.arraySize - 1);
            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_MethodName").stringValue = method;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private IReadOnlyList<ValidationMessage> Validate()
        {
            return SpellBookEditor.CollectMessages(new Object[] { _relay });
        }
    }
}
