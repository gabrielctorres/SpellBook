using UnityEngine;
using Sirenix.OdinInspector;

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
        [HorizontalGroup("Split", 65)]
        [PreviewField(60, ObjectFieldAlignment.Left)]
        [HideLabel]
        public Sprite Icon;

        [VerticalGroup("Split/Right")]
        [LabelWidth(100)]
        public string DisplayName;

        [VerticalGroup("Split/Right")]
        [LabelWidth(100)]
        [EnumPaging]
        public AttributeType Type = AttributeType.Primary;

        [VerticalGroup("Split/Right")]
        [ShowIf("Type", AttributeType.Derived)]
        [LabelWidth(100)]
        [Required]
        [InlineEditor]
        public Formula Formula;

        [BoxGroup("Limits")]
        [HorizontalGroup("Limits/Range")]
        [LabelWidth(70)]
        public float MinValue = 0f;

        [HorizontalGroup("Limits/Range")]
        [LabelWidth(70)]
        public float MaxValue = 9999f;
    }
}

