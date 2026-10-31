using System;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.Core;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Attributes
{
    [CreateAssetMenu(fileName = "Formula_Simple", menuName = "GAS/Formulas/Simple Attribute Formula")]
    [HelpURL(DocUrls.Gas + "#simpleattributeformula")]
    public class SimpleAttributeFormula : Formula
    {
        [Serializable]
        public struct AttributeComponent
        {
            public AttributeDefinition Attribute;

            public float Multiplier;
            
            public TargetSource Source;
        }

        public enum TargetSource
        {
            Source,
            Target
        }

        [Header("Calculation")]
        [SerializeField] private float baseValue;

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

