using System;
using SpellBook.Core.Inspector;
using UnityEngine;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Attributes
{
    [Serializable]
    public struct ModifierValueSource
    {
        public enum SourceType
        {
            Constant,
            Attribute,
            Formula
        }

        public SourceType Type;

        [ShowIf(nameof(Type), SourceType.Constant)]
        public float ConstantValue;

        [ShowIf(nameof(Type), SourceType.Attribute)]
        public AttributeDefinition Attribute;

        [ShowIf(nameof(Type), SourceType.Formula)]
        public Formula Formula;

        public float GetValue(AbilitySystemComponent source, AbilitySystemComponent target)
        {
            switch (Type)
            {
                case SourceType.Constant:
                    return ConstantValue;
                case SourceType.Attribute:
                    return source != null ? source.AttributeSet.GetValue(Attribute) : 0f;
                case SourceType.Formula:
                    return Formula != null ? Formula.Calculate(source, target) : 0f;
                default:
                    return 0f;
            }
        }
    }
}

