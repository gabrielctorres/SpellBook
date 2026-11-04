#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Triggers;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Text;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using SpellBook.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Fonte do slot (SL7): marcas de ativa, alternar ligada, canalizando e autocast lidas do B-012, sem substituir o
    /// estado de falha (CA9, R6), e a falha do autocast que não aparece no slot (resto do CA7, R7).
    /// </summary>
    public class SlotActiveStateTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;
        private TimeFactory _time;
        private ManualTimeSource _clock;
        private AttributeDefinition _mana;
        private GameplayTag _slotQ;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _clock = _time.CreateManual();
            _mana = _gas.CreateAttribute("Mana");
            _slotQ = _gas.CreateTag("Slot.Q");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Channel_Toggle_Autocast_SetActiveFlags_StateUnchanged()
        {
            AbilitySystemComponent asc = Asc(15f);
            GameplayAbility plain = _gas.CreateAbility();
            GameplayAbility channel = Channel(3f);
            GameplayAbility aura = Toggle();
            aura.Autocast.Allowed = true;
            GameplayAbility bolt = Autocast(startsOn: false);
            asc.GiveAbility(plain);
            asc.GiveAbility(channel);
            asc.GiveAbility(aura);
            AbilitySpec boltSpec = asc.GiveAbility(bolt);
            Rig plainRig = CreateSlot(asc, plain);
            Rig emptyRig = CreateSlot(asc, _gas.CreateAbility());
            Rig channelRig = CreateSlot(asc, channel);
            Rig auraRig = CreateSlot(asc, aura);
            Rig boltRig = CreateSlot(asc, bolt);

            Assert.That(asc.TryActivateAbility(channel, AbilityTarget.None), Is.EqualTo(AbilityActivationResult.Success));
            SlotModel channeling = channelRig.View.Last;
            Assert.That(asc.TryActivateAbility(channel, AbilityTarget.None), Is.EqualTo(AbilityActivationResult.AlreadyActive));
            SlotModel retried = channelRig.View.Last;
            Step(channelRig, asc, 3.5f);
            SlotModel channelEnded = channelRig.View.Last;
            Assert.That(asc.TryActivateAbility(aura, AbilityTarget.None), Is.EqualTo(AbilityActivationResult.Success));
            SlotModel auraOn = auraRig.View.Last;
            asc.TryActivateAbility(aura, AbilityTarget.None);
            SlotModel auraOff = auraRig.View.Last;
            SlotModel boltBefore = boltRig.View.Last;
            Assert.That(asc.SetAutocast(boltSpec, true), Is.True);
            SlotModel boltOn = boltRig.View.Last;

            AssertNoFlags(plainRig.View.Last, "habilidade comum");
            Assert.That(plainRig.View.Last.State, Is.EqualTo(SlotState.Ready));
            AssertNoFlags(emptyRig.View.Last, "slot vazio");
            Assert.That(emptyRig.View.Last.State, Is.EqualTo(SlotState.Empty));

            Assert.That(channeling.IsActive, Is.True);
            Assert.That(channeling.IsChanneling, Is.True);
            Assert.That(channeling.IsToggledOn, Is.False);
            Assert.That(channeling.State, Is.EqualTo(SlotState.Ready), "ativa é marca, não estado");
            Assert.That(retried.FailureKey, Is.EqualTo(GasTextKeys.ActivationAlreadyActive));
            Assert.That(retried.FailureVisible, Is.True);
            Assert.That(retried.IsChanneling, Is.True, "o motivo não apaga a marca");
            AssertNoFlags(channelEnded, "a canalização acabou");

            Assert.That(auraOn.IsActive, Is.True);
            Assert.That(auraOn.IsToggledOn, Is.True);
            Assert.That(auraOn.IsChanneling, Is.False);
            Assert.That(auraOn.AutocastAllowed, Is.False, "Toggle não aceita autocast, como no SetAutocast");
            AssertNoFlags(auraOff, "o toggle desligou");
            Assert.That(auraOff.State, Is.EqualTo(SlotState.NoResource), "desligada, volta o estado de falha");

            Assert.That(boltBefore.AutocastAllowed, Is.True);
            Assert.That(boltBefore.AutocastEnabled, Is.False);
            Assert.That(boltOn.AutocastAllowed, Is.True);
            Assert.That(boltOn.AutocastEnabled, Is.True);
            Assert.That(boltOn.IsActive, Is.False);
            Assert.That(boltOn.State, Is.EqualTo(SlotState.NoResource), "o autocast não substitui a falha");
        }

        [Test]
        public void AutocastFailure_NotShown()
        {
            AbilitySystemComponent asc = Asc(15f);
            GameplayAbility bolt = Autocast(startsOn: true);
            asc.GiveAbility(bolt);
            Rig rig = CreateSlot(asc, bolt);

            for (int i = 0; i < 4; i++) Step(rig, asc, 1f);
            SlotModel afterAutocast = rig.View.Last;
            float mana = asc.GetAttributeValue(_mana);
            asc.TryActivateAbility(bolt, AbilityTarget.None);

            Assert.That(mana, Is.EqualTo(5f), "a primeira passada ativou pelo autocast; as outras caem no custo");
            Assert.That(afterAutocast.State, Is.EqualTo(SlotState.NoResource));
            Assert.That(afterAutocast.AutocastEnabled, Is.True);
            Assert.That(afterAutocast.FailureCount, Is.Zero, "a falha do autocast é silenciosa");
            Assert.That(afterAutocast.FailureVisible, Is.False);
            Assert.That(afterAutocast.FailureKey, Is.Null);
            Assert.That(rig.View.Last.FailureCount, Is.EqualTo(1), "a tentativa manual mostra o motivo");
            Assert.That(rig.View.Last.FailureKey, Is.EqualTo(GasTextKeys.ActivationCannotAffordCost));
        }

        [Test]
        public void ActiveFlags_ByEvent()
        {
            AbilitySystemComponent asc = Asc(100f);
            GameplayAbility bolt = Autocast(startsOn: false);
            GameplayAbility aura = Toggle();
            GameplayAbility channel = Channel(0f);
            AbilitySpec boltSpec = asc.GiveAbility(bolt);
            asc.GiveAbility(aura);
            AbilitySpec channelSpec = asc.GiveAbility(channel);
            Rig boltRig = CreateSlot(asc, bolt);
            Rig auraRig = CreateSlot(asc, aura);
            Rig channelRig = CreateSlot(asc, channel);

            int rendersBefore = boltRig.View.Renders;
            asc.SetAutocast(boltSpec, true);
            SlotModel autocastOn = boltRig.View.Last;
            int rendersOn = boltRig.View.Renders - rendersBefore;
            asc.SetAutocast(boltSpec, false);
            SlotModel autocastOff = boltRig.View.Last;
            asc.TryActivateAbility(aura, AbilityTarget.None);
            SlotModel auraOn = auraRig.View.Last;
            asc.TryActivateAbility(aura, AbilityTarget.None);
            SlotModel auraOff = auraRig.View.Last;
            asc.TryActivateAbility(channel, AbilityTarget.None);
            SlotModel channeling = channelRig.View.Last;
            asc.CancelAbility(channelSpec);
            SlotModel cancelled = channelRig.View.Last;
            int ticking = boltRig.Driver.TickingCount + auraRig.Driver.TickingCount + channelRig.Driver.TickingCount;
            boltRig.Slot.SetActive(false);
            int rendersDetached = boltRig.View.Renders;
            asc.SetAutocast(boltSpec, true);

            Assert.That(autocastOn.AutocastEnabled, Is.True, "o autocast chega pelo evento");
            Assert.That(rendersOn, Is.EqualTo(1), "um desenho por evento");
            Assert.That(autocastOff.AutocastEnabled, Is.False);
            Assert.That(auraOn.IsToggledOn, Is.True);
            Assert.That(auraOff.IsToggledOn, Is.False);
            Assert.That(auraOff.IsActive, Is.False);
            Assert.That(channeling.IsChanneling, Is.True);
            Assert.That(cancelled.IsChanneling, Is.False, "cancelada, a marca sai");
            Assert.That(cancelled.IsActive, Is.False);
            Assert.That(ticking, Is.Zero, "as marcas não pedem tick");
            Assert.That(boltRig.View.Renders, Is.EqualTo(rendersDetached), "desligado, o slot solta o evento do autocast");
        }

        private static void AssertNoFlags(SlotModel model, string message)
        {
            Assert.That(model.IsActive, Is.False, message);
            Assert.That(model.IsToggledOn, Is.False, message);
            Assert.That(model.IsChanneling, Is.False, message);
            Assert.That(model.AutocastAllowed, Is.False, message);
            Assert.That(model.AutocastEnabled, Is.False, message);
        }

        private static void Step(Rig rig, AbilitySystemComponent asc, float seconds)
        {
            rig.Clock.Advance(seconds);
            asc.Tick(seconds);
            rig.Driver.Tick();
        }

        private AbilitySystemComponent Asc(float mana)
        {
            AbilitySystemComponent asc = _gas.CreateAsc((_mana, mana));
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_clock);
            return asc;
        }

        // Canaliza pela duração; 0 canaliza até ser cancelada
        private GameplayAbility Channel(float duration)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Behavior = AbilityBehavior.Channel;
            ability.ChannelDuration = ScalableFloat.Constant(duration);
            return ability;
        }

        // Alternar que custa 10 de mana ao ligar
        private GameplayAbility Toggle()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Behavior = AbilityBehavior.Toggle;
            ability.CostEffect = Cost(-10f);
            return ability;
        }

        // Instantânea de 10 de mana que o autocast tenta quando pronta
        private GameplayAbility Autocast(bool startsOn)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.CostEffect = Cost(-10f);
            ability.Autocast.Allowed = true;
            ability.Autocast.StartsOn = startsOn;
            ability.Triggers.Add(new WhenReadyTrigger { OnlyWithAutocast = true });
            return ability;
        }

        private GameplayEffect Cost(float amount)
        {
            GameplayEffect cost = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            cost.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = _mana,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource
                {
                    Type = ModifierValueSource.SourceType.Value,
                    Value = ScalableFloat.Constant(amount)
                }
            });
            return cost;
        }

        // Slot sob um driver com o relógio manual, ligado ao ASC pelo dono explícito
        private Rig CreateSlot(AbilitySystemComponent asc, GameplayAbility ability)
        {
            var loadout = Track(ScriptableObject.CreateInstance<AbilityLoadout>());
            using (var serialized = new SerializedObject(loadout))
            {
                SerializedProperty list = serialized.FindProperty("entries");
                list.InsertArrayElementAtIndex(0);
                list.GetArrayElementAtIndex(0).FindPropertyRelative("slot.definition").objectReferenceValue = _slotQ.GetDefinition();
                list.GetArrayElementAtIndex(0).FindPropertyRelative("ability").objectReferenceValue = ability;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var rig = new Rig { Clock = _clock };
            rig.Driver = Track(new GameObject("tela")).AddComponent<UIUpdateDriver>();
            rig.Driver.SetTimeSource(_clock);
            rig.Slot = Track(new GameObject("slot"));
            rig.Slot.SetActive(false);
            rig.Slot.transform.SetParent(rig.Driver.transform);
            rig.View = rig.Slot.AddComponent<Recorder>();
            AbilitySlotBinder binder = rig.Slot.AddComponent<AbilitySlotBinder>();
            binder.SetOwner(ActorResolver.ForExplicit(asc));
            binder.SetSource(new AbilitySlotSource { Loadout = loadout, Slot = _slotQ });
            binder.SetView(rig.View);
            rig.Slot.SetActive(true);
            return rig;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private sealed class Rig
        {
            public ManualTimeSource Clock;
            public UIUpdateDriver Driver;
            public GameObject Slot;
            public Recorder View;
        }

        /// <summary>View de teste: guarda o último modelo e conta os desenhos.</summary>
        public sealed class Recorder : MonoBehaviour, IView<SlotModel>
        {
            public SlotModel Last;
            public int Renders;

            public void Render(in SlotModel model)
            {
                Renders++;
                Last = model;
            }
        }
    }
}
#endif
