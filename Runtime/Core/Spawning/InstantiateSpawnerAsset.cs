using UnityEngine;

namespace SpellBook.Core
{
    /// <summary>Spawner padrão: cria com <c>Instantiate</c> e devolve destruindo a instância (ADR 0015).</summary>
    [CreateAssetMenu(menuName = "SpellBook/Spawn/Instantiate Spawner", fileName = "InstantiateSpawner")]
    [HelpURL(DocUrls.Pooling + "#spawnerasset")]
    public class InstantiateSpawnerAsset : SpawnerAsset
    {
        public override GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return Instantiate(prefab, position, rotation, parent);
        }

        /// <summary>Destrói a instância; fora do Play usa <c>DestroyImmediate</c>, porque <c>Destroy</c> não roda.</summary>
        public override void Release(GameObject instance)
        {
            if (instance == null) return;

            if (Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
        }
    }
}
