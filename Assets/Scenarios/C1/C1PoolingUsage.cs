using SpellBook.Core;
using SpellBook.Pooling;
using UnityEngine;

namespace SpellBook.Scenarios.C1
{
    /// <summary>
    /// Cenário C1 (CA6, N4): script de um projeto que referencia só o <c>SpellBook.Core</c> e o <c>SpellBook.Pooling</c>
    /// e usa um <see cref="PooledSpawnerAsset"/>. Ele compilar no projeto da raiz é a verificação; não fica em cena.
    /// </summary>
    public class C1PoolingUsage : MonoBehaviour
    {
        [SerializeField] private PooledSpawnerAsset spawner;
        [SerializeField] private GameObject prefab;

        /// <summary>Cria uma instância do <c>prefab</c> pelo spawner com pool e devolve, ou <c>null</c> sem spawner ou prefab.</summary>
        public GameObject SpawnOne()
        {
            if (spawner == null || prefab == null)
            {
                return null;
            }

            SpawnerAsset asSpawner = spawner;
            return asSpawner.Spawn(prefab, transform.position, transform.rotation);
        }

        /// <summary>Devolve uma instância criada por <see cref="SpawnOne"/> ao pool.</summary>
        public void Release(GameObject instance)
        {
            if (spawner != null && instance != null)
            {
                spawner.Release(instance);
            }
        }
    }
}
