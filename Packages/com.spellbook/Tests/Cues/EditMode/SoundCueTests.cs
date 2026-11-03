using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEngine;

namespace SpellBook.Cues.Tests
{
    /// <summary>Som do Cues em pool: sorteio, volume e altura, fim do clipe e loop (CA1, CA4, CA6, CA12).</summary>
    public class SoundCueTests
    {
        private const int Seed = 12345;

        private CuesFactory _cues;
        private TimeFactory _times;
        private ManualTimeSource _time;
        private RecordingSpawnerAsset _spawner;

        [SetUp]
        public void SetUp()
        {
            _cues = new CuesFactory();
            _times = new TimeFactory();
            _time = _times.CreateManual();
            _spawner = _cues.CreateSpawner();
        }

        [TearDown]
        public void TearDown()
        {
            _cues.Dispose();
            _times.Dispose();
        }

        [Test]
        public void RandomOne_PicksBySeed_All_PlaysAll()
        {
            GameplayTag one = _cues.CreateTag("Cue.One");
            GameplayTag all = _cues.CreateTag("Cue.All");
            AudioClip[] clips = { _cues.CreateClip("A", 1f), _cues.CreateClip("B", 1f), _cues.CreateClip("C", 1f) };
            var vfx = new VfxCueItem { Prefab = _cues.CreateObject("Vfx_Test"), End = CueEnd.FixedDuration };
            CueDefinition oneCue = _cues.CreateCue(one, CueKind.Burst, vfx);
            _cues.SetSounds(oneCue, SoundSelection.RandomOne, Sound(clips[0]), Sound(clips[1]), Sound(clips[2]));
            CueDefinition allCue = _cues.CreateCue(all, CueKind.Burst);
            _cues.SetSounds(allCue, SoundSelection.All, Sound(clips[0]), Sound(clips[1]), Sound(clips[2]));
            CueRouter router = Router(oneCue, allCue);
            router.SetRandomSource(new SeededRandomSource(Seed));
            var expected = new SeededRandomSource(Seed);

            var picked = new List<AudioClip>();
            var predicted = new List<AudioClip>();
            for (int i = 0; i < 6; i++)
            {
                router.Execute(one, CueParameters.At(Vector3.zero));
                Assert.That(_spawner.Live[2 * i].GetComponent<AudioSource>(), Is.Null, "o VFX toca antes do som");
                picked.Add(Source(2 * i + 1).clip);
                predicted.Add(clips[expected.Range(3)]);
                expected.Value(); // volume
                expected.Value(); // altura
            }

            Assert.That(picked, Is.EqualTo(predicted), "um som por vez, sorteado pela fonte injetada");
            Assert.That(new HashSet<AudioClip>(picked).Count, Is.GreaterThan(1), "o sorteio não fica num som só");

            int before = _spawner.Live.Count;
            router.Execute(all, CueParameters.At(Vector3.zero));

            Assert.That(_spawner.Live.Count, Is.EqualTo(before + 3));
            Assert.That(Source(before).clip, Is.SameAs(clips[0]));
            Assert.That(Source(before + 1).clip, Is.SameAs(clips[1]));
            Assert.That(Source(before + 2).clip, Is.SameAs(clips[2]));
        }

