using UnityEngine;

namespace SpellBook.Core.Tests.References
{
    /// <summary>
    /// Asset com um campo de objeto e um <c>[SerializeReference]</c>, para a busca de referências. Fica em arquivo
    /// próprio para a Unity associar o script ao asset.
    /// </summary>
    internal class ReferenceHolderAsset : ScriptableObject
    {
        [SerializeField] internal Object target;
        [SerializeReference] internal ReferenceSlot slot;
    }

    /// <summary>Objeto gerenciado que guarda uma referência, como uma ação dentro de uma lista.</summary>
    [System.Serializable]
    internal class ReferenceSlot
    {
        [SerializeField] internal Object target;
    }
}
