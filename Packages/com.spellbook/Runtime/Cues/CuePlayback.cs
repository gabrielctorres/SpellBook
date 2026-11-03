using System.Collections.Generic;
using SpellBook.Core;
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
        }

        private readonly CueRouter _router;
        private readonly List<Tracked> _objects = new List<Tracked>(4);

        internal CuePlayback(CueRouter router, int slot)
        {
            _router = router;
            Slot = slot;
        }

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

            _objects.Add(new Tracked { Instance = instance, Spawner = source });
            return instance;
        }

        internal void Begin(CueDefinition cue, long sequence)
        {
            Cue = cue;
            Sequence = sequence;
            IsActive = true;
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
