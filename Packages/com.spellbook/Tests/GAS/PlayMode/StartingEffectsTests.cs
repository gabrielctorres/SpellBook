using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.GAS.Tests
{
    /// <summary>Efeitos de partida do ASC (B-005, AR7: CA27, CA28; cenário C11).</summary>
    public class StartingEffectsTests
    {
        private GasFactory _gas;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects) Object.Destroy(go);
            _objects.Clear();
            _gas.Dispose();
        }

        [Test]
        public void StartingRegen_AppliedAfterAttributeSet_RaisesHealth()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            health.MaxValue = 1000f;
            GameplayEffect bonus = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            bonus.Modifiers.Add(Add(health, 10f));
            GameplayEffect regen = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            regen.Period = 1f;
            regen.PeriodicModifiers.Add(Add(health, 5f));

            AbilitySystemComponent asc = CreateAsc(health, 50f, bonus, regen);
            float atStart = asc.GetAttributeValue(health);
            asc.Tick(1f);
            asc.Tick(1f);

            Assert.That(atStart, Is.EqualTo(60f), "o instantâneo de partida já atuou sobre o atributo inicial");
            Assert.That(asc.ActiveEffects.Count, Is.EqualTo(1));
            Assert.That(asc.ActiveEffects[0].Effect, Is.SameAs(regen));
            Assert.That(asc.ActiveEffects[0].Source, Is.SameAs(asc), "a origem é o próprio ASC");
            Assert.That(asc.GetAttributeValue(health), Is.EqualTo(70f));
        }

        [Test]
        public void BlockedStartingEffect_Warns_OthersApplied()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            health.MaxValue = 1000f;
            GameplayTag immune = _gas.CreateTag("Immune");
            GameplayEffect grantsImmunity = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f, immune);
            GameplayEffect blocked = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            blocked.name = "GE_Blocked";
            blocked.ApplicationImmunityTags.Add(immune);
            blocked.Modifiers.Add(Add(health, -40f));
            GameplayEffect after = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            after.Modifiers.Add(Add(health, 7f));
            LogAssert.Expect(LogType.Warning, new Regex("GE_Blocked.*BlockedByImmunity"));

            AbilitySystemComponent asc = CreateAsc(health, 50f, grantsImmunity, null, blocked, after);

            Assert.That(asc.HasTag(immune), Is.True);
            Assert.That(asc.GetAttributeValue(health), Is.EqualTo(57f), "o bloqueado não atua e o seguinte é aplicado");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EmptyList_DoesNothing()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");

            AbilitySystemComponent asc = CreateAsc(health, 50f);

            Assert.That(asc.ActiveEffects, Is.Empty);
            Assert.That(asc.GetAttributeValue(health), Is.EqualTo(50f));
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>ASC inativo configurado pelo <c>SerializedObject</c> e ativado no fim, para o <c>Awake</c> ver a lista.</summary>
        private AbilitySystemComponent CreateAsc(AttributeDefinition attribute, float initialValue, params GameplayEffect[] startingEffects)
        {
            var go = new GameObject("ASC");
            go.SetActive(false);
            _objects.Add(go);
            var asc = go.AddComponent<AbilitySystemComponent>();
            var setObject = new SerializedObject(go.GetComponent<AttributeSet>());
            SerializedProperty attributes = setObject.FindProperty("startingAttributes");
            attributes.arraySize = 1;
            SerializedProperty entry = attributes.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative(nameof(AttributeSet.InitialAttribute.Definition)).objectReferenceValue = attribute;
            entry.FindPropertyRelative(nameof(AttributeSet.InitialAttribute.InitialValue)).floatValue = initialValue;
            setObject.ApplyModifiedPropertiesWithoutUndo();

            var ascObject = new SerializedObject(asc);
            ascObject.FindProperty("autoTick").boolValue = false;
            SerializedProperty list = ascObject.FindProperty("startingEffects");
            list.arraySize = startingEffects.Length;
            for (int i = 0; i < startingEffects.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = startingEffects[i];
            ascObject.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(true);
            return asc;
        }

        private static AttributeEffectModifier Add(AttributeDefinition attribute, float value)
        {
            return new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(value) }
            };
        }
    }
}
