using System.Collections;
using NUnit.Framework;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.Core.Tests
{
    /// <summary><c>SpawnerAsset.ReleaseWhenParticlesStop</c> devolve a instância quando a partícula para (ADR 0015; R25).</summary>
    public class ParticleReleaseTests
    {
        private const int MaxFramesToRelease = 10;

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

        [UnityTest]
        public IEnumerator ReleaseWhenParticlesStop_ReleasesOnStop()
        {
            InstantiateSpawnerAsset spawner = _factory.CreateInstantiateSpawner();
            GameObject instance = _factory.Track(
                spawner.Spawn(_factory.CreateParticlePrefab(), Vector3.zero, Quaternion.identity));
            var particles = instance.GetComponent<ParticleSystem>();
            spawner.ReleaseWhenParticlesStop(instance);
            particles.Play();
            yield return null;
            Assert.That(instance != null, Is.True, "A instância não deveria ser devolvida enquanto a partícula toca.");

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (int frame = 0; frame < MaxFramesToRelease && instance != null; frame++)
            {
                yield return null;
            }

            Assert.That(instance == null, Is.True, "A instância deveria ser devolvida (destruída) quando a partícula parou.");
        }

        [UnityTest]
        public IEnumerator ReleaseWhenParticlesStop_NotCalled_KeepsInstance()
        {
            InstantiateSpawnerAsset spawner = _factory.CreateInstantiateSpawner();
            GameObject instance = _factory.Track(
                spawner.Spawn(_factory.CreateParticlePrefab(), Vector3.zero, Quaternion.identity));
            var particles = instance.GetComponent<ParticleSystem>();
            particles.Play();
            yield return null;

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (int frame = 0; frame < MaxFramesToRelease; frame++)
            {
                yield return null;
            }

            Assert.That(instance != null, Is.True, "Sem ReleaseWhenParticlesStop, a instância não é devolvida.");
        }
    }
}
