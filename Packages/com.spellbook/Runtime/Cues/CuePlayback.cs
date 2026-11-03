using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Time;
using UnityEngine;

namespace SpellBook.Cues
{
    /// <summary>
    /// Uma execução de cue no <see cref="CueRouter"/>. Os itens criam objetos por <see cref="Spawn"/>, e o roteador os
    /// devolve ao spawner no fim. Cada slot do roteador tem uma, reutilizada.
    /// </summary>
    public sealed class CuePlayback
    {
        private struct Tracked
        {
            public GameObject Instance;
            public SpawnerAsset Spawner;
            public CueEnd End;
            public float Remaining;
        }

        private readonly CueRouter _router;
        private readonly List<Tracked> _objects = new List<Tracked>(4);

        internal CuePlayback(CueRouter router, int slot)
        {
            _router = router;
            Slot = slot;
        }

        /// <summary>Fonte de tempo do roteador.</summary>
        public ITimeSource Time => _router.TimeSource;

        internal int Slot { get; }

        internal int Generation { get; private set; } = 1;

        internal bool IsActive { get; private set; }

        internal CueDefinition Cue { get; private set; }

        internal long Sequence { get; private set; }

        internal CueHandle Handle => new CueHandle(Slot, Generation);

        internal bool HasObjects => _objects.Count > 0;

        /// <summary>
        /// Cria <paramref name="prefab"/> pelo <paramref name="spawner"/> (vazio: o padrão do roteador) e o rastreia até o
        /// fim do <paramref name="request"/>. Devolve <c>null</c> se o spawner não criar.
        /// </summary>
        public GameObject Spawn(GameObject prefab, SpawnerAsset spawner, in CueSpawnRequest request)
        {
            if (prefab == null) return null;

            SpawnerAsset source = spawner != null ? spawner : _router.DefaultSpawner;
            Vector3 position = request.Position + request.Offset;
            Quaternion rotation = request.Rotation;
            GameObject instance = source.Spawn(prefab, position, rotation);
            if (instance == null) return null;

            _objects.Add(new Tracked
            {
                Instance = instance, Spawner = source, End = request.End, Remaining = request.Duration,
            });
            return instance;
        }

        internal void Begin(CueDefinition cue, long sequence)
        {
            Cue = cue;
            Sequence = sequence;
            IsActive = true;
        }

        /// <summary>Conta o tempo e devolve ao spawner o que acabou. Falso quando não sobra objeto.</summary>
        internal bool Tick(float deltaTime)
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                Tracked tracked = _objects[i];
                if (tracked.Instance == null)
                {
                    _objects.RemoveAt(i);
                    continue;
                }

                if (tracked.End == CueEnd.FixedDuration)
                {
                    tracked.Remaining -= deltaTime;
                    if (tracked.Remaining <= 0f)
                    {
                        ReleaseAt(i);
                        continue;
                    }

                    _objects[i] = tracked;
                }
            }

            return _objects.Count > 0;
        }

        /// <summary>Devolve todos os objetos e libera o slot; os handles dele deixam de valer.</summary>
        internal void End()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                ReleaseAt(i);
            }

            Cue = null;
            IsActive = false;
            Generation++;
        }

        private void ReleaseAt(int index)
        {
            Tracked tracked = _objects[index];
            _objects.RemoveAt(index);
            if (tracked.Instance != null) tracked.Spawner.Release(tracked.Instance);
        }
    }
}
