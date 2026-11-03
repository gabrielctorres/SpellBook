#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Listas "Ao terminar" e "Ao ser cancelada" da habilidade (EV3c). Os ASCs têm <c>autoTick</c> desligado: só avançam
    /// por <c>Tick</c>.
    /// </summary>
    public class AbilityEndCancelListsTests
    {
        public enum EndPath { SameCall, Tick, Cancel }

        private GasFactory _gas;
        private TimeFactory _time;
        private List<string> _log;

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
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void OnEnd_RunsOnceAfterOnActivate_BeforeAbilityEnded()
        {
            GameplayAbility ability = _gas.CreateAbility(Record("activate"));
            ability.OnEnd.Add(Record("end"));
            ability.OnCancel.Add(Record("cancel-list"));
            AbilitySystemComponent asc = CreateAsc(ability);
            asc.OnAbilityEnded += (_, cancelled) => _log.Add(cancelled ? "ended-event(cancelado)" : "ended-event");

            asc.TryActivateAbility(ability, Vector3.zero);

            Assert.That(_log, Is.EqualTo(new[] { "activate", "end", "ended-event" }));
        }

        [Test]
        public void Cancel_RunsOnCancelListAfterRunningActionsOnCancel_NotOnEnd()
        {
            GameplayAbility ability = _gas.CreateAbility(new WaitThenRecordAction(10f, "acao", _log));
            ability.OnEnd.Add(Record("end"));
            ability.OnCancel.Add(Record("cancel-list"));
            AbilitySystemComponent asc = CreateAsc(ability);
            asc.OnAbilityEnded += (_, cancelled) => _log.Add(cancelled ? "ended-event(cancelado)" : "ended-event");
            asc.TryActivateAbility(ability, Vector3.zero);

            asc.CancelAbility(ability);

            Assert.That(_log, Is.EqualTo(new[] { "cancel:acao", "cancel-list", "ended-event(cancelado)" }));
        }

        [TestCase(EndPath.SameCall)]
        [TestCase(EndPath.Tick)]
        [TestCase(EndPath.Cancel)]
        public void EndAndCancelLists_ContextCopiesActivation(EndPath path)
        {
            AbilitySystemComponent target = _gas.CreateAsc();
            var capture = new CaptureAction();
            GameplayAbility ability = _gas.CreateAbility(new AddEventTargetAction(target));
            if (path != EndPath.SameCall) ability.OnActivate.Add(new WaitThenRecordAction(1f, "wait", _log));
            if (path == EndPath.Cancel) ability.OnCancel.Add(capture);
            else ability.OnEnd.Add(capture);
            SetMaxLevel(ability, 3);
            AbilitySystemComponent asc = CreateAsc();
            AbilitySpec spec = asc.GiveAbility(ability, 3);
            var point = new Vector3(1f, 2f, 3f);

            asc.TryActivateAbility(ability, point);
            if (path == EndPath.Tick) asc.Tick(1f);
            if (path == EndPath.Cancel) asc.CancelAbility(ability);

            Assert.That(capture.Runs, Is.EqualTo(1));
            Assert.That(capture.Ability, Is.SameAs(ability));
            Assert.That(capture.Spec, Is.SameAs(spec));
            Assert.That(capture.Level, Is.EqualTo(3));
            Assert.That(capture.Instigator, Is.SameAs(asc));
            Assert.That(capture.HasPoint, Is.True);
            Assert.That(capture.Point, Is.EqualTo(point));
            Assert.That(capture.EventTargets, Is.EqualTo(new[] { target }));
        }

        [Test]
        public void ActiveContext_FilledUntilAbilityEnded()
        {
            AbilitySystemComponent target = _gas.CreateAsc();
            var capture = new CaptureAction();
            GameplayAbility ability = _gas.CreateAbility(new AddEventTargetAction(target), new WaitThenRecordAction(1f, "wait", _log));
            ability.OnEnd.Add(capture);
            AbilitySystemComponent asc = CreateAsc(ability);
            bool endedWithContext = true;
            asc.OnAbilityEnded += (s, _) => endedWithContext = s.ActiveContext != null;
            asc.TryActivateAbility(ability, Vector3.zero);

            asc.Tick(1f);

            Assert.That(capture.ActiveAbility, Is.SameAs(ability), "ActiveContext não tinha a habilidade da ativação");
            Assert.That(capture.ActiveEventTargets, Is.EqualTo(new[] { target }));
            Assert.That(endedWithContext, Is.False, "ActiveContext ainda preenchido no OnAbilityEnded");
        }

        [Test]
        public void OnEndWithWait_AbilityEndsNow_ListFinishesByTick()
        {
            GameplayAbility ability = _gas.CreateAbility(Record("activate"));
            ability.OnEnd.Add(new WaitThenRecordAction(1f, "after", _log));
            AbilitySystemComponent asc = CreateAsc(ability);
            AbilitySpec spec = asc.FindSpec(ability);

            asc.TryActivateAbility(ability, Vector3.zero);
            Assert.That(spec.IsActive, Is.False, "a espera de 'Ao terminar' segurou a habilidade ativa");
            asc.Tick(0.5f);
            asc.TryActivateAbility(ability, Vector3.zero);
            asc.Tick(0.5f);

            Assert.That(_log, Is.EqualTo(new[] { "activate", "activate", "after" }), "a nova ativação cancelou a lista");
            Assert.That(_log.Contains("cancel:after"), Is.False);
            asc.Tick(0.5f);
            Assert.That(_log, Is.EqualTo(new[] { "activate", "activate", "after", "after" }));
        }

        [Test]
        public void EmptyEndCancelLists_DoNotAllocate()
        {
            var counter = new Counter();
            GameplayAbility ability = _gas.CreateAbility(new CountAction(counter));
            AbilitySystemComponent asc = CreateAsc(ability);
            AbilitySpec spec = asc.FindSpec(ability);
            asc.TryActivateAbility(spec, Vector3.zero);
            counter.Value = 0;

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    asc.TryActivateAbility(spec, Vector3.zero);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(counter.Value, Is.EqualTo(1000));
            Assert.That(spec.IsActive, Is.False);
        }

        private AbilitySystemComponent CreateAsc(params GameplayAbility[] abilities)
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            foreach (GameplayAbility ability in abilities) asc.GiveAbility(ability);
            return asc;
        }

        private static void SetMaxLevel(GameplayAbility ability, int maxLevel)
        {
            var so = new SerializedObject(ability);
            so.FindProperty("maxLevel").intValue = maxLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private RecordAction Record(string name) => new RecordAction(name, _log);

        private sealed class Counter
        {
            public int Value;
        }

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

        [Serializable]
        private sealed class CountAction : GameplayAction
        {
            private readonly Counter _counter;

            public CountAction(Counter counter) => _counter = counter;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _counter.Value++;
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

        /// <summary>Põe um alvo nos alvos do evento, como faria uma ação de mira durante a ativação.</summary>
        [Serializable]
        private sealed class AddEventTargetAction : GameplayAction
        {
            private readonly AbilitySystemComponent _target;

            public AddEventTargetAction(AbilitySystemComponent target) => _target = target;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                context.EventTargets.Add(_target);
                return ActionStatus.Done;
            }
        }

        /// <summary>Copia o contexto que recebe e o <c>ActiveContext</c> do spec, porque os contextos voltam ao pool.</summary>
        [Serializable]
        private sealed class CaptureAction : GameplayAction
        {
            public int Runs;
            public GameplayAbility Ability;
            public AbilitySpec Spec;
            public int Level;
            public AbilitySystemComponent Instigator;
            public bool HasPoint;
            public Vector3 Point;
            public readonly List<AbilitySystemComponent> EventTargets = new List<AbilitySystemComponent>();
            public GameplayAbility ActiveAbility;
            public readonly List<AbilitySystemComponent> ActiveEventTargets = new List<AbilitySystemComponent>();

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Runs++;
                Ability = context.Ability;
                Spec = context.Spec;
                Level = context.Level;
                Instigator = context.Instigator;
                HasPoint = context.HasPoint;
                Point = context.Point;
                EventTargets.AddRange(context.EventTargets);
                ActionContext active = context.Spec?.ActiveContext;
                if (active != null)
                {
                    ActiveAbility = active.Ability;
                    ActiveEventTargets.AddRange(active.EventTargets);
                }
                return ActionStatus.Done;
            }
        }
    }
}
#endif
