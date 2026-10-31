using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.Events;
using SpellBook.GAS.Abilities;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>
    /// <see cref="RaiseEventAction"/> dispara a emissão uma vez, com o payload fixo, e termina no mesmo quadro (CA61).
    /// Ação sem emissão ou sem canal é erro de validação, também pela habilidade que a contém (CA35, parte).
    /// </summary>
    public class RaiseEventActionTests
    {
        private GasFactory _gas;
        private EventsFactory _events;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _events = new EventsFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _events.Dispose();
        }

        [Test]
        public void Action_IntChannelFixed7_Raises7()
        {
            var channel = _events.CreateChannel<IntEventChannel>();
            RaiseEventAction action = CreateAction(channel, p => p.intValue = 7);
            var received = new List<int>();
            Action<int> onRaised = received.Add;
            channel.Raised += onRaised;

            IEnumerator run = action.Execute(GasFactory.CreateAbilityContext(null, Vector3.zero));
            bool yielded = run.MoveNext();
            channel.Raised -= onRaised;

            Assert.That(yielded, Is.False, "a ação não terminou no mesmo quadro");
            Assert.That(received, Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void Action_NoChannel_IsValidationError()
        {
            var withoutEmission = new RaiseEventAction();
            RaiseEventAction withoutChannel = CreateAction(null, _ => { });
            GameplayAbility ability = _gas.CreateAbility(default, withoutChannel);
            var noEmission = new ValidationResult(null);
            var noChannel = new ValidationResult(null);
            var abilityResult = new ValidationResult(ability);

            withoutEmission.Validate(noEmission);
            withoutChannel.Validate(noChannel);
            ability.Validate(abilityResult);

            Assert.That(noEmission.HasErrors, Is.True, "ação sem emissão não deu erro");
            Assert.That(noChannel.HasErrors, Is.True, "emissão sem canal não deu erro");
            Assert.That(abilityResult.HasErrors, Is.True, "a habilidade não repassou a validação à ação");
        }

        [Test]
        public void Ability_ActionWithChannel_HasNoValidationIssues()
        {
            RaiseEventAction action = CreateAction(_events.CreateChannel<VoidEventChannel>(), _ => { });
            GameplayAbility ability = _gas.CreateAbility(default, action, null);
            var result = new ValidationResult(ability);

            ability.Validate(result);

            Assert.That(result.Messages, Is.Empty);
        }

        // Ação dentro de uma habilidade, com a emissão criada a partir do canal e o payload configurado pelo
        // SerializedProperty, como o Inspector faz. Sem canal, a emissão é de int e fica sem canal.
        private RaiseEventAction CreateAction(EventChannel channel, Action<SerializedProperty> configurePayload)
        {
            GameplayAbility ability = _gas.CreateAbility(default, new RaiseEventAction());
            using (var serialized = new SerializedObject(ability))
            {
                SerializedProperty emission = serialized.FindProperty(nameof(GameplayAbility.Actions))
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("emission");
                emission.managedReferenceValue = channel != null ? EventEmission.CreateFor(channel) : new EventEmission<int>();
                configurePayload(emission.FindPropertyRelative("payload"));
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return (RaiseEventAction)ability.Actions[0];
        }
    }
}
