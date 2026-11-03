using NUnit.Framework;
// --8<-- [start:using-core]
using SpellBook.Core;
// --8<-- [end:using-core]
// --8<-- [start:using-pooling]
using SpellBook.Pooling;
// --8<-- [end:using-pooling]
using SpellBook.Testing;
using UnityEditor;
// --8<-- [start:using-unityengine]
using UnityEngine;
// --8<-- [end:using-unityengine]

namespace SpellBook.Pooling.Tests.Examples
{
    /// <summary>
    /// Exemplos da página <c>modulos/pooling.md</c> do manual. Cada trecho entre os marcadores <c>--8&lt;--</c> aparece
    /// na página, e os testes conferem o que o texto em volta diz dele (guia do manual, parte 9).
    /// </summary>
    public class PoolingManualExamples
    {
        private const string PoolsRootName = "[SpellBook Pools]";

        private SpawnFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = new SpawnFactory();
        }

        [TearDown]
        public void TearDown()
        {
            GameObject root = GameObject.Find(PoolsRootName);
            if (root != null) Object.DestroyImmediate(root);

            _factory.Dispose();
        }

        [Test]
        public void OrDefault_EmptyField_SpawnsWithDefaultSpawner()
        {
            var efeito = _factory.CreatePrefab("Efeito_Test").AddComponent<EfeitoDeImpacto>();
            GameObject prefab = _factory.CreatePrefab();
            var position = new Vector3(1f, 2f, 3f);

            GameObject vfx = _factory.Track(efeito.Tocar(prefab, position, Quaternion.identity));

            Assert.That(SpawnerAsset.OrDefault(null), Is.SameAs(SpawnerAsset.Default));
            Assert.That(vfx, Is.Not.Null);
            Assert.That(vfx, Is.Not.SameAs(prefab));
            Assert.That(vfx.transform.position, Is.EqualTo(position));
        }

        [Test]
        public void CountingSpawnerAsset_Spawn_CountsAliveInstance()
        {
            var counting = _factory.Track(ScriptableObject.CreateInstance<CountingSpawnerAsset>());
            GameObject prefab = _factory.CreatePrefab();

            GameObject instance = _factory.Track(counting.Spawn(prefab, Vector3.one, Quaternion.identity));
            counting.Release(null);

            Assert.That(instance, Is.Not.SameAs(prefab));
            Assert.That(instance.transform.position, Is.EqualTo(Vector3.one));
            Assert.That(counting.Alive, Is.EqualTo(1));
        }

        [Test]
        public void ReleaseWhenParticlesStop_SetsStopActionToCallback()
        {
            SpawnerAsset spawner = _factory.CreateInstantiateSpawner();
            GameObject prefab = _factory.CreateParticlePrefab();
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;

            // --8<-- [start:release-when-particles-stop]
            SpawnerAsset vfxSpawner = SpawnerAsset.OrDefault(spawner);
            GameObject vfx = vfxSpawner.Spawn(prefab, position, rotation);
            vfxSpawner.ReleaseWhenParticlesStop(vfx);
            // --8<-- [end:release-when-particles-stop]

            _factory.Track(vfx);
            Assert.That(vfxSpawner, Is.SameAs(spawner));
            Assert.That(vfx.GetComponent<ParticleSystem>().main.stopAction, Is.EqualTo(ParticleSystemStopAction.Callback));
        }

        [Test]
        public void Arma_SpawnThenRelease_ReturnsInstanceToPool()
        {
            PooledSpawnerAsset pooled = CreatePooledSpawner(initialSize: 1);
            var arma = _factory.CreatePrefab("Arma_Test").AddComponent<Arma>();
            SetSpawner(arma, pooled);
            GameObject prefab = _factory.CreatePrefab();

            GameObject bullet = arma.Disparar(prefab, Vector3.zero, Quaternion.identity);
            GameObject next = _factory.Track(pooled.Spawn(prefab, Vector3.one, Quaternion.identity));

            Assert.That(bullet, Is.Not.Null);
            Assert.That(next, Is.SameAs(bullet));
            Assert.That(next.activeSelf, Is.True);
        }

        [Test]
        public void Bullet_IsPoolable_AndReusedByPooledSpawner()
        {
            PooledSpawnerAsset pooled = CreatePooledSpawner(initialSize: 1);
            GameObject prefab = _factory.CreatePrefab();
            prefab.AddComponent<Bullet>();

            GameObject first = _factory.Track(pooled.Spawn(prefab, Vector3.zero, Quaternion.identity));
            pooled.Release(first);
            GameObject second = _factory.Track(pooled.Spawn(prefab, Vector3.zero, Quaternion.identity));

            Assert.That(typeof(IPoolable).IsAssignableFrom(typeof(Bullet)), Is.True);
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.GetComponent<Bullet>(), Is.Not.Null);
        }

        private PooledSpawnerAsset CreatePooledSpawner(int initialSize)
        {
            var pooled = _factory.Track(ScriptableObject.CreateInstance<PooledSpawnerAsset>());
            var serialized = new SerializedObject(pooled);
            serialized.FindProperty("initialSize").intValue = initialSize;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return pooled;
        }

        private static void SetSpawner(Arma arma, SpawnerAsset spawner)
        {
            var serialized = new SerializedObject(arma);
            serialized.FindProperty("spawner").objectReferenceValue = spawner;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public sealed class EfeitoDeImpacto : MonoBehaviour
    {
        // --8<-- [start:campo-spawner]

        [SerializeField] private SpawnerAsset spawner;
        // --8<-- [end:campo-spawner]

        public GameObject Tocar(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            // --8<-- [start:or-default]

            GameObject vfx = SpawnerAsset.OrDefault(spawner).Spawn(prefab, position, rotation);
            // --8<-- [end:or-default]
            return vfx;
        }
    }

    // --8<-- [start:counting-spawner]

    [CreateAssetMenu(menuName = "MeuJogo/Spawner com contagem")]
    public class CountingSpawnerAsset : SpawnerAsset
    {
        public int Alive { get; private set; }

        public override GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            Alive++;
            return Instantiate(prefab, position, rotation, parent);
        }

        public override void Release(GameObject instance)
        {
            if (instance == null) return;
            Alive--;
            Destroy(instance);
        }
    }
    // --8<-- [end:counting-spawner]

    public sealed class Arma : MonoBehaviour
    {
        // --8<-- [start:campo-pooled-spawner]

        [SerializeField] private SpawnerAsset spawner; // arraste um PooledSpawnerAsset aqui
        // --8<-- [end:campo-pooled-spawner]

        public GameObject Disparar(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            // --8<-- [start:pooled-spawn-release]

            GameObject bullet = spawner.Spawn(prefab, position, rotation);
            // ...quando o objeto não serve mais:
            spawner.Release(bullet);
            // --8<-- [end:pooled-spawn-release]
            return bullet;
        }
    }

    // --8<-- [start:bullet]

    public class Bullet : MonoBehaviour, IPoolable
    {
        public void OnSpawnFromPool() { /* reinicia vida, velocidade... */ }
        public void OnReturnToPool() { /* para efeitos, limpa alvos... */ }
    }
    // --8<-- [end:bullet]
}
