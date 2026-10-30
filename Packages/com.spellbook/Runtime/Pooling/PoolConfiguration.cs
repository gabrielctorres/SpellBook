using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Pooling
{
    /// <summary>
    /// Define as configurações de pool para um prefab específico.
    /// </summary>
    [CreateAssetMenu(fileName = "PoolConfig_", menuName = "SpellBook/Pooling/Pool Configuration")]
    [HelpURL(DocUrls.Pooling + "#poolmanager")]
    public class PoolConfiguration : ScriptableObject
    {
        public GameObject Prefab;
        public int InitialSize = 20;
        public bool CanExpand = true;
    }
}

