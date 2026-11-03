using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Esperas das ações e do projétil contam o tempo do ASC ou da fonte de tempo, não o relógio da Unity (CA10; Spec 4.4).
    /// As ações rodam pelo executor do ASC: sem espera, na mesma chamada (R8); a espera conta do fim do passo anterior,
    /// também com um <c>Tick</c> no mesmo quadro (R6). Os ASCs têm <c>autoTick</c> desligado: só avançam por <c>Tick</c>.
    /// </summary>
    public class AbilityWaitTests
    {
        // Cada quadro avança 0,1 s no relógio escalado da Unity, sem depender do tempo real.
        private const float FrameSeconds = 0.1f;

        private GasFactory _gas;
        private TimeFactory _time;
        private SpawnFactory _spawn;
        private float _captureDeltaTime;

        [SetUp]
        public void SetUp()
        {
            _captureDeltaTime = UnityEngine.Time.captureDeltaTime;
            UnityEngine.Time.captureDeltaTime = FrameSeconds;
            _gas = new GasFactory();
            _time = new TimeFactory();
            _spawn = new SpawnFactory();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Time.captureDeltaTime = _captureDeltaTime;
            _gas.Dispose();
            _time.Dispose();
            _spawn.Dispose();
        }

        [UnityTest]
        public IEnumerator WaitAction_FinishesByAscTicks_NotRealTime()
        {
            GameplayAbility ability = _gas.CreateAbility(new WaitAction { Duration = 0.2f });
            AbilitySystemComponent asc = CreateManualAsc(ability);

            asc.TryActivateAbility(ability, Vector3.zero);
            for (int i = 0; i < 5; i++)
            {
                yield return null; // 0,5 s no relógio da Unity
            }

            bool activeWithoutTicks = asc.IsAbilityActive(ability);
            asc.Tick(0.3f);
            yield return null;

            Assert.That(activeWithoutTicks, Is.True, "a espera terminou pelo relógio da Unity");
            Assert.That(asc.IsAbilityActive(ability), Is.False, "a espera não terminou depois do Tick");
        }

        [Test]
        public void WaitAction_ZeroOrNegative_CompletesInSameCall()
        {
            foreach (float duration in new[] { 0f, -1f })
            {
                GameplayAbility ability = _gas.CreateAbility(new WaitAction { Duration = duration });
                AbilitySystemComponent asc = CreateManualAsc(ability);

                asc.TryActivateAbility(ability, Vector3.zero);

                Assert.That(asc.IsAbilityActive(ability), Is.False, $"Duration {duration}: a espera custou um quadro");
            }
        }

        [TestCase(WaitStep.Wait)]
        [TestCase(WaitStep.RepeatInterval)]
        [TestCase(WaitStep.DelayBetweenShots)]
        public void TickInSameFrameAsStepEnd_CountsForWait(WaitStep step)
        {
            var counter = new CountingAction();
            CountingSpawner spawner = CreateCountingSpawner();
            GameplayAbility ability = _gas.CreateAbility(CreateStepsWithWait(step, counter, spawner));
            AbilitySystemComponent asc = CreateManualAsc(ability);

            asc.TryActivateAbility(ability, new Vector3(0f, 0f, 10f));
            int runsBeforeTick = step == WaitStep.DelayBetweenShots ? spawner.Count : counter.Count;
            asc.Tick(1f); // no mesmo quadro em que o passo anterior terminou
            int runsAfterTick = step == WaitStep.DelayBetweenShots ? spawner.Count : counter.Count;

            Assert.That(runsBeforeTick, Is.EqualTo(1), "o passo antes da espera não rodou na ativação");
            Assert.That(runsAfterTick, Is.EqualTo(2), "o Tick no mesmo quadro não contou para a espera");
        }

        [Test]
        public void RepeatCount3Interval0_SameCall()
        {
            var counter = new CountingAction();
            var repeat = new RepeatAction { Count = 3, Interval = 0f };
            repeat.Actions.Add(counter);
            GameplayAbility ability = _gas.CreateAbility(repeat);
            AbilitySystemComponent asc = CreateManualAsc(ability);

            asc.TryActivateAbility(ability, Vector3.zero);

            Assert.That(counter.Count, Is.EqualTo(3), "as repetições sem intervalo não rodaram na mesma chamada");
            Assert.That(asc.IsAbilityActive(ability), Is.False, "a habilidade não terminou na mesma chamada");
        }

        [UnityTest]
        public IEnumerator RepeatAction_Interval_CountsAscTicks()
        {
            var counter = new CountingAction();
            var repeat = new RepeatAction { Count = 2, Interval = 1f };
            repeat.Actions.Add(counter);
            GameplayAbility ability = _gas.CreateAbility(repeat);
            AbilitySystemComponent asc = CreateManualAsc(ability);

            asc.TryActivateAbility(ability, Vector3.zero);
            // A 1ª repetição termina na ativação, e o intervalo conta dali; os quadros sem Tick não contam
            yield return null;
            yield return null;
            asc.Tick(0.5f);
            yield return null;
            int afterHalf = counter.Count;
            asc.Tick(0.6f);
            yield return null;

            Assert.That(afterHalf, Is.EqualTo(1), "a 2ª repetição não esperou o intervalo");
            Assert.That(counter.Count, Is.EqualTo(2), "a 2ª repetição não rodou depois do intervalo");
        }

        [UnityTest]
        public IEnumerator SpawnProjectile_DelayBetweenShots_CountsAscTicks()
        {
            var spawner = _spawn.Track(ScriptableObject.CreateInstance<CountingSpawner>());
            spawner.Factory = _spawn;
            var shoot = new SpawnProjectileAction
            {
                ProjectilePrefab = _spawn.CreatePrefab("Shot_Test"),
                Spawner = spawner,
                ProjectileCount = 2,
                DelayBetweenShots = 1f,
            };
            GameplayAbility ability = _gas.CreateAbility(shoot);
            AbilitySystemComponent asc = CreateManualAsc(ability);

            asc.TryActivateAbility(ability, new Vector3(0f, 0f, 10f));
            asc.Tick(0.5f);
            yield return null;
            int afterHalf = spawner.Count;
            asc.Tick(0.6f);
            yield return null;

            Assert.That(afterHalf, Is.EqualTo(1), "o 2º tiro não esperou o DelayBetweenShots");
            Assert.That(spawner.Count, Is.EqualTo(2), "o 2º tiro não saiu depois do DelayBetweenShots");
        }

        [Test]
        public void SameAction_InAbilityAndEffectLists()
        {
            var counter = new CountingAction();
            GameplayAbility ability = _gas.CreateAbility(counter);
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            effect.OnApplied.Add(counter);
            AbilitySystemComponent asc = CreateManualAsc(ability);

            asc.TryActivateAbility(ability, Vector3.zero);
            asc.ApplyEffect(effect);

            Assert.That(counter.Count, Is.EqualTo(2), "a mesma ação não rodou nas duas listas");
            Assert.That(asc.IsAbilityActive(ability), Is.False);
        }

        [Test]
        public void Projectile_ImpactRunsOnCasterAsc()
        {
            var counter = new CountingAction();
            AbilitySystemComponent caster = CreateManualAsc();
            CollidingProjectile projectile = CreateCollidingProjectile();
            projectile.Setup(CreateCountingSpawner(), caster, new List<GameplayAction> { new WaitAction { Duration = 1f }, counter });

            projectile.Collide(_gas.CreateAsc().gameObject);
            int runsBeforeTick = counter.Count;
            caster.Tick(1f);

            Assert.That(runsBeforeTick, Is.EqualTo(0), "a ação depois da espera rodou antes do Tick");
            Assert.That(counter.Count, Is.EqualTo(1), "a lista do impacto não andou pelo Tick do ASC de quem lançou");
        }

        [UnityTest]
        public IEnumerator Projectile_Lifetime_CountsItsTimeSource()
        {
            ManualTimeSource source = _time.CreateManual();
            var go = _spawn.Track(new GameObject("Projectile_Test"));
            go.SetActive(false);
            var projectile = go.AddComponent<Projectile>();
            projectile.Lifetime = 1f;
            projectile.Speed = 0f;
            SetTimeSource(projectile, source);
            go.SetActive(true);

            source.Advance(0.6f);
            yield return new WaitForFixedUpdate();
            bool aliveAfter0_6 = go != null && go.activeInHierarchy;
            source.Advance(0.6f);
            yield return new WaitForFixedUpdate();
            yield return null; // o spawner padrão destrói no fim do quadro

            Assert.That(aliveAfter0_6, Is.True, "o projétil voltou antes do fim da vida");
            Assert.That(go == null, Is.True, "o projétil não voltou depois de 1,2 s na fonte de tempo");
        }

        [UnityTest]
        public IEnumerator Projectile_Lifetime_ReleasesOnce()
        {
            float fixedDeltaTime = UnityEngine.Time.fixedDeltaTime;
            UnityEngine.Time.fixedDeltaTime = FrameSeconds / 5f; // 5 passos de física por quadro
            try
            {
                ManualTimeSource source = _time.CreateManual();
                var spawner = _spawn.Track(ScriptableObject.CreateInstance<CountingSpawner>());
                var go = _spawn.Track(new GameObject("Projectile_Test"));
                go.SetActive(false);
                var projectile = go.AddComponent<Projectile>();
                projectile.Lifetime = 1f;
                projectile.Speed = 0f;
                SetTimeSource(projectile, source);
                go.SetActive(true);
                projectile.Setup(spawner, _gas.CreateAsc(), new List<GameplayAction>());

                source.Advance(1.2f);
                yield return null; // o Destroy do spawner só vale no fim do quadro

                Assert.That(spawner.Released, Is.EqualTo(1), "o projétil não voltou exatamente uma vez pelo spawner");
            }
            finally
            {
                UnityEngine.Time.fixedDeltaTime = fixedDeltaTime;
            }
        }

        [UnityTest]
        public IEnumerator Projectile_Invulnerability_CountsItsTimeSource()
        {
            ManualTimeSource source = _time.CreateManual();
            var spawner = _spawn.Track(ScriptableObject.CreateInstance<CountingSpawner>());
            var go = _spawn.Track(new GameObject("Projectile_Test"));
            go.SetActive(false);
            var projectile = go.AddComponent<CollidingProjectile>();
            projectile.InitialInvulnerabilityDuration = 1f;
            projectile.Lifetime = 0f; // sem limite de vida
            projectile.Speed = 0f;
            SetTimeSource(projectile, source);
            go.SetActive(true);
            projectile.Setup(spawner, _gas.CreateAsc(), new List<GameplayAction>());
            GameObject target = _gas.CreateAsc().gameObject;

            projectile.Collide(target);
            int releasedWhileInvulnerable = spawner.Released;
            source.Advance(1f);
            yield return new WaitForFixedUpdate();
            projectile.Collide(target);

            Assert.That(releasedWhileInvulnerable, Is.EqualTo(0), "o choque não foi ignorado durante a invulnerabilidade");
            Assert.That(spawner.Released, Is.EqualTo(1), "o choque não devolveu o projétil depois de 1 s na fonte de tempo");
        }

        [UnityTest]
        public IEnumerator Projectile_InvulnerableFromSubclass_WithZeroDuration_IgnoresCollision()
        {
            ManualTimeSource source = _time.CreateManual();
            var spawner = _spawn.Track(ScriptableObject.CreateInstance<CountingSpawner>());
            var go = _spawn.Track(new GameObject("Projectile_Test"));
            go.SetActive(false);
            var projectile = go.AddComponent<InvulnerableProjectile>();
            projectile.InitialInvulnerabilityDuration = 0f;
            projectile.Lifetime = 0f; // sem limite de vida
            projectile.Speed = 0f;
            SetTimeSource(projectile, source);
            go.SetActive(true);
            projectile.Setup(spawner, _gas.CreateAsc(), new List<GameplayAction>());

            source.Advance(1f);
            yield return new WaitForFixedUpdate();
            projectile.Collide(_gas.CreateAsc().gameObject);

            Assert.That(spawner.Released, Is.EqualTo(0), "o contador desligou a invulnerabilidade ligada pela subclasse");
        }

        private static void SetTimeSource(Projectile projectile, TimeSourceAsset source)
        {
            var serialized = new SerializedObject(projectile);
            serialized.FindProperty("timeSource").objectReferenceValue = source;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Passo que muda a contagem (ação ou tiro), uma espera de 1 s e o mesmo passo de novo.</summary>
        private GameplayAction[] CreateStepsWithWait(WaitStep step, CountingAction counter, CountingSpawner spawner)
        {
            switch (step)
            {
                case WaitStep.Wait:
                    return new GameplayAction[] { counter, new WaitAction { Duration = 1f }, counter };
                case WaitStep.RepeatInterval:
                    var repeat = new RepeatAction { Count = 2, Interval = 1f };
                    repeat.Actions.Add(counter);
                    return new GameplayAction[] { repeat };
                default:
                    return new GameplayAction[]
                    {
                        new SpawnProjectileAction
                        {
                            ProjectilePrefab = _spawn.CreatePrefab("Shot_Test"),
                            Spawner = spawner,
                            ProjectileCount = 2,
                            DelayBetweenShots = 1f,
                        },
                    };
            }
        }

        private CountingSpawner CreateCountingSpawner()
        {
            var spawner = _spawn.Track(ScriptableObject.CreateInstance<CountingSpawner>());
            spawner.Factory = _spawn;
            return spawner;
        }

        /// <summary>Projétil parado, sem limite de vida e sem invulnerabilidade.</summary>
        private CollidingProjectile CreateCollidingProjectile()
        {
            var go = _spawn.Track(new GameObject("Projectile_Test"));
            go.SetActive(false);
            var projectile = go.AddComponent<CollidingProjectile>();
            projectile.InitialInvulnerabilityDuration = 0f;
            projectile.Lifetime = 0f;
            projectile.Speed = 0f;
            SetTimeSource(projectile, _time.CreateManual());
            go.SetActive(true);
            return projectile;
        }

        /// <summary>ASC com fonte manual e <c>autoTick</c> desligado, com as <paramref name="abilities"/> concedidas.</summary>
        private AbilitySystemComponent CreateManualAsc(params GameplayAbility[] abilities)
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            foreach (GameplayAbility ability in abilities)
            {
                asc.GiveAbility(ability);
            }

            return asc;
        }

        public enum WaitStep { Wait, RepeatInterval, DelayBetweenShots }

        private sealed class CountingAction : GameplayAction
        {
            public int Count;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Count++;
                return ActionStatus.Done;
            }
        }

        /// <summary>Projétil que expõe o <c>HandleCollision</c> para simular um choque sem física.</summary>
        private class CollidingProjectile : Projectile
        {
            public void Collide(GameObject other) => HandleCollision(other);
        }

        /// <summary>Projétil cuja subclasse liga a invulnerabilidade no <c>Initialization</c>, sem duração.</summary>
        private sealed class InvulnerableProjectile : CollidingProjectile
        {
            protected override void Initialization()
            {
                base.Initialization();
                _isInvulnerable = true;
            }
        }

        /// <summary>Spawner que cria por <c>Instantiate</c> e conta quantas instâncias criou e recebeu de volta.</summary>
        private sealed class CountingSpawner : SpawnerAsset
        {
            public int Count;
            public int Released;
            public SpawnFactory Factory;

            public override GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            {
                Count++;
                return Factory.Track(Instantiate(prefab, position, rotation, parent));
            }

            public override void Release(GameObject instance)
            {
                Released++;
                Destroy(instance);
            }
        }
    }
}
