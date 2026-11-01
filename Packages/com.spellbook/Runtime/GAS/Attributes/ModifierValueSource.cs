using System;
using SpellBook.Core.Inspector;
using UnityEngine;
using UnityEngine.Serialization;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;

namespace SpellBook.GAS.Attributes
{
    /// <summary>De onde vem o valor de um modificador. É classe; o valor fixo é um <see cref="ScalableFloat"/>.</summary>
    [Serializable]
    public class ModifierValueSource : ISerializationCallbackReceiver
    {
        public enum SourceType
        {
            Value,
            Attribute,
            Formula
        }

        public SourceType Type;

        /// <summary>Valor por nível (fonte <see cref="SourceType.Value"/>). Sem habilidade, vale o nível 1.</summary>
        [ShowIf(nameof(Type), SourceType.Value)]
        public ScalableFloat Value = ScalableFloat.Constant(0);

        // Só leitura do formato 2.x ("ConstantValue: 5"); convertido em OnAfterDeserialize (ADR 0009). Sai antes da 3.0 final.
        [FormerlySerializedAs("ConstantValue")]
        [SerializeField, HideInInspector] private float legacyConstant = float.NaN;

        [ShowIf(nameof(Type), SourceType.Attribute)]
        public AttributeDefinition Attribute;

        [ShowIf(nameof(Type), SourceType.Formula)]
        public Formula Formula;

        /// <summary>Valor com <paramref name="source"/> como instigador, no nível 1 e sem tabela.</summary>
        public float GetValue(AbilitySystemComponent source, AbilitySystemComponent target)
        {
            return Evaluate(new EffectContext(source, target, null, 1, false, Vector3.zero, null));
        }

        /// <summary>
        /// Valor no <paramref name="context"/>: o <see cref="ScalableFloat"/> no nível e na tabela do contexto; atributo e fórmula
        /// leem o instigador. Sem alocação.
        /// </summary>
        internal float Evaluate(in EffectContext context)
        {
            switch (Type)
            {
                case SourceType.Value:
                    return Value != null ? Value.Evaluate(context.Level, context.Table) : 0f;
                case SourceType.Attribute:
                    return context.Instigator != null ? context.Instigator.AttributeSet.GetValue(Attribute) : 0f;
                case SourceType.Formula:
                    return Formula != null ? Formula.Calculate(context.Instigator, context.Target) : 0f;
                default:
                    return 0f;
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (float.IsNaN(legacyConstant)) return;
            Value = ScalableFloat.Constant(legacyConstant);
            legacyConstant = float.NaN;
        }
    }
}
