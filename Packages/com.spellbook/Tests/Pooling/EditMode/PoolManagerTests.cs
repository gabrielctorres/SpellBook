using NUnit.Framework;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellBook.Pooling.Tests
{
    /// <summary><c>PoolManager.Get</c> de um prefab sem configuração prévia: pool dinâmico, sem criar <c>PoolConfiguration</c>.</summary>
    public class PoolManagerTests
    {
        private SpawnFactory _factory;
        private int _sceneRootCountBefore;

        [SetUp]
        public void SetUp()
        {
            _factory = new SpawnFactory();
            _sceneRootCountBefore = SceneManager.GetActiveScene().rootCount;
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();

            Assert.That(SceneManager.GetActiveScene().rootCount, Is.EqualTo(_sceneRootCountBefore),
                "O teste deixou objetos na cena; rastreie o manager e toda instância do Get com _factory.Track.");
        }

        [Test]
        public void PoolManager_GetWithoutConfig_CreatesNoPoolConfiguration()
        {
            PoolManager manager = CreatePoolManager();
            GameObject prefab = _factory.CreatePrefab();
            int before = Resources.FindObjectsOfTypeAll<PoolConfiguration>().Length;

            _factory.Track(manager.Get(prefab, Vector3.zero, Quaternion.identity));

            Assert.That(Resources.FindObjectsOfTypeAll<PoolConfiguration>().Length, Is.EqualTo(before));
        }

        [Test]
        public void PoolManager_GetWithoutConfig_UsesDefaults()
        {
            const int defaultInitialSize = 5;
            PoolManager manager = CreatePoolManager();
            GameObject prefab = _factory.CreatePrefab();

            _factory.Track(manager.Get(prefab, Vector3.zero, Quaternion.identity));
            ComponentPooler pooler = manager.GetPooler(prefab);
            Assert.That(CountIdleInstances(pooler), Is.EqualTo(defaultInitialSize - 1), "O pool dinâmico deveria nascer com 5 instâncias.");

            for (int i = 1; i < defaultInitialSize; i++)
            {
                _factory.Track(manager.Get(prefab, Vector3.zero, Quaternion.identity));
            }

            Assert.That(CountIdleInstances(pooler), Is.Zero);
            GameObject extra = _factory.Track(manager.Get(prefab, Vector3.zero, Quaternion.identity));
            Assert.That(extra, Is.Not.Null, "O pool dinâmico deveria crescer quando esvazia.");
        }

        private PoolManager CreatePoolManager()
        {
            // No modo de edição, o AddComponent não chama o Awake: o manager não vira Instance nem pré-aquece pools.
            GameObject host = _factory.Track(new GameObject("PoolManager_Test"));
            return host.AddComponent<PoolManager>();
        }

        private static int CountIdleInstances(ComponentPooler pooler)
        {
            return pooler.GetComponentsInChildren<PoolableObject>(true).Length;
        }
    }
}
