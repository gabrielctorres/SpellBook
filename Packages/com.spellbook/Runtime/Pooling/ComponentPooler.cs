using System.Collections.Generic;
using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Pooling
{
    /// <summary>
    /// Pooler de objetos baseado em componente.
    /// Gerencia uma fila (Queue) para acesso O(1) e utiliza uma Safe Zone para instanciar objetos.
    /// </summary>
    [HelpURL(DocUrls.Pooling + "#componentpooler")]
    public class ComponentPooler : MonoBehaviour
    {
        private PoolConfiguration _config;
        private GameObject _prefab;
        private bool _canExpand;
        private Queue<GameObject> _poolQueue = new Queue<GameObject>();
        private Transform _container;
        
        // Posição segura longe do mapa para evitar flashes no 0,0,0 durante o spawn
        private static Vector3 _safeZone = new Vector3(9999, 9999, 9999);

        public void Initialize(PoolConfiguration config)
        {
            _config = config;
            Initialize(config.Prefab, config.InitialSize, config.CanExpand);
        }

        /// <summary>Inicia o pool sem asset de configuração (pool criado em runtime).</summary>
        internal void Initialize(GameObject prefab, int initialSize, bool canExpand)
        {
            _prefab = prefab;
            _canExpand = canExpand;
            _container = new GameObject($"[Container] {prefab.name}").transform;
            _container.SetParent(this.transform);
            _container.position = _safeZone;

            for (int i = 0; i < initialSize; i++)
            {
                CreateNewInstance();
            }
        }

        private GameObject CreateNewInstance()
        {
            // Instancia na safe zone e já desativado
            GameObject obj = Instantiate(_prefab, _container.position, Quaternion.identity, _container);
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
                if (_config != null ? _config.CanExpand : _canExpand) CreateNewInstance();
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

