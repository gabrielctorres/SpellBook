using SpellBook.Core.Inspector;
using UnityEngine;

namespace SpellBook.GAS.Attributes
{
    public enum AttributeType
    {
        Primary, // Has base value and modifiers
        Derived  // Calculated entirely from a formula
    }

    [CreateAssetMenu(fileName = "NewAttribute", menuName = "GAS/Attribute Definition")]
    public class AttributeDefinition : ScriptableObject
    {
        public Sprite Icon;

        public string DisplayName;

        public AttributeType Type = AttributeType.Primary;

        [ShowIf(nameof(Type), AttributeType.Derived)]
        [Tooltip("Obrigatório quando Type = Derived")]
        public Formula Formula;

        [Header("Limits")]
        public float MinValue = 0f;

        public float MaxValue = 9999f;
    }
}

