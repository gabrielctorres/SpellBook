using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Pooling
{
    /// <summary>
    /// Spawner com pool (ADR 0015): cria um <see cref="ComponentPooler"/> por prefab sob demanda, embaixo da raiz
    /// <c>[SpellBook Pools]</c>. Não procura o <see cref="PoolManager"/> da cena. O cache de pools é limpo ao entrar em Play.
    /// </summary>
    [CreateAssetMenu(menuName = "SpellBook/Pooling/Pooled Spawner", fileName = "PooledSpawner")]
    [HelpURL(DocUrls.Pooling + "#pooledspawnerasset")]
    public class PooledSpawnerAsset : SpawnerAsset, IValidatable
    {
        private const string RootName = "[SpellBook Pools]";

        private static readonly List<PooledSpawnerAsset> _live = new List<PooledSpawnerAsset>();
        private static Transform _root;

        [Tooltip("Instâncias criadas quando o pool de um prefab nasce, no primeiro Spawn dele.")]
        [SerializeField] private int initialSize = 5;

        [Tooltip("Cria uma instância nova quando o pool está vazio. Desligado, o Spawn devolve null.")]
        [SerializeField] private bool canExpand = true;

        private readonly Dictionary<GameObject, ComponentPooler> _pools = new Dictionary<GameObject, ComponentPooler>();

        /// <summary>Tira uma instância do pool do <paramref name="prefab"/>; devolve <c>null</c> se o pool estiver vazio e não puder crescer.</summary>
        public override GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));

            GameObject instance = GetOrCreatePooler(prefab).Get(position, rotation);
            if (instance != null && parent != null) instance.transform.SetParent(parent, true);
            return instance;
        }

        /// <summary>Devolve a instância ao pool de origem; se ela não veio de um pool, destrói.</summary>
        public override void Release(GameObject instance)
        {
            if (instance == null) return;

            if (instance.TryGetComponent<PoolableObject>(out var poolable) && poolable.OriginPool != null)
            {
                poolable.OriginPool.ReturnToPool(instance);
                return;
            }

            if (Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
        }

        public void Validate(ValidationResult result)
        {
            if (initialSize < 0)
            {
                result.AddError("O tamanho inicial do pool não pode ser negativo. Use 0 ou mais.", nameof(initialSize));
            }
        }

        private ComponentPooler GetOrCreatePooler(GameObject prefab)
        {
            if (_pools.TryGetValue(prefab, out ComponentPooler pooler) && pooler != null) return pooler;

            var config = CreateInstance<PoolConfiguration>();
            config.Prefab = prefab;
            config.InitialSize = initialSize;
            config.CanExpand = canExpand;

            var poolObject = new GameObject($"Pool_{prefab.name}");
            poolObject.transform.SetParent(GetRoot(), false);
            pooler = poolObject.AddComponent<ComponentPooler>();
            pooler.Initialize(config);
            _pools[prefab] = pooler;
            return pooler;
        }

        private static Transform GetRoot()
        {
            if (_root == null)
            {
                _root = new GameObject(RootName).transform;
                if (Application.isPlaying) DontDestroyOnLoad(_root.gameObject);
            }

            return _root;
        }

        private void OnEnable()
        {
            if (!_live.Contains(this)) _live.Add(this);
        }

        private void OnDisable()
        {
            _live.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAllOnSessionStart()
        {
            _root = null;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i] == null) _live.RemoveAt(i);
                else _live[i]._pools.Clear();
            }
        }
    }
}
