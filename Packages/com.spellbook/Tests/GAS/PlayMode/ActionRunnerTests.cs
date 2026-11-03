using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Executor de ações do ASC (B-012, EV1a e EV1b; ADR 0034).</summary>
    public class ActionRunnerTests
    {
        private static readonly object Owner = new object();

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
        public void InstantSequence_RunsInSameCall()
        {
            AbilitySystemComponent asc = CreateAsc();

            asc.RunActions(new GameplayAction[] { Log("a"), Log("b"), Log("c") }, null, Owner);

            Assert.That(_log, Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(asc.HasRunningActions(Owner), Is.False);
        }

        [Test]
        public void Wait_CountsFromEndOfPreviousStep_TickInSameFrameCounts()
        {
            AbilitySystemComponent asc = CreateAsc();
            asc.Tick(0.5f);
            asc.RunActions(new GameplayAction[] { new WaitTestAction(1f), Log("after") }, null, Owner);

            asc.Tick(0.75f);
            Assert.That(_log, Is.Empty, "a espera de 1 s acabou com 0,75 s");
            asc.Tick(0.25f);

            Assert.That(_log, Is.EqualTo(new[] { "after" }));
        }

        [Test]
        public void Wait_BigTick_CarriesExcess()
        {
            AbilitySystemComponent asc = CreateAsc();
            asc.RunActions(new GameplayAction[] { new WaitTestAction(1f), Log("first"), new WaitTestAction(1f), Log("second") }, null, Owner);

            asc.Tick(1.5f);
            Assert.That(_log, Is.EqualTo(new[] { "first" }));
            asc.Tick(0.5f);

            Assert.That(_log, Is.EqualTo(new[] { "first", "second" }), "o excesso de 0,5 s do primeiro Tick não passou adiante");
        }

        [Test]
        public void RunChildren_ResumesParent()
        {
            AbilitySystemComponent asc = CreateAsc();
            var parent = new ParentAction("parent", _log, false, Log("a"), new WaitTestAction(1f), Log("b"));
            asc.RunActions(new GameplayAction[] { parent, Log("c") }, null, Owner);
            Assert.That(_log, Is.EqualTo(new[] { "a" }));

            asc.Tick(1f);

            Assert.That(_log, Is.EqualTo(new[] { "a", "b", "parent:resumed", "c" }));
        }

        [Test]
        public void RunParallel_ResumesWhenAllDone()
        {
            AbilitySystemComponent asc = CreateAsc();
            var parallel = new ParentAction("parallel", _log, true, new WaitThenLogAction(1f, "x", _log), new WaitThenLogAction(2f, "y", _log));
            asc.RunActions(new GameplayAction[] { parallel, Log("after") }, null, Owner);

            asc.Tick(1f);
            Assert.That(_log, Is.EqualTo(new[] { "x" }));
            asc.Tick(1f);

            Assert.That(_log, Is.EqualTo(new[] { "x", "y", "parallel:resumed", "after" }));
            Assert.That(asc.HasRunningActions(Owner), Is.False);
        }

        [Test]
        public void CancelOwner_OnCancelReverseOrder_NoException()
        {
            AbilitySystemComponent asc = CreateAsc();
            var parallel = new ParentAction("parallel", _log, true, new WaitThenLogAction(5f, "A", _log), new WaitThenLogAction(5f, "B", _log));
            var outer = new ReentrantCancelAction(_log, parallel);
            asc.RunActions(new GameplayAction[] { outer }, null, Owner);
            Assert.That(asc.HasRunningActions(Owner), Is.True);

            Assert.DoesNotThrow(() => asc.CancelActions(Owner));

            Assert.That(_log, Is.EqualTo(new[] { "cancel:B", "cancel:A", "cancel:parallel", "cancel:outer", "spawned" }));
            Assert.That(asc.HasRunningActions(Owner), Is.False);
            asc.Tick(10f);
            Assert.That(_log, Has.Count.EqualTo(5), "uma ação cancelada voltou a rodar");
        }

        [Test]
        public void TwoExecutions_SameAction_NoSharedState()
        {
            AbilitySystemComponent asc = CreateAsc();
            var shared = new GameplayAction[] { new WaitTestAction(1f), Log("done") };
            asc.RunActions(shared, null, Owner);
            asc.Tick(0.5f);
            asc.RunActions(shared, null, Owner);

            asc.Tick(0.5f);
            Assert.That(_log, Has.Count.EqualTo(1), "a segunda execução herdou o estado da primeira");
            asc.Tick(0.5f);

            Assert.That(_log, Has.Count.EqualTo(2));
        }

        [Test]
        public void Executions_10000_DoNotAllocate_AfterWarmup()
        {
            AbilitySystemComponent asc = CreateAsc();
            var counter = new Counter();
            var actions = new GameplayAction[] { new WaitTestAction(0.1f), new CountAction(counter) };
            asc.RunActions(actions, null, Owner);
            asc.Tick(0.1f);
            counter.Value = 0;

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    asc.RunActions(actions, null, Owner);
                    asc.Tick(0.1f);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(counter.Value, Is.EqualTo(10000));
        }

        [Test]
        public void CreateDerived_KeepsInstigatorAbilityLevel_SetsPoint_CopiesCustomData()
        {
            AbilitySystemComponent asc = CreateAsc();
            AbilitySystemComponent instigator = _gas.CreateAsc();
            GameplayAbility ability = _gas.CreateAbility();
            var payload = new object();
            var context = new ActionContext { Instigator = instigator };
            SetInternal(context, nameof(ActionContext.Ability), ability);
            SetInternal(context, nameof(ActionContext.Level), 3);
            context.CustomData["payload"] = payload;
            var derive = new DeriveAction(new Vector3(1f, 2f, 3f));

            asc.RunActions(new GameplayAction[] { derive }, context, Owner);

            ActionContext derived = derive.Derived;
            Assert.That(derived, Is.Not.Null.And.Not.SameAs(context));
            Assert.That(derived.HasPoint, Is.True);
            Assert.That(derived.Point, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(derived.Owner, Is.SameAs(asc));
            Assert.That(derived.Instigator, Is.SameAs(instigator));
            Assert.That(derived.Ability, Is.SameAs(ability));
            Assert.That(derived.Level, Is.EqualTo(3));
            Assert.That(derived.CustomData["payload"], Is.SameAs(payload));
            Assert.That(derive.OriginalHadAddedKey, Is.False, "a chave acrescentada no derivado apareceu no original");
        }

        private static void SetInternal(ActionContext context, string property, object value)
        {
            // Ability e Level têm set interno; o EV2 e o EV3 os preenchem pela ativação
            typeof(ActionContext).GetProperty(property).SetValue(context, value);
        }

        private AbilitySystemComponent CreateAsc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        private LogAction Log(string name) => new LogAction(name, _log);

        private sealed class Counter
        {
            public int Value;
        }

        [Serializable]
        private sealed class LogAction : GameplayAction
        {
            private readonly string _name;
            private readonly List<string> _log;

            public LogAction(string name, List<string> log)
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

        [Serializable]
        private sealed class WaitTestAction : GameplayAction
        {
            private readonly float _seconds;

            public WaitTestAction(float seconds) => _seconds = seconds;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                if (frame.Phase > 0) return ActionStatus.Done;
                frame.Phase = 1;
                return ActionStatus.Wait(_seconds);
            }
        }

        /// <summary>Cria um contexto derivado, acrescenta uma chave nele e anota se ela apareceu no original.</summary>
        [Serializable]
        private sealed class DeriveAction : GameplayAction
        {
            private readonly Vector3 _point;

            public DeriveAction(Vector3 point) => _point = point;

            public ActionContext Derived { get; private set; }
            public bool OriginalHadAddedKey { get; private set; }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Derived = context.CreateDerived(_point);
                Derived.CustomData["added"] = 1;
                OriginalHadAddedKey = context.CustomData.ContainsKey("added");
                return ActionStatus.Done;
            }
        }

        /// <summary>Espera e escreve no registro; no cancelamento, escreve <c>cancel:nome</c>.</summary>
        [Serializable]
        private sealed class WaitThenLogAction : GameplayAction
        {
            private readonly float _seconds;
            private readonly string _name;
            private readonly List<string> _log;

            public WaitThenLogAction(float seconds, string name, List<string> log)
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

        /// <summary>Roda as filhas (em sequência ou em paralelo) e escreve <c>nome:resumed</c> quando volta.</summary>
        [Serializable]
        private sealed class ParentAction : GameplayAction
        {
            private readonly string _name;
            private readonly List<string> _log;
            private readonly bool _parallel;
            private readonly GameplayAction[] _children;

            public ParentAction(string name, List<string> log, bool parallel, params GameplayAction[] children)
            {
                _name = name;
                _log = log;
                _parallel = parallel;
                _children = children;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                if (frame.Phase == 0)
                {
                    frame.Phase = 1;
                    return _parallel ? ActionStatus.RunParallel(_children) : ActionStatus.Run(_children);
                }
                _log.Add(_name + ":resumed");
                return ActionStatus.Done;
            }

            protected override void OnCancel(ActionContext context, ref ActionFrame frame) => _log.Add("cancel:" + _name);
        }

        /// <summary>No cancelamento, cancela o próprio dono de novo e inicia outra execução do mesmo dono.</summary>
        [Serializable]
        private sealed class ReentrantCancelAction : GameplayAction
        {
            private readonly List<string> _log;
            private readonly GameplayAction[] _children;

            public ReentrantCancelAction(List<string> log, GameplayAction child)
            {
                _log = log;
                _children = new[] { child };
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                if (frame.Phase > 0) return ActionStatus.Done;
                frame.Phase = 1;
                return ActionStatus.Run(_children);
            }

            protected override void OnCancel(ActionContext context, ref ActionFrame frame)
            {
                _log.Add("cancel:outer");
                AbilitySystemComponent owner = context.Owner;
                owner.CancelActions(Owner);
                owner.RunActions(new GameplayAction[] { new LogAction("spawned", _log) }, null, Owner);
            }
        }
    }
}
