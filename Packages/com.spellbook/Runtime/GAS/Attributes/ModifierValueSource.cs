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
            Formula,
            SetByCaller,
            TargetAttribute
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

        /// <summary>Nome do valor passado por quem aplica (fonte <see cref="SourceType.SetByCaller"/>).</summary>
        [ShowIf(nameof(Type), SourceType.SetByCaller)]
        [Tooltip("Nome do valor passado por quem aplica o efeito (SetByCallerValues). Letras, números e sublinhado.")]
        public string CallerName;

        [ShowIf(nameof(Type), SourceType.SetByCaller)]
        [Tooltip("Sem o valor do chamador, vale o Caller Default. Desligado, vale 0 com aviso.")]
        public bool CallerHasDefault;

        [ShowIf(nameof(Type), SourceType.SetByCaller)]
        public float CallerDefault;

        /// <summary>Atributo lido do alvo do efeito (fonte <see cref="SourceType.TargetAttribute"/>).</summary>
        [ShowIf(nameof(Type), SourceType.TargetAttribute)]
        public AttributeDefinition TargetAttributeDefinition;

        [NonSerialized] private bool _warnedMissingCaller;

        /// <summary>A fonte lê o instigador (atributo ou fórmula); com o instigador destruído, o tique usa o último valor.</summary>
        internal bool ReadsInstigator => Type == SourceType.Attribute || Type == SourceType.Formula;

        /// <summary>Valor com <paramref name="source"/> como instigador, no nível 1 e sem tabela.</summary>
        public float GetValue(AbilitySystemComponent source, AbilitySystemComponent target)
        {
            return Evaluate(new EffectContext(source, target, null, 1, false, Vector3.zero, null));
        }

        /// <summary>Valor no <paramref name="context"/>, como o ASC avalia. Para quem está fora do caminho quente.</summary>
        public float GetValue(in EffectContext context) => Evaluate(in context);

        /// <summary>
        /// Valor no <paramref name="context"/>: o <see cref="ScalableFloat"/> no nível e na tabela do contexto; atributo e fórmula
        /// leem o instigador; o valor do chamador vem dos <see cref="EffectContext.CallerValues"/>; o atributo do alvo, do
        /// <see cref="EffectContext.Target"/>. Sem alocação.
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
                case SourceType.SetByCaller:
                    return EvaluateCaller(in context);
                case SourceType.TargetAttribute:
                    return context.Target != null ? context.Target.GetAttributeValue(TargetAttributeDefinition) : 0f;
                default:
                    return 0f;
            }
        }

        private float EvaluateCaller(in EffectContext context)
        {
            IReadOnlySetByCaller values = context.CallerValues;
            if (values != null && values.Count > 0 && values.TryGet(CallerName, out float value)) return value;
            if (CallerHasDefault) return CallerDefault;

            if (!_warnedMissingCaller)
            {
                _warnedMissingCaller = true;
                Debug.LogWarning($"[GAS] SetByCaller '{CallerName}' sem valor do chamador e sem padrão: vale 0.");
            }
            return 0f;
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
