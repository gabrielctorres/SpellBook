using System;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Tests
{
    /// <summary>Validação das reações prontas (R35, CA35 parte): campo obrigatório vazio é erro.</summary>
    public class ReactionValidationTests
    {
        [TestCase(typeof(SetActiveReaction))]
        [TestCase(typeof(ToggleComponentReaction))]
        [TestCase(typeof(LogReaction))]
        [TestCase(typeof(RaiseChannelReaction))]
        public void EmptyRequiredField_IsValidationError(Type reactionType)
        {
            var reaction = (IValidatable)Activator.CreateInstance(reactionType);
            var result = new ValidationResult(null);

            reaction.Validate(result);

            Assert.That(result.HasErrors, Is.True);
        }

        [Test]
        public void RaiseVoid_NonVoidTarget_IsValidationError()
        {
            using var events = new EventsFactory();
            var go = new GameObject("Listener");
            go.SetActive(false);
            try
            {
                var reaction = ConfigureRaiseChannel(
                    go.AddComponent<EventListener>(),
                    RaiseChannelReaction.Mode.RaiseVoid,
                    events.CreateChannel<IntEventChannel>());
                var result = new ValidationResult(null);

                reaction.Validate(result);

                var errors = result.Messages.Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.Message);
                Assert.That(errors, Has.Some.Contains("RaiseVoid"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // Uma resposta Int com um RaiseChannel configurado pelo SerializedProperty, como o Inspector faz.
        private static RaiseChannelReaction ConfigureRaiseChannel(
            EventListener listener, RaiseChannelReaction.Mode mode, EventChannel target)
        {
            var serialized = new SerializedObject(listener);
            SerializedProperty responses = serialized.FindProperty("responses");
            responses.arraySize = 1;
            SerializedProperty response = responses.GetArrayElementAtIndex(0);
            response.managedReferenceValue = new IntEventResponse();

            SerializedProperty reactions = response.FindPropertyRelative("reactions");
            reactions.arraySize = 1;
            SerializedProperty element = reactions.GetArrayElementAtIndex(0);
            element.managedReferenceValue = new RaiseChannelReaction();
            element.FindPropertyRelative("mode").intValue = (int)mode;
            element.FindPropertyRelative("target").objectReferenceValue = target;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            serialized.Update();
            return (RaiseChannelReaction)serialized.FindProperty("responses").GetArrayElementAtIndex(0)
                .FindPropertyRelative("reactions").GetArrayElementAtIndex(0).managedReferenceValue;
        }
    }
}
