using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Events;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>O par <c>abilityFailed</c> do <see cref="AbilitySystemEventRelay"/> leva a tag e a chave do motivo (AV5).</summary>
    public class EventRelayFailureTests
    {
        private GasFactory _gas;
        private EventsFactory _events;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _events = new EventsFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects)
            {
                if (go != null) Object.Destroy(go);
            }

            _objects.Clear();
            _gas.Dispose();
            _events.Dispose();
        }

        [UnityTest]
        public IEnumerator Relay_AbilityFailed_HasTagAndKey()
        {
            GameplayTag reason = _gas.CreateTag("Falha.SemAlvo");
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule(new HasTargetPointCondition(), reason, "ui.sem_alvo"));
            var failed = _events.CreateChannel<AbilityEventChannel>();
            AbilitySystemComponent asc = _gas.CreateAsc();
            asc.GiveAbility(ability);
            CreateRelay(asc, failed);
            yield return null;

            asc.TryActivateAbility(ability, AbilityTarget.None);

            AbilityEventData data = failed.LastPayload;
            Assert.That(data.Owner, Is.SameAs(asc));
            Assert.That(data.Ability, Is.SameAs(ability));
            Assert.That(data.FailureReason, Is.EqualTo(AbilityActivationResult.BlockedByCondition));
            Assert.That(data.FailureTag, Is.EqualTo(reason));
            Assert.That(data.FailureKey, Is.EqualTo("ui.sem_alvo"));
        }

        [UnityTest]
        public IEnumerator Relay_AbilityFailed_WithoutReason_TagInvalidKeyNull()
        {
            GameplayAbility notGranted = _gas.CreateAbility();
            var failed = _events.CreateChannel<AbilityEventChannel>();
            AbilitySystemComponent asc = _gas.CreateAsc();
            CreateRelay(asc, failed);
            yield return null;

            asc.TryActivateAbility(notGranted, AbilityTarget.None);

            AbilityEventData data = failed.LastPayload;
            Assert.That(data.FailureReason, Is.EqualTo(AbilityActivationResult.NotGranted));
            Assert.That(data.FailureTag.IsValid, Is.False);
            Assert.That(data.FailureKey, Is.Null);
        }

        private void CreateRelay(AbilitySystemComponent asc, EventChannel failedChannel)
        {
            var go = new GameObject("Relay");
            _objects.Add(go);
            go.SetActive(false);
            var relay = go.AddComponent<AbilitySystemEventRelay>();
            using (var serialized = new SerializedObject(relay))
            {
                serialized.FindProperty("abilitySystem").objectReferenceValue = asc;
                serialized.FindProperty("abilityFailed.channel").objectReferenceValue = failedChannel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            go.SetActive(true);
        }
    }
}
