using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Tests.Inspector;
using SpellBook.Core.Tests.Validation;
using SpellBook.Core.Validation;
using SpellBook.Events;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Bridges;
using SpellBook.Pooling;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Core.Tests
{
    /// <summary>
    /// Validador do projeto (Spec, seção 4.9): cada regra do R35 sobre assets vira erro (CA35), corrigidas não há
    /// erro, uma exceção não para a varredura, o tipo ausente é erro sem apagar dados (CA64) e o host está limpo (R34).
    /// </summary>
    public class ProjectValidationTests
    {
        private readonly List<Object> _created = new List<Object>();
        private EventsFactory _events;
        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _events = new EventsFactory();
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created.Where(o => o != null))
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            _gas.Dispose();
            _events.Dispose();
            MissingTypeAsset.Delete();
        }

        [Test]
        public void Validator_ReportsEachR35Error()
        {
            EventListener noChannel = Listener(null, null, null);
            EventListener emptyReaction = Listener(_events.CreateChannel<IntEventChannel>(), new SetActiveReaction(), null);
            EventListener escapeWithoutTarget = Listener(_events.CreateChannel<IntEventChannel>(), null, "SetActive");
            EventEmitter emitter = Emitter(null);
            GameplayAbility ability = Ability(null);
            PooledSpawnerAsset spawner = Spawner(-1);

            List<ValidationMessage> messages = Validate(noChannel.gameObject, emptyReaction.gameObject,
                escapeWithoutTarget.gameObject, emitter.gameObject, ability, spawner);

            Assert.That(ErrorsOf(messages, noChannel), Has.Some.Contains("sem canal"));
            Assert.That(ErrorsOf(messages, emptyReaction), Has.Some.Contains("SetActive sem GameObject"));
            Assert.That(ErrorsOf(messages, escapeWithoutTarget), Has.Some.Contains("sem alvo"));
            Assert.That(ErrorsOf(messages, emitter), Has.Some.Contains("EventEmitter sem emissão"));
            Assert.That(ErrorsOf(messages, ability), Has.Some.Contains("sem canal"));
            Assert.That(ErrorsOf(messages, spawner), Has.Some.Contains("tamanho inicial"));
        }

        [Test]
        public void Validator_FixedObjects_NoErrors()
        {
            EventListener listener = Listener(_events.CreateChannel<IntEventChannel>(), new SetActiveReaction(), "SetActive");
            SetReactionTarget(listener);
            EventEmitter emitter = Emitter(_events.CreateChannel<VoidEventChannel>());
            GameplayAbility ability = Ability(_events.CreateChannel<VoidEventChannel>());
            PooledSpawnerAsset spawner = Spawner(0);

            List<ValidationMessage> messages = Validate(listener.gameObject, emitter.gameObject, ability, spawner);

            Assert.That(messages, Is.Empty);
        }

        [Test]
        public void Validator_ExceptionInValidate_BecomesErrorAndContinues()
        {
            ValidatableHolder throwing = Track(ScriptableObject.CreateInstance<ValidatableHolder>());
            throwing.throws = true;
            ValidatableHolder invalid = Track(ScriptableObject.CreateInstance<ValidatableHolder>());
            invalid.valid = false;

            List<ValidationMessage> messages = Validate(throwing, invalid);

            Assert.That(ErrorsOf(messages, throwing), Is.EqualTo(new[] { "A validação lançou InvalidOperationException: falhou" }));
            Assert.That(ErrorsOf(messages, invalid), Is.EqualTo(new[] { ValidatableHolder.InvalidMessage }));
        }

        [Test]
        public void Validator_MissingSerializeReferenceType_IsError_DataKept()
        {
            PickHolder holder = MissingTypeAsset.Create();
            string expected = MissingReferenceTypes.FormatMessage(MissingReferenceTypes.Get(holder)[0]);

            List<ValidationMessage> messages = Validate(holder);

            Assert.That(ErrorsOf(messages, holder), Is.EqualTo(new[] { expected }));
            Assert.That(expected, Does.Contain(MissingTypeAsset.Assembly).And.Contain(MissingTypeAsset.GhostClass));
            Assert.That(MissingTypeAsset.ReadFile(), Does.Contain("class: " + MissingTypeAsset.GhostClass + ",")
                .And.Contain("Value: " + MissingTypeAsset.GhostValue));
            Assert.That(MissingReferenceTypes.Get(holder).Count, Is.EqualTo(1), "o validador limpou a referência");
        }

        [Test]
        public void Project_HasNoValidationErrors()
        {
            var messages = new List<ValidationMessage>();

            ProjectValidator.Validate(ProjectValidator.CollectProjectTargets(), messages);

            string[] errors = messages.Where(m => m.Severity == ValidationSeverity.Error)
                .Select(m => $"{(m.Context != null ? m.Context.name : "?")}: {m.Message}")
                .ToArray();
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void ValidateTypes_TypeWithoutHelpUrl_IsErrorOnItsScript()
        {
            var messages = new List<ValidationMessage>();

            ProjectValidator.ValidateTypes(new[] { typeof(NoHelpUrlScriptAsset).Assembly }, messages);

            ValidationMessage[] errors = messages
                .Where(m => m.Severity == ValidationSeverity.Error && m.Message.Contains(typeof(NoHelpUrlScriptAsset).FullName))
                .ToArray();
            Assert.That(errors, Has.Length.EqualTo(1));
            Assert.That(errors[0].Message, Does.Contain(HelpUrlRule.MissingReason));
            var script = errors[0].Context as MonoScript;
            Assert.That(script, Is.Not.Null, "o alvo do erro não é o MonoScript do tipo");
            Assert.That(script.GetClass(), Is.EqualTo(typeof(NoHelpUrlScriptAsset)));
        }

        [Test]
        public void ValidateTypes_TypeWithoutScript_IsErrorWithoutTarget()
        {
            var messages = new List<ValidationMessage>();

            ProjectValidator.ValidateTypes(new[] { typeof(NoHelpUrlTestAsset).Assembly }, messages);

            ValidationMessage error = messages.Single(m => m.Message.Contains(typeof(NoHelpUrlTestAsset).FullName));
            Assert.That(error.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(error.Context, Is.Null);
        }

        [Test]
        public void ValidateTypes_RuntimeAssemblies_NoErrors()
        {
            var messages = new List<ValidationMessage>();

            ProjectValidator.ValidateTypes(HelpUrlRule.RuntimeAssemblies(), messages);

            Assert.That(messages.Select(m => m.Message), Is.Empty);
        }

        private static List<ValidationMessage> Validate(params Object[] targets)
        {
            var messages = new List<ValidationMessage>();
            ProjectValidator.Validate(targets, messages);
            return messages;
        }

        private static string[] ErrorsOf(IEnumerable<ValidationMessage> messages, Object context)
        {
            return messages.Where(m => m.Severity == ValidationSeverity.Error && m.Context == context)
                .Select(m => m.Message)
                .ToArray();
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        // Ouvinte com uma resposta Int; opcionalmente uma reação e uma chamada do escape sem alvo.
        private EventListener Listener(EventChannel channel, Reaction reaction, string escapeMethod)
        {
            var go = Track(new GameObject("Listener"));
            go.SetActive(false);
            var listener = go.AddComponent<EventListener>();
            using (var serialized = new SerializedObject(listener))
            {
                SerializedProperty responses = serialized.FindProperty("responses");
                responses.arraySize = 1;
                SerializedProperty response = responses.GetArrayElementAtIndex(0);
                response.managedReferenceValue = new IntEventResponse();
                response.FindPropertyRelative("channel").objectReferenceValue = channel;
                if (reaction != null)
                {
                    SerializedProperty reactions = response.FindPropertyRelative("reactions");
                    reactions.arraySize = 1;
                    reactions.GetArrayElementAtIndex(0).managedReferenceValue = reaction;
                }

                if (escapeMethod != null)
                {
                    SerializedProperty calls = response.FindPropertyRelative("escape.Event.m_PersistentCalls.m_Calls");
                    calls.arraySize = 1;
                    calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_MethodName").stringValue = escapeMethod;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return listener;
        }

        // Liga o alvo da reação e o alvo do escape ao próprio GameObject do ouvinte.
        private static void SetReactionTarget(EventListener listener)
        {
            using (var serialized = new SerializedObject(listener))
            {
                SerializedProperty response = serialized.FindProperty("responses").GetArrayElementAtIndex(0);
                response.FindPropertyRelative("reactions").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("target").objectReferenceValue = listener.gameObject;
                response.FindPropertyRelative("escape.Event.m_PersistentCalls.m_Calls").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("m_Target").objectReferenceValue = listener.gameObject;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private EventEmitter Emitter(EventChannel channel)
        {
            var go = Track(new GameObject("Emitter"));
            var emitter = go.AddComponent<EventEmitter>();
            using (var serialized = new SerializedObject(emitter))
            {
                serialized.FindProperty("emission").managedReferenceValue = EventEmission.CreateFor(channel);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return emitter;
        }

        // Habilidade com uma RaiseEventAction; sem canal, a emissão é de int e fica sem canal.
        private GameplayAbility Ability(EventChannel channel)
        {
            GameplayAbility ability = _gas.CreateAbility(default, new RaiseEventAction());
            using (var serialized = new SerializedObject(ability))
            {
                serialized.FindProperty(nameof(GameplayAbility.Actions)).GetArrayElementAtIndex(0)
                    .FindPropertyRelative("emission").managedReferenceValue =
                    channel != null ? EventEmission.CreateFor(channel) : new EventEmission<int>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return ability;
        }

        private PooledSpawnerAsset Spawner(int initialSize)
        {
            PooledSpawnerAsset spawner = Track(ScriptableObject.CreateInstance<PooledSpawnerAsset>());
            using (var serialized = new SerializedObject(spawner))
            {
                serialized.FindProperty("initialSize").intValue = initialSize;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return spawner;
        }
    }
}
