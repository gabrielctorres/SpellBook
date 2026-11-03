using System.Collections.Generic;
using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Testing
{
    /// <summary>
    /// Spawner de teste com pool: conta criações e devoluções e guarda as instâncias vivas. Com <see cref="ReturnsNull"/>,
    /// <c>Spawn</c> devolve <c>null</c>, como um pool vazio que não cresce. Depois do aquecimento, não aloca.
    /// </summary>
    public sealed class RecordingSpawnerAsset : SpawnerAsset
    {
        private readonly List<GameObject> _pool = new List<GameObject>();
        private readonly List<GameObject> _live = new List<GameObject>();

        /// <summary>Faz <c>Spawn</c> devolver <c>null</c>.</summary>
        public bool ReturnsNull { get; set; }

        /// <summary>Quantas instâncias foram entregues.</summary>
        public int SpawnCount { get; private set; }

        /// <summary>Quantas instâncias voltaram.</summary>
        public int ReleaseCount { get; private set; }

        /// <summary>Instâncias entregues que ainda não voltaram, na ordem de entrega.</summary>
        public IReadOnlyList<GameObject> Live => _live;

        public override GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (ReturnsNull) return null;

            GameObject instance;
            if (_pool.Count > 0)
            {
                instance = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                instance = Instantiate(prefab);
                instance.hideFlags = HideFlags.DontSave;
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            _live.Add(instance);
            SpawnCount++;
            return instance;
        }

        public override void Release(GameObject instance)
        {
            instance.SetActive(false);
            _live.Remove(instance);
            _pool.Add(instance);
            ReleaseCount++;
        }

        private void OnDestroy()
        {
            DestroyAll(_pool);
            DestroyAll(_live);
        }

        private static void DestroyAll(List<GameObject> objects)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null) DestroyImmediate(objects[i]);
            }

            objects.Clear();
        }
    }
}
