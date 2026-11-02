using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Attributes
{
    /// <summary>Como <see cref="ReduceByAttribute"/> usa o valor do atributo de origem.</summary>
    public enum ReductionMode
    {
        /// <summary>Reduz |Delta| × valor, com o valor limitado a 0..1 (0,5 = metade).</summary>
        Percent,

        /// <summary>Reduz até o valor, sem passar de |Delta|: a alteração nunca troca de sinal.</summary>
        Flat
    }

    /// <summary>
    /// Reduz a alteração pelo valor atual de um atributo do dono (resistência, armadura), só para efeitos com alguma das
    /// <see cref="DamageTags"/>. Atributo ausente no dono: não faz nada.
    /// </summary>
    [Serializable]
    [TypeCategory("Dano/Redução", Summary = "Reduz a alteração pelo valor de um atributo do dono.",
        Line = "{sourceAttribute} {mode}{onlyNegative? (só dano)}")]
    [HelpURL(DocUrls.Damage)]
    public sealed class ReduceByAttribute : AttributeChangeProcessor
    {
        [Tooltip("Atributo do dono que dá a redução (resistência, armadura).")]
        [SerializeField] private AttributeDefinition sourceAttribute;

        [Tooltip("Percent: reduz |Delta| × valor (0 a 1). Flat: reduz até o valor, sem trocar o sinal.")]
        [SerializeField] private ReductionMode mode = ReductionMode.Percent;

        [Tooltip("Tipos de dano reduzidos, pela hierarquia. Vazio: qualquer alteração, com ou sem efeito.")]
        [SerializeField] private GameplayTagContainer damageTags = new GameplayTagContainer();

        [Tooltip("Só reduz alterações negativas (dano); curas passam inteiras.")]
        [SerializeField] private bool onlyNegative = true;

        /// <summary>Atributo do dono que dá a redução.</summary>
        public AttributeDefinition SourceAttribute
        {
            get => sourceAttribute;
            set => sourceAttribute = value;
        }

        /// <summary>Como o valor do atributo reduz a alteração.</summary>
        public ReductionMode Mode
        {
            get => mode;
            set => mode = value;
        }

        /// <summary>Tipos de dano reduzidos; vazio reduz qualquer alteração. Nunca nulo.</summary>
        public GameplayTagContainer DamageTags => damageTags ??= new GameplayTagContainer();

        /// <summary>Só reduz alterações negativas. Ligado por padrão.</summary>
        public bool OnlyNegative
        {
            get => onlyNegative;
            set => onlyNegative = value;
        }

        public override void Process(ref AttributeChange change, in EffectContext context, AttributeChangeQueue queue)
        {
            float delta = change.Delta;
            if (delta == 0f || (onlyNegative && delta > 0f)) return;
            if (!change.HasDamageTag(DamageTags)) return;
            if (!queue.TryGetState(sourceAttribute, out AttributeState source)) return;

            float magnitude = Mathf.Abs(delta);
            float reduction = mode == ReductionMode.Percent
                ? magnitude * Mathf.Clamp01(source.CurrentValue)
                : Mathf.Clamp(source.CurrentValue, 0f, magnitude);
            change.Delta = Mathf.Sign(delta) * (magnitude - reduction);
        }

        /// <summary>Erro: sem atributo de origem.</summary>
        public override void Validate(ValidationResult result, string propertyPath, AttributeDefinition owner)
        {
            if (sourceAttribute == null)
            {
                result.AddError("ReduceByAttribute sem atributo: escolha o Source Attribute.", propertyPath + "." + nameof(sourceAttribute));
            }
        }

        internal override void CollectAttributes(List<AttributeDefinition> attributes)
        {
            if (sourceAttribute != null) attributes.Add(sourceAttribute);
        }
    }
}
