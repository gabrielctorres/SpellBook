using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Thinkers;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Vida, pulsos e término do <see cref="GameplayThinker"/> (B-014, TK4; ADR 0036, itens 1 a 3). Os ASCs têm
    /// <c>autoTick</c> desligado: só avançam por <c>Tick</c>. O thinker é ativado direto e volta por um spawner de uma
    /// vaga, que só o desativa.
    /// </summary>
    public class ThinkerTests
    {
        private GasFactory _gas;
        private List<string> _log;
        private OneSlotSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _log = new List<string>();
            _spawner = ScriptableObject.CreateInstance<OneSlotSpawner>();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            Object.Destroy(_spawner);
        }

        [Test]
        public void Created_PulsesAt1To5_EndedOnce_ThenReturnsToPool()
        {
            GameplayThinker thinker = CreateActive(5f, 1f, t =>
            {
                t.Behaviour = new RecordBehaviour(_log);
                t.OnCreated.Add(Log("created"));
                t.OnPulse.Add(new LogAction(_log, c => $"pulse@{c.Owner.TickedTime}"));
                t.OnEnded.Add(LogReason("ended"));
            });

            thinker.Begin(null, null, _spawner);
            for (int i = 0; i < 6; i++) thinker.AbilitySystem.Tick(1f);

            Assert.That(_log, Is.EqualTo(new[]
            {
                "b:begin", "created",
                "b:pulse", "pulse@1", "b:pulse", "pulse@2", "b:pulse", "pulse@3", "b:pulse", "pulse@4", "b:pulse", "pulse@5",
                "ended:Expired", "b:end",
            }));
            Assert.That(thinker.HasEnded, Is.True);
            Assert.That(_spawner.Releases, Is.EqualTo(1));
            Assert.That(thinker.gameObject.activeSelf, Is.False, "o thinker não voltou ao spawner");
        }

        [Test]
        public void Pulse_TickInSameFrameAsPulseEnd_Counts()
        {
            GameplayThinker thinker = CreateActive(0f, 1f, t => t.OnPulse.Add(Log("pulse")));
            AbilitySystemComponent asc = thinker.AbilitySystem;
            asc.Tick(0.5f);
            thinker.Begin(null, null, _spawner);

            asc.Tick(0.75f);
            Assert.That(_log, Is.Empty, "o pulso de 1 s veio com 0,75 s");
            asc.Tick(0.25f);
            Assert.That(_log, Has.Count.EqualTo(1), "o Tick no mesmo quadro do fim do intervalo não contou");
            asc.Tick(1.5f);
            Assert.That(_log, Has.Count.EqualTo(2));
            asc.Tick(0.5f);

            Assert.That(_log, Has.Count.EqualTo(3), "o excesso de 0,5 s do Tick grande não passou ao pulso seguinte");
        }

        [Test]
        public void SameAction_InPulseAndAbilityActivate()
        {
            GameplayTag tag = _gas.CreateTag("Test.Applied");
            var apply = new ApplyEffectAction { Effects = new List<GameplayEffect> { _gas.CreateDurationEffect(10f, tag) } };
            AbilitySystemComponent caster = CreateAsc();
            AbilitySystemComponent target = CreateAsc();
            GameplayAbility ability = _gas.CreateAbility(apply);
            caster.GiveAbility(ability);
            GameplayThinker thinker = CreateActive(0f, 1f, t => t.OnPulse.Add(apply));

            thinker.Begin(caster, null, _spawner);
            thinker.AbilitySystem.Tick(1f);
            caster.TryActivateAbility(ability, AbilityTarget.AtUnit(target));

            Assert.That(thinker.AbilitySystem.HasTag(tag), Is.True, "o pulso não aplicou no próprio thinker");
            Assert.That(thinker.AbilitySystem.ActiveEffects[0].Context.Instigator, Is.SameAs(caster));
            Assert.That(target.HasTag(tag), Is.True, "a ativação não aplicou no alvo");
        }

        [Test]
        public void End_CancelsRunningAndDelayed_TwiceDoesNotRepeat()
        {
            GameplayThinker thinker = CreateActive(0f, 1f, t =>
            {
                t.OnPulse.Add(new DelayedAction
                {
                    Delay = ScalableFloat.Constant(5f),
                    Actions = new List<GameplayAction> { Log("delayed") },
                });
                t.OnPulse.Add(new WaitThenRecordAction(10f, "long", _log));
                t.OnEnded.Add(LogReason("ended"));
            });
            thinker.Begin(null, null, _spawner);
            thinker.AbilitySystem.Tick(1f);

            thinker.End(ActionEndReason.Released);
            thinker.End(ActionEndReason.Expired);
            thinker.AbilitySystem.Tick(20f);

            Assert.That(_log, Is.EqualTo(new[] { "cancel:long", "ended:Released" }));
            Assert.That(_spawner.Releases, Is.EqualTo(1));
        }

        [Test]
        public void FromPool_StartsClean()
        {
            AbilitySystemComponent caster = CreateAsc();
            GameplayThinker thinker = CreateActive(2f, 1f, t =>
            {
                t.OnCreated.Add(LogContext("created"));
                t.OnPulse.Add(Log("pulse"));
                t.OnEnded.Add(LogReason("ended"));
            });
            thinker.Begin(caster, null, _spawner);
            thinker.AbilitySystem.Tick(1f);
            thinker.End(ActionEndReason.Released);

            GameObject reused = _spawner.Spawn(null, Vector3.zero, Quaternion.identity);
            thinker.Begin(null, null, _spawner);
            thinker.AbilitySystem.Tick(1f);
            bool endedBeforeDuration = thinker.HasEnded;
            thinker.AbilitySystem.Tick(1f);

            Assert.That(reused, Is.SameAs(thinker.gameObject));
            Assert.That(endedBeforeDuration, Is.False, "o término da vida anterior passou à nova");
            Assert.That(_log, Is.EqualTo(new[]
            {
                "created:Thinker/ASC/1/Thinker", "pulse", "ended:Released",
                "created:Thinker/none/1/Thinker", "pulse", "pulse", "ended:Expired",
            }));
            Assert.That(_spawner.Releases, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ScenePlaced_NoCreator_RunsWithOwnAsc()
        {
            GameplayThinker thinker = _gas.CreateThinkerPrefab(1f, 0f, t =>
            {
                t.OnCreated.Add(LogContext("created"));
                t.OnEnded.Add(LogReason("ended"));
            });

            thinker.gameObject.SetActive(true);
            yield return null;
            bool createdOnStart = _log.Count == 1;
            thinker.AbilitySystem.Tick(1f);
            yield return null;

            Assert.That(createdOnStart, Is.True, "o thinker da cena não começou no Start");
            Assert.That(_log, Is.EqualTo(new[] { "created:Thinker/none/1/Thinker", "ended:Expired" }));
            Assert.That(thinker == null, Is.True, "o thinker da cena não voltou pelo spawner padrão");
        }

        [Test]
        public void PulseOverlap_LongPulseDoesNotDelayNext()
        {
            GameplayThinker thinker = CreateActive(0f, 1f, t =>
            {
                t.OnPulse.Add(Log("pulse"));
                t.OnPulse.Add(new WaitThenRecordAction(2.5f, "end-of-pulse", _log));
            });
            thinker.Begin(null, null, _spawner);

            for (int i = 0; i < 4; i++) thinker.AbilitySystem.Tick(1f);

            Assert.That(_log, Has.Exactly(4).EqualTo("pulse"), "a espera de um pulso atrasou o seguinte");
            Assert.That(_log, Has.Exactly(1).EqualTo("end-of-pulse"));
        }

        private GameplayThinker CreateActive(float duration, float pulseInterval, Action<GameplayThinker> configure)
        {
            GameplayThinker thinker = _gas.CreateThinkerPrefab(duration, pulseInterval, configure);
            thinker.gameObject.SetActive(true);
            return thinker;
        }

        private LogAction Log(string name) => new LogAction(_log, _ => name);

        private LogAction LogReason(string name) => new LogAction(_log, c => $"{name}:{c.EndReason}");

        // Dono, instigador, nível e primeiro alvo da execução
        private LogAction LogContext(string name) => new LogAction(_log, c =>
            $"{name}:{c.Owner.name}/{(c.Instigator != null ? c.Instigator.name : "none")}/{c.Level}/{c.Targets[0].name}");

        private AbilitySystemComponent CreateAsc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            return asc;
        }

        /// <summary>Spawner de uma vaga: devolver só desativa, e criar reativa o último devolvido.</summary>
        private sealed class OneSlotSpawner : SpawnerAsset
        {
            private GameObject _released;

            public int Releases { get; private set; }

            public override GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            {
                GameObject instance = _released;
                _released = null;
                if (instance != null) instance.SetActive(true);
                return instance;
            }

            public override void Release(GameObject instance)
            {
                Releases++;
                instance.SetActive(false);
                _released = instance;
            }
        }

        [Serializable]
        private sealed class RecordBehaviour : ThinkerBehaviour
        {
            private readonly List<string> _log;

            public RecordBehaviour(List<string> log) => _log = log;

            public override void OnBegin(object state, GameplayThinker thinker, ActionContext context) => _log.Add("b:begin");

            public override void OnPulse(object state, GameplayThinker thinker, ActionContext context) => _log.Add("b:pulse");

            public override void OnEnd(object state, GameplayThinker thinker, ActionContext context) => _log.Add("b:end");
        }

        /// <summary>Escreve no registro o texto que <c>text</c> monta a partir do contexto.</summary>
        [Serializable]
        private sealed class LogAction : GameplayAction
        {
            private readonly List<string> _log;
            private readonly Func<ActionContext, string> _text;

            public LogAction(List<string> log, Func<ActionContext, string> text)
            {
                _log = log;
                _text = text;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _log.Add(_text(context));
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
