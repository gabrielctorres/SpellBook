using System.Collections.Generic;
using UnityEngine;

namespace SpellBook.Systems.Pooling
{
    /// <summary>
    /// Pooler de objetos baseado em componente.
    /// Gerencia uma fila (Queue) para acesso O(1) e utiliza uma Safe Zone para instanciar objetos.
    /// </summary>
    public class ComponentPooler : MonoBehaviour
    {
        private PoolConfiguration _config;
        private Queue<GameObject> _poolQueue = new Queue<GameObject>();
        private Transform _container;
        
        // Posição segura longe do mapa para evitar flashes no 0,0,0 durante o spawn
        private static Vector3 _safeZone = new Vector3(9999, 9999, 9999);

        public void Initialize(PoolConfiguration config)
        {
            _config = config;
            _container = new GameObject($"[Container] {config.Prefab.name}").transform;
            _container.SetParent(this.transform);
            _container.position = _safeZone;

            for (int i = 0; i < config.InitialSize; i++)
            {
                CreateNewInstance();
            }
        }

        private GameObject CreateNewInstance()
        {
            // Instancia na safe zone e já desativado
            GameObject obj = Instantiate(_config.Prefab, _container.position, Quaternion.identity, _container);
            obj.SetActive(false);
            
            if (!obj.TryGetComponent<PoolableObject>(out var p))
                p = obj.AddComponent<PoolableObject>();
            
            p.SetOriginPool(this);
            _poolQueue.Enqueue(obj);
            return obj;
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            if (_poolQueue.Count == 0)
            {
                if (_config.CanExpand) CreateNewInstance();
                else return null;
            }

            GameObject obj = _poolQueue.Dequeue();
            
            // Move e rotaciona ANTES de ativar para evitar bugs visuais
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.transform.SetParent(null); 
            obj.SetActive(true);

            if (obj.TryGetComponent<IPoolable>(out var poolable))
            {
                poolable.OnSpawnFromPool();
            }

            return obj;
        }

        public void ReturnToPool(GameObject obj)
        {
            if (obj == null) return;

            if (obj.TryGetComponent<IPoolable>(out var poolable))
            {
                poolable.OnReturnToPool();
            }

            obj.SetActive(false);
            obj.transform.SetParent(_container);
            obj.transform.position = _container.position; // Volta para a safe zone
            _poolQueue.Enqueue(obj);
        }
    }
}

