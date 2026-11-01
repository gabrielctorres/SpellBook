using SpellBook.Core;
using UnityEngine;

namespace SpellBook.GAS.Attributes
{
    /// <summary>Grupo de atributos para a interface (por exemplo, "Ofensivo" ou "Defensivo"), escolhido em <see cref="AttributeDisplay.Category"/>.</summary>
    [CreateAssetMenu(fileName = "NewAttributeCategory", menuName = "GAS/Attribute Category")]
    [HelpURL(DocUrls.Gas + "#attributecategory")]
    public class AttributeCategory : ScriptableObject
    {
        public string DisplayName;

        [Tooltip("Ordem da categoria na interface; menor vem primeiro")]
        public int Order;
    }
}
