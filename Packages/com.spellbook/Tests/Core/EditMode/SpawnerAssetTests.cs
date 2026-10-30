using System;
using NUnit.Framework;
using SpellBook.Testing;
using UnityEngine;

namespace SpellBook.Core.Tests
{
    /// <summary><c>SpawnerAsset</c> e a implementação padrão por Instantiate (ADR 0015; CA27).</summary>
    public class SpawnerAssetTests
    {
        private SpawnFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = new SpawnFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
        }

        [Test]
        public void Instantiate_SpawnsAtPositionAndRotation()
        {
            InstantiateSpawnerAsset spawner = _factory.CreateInstantiateSpawner();
            GameObject prefab = _factory.CreatePrefab();
            var position = new Vector3(1f, 2f, 3f);
            Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject instance = _factory.Track(spawner.Spawn(prefab, position, rotation));

            Assert.That(instance, Is.Not.Null);
            Assert.That(instance, Is.Not.SameAs(prefab));
            Assert.That(instance.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(instance.transform.rotation, rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void Instantiate_SpawnsUnderParent()
        {
            InstantiateSpawnerAsset spawner = _factory.CreateInstantiateSpawner();
            GameObject prefab = _factory.CreatePrefab();
            Transform parent = _factory.CreatePrefab("Parent_Test").transform;

            GameObject instance = spawner.Spawn(prefab, Vector3.zero, Quaternion.identity, parent);

            Assert.That(instance.transform.parent, Is.SameAs(parent));
        }

        [Test]
        public void Instantiate_ReleaseDestroys()
        {
            InstantiateSpawnerAsset spawner = _factory.CreateInstantiateSpawner();
            GameObject instance = _factory.Track(spawner.Spawn(_factory.CreatePrefab(), Vector3.zero, Quaternion.identity));

            spawner.Release(instance);

            Assert.That(instance == null, Is.True, "A instância devolvida deveria ter sido destruída.");
        }

        [Test]
        public void Default_IsInstantiateSpawner()
        {
            SpawnerAsset result = SpawnerAsset.Default;

            Assert.That(result, Is.InstanceOf<InstantiateSpawnerAsset>());
            Assert.That(result, Is.SameAs(SpawnerAsset.Default));
            Assert.That(result.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
        }

        [Test]
        public void OrDefault_Null_ReturnsDefault()
        {
            SpawnerAsset result = SpawnerAsset.OrDefault(null);

            Assert.That(result, Is.SameAs(SpawnerAsset.Default));
        }

        [Test]
        public void OrDefault_Configured_ReturnsConfigured()
        {
            InstantiateSpawnerAsset spawner = _factory.CreateInstantiateSpawner();

            SpawnerAsset result = SpawnerAsset.OrDefault(spawner);

            Assert.That(result, Is.SameAs(spawner));
        }

        [Test]
        public void ReleaseWhenParticlesStop_SetsCallbackStopAction()
        {
            InstantiateSpawnerAsset spawner = _factory.CreateInstantiateSpawner();
            GameObject instance = _factory.Track(
                spawner.Spawn(_factory.CreateParticlePrefab(), Vector3.zero, Quaternion.identity));

            spawner.ReleaseWhenParticlesStop(instance);

            Assert.That(instance.GetComponent<ParticleSystem>().main.stopAction,
                Is.EqualTo(ParticleSystemStopAction.Callback));
        }

        [Test]
        public void ReleaseWhenParticlesStop_WithoutParticleSystem_Throws()
        {
            InstantiateSpawnerAsset spawner = _factory.CreateInstantiateSpawner();
            GameObject instance = _factory.Track(spawner.Spawn(_factory.CreatePrefab(), Vector3.zero, Quaternion.identity));

            Assert.Throws<ArgumentException>(() => spawner.ReleaseWhenParticlesStop(instance));
        }
    }
}
