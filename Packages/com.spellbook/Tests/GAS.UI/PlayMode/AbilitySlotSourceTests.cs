#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using SpellBook.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Fonte do slot (SL4): recarga, recarga reduzida, custo, tag bloqueadora, rótulo da tecla, o "pronto" igual à
    /// tentativa até o custo e atualizar sem alocar (CA2, CA3, CA5, CA6, CA10, CA14 e CA16).
    /// </summary>
    public class AbilitySlotSourceTests
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
        public void Cooldown8s_ReadyCooldownReady_FractionSeconds_PulseOnce_PauseStops()
        {
            AbilitySystemComponent asc = Asc(0f);
            GameplayAbility fireball = _gas.CreateAbilityWithCooldown(8f);
            asc.GiveAbility(fireball);
            Rig rig = CreateSlot(asc, fireball, null);
            SlotState initial = rig.View.Last.State;

            asc.TryActivateAbility(fireball, Vector3.zero);
            SlotModel started = rig.View.Last;
            Step(rig, asc, 2f);
            SlotModel after2 = rig.View.Last;
            int rendersPaused = rig.View.Renders;
            rig.Driver.Tick();
            rig.Driver.Tick();
            SlotModel paused = rig.View.Last;
            Step(rig, asc, 6f);

            Assert.That(initial, Is.EqualTo(SlotState.Ready));
            Assert.That(rig.View.First.ReadyPulseCount, Is.Zero, "não pulsa no primeiro desenho");
            Assert.That(started.State, Is.EqualTo(SlotState.OnCooldown));
            Assert.That(started.CooldownFraction, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(started.CooldownRemaining, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(after2.CooldownFraction, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(after2.CooldownRemaining, Is.EqualTo(6f).Within(1e-4f));
            Assert.That(rig.View.Renders, Is.GreaterThan(rendersPaused), "o driver lê a recarga a cada tick");
            Assert.That(paused.CooldownFraction, Is.EqualTo(after2.CooldownFraction), "em pausa o radial não anda");
            Assert.That(rig.View.Last.State, Is.EqualTo(SlotState.Ready));
            Assert.That(rig.View.Last.CooldownFraction, Is.Zero);
            Assert.That(rig.View.Last.ReadyPulseCount, Is.EqualTo(1));
            Assert.That(rig.Driver.TickingCount, Is.Zero, "pronto, o slot solta o tick");
        }

        [Test]
        public void ReducedCooldown_ShowsRealDuration()
        {
            AttributeDefinition cdr = _gas.CreateAttribute("CooldownReduction");
            AbilitySystemComponent asc = Asc(0f, (cdr, 0.25f));
            GameplayAbility fireball = _gas.CreateAbilityWithCooldown(8f);
            fireball.CooldownReductionAttribute = cdr;
            asc.GiveAbility(fireball);
            Rig rig = CreateSlot(asc, fireball, null);

            asc.TryActivateAbility(fireball, Vector3.zero);
            SlotModel started = rig.View.Last;
            Step(rig, asc, 3f);

            Assert.That(started.CooldownRemaining, Is.EqualTo(6f).Within(1e-4f), "8 s com 25 % de redução");
            Assert.That(rig.View.Last.CooldownRemaining, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(rig.View.Last.CooldownFraction, Is.EqualTo(0.5f).Within(1e-4f), "3 de 6 s, não 3 de 8 s");
        }

        [Test]
        public void LowMana_NoResource_BackWhenManaRises()
        {
            AbilitySystemComponent asc = Asc(5f);
            GameplayAbility fireball = _gas.CreateAbility();
            fireball.CostEffect = Cost(10f);
            asc.GiveAbility(fireball);
            Rig rig = CreateSlot(asc, fireball, null);
            SlotState low = rig.View.Last.State;

            State(asc, _mana).SetBaseValue(20f);

            Assert.That(low, Is.EqualTo(SlotState.NoResource));
            Assert.That(rig.View.Last.State, Is.EqualTo(SlotState.Ready), "a mana sobe e o slot volta por evento");
            Assert.That(asc.GetAttributeValue(_mana), Is.EqualTo(20f), "a consulta não cobra o custo");
        }

        [Test]
        public void BlockingTag_Blocked_BackOnRemove()
        {
            GameplayTag stunned = _gas.CreateTag("Stunned");
            AbilitySystemComponent asc = Asc(0f);
            GameplayAbility fireball = _gas.CreateAbility();
            fireball.BlockedTags.Add(stunned);
            asc.GiveAbility(fireball);
            Rig rig = CreateSlot(asc, fireball, null);

            asc.AddTag(stunned);
            SlotState blocked = rig.View.Last.State;
            asc.RemoveTag(stunned);

            Assert.That(blocked, Is.EqualTo(SlotState.Blocked));
            Assert.That(rig.View.Last.State, Is.EqualTo(SlotState.Ready));
        }

        [Test]
        public void KeyLabel_FromProvider_UpdatesOnChanged()
        {
            AbilitySystemComponent asc = Asc(0f);
            GameplayAbility fireball = _gas.CreateAbility();
            asc.GiveAbility(fireball);
            var labels = Track(ScriptableObject.CreateInstance<TestKeyLabels>());
            labels.Key = _slotQ.GetDefinition();
            labels.Label = "Q";
            Rig rig = CreateSlot(asc, fireball, labels);
            string first = Text(rig.View.Last.KeyLabel);

            labels.SetLabel("Botão A");

            Assert.That(first, Is.EqualTo("Q"));
            Assert.That(Text(rig.View.Last.KeyLabel), Is.EqualTo("Botão A"));
        }

        [Test]
        public void SlotState_MatchesTryActivate_UpToCost()
        {
            GameplayTag stunned = _gas.CreateTag("Stunned");
            GameplayTag ready = _gas.CreateTag("Ready");
            GameplayTag channel = _gas.CreateTag("Channel");
            var cases = new (string Name, SlotState Expected, AbilityActivationResult Try, System.Action<AbilitySystemComponent, GameplayAbility> Arrange)[]
            {
                ("não concedida", SlotState.Empty, AbilityActivationResult.NotGranted, (_, _) => { }),
                ("tag bloqueadora", SlotState.Blocked, AbilityActivationResult.BlockedByTags, (asc, a) => { Give(asc, a); asc.AddTag(stunned); }),
                ("tag exigida", SlotState.Blocked, AbilityActivationResult.MissingRequiredTags, (asc, a) => { Give(asc, a); a.RequiredTags.Add(ready); }),
                ("habilidade ativa", SlotState.Blocked, AbilityActivationResult.BlockedByActiveAbility, (asc, a) => BlockByActive(asc, a, channel)),
                ("recarga", SlotState.OnCooldown, AbilityActivationResult.OnCooldown, (asc, a) => { Give(asc, a); asc.TryActivateAbility(a, Vector3.zero); }),
                ("custo", SlotState.NoResource, AbilityActivationResult.CannotAffordCost, (asc, a) => { Give(asc, a); State(asc, _mana).SetBaseValue(5f); }),
                ("pronta", SlotState.Ready, AbilityActivationResult.Success, (asc, a) => Give(asc, a)),
            };

            foreach (var c in cases)
            {
                AbilitySystemComponent asc = Asc(50f);
                GameplayAbility ability = _gas.CreateAbilityWithCooldown(5f);
                ability.CostEffect = Cost(10f);
                ability.BlockedTags.Add(stunned);
                ability.AbilityTag = channel;
                c.Arrange(asc, ability);
                SlotState shown = CreateSlot(asc, ability, null).View.Last.State;

                AbilityActivationResult tried = asc.TryActivateAbility(ability, Vector3.zero);

                Assert.That(shown, Is.EqualTo(c.Expected), c.Name);
                Assert.That(tried, Is.EqualTo(c.Try), c.Name);
                Assert.That(shown == SlotState.Ready, Is.EqualTo(tried == AbilityActivationResult.Success), c.Name);
            }
        }

        [Test]
        public void SlotUpdates10000_DoNotAllocate()
        {
            GameplayTag stunned = _gas.CreateTag("Stunned");
            AbilitySystemComponent asc = Asc(50f);
            GameplayAbility fireball = _gas.CreateAbilityWithCooldown(100f);
            fireball.CostEffect = Cost(10f);
            fireball.BlockedTags.Add(stunned);
            asc.GiveAbility(fireball);
            var labels = Track(ScriptableObject.CreateInstance<TestKeyLabels>());
            labels.Key = _slotQ.GetDefinition();
            labels.Label = "Q";
            Rig rig = CreateSlot(asc, fireball, labels);
            asc.TryActivateAbility(fireball, Vector3.zero);
            Updates(rig, asc, stunned, 10);

            Assert.That(() => Updates(rig, asc, stunned, 10000), Is.Not.AllocatingGCMemory());
            Assert.That(rig.View.Last.State, Is.EqualTo(SlotState.OnCooldown));
            Assert.That(rig.View.Renders, Is.GreaterThan(20000), "cada tick e cada tag desenham o slot");
        }

        [Test]
        public void Preview_HalfCooldownTwoCharges_LabelQ()
        {
            GameplayAbility fireball = _gas.CreateAbilityWithCooldown(8f);
            var source = new AbilitySlotSource { Loadout = Loadout(fireball, null), Slot = _slotQ };

            SlotModel preview = source.CreatePreview(new UIPreviewContext(1));

            Assert.That(preview.Subject, Is.SameAs(fireball));
            Assert.That(preview.CooldownFraction, Is.EqualTo(0.5f));
            Assert.That(preview.CooldownRemaining, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(preview.Charges, Is.EqualTo(2));
            Assert.That(Text(preview.KeyLabel), Is.EqualTo("Q"));
            Assert.That(Text(source.CreatePreview(new UIPreviewContext(1)).KeyLabel), Is.EqualTo("Q"),
                "o buffer da prévia é guardado e escrito do zero");
        }

        // Uma atualização por tag (entra e sai) e um tick com a recarga correndo
        private static void Updates(Rig rig, AbilitySystemComponent asc, GameplayTag tag, int count)
        {
            for (int i = 0; i < count; i++)
            {
                asc.AddTag(tag);
                asc.RemoveTag(tag);
                Step(rig, asc, 0.001f);
            }
        }

        private static void Step(Rig rig, AbilitySystemComponent asc, float seconds)
        {
            rig.Clock.Advance(seconds);
            asc.Tick(seconds);
            rig.Driver.Tick();
        }

        private static void Give(AbilitySystemComponent asc, GameplayAbility ability) => asc.GiveAbility(ability);

        // Outra habilidade Toggle ligada bloqueia as da tag pelo BlockAbilitiesWithTag
        private void BlockByActive(AbilitySystemComponent asc, GameplayAbility ability, GameplayTag tag)
        {
            Give(asc, ability);
            GameplayAbility aura = _gas.CreateAbility();
            aura.Behavior = AbilityBehavior.Toggle;
            aura.BlockAbilitiesWithTag.Add(tag);
            asc.GiveAbility(aura);
            asc.TryActivateAbility(aura, Vector3.zero);
        }

        private AbilitySystemComponent Asc(float mana, params (AttributeDefinition, float)[] more)
        {
            var attributes = new (AttributeDefinition, float)[more.Length + 1];
            attributes[0] = (_mana, mana);
            more.CopyTo(attributes, 1);
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_clock);
            return asc;
        }

        private GameplayEffect Cost(float amount)
        {
            GameplayEffect cost = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            cost.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = _mana,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(-amount) }
            });
            return cost;
        }

        private static AttributeState State(AbilitySystemComponent asc, AttributeDefinition definition)
        {
            Assert.That(asc.AttributeSet.TryGetState(definition, out AttributeState state), Is.True);
            return state;
        }

        private static string Text(TextSpan span) => span.IsEmpty ? string.Empty : span.Buffer.ToString(span);

        /// <summary>Loadout com a entrada <c>Slot.Q</c>, gravado pelo <see cref="SerializedObject"/>.</summary>
        private AbilityLoadout Loadout(GameplayAbility ability, KeyLabelProvider labels)
        {
            var loadout = Track(ScriptableObject.CreateInstance<AbilityLoadout>());
            var serialized = new SerializedObject(loadout);
            SerializedProperty list = serialized.FindProperty("entries");
            list.InsertArrayElementAtIndex(0);
            list.GetArrayElementAtIndex(0).FindPropertyRelative("slot.definition").objectReferenceValue = _slotQ.GetDefinition();
            list.GetArrayElementAtIndex(0).FindPropertyRelative("ability").objectReferenceValue = ability;
            serialized.FindProperty("keyLabels").objectReferenceValue = labels;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return loadout;
        }

        // Slot sob um driver com o relógio manual, ligado ao ASC pelo dono explícito
        private Rig CreateSlot(AbilitySystemComponent asc, GameplayAbility ability, KeyLabelProvider labels)
        {
            var rig = new Rig { Clock = _clock };
            rig.Driver = Track(new GameObject("tela")).AddComponent<UIUpdateDriver>();
            rig.Driver.SetTimeSource(_clock);
            var slot = Track(new GameObject("slot"));
            slot.SetActive(false);
            slot.transform.SetParent(rig.Driver.transform);
            rig.View = slot.AddComponent<Recorder>();
            AbilitySlotBinder binder = slot.AddComponent<AbilitySlotBinder>();
            binder.SetOwner(ActorResolver.ForExplicit(asc));
            binder.SetSource(new AbilitySlotSource { Loadout = Loadout(ability, labels), Slot = _slotQ });
            binder.SetView(rig.View);
            slot.SetActive(true);
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
            public Recorder View;
        }

        /// <summary>View de teste: guarda o primeiro e o último modelo e conta os desenhos.</summary>
        public sealed class Recorder : MonoBehaviour, IView<SlotModel>
        {
            public int Renders;
            public SlotModel First;
            public SlotModel Last;

            public void Render(in SlotModel model)
            {
                if (Renders++ == 0) First = model;
                Last = model;
            }
        }

        /// <summary>Rótulo de teste: um texto para uma chave, trocado por código com aviso.</summary>
        public sealed class TestKeyLabels : KeyLabelProvider
        {
            public ScriptableObject Key;
            public string Label;

            public void SetLabel(string label)
            {
                Label = label;
                RaiseChanged();
            }

            public override bool TryWriteLabel(ScriptableObject slotKey, ITextProvider text, TextBuffer into)
            {
                if (slotKey == null || slotKey != Key) return false;

                into.Append(Label);
                return true;
            }
        }
    }
}
#endif
