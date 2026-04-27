using UnityEngine;

namespace SpellBook.Systems.Pooling
{
    /// <summary>
    /// Define as configurações de pool para um prefab específico.
    /// </summary>
    [CreateAssetMenu(fileName = "PoolConfig_", menuName = "PoolingSystem/Pool Configuration")]
    public class PoolConfiguration : ScriptableObject
    {
        public GameObject Prefab;
        public int InitialSize = 20;
        public bool CanExpand = true;
    }
}

