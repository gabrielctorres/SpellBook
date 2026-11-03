using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.Cues.Tests
{
    /// <summary>Roteador: tocar, ligar, desligar, limite, tempo e âncora (CA1, CA2, CA5, CA7, CA9).</summary>
    public class CueRouterTests
    {
        private CuesFactory _cues;
        private TimeFactory _times;
        private ManualTimeSource _time;
        private RecordingSpawnerAsset _spawner;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _cues = new CuesFactory();
            _times = new TimeFactory();
            _time = _times.CreateManual();
            _spawner = _cues.CreateSpawner();
            _prefab = _cues.CreateObject("CuePrefab_Test");
        }

        [TearDown]
        public void TearDown()
        {
            _cues.Dispose();
            _times.Dispose();
        }

        [Test]
        public void Execute_PlaysAllItems_ReturnsTrue()
        {
            GameplayTag tag = _cues.CreateTag("Cue.Hit");
            CueRouter router = Router(_cues.CreateCue(tag, CueKind.Burst, Item(CueEnd.FixedDuration), Item(CueEnd.FixedDuration)));

            bool played = router.Execute(tag, CueParameters.At(new Vector3(1f, 2f, 3f)));

            Assert.That(played, Is.True);
            Assert.That(_spawner.SpawnCount, Is.EqualTo(2));
            Assert.That(_spawner.Live[0].transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(router.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void UnknownTag_NoException_ReturnsFalse_WarnsOnce()
        {
            GameplayTag known = _cues.CreateTag("Cue.Hit");
            GameplayTag unknown = _cues.CreateTag("Cue.Other");
            CueRouter router = Router(_cues.CreateCue(known, CueKind.Burst, Item(CueEnd.FixedDuration)));
            LogAssert.Expect(LogType.Warning, new Regex("Cue.Other"));

            bool first = router.Execute(unknown, CueParameters.At(Vector3.zero));
            bool second = router.Execute(unknown, CueParameters.At(Vector3.zero));
            CueHandle handle = router.Add(unknown, CueParameters.At(Vector3.zero));

            Assert.That(first, Is.False);
            Assert.That(second, Is.False);
            Assert.That(handle.IsValid, Is.False);
            Assert.That(_spawner.SpawnCount, Is.EqualTo(0));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Add_Remove_RepeatedAndStaleHandle_NoOp_RemoveAll()
        {
            GameplayTag aura = _cues.CreateTag("Cue.Aura");
            GameplayTag fire = _cues.CreateTag("Cue.Aura.Fire", aura);
            GameplayTag other = _cues.CreateTag("Cue.Other");
            CueRouter router = Router(
                _cues.CreateCue(fire, CueKind.Looping, Item(CueEnd.OnRemove)),
                _cues.CreateCue(other, CueKind.Looping, Item(CueEnd.OnRemove)));

            CueHandle first = router.Add(fire, CueParameters.At(Vector3.zero));
            bool activeAfterAdd = router.IsActive(first);
            bool removed = router.Remove(first);
            bool removedAgain = router.Remove(first);
            CueHandle reused = router.Add(fire, CueParameters.At(Vector3.zero));
            bool staleRemoved = router.Remove(first);

            Assert.That(activeAfterAdd, Is.True);
            Assert.That(removed, Is.True);
            Assert.That(removedAgain, Is.False);
            Assert.That(staleRemoved, Is.False, "handle antigo de slot reutilizado");
            Assert.That(router.IsActive(reused), Is.True);
            Assert.That(router.Remove(default), Is.False);

            CueHandle otherHandle = router.Add(other, CueParameters.At(Vector3.zero));
            router.RemoveAll(aura);

            Assert.That(router.IsActive(reused), Is.False, "RemoveAll casa a tag filha");
            Assert.That(router.IsActive(otherHandle), Is.True);

            router.Add(fire, CueParameters.At(Vector3.zero));
            router.RemoveAll();

            Assert.That(router.ActiveCount, Is.EqualTo(0));
            Assert.That(_spawner.Live, Is.Empty);
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(_spawner.SpawnCount));
        }

        [Test]
        public void FixedDuration_ReleasesByTimeSource_PausedDoesNot()
        {
            GameplayTag tag = _cues.CreateTag("Cue.Hit");
            CueRouter router = Router(_cues.CreateCue(tag, CueKind.Burst, Item(CueEnd.FixedDuration, duration: 1f)));
            router.Update();
            CueHandle handle = router.Add(tag, CueParameters.At(Vector3.zero));

            _time.Advance(0.6f);
            router.Update();
            for (int i = 0; i < 5; i++)
            {
                router.Update(); // fonte parada: pausa
            }

            Assert.That(_spawner.ReleaseCount, Is.EqualTo(0));
            Assert.That(router.IsActive(handle), Is.True);

            _time.Advance(0.5f);
            router.Update();

            Assert.That(_spawner.ReleaseCount, Is.EqualTo(1));
            Assert.That(router.IsActive(handle), Is.False, "o handle do Burst deixa de valer quando ele acaba");
            Assert.That(router.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void MaxSimultaneous_DropNew_ReplaceOldest()
        {
            GameplayTag dropTag = _cues.CreateTag("Cue.Drop");
            GameplayTag replaceTag = _cues.CreateTag("Cue.Replace");
            CueRouter router = Router(
                _cues.CreateCue(dropTag, CueKind.Looping, 3, CueLimitPolicy.DropNew, Item(CueEnd.OnRemove)),
                _cues.CreateCue(replaceTag, CueKind.Looping, 3, CueLimitPolicy.ReplaceOldest, Item(CueEnd.OnRemove)));
            var drop = new CueHandle[4];
            var replace = new CueHandle[4];

            for (int i = 0; i < 4; i++)
            {
                drop[i] = router.Add(dropTag, CueParameters.At(Vector3.zero));
            }

            Assert.That(drop[3].IsValid, Is.False);
            Assert.That(_spawner.SpawnCount, Is.EqualTo(3), "o quarto não cria objeto");

            for (int i = 0; i < 4; i++)
            {
                replace[i] = router.Add(replaceTag, CueParameters.At(Vector3.zero));
            }

            Assert.That(router.IsActive(replace[0]), Is.False, "o mais antigo sai");
            Assert.That(router.IsActive(replace[3]), Is.True);
            Assert.That(router.ActiveCount, Is.EqualTo(6));
            Assert.That(_spawner.Live.Count, Is.EqualTo(6));
        }

        [Test]
        public void RouterPublishesInAnchor_UnsetOnDisable()
        {
            GameplayTag tag = _cues.CreateTag("Cue.Aura");
            CueRouterAnchor anchor = _cues.CreateAnchor();
            CueRouter router = _cues.CreateRouter(_time, _spawner, anchor,
                _cues.CreateCue(tag, CueKind.Looping, Item(CueEnd.OnRemove)));

            router.Awake();
            router.OnEnable();
            CueRouter published = anchor.Value;
            router.Add(tag, CueParameters.At(Vector3.zero));
            router.OnDisable();

            Assert.That(published, Is.SameAs(router));
            Assert.That(anchor.HasValue, Is.False);
            Assert.That(router.ActiveCount, Is.EqualTo(0), "OnDisable remove os cues");
            Assert.That(_spawner.Live, Is.Empty);
        }

        private CueRouter Router(params CueDefinition[] cues)
        {
            return _cues.CreateRouter(_time, _spawner, null, cues);
        }

        private TestCueItem Item(
            CueEnd end, CuePlacement placement = CuePlacement.World, Vector3 offset = default, float duration = 0.5f,
            SpawnerAsset spawner = null)
        {
            return new TestCueItem
            {
                Prefab = _prefab, Spawner = spawner, Placement = placement, Offset = offset, End = end, Duration = duration,
            };
        }

        /// <summary>Item de teste: cria uma cópia do prefab pelo roteador, com o posicionamento e o fim dados.</summary>
        [Serializable]
        public sealed class TestCueItem : CueItem
        {
            public GameObject Prefab;
            public SpawnerAsset Spawner;
            public CuePlacement Placement;
            public Vector3 Offset;
            public CueEnd End;
            public float Duration;

            public override void Play(in CueParameters parameters, CuePlayback playback)
            {
                playback.Spawn(Prefab, Spawner, new CueSpawnRequest(
                    parameters.Position, parameters.Rotation, Placement, parameters.Target, Offset, End, Duration));
            }
        }
    }
}
