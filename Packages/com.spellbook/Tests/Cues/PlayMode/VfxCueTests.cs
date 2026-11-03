using System.Collections;
using NUnit.Framework;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.Cues.Tests
{
    /// <summary>VFX do Cues: fim por partículas e por duração, escala pela magnitude e anexo ao alvo (CA4, CA5).</summary>
    public class VfxCueTests
    {
        private CuesFactory _cues;
        private SpawnFactory _spawns;
        private TimeFactory _times;
        private ManualTimeSource _time;
        private RecordingSpawnerAsset _spawner;

        [SetUp]
        public void SetUp()
        {
            _cues = new CuesFactory();
            _spawns = new SpawnFactory();
            _times = new TimeFactory();
            _time = _times.CreateManual();
            _spawner = _cues.CreateSpawner();
        }

        [TearDown]
        public void TearDown()
        {
            _cues.Dispose();
            _spawns.Dispose();
            _times.Dispose();
        }

        [Test]
        public void Vfx_ParticlesStop_ReturnsToSpawner()
        {
            GameplayTag tag = _cues.CreateTag("Cue.Hit");
            CueRouter router = Router(_cues.CreateCue(tag, CueKind.Burst, Vfx(ParticlePrefab(), CueEnd.ParticlesStop)));

            CueHandle handle = router.Add(tag, CueParameters.At(Vector3.zero));
            var particles = _spawner.Live[0].GetComponent<ParticleSystem>();
            particles.Emit(5);
            router.Tick(10f);

            Assert.That(particles.isPlaying, Is.True, "o item toca as partículas");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(0), "partículas vivas não voltam, nem com o tempo");
            Assert.That(router.IsActive(handle), Is.True);

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            router.Tick(0f);

            Assert.That(_spawner.ReleaseCount, Is.EqualTo(1));
            Assert.That(_spawner.Live, Is.Empty);
            Assert.That(router.IsActive(handle), Is.False);
        }

        [UnityTest]
        public IEnumerator Vfx_FixedDuration_Returns_PausedDoesNot()
        {
            GameplayTag tag = _cues.CreateTag("Cue.Hit");
            VfxCueItem item = Vfx(_spawns.CreatePrefab(), CueEnd.FixedDuration);
            item.Duration = 1f;
            CueRouter router = Router(_cues.CreateCue(tag, CueKind.Burst, item));
            yield return null;

            router.Execute(tag, CueParameters.At(Vector3.zero));
            _time.Advance(0.6f);
            yield return null;
            for (int frame = 0; frame < 5; frame++)
            {
                yield return null; // fonte parada: pausa
            }

            Assert.That(_spawner.SpawnCount, Is.EqualTo(1));
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(0));

            _time.Advance(0.5f);
            yield return null;

            Assert.That(_spawner.ReleaseCount, Is.EqualTo(1));
            Assert.That(router.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Vfx_ScaleByMagnitude_DefaultWhenNone()
        {
            GameplayTag scaled = _cues.CreateTag("Cue.Scaled");
            GameplayTag plain = _cues.CreateTag("Cue.Plain");
            GameObject prefab = _spawns.CreatePrefab();
            prefab.transform.localScale = new Vector3(2f, 2f, 2f);
            VfxCueItem item = Vfx(prefab, CueEnd.OnRemove);
            item.ScaleByMagnitude = true;
            item.ScaleRange = new Vector2(0.5f, 1.5f);
            CueRouter router = Router(
                _cues.CreateCue(scaled, CueKind.Looping, item),
                _cues.CreateCue(plain, CueKind.Looping, Vfx(prefab, CueEnd.OnRemove)));

            router.Add(scaled, CueParameters.At(Vector3.zero).WithMagnitude(50f, 0.5f));
            router.Add(scaled, CueParameters.At(Vector3.zero));
            CueHandle reused = router.Add(scaled, CueParameters.At(Vector3.zero).WithMagnitude(0f, 0f));
            router.Remove(reused);
            router.Add(plain, CueParameters.At(Vector3.zero));

            Assert.That(_spawner.Live[0].transform.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)), "0,5 na faixa 0,5–1,5");
            Assert.That(_spawner.Live[1].transform.localScale, Is.EqualTo(new Vector3(3f, 3f, 3f)),
                "sem magnitude, vale o padrão da definição (1)");
            Assert.That(_spawner.Live[2].transform.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)),
                "o objeto reutilizado do pool volta à escala do prefab");
        }

        [Test]
        public void LoopingVfx_Remove_StopsThenReturns()
        {
            GameplayTag tag = _cues.CreateTag("Cue.Aura");
            CueRouter router = Router(_cues.CreateCue(tag, CueKind.Looping,
                Vfx(ParticlePrefab(), CueEnd.ParticlesStop), Vfx(_spawns.CreatePrefab(), CueEnd.OnRemove)));

            CueHandle handle = router.Add(tag, CueParameters.At(Vector3.zero));
            var particles = _spawner.Live[0].GetComponent<ParticleSystem>();
            particles.Emit(5);
            bool removed = router.Remove(handle);
            router.Tick(0.1f);

            Assert.That(removed, Is.True);
            Assert.That(particles.isEmitting, Is.False, "Remove para a emissão");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(1), "o OnRemove volta na hora; as partículas esperam acabar");
            Assert.That(router.IsActive(handle), Is.False);
            Assert.That(router.ActiveCount, Is.EqualTo(0));

            particles.Clear(true);
            router.Tick(0.1f);

            Assert.That(_spawner.ReleaseCount, Is.EqualTo(2));
            Assert.That(_spawner.Live, Is.Empty);
        }

        [Test]
        public void AttachToTarget_CopiesRotation_NoParenting()
        {
            GameplayTag tag = _cues.CreateTag("Cue.Aura");
            VfxCueItem item = Vfx(_spawns.CreatePrefab(), CueEnd.OnRemove);
            item.Placement = CuePlacement.AttachToTarget;
            item.Offset = new Vector3(0f, 0f, 1f);
            CueRouter router = Router(_cues.CreateCue(tag, CueKind.Looping, item));
            GameObject target = _cues.CreateObject("Target");

            router.Add(tag, CueParameters.On(target.transform));
            Transform instance = _spawner.Live[0].transform;
            target.transform.SetPositionAndRotation(new Vector3(5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            router.Tick(0.1f);

            Assert.That(Vector3.Distance(instance.position, new Vector3(6f, 0f, 0f)), Is.LessThan(1e-4f),
                "deslocamento girado com o alvo");
            Assert.That(Quaternion.Angle(instance.rotation, target.transform.rotation), Is.LessThan(0.01f));
            Assert.That(instance.parent, Is.Null);
        }

        [Test]
        public void AttachToTarget_AppliesExtraRotationOverTargetEachTick()
        {
            GameplayTag tag = _cues.CreateTag("Cue.Aura");
            VfxCueItem item = Vfx(_spawns.CreatePrefab(), CueEnd.OnRemove);
            item.Placement = CuePlacement.AttachToTarget;
            item.Offset = new Vector3(0f, 0f, 1f);
            item.Rotation = new Vector3(90f, 0f, 0f);
            CueRouter router = Router(_cues.CreateCue(tag, CueKind.Looping, item));
            GameObject target = _cues.CreateObject("Target");

            router.Add(tag, CueParameters.On(target.transform));
            Transform instance = _spawner.Live[0].transform;
            Quaternion extra = Quaternion.Euler(90f, 0f, 0f);
            Assert.That(Quaternion.Angle(instance.rotation, extra), Is.LessThan(0.01f), "rotação extra ao nascer");

            target.transform.SetPositionAndRotation(new Vector3(5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            router.Tick(0.1f);

            Assert.That(Quaternion.Angle(instance.rotation, target.transform.rotation * extra), Is.LessThan(0.01f),
                "rotação extra sobre a do alvo, a cada tick");
            Assert.That(Vector3.Distance(instance.position, new Vector3(6f, 0f, 0f)), Is.LessThan(1e-4f),
                "o deslocamento gira só com o alvo");
        }

        private CueRouter Router(params CueDefinition[] cues)
        {
            return _cues.CreateRouter(_time, _spawner, null, cues);
        }

        private static VfxCueItem Vfx(GameObject prefab, CueEnd end)
        {
            return new VfxCueItem { Prefab = prefab, End = end };
        }

        /// <summary>Prefab com partículas que vivem 5 s, para o teste decidir quando acabam.</summary>
        private GameObject ParticlePrefab()
        {
            GameObject prefab = _spawns.CreateParticlePrefab();
            ParticleSystem.MainModule main = prefab.GetComponent<ParticleSystem>().main;
            main.startLifetime = 5f;
            return prefab;
        }
    }
}
