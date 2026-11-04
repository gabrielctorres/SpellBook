#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Conditions;
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
    /// Fonte do slot (SL5): cargas e a recarga da próxima, pulso na carga que volta, motivo da falha com chave, tag e
    /// texto do provedor na ordem da ADR 0022, e o nível que muda custo e recarga (CA4 e CA7, menos o autocast).
    /// </summary>
    public class SlotChargesAndFailureTests
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
        public void Charges3_Spend_Shows2_NextRadial_NoCharges()
        {
            AbilitySystemComponent asc = Asc(0f);
            GameplayAbility dash = Charged(3, 4f);
            asc.GiveAbility(dash);
            Rig rig = CreateSlot(asc, dash);
            SlotModel full = rig.View.Last;

            asc.TryActivateAbility(dash, Vector3.zero);
            SlotModel spent = rig.View.Last;
            Step(rig, asc, 1f);
            SlotModel after1 = rig.View.Last;
            asc.TryActivateAbility(dash, Vector3.zero);
            asc.TryActivateAbility(dash, Vector3.zero);

            Assert.That(full.HasCharges, Is.True);
            Assert.That(full.Charges, Is.EqualTo(3));
            Assert.That(full.MaxCharges, Is.EqualTo(3));
            Assert.That(full.ChargeFraction, Is.Zero, "cheio, a próxima carga não corre");
            Assert.That(spent.Charges, Is.EqualTo(2));
            Assert.That(spent.ChargeFraction, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(after1.ChargeFraction, Is.EqualTo(0.75f).Within(1e-4f), "o radial da próxima carga anda no tick");
            Assert.That(rig.View.Last.Charges, Is.Zero);
            Assert.That(rig.View.Last.State, Is.EqualTo(SlotState.NoCharges));
            Assert.That(asc.TryActivateAbility(dash, Vector3.zero), Is.EqualTo(AbilityActivationResult.NoCharges));
        }

        [Test]
        public void ChargeBack_PulsesOnce()
        {
            AbilitySystemComponent asc = Asc(0f);
            GameplayAbility dash = Charged(2, 2f);
            asc.GiveAbility(dash);
            Rig rig = CreateSlot(asc, dash);

            asc.TryActivateAbility(dash, Vector3.zero);
            int spentPulses = rig.View.Last.ReadyPulseCount;
            Step(rig, asc, 2f);

            Assert.That(rig.View.First.ReadyPulseCount, Is.Zero, "não pulsa no primeiro desenho");
            Assert.That(spentPulses, Is.Zero, "gastar não pulsa");
            Assert.That(rig.View.Last.Charges, Is.EqualTo(2));
            Assert.That(rig.View.Last.ReadyPulseCount, Is.EqualTo(1));
            Assert.That(rig.Driver.TickingCount, Is.Zero, "cheio, o slot solta o tick");
        }

        [Test]
        public void LowMana_TryActivate_FailureKeyTagText_OneSecond_CountUp()
        {
            AbilitySystemComponent asc = Asc(5f);
            GameplayAbility fireball = _gas.CreateAbility();
            fireball.CostEffect = Cost(ScalableFloat.Constant(-10f));
            asc.GiveAbility(fireball);
            Rig rig = CreateSlot(asc, fireball);
            SlotModel before = rig.View.Last;

            asc.GetActivationResult(fireball, AbilityTarget.None, out ActivationFailure _);
            int afterQuery = rig.View.Last.FailureCount;
            asc.TryActivateAbility(fireball, Vector3.zero);
            SlotModel failed = rig.View.Last;
            Step(rig, asc, 0.5f);
            bool visibleAtHalf = rig.View.Last.FailureVisible;
            Step(rig, asc, 0.6f);
            SlotModel expired = rig.View.Last;
            int tickingExpired = rig.Driver.TickingCount;
            asc.TryActivateAbility(fireball, Vector3.zero);

            Assert.That(before.FailureCount, Is.Zero);
            Assert.That(before.FailureVisible, Is.False);
            Assert.That(afterQuery, Is.Zero, "a consulta não é falha");
            Assert.That(failed.State, Is.EqualTo(SlotState.NoResource));
            Assert.That(failed.FailureKey, Is.EqualTo(GasTextKeys.ActivationCannotAffordCost));
            Assert.That(failed.FailureTag, Is.Null, "a falha de custo não tem tag");
            Assert.That(Text(failed.FailureText), Is.EqualTo("Recurso insuficiente"), "o texto vem do provedor do tema");
            Assert.That(failed.FailureVisible, Is.True);
            Assert.That(failed.FailureCount, Is.EqualTo(1));
            Assert.That(visibleAtHalf, Is.True);
            Assert.That(expired.FailureVisible, Is.False, "some depois de 1 s");
            Assert.That(expired.FailureKey, Is.EqualTo(GasTextKeys.ActivationCannotAffordCost), "o último motivo fica");
            Assert.That(tickingExpired, Is.Zero, "sem motivo visível, o slot solta o tick");
            Assert.That(rig.View.Last.FailureCount, Is.EqualTo(2));
            Assert.That(rig.View.Last.FailureVisible, Is.True);
        }

        [Test]
        public void FailureOrder_FollowsAdr0022()
        {
            GameplayTag stunned = _gas.CreateTag("Stunned");
            AbilitySystemComponent asc = Asc(50f);
            GameplayAbility fireball = _gas.CreateAbilityWithCooldown(2f);
            fireball.CostEffect = Cost(ScalableFloat.Constant(-10f));
            fireball.BlockedTags.Add(stunned);
            fireball.ActivationConditions.Add(new ActivationRule(new HasTargetPointCondition()));
            asc.GiveAbility(fireball);
            Rig rig = CreateSlot(asc, fireball);
            Assert.That(asc.TryActivateAbility(fireball, Vector3.zero), Is.EqualTo(AbilityActivationResult.Success));
            State(asc, _mana).SetBaseValue(5f);
            asc.AddTag(stunned);

            SlotModel tags = Fail(asc, fireball, rig);
            asc.RemoveTag(stunned);
            SlotModel cooldown = Fail(asc, fireball, rig);
            Step(rig, asc, 2f);
            SlotModel cost = Fail(asc, fireball, rig);
            State(asc, _mana).SetBaseValue(50f);
            SlotModel condition = Fail(asc, fireball, rig);

            Assert.That(tags.FailureKey, Is.EqualTo(GasTextKeys.ActivationBlockedByTags), "tags primeiro");
            Assert.That(tags.FailureTag, Is.SameAs(stunned.GetDefinition()));
            Assert.That(tags.State, Is.EqualTo(SlotState.Blocked));
            Assert.That(cooldown.FailureKey, Is.EqualTo(GasTextKeys.ActivationOnCooldown), "recarga antes do custo");
            Assert.That(cooldown.FailureTag, Is.Null);
            Assert.That(cost.FailureKey, Is.EqualTo(GasTextKeys.ActivationCannotAffordCost), "custo antes da condição");
            Assert.That(condition.FailureKey, Is.EqualTo(GasTextKeys.ActivationNoTarget), "a chave da regra");
            Assert.That(condition.State, Is.EqualTo(SlotState.Ready), "a condição aparece só como motivo (Q3)");
            Assert.That(condition.FailureCount, Is.EqualTo(4));
        }

        [Test]
        public void LevelChange_UpdatesCooldownAndCost()
        {
            AbilitySystemComponent asc = Asc(20f);
            GameplayAbility fireball = _gas.CreateAbilityWithCooldown(8f);
            fireball.CooldownEffect.Duration = ScalableFloat.FromArray(8f, 4f);
            fireball.CostEffect = Cost(ScalableFloat.FromArray(-10f, -30f));
            SetMaxLevel(fireball, 2);
            asc.GiveAbility(fireball);
            Rig rig = CreateSlot(asc, fireball);
            SlotState level1 = rig.View.Last.State;

            asc.SetAbilityLevel(fireball, 2);
            SlotState level2 = rig.View.Last.State;
            int ticking = rig.Driver.TickingCount;
            State(asc, _mana).SetBaseValue(100f);
            asc.TryActivateAbility(fireball, Vector3.zero);

            Assert.That(level1, Is.EqualTo(SlotState.Ready));
            Assert.That(level2, Is.EqualTo(SlotState.NoResource), "o custo do nível 2 chega pelo evento do nível");
            Assert.That(ticking, Is.Zero, "sem tick");
            Assert.That(rig.View.Last.CooldownRemaining, Is.EqualTo(4f).Within(1e-4f), "a recarga do nível 2");
        }

        // Tentativa sem alvo; devolve o modelo depois da falha
        private static SlotModel Fail(AbilitySystemComponent asc, GameplayAbility ability, Rig rig)
        {
            Assert.That(asc.TryActivateAbility(ability, AbilityTarget.None), Is.Not.EqualTo(AbilityActivationResult.Success));
            return rig.View.Last;
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

        private GameplayAbility Charged(int maxCharges, float rechargeTime)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Charges.Enabled = true;
            ability.Charges.MaxCharges = new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.Value,
                Value = ScalableFloat.Constant(maxCharges)
            };
            ability.Charges.RechargeTime = ScalableFloat.Constant(rechargeTime);
            return ability;
        }

        private GameplayEffect Cost(ScalableFloat amount)
        {
            GameplayEffect cost = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            cost.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = _mana,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = amount }
            });
            return cost;
        }

        private static void SetMaxLevel(GameplayAbility ability, int maxLevel)
        {
            using var serialized = new SerializedObject(ability);
            serialized.FindProperty("maxLevel").intValue = maxLevel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AttributeState State(AbilitySystemComponent asc, AttributeDefinition definition)
        {
            Assert.That(asc.AttributeSet.TryGetState(definition, out AttributeState state), Is.True);
            return state;
        }

        private static string Text(TextSpan span) => span.IsEmpty ? string.Empty : span.Buffer.ToString(span);

        // Slot sob um driver com o relógio manual, ligado ao ASC pelo dono explícito, com o tema do provedor de teste
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

            var theme = Track(ScriptableObject.CreateInstance<UITheme>());
            using (var serialized = new SerializedObject(theme))
            {
                serialized.FindProperty("text").objectReferenceValue = Track(ScriptableObject.CreateInstance<TestText>());
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var rig = new Rig { Clock = _clock };
            rig.Driver = Track(new GameObject("tela")).AddComponent<UIUpdateDriver>();
            rig.Driver.SetTimeSource(_clock);
            var slot = Track(new GameObject("slot"));
            slot.SetActive(false);
            slot.transform.SetParent(rig.Driver.transform);
            rig.View = slot.AddComponent<Recorder>();
            AbilitySlotBinder binder = slot.AddComponent<AbilitySlotBinder>();
            binder.SetOwner(ActorResolver.ForExplicit(asc));
            binder.SetTheme(theme);
            binder.SetSource(new AbilitySlotSource { Loadout = loadout, Slot = _slotQ });
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

        /// <summary>View de teste: guarda o primeiro e o último modelo.</summary>
        public sealed class Recorder : MonoBehaviour, IView<SlotModel>
        {
            public SlotModel First;
            public SlotModel Last;
            private bool _rendered;

            public void Render(in SlotModel model)
            {
                if (!_rendered) First = model;
                _rendered = true;
                Last = model;
            }
        }

        /// <summary>Provedor de teste: o texto do custo; as outras chaves caem no texto padrão.</summary>
        public sealed class TestText : TextProviderAsset
        {
            public override IFormatProvider Culture => CultureInfo.InvariantCulture;

            public override string GetText(string key, string fallback) =>
                key == GasTextKeys.ActivationCannotAffordCost ? "Recurso insuficiente" : fallback;
        }
    }
}
#endif
