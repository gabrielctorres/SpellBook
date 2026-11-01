using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Events;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>
    /// <see cref="AbilitySystemEventRelay"/>: pares de efeito, bloqueio, habilidade, atributo e tag, ciclo de vida e
    /// reentrada (CA11 a CA19).
    /// </summary>
    public class EventRelayTests
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
        public IEnumerator Blocked_RaisesChannel_ListenerReacts()
        {
            GameplayTag immune = _gas.CreateTag("Imune.Veneno");
            GameplayEffect poison = _gas.CreateDurationEffect(5f);
            poison.name = "Veneno";
            poison.ApplicationImmunityTags.Add(immune);
            var channel = _events.CreateChannel<EffectEventChannel>();
            var names = _events.CreateChannel<StringEventChannel>();
            AbilitySystemComponent target = _gas.CreateAsc();
            AbilitySystemComponent caster = _gas.CreateAsc();
            target.ApplyEffect(_gas.CreateDurationEffect(10f, immune));
            CreateRelay(target, so => SetPair(so, "effectBlocked", channel, names));
            CreateLogListener(channel, "som de imune");
            LogAssert.Expect(LogType.Log, "som de imune");
            yield return null;

            target.ApplyEffect(poison, caster);

            EffectEventData data = channel.LastPayload;
            Assert.That(data.Owner, Is.SameAs(target));
            Assert.That(data.Source, Is.SameAs(caster));
            Assert.That(data.Effect, Is.SameAs(poison));
            Assert.That(data.Result, Is.EqualTo(EffectApplicationResult.BlockedByImmunity));
            Assert.That(names.LastPayload, Is.EqualTo("Veneno"), "o canal de texto não recebeu o nome do efeito");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator UnityEventWithoutChannel_AndChannelWithoutUnityEvent()
        {
            GameplayTag reason = _gas.CreateTag("Bloqueio.Silenciado");
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            var blocked = _events.CreateChannel<BlockedEventChannel>();
            AbilitySystemComponent asc = _gas.CreateAsc();
            AbilitySystemComponent caster = _gas.CreateAsc();
            var recorder = CreateObject("Recorder").AddComponent<Recorder>();
            CreateRelay(asc, so =>
            {
                AddPersistentCall(so.FindProperty("effectApplied.response"), recorder, nameof(Recorder.OnEffect));
                SetPair(so, "blocked", blocked);
            });
            int raises = 0;
            Action<BlockedEventData> onBlocked = _ => raises++;
            blocked.Raised += onBlocked;
            yield return null;

            asc.ApplyEffect(effect, caster);
            asc.ReportBlocked(reason, caster);
            blocked.Raised -= onBlocked;

            Assert.That(recorder.Effects, Is.EqualTo(new[] { effect }), "o UnityEvent sem canal não disparou uma vez");
            Assert.That(raises, Is.EqualTo(1), "o canal sem UnityEvent não disparou uma vez");
            Assert.That(blocked.LastPayload.Owner, Is.SameAs(asc));
            Assert.That(blocked.LastPayload.Reason, Is.EqualTo(reason));
            Assert.That(blocked.LastPayload.Source, Is.SameAs(caster));
        }

        [UnityTest]
        public IEnumerator EffectAppliedRemovedStacks_Relayed()
        {
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            effect.CanStack = true;
            effect.MaxStacks = 3;
            var applied = _events.CreateChannel<EffectEventChannel>();
            var stacks = _events.CreateChannel<EffectEventChannel>();
            var stackCount = _events.CreateChannel<FloatEventChannel>();
            var removed = _events.CreateChannel<EffectEventChannel>();
            AbilitySystemComponent asc = _gas.CreateAsc();
            AbilitySystemComponent caster = _gas.CreateAsc();
            CreateRelay(asc, so =>
            {
                SetPair(so, "effectApplied", applied);
                SetPair(so, "effectStacksChanged", stacks, stackCount);
                SetPair(so, "effectRemoved", removed);
            });
            yield return null;

            ActiveEffectInstance instance = asc.ApplyEffect(effect, caster);
            asc.ApplyEffect(effect, caster);
            asc.RemoveEffect(instance);

            Assert.That(applied.LastPayload.Effect, Is.SameAs(effect));
            Assert.That(applied.LastPayload.Owner, Is.SameAs(asc));
            Assert.That(applied.LastPayload.Source, Is.SameAs(caster));
            Assert.That(applied.LastPayload.Stacks, Is.EqualTo(1));
            Assert.That(applied.LastPayload.Result, Is.EqualTo(EffectApplicationResult.Applied));
            Assert.That(stacks.LastPayload.PreviousStacks, Is.EqualTo(1));
            Assert.That(stacks.LastPayload.Stacks, Is.EqualTo(2));
            Assert.That(stackCount.LastPayload, Is.EqualTo(2f), "o canal de valor não recebeu as pilhas");
            Assert.That(removed.LastPayload.Effect, Is.SameAs(effect));
            Assert.That(removed.LastPayload.RemovalReason, Is.EqualTo(EffectRemovalReason.Removed));
            Assert.That(removed.LastPayload.PreviousStacks, Is.EqualTo(2));
            Assert.That(removed.LastPayload.Stacks, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator DisableAndDestroy_Unsubscribe_DestroyAscFirst_NoException()
        {
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            var applied = _events.CreateChannel<EffectEventChannel>();
            AbilitySystemComponent asc = _gas.CreateAsc();
            AbilitySystemEventRelay relay = CreateRelay(asc, so => SetPair(so, "effectApplied", applied));
            int raises = 0;
            Action<EffectEventData> onApplied = _ => raises++;
            applied.Raised += onApplied;
            yield return null;

            relay.enabled = false;
            asc.RemoveEffect(asc.ApplyEffect(effect));
            int afterDisable = raises;
            relay.enabled = true;
            asc.RemoveEffect(asc.ApplyEffect(effect));
            int afterEnable = raises;
            Object.Destroy(relay);
            yield return null;
            asc.ApplyEffect(effect);
            applied.Raised -= onApplied;

            AbilitySystemComponent other = _gas.CreateAsc();
            AbilitySystemEventRelay otherRelay = CreateRelay(other, so => SetPair(so, "effectApplied", applied));
            Object.Destroy(other.gameObject);
            yield return null;
            Object.Destroy(otherRelay.gameObject);
            yield return null;

            Assert.That(afterDisable, Is.EqualTo(0), "o relay desabilitado repassou o evento");
            Assert.That(afterEnable, Is.EqualTo(1), "o relay reabilitado não voltou a repassar");
            Assert.That(raises, Is.EqualTo(1), "o relay destruído repassou o evento");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ListenerAppliesEffectInsideCallback_NoExceptionNoRepeat()
        {
            GameplayEffect first = _gas.CreateDurationEffect(5f);
            GameplayEffect second = _gas.CreateDurationEffect(5f);
            var applied = _events.CreateChannel<EffectEventChannel>();
            AbilitySystemComponent asc = _gas.CreateAsc();
            CreateRelay(asc, so => SetPair(so, "effectApplied", applied));
            var received = new List<GameplayEffect>();
            Action<EffectEventData> onApplied = data =>
            {
                received.Add(data.Effect);
                if (data.Effect != first) return;
                asc.ApplyEffect(first);
                asc.ApplyEffect(second);
            };
            applied.Raised += onApplied;
            yield return null;

            Assert.DoesNotThrow(() => asc.ApplyEffect(first));
            applied.Raised -= onApplied;

            Assert.That(received, Is.EqualTo(new[] { first, second }), "o relay repetiu ou perdeu um disparo");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AttributeChanged_RaisesFloatWithNewValue()
        {
            AttributeDefinition health = _gas.CreateAttribute("Vida");
            var changed = _events.CreateChannel<AttributeChangedEventChannel>();
            var values = _events.CreateChannel<FloatEventChannel>();
            AbilitySystemComponent asc = _gas.CreateAsc((health, 100f));
            CreateRelay(asc, so => SetPair(so, "attributeChanged", changed, values));
            yield return null;

            State(asc, health).SetBaseValue(70f);

            AttributeChangedData data = changed.LastPayload;
            Assert.That(data.Owner, Is.SameAs(asc));
            Assert.That(data.Attribute, Is.SameAs(health));
            Assert.That(data.OldValue, Is.EqualTo(100f));
            Assert.That(data.NewValue, Is.EqualTo(70f));
            Assert.That(data.Delta, Is.EqualTo(-30f));
            Assert.That(values.LastPayload, Is.EqualTo(70f), "o canal de valor não recebeu o valor novo");
        }

        [UnityTest]
        public IEnumerator AttributeDecreased_OnlyOnDrop_NegativeDelta_FilterIgnoresOthers()
        {
            AttributeDefinition health = _gas.CreateAttribute("Vida");
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            var decreased = _events.CreateChannel<AttributeChangedEventChannel>();
            var deltas = _events.CreateChannel<FloatEventChannel>();
            var unfiltered = _events.CreateChannel<AttributeChangedEventChannel>();
            AbilitySystemComponent asc = _gas.CreateAsc((health, 100f), (mana, 50f));
            CreateRelay(asc, so =>
            {
                SetPair(so, "attributeDecreased", decreased, deltas);
                so.FindProperty("decreasedAttribute").objectReferenceValue = health;
            });
            AbilitySystemComponent other = _gas.CreateAsc((health, 100f), (mana, 50f));
            CreateRelay(other, so => SetPair(so, "attributeDecreased", unfiltered));
            var drops = new List<float>();
            Action<AttributeChangedData> onDecreased = data => drops.Add(data.Delta);
            decreased.Raised += onDecreased;
            int unfilteredRaises = 0;
            Action<AttributeChangedData> onUnfiltered = _ => unfilteredRaises++;
            unfiltered.Raised += onUnfiltered;
            yield return null;

            State(asc, health).SetBaseValue(70f);
            State(asc, health).SetBaseValue(80f);
            State(asc, mana).SetBaseValue(20f);
            State(other, health).SetBaseValue(90f);
            State(other, mana).SetBaseValue(40f);
            State(other, mana).SetBaseValue(45f);
            decreased.Raised -= onDecreased;
            unfiltered.Raised -= onUnfiltered;

            Assert.That(drops, Is.EqualTo(new[] { -30f }), "o filtro deixou passar outro atributo ou uma subida");
            Assert.That(decreased.LastPayload.Attribute, Is.SameAs(health));
            Assert.That(deltas.LastPayload, Is.EqualTo(-30f), "o canal de valor não recebeu a variação negativa");
            Assert.That(unfilteredRaises, Is.EqualTo(2), "sem filtro, o par não disparou nas duas quedas");
        }

        [UnityTest]
        public IEnumerator TagAddedRemoved_WithCount()
        {
            GameplayTag burning = _gas.CreateTag("Estado.Queimando");
            var added = _events.CreateChannel<TagChangedEventChannel>();
            var addedCount = _events.CreateChannel<FloatEventChannel>();
            var removed = _events.CreateChannel<TagChangedEventChannel>();
            var removedName = _events.CreateChannel<StringEventChannel>();
            AbilitySystemComponent asc = _gas.CreateAsc();
            CreateRelay(asc, so =>
            {
                SetPair(so, "tagAdded", added, addedCount);
                SetPair(so, "tagRemoved", removed, removedName);
            });
            int addedRaises = 0;
            int removedRaises = 0;
            Action<TagChangedData> onAdded = _ => addedRaises++;
            Action<TagChangedData> onRemoved = _ => removedRaises++;
            added.Raised += onAdded;
            removed.Raised += onRemoved;
            yield return null;

            asc.AddTag(burning);
            TagChangedData afterAdd = added.LastPayload;
            asc.AddTag(burning);
            asc.RemoveTag(burning);
            asc.RemoveTag(burning);
            added.Raised -= onAdded;
            removed.Raised -= onRemoved;

            Assert.That(addedRaises, Is.EqualTo(1));
            Assert.That(removedRaises, Is.EqualTo(1));
            Assert.That(afterAdd.Owner, Is.SameAs(asc));
            Assert.That(afterAdd.Tag, Is.EqualTo(burning));
            Assert.That(afterAdd.Added, Is.True);
            Assert.That(afterAdd.Count, Is.EqualTo(1));
            Assert.That(addedCount.LastPayload, Is.EqualTo(1f), "o canal de valor não recebeu a contagem");
            Assert.That(removed.LastPayload.Added, Is.False);
            Assert.That(removed.LastPayload.Count, Is.EqualTo(0));
            Assert.That(removedName.LastPayload, Is.EqualTo(burning.Name), "o canal de texto não recebeu o nome da tag");
        }

        [UnityTest]
        public IEnumerator AbilityActivatedEndedFailed_OnceEach_WithResult()
        {
            GameplayAbility ability = _gas.CreateAbility();
            GameplayAbility notGranted = _gas.CreateAbility();
            var activated = _events.CreateChannel<AbilityEventChannel>();
            var activatedLevel = _events.CreateChannel<FloatEventChannel>();
            var ended = _events.CreateChannel<AbilityEventChannel>();
            var failed = _events.CreateChannel<AbilityEventChannel>();
            AbilitySystemComponent asc = _gas.CreateAsc();
            asc.GiveAbility(ability);
            CreateRelay(asc, so =>
            {
                SetPair(so, "abilityActivated", activated, activatedLevel);
                SetPair(so, "abilityEnded", ended);
                SetPair(so, "abilityFailed", failed);
            });
            var order = new List<string>();
            Action<AbilityEventData> onActivated = _ => order.Add("activated");
            Action<AbilityEventData> onEnded = _ => order.Add("ended");
            Action<AbilityEventData> onFailed = _ => order.Add("failed");
            activated.Raised += onActivated;
            ended.Raised += onEnded;
            failed.Raised += onFailed;
            yield return null;

            AbilityActivationResult success = asc.TryActivateAbility(ability, Vector3.zero);
            yield return null; // a sequência vazia termina na rotina, no quadro seguinte
            AbilityActivationResult failure = asc.TryActivateAbility(notGranted, Vector3.zero);
            activated.Raised -= onActivated;
            ended.Raised -= onEnded;
            failed.Raised -= onFailed;

            Assert.That(success, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(failure, Is.EqualTo(AbilityActivationResult.NotGranted));
            Assert.That(order, Is.EqualTo(new[] { "activated", "ended", "failed" }));
            Assert.That(activated.LastPayload.Owner, Is.SameAs(asc));
            Assert.That(activated.LastPayload.Ability, Is.SameAs(ability));
            Assert.That(activated.LastPayload.Level, Is.EqualTo(1));
            Assert.That(activatedLevel.LastPayload, Is.EqualTo(1f), "o canal de valor não recebeu o nível");
            Assert.That(ended.LastPayload.Ability, Is.SameAs(ability));
            Assert.That(ended.LastPayload.Cancelled, Is.False);
            Assert.That(failed.LastPayload.Ability, Is.SameAs(notGranted));
            Assert.That(failed.LastPayload.FailureReason, Is.EqualTo(AbilityActivationResult.NotGranted));
            Assert.That(failed.LastPayload.Level, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator AbilityGrantedRemoved_Relayed()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.name = "Bola de Fogo";
            var granted = _events.CreateChannel<AbilityEventChannel>();
            var grantedName = _events.CreateChannel<StringEventChannel>();
            var removed = _events.CreateChannel<AbilityEventChannel>();
            var removedOwner = _events.CreateChannel<GameObjectEventChannel>();
            AbilitySystemComponent asc = _gas.CreateAsc();
            CreateRelay(asc, so =>
            {
                SetPair(so, "abilityGranted", granted, grantedName);
                SetPair(so, "abilityRemoved", removed, removedOwner);
            });
            yield return null;

            asc.GiveAbility(ability);
            asc.RemoveAbility(ability);

            Assert.That(granted.LastPayload.Owner, Is.SameAs(asc));
            Assert.That(granted.LastPayload.Ability, Is.SameAs(ability));
            Assert.That(granted.LastPayload.Level, Is.EqualTo(1));
            Assert.That(grantedName.LastPayload, Is.EqualTo("Bola de Fogo"), "o canal de texto não recebeu o nome da habilidade");
            Assert.That(removed.LastPayload.Ability, Is.SameAs(ability));
            Assert.That(removedOwner.LastPayload, Is.SameAs(asc.gameObject), "o canal de objeto não recebeu o dono");
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator RecentFirings_KeepsLastTen_OldestLeaves()
        {
            GameplayTag burning = _gas.CreateTag("Estado.Queimando");
            AbilitySystemComponent asc = _gas.CreateAsc();
            AbilitySystemEventRelay relay = CreateRelay(asc, _ => { });
            yield return null;
            asc.AddTag(burning);
            yield return null;
            int frame = UnityEngine.Time.frameCount;

            for (int i = 0; i < 5; i++)
            {
                asc.RemoveTag(burning);
                asc.AddTag(burning);
            }

            var pairs = new List<string>();
            var frames = new List<int>();
            for (int i = 0; i < relay.RecentFiringCount; i++)
            {
                AbilitySystemEventRelay.RelayFiring firing = relay.GetRecentFiring(i);
                pairs.Add(firing.Pair);
                frames.Add(firing.Frame);
            }

            Assert.That(relay.RecentFiringCount, Is.EqualTo(10));
            for (int i = 0; i < pairs.Count; i++)
            {
                Assert.That(pairs[i], Is.EqualTo(i % 2 == 0 ? "tagRemoved" : "tagAdded"), "disparo " + i);
            }

            Assert.That(frames, Has.All.EqualTo(frame), "o disparo mais antigo, do quadro anterior, não saiu");
        }
#endif

        private static AttributeState State(AbilitySystemComponent asc, AttributeDefinition attribute)
        {
            Assert.That(asc.AttributeSet.TryGetState(attribute, out AttributeState state), Is.True);
            return state;
        }

        private GameObject CreateObject(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        // Relay num objeto próprio, configurado pelo SerializedObject antes de ativar, como o Inspector faria.
        private AbilitySystemEventRelay CreateRelay(AbilitySystemComponent asc, Action<SerializedObject> configure)
        {
            GameObject go = CreateObject("Relay");
            go.SetActive(false);
            var relay = go.AddComponent<AbilitySystemEventRelay>();
            using (var serialized = new SerializedObject(relay))
            {
                serialized.FindProperty("abilitySystem").objectReferenceValue = asc;
                configure(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            go.SetActive(true);
            return relay;
        }

        private static void SetPair(SerializedObject serialized, string pair, EventChannel channel, EventChannel valueChannel = null)
        {
            serialized.FindProperty(pair + ".channel").objectReferenceValue = channel;
            serialized.FindProperty(pair + ".valueChannel").objectReferenceValue = valueChannel;
        }

        // Mesmo formato que o drawer de UnityEvent grava quando o designer liga um método no Inspector.
        private static void AddPersistentCall(SerializedProperty unityEvent, Object target, string method)
        {
            SerializedProperty calls = unityEvent.FindPropertyRelative("m_PersistentCalls.m_Calls");
            calls.arraySize = 1;
            SerializedProperty call = calls.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = method;
            call.FindPropertyRelative("m_Mode").intValue = (int)PersistentListenerMode.EventDefined;
            call.FindPropertyRelative("m_CallState").intValue = (int)UnityEventCallState.RuntimeOnly;
        }

        // Ouvinte com uma resposta de efeito e um Log, configurado pelo SerializedProperty, como o Inspector faz.
        private void CreateLogListener(EventChannel channel, string message)
        {
            GameObject go = CreateObject("Listener");
            go.SetActive(false);
            var listener = go.AddComponent<EventListener>();
            using (var serialized = new SerializedObject(listener))
            {
                SerializedProperty responses = serialized.FindProperty("responses");
                responses.arraySize = 1;
                SerializedProperty response = responses.GetArrayElementAtIndex(0);
                response.managedReferenceValue = new EffectEventResponse();
                response.FindPropertyRelative("channel").objectReferenceValue = channel;

                SerializedProperty reactions = response.FindPropertyRelative("reactions");
                reactions.arraySize = 1;
                SerializedProperty reaction = reactions.GetArrayElementAtIndex(0);
                reaction.managedReferenceValue = new LogReaction();
                reaction.FindPropertyRelative("message").stringValue = message;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            go.SetActive(true);
        }

        public sealed class Recorder : MonoBehaviour
        {
            public readonly List<GameplayEffect> Effects = new List<GameplayEffect>();

            public void OnEffect(EffectEventData data)
            {
                Effects.Add(data.Effect);
            }
        }
    }
}
