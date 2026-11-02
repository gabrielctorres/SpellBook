using System;
using System.Collections;
using NUnit.Framework;
using SpellBook.Events;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>
    /// Pares de mínimo e máximo do <see cref="AbilitySystemEventRelay"/> e queda com efeito e instigador
    /// (B-010, DM6: CA21, CA22, C13).
    /// </summary>
    public class RelayLimitsTests
    {
        private GasFactory _gas;
        private EventsFactory _events;
        private GameObject _relayObject;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _events = new EventsFactory();
        }

        [TearDown]
        public void TearDown()
        {
            if (_relayObject != null) Object.Destroy(_relayObject);
            _gas.Dispose();
            _events.Dispose();
        }

        [UnityTest]
        public IEnumerator Depleted_And_ReachedMax_RaiseChannelWithInstigatorAndEffect()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            var depleted = _events.CreateChannel<AttributeLimitEventChannel>();
            var depletedValues = _events.CreateChannel<FloatEventChannel>();
            var reachedMax = _events.CreateChannel<AttributeLimitEventChannel>();
            var reachedMaxNames = _events.CreateChannel<StringEventChannel>();
            AbilitySystemComponent target = _gas.CreateAsc((health, 100f));
            AbilitySystemComponent attacker = _gas.CreateAsc();
            AbilitySystemComponent healer = _gas.CreateAsc();
            CreateRelay(target, so =>
            {
                SetPair(so, "attributeDepleted", depleted, depletedValues);
                SetPair(so, "attributeReachedMax", reachedMax, reachedMaxNames);
            });
            GameplayEffect hit = Instant(health, -150f);
            GameplayEffect heal = Instant(health, 200f);
            yield return null;

            target.ApplyEffect(hit, attacker);
            AttributeLimitEventData death = depleted.LastPayload;
            target.ApplyEffect(heal, healer);

            Assert.That(death.Owner, Is.SameAs(target));
            Assert.That(death.Attribute, Is.SameAs(health));
            Assert.That(death.Value, Is.EqualTo(0f));
            Assert.That(death.Instigator, Is.SameAs(attacker));
            Assert.That(death.Effect, Is.SameAs(hit));
            Assert.That(depletedValues.LastPayload, Is.EqualTo(0f), "o canal de valor não recebeu o valor no mínimo");
            AttributeLimitEventData full = reachedMax.LastPayload;
            Assert.That(full.Owner, Is.SameAs(target));
            Assert.That(full.Attribute, Is.SameAs(health));
            Assert.That(full.Value, Is.EqualTo(100f));
            Assert.That(full.Instigator, Is.SameAs(healer));
            Assert.That(full.Effect, Is.SameAs(heal));
            Assert.That(reachedMaxNames.LastPayload, Is.EqualTo("Vida"), "o canal de texto não recebeu o nome do atributo");
        }

        [UnityTest]
        public IEnumerator DisableRelay_Unsubscribes()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            var depleted = _events.CreateChannel<AttributeLimitEventChannel>();
            var reachedMax = _events.CreateChannel<AttributeLimitEventChannel>();
            AbilitySystemComponent target = _gas.CreateAsc((health, 100f));
            AbilitySystemEventRelay relay = CreateRelay(target, so =>
            {
                SetPair(so, "attributeDepleted", depleted);
                SetPair(so, "attributeReachedMax", reachedMax);
            });
            int raises = 0;
            Action<AttributeLimitEventData> onLimit = _ => raises++;
            depleted.Raised += onLimit;
            reachedMax.Raised += onLimit;
            yield return null;

            relay.enabled = false;
            target.ApplyAttributeChange(health, -100f);
            target.ApplyAttributeChange(health, 100f);
            int afterDisable = raises;
            relay.enabled = true;
            target.ApplyAttributeChange(health, -100f);
            target.ApplyAttributeChange(health, 100f);
            depleted.Raised -= onLimit;
            reachedMax.Raised -= onLimit;

            Assert.That(afterDisable, Is.EqualTo(0), "o relay desabilitado repassou o mínimo ou o máximo");
            Assert.That(raises, Is.EqualTo(2), "o relay reabilitado não voltou a repassar o mínimo e o máximo");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Decreased_IncludesEffectWithDamageTags()
        {
            GameplayTag fire = _gas.CreateTag("Dano.Fogo");
            AttributeDefinition health = CreateAttribute("Vida");
            var decreased = _events.CreateChannel<AttributeChangedEventChannel>();
            AbilitySystemComponent target = _gas.CreateAsc((health, 100f));
            AbilitySystemComponent attacker = _gas.CreateAsc();
            CreateRelay(target, so => SetPair(so, "attributeDecreased", decreased));
            GameplayEffect burn = Instant(health, -30f);
            burn.DamageTags.Add(fire);
            yield return null;

            target.ApplyEffect(burn, attacker);
            AttributeChangedData fromEffect = decreased.LastPayload;
            Assert.That(target.AttributeSet.TryGetState(health, out AttributeState state), Is.True);
            state.SetBaseValue(50f);
            AttributeChangedData direct = decreased.LastPayload;

            Assert.That(fromEffect.Delta, Is.EqualTo(-30f));
            Assert.That(fromEffect.Effect, Is.SameAs(burn));
            Assert.That(fromEffect.Instigator, Is.SameAs(attacker));
            Assert.That(fromEffect.Effect.DamageTags.HasTag(fire), Is.True, "as tags de dano vêm do efeito");
            Assert.That(direct.Delta, Is.EqualTo(-20f));
            Assert.That(direct.Effect, Is.Null, "mudança fora do pipeline não tem efeito");
            Assert.That(direct.Instigator, Is.Null, "mudança fora do pipeline não tem instigador");
        }

        private AttributeDefinition CreateAttribute(string name)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = 0f;
            definition.MaxValue = 100f;
            return definition;
        }

        private GameplayEffect Instant(AttributeDefinition attribute, float amount)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(amount) },
            });
            return effect;
        }

        // Relay num objeto próprio, configurado pelo SerializedObject antes de ativar, como o Inspector faria.
        private AbilitySystemEventRelay CreateRelay(AbilitySystemComponent asc, Action<SerializedObject> configure)
        {
            _relayObject = new GameObject("Relay");
            _relayObject.SetActive(false);
            var relay = _relayObject.AddComponent<AbilitySystemEventRelay>();
            using (var serialized = new SerializedObject(relay))
            {
                serialized.FindProperty("abilitySystem").objectReferenceValue = asc;
                configure(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            _relayObject.SetActive(true);
            return relay;
        }

        private static void SetPair(SerializedObject serialized, string pair, EventChannel channel, EventChannel valueChannel = null)
        {
            serialized.FindProperty(pair + ".channel").objectReferenceValue = channel;
            serialized.FindProperty(pair + ".valueChannel").objectReferenceValue = valueChannel;
        }
    }
}
