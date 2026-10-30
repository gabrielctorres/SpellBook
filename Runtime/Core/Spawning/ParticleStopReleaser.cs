using UnityEngine;

namespace SpellBook.Core
{
    /// <summary>Ligado por <see cref="SpawnerAsset.ReleaseWhenParticlesStop"/>: devolve o objeto quando a partícula para.</summary>
    [AddComponentMenu("")]
    internal sealed class ParticleStopReleaser : MonoBehaviour
    {
        internal SpawnerAsset Spawner { get; set; }

        private void OnParticleSystemStopped()
        {
            if (Spawner != null) Spawner.Release(gameObject);
        }
    }
}
