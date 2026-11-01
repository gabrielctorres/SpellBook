using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Diagnostics;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Modelo do GAS Debugger: linhas pela API pública e log de eventos (B-003, EA7; R16 a R18).</summary>
    public class GasDebuggerModelTests
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
            foreach (GameObject go in _objects) Object.DestroyImmediate(go);
            _objects.Clear();
            _gas.Dispose();
        }

        // ASC sem Awake (EditMode): serve para habilidades, tags e recarga, que não usam o AttributeSet.
        private AbilitySystemComponent CreateAscWithoutAwake(string name = "ASC")
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go.AddComponent<AbilitySystemComponent>();
        }

        [Test]
        public void AttributeRows_BaseCurrentMinMax_ModifiersWithSource()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            health.MinValue = 0f;
            health.MaxValue = 200f;
            var state = new AttributeState(health, 100f, null);
            var instance = new ActiveEffectInstance(_gas.CreateDurationEffect(5f), null, null);
            var add = new AttributeModifier(20f, ModifierOperation.Add, instance);
            var multiply = new AttributeModifier(0.5f, ModifierOperation.Multiply, health);
            state.AddModifier(add);
            state.AddModifier(multiply);
            var model = new GasDebuggerModel();
            var rows = new List<AttributeRow>();

            model.BuildAttributeRows(new[] { state }, rows);

            Assert.That(rows.Count, Is.EqualTo(3));
            Assert.That(rows[0].IsModifier, Is.False);
            Assert.That(rows[0].Definition, Is.SameAs(health));
            Assert.That(rows[0].BaseValue, Is.EqualTo(100f));
            Assert.That(rows[0].CurrentValue, Is.EqualTo(180f));
            Assert.That(rows[0].MinValue, Is.EqualTo(0f));
            Assert.That(rows[0].MaxValue, Is.EqualTo(200f));
            Assert.That(rows[1].IsModifier, Is.True);
            Assert.That(rows[1].Definition, Is.SameAs(health));
            Assert.That(rows[1].Operation, Is.EqualTo(ModifierOperation.Add));
            Assert.That(rows[1].ModifierValue, Is.EqualTo(20f));
            Assert.That(rows[1].Source, Is.SameAs(instance));
            Assert.That(rows[2].Operation, Is.EqualTo(ModifierOperation.Multiply));
            Assert.That(rows[2].ModifierValue, Is.EqualTo(0.5f));
            Assert.That(rows[2].Source, Is.SameAs(health));
        }

        [Test]
        public void TagRows_SortedWithCounts()
        {
            GameplayTag stun = _gas.CreateTag("State.Stun");
            GameplayTag burn = _gas.CreateTag("State.Burn");
            GameplayTag armor = _gas.CreateTag("Buff.Armor");
            AbilitySystemComponent asc = CreateAscWithoutAwake();
            asc.AddTag(stun);
            asc.AddTag(burn);
            asc.AddTag(burn);
            asc.AddTag(armor);
            var model = new GasDebuggerModel();
            var rows = new List<TagRow>();

            model.BuildTagRows(asc.TagCounts, rows);

            Assert.That(rows.Count, Is.EqualTo(3));
            Assert.That(rows[0].Tag, Is.EqualTo(armor));
            Assert.That(rows[0].Count, Is.EqualTo(1));
            Assert.That(rows[1].Tag, Is.EqualTo(burn));
            Assert.That(rows[1].Count, Is.EqualTo(2));
            Assert.That(rows[2].Tag, Is.EqualTo(stun));
            Assert.That(rows[2].Count, Is.EqualTo(1));
        }

        [Test]
        public void EffectRows_RemainingAppliedStacksSource()
        {
            AbilitySystemComponent source = CreateAscWithoutAwake("Source");
            AbilitySystemComponent target = CreateAscWithoutAwake("Target");
            GameplayEffect effect = _gas.CreateDurationEffect(10f);
            effect.CanStack = true;
            effect.MaxStacks = 5;
            ActiveEffectInstance instance = target.ApplyEffect(effect, 8f, source);
            target.ApplyEffect(effect, source);
            target.Tick(3f);
            var model = new GasDebuggerModel();
            var rows = new List<EffectRow>();

            model.BuildEffectRows(target.ActiveEffects, rows);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].Instance, Is.SameAs(instance));
            Assert.That(rows[0].Effect, Is.SameAs(effect));
            Assert.That(rows[0].Source, Is.SameAs(source));
            Assert.That(rows[0].TimeRemaining, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(rows[0].AppliedDuration, Is.EqualTo(8f));
            Assert.That(rows[0].Stacks, Is.EqualTo(2));
        }

        [Test]
        public void AbilityRows_LevelActiveCooldown()
        {
            AbilitySystemComponent asc = CreateAscWithoutAwake();
            GameplayAbility withCooldown = _gas.CreateAbilityWithCooldown(4f);
            GameplayAbility ready = _gas.CreateAbility();
            asc.GiveAbility(withCooldown, 3);
            asc.GiveAbility(ready);
            asc.ApplyEffect(withCooldown.CooldownEffect);
            asc.Tick(1f);
            var model = new GasDebuggerModel();
            var rows = new List<AbilityRow>();

            model.BuildAbilityRows(asc, rows);

            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].Spec, Is.SameAs(asc.FindSpec(withCooldown)));
            Assert.That(rows[0].Ability, Is.SameAs(withCooldown));
            Assert.That(rows[0].Level, Is.EqualTo(3));
            Assert.That(rows[0].IsActive, Is.False);
            Assert.That(rows[0].CooldownRemaining, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(rows[0].CooldownNormalized, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(rows[1].Ability, Is.SameAs(ready));
            Assert.That(rows[1].Level, Is.EqualTo(1));
            Assert.That(rows[1].CooldownRemaining, Is.EqualTo(0f));
            Assert.That(rows[1].CooldownNormalized, Is.EqualTo(0f));
        }

        [Test]
        public void EventLog_KeepsLast50()
        {
            AbilitySystemComponent asc = CreateAscWithoutAwake();
            GameplayAbility ability = _gas.CreateAbility();
            var log = new EventLog();
            log.Attach(asc);

            for (int i = 0; i < 30; i++)
            {
                asc.GiveAbility(ability);
                asc.RemoveAbility(ability);
            }

            Assert.That(EventLog.Capacity, Is.EqualTo(50));
            Assert.That(log.Count, Is.EqualTo(50));
            Assert.That(log[0].Kind, Is.EqualTo(GasEventKind.AbilityGranted)); // eventos 11 a 60: o 11º é concessão
            Assert.That(log[49].Kind, Is.EqualTo(GasEventKind.AbilityRemoved));
            Assert.That(log[49].Ability.Ability, Is.SameAs(ability));
        }

        [Test]
        public void EventLog_RecordsEffectEventsAndStopsAfterDetach()
        {
            AbilitySystemComponent asc = CreateAscWithoutAwake();
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            effect.CanStack = true;
            effect.MaxStacks = 3;
            var log = new EventLog();
            log.Attach(asc);

            ActiveEffectInstance instance = asc.ApplyEffect(effect);
            asc.ApplyEffect(effect);
            asc.RemoveEffect(instance);
            log.Detach();
            asc.ApplyEffect(effect);

            Assert.That(log.Count, Is.EqualTo(3));
            Assert.That(log[0].Kind, Is.EqualTo(GasEventKind.EffectApplied));
            Assert.That(log[0].Effect, Is.SameAs(instance));
            Assert.That(log[1].Kind, Is.EqualTo(GasEventKind.EffectStacksChanged));
            Assert.That(log[1].OldStacks, Is.EqualTo(1));
            Assert.That(log[1].NewStacks, Is.EqualTo(2));
            Assert.That(log[2].Kind, Is.EqualTo(GasEventKind.EffectRemoved));
            Assert.That(log[2].RemovalReason, Is.EqualTo(EffectRemovalReason.Removed));
        }

        [Test]
        public void EventLog_RecordsCooldownChanges()
        {
            AbilitySystemComponent asc = CreateAscWithoutAwake();
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(4f);
            asc.GiveAbility(ability);
            asc.TryActivateAbility(ability, Vector3.zero); // a ativação passa a acompanhar a recarga (OnCooldownChanged)
            var log = new EventLog();
            log.Attach(asc);

            asc.ReduceCooldown(ability, 1f);

            Assert.That(log.Count, Is.EqualTo(1));
            Assert.That(log[0].Kind, Is.EqualTo(GasEventKind.CooldownChanged));
            Assert.That(log[0].CooldownChange, Is.EqualTo(CooldownChange.Changed));
            Assert.That(log[0].Ability, Is.SameAs(asc.FindSpec(ability)));
        }

        [Test]
        public void Build_AfterWarmup_DoesNotAllocate()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            var state = new AttributeState(health, 100f, null);
            state.AddModifier(new AttributeModifier(5f, ModifierOperation.Add, health));
            var states = new List<AttributeState> { state };
            AbilitySystemComponent asc = CreateAscWithoutAwake();
            asc.AddTag(_gas.CreateTag("B"));
            asc.AddTag(_gas.CreateTag("A"));
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(4f);
            asc.GiveAbility(ability);
            asc.TryActivateAbility(ability, Vector3.zero); // a ativação passa a acompanhar a recarga (OnCooldownChanged)
            var log = new EventLog();
            log.Attach(asc);
            var model = new GasDebuggerModel();
            var attributeRows = new List<AttributeRow>();
            var tagRows = new List<TagRow>();
            var effectRows = new List<EffectRow>();
            var abilityRows = new List<AbilityRow>();
            model.BuildAttributeRows(states, attributeRows); // aquecimento
            model.BuildTagRows(asc.TagCounts, tagRows);
            model.BuildEffectRows(asc.ActiveEffects, effectRows);
            model.BuildAbilityRows(asc, abilityRows);
            asc.ReduceCooldown(ability, 0.001f);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    model.BuildAttributeRows(states, attributeRows);
                    model.BuildTagRows(asc.TagCounts, tagRows);
                    model.BuildEffectRows(asc.ActiveEffects, effectRows);
                    model.BuildAbilityRows(asc, abilityRows);
                    asc.ReduceCooldown(ability, 0.001f); // grava no log a cada volta
                }
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
