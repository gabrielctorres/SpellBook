using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Pooling
{
    /// <summary>
    /// Componente que permite que um objeto seja reutilizado pelo PoolManager/ComponentPooler.
    /// </summary>
    [HelpURL(DocUrls.Pooling + "#componentpooler")]
    public class PoolableObject : MonoBehaviour
    {
        public string PrefabKey { get; set; }
        
        // Referência ao pooler que gerencia este objeto
        private ComponentPooler _originPool;

        /// <summary>Pool que criou o objeto, ou <c>null</c> se ele não veio de um pool.</summary>
        internal ComponentPooler OriginPool => _originPool;

        public void SetOriginPool(ComponentPooler pool)
        {
            _originPool = pool;
        }

        public void ReturnToPool()
        {
            gameObject.SetActive(false);
            
            if (_originPool != null)
            {
                _originPool.ReturnToPool(this.gameObject);
            }
            else
            {
                // Fallback caso tenha sido instanciado manualmente
                Destroy(gameObject);
            }
        }
    }
}

