using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Vínculo de execução <see cref="ExecutionLink"/> e <see cref="ActionStatus.WaitUntilCancelled"/> (B-014, TK1; ADR 0036,
    /// item 4). Os ASCs têm <c>autoTick</c> desligado: só avançam por <c>Tick</c>.
    /// </summary>
    public class ExecutionLinkTests
    {
        public enum RemovalPath { Removed, RemovedByTag, Expired, StacksDepleted }

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
        public void FollowsOwner_CancelledWhenActivationEnds_DoesNotHoldIt()
        {
            GameplayAbility ability = _gas.CreateAbility(new StartWatchAction(Watch()), new WaitThenRecordAction(1f, "acao", _log));
            ability.OnEnd.Add(new RecordAction("end", _log));
            AbilitySystemComponent asc = CreateAsc(ability);
            AbilitySpec spec = asc.FindSpec(ability);
            asc.OnAbilityEnded += (_, cancelled) => _log.Add(cancelled ? "ended-event(cancelado)" : "ended-event");

            asc.TryActivateAbility(spec, Vector3.zero);
            bool activeWhileWaiting = spec.IsActive;
            asc.Tick(1f);

            Assert.That(activeWhileWaiting, Is.True, "a ativação terminou antes da espera de 1 s");
            Assert.That(spec.IsActive, Is.False, "a vigia FollowsOwner segurou a ativação");
            Assert.That(_log, Is.EqualTo(new[] { "acao", "cancel:watch", "end", "ended-event" }));
            asc.Tick(100f);
            Assert.That(_log, Has.Count.EqualTo(4), "a vigia voltou a rodar depois do fim");
        }

        [TestCase(RemovalPath.Removed)]
        [TestCase(RemovalPath.RemovedByTag)]
        [TestCase(RemovalPath.Expired)]
        [TestCase(RemovalPath.StacksDepleted)]
        public void FollowsOwner_CancelledWithEffectRemoval_AllReasons(RemovalPath path)
        {
            GameplayTag tag = _gas.CreateTag("Test.Watched");
            GameplayEffect effect = _gas.CreateDurationEffect(10f, tag);
            effect.OnApplied.Add(new StartWatchAction(Watch()));
            AbilitySystemComponent target = CreateAsc();

            ActiveEffectInstance instance = target.ApplyEffect(effect);
            target.Tick(1f);
            string[] beforeRemoval = _log.ToArray();
            switch (path)
            {
                case RemovalPath.Removed: target.RemoveEffect(instance); break;
                case RemovalPath.RemovedByTag: target.RemoveEffectsWithTag(tag); break;
                case RemovalPath.Expired: target.Tick(9f); break;
                case RemovalPath.StacksDepleted: target.SetEffectStacks(instance, 0); break;
            }

            Assert.That(beforeRemoval, Is.Empty, "a vigia terminou antes da remoção");
            Assert.That(target.ActiveEffects, Has.No.Member(instance), "a instância não saiu");
            Assert.That(_log, Is.EqualTo(new[] { "cancel:watch" }));
        }

        [Test]
        public void FollowsOwner_CancelledOnAbilityCancel()
        {
            GameplayAbility ability = _gas.CreateAbility(new StartWatchAction(Watch()), new WaitThenRecordAction(10f, "acao", _log));
            ability.OnCancel.Add(new RecordAction("cancel-list", _log));
            AbilitySystemComponent asc = CreateAsc(ability);
            AbilitySpec spec = asc.FindSpec(ability);
            asc.OnAbilityEnded += (_, cancelled) => _log.Add(cancelled ? "ended-event(cancelado)" : "ended-event");
            asc.TryActivateAbility(spec, Vector3.zero);

            asc.CancelAbility(spec);

            Assert.That(spec.IsActive, Is.False);
            Assert.That(_log, Is.EquivalentTo(new[] { "cancel:acao", "cancel:watch", "cancel-list", "ended-event(cancelado)" }));
            Assert.That(_log.GetRange(2, 2), Is.EqualTo(new[] { "cancel-list", "ended-event(cancelado)" }),
                "a vigia foi cancelada depois da lista \"Ao ser cancelada\"");
        }

        [Test]
        public void WaitUntilCancelled_NeverEndsByTime()
        {
            AbilitySystemComponent asc = CreateAsc();
            asc.RunActions(new GameplayAction[] { Watch(), new RecordAction("after", _log) }, null, Owner);

            asc.Tick(1f);
            asc.Tick(1000000f);
            asc.Tick(float.MaxValue);
            bool runningAfterTicks = asc.HasRunningActions(Owner);
            asc.CancelActions(Owner);

            Assert.That(runningAfterTicks, Is.True, "a espera até cancelar terminou pelo tempo");
            Assert.That(_log, Is.EqualTo(new[] { "cancel:watch" }));
            Assert.That(asc.HasRunningActions(Owner), Is.False);
        }

        [Test]
        public void Holds_StillDefault_DelayedRuleUnchanged()
        {
            var delayed = new DelayedAction
            {
                Delay = ScalableFloat.Constant(1f),
                ContinueIfOwnerEnds = false,
                Actions = new List<GameplayAction> { new RecordAction("atrasada", _log) },
            };
            GameplayAbility ability = _gas.CreateAbility(delayed);
            AbilitySystemComponent asc = CreateAsc(ability);
            AbilitySpec spec = asc.FindSpec(ability);

            asc.TryActivateAbility(spec, Vector3.zero);
            bool activeBeforeDelay = spec.IsActive;
            asc.Tick(1f);

            Assert.That(activeBeforeDelay, Is.True, "a execução atrasada (Holds) deixou de segurar a ativação");
            Assert.That(_log, Is.EqualTo(new[] { "atrasada" }));
            Assert.That(spec.IsActive, Is.False);
        }

        [Test]
        public void FollowsOwner_DoesNotAllocate_AfterWarmup()
        {
            var cancelled = new Counter();
            GameplayAbility ability = _gas.CreateAbility(new StartWatchAction(new WatchAction(null, cancelled)));
            AbilitySystemComponent asc = CreateAsc(ability);
            AbilitySpec spec = asc.FindSpec(ability);
            asc.TryActivateAbility(spec, Vector3.zero);
            cancelled.Value = 0;

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++) asc.TryActivateAbility(spec, Vector3.zero);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(cancelled.Value, Is.EqualTo(1000), "nem toda ativação terminou cancelando a vigia");
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

        private WatchAction Watch() => new WatchAction(_log, null);

        private sealed class Counter
        {
            public int Value;
        }

        /// <summary>
        /// Começa no ASC dono uma execução <see cref="ExecutionLink.FollowsOwner"/> das <c>watch</c>, com dono lógico o spec
        /// da ativação ou, fora dela, a instância de efeito.
        /// </summary>
        [Serializable]
        private sealed class StartWatchAction : GameplayAction
        {
            private readonly GameplayAction[] _watch;

            public StartWatchAction(GameplayAction watch) => _watch = new[] { watch };

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                object owner = context.Spec != null ? context.Spec : (object)context.EffectInstance;
                context.Owner.RunActions(_watch, null, owner, ExecutionLink.FollowsOwner);
                return ActionStatus.Done;
            }
        }

        /// <summary>Espera até ser cancelada; no cancelamento, escreve <c>cancel:watch</c> ou soma no contador.</summary>
        [Serializable]
        private sealed class WatchAction : GameplayAction
        {
            private readonly List<string> _log;
            private readonly Counter _cancelled;

            public WatchAction(List<string> log, Counter cancelled)
            {
                _log = log;
                _cancelled = cancelled;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                if (frame.Phase > 0) return ActionStatus.Done;
                frame.Phase = 1;
                return ActionStatus.WaitUntilCancelled;
            }

            protected override void OnCancel(ActionContext context, ref ActionFrame frame)
            {
                _log?.Add("cancel:watch");
                if (_cancelled != null) _cancelled.Value++;
            }
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
    }
}
