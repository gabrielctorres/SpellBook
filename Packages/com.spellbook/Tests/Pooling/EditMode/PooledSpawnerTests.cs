using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellBook.Pooling.Tests
{
    /// <summary><c>PooledSpawnerAsset</c>: pool sob demanda sem <c>PoolManager</c> na cena (ADR 0015; CA28, CA35).</summary>
    public class PooledSpawnerTests
    {
        private const string PoolsRootName = "[SpellBook Pools]";

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
            GameObject root = GameObject.Find(PoolsRootName);
            if (root != null) Object.DestroyImmediate(root);

            _factory.Dispose();

            Assert.That(SceneManager.GetActiveScene().rootCount, Is.EqualTo(_sceneRootCountBefore),
                "O teste deixou objetos na cena; rastreie toda instância do Spawn com _factory.Track.");
        }

        [Test]
        public void Pooled_NoPoolManager_SpawnsWithoutException()
        {
            PooledSpawnerAsset spawner = CreatePooledSpawner();
            GameObject prefab = _factory.CreatePrefab();
            var position = new Vector3(1f, 2f, 3f);
            Assert.That(Object.FindAnyObjectByType<PoolManager>(), Is.Null, "O teste precisa de uma cena sem PoolManager.");

            GameObject instance = null;
            Assert.DoesNotThrow(() => instance = _factory.Track(spawner.Spawn(prefab, position, Quaternion.identity)));

            Assert.That(instance, Is.Not.Null);
            Assert.That(instance, Is.Not.SameAs(prefab));
            Assert.That(instance.activeSelf, Is.True);
            Assert.That(instance.transform.position, Is.EqualTo(position));
        }

        [Test]
        public void Pooled_ReleaseThenSpawn_ReusesSameInstance()
        {
            PooledSpawnerAsset spawner = CreatePooledSpawner(initialSize: 1);
            GameObject prefab = _factory.CreatePrefab();
            GameObject first = _factory.Track(spawner.Spawn(prefab, Vector3.zero, Quaternion.identity));

            spawner.Release(first);
            GameObject second = _factory.Track(spawner.Spawn(prefab, Vector3.one, Quaternion.identity));

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.activeSelf, Is.True);
            Assert.That(second.transform.position, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void Pooled_ReleaseNonPooled_Destroys()
        {
            PooledSpawnerAsset spawner = CreatePooledSpawner();
            GameObject notPooled = _factory.CreatePrefab("NotPooled_Test");

            spawner.Release(notPooled);

            Assert.That(notPooled == null, Is.True, "Um objeto que não veio de um pool deveria ter sido destruído.");
        }

        [Test]
        public void Pooled_NegativeInitialSize_IsValidationError()
        {
            PooledSpawnerAsset spawner = CreatePooledSpawner(initialSize: -1);
            var result = new ValidationResult(spawner);

            spawner.Validate(result);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Messages, Has.Some.Matches<ValidationMessage>(
                m => m.Severity == ValidationSeverity.Error && m.PropertyPath == "initialSize"));
        }

        [Test]
        public void Pooled_DefaultValues_HaveNoValidationMessages()
        {
            PooledSpawnerAsset spawner = CreatePooledSpawner();
            var result = new ValidationResult(spawner);

            spawner.Validate(result);

            Assert.That(result.Messages, Is.Empty);
        }

        private PooledSpawnerAsset CreatePooledSpawner(int? initialSize = null)
        {
            var spawner = _factory.Track(ScriptableObject.CreateInstance<PooledSpawnerAsset>());
            spawner.name = "PooledSpawner_Test";

            if (initialSize.HasValue)
            {
                var serialized = new SerializedObject(spawner);
                serialized.FindProperty("initialSize").intValue = initialSize.Value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return spawner;
        }
    }
}
