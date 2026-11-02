using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary>Executor de ações do ASC (B-012, EV1a; ADR 0034).</summary>
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

        private AbilitySystemComponent CreateAsc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        private LogAction Log(string name) => new LogAction(name, _log);

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
    }
}
