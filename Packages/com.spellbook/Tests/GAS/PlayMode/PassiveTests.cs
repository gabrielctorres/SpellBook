#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Habilidade <see cref="AbilityBehavior.Passive"/> e listas "Ao ser concedida" e "Ao ser retirada" (EV12: CA24, C16).
    /// Os ASCs têm <c>autoTick</c> desligado: só avançam por <c>Tick</c>.
    /// </summary>
    public class PassiveTests
    {
        private GasFactory _gas;
        private TimeFactory _time;
        private List<string> _log;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _log = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects) UnityEngine.Object.Destroy(go);
            _objects.Clear();
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Passive_ActiveOnGrant_EndsOnRemoval_OnEndRuns()
        {
            GameplayAbility ability = Passive(Record("on-activate"));
            ability.OnEnd.Add(Record("on-end"));
            ability.OnCancel.Add(Record("cancel-list"));
            AbilitySystemComponent asc = CreateAsc();
            int failures = 0;
            asc.OnAbilityFailed += (_, _) => failures++;
            asc.OnAbilityGranted += _ => _log.Add("granted-event");
            asc.OnAbilityActivated += _ => _log.Add("activated-event");
            asc.OnAbilityEnded += (_, cancelled) => _log.Add(cancelled ? "ended-event(cancelado)" : "ended-event");
            asc.OnAbilityRemoved += _ => _log.Add("removed-event");

            AbilitySpec spec = asc.GiveAbility(ability);
            Assert.That(spec.IsActive, Is.True, "a passiva não ativou na concessão");
            for (int i = 0; i < 10; i++) asc.Tick(1f);
            Assert.That(spec.IsActive, Is.True, "a passiva terminou ao fim de 'Ao ativar'");

            asc.RemoveAbility(ability);

            Assert.That(spec.IsActive, Is.False);
            Assert.That(failures, Is.Zero);
            Assert.That(_log, Is.EqualTo(new[]
            {
                "granted-event", "activated-event", "on-activate", "on-end", "ended-event", "removed-event"
            }));
        }

        [Test]
        public void Passive_WaitInOnActivate_CancelledOnRemoval_ReverseOnCancel()
        {
            GameplayAbility ability = Passive(new WaitThenRecordAction(10f, "outer", _log), new WaitThenRecordAction(10f, "never", _log));
            ability.OnEnd.Add(Record("on-end"));
            AbilitySystemComponent asc = CreateAsc();
            AbilitySpec spec = asc.GiveAbility(ability);
            asc.Tick(1f);

            asc.RemoveAbility(ability);

            Assert.That(spec.IsActive, Is.False);
            Assert.That(_log, Is.EqualTo(new[] { "cancel:outer", "on-end" }));
            asc.Tick(20f);
            Assert.That(_log, Is.EqualTo(new[] { "cancel:outer", "on-end" }), "a espera continuou depois da retirada");
        }

        [Test]
        public void Passive_FailedChecks_Silent_CancelByTagEnds()
        {
            GameplayTag required = _gas.CreateTag("Required");
            GameplayTag stun = _gas.CreateTag("Stun");
            GameplayAbility blocked = Passive();
            blocked.RequiredTags.Add(required);
            GameplayAbility cancellable = Passive(Record("on-activate"));
            cancellable.CancelledByTags.Add(stun);
            cancellable.OnCancel.Add(Record("cancel-list"));
            cancellable.OnEnd.Add(Record("on-end"));
            AbilitySystemComponent asc = CreateAsc();
            int failures = 0;
            asc.OnAbilityFailed += (_, _) => failures++;
            bool? endedCancelled = null;
            asc.OnAbilityEnded += (_, cancelled) => endedCancelled = cancelled;

            AbilitySpec blockedSpec = asc.GiveAbility(blocked);
            AbilitySpec spec = asc.GiveAbility(cancellable);
            Assert.That(blockedSpec.IsActive, Is.False);
            Assert.That(failures, Is.Zero, "a tentativa da passiva não é silenciosa");
            Assert.That(spec.IsActive, Is.True);

            asc.AddTag(stun);
            asc.Tick(1f);
            asc.Tick(1f);

            Assert.That(spec.IsActive, Is.False);
            Assert.That(endedCancelled, Is.True);
            Assert.That(_log, Is.EqualTo(new[] { "on-activate", "cancel-list" }));
        }

        [Test]
        public void Passive_InStartingAbilities_ActiveAfterAwake()
        {
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            GameplayAbility ability = Passive();
            ability.CostEffect = Cost(mana, 10f);

            AbilitySystemComponent asc = CreateAscWithStartingAbility(mana, 50f, ability);

            AbilitySpec spec = asc.FindSpec(ability);
            Assert.That(spec, Is.Not.Null);
            Assert.That(spec.IsActive, Is.True, "a passiva de partida não ativou no Awake");
            Assert.That(asc.GetAttributeValue(mana), Is.EqualTo(40f));
        }

        [Test]
        public void GrantedRemovedLists_Run()
        {
            var granted = new CaptureAction();
            var removed = new CaptureAction();
            GameplayAbility ability = _gas.CreateAbility(Record("on-activate"));
            ability.OnGranted.Add(granted);
            ability.OnRemoved.Add(removed);
            SetMaxLevel(ability, 3);
            AbilitySystemComponent asc = CreateAsc();

            AbilitySpec spec = asc.GiveAbility(ability, 2);
            asc.GiveAbility(ability, 2);

            Assert.That(granted.Runs, Is.EqualTo(1), "conceder de novo rodou 'Ao ser concedida'");
            Assert.That(granted.Spec, Is.SameAs(spec));
            Assert.That(granted.Ability, Is.SameAs(ability));
            Assert.That(granted.Level, Is.EqualTo(2));
            Assert.That(granted.Targets, Is.EqualTo(new[] { asc }));
            Assert.That(removed.Runs, Is.Zero);
            Assert.That(spec.IsActive, Is.False, "uma Instant ativou na concessão");

            asc.TryActivateAbility(ability, AbilityTarget.None);
            Assert.That(spec.IsActive, Is.False, "a Instant não terminou ao fim de 'Ao ativar'");

            asc.RemoveAbility(ability);

            Assert.That(removed.Runs, Is.EqualTo(1));
            Assert.That(removed.Spec, Is.SameAs(spec));
            Assert.That(removed.Level, Is.EqualTo(2));
            Assert.That(removed.Targets, Is.EqualTo(new[] { asc }));
            Assert.That(granted.Runs, Is.EqualTo(1));
        }

        private GameplayAbility Passive(params GameplayAction[] actions)
        {
            GameplayAbility ability = _gas.CreateAbility(actions);
            ability.Behavior = AbilityBehavior.Passive;
            return ability;
        }

        private AbilitySystemComponent CreateAsc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        /// <summary>ASC inativo configurado pelo <c>SerializedObject</c> e ativado no fim, para o <c>Awake</c> ver a lista.</summary>
        private AbilitySystemComponent CreateAscWithStartingAbility(AttributeDefinition attribute, float initialValue, GameplayAbility ability)
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
            SerializedProperty list = ascObject.FindProperty("startingAbilities");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = ability;
            ascObject.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(true);
            return asc;
        }

        private GameplayEffect Cost(AttributeDefinition attribute, float amount)
        {
            GameplayEffect cost = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            cost.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(-amount) }
            });
            return cost;
        }

        private static void SetMaxLevel(GameplayAbility ability, int maxLevel)
        {
            var so = new SerializedObject(ability);
            so.FindProperty("maxLevel").intValue = maxLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private RecordAction Record(string name) => new RecordAction(name, _log);

        [Serializable]
        private sealed class RecordAction : GameplayAction
        {
            private readonly string _name;
            private readonly List<string> _log;

            public RecordAction(string name, List<string> log)
            {
                _name = name;
                _log = log;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _log.Add(_name);
                return ActionStatus.Done;
            }
        }

        /// <summary>Espera e escreve no registro; no cancelamento, escreve <c>cancel:nome</c>.</summary>
        [Serializable]
        private sealed class WaitThenRecordAction : GameplayAction
        {
            private readonly float _seconds;
            private readonly string _name;
            private readonly List<string> _log;

            public WaitThenRecordAction(float seconds, string name, List<string> log)
            {
                _seconds = seconds;
                _name = name;
                _log = log;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                if (frame.Phase == 0)
                {
                    frame.Phase = 1;
                    return ActionStatus.Wait(_seconds);
                }
                _log.Add(_name);
                return ActionStatus.Done;
            }

            protected override void OnCancel(ActionContext context, ref ActionFrame frame) => _log.Add("cancel:" + _name);
        }

        /// <summary>Copia o contexto que recebe, porque os contextos voltam ao pool.</summary>
        [Serializable]
        private sealed class CaptureAction : GameplayAction
        {
            public int Runs;
            public GameplayAbility Ability;
            public AbilitySpec Spec;
            public int Level;
            public readonly List<AbilitySystemComponent> Targets = new List<AbilitySystemComponent>();

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Runs++;
                Ability = context.Ability;
                Spec = context.Spec;
                Level = context.Level;
                Targets.Clear();
                Targets.AddRange(context.Targets);
                return ActionStatus.Done;
            }
        }
    }
}
#endif
