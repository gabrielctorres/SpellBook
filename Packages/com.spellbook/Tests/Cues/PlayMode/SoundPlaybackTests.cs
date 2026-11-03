using System.Collections;
using NUnit.Framework;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.Cues.Tests
{
    /// <summary>Som real no prefab de áudio do pacote, devolvido ao pool no fim do clipe (CA6, N6).</summary>
    public class SoundPlaybackTests
    {
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

        [UnityTest]
        public IEnumerator RealAudio_PlaysAndReturns()
        {
            GameObject prefab = CuesFactory.LoadPackageAudioPrefab();
            Assert.That(prefab, Is.Not.Null, "o pacote entrega o prefab de áudio");
            GameplayTag tag = _cues.CreateTag("Cue.Hit");
            AudioClip clip = _cues.CreateClip("Tone", 0.25f, tone: true);
            CueDefinition cue = _cues.CreateCue(tag, CueKind.Burst);
            _cues.SetSounds(cue, SoundSelection.RandomOne, new SoundCueItem { Clip = clip });
            CueRouter router = _cues.CreateRouter(_time, _spawner, null, cue);
            _cues.SetAudioPrefab(router, prefab);
            yield return null;

            router.Execute(tag, CueParameters.At(Vector3.zero));
            AudioSource source = _spawner.Live[0].GetComponent<AudioSource>();
            yield return null;

            Assert.That(source.clip, Is.SameAs(clip));
            Assert.That(source.isPlaying, Is.True, "toca no AudioSource do objeto do spawner");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(0));

            _time.Advance(0.3f);
            yield return null;

            Assert.That(_spawner.ReleaseCount, Is.EqualTo(1), "volta ao pool no fim do clipe");
            Assert.That(_spawner.Live, Is.Empty);
        }
    }
}
