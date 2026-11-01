using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Esperas das ações e do projétil contam o tempo do ASC ou da fonte de tempo, não o relógio da Unity (CA10; Spec 4.4).
    /// Os ASCs têm <c>autoTick</c> desligado: só avançam por <c>Tick</c>.
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

        [UnityTest]
        public IEnumerator WaitAction_ZeroDuration_WaitsOneFrame()
        {
            foreach (float duration in new[] { 0f, -1f })
            {
                GameplayAbility ability = _gas.CreateAbility(new WaitAction { Duration = duration });
                AbilitySystemComponent asc = CreateManualAsc(ability);

                asc.TryActivateAbility(ability, Vector3.zero);
                bool activeAfterStart = asc.IsAbilityActive(ability);
                yield return null;
                yield return null;

                Assert.That(activeAfterStart, Is.True, $"Duration {duration}: a espera terminou sem ceder um quadro");
                Assert.That(asc.IsAbilityActive(ability), Is.False, $"Duration {duration}: a espera não terminou no quadro seguinte");
            }
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
            // A 1ª repetição termina na hora, e a Unity só retoma o RepeatAction no quadro seguinte (corrotina
            // aninhada que termina sem ceder custa um quadro ao pai): o intervalo começa a contar ali, como na 2.x.
            // Dois quadros, para não depender da ordem das corrotinas dentro do quadro.
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
        public void WaitSeconds_CountsRemainingAcrossTicks()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            IEnumerator wait = GasFactory.CreateAbilityContext(asc, Vector3.zero).WaitSeconds(1f);

            bool waitingAtStart = wait.MoveNext();
            asc.Tick(0.4f);
            bool waitingAfter0_4 = wait.MoveNext();
            asc.Tick(0.7f);
            bool waitingAfter1_1 = wait.MoveNext();

            Assert.That(waitingAtStart, Is.True);
            Assert.That(waitingAfter0_4, Is.True, "a espera terminou antes de 1 s de Tick");
            Assert.That(waitingAfter1_1, Is.False, "a espera não terminou depois de 1,1 s de Tick");
        }

        [UnityTest]
        public IEnumerator WaitSeconds_SourceDestroyed_Ends()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            IEnumerator wait = GasFactory.CreateAbilityContext(asc, Vector3.zero).WaitSeconds(1f);
            Assert.That(wait.MoveNext(), Is.True);

            Object.Destroy(asc.gameObject);
            yield return null;

            Assert.That(wait.MoveNext(), Is.False, "a espera continuou com o ASC destruído");
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
                projectile.Setup(spawner, _gas.CreateAsc(), new List<AbilityAction>());

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
            projectile.Setup(spawner, _gas.CreateAsc(), new List<AbilityAction>());
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
            projectile.Setup(spawner, _gas.CreateAsc(), new List<AbilityAction>());

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

        private sealed class CountingAction : AbilityAction
        {
            public int Count;

            public override IEnumerator Execute(AbilityContext context)
            {
                Count++;
                yield break;
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
