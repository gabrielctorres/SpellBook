using System;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Core;
using Sirenix.OdinInspector;
using System.Linq;

namespace SpellBook.GAS.Attributes
{
    [CreateAssetMenu(fileName = "Formula_Expression", menuName = "GAS/Formulas/Expression Formula")]
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
            [HorizontalGroup("Row", 0.2f)]
            [LabelText("[Tag]")]
            public string Token;

            [HorizontalGroup("Row")]
            [HideLabel]
            [Required]
            public AttributeDefinition Attribute;

            [HorizontalGroup("Row", 0.2f)]
            [HideLabel]
            public TargetSource Source;
        }

        [BoxGroup("Expression Engine")]
        [InfoBox("Variables are mapped via [Tags]. Example: ([ATK] * 2) / [DEF]", InfoMessageType.Info)]
        [TextArea(3, 5)]
        [HideLabel]
        [OnValueChanged("UpdatePreview")]
        public string Expression = "0";

        [BoxGroup("Expression Engine")]
        [LabelText("Variable Tokens Mapping")]
        [TableList(AlwaysExpanded = true, NumberOfItemsPerPage = 5)]
        public List<VariableMapping> Mappings = new List<VariableMapping>();

        [BoxGroup("Expression Engine")]
        [Title("Calculated Preview (Reference)")]
        [ReadOnly]
        [ShowInInspector]
        [GUIColor(0.8f, 1f, 0.8f)]
        private string _sanitizedPreview;

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

        private void UpdatePreview()
        {
            // Just a helper to visualize how tags are replaced. 
            // Real evaluation needs a running ASC which isn't available easily in pure asset preview.
            _sanitizedPreview = Expression;
            foreach (var mapping in Mappings)
            {
                if (!string.IsNullOrEmpty(mapping.Token))
                {
                    _sanitizedPreview = _sanitizedPreview.Replace($"[{mapping.Token}]", $"({mapping.Attribute?.name ?? "null"})");
                }
            }
        }
    }
}

