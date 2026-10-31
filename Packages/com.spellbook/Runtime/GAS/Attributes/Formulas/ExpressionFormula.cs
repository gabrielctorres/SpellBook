using System;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.Core;
using SpellBook.GAS.Core;
using System.Linq;

namespace SpellBook.GAS.Attributes
{
    [CreateAssetMenu(fileName = "Formula_Expression", menuName = "GAS/Formulas/Expression Formula")]
    [HelpURL(DocUrls.Gas + "#expressionformula")]
    public class ExpressionFormula : Formula
    {
        public enum TargetSource
        {
            Source,
            Target
        }

        [Serializable]
        public struct VariableMapping
        {
            public string Token;

            public AttributeDefinition Attribute;

            public TargetSource Source;
        }

        [Tooltip("Variables are mapped via [Tags]. Example: ([ATK] * 2) / [DEF]")]
        [TextArea(3, 5)]
        public string Expression = "0";

        public List<VariableMapping> Mappings = new List<VariableMapping>();

        public override float Calculate(AbilitySystemComponent source, AbilitySystemComponent target)
        {
            string sanitizedExpression = Expression;

            foreach (var mapping in Mappings)
            {
                if (string.IsNullOrEmpty(mapping.Token) || mapping.Attribute == null) continue;

                AbilitySystemComponent asc = mapping.Source == TargetSource.Source ? source : target;
                float value = asc != null ? asc.AttributeSet.GetValue(mapping.Attribute) : 0;

                // Replace [TOKEN] with actual value. We use string.Replace with brackets to be precise.
                string tokenWithBrackets = $"[{mapping.Token}]";
                sanitizedExpression = sanitizedExpression.Replace(tokenWithBrackets, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            return ExpressionParser.Evaluate(sanitizedExpression);
        }

        public override IEnumerable<AttributeDefinition> GetSourceDependencies()
        {
            return Mappings
                .Where(m => m.Source == TargetSource.Source && m.Attribute != null)
                .Select(m => m.Attribute);
        }
    }
}

