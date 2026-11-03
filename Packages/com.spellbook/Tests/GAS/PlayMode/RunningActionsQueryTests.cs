using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Leitura das ações em execução de um ASC, para o indicador de "rodando" do Inspector (EV22; Spec, Design 10a).</summary>
    public class RunningActionsQueryTests
    {
        private static readonly object Owner = new object();

        private GasFactory _gas;
        private TimeFactory _time;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void CollectRunningActions_WaitInsideRepeat_ListsLeafAndParent()
        {
            AbilitySystemComponent asc = CreateAsc();
            WaitAction wait = Wait(1f);
            RepeatAction repeat = Repeat(wait);
            var into = new List<GameplayAction> { new WaitAction() };

            asc.RunActions(new GameplayAction[] { repeat }, null, Owner);
            asc.CollectRunningActions(into);

            Assert.That(into, Is.EquivalentTo(new GameplayAction[] { repeat, wait }), "a lista não foi limpa ou faltou um quadro");
        }

        [Test]
        public void CollectRunningActions_AfterExecutionEnds_Empty()
        {
            AbilitySystemComponent asc = CreateAsc();
            asc.RunActions(new GameplayAction[] { Repeat(Wait(1f)) }, null, Owner);
            var into = new List<GameplayAction>();

            asc.Tick(1f);
            asc.CollectRunningActions(into);

            Assert.That(into, Is.Empty);
        }

        [Test]
        public void CollectRunningActions_CancelledOwner_Empty()
        {
            AbilitySystemComponent asc = CreateAsc();
            asc.RunActions(new GameplayAction[] { Repeat(Wait(1f)) }, null, Owner);
            var into = new List<GameplayAction>();

            asc.CancelActions(Owner);
            asc.CollectRunningActions(into);

            Assert.That(into, Is.Empty);
        }

        [Test]
        public void CollectRunningActions_AfterWarmup_DoesNotAllocate()
        {
            AbilitySystemComponent asc = CreateAsc();
            asc.RunActions(new GameplayAction[] { Repeat(Wait(1f)) }, null, Owner);
            asc.RunActions(new GameplayAction[] { Wait(1f) }, null, null);
            var into = new List<GameplayAction>();
            asc.CollectRunningActions(into);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++) asc.CollectRunningActions(into);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(into, Has.Count.EqualTo(3));
        }

        private AbilitySystemComponent CreateAsc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        private static WaitAction Wait(float seconds) => new WaitAction { Duration = ScalableFloat.Constant(seconds) };

        private static RepeatAction Repeat(GameplayAction child)
        {
            var repeat = new RepeatAction { Count = 1 };
            repeat.Actions.Add(child);
            return repeat;
        }
    }
}
