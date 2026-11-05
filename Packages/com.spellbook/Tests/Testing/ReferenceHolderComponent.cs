using UnityEngine;

namespace SpellBook.Testing
{
    /// <summary>
    /// Componente com um campo de objeto, para a busca de referências em prefabs e cenas. Fica num assembly de runtime
    /// para poder ser adicionado a um GameObject, e em arquivo próprio para a Unity associar o script.
    /// </summary>
    public sealed class ReferenceHolderComponent : MonoBehaviour
    {
        [SerializeField] private Object target;

        /// <summary>Objeto referenciado pelo campo serializado <c>target</c>.</summary>
        public Object Target
        {
            get => target;
            set => target = value;
        }
    }
}
