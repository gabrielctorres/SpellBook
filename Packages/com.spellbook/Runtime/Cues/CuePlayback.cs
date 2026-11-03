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
            public Quaternion LocalRotation;
            public CueEnd End;
            public float Remaining;
            public ParticleSystem Particles;
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

        /// <summary>Fonte de aleatoriedade do roteador.</summary>
        public IRandomSource Random => _router.Random;

        internal int Slot { get; }

        internal int Generation { get; private set; } = 1;

        internal bool IsActive { get; private set; }

        /// <summary>Removido, mas com partículas que ainda não acabaram: o slot só volta ao livre quando esvaziar.</summary>
        internal bool IsDraining { get; private set; }

        internal CueDefinition Cue { get; private set; }

        internal long Sequence { get; private set; }

        internal CueHandle Handle => new CueHandle(Slot, Generation);

        internal bool HasObjects => _objects.Count > 0;

        /// <summary>Prefab de áudio do roteador. Sem ele, avisa uma vez por roteador.</summary>
        internal GameObject AudioPrefab
        {
            get
            {
                GameObject prefab = _router.AudioPrefab;
                if (prefab == null && _router.ShouldWarn(_router))
                {
                    Debug.LogWarning($"'{_router.name}' não tem prefab de áudio; os sons não tocam.", _router);
                }

                return prefab;
            }
        }

        /// <summary>
        /// Magnitude normalizada (0 a 1) dos <paramref name="parameters"/>; sem magnitude, a padrão da definição do cue.
        /// </summary>
        public float NormalizedMagnitude(in CueParameters parameters)
        {
            return parameters.HasMagnitude ? parameters.NormalizedMagnitude : Cue.DefaultMagnitude;
        }

        /// <summary>
        /// Cria <paramref name="prefab"/> pelo <paramref name="spawner"/> (vazio: o padrão do roteador) e o rastreia até o
        /// fim do <paramref name="request"/>. Devolve <c>null</c>, com um aviso, se o spawner não criar.
        /// </summary>
        public GameObject Spawn(GameObject prefab, SpawnerAsset spawner, in CueSpawnRequest request)
        {
            if (prefab == null) return null;

            SpawnerAsset source = spawner != null ? spawner : _router.DefaultSpawner;
            // O quaternion default (todo zero) não é uma rotação: vale como sem rotação extra. O == da Unity compara pelo
            // produto escalar e não reconhece o zero, por isso o Equals.
            Quaternion localRotation = request.LocalRotation.Equals(default(Quaternion))
                ? Quaternion.identity
                : request.LocalRotation;
            var tracked = new Tracked
            {
                Spawner = source,
                // Sem alvo, não há o que seguir: o objeto fica onde nasceu
                Placement = request.Target != null ? request.Placement : CuePlacement.World,
                Target = request.Target,
                Offset = request.Offset,
                LocalRotation = localRotation,
                End = request.End,
                Remaining = request.Duration,
            };

            Vector3 position = request.Position + request.Offset;
            Quaternion rotation = request.Rotation * localRotation;
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
            if (tracked.End == CueEnd.ParticlesStop) instance.TryGetComponent(out tracked.Particles);
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
                        if (IsActive && Cue.Kind == CueKind.Looping) return false;
                        ReleaseAt(i);
                        continue;
                    }

                    Vector3 position = tracked.Transform.position;
                    Quaternion rotation = tracked.Transform.rotation;
                    Pose(in tracked, ref position, ref rotation);
                    tracked.Transform.SetPositionAndRotation(position, rotation);
                }

                if (tracked.End == CueEnd.ParticlesStop)
                {
                    // Sem partícula na raiz, não há o que esperar
                    if (tracked.Particles == null || !tracked.Particles.IsAlive(true)) ReleaseAt(i);
                }
                else if (tracked.End == CueEnd.FixedDuration || tracked.End == CueEnd.ClipEnd)
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

        /// <summary>
        /// Remove o cue: os handles deixam de valer, as partículas vivas param de emitir e ficam até acabar, e o resto
        /// volta na hora. Verdadeiro se o slot ficou esvaziando.
        /// </summary>
        internal bool Stop()
        {
            Deactivate();
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                ParticleSystem particles = _objects[i].Particles;
                if (particles != null && particles.IsAlive(true))
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                else
                {
                    ReleaseAt(i);
                }
            }

            IsDraining = _objects.Count > 0;
            if (!IsDraining) Cue = null;
            return IsDraining;
        }

        /// <summary>Devolve todos os objetos na hora e libera o slot; os handles dele deixam de valer.</summary>
        internal void End()
        {
            if (IsActive) Deactivate();
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                ReleaseAt(i);
            }

            Cue = null;
            IsDraining = false;
        }

        private void Deactivate()
        {
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
                // A rotação extra não gira o deslocamento: ele acompanha só o alvo
                Quaternion targetRotation = target.rotation;
                rotation = targetRotation * tracked.LocalRotation;
                position = target.position + targetRotation * tracked.Offset;
            }
            else
            {
                position = target.position + tracked.Offset;
            }
        }
    }
}
