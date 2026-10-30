using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using UnityEngine;

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

            Assert.DoesNotThrow(() => RunToEnd(action.Execute(context)));

            List<Projectile> spawned = TrackSpawned<Projectile>(prefab);
            Assert.That(spawned, Has.Count.EqualTo(1), "o projétil deveria ter sido criado pelo spawner padrão");
        }

        [Test]
        public void PlayCue_NoSpawner_NoPoolManager_NoException()
        {
            GameObject prefab = _spawn.CreateParticlePrefab("Cue_Test");
            var position = new Vector3(3f, 0f, 4f);
            var action = new PlayCueAction { VFX_Prefab = prefab, TargetSource = AbilityTargetSource.Location };
            AbilityContext context = GasFactory.CreateAbilityContext(_gas.CreateAsc(), position);

            Assert.DoesNotThrow(() => RunToEnd(action.Execute(context)));

            List<ParticleSystem> spawned = TrackSpawned<ParticleSystem>(prefab);
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

        private RecordingSpawner CreateRecordingSpawner()
        {
            var spawner = _spawn.Track(ScriptableObject.CreateInstance<RecordingSpawner>());
            spawner.Factory = _spawn;
            return spawner;
        }

        /// <summary>Instâncias de <paramref name="prefab"/> criadas pelo teste, registradas para o <c>Dispose</c>.</summary>
        private List<T> TrackSpawned<T>(GameObject prefab) where T : Component
        {
            var spawned = new List<T>();
            foreach (T component in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (component.gameObject == prefab) continue;
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
    }
}
