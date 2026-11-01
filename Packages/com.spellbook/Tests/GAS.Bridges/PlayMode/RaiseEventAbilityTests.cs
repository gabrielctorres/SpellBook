using System;
using System.Collections;
using NUnit.Framework;
using SpellBook.Events;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Void = SpellBook.Core.Void;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>Uma habilidade com <see cref="RaiseEventAction"/> num canal Void dispara uma vez, e um ouvinte reage (CA52).</summary>
    public class RaiseEventAbilityTests
    {
        private GasFactory _gas;
        private EventsFactory _events;
        private GameObject _listener;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _events = new EventsFactory();
        }

        [TearDown]
        public void TearDown()
        {
            if (_listener != null) Object.Destroy(_listener);
            _gas.Dispose();
            _events.Dispose();
        }

        [UnityTest]
        public IEnumerator Ability_RaiseVoid_OnceAndListenerReacts()
        {
            var channel = _events.CreateChannel<VoidEventChannel>();
            GameplayAbility ability = _gas.CreateAbility(new RaiseEventAction());
            SetEmission(ability, channel);
            CreateLogListener(channel, "Bola de fogo");
            AbilitySystemComponent asc = _gas.CreateAsc();
            asc.GiveAbility(ability);
            int raises = 0;
            Action<Void> onRaised = _ => raises++;
            channel.Raised += onRaised;
            LogAssert.Expect(LogType.Log, "Bola de fogo");

            asc.TryActivateAbility(ability, Vector3.zero);
            yield return null;
            yield return null;
            channel.Raised -= onRaised;

            Assert.That(raises, Is.EqualTo(1), "a habilidade não disparou o canal exatamente uma vez");
            Assert.That(asc.IsAbilityActive(ability), Is.False, "a habilidade não terminou");
            LogAssert.NoUnexpectedReceived();
        }

        private static void SetEmission(GameplayAbility ability, EventChannel channel)
        {
            using (var serialized = new SerializedObject(ability))
            {
                serialized.FindProperty(nameof(GameplayAbility.Actions))
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("emission")
                    .managedReferenceValue = EventEmission.CreateFor(channel);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Ouvinte com uma resposta Void e um Log, configurado pelo SerializedProperty, como o Inspector faz.
        private void CreateLogListener(EventChannel channel, string message)
        {
            _listener = new GameObject("Listener");
            _listener.SetActive(false);
            var listener = _listener.AddComponent<EventListener>();

            using (var serialized = new SerializedObject(listener))
            {
                SerializedProperty responses = serialized.FindProperty("responses");
                responses.arraySize = 1;
                SerializedProperty response = responses.GetArrayElementAtIndex(0);
                response.managedReferenceValue = new VoidEventResponse();
                response.FindPropertyRelative("channel").objectReferenceValue = channel;

                SerializedProperty reactions = response.FindPropertyRelative("reactions");
                reactions.arraySize = 1;
                SerializedProperty reaction = reactions.GetArrayElementAtIndex(0);
                reaction.managedReferenceValue = new LogReaction();
                reaction.FindPropertyRelative("message").stringValue = message;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            _listener.SetActive(true);
        }
    }
}
