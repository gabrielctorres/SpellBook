using System;
using UnityEngine;

namespace SpellBook.Core
{
    /// <summary>
    /// Ponto único de criação e devolução de objetos de gameplay (ADR 0015). Cada sistema recebe um spawner por
    /// campo serializado; campo vazio usa o <see cref="Default"/>. O objeto volta pelo mesmo spawner que o criou.
    /// </summary>
    public abstract class SpawnerAsset : ScriptableObject
    {
        private static SpawnerAsset _default;

        /// <summary>Spawner padrão por Instantiate, criado sob demanda e não salvo em disco.</summary>
        public static SpawnerAsset Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<InstantiateSpawnerAsset>();
                    _default.name = "Default Spawner";
                    _default.hideFlags = HideFlags.DontSave;
                }

                return _default;
            }
        }

        /// <summary>O <paramref name="spawner"/> configurado, ou o <see cref="Default"/> se estiver vazio.</summary>
        public static SpawnerAsset OrDefault(SpawnerAsset spawner)
        {
            return spawner != null ? spawner : Default;
        }

        /// <returns>A instância, ou <c>null</c> se o spawner não puder criar uma (por exemplo, pool vazio que não cresce).</returns>
        public abstract GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null);

        public abstract void Release(GameObject instance);

        /// <summary>
        /// Devolve a <paramref name="instance"/> a este spawner quando o <see cref="ParticleSystem"/> da raiz dela parar.
        /// Troca o <c>stopAction</c> da partícula para <see cref="ParticleSystemStopAction.Callback"/>; não lê o relógio.
        /// </summary>
        public void ReleaseWhenParticlesStop(GameObject instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var particles = instance.GetComponent<ParticleSystem>();
            if (particles == null)
            {
                throw new ArgumentException($"'{instance.name}' não tem um ParticleSystem na raiz.", nameof(instance));
            }

            ParticleSystem.MainModule main = particles.main;
            main.stopAction = ParticleSystemStopAction.Callback;

            var releaser = instance.GetComponent<ParticleStopReleaser>();
            if (releaser == null) releaser = instance.AddComponent<ParticleStopReleaser>();
            releaser.Spawner = this;
        }
    }
}
