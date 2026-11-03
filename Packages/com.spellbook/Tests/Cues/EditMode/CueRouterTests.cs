using System;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;

namespace SpellBook.Cues.Tests
{
    /// <summary>Roteador: tocar, ligar e desligar (CA1, CA7).</summary>
    public class CueRouterTests
    {
        private CuesFactory _cues;
        private RecordingSpawnerAsset _spawner;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _cues = new CuesFactory();
            _spawner = _cues.CreateSpawner();
            _prefab = _cues.CreateObject("CuePrefab_Test");
        }

        [TearDown]
        public void TearDown()
        {
            _cues.Dispose();
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

        private CueRouter Router(params CueDefinition[] cues)
        {
            return _cues.CreateRouter(_spawner, cues);
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
