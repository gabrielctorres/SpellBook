using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Events;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>
    /// Payload dinâmico da <see cref="RaiseEventAction"/> nas listas de ações do efeito: o valor sai do efeito, do nível e
    /// do evento guardados no <see cref="ActionContext"/> (CA21 e CA22 do B-004).
    /// </summary>
    public class RaiseEventEffectPayloadTests
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
        public void RaiseEvent_InEffectTick_UsesInstance_InAbility_UsesCaster()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AbilitySystemComponent caster = CreateAsc((health, 80f));
            AbilitySystemComponent target = CreateAsc((health, 30f));
            var channel = _events.CreateChannel<FloatEventChannel>();
            GameplayEffect effect = CreatePeriodicEffect();
            effect.CanStack = true;
            effect.MaxStacks = 3;
            AddAction(effect, nameof(GameplayEffect.OnTick), channel, GasPayloadSource.AttributeValue, health);
            AddAction(effect, nameof(GameplayEffect.OnTick), channel, GasPayloadSource.Stacks);
            var received = Collect(channel);

            ActiveEffectInstance instance = target.ApplyEffect(effect, caster);
            Assert.That(instance.SetStacks(2), Is.True, "o efeito não aceitou 2 pilhas");
            target.Tick(1f);

            Assert.That(received, Is.EqualTo(new[] { 30f, 2f }), "no tique, o atributo é o do dono e as pilhas, as da instância");

            received.Clear();
            RaiseEventAction abilityAction = AbilityAction(channel, GasPayloadSource.AttributeValue, health);
            CreateAsc().RunActions(new GameplayAction[] { abilityAction }, GasFactory.CreateActionContext(caster, Vector3.zero, target), null);

            Assert.That(received, Is.EqualTo(new[] { 80f }), "na habilidade, o atributo é o do conjurador");
        }

        [Test]
        public void OnTick_RaisesOncePerTick()
        {
            AbilitySystemComponent target = CreateAsc();
            var channel = _events.CreateChannel<IntEventChannel>();
            GameplayEffect effect = CreatePeriodicEffect();
            AddAction(effect, nameof(GameplayEffect.OnTick), channel, GasPayloadSource.Fixed);
            var received = Collect(channel);

            target.ApplyEffect(effect, CreateAsc());
            Assert.That(received, Is.Empty, "disparou antes do primeiro tique");

            for (int tick = 1; tick <= 3; tick++)
            {
                target.Tick(1f);
                Assert.That(received, Has.Count.EqualTo(tick), $"o tique {tick} não disparou uma vez");
            }
        }

        [Test]
        public void Float_TickDamage_AtContextLevel()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            var channel = _events.CreateChannel<FloatEventChannel>();
            GameplayEffect levelOne = CreatePoison(health, 1);
            GameplayEffect levelThree = CreatePoison(health, 3);
            AddAction(levelOne, nameof(GameplayEffect.OnTick), channel, GasPayloadSource.ModifierMagnitude);
            AddAction(levelThree, nameof(GameplayEffect.OnTick), channel, GasPayloadSource.ModifierMagnitude);
            var received = Collect(channel);

            AbilitySystemComponent target = CreateAsc((health, 100f));
            target.ApplyEffect(levelThree, CreateAsc());
            target.Tick(1f);

            Assert.That(received, Is.EqualTo(new[] { -15f }), "o dano do tique não é o do nível 3");

            received.Clear();
            AbilitySystemComponent other = CreateAsc((health, 100f));
            other.ApplyEffect(levelOne, CreateAsc());
            other.Tick(1f);

            Assert.That(received, Is.EqualTo(new[] { -5f }), "o dano do tique não é o do nível 1");
        }

        [Test]
        public void String_EffectName()
        {
            var channel = _events.CreateChannel<StringEventChannel>();
            GameplayEffect effect = CreatePeriodicEffect();
            effect.name = "Queimadura";
            AddAction(effect, nameof(GameplayEffect.OnApplied), channel, GasPayloadSource.EffectName);
            AddAction(effect, nameof(GameplayEffect.OnTick), channel, GasPayloadSource.EffectName);
            var received = Collect(channel);

            AbilitySystemComponent target = CreateAsc();
            target.ApplyEffect(effect, CreateAsc());
            target.Tick(1f);

            Assert.That(received, Is.EqualTo(new[] { "Queimadura", "Queimadura" }));
        }

        [Test]
        public void GameObject_Owner()
        {
            AbilitySystemComponent caster = CreateAsc();
            AbilitySystemComponent target = CreateAsc();
            var channel = _events.CreateChannel<GameObjectEventChannel>();
            GameplayEffect effect = CreatePeriodicEffect();
            AddAction(effect, nameof(GameplayEffect.OnTick), channel, GasPayloadSource.TargetObject);
            AddAction(effect, nameof(GameplayEffect.OnTick), channel, GasPayloadSource.SourceObject);
            var received = Collect(channel);

            target.ApplyEffect(effect, caster);
            target.Tick(1f);

            Assert.That(received, Is.EqualTo(new[] { target.gameObject, caster.gameObject }),
                "TargetObject não é o dono do efeito ou SourceObject não é quem aplicou");
        }

        [Test]
        public void InstantEffect_RaisesWithEffectNameAndStacks1()
        {
            var names = _events.CreateChannel<StringEventChannel>();
            var stacks = _events.CreateChannel<FloatEventChannel>();
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.name = "Cura";
            AddAction(effect, nameof(GameplayEffect.OnApplied), names, GasPayloadSource.EffectName);
            AddAction(effect, nameof(GameplayEffect.OnApplied), stacks, GasPayloadSource.Stacks);
            var receivedNames = Collect(names);
            var receivedStacks = Collect(stacks);

            CreateAsc().ApplyEffect(effect, CreateAsc());

            Assert.That(receivedNames, Is.EqualTo(new[] { "Cura" }));
            Assert.That(receivedStacks, Is.EqualTo(new[] { 1f }));
        }

        [Test]
        public void EventData_InEffect_FillsEffectEventData()
        {
            AbilitySystemComponent caster = CreateAsc();
            AbilitySystemComponent target = CreateAsc();
            var channel = _events.CreateChannel<EffectEventChannel>();
            GameplayEffect effect = CreatePeriodicEffect();
            AddAction(effect, nameof(GameplayEffect.OnTick), channel, GasPayloadSource.EventData);
            var received = Collect(channel);

            target.ApplyEffect(effect, caster);
            target.Tick(1f);

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Owner, Is.SameAs(target));
            Assert.That(received[0].Source, Is.SameAs(caster));
            Assert.That(received[0].Effect, Is.SameAs(effect));
            Assert.That(received[0].Stacks, Is.EqualTo(1));
        }

        [Test]
        public void ModifierIndexOutOfRange_RaisesNothing_WarnsOnce()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            var channel = _events.CreateChannel<FloatEventChannel>();
            GameplayEffect effect = CreatePoison(health, 1);
            AddAction(effect, nameof(GameplayEffect.OnTick), channel, GasPayloadSource.ModifierMagnitude, modifierIndex: 1);
            var received = Collect(channel);
            AbilitySystemComponent target = CreateAsc((health, 100f));
            target.ApplyEffect(effect, CreateAsc());

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("modifierIndex 1"));
            target.Tick(1f);
            target.Tick(1f);

            Assert.That(received, Is.Empty, "índice além da lista disparou o canal");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EffectSources_OutsideEffect_RaiseNothing()
        {
            var floats = _events.CreateChannel<FloatEventChannel>();
            var names = _events.CreateChannel<StringEventChannel>();
            var received = Collect(floats);
            var receivedNames = Collect(names);
            var actions = new GameplayAction[]
            {
                AbilityAction(floats, GasPayloadSource.ModifierMagnitude, null),
                AbilityAction(floats, GasPayloadSource.Stacks, null),
                AbilityAction(names, GasPayloadSource.EffectName, null),
            };

            Assert.DoesNotThrow(() =>
                CreateAsc().RunActions(actions, GasFactory.CreateActionContext(CreateAsc(), Vector3.zero), null));

            Assert.That(received, Is.Empty, "fonte do efeito disparou fora de efeito");
            Assert.That(receivedNames, Is.Empty, "EffectName disparou fora de efeito");
        }

        private AbilitySystemComponent CreateAsc(params (AttributeDefinition Definition, float InitialValue)[] attributes)
        {
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            GasFactory.SetAutoTick(asc, false);
            return asc;
        }

        private GameplayEffect CreatePeriodicEffect()
        {
            GameplayEffect effect = _gas.CreateDurationEffect(10f);
            effect.Period = 1f;
            return effect;
        }

        // Veneno com dano periódico de -5, -10 e -15 nos níveis 1 a 3, aplicado no nível padrão pedido.
        private GameplayEffect CreatePoison(AttributeDefinition health, int defaultLevel)
        {
            GameplayEffect effect = CreatePeriodicEffect();
            effect.PeriodicModifiers.Add(new AttributeEffectModifier
            {
                Attribute = health,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource
                {
                    Type = ModifierValueSource.SourceType.Value,
                    Value = ScalableFloat.FromArray(-5f, -10f, -15f)
                },
            });
            using (var serialized = new SerializedObject(effect))
            {
                serialized.FindProperty("defaultLevel").intValue = defaultLevel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return effect;
        }

        private List<T> Collect<T>(EventChannel<T> channel)
        {
            var received = new List<T>();
            Action<T> onRaised = received.Add;
            channel.Raised += onRaised;
            return received;
        }

        // Acrescenta uma RaiseEventAction à lista do efeito e a configura pelo SerializedProperty, como o Inspector faz.
        private static void AddAction(GameplayEffect effect, string list, EventChannel channel, GasPayloadSource source,
            AttributeDefinition attribute = null, int modifierIndex = 0)
        {
            List<GameplayAction> actions = list == nameof(GameplayEffect.OnTick) ? effect.OnTick : effect.OnApplied;
            actions.Add(new RaiseEventAction());
            using (var serialized = new SerializedObject(effect))
            {
                SerializedProperty action = serialized.FindProperty(list).GetArrayElementAtIndex(actions.Count - 1);
                Configure(action, channel, source, attribute, modifierIndex);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private RaiseEventAction AbilityAction(EventChannel channel, GasPayloadSource source, AttributeDefinition attribute)
        {
            GameplayAbility ability = _gas.CreateAbility(new RaiseEventAction());
            using (var serialized = new SerializedObject(ability))
            {
                SerializedProperty action = serialized.FindProperty(nameof(GameplayAbility.OnActivate)).GetArrayElementAtIndex(0);
                Configure(action, channel, source, attribute, 0);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return (RaiseEventAction)ability.OnActivate[0];
        }

        private static void Configure(SerializedProperty action, EventChannel channel, GasPayloadSource source,
            AttributeDefinition attribute, int modifierIndex)
        {
            action.FindPropertyRelative("emission").managedReferenceValue = EventEmission.CreateFor(channel);
            action.FindPropertyRelative("payloadSource").enumValueIndex = (int)source;
            action.FindPropertyRelative("attribute").objectReferenceValue = attribute;
            action.FindPropertyRelative("modifierIndex").intValue = modifierIndex;
        }
    }
}