        [Test]
        public void VolumePitch_FromRangeBySeed_MagnitudeScalesVolume()
        {
            GameplayTag tag = _cues.CreateTag("Cue.Hit");
            SoundCueItem sound = Sound(_cues.CreateClip("A", 1f));
            sound.Volume = new Vector2(0.2f, 0.6f);
            sound.Pitch = new Vector2(0.5f, 1.5f);
            sound.SpatialBlend = 0.7f;
            sound.VolumeByMagnitude = true;
            CueDefinition cue = _cues.CreateCue(tag, CueKind.Burst);
            _cues.SetSounds(cue, SoundSelection.RandomOne, sound);
            CueRouter router = Router(cue);
            router.SetRandomSource(new SeededRandomSource(Seed));
            var expected = new SeededRandomSource(Seed);

            router.Execute(tag, CueParameters.At(Vector3.zero).WithMagnitude(10f, 0.5f));
            router.Execute(tag, CueParameters.At(Vector3.zero));
            float volume1 = Mathf.Lerp(0.2f, 0.6f, expected.Value());
            float pitch1 = Mathf.Lerp(0.5f, 1.5f, expected.Value());
            float volume2 = Mathf.Lerp(0.2f, 0.6f, expected.Value());
            float pitch2 = Mathf.Lerp(0.5f, 1.5f, expected.Value());

            Assert.That(Source(0).volume, Is.EqualTo(volume1 * 0.5f).Within(1e-5f), "volume sorteado vezes a magnitude");
            Assert.That(Source(0).pitch, Is.EqualTo(pitch1).Within(1e-5f));
            Assert.That(Source(1).volume, Is.EqualTo(volume2).Within(1e-5f), "sem magnitude, vale o padrão da definição (1)");
            Assert.That(Source(1).pitch, Is.EqualTo(pitch2).Within(1e-5f));
            Assert.That(Source(0).spatialBlend, Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(Source(0).loop, Is.False);
        }

        [Test]
        public void Sound_ReturnsAtClipEndOverPitch_ByTimeSource()
        {
            GameplayTag tag = _cues.CreateTag("Cue.Hit");
            SoundCueItem fast = Sound(_cues.CreateClip("A", 1f));
            fast.Pitch = new Vector2(2f, 2f);
            SoundCueItem reversed = Sound(_cues.CreateClip("B", 1f));
            reversed.Pitch = new Vector2(-2f, -2f);
            CueDefinition cue = _cues.CreateCue(tag, CueKind.Burst);
            _cues.SetSounds(cue, SoundSelection.All, fast, reversed);
            CueRouter router = Router(cue);

            CueHandle handle = router.Add(tag, CueParameters.At(Vector3.zero));
            _time.Advance(0.4f);
            router.Update();
            router.Update(); // fonte parada: pausa

            Assert.That(_spawner.SpawnCount, Is.EqualTo(2));
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(0), "1 s de clipe a altura 2 dura 0,5 s");
            Assert.That(router.IsActive(handle), Is.True);

            _time.Advance(0.11f);
            router.Update();

            Assert.That(_spawner.ReleaseCount, Is.EqualTo(2), "a altura negativa conta pelo módulo");
            Assert.That(router.IsActive(handle), Is.False);
            Assert.That(router.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void TwoSounds_TwoObjects_LiveCountStable()
        {
            GameplayTag tag = _cues.CreateTag("Cue.Hit");
            CueDefinition cue = _cues.CreateCue(tag, CueKind.Burst);
            _cues.SetSounds(cue, SoundSelection.RandomOne, Sound(_cues.CreateClip("A", 1f)));
            CueRouter router = Router(cue);

            router.Execute(tag, CueParameters.At(Vector3.zero));
            router.Execute(tag, CueParameters.At(Vector3.zero));
            var first = new List<GameObject>(_spawner.Live);
            router.Tick(1.1f);

            Assert.That(first.Count, Is.EqualTo(2));
            Assert.That(first[0], Is.Not.SameAs(first[1]), "dois sons ao mesmo tempo, dois objetos");
            Assert.That(_spawner.Live, Is.Empty, "voltam ao fim do clipe");

            router.Execute(tag, CueParameters.At(Vector3.zero));
            router.Execute(tag, CueParameters.At(Vector3.zero));

            Assert.That(_spawner.Live, Is.EquivalentTo(first), "os mesmos objetos voltam do pool");
            router.Tick(1.1f);
            Assert.That(_spawner.Live, Is.Empty);
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(4));
        }

        [Test]
        public void LoopingSound_UntilRemove()
        {
            GameplayTag tag = _cues.CreateTag("Cue.Aura");
            SoundCueItem sound = Sound(_cues.CreateClip("A", 1f));
            sound.Loop = true;
            CueDefinition cue = _cues.CreateCue(tag, CueKind.Looping);
            _cues.SetSounds(cue, SoundSelection.RandomOne, sound);
            CueRouter router = Router(cue);

            CueHandle handle = router.Add(tag, CueParameters.At(Vector3.zero));
            router.Tick(100f);

            Assert.That(_spawner.Live.Count, Is.EqualTo(1), "com loop, não volta no fim do clipe");
            Assert.That(Source(0).loop, Is.True);
            Assert.That(router.IsActive(handle), Is.True);

            router.Remove(handle);

            Assert.That(_spawner.Live, Is.Empty);
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(1));
        }

        private CueRouter Router(params CueDefinition[] cues)
        {
            CueRouter router = _cues.CreateRouter(_time, _spawner, null, cues);
            _cues.SetAudioPrefab(router, _cues.CreateAudioPrefab());
            return router;
        }

        private AudioSource Source(int liveIndex)
        {
            return _spawner.Live[liveIndex].GetComponent<AudioSource>();
        }

        private static SoundCueItem Sound(AudioClip clip)
        {
            return new SoundCueItem { Clip = clip };
        }
    }
}
