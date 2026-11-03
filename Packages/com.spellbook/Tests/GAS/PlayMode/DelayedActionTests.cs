using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// <c>DelayedAction</c> agenda as filhas sem bloquear a lista, no tempo do ASC, e a execução agendada pertence ao dono
    /// da lista (B-012, EV6; CA16, CA17). Os ASCs têm <c>autoTick</c> desligado: só avançam por <c>Tick</c>.
    /// </summary>
    public class DelayedActionTests
    {
        public enum OwnerKind { Ability, Effect }

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
        public void Delayed_DoesNotBlock_NextSameCall_DelayedAt2s()
        {
            AbilitySystemComponent caster = CreateManualAsc();

            caster.RunActions(new GameplayAction[] { Delayed(2f, false, Record("atrasada")), Record("seguinte") },
                GasFactory.CreateActionContext(caster, Vector3.zero), Owner);
            string[] afterRun = _log.ToArray();
            caster.Tick(1.5f);
            string[] before2s = _log.ToArray();
            caster.Tick(0.5f);

            Assert.That(afterRun, Is.EqualTo(new[] { "seguinte" }), "a ação seguinte não rodou na mesma chamada");
            Assert.That(before2s, Is.EqualTo(new[] { "seguinte" }), "a atrasada rodou antes de 2 s");
            Assert.That(_log, Is.EqualTo(new[] { "seguinte", "atrasada" }), "a atrasada não rodou aos 2 s");
        }

        [TestCase(OwnerKind.Ability, false, 0)]
        [TestCase(OwnerKind.Ability, true, 1)]
        [TestCase(OwnerKind.Effect, false, 0)]
        [TestCase(OwnerKind.Effect, true, 1)]
        public void Delayed_CancelledWithOwner_ContinueRuns(OwnerKind ownerKind, bool continueIfOwnerEnds, int expectedRuns)
        {
            AbilitySystemComponent asc = CreateManualAsc();
            DelayedAction delayed = Delayed(2f, continueIfOwnerEnds, Record("atrasada"));

            if (ownerKind == OwnerKind.Ability)
            {
                GameplayAbility ability = _gas.CreateAbility(delayed);
                asc.GiveAbility(ability);
                asc.TryActivateAbility(ability, Vector3.zero);
                asc.Tick(1f);
                asc.CancelAbility(ability);
            }
            else
            {
                GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
                effect.OnApplied.Add(delayed);
                ActiveEffectInstance instance = asc.ApplyEffect(effect);
                asc.Tick(1f);
                asc.RemoveEffect(instance);
            }
            asc.Tick(1f);

            Assert.That(_log.Count, Is.EqualTo(expectedRuns),
                $"dono {ownerKind}, continuar {continueIfOwnerEnds}: a atrasada rodou {_log.Count} vezes");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AbilityStaysActiveUntilDelayedRuns(bool continueIfOwnerEnds)
        {
            GameplayAbility ability = _gas.CreateAbility(Delayed(2f, continueIfOwnerEnds, Record("atrasada")), Record("seguinte"));
            AbilitySystemComponent asc = CreateManualAsc();
            AbilitySpec spec = asc.GiveAbility(ability);
            asc.OnAbilityEnded += (_, cancelled) => _log.Add(cancelled ? "fim(cancelada)" : "fim");

            asc.TryActivateAbility(ability, Vector3.zero);
            bool activeAfterRun = spec.IsActive;
            asc.Tick(1.5f);
            bool activeBefore2s = spec.IsActive;
            asc.Tick(0.5f);

            if (continueIfOwnerEnds)
            {
                Assert.That(activeAfterRun, Is.False, "com continuar, a atrasada não tem dono e não segura a habilidade");
                Assert.That(_log, Is.EqualTo(new[] { "seguinte", "fim", "atrasada" }));
                return;
            }
            Assert.That(activeAfterRun, Is.True, "a habilidade terminou com a atrasada pendente");
            Assert.That(activeBefore2s, Is.True, "a habilidade terminou antes de a atrasada rodar");
            Assert.That(spec.IsActive, Is.False, "a habilidade não terminou depois de a atrasada rodar");
            Assert.That(_log, Is.EqualTo(new[] { "seguinte", "atrasada", "fim" }));
        }

        private AbilitySystemComponent CreateManualAsc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        private static DelayedAction Delayed(float seconds, bool continueIfOwnerEnds, params GameplayAction[] actions)
        {
            return new DelayedAction
            {
                Delay = ScalableFloat.Constant(seconds),
                ContinueIfOwnerEnds = continueIfOwnerEnds,
                Actions = new List<GameplayAction>(actions),
            };
        }

        private RecordAction Record(string entry) => new RecordAction(entry, _log);

        [Serializable]
        private sealed class RecordAction : GameplayAction
        {
            private readonly string _entry;
            private readonly List<string> _log;

            public RecordAction(string entry, List<string> log)
            {
                _entry = entry;
                _log = log;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _log.Add(_entry);
                return ActionStatus.Done;
            }
        }
    }
}
