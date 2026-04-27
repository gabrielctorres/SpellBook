using System;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Core;
using Sirenix.OdinInspector;

namespace SpellBook.GAS.Attributes
{
    [CreateAssetMenu(fileName = "Formula_Simple", menuName = "GAS/Formulas/Simple Attribute Formula")]
    public class SimpleAttributeFormula : Formula
    {
        [Serializable]
        public struct AttributeComponent
        {
            [HorizontalGroup("Row")]
            [HideLabel]
            [Required]
            public AttributeDefinition Attribute;

            [HorizontalGroup("Row", 0.2f)]
            [HideLabel]
            public float Multiplier;
            
            [HorizontalGroup("Row", 0.2f)]
            [HideLabel]
            [EnumPaging]
            public TargetSource Source;
        }

        public enum TargetSource
        {
            Source,
            Target
        }

        [BoxGroup("Calculation")]
        [LabelWidth(100)]
        [SerializeField] private float baseValue;

        [BoxGroup("Calculation")]
        [TableList(AlwaysExpanded = true)]
        [SerializeField] private List<AttributeComponent> attributes;

        public override float Calculate(AbilitySystemComponent source, AbilitySystemComponent target)
        {
            float total = baseValue;

            foreach (var comp in attributes)
            {
                AbilitySystemComponent asc = comp.Source == TargetSource.Source ? source : target;
                if (asc != null)
                {
                    total += asc.AttributeSet.GetValue(comp.Attribute) * comp.Multiplier;
                }
            }

            return total;
        }

        public override IEnumerable<AttributeDefinition> GetSourceDependencies()
        {
            foreach (var comp in attributes)
            {
                if (comp.Source == TargetSource.Source && comp.Attribute != null)
                {
                    yield return comp.Attribute;
                }
            }
        }
    }
}

