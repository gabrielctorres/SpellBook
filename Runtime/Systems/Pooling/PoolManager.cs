using System.Collections.Generic;
using UnityEngine;

namespace SpellBook.Systems.Pooling
{
    /// <summary>
    /// Gerenciador Central de Pooling Modular. 
    /// Recebe uma lista de PoolConfigurations para realizar o pre-warm no Awake.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        [Header("Pre-warm Configurations")]
        [SerializeField] private List<PoolConfiguration> _startingPools;

        private Dictionary<GameObject, ComponentPooler> _activePools = new Dictionary<GameObject, ComponentPooler>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeStartingPools();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeStartingPools()
        {
            foreach (var config in _startingPools)
            {
                if (config != null && config.Prefab != null)
                {
                    CreatePooler(config);
                }
            }
        }

        /// <summary>
        /// Obtém um objeto do pooler associado ao prefab.
        /// </summary>
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (!_activePools.TryGetValue(prefab, out var pooler))
            {
                // Fallback: Cria um pooler dinâmico se não houver configuração prévia
                var dynamicConfig = ScriptableObject.CreateInstance<PoolConfiguration>();
                dynamicConfig.Prefab = prefab;
                dynamicConfig.InitialSize = 5;
                dynamicConfig.CanExpand = true;
                pooler = CreatePooler(dynamicConfig);
            }

            return pooler.Get(position, rotation);
        }

        private ComponentPooler CreatePooler(PoolConfiguration config)
        {
            if (_activePools.ContainsKey(config.Prefab)) return _activePools[config.Prefab];

            GameObject go = new GameObject($"Pool_{config.Prefab.name}");
            go.transform.SetParent(this.transform);
            var pooler = go.AddComponent<ComponentPooler>();
            pooler.Initialize(config);
            _activePools.Add(config.Prefab, pooler);
            return pooler;
        }

        // Helper para o Projétil saber como voltar
        public ComponentPooler GetPooler(GameObject prefab)
        {
            if (prefab != null && _activePools.TryGetValue(prefab, out var pooler))
            {
                return pooler;
            }
            return null;
        }
    }
}

