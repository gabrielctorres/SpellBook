using System;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using UnityEngine;

namespace SpellBook.GAS.Conditions
{
    /// <summary>
    /// Compara o valor atual de um atributo do <see cref="ConditionContext.Owner"/> com um valor (ou, com
    /// <see cref="UseRatio"/>, o valor dividido pelo máximo efetivo). O valor é avaliado com um <see cref="EffectContext"/>
    /// em que o dono é instigador e alvo, na habilidade e no nível do contexto. Atributo ausente ou sem dono: falsa, sem aviso.
    /// </summary>
    [Serializable]
    [TypeCategory("Atributos", Summary = "Compara um atributo do dono com um valor.",
        Line = "{attribute} {comparison} {value.Value}{useRatio? (proporção)}")]
    public sealed class AttributeThresholdCondition : Condition<ConditionContext>
    {
        /// <summary>Como o valor do atributo é comparado.</summary>
        public enum Comparison
        {
            Greater,
            GreaterOrEqual,
            Less,
            LessOrEqual,
            Equal
        }

        /// <summary>Tolerância da comparação <see cref="Comparison.Equal"/>.</summary>
        public const float EqualTolerance = 1e-4f;

        [SerializeField] private AttributeDefinition attribute;
        [SerializeField] private Comparison comparison = Comparison.GreaterOrEqual;
        [SerializeField] private ModifierValueSource value = new ModifierValueSource();
        [Tooltip("Compara valor ÷ máximo efetivo do atributo. Máximo ≤ 0 dá falso.")]
        [SerializeField] private bool useRatio;

        /// <summary>Atributo lido no dono.</summary>
        public AttributeDefinition Attribute
        {
            get => attribute;
            set => attribute = value;
        }

        /// <summary>Comparação entre o atributo (à esquerda) e o valor.</summary>
        public Comparison ComparisonType
        {
            get => comparison;
            set => comparison = value;
        }

        /// <summary>Valor comparado; <c>SetByCaller</c> e <c>TargetAttribute</c> não têm sentido aqui.</summary>
        public ModifierValueSource Value => value;

        /// <summary>Compara a proporção valor ÷ máximo efetivo em vez do valor.</summary>
        public bool UseRatio
        {
            get => useRatio;
            set => useRatio = value;
        }

        public override string DefaultFailureKey => "gas.activation.attribute_threshold";

        public override bool Evaluate(in ConditionContext context)
        {
            if (context.Owner == null || context.Owner.AttributeSet == null) return false;
            if (!context.Owner.AttributeSet.TryGetState(attribute, out AttributeState state)) return false;

            float current = state.CurrentValue;
            if (useRatio)
            {
                float max = state.MaxValue;
                if (!(max > 0f)) return false;
                current /= max;
            }

            var effectContext = new EffectContext(context.Owner, context.Owner, context.Ability, context.Level, false, Vector3.zero, null);
            float threshold = value != null ? value.GetValue(in effectContext) : 0f;
            return Compare(current, threshold);
        }

        private bool Compare(float current, float threshold)
        {
            switch (comparison)
            {
                case Comparison.Greater:
                    return current > threshold;
                case Comparison.GreaterOrEqual:
                    return current >= threshold;
                case Comparison.Less:
                    return current < threshold;
                case Comparison.LessOrEqual:
                    return current <= threshold;
                case Comparison.Equal:
                    return Math.Abs(current - threshold) <= EqualTolerance;
                default:
                    return false;
            }
        }
    }
}
