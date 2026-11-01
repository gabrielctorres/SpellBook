using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.GAS.Tests
{
    /// <summary>Ações de spawn e projétil criam e devolvem pelo <c>SpawnerAsset</c> (CA29, CA62; ADR 0015).</summary>
    public class SpawnActionsTests
    {
        private GasFactory _gas;
        private SpawnFactory _spawn;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _spawn = new SpawnFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _spawn.Dispose();
        }

        [Test]
        public void SpawnProjectile_NoSpawner_NoPoolManager_NoException()
        {
            GameObject prefab = _spawn.CreatePrefab("Projectile_Test");
            prefab.AddComponent<Projectile>();
            AbilitySystemComponent caster = _gas.CreateAsc();
            var action = new SpawnProjectileAction { ProjectilePrefab = prefab };
            AbilityContext context = GasFactory.CreateAbilityContext(caster, new Vector3(0f, 0f, 10f));
            HashSet<Projectile> existing = FindExisting<Projectile>();

            Assert.DoesNotThrow(() => RunToEnd(action.Execute(context)));

            List<Projectile> spawned = TrackSpawned(existing);
            Assert.That(spawned, Has.Count.EqualTo(1), "o projétil deveria ter sido criado pelo spawner padrão");
        }

        [Test]
        public void PlayCue_NoSpawner_NoPoolManager_NoException()
        {
            GameObject prefab = _spawn.CreateParticlePrefab("Cue_Test");
            var position = new Vector3(3f, 0f, 4f);
            var action = new PlayCueAction { VFX_Prefab = prefab, TargetSource = AbilityTargetSource.Location };
            AbilityContext context = GasFactory.CreateAbilityContext(_gas.CreateAsc(), position);
            HashSet<ParticleSystem> existing = FindExisting<ParticleSystem>();

            Assert.DoesNotThrow(() => RunToEnd(action.Execute(context)));

            List<ParticleSystem> spawned = TrackSpawned(existing);
            Assert.That(spawned, Has.Count.EqualTo(1), "o VFX deveria ter sido criado pelo spawner padrão");
            Assert.That(spawned[0].transform.position, Is.EqualTo(position));
        }

        [Test]
        public void SpawnVfx_ReleasesThroughConfiguredSpawner()
        {
            RecordingSpawner spawner = CreateRecordingSpawner();
            var action = new SpawnVFXEffectAction
            {
                Prefab = _spawn.CreatePrefab("Vfx_Test"),
                AttachPointName = string.Empty,
                DestroyOnRemoval = true,
                Spawner = spawner,
            };
            GameplayEffect effect = _gas.CreateDurationEffect(2f);
            effect.OnApplication = new List<EffectAction> { action };
            AbilitySystemComponent target = _gas.CreateAsc();
            ActiveEffectInstance instance = target.ApplyEffect(effect);

            target.RemoveEffect(instance);

            Assert.That(spawner.Spawned, Has.Count.EqualTo(1), "o VFX deveria ser criado pelo spawner configurado");
            Assert.That(spawner.Released, Is.EqualTo(spawner.Spawned), "o VFX deveria voltar pelo mesmo spawner");
        }

        [Test]
        public void Projectile_ReturnsThroughSpawner()
        {
            RecordingSpawner spawner = CreateRecordingSpawner();
            GameObject go = _spawn.Track(new GameObject("Projectile_Test"));
            var projectile = go.AddComponent<Projectile>();
            AbilityContext context = GasFactory.CreateAbilityContext(_gas.CreateAsc(), Vector3.zero);
            projectile.Setup(spawner, context, new List<AbilityAction>());

            projectile.ReturnToPool();

            Assert.That(spawner.Released, Is.EqualTo(new[] { go }));
        }

        [Test]
        public void SpawnProjectile_SpawnerReturnsNull_SkipsShotsWithWarning()
        {
            NullSpawner spawner = CreateNullSpawner(nullCalls: int.MaxValue);
            GameObject prefab = _spawn.CreatePrefab("Projectile_Test");
            prefab.AddComponent<Projectile>();
            var action = new SpawnProjectileAction { ProjectilePrefab = prefab, Spawner = spawner, ProjectileCount = 2 };
            AbilityContext context = GasFactory.CreateAbilityContext(_gas.CreateAsc(), new Vector3(0f, 0f, 10f));
            LogAssert.Expect(LogType.Warning, SpawnFailedWarning(spawner, prefab));
            LogAssert.Expect(LogType.Warning, SpawnFailedWarning(spawner, prefab));

            Assert.DoesNotThrow(() => RunToEnd(action.Execute(context)));

            Assert.That(spawner.SpawnCalls, Is.EqualTo(2), "cada tiro da rajada deveria pedir uma instância ao spawner");
        }

        [Test]
        public void SpawnProjectile_SpawnerReturnsNullOnce_NextShotStillSpawns()
        {
            NullSpawner spawner = CreateNullSpawner(nullCalls: 1);
            GameObject prefab = _spawn.CreatePrefab("Projectile_Test");
            prefab.AddComponent<Projectile>();
            AbilitySystemComponent caster = _gas.CreateAsc();
            var action = new SpawnProjectileAction { ProjectilePrefab = prefab, Spawner = spawner, ProjectileCount = 2 };
            AbilityContext context = GasFactory.CreateAbilityContext(caster, new Vector3(0f, 0f, 10f));
            LogAssert.Expect(LogType.Warning, SpawnFailedWarning(spawner, prefab));

            RunToEnd(action.Execute(context));

            Assert.That(spawner.Spawned, Has.Count.EqualTo(1), "o segundo tiro deveria sair depois do primeiro falhar");
            Projectile projectile = spawner.Spawned[0].GetComponent<Projectile>();
            Assert.That(projectile.Source, Is.SameAs(caster), "o projétil do segundo tiro deveria receber o Setup");
        }

        [Test]
        public void SpawnVfx_SpawnerReturnsNull_WarnsWithoutException()
        {
            NullSpawner spawner = CreateNullSpawner(nullCalls: int.MaxValue);
            GameObject prefab = _spawn.CreatePrefab("Vfx_Test");
            var action = new SpawnVFXEffectAction
            {
                Prefab = prefab,
                AttachPointName = string.Empty,
                DestroyOnRemoval = false,
                Spawner = spawner,
            };
            AbilitySystemComponent target = _gas.CreateAsc();
            LogAssert.Expect(LogType.Warning, SpawnFailedWarning(spawner, prefab));

            Assert.DoesNotThrow(() => action.Execute(target, target, null));
        }

        [Test]
        public void SpawnVfx_SpawnerReturnsNull_RemovalReleasesNothing()
        {
            NullSpawner spawner = CreateNullSpawner(nullCalls: int.MaxValue);
            GameObject prefab = _spawn.CreatePrefab("Vfx_Test");
            var action = new SpawnVFXEffectAction
            {
                Prefab = prefab,
                AttachPointName = string.Empty,
                DestroyOnRemoval = true,
                Spawner = spawner,
            };
            GameplayEffect effect = _gas.CreateDurationEffect(2f);
            effect.OnApplication = new List<EffectAction> { action };
            AbilitySystemComponent target = _gas.CreateAsc();
            LogAssert.Expect(LogType.Warning, SpawnFailedWarning(spawner, prefab));
            ActiveEffectInstance instance = target.ApplyEffect(effect);

            target.RemoveEffect(instance);

            Assert.That(spawner.ReleaseCalls, Is.EqualTo(0), "sem instância criada, a remoção não deveria devolver nada");
        }

        private RecordingSpawner CreateRecordingSpawner()
        {
            var spawner = _spawn.Track(ScriptableObject.CreateInstance<RecordingSpawner>());
            spawner.Factory = _spawn;
            return spawner;
        }

        private NullSpawner CreateNullSpawner(int nullCalls)
        {
            var spawner = _spawn.Track(ScriptableObject.CreateInstance<NullSpawner>());
            spawner.name = "Null_Spawner";
            spawner.Factory = _spawn;
            spawner.NullCalls = nullCalls;
            return spawner;
        }

        /// <summary>Aviso de spawn sem instância, que cita o spawner e o prefab.</summary>
        private static Regex SpawnFailedWarning(SpawnerAsset spawner, GameObject prefab)
        {
            return new Regex($"{Regex.Escape(spawner.name)}.*{Regex.Escape(prefab.name)}");
        }

        /// <summary>
        /// Componentes <typeparamref name="T"/> já na cena antes da ação: o prefab do teste e o que um teste anterior deixou
        /// (o <c>Destroy</c> do <c>TearDown</c> só vale no fim do quadro, e um <c>[Test]</c> roda no mesmo quadro do anterior).
        /// </summary>
        private static HashSet<T> FindExisting<T>() where T : Component
        {
            return new HashSet<T>(Object.FindObjectsByType<T>(FindObjectsSortMode.None));
        }

        /// <summary>Componentes <typeparamref name="T"/> criados depois de <paramref name="existing"/>, registrados para o <c>Dispose</c>.</summary>
        private List<T> TrackSpawned<T>(HashSet<T> existing) where T : Component
        {
            var spawned = new List<T>();
            foreach (T component in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (existing.Contains(component)) continue;
                spawned.Add(component);
                _spawn.Track(component.gameObject);
            }

            return spawned;
        }

        private static void RunToEnd(IEnumerator routine)
        {
            while (routine.MoveNext()) { }
        }

        /// <summary>Spawner que cria por <c>Instantiate</c> e anota o que criou e o que recebeu de volta.</summary>
        private sealed class RecordingSpawner : SpawnerAsset
        {
            public readonly List<GameObject> Spawned = new List<GameObject>();
            public readonly List<GameObject> Released = new List<GameObject>();
            public SpawnFactory Factory;

            public override GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            {
                GameObject instance = Factory.Track(Instantiate(prefab, position, rotation, parent));
                Spawned.Add(instance);
                return instance;
            }

            public override void Release(GameObject instance)
            {
                Released.Add(instance);
            }
        }

        /// <summary>
        /// Spawner que devolve <c>null</c> nas primeiras <see cref="NullCalls"/> chamadas, como um pool vazio que não cresce,
        /// e depois cria por <c>Instantiate</c>. Conta as chamadas de <c>Release</c>.
        /// </summary>
        private sealed class NullSpawner : SpawnerAsset
        {
            public readonly List<GameObject> Spawned = new List<GameObject>();
            public SpawnFactory Factory;
            public int NullCalls;
            public int SpawnCalls;
            public int ReleaseCalls;

            public override GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            {
                SpawnCalls++;
                if (SpawnCalls <= NullCalls) return null;

                GameObject instance = Factory.Track(Instantiate(prefab, position, rotation, parent));
                Spawned.Add(instance);
                return instance;
            }

            public override void Release(GameObject instance)
            {
                ReleaseCalls++;
            }
        }
    }
}
