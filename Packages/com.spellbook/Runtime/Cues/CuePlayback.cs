using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Time;
using UnityEngine;

namespace SpellBook.Cues
{
    /// <summary>
    /// Uma execução de cue no <see cref="CueRouter"/>. Os itens criam objetos por <see cref="Spawn"/>; o roteador os
    /// posiciona a cada tick e os devolve ao spawner no fim. Cada slot do roteador tem uma, reutilizada.
    /// </summary>
    public sealed class CuePlayback
    {
        private struct Tracked
        {
            public GameObject Instance;
            public Transform Transform;
            public SpawnerAsset Spawner;
            public CuePlacement Placement;
            public Transform Target;
            public Vector3 Offset;
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
        /// fim do <paramref name="request"/>. Devolve <c>null</c>, com um aviso, se o spawner não criar.
        /// </summary>
        public GameObject Spawn(GameObject prefab, SpawnerAsset spawner, in CueSpawnRequest request)
        {
            if (prefab == null) return null;

            SpawnerAsset source = spawner != null ? spawner : _router.DefaultSpawner;
            var tracked = new Tracked
            {
                Spawner = source,
                // Sem alvo, não há o que seguir: o objeto fica onde nasceu
                Placement = request.Target != null ? request.Placement : CuePlacement.World,
                Target = request.Target,
                Offset = request.Offset,
                End = request.End,
                Remaining = request.Duration,
            };

            Vector3 position = request.Position + request.Offset;
            Quaternion rotation = request.Rotation;
            Pose(in tracked, ref position, ref rotation);
            GameObject instance = source.Spawn(prefab, position, rotation);
            if (instance == null)
            {
                if (_router.ShouldWarn(source))
                {
                    Debug.LogWarning($"O spawner '{source.name}' devolveu null para '{prefab.name}'; o item foi pulado.", _router);
                }

                return null;
            }

            tracked.Instance = instance;
            tracked.Transform = instance.transform;
            _objects.Add(tracked);
            return instance;
        }

        internal void Begin(CueDefinition cue, long sequence)
        {
            Cue = cue;
            Sequence = sequence;
            IsActive = true;
        }

        /// <summary>Posiciona e conta o tempo. Falso se a execução acabou: sem objetos, ou alvo perdido num Looping.</summary>
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

                if (tracked.Placement != CuePlacement.World)
                {
                    if (tracked.Target == null || !tracked.Target.gameObject.activeInHierarchy)
                    {
                        if (Cue.Kind == CueKind.Looping) return false;
                        ReleaseAt(i);
                        continue;
                    }

                    Vector3 position = tracked.Transform.position;
                    Quaternion rotation = tracked.Transform.rotation;
                    Pose(in tracked, ref position, ref rotation);
                    tracked.Transform.SetPositionAndRotation(position, rotation);
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

        private static void Pose(in Tracked tracked, ref Vector3 position, ref Quaternion rotation)
        {
            if (tracked.Placement == CuePlacement.World) return;

            Transform target = tracked.Target;
            if (tracked.Placement == CuePlacement.AttachToTarget)
            {
                rotation = target.rotation;
                position = target.position + rotation * tracked.Offset;
            }
            else
            {
                position = target.position + tracked.Offset;
            }
        }
    }
}
