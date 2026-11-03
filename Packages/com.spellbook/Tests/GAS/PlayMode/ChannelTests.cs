using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Habilidade <see cref="AbilityBehavior.Channel"/> e leitura da canalização (EV13: CA26, C18; CA27, parte de Channel).
    /// Os ASCs têm <c>autoTick</c> desligado: só avançam por <c>Tick</c>.
    /// </summary>
    public class ChannelTests
    {
        public enum CancelPath { CancelledByTag, CancelAbility }

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
        public void Channel3s_EndsCleanly_OnEndRuns()
        {
            GameplayAbility ability = Channel(3f, 0f, new WaitThenRecordAction(10f, "wait", _log));
            ability.OnEnd.Add(new RecordAction("on-end", _log));
            ability.OnCancel.Add(new RecordAction("on-cancel", _log));
            AbilitySystemComponent asc = CreateAsc();
            AbilitySpec spec = asc.GiveAbility(ability);
            asc.OnAbilityEnded += (_, cancelled) => _log.Add(cancelled ? "ended(cancelado)" : "ended");

            asc.TryActivateAbility(ability, AbilityTarget.None);
            asc.Tick(1f);
            asc.Tick(1f);
            bool channelingAt2 = spec.IsChanneling;
            asc.Tick(1f);

            Assert.That(channelingAt2, Is.True, "a canalização terminou antes da duração");
            Assert.That(spec.IsActive, Is.False, "a canalização não terminou na duração");
            Assert.That(_log, Is.EqualTo(new[] { "cancel:wait", "on-end", "ended" }));
        }

        [Test]
        public void NoDuration_UntilCancelled()
        {
            GameplayAbility ability = Channel(0f, 0f);
            ability.OnEnd.Add(new RecordAction("on-end", _log));
            ability.OnCancel.Add(new RecordAction("on-cancel", _log));
            AbilitySystemComponent asc = CreateAsc();
            AbilitySpec spec = asc.GiveAbility(ability);
            asc.OnAbilityEnded += (_, cancelled) => _log.Add(cancelled ? "ended(cancelado)" : "ended");

            asc.TryActivateAbility(ability, AbilityTarget.None);
            asc.Tick(100f);
            bool channelingAfter100 = spec.IsChanneling;
            asc.CancelAbility(ability);

            Assert.That(channelingAfter100, Is.True, "sem duração, a canalização terminou sozinha");
            Assert.That(spec.IsChanneling, Is.False);
            Assert.That(_log, Is.EqualTo(new[] { "on-cancel", "ended(cancelado)" }));
        }

        [TestCase(CancelPath.CancelledByTag)]
        [TestCase(CancelPath.CancelAbility)]
        public void CancelByTagAndCode_ReverseOnCancel(CancelPath path)
        {
            GameplayTag stun = _gas.CreateTag("Stun");
            GameplayAbility ability = Channel(10f, 1f,
                new ParentAction("outer", _log, new WaitThenRecordAction(20f, "inner", _log)));
            ability.OnChannelTick.Add(new WaitThenRecordAction(20f, "step", _log));
            ability.OnCancel.Add(new RecordAction("on-cancel", _log));
            ability.OnEnd.Add(new RecordAction("on-end", _log));
            ability.CancelledByTags.Add(stun);
            AbilitySystemComponent asc = CreateAsc();
            asc.GiveAbility(ability);
            asc.OnAbilityEnded += (_, cancelled) => _log.Add(cancelled ? "ended(cancelado)" : "ended");
            asc.TryActivateAbility(ability, AbilityTarget.None);
            asc.Tick(1f); // o primeiro passo começa a esperar

            if (path == CancelPath.CancelledByTag) asc.AddTag(stun);
            else asc.CancelAbility(ability);

            Assert.That(_log, Is.EqualTo(new[] { "cancel:step", "cancel:inner", "cancel:outer", "on-cancel", "ended(cancelado)" }));
        }

        [Test]
        public void ChannelTicks_PerInterval_CountsSameFrameTick()
        {
            GameplayAbility ability = Channel(3f, 1f);
            ability.OnChannelTick.Add(new RecordAction("step", _log));
            ability.OnEnd.Add(new RecordAction("on-end", _log));
            AbilitySystemComponent asc = CreateAsc();
            AbilitySpec spec = asc.GiveAbility(ability);
            asc.OnAbilityEnded += (_, cancelled) => _log.Add(cancelled ? "ended(cancelado)" : "ended");

            asc.TryActivateAbility(ability, AbilityTarget.None);
            asc.Tick(0.5f);
            int stepsAtHalf = _log.Count;
            asc.Tick(0.5f);
            int stepsAt1 = _log.Count;
            asc.Tick(2f); // cobre os passos 2 e 3; o 3 cai no mesmo horário do fim

            Assert.That(stepsAtHalf, Is.Zero, "um passo rodou antes do intervalo");
            Assert.That(stepsAt1, Is.EqualTo(1));
            Assert.That(spec.IsActive, Is.False);
            Assert.That(_log, Is.EqualTo(new[] { "step", "step", "step", "on-end", "ended" }));
        }

        [Test]
        public void Channel_OwnedTags()
        {
            GameplayTag casting = _gas.CreateTag("State.Casting");
            GameplayAbility ability = Channel(2f, 0f);
            ability.ActivationOwnedTags.Add(casting);
            AbilitySystemComponent asc = CreateAsc();
            asc.AddTag(casting);
            asc.GiveAbility(ability);

            asc.TryActivateAbility(ability, AbilityTarget.None);
            asc.Tick(1f);
            int whileChanneling = asc.GetTagCount(casting);
            asc.Tick(1f);

            Assert.That(whileChanneling, Is.EqualTo(2), "canalizando, a habilidade não concedeu a tag");
            Assert.That(asc.GetTagCount(casting), Is.EqualTo(1), "no fim, a contagem não voltou à de antes");
        }

        [Test]
        public void ChannelReadApi_ElapsedRemainingNormalized_IndeterminateZero()
        {
            GameplayAbility timed = Channel(0f, 0f);
            timed.ChannelDuration = ScalableFloat.FromArray(2f, 4f);
            SetMaxLevel(timed, 2);
            GameplayAbility open = Channel(0f, 0f);
            AbilitySystemComponent asc = CreateAsc();
            AbilitySpec timedSpec = asc.GiveAbility(timed, 2);
            AbilitySpec openSpec = asc.GiveAbility(open);
            float durationOnActivated = -1f;
            asc.OnAbilityActivated += spec => { if (spec == timedSpec) durationOnActivated = spec.ChannelDuration; };

            asc.TryActivateAbility(timed, AbilityTarget.None);
            asc.TryActivateAbility(open, AbilityTarget.None);
            asc.Tick(1.5f);

            Assert.That(durationOnActivated, Is.EqualTo(4f), "a duração aplicada não é a do nível 2 no OnAbilityActivated");
            Assert.That(asc.GetChannelElapsed(timedSpec), Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(asc.GetChannelRemaining(timedSpec), Is.EqualTo(2.5f).Within(1e-5f));
            Assert.That(asc.GetChannelNormalized(timedSpec), Is.EqualTo(0.375f).Within(1e-5f));

            Assert.That(openSpec.IsChanneling, Is.True);
            Assert.That(openSpec.ChannelDuration, Is.Zero);
            Assert.That(asc.GetChannelElapsed(openSpec), Is.EqualTo(1.5f).Within(1e-5f), "indeterminada, o decorrido não contou");
            Assert.That(asc.GetChannelRemaining(openSpec), Is.Zero);
            Assert.That(asc.GetChannelNormalized(openSpec), Is.Zero);

            asc.Tick(3f);

            Assert.That(timedSpec.IsChanneling, Is.False);
            Assert.That(timedSpec.ChannelDuration, Is.Zero);
            Assert.That(asc.GetChannelElapsed(timedSpec), Is.Zero);
            Assert.That(asc.GetChannelRemaining(timedSpec), Is.Zero);
            Assert.That(asc.GetChannelNormalized(timedSpec), Is.Zero);
            Assert.That(asc.GetChannelElapsed(null), Is.Zero);
            Assert.That(asc.GetChannelRemaining(null), Is.Zero);
            Assert.That(asc.GetChannelNormalized(null), Is.Zero);
        }

        [Test]
        public void ReactivateInOnEnd_NewChannelKeepsDurationAndEnds()
        {
            GameplayAbility ability = Channel(2f, 0f);
            AbilitySystemComponent asc = CreateAsc();
            AbilitySpec spec = asc.GiveAbility(ability);
            int reactivations = 0;
            ability.OnEnd.Add(new CallbackAction(() =>
            {
                if (reactivations++ == 0) asc.TryActivateAbility(ability, AbilityTarget.None);
            }));
            asc.OnAbilityEnded += (_, cancelled) => _log.Add(cancelled ? "ended(cancelado)" : "ended");

            asc.TryActivateAbility(ability, AbilityTarget.None);
            asc.Tick(2f); // a primeira termina e o OnEnd começa a segunda
            float durationAfterReactivation = spec.ChannelDuration;
            bool channelingAfterReactivation = spec.IsChanneling;
            asc.Tick(1f);
            float normalizedAt1 = asc.GetChannelNormalized(spec);
            asc.Tick(1f);

            Assert.That(channelingAfterReactivation, Is.True, "o OnEnd não começou a segunda canalização");
            Assert.That(durationAfterReactivation, Is.EqualTo(2f), "a duração da nova ativação foi zerada pelo fim da anterior");
            Assert.That(normalizedAt1, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(spec.IsChanneling, Is.False, "a segunda canalização não terminou na duração");
            Assert.That(_log, Is.EqualTo(new[] { "ended", "ended" }));
        }

        [Test]
        public void IntervalSetToZeroWhileChanneling_KeepsIntervalOfActivation()
        {
            GameplayAbility ability = Channel(10f, 1f);
            AbilitySystemComponent asc = CreateAsc();
            AbilitySpec spec = asc.GiveAbility(ability);
            int steps = 0;
            // Freio do teste: sem a correção o laço não acaba; o cancelamento no 5º passo o interrompe pela guarda de ativação
            ability.OnChannelTick.Add(new CallbackAction(() =>
            {
                if (++steps >= 5) asc.CancelAbility(spec);
            }));

            asc.TryActivateAbility(ability, AbilityTarget.None);
            asc.Tick(0.5f);
            ability.ChannelTickInterval = 0f;
            asc.Tick(1f);

            Assert.That(steps, Is.EqualTo(1), "mudar o intervalo para 0 durante a canalização repetiu o passo no mesmo horário");
            Assert.That(spec.IsChanneling, Is.True);
            asc.Tick(1f);
            Assert.That(steps, Is.EqualTo(2), "a canalização em curso não seguiu o intervalo da ativação");
        }

        private GameplayAbility Channel(float duration, float interval, params GameplayAction[] actions)
        {
            GameplayAbility ability = _gas.CreateAbility(actions);
            ability.Behavior = AbilityBehavior.Channel;
            ability.ChannelDuration = ScalableFloat.Constant(duration);
            ability.ChannelTickInterval = interval;
            return ability;
        }

        private static void SetMaxLevel(GameplayAbility ability, int maxLevel)
        {
            var so = new SerializedObject(ability);
            so.FindProperty("maxLevel").intValue = maxLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private AbilitySystemComponent CreateAsc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        [Serializable]
        private sealed class CallbackAction : GameplayAction
        {
            private readonly Action _callback;

            public CallbackAction(Action callback) => _callback = callback;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _callback();
                return ActionStatus.Done;
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

        /// <summary>Roda as filhas e, no cancelamento, escreve <c>cancel:nome</c>.</summary>
        [Serializable]
        private sealed class ParentAction : GameplayAction
        {
            private readonly string _name;
            private readonly List<string> _log;
            private readonly GameplayAction[] _children;

            public ParentAction(string name, List<string> log, params GameplayAction[] children)
            {
                _name = name;
                _log = log;
                _children = children;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                if (frame.Phase == 0)
                {
                    frame.Phase = 1;
                    return ActionStatus.Run(_children);
                }
                return ActionStatus.Done;
            }

            protected override void OnCancel(ActionContext context, ref ActionFrame frame) => _log.Add("cancel:" + _name);
        }
    }
}
