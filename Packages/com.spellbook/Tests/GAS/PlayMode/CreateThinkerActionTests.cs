using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Thinkers;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// <see cref="CreateThinkerAction"/>, criador, seguir o dono e terminar com a execução (B-014, TK5; ADR 0036, itens 1,
    /// 5 e 6). Os ASCs têm <c>autoTick</c> desligado. O spawner entrega o próprio modelo do thinker, que fica inativo até
    /// ser criado, e devolver só o desativa.
    /// </summary>
    public class CreateThinkerActionTests
    {
        public enum Ending { EffectRemoved, AbilityCancelled }

        private GasFactory _gas;
        private TimeFactory _time;
        private List<string> _log;
        private List<ModelSpawner> _spawners;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _log = new List<string>();
            _spawners = new List<ModelSpawner>();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _time.Dispose();
            foreach (ModelSpawner spawner in _spawners) Object.Destroy(spawner);
        }

        [Test]
        public void Create_BySpawner_WithCreatorAbilityLevelContext_ReturnsThinker()
        {
            Captured created = null;
            GameplayThinker thinker = Model(0f, t =>
            {
                t.EndWithExecution = false;
                t.OnCreated.Add(new CallbackAction(c => created = new Captured(c)));
            });
            ModelSpawner spawner = Spawner(thinker);
            GameplayThinker last = null;
            GameplayAbility ability = _gas.CreateAbility(Create(thinker, spawner, ThinkerPosition.Point),
                new CallbackAction(c => last = c.LastCreatedThinker));
            var so = new SerializedObject(ability);
            so.FindProperty("maxLevel").intValue = 3;
            so.ApplyModifiedPropertiesWithoutUndo();
            AbilitySystemComponent caster = CreateAsc();
            AbilitySpec spec = caster.GiveAbility(ability, 3);

            caster.TryActivateAbility(spec, new Vector3(4f, 0f, 2f));

            Assert.That(spawner.Spawns, Is.EqualTo(1));
            Assert.That(thinker.gameObject.activeSelf, Is.True);
            Assert.That(thinker.transform.position, Is.EqualTo(new Vector3(4f, 0f, 2f)));
            Assert.That(created, Is.Not.Null, "\"Ao criar\" não rodou");
            Assert.That(created.Owner, Is.SameAs(thinker.AbilitySystem), "a execução não roda no ASC do thinker");
            Assert.That(created.Instigator, Is.SameAs(caster));
            Assert.That(created.Ability, Is.SameAs(ability));
            Assert.That(created.Level, Is.EqualTo(3));
            Assert.That(last, Is.SameAs(thinker), "o thinker não voltou ao contexto");
        }

        [Test]
        public void TimeSource_InheritedFromCreator()
        {
            GameplayThinker thinker = Model(0f, t => t.EndWithExecution = false);
            AbilitySystemComponent caster = CreateAsc();
            caster.GiveAbility(_gas.CreateAbility(Create(thinker, Spawner(thinker), ThinkerPosition.Caster)));
            ITimeSource source = caster.TimeSource;

            caster.TryActivateAbility(caster.Abilities[0], Vector3.zero);

            Assert.That(thinker.AbilitySystem.TimeSource, Is.SameAs(source));
        }

        [UnityTest]
        public IEnumerator FollowOwner_Tracks_CreatorDisabledOrDestroyed_EndsOwnerGone()
        {
            foreach (bool destroy in new[] { false, true })
            {
                _log.Clear();
                GameplayThinker thinker = Model(0f, t =>
                {
                    t.FollowOwner = true;
                    t.FollowRotation = true;
                    t.EndWithExecution = false;
                    t.OnEnded.Add(LogReason("ended"));
                });
                ModelSpawner spawner = Spawner(thinker);
                AbilitySystemComponent caster = CreateAsc();
                caster.GiveAbility(_gas.CreateAbility(Create(thinker, spawner, ThinkerPosition.Caster)));
                caster.TryActivateAbility(caster.Abilities[0], Vector3.zero);

                caster.transform.SetPositionAndRotation(new Vector3(3f, 0f, 1f), Quaternion.Euler(0f, 90f, 0f));
                yield return null;
                Assert.That(thinker.transform.position, Is.EqualTo(new Vector3(3f, 0f, 1f)), "o thinker não seguiu o dono");
                Assert.That(Quaternion.Angle(thinker.transform.rotation, caster.transform.rotation), Is.LessThan(0.01f));
                Assert.That(_log, Is.Empty, "o thinker terminou com o dono ativo");

                if (destroy) Object.Destroy(caster.gameObject);
                else caster.gameObject.SetActive(false);
                yield return null;
                yield return null;

                Assert.That(_log, Is.EqualTo(new[] { "ended:OwnerGone" }), destroy ? "dono destruído" : "dono desativado");
                Assert.That(spawner.Releases, Is.EqualTo(1));
            }
        }

        // Padrão (seguir e terminar com a execução) com a ativação em curso: o OnDisable do ASC cancela a vigia antes do
        // LateUpdate do thinker, e o motivo continua OwnerGone (PRD R6; ADR 0036, item 6)
        [UnityTest]
        public IEnumerator DefaultConfig_ActivationRunning_CreatorDisabledOrDestroyed_EndsOwnerGone()
        {
            foreach (bool destroy in new[] { false, true })
            {
                _log.Clear();
                GameplayThinker thinker = Model(0f, t =>
                {
                    t.FollowOwner = true;
                    t.EndWithExecution = true;
                    t.OnEnded.Add(LogReason("ended"));
                });
                ModelSpawner spawner = Spawner(thinker);
                AbilitySystemComponent caster = CreateAsc();
                AbilitySpec spec = caster.GiveAbility(
                    _gas.CreateAbility(Create(thinker, spawner, ThinkerPosition.Caster), new WaitAction(10f)));
                caster.TryActivateAbility(spec, Vector3.zero);
                yield return null;
                Assert.That(_log, Is.Empty, "o thinker terminou com a ativação em curso");

                if (destroy) Object.Destroy(caster.gameObject);
                else caster.gameObject.SetActive(false);
                yield return null;
                yield return null;

                Assert.That(_log, Is.EqualTo(new[] { "ended:OwnerGone" }), destroy ? "dono destruído" : "dono desativado");
                Assert.That(spawner.Releases, Is.EqualTo(1));
            }
        }

        [TestCase(Ending.EffectRemoved)]
        [TestCase(Ending.AbilityCancelled)]
        public void EndWithExecution_EffectRemoved_AbilityCancelled_EndsExecutionEnded(Ending ending)
        {
            GameplayThinker thinker = Model(0f, t => t.OnEnded.Add(LogReason("ended")));
            ModelSpawner spawner = Spawner(thinker);
            CreateThinkerAction create = Create(thinker, spawner, ThinkerPosition.Caster);
            AbilitySystemComponent caster = CreateAsc();

            bool aliveBefore;
            if (ending == Ending.EffectRemoved)
            {
                GameplayEffect effect = _gas.CreateDurationEffect(10f);
                effect.OnApplied.Add(create);
                ActiveEffectInstance instance = caster.ApplyEffect(effect);
                caster.Tick(1f);
                aliveBefore = !thinker.HasEnded;
                caster.RemoveEffect(instance);
            }
            else
            {
                GameplayAbility ability = _gas.CreateAbility(create, new WaitAction(10f));
                AbilitySpec spec = caster.GiveAbility(ability);
                caster.TryActivateAbility(spec, Vector3.zero);
                caster.Tick(1f);
                aliveBefore = !thinker.HasEnded;
                caster.CancelAbility(spec);
            }
            caster.Tick(20f);

            Assert.That(aliveBefore, Is.True, "o thinker terminou antes do dono");
            Assert.That(_log, Is.EqualTo(new[] { "ended:ExecutionEnded" }));
            Assert.That(spawner.Releases, Is.EqualTo(1));
        }

        [Test]
        public void EndWithExecution_ThinkerEndsFirst_WatchCancelled()
        {
            GameplayThinker thinker = Model(1f, t => t.OnEnded.Add(LogReason("ended")));
            ModelSpawner spawner = Spawner(thinker);
            GameplayEffect effect = _gas.CreateDurationEffect(10f);
            effect.OnApplied.Add(Create(thinker, spawner, ThinkerPosition.Caster));
            AbilitySystemComponent target = CreateAsc();
            ActiveEffectInstance instance = target.ApplyEffect(effect);
            thinker.AbilitySystem.Tick(1f);

            // A mesma instância volta numa vida nova, sem vínculo com o efeito
            thinker.EndWithExecution = false;
            AbilitySystemComponent other = CreateAsc();
            other.GiveAbility(_gas.CreateAbility(Create(thinker, spawner, ThinkerPosition.Caster)));
            other.TryActivateAbility(other.Abilities[0], Vector3.zero);
            target.RemoveEffect(instance);

            Assert.That(_log, Is.EqualTo(new[] { "ended:Expired" }));
            Assert.That(thinker.HasEnded, Is.False, "a vigia da vida anterior terminou a vida nova");
            Assert.That(spawner.Spawns, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator NoFlags_OutlivesCreator()
        {
            GameplayThinker thinker = Model(2f, t =>
            {
                t.EndWithExecution = false;
                t.OnEnded.Add(LogReason("ended"));
            });
            AbilitySystemComponent caster = CreateAsc();
            caster.GiveAbility(_gas.CreateAbility(Create(thinker, Spawner(thinker), ThinkerPosition.Caster)));
            caster.TryActivateAbility(caster.Abilities[0], Vector3.zero);

            caster.transform.position = new Vector3(5f, 0f, 0f);
            yield return null;
            Vector3 afterMove = thinker.transform.position;
            Object.Destroy(caster.gameObject);
            yield return null;
            yield return null;
            bool endedWithCreator = thinker.HasEnded;
            thinker.AbilitySystem.Tick(2f);

            Assert.That(afterMove, Is.EqualTo(Vector3.zero), "sem seguir, o thinker saiu do lugar");
            Assert.That(endedWithCreator, Is.False, "sem seguir nem terminar com a execução, o thinker morreu com o criador");
            Assert.That(_log, Is.EqualTo(new[] { "ended:Expired" }));
        }

        [Test]
        public void SpawnerReturnsNull_SkipsWithWarning()
        {
            GameplayThinker thinker = Model(0f, null);
            ModelSpawner spawner = Spawner(null);
            GameplayAbility ability = _gas.CreateAbility(Create(thinker, spawner, ThinkerPosition.Caster),
                new CallbackAction(c => _log.Add("after:" + (c.LastCreatedThinker == null ? "none" : "thinker"))));
            AbilitySystemComponent caster = CreateAsc();
            AbilitySpec spec = caster.GiveAbility(ability);
            int warnings = 0;
            Application.LogCallback count = (message, _, type) =>
            {
                if (type == LogType.Warning && message.Contains(nameof(CreateThinkerAction))) warnings++;
            };

            Application.logMessageReceived += count;
            try
            {
                caster.TryActivateAbility(spec, Vector3.zero);
                caster.TryActivateAbility(spec, Vector3.zero);
            }
            finally
            {
                Application.logMessageReceived -= count;
            }

            Assert.That(spawner.Spawns, Is.EqualTo(2));
            Assert.That(warnings, Is.EqualTo(1), "o aviso não saiu uma vez só");
            Assert.That(_log, Is.EqualTo(new[] { "after:none", "after:none" }));
            Assert.That(thinker.gameObject.activeSelf, Is.False);
        }

        private GameplayThinker Model(float duration, Action<GameplayThinker> configure)
        {
            return _gas.CreateThinkerPrefab(duration, 0f, configure);
        }

        private ModelSpawner Spawner(GameplayThinker model)
        {
            var spawner = ScriptableObject.CreateInstance<ModelSpawner>();
            spawner.Model = model != null ? model.gameObject : null;
            _spawners.Add(spawner);
            return spawner;
        }

        private static CreateThinkerAction Create(GameplayThinker prefab, SpawnerAsset spawner, ThinkerPosition position)
        {
            return new CreateThinkerAction { Prefab = prefab, Spawner = spawner, Position = position };
        }

        private AbilitySystemComponent CreateAsc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        private CallbackAction LogReason(string name) => new CallbackAction(c => _log.Add($"{name}:{c.EndReason}"));

        /// <summary>Campos lidos pelo teste, copiados porque o contexto volta ao pool.</summary>
        private sealed class Captured
        {
            public readonly AbilitySystemComponent Owner;
            public readonly AbilitySystemComponent Instigator;
            public readonly GameplayAbility Ability;
            public readonly int Level;

            public Captured(ActionContext context)
            {
                Owner = context.Owner;
                Instigator = context.Instigator;
                Ability = context.Ability;
                Level = context.Level;
            }
        }

        /// <summary>Entrega o modelo na posição pedida e o ativa; sem modelo, devolve <c>null</c>. Devolver desativa.</summary>
        private sealed class ModelSpawner : SpawnerAsset
        {
            public GameObject Model;

            public int Spawns { get; private set; }
            public int Releases { get; private set; }

            public override GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            {
                Spawns++;
                if (Model == null) return null;
                Model.transform.SetPositionAndRotation(position, rotation);
                Model.SetActive(true);
                return Model;
            }

            public override void Release(GameObject instance)
            {
                Releases++;
                instance.SetActive(false);
            }
        }

        [Serializable]
        private sealed class CallbackAction : GameplayAction
        {
            private readonly Action<ActionContext> _callback;

            public CallbackAction(Action<ActionContext> callback) => _callback = callback;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _callback(context);
                return ActionStatus.Done;
            }
        }

        [Serializable]
        private sealed class WaitAction : GameplayAction
        {
            private readonly float _seconds;

            public WaitAction(float seconds) => _seconds = seconds;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                if (frame.Phase > 0) return ActionStatus.Done;
                frame.Phase = 1;
                return ActionStatus.Wait(_seconds);
            }
        }
    }
}
