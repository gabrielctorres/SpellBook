using System;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Attributes
{
    /// <summary>
    /// Desvia parte do dano para um atributo de escudo do mesmo dono, até o escudo chegar ao mínimo. O escudo gasto é uma
    /// alteração redirecionada, aplicada antes da alteração original (ADR 0033, item 5). Só age em alterações negativas
    /// de efeitos com alguma das <see cref="DamageTags"/>; escudo ausente no dono: não faz nada.
    /// </summary>
    [Serializable]
    [TypeCategory("Dano/Escudo", Summary = "Desvia o dano para um atributo de escudo do dono.",
        Line = "Escudo {shield} × {fraction}")]
    [HelpURL(DocUrls.Damage)]
    public sealed class AbsorbWithAttribute : AttributeChangeProcessor
    {
        [Tooltip("Atributo do dono que absorve o dano (escudo). Gasto até o mínimo dele.")]
        [SerializeField] private AttributeDefinition shield;

        [Tooltip("Tipos de dano absorvidos, pela hierarquia. Vazio: qualquer dano, com ou sem efeito.")]
        [SerializeField] private GameplayTagContainer damageTags = new GameplayTagContainer();

        [Tooltip("Fração do dano que o escudo pode absorver, de 0 a 1. O resto vai ao atributo.")]
        [Range(0f, 1f)]
        [SerializeField] private float fraction = 1f;

        /// <summary>Atributo do dono que absorve o dano.</summary>
        public AttributeDefinition Shield
        {
            get => shield;
            set => shield = value;
        }

        /// <summary>Tipos de dano absorvidos; vazio absorve qualquer dano. Nunca nulo.</summary>
        public GameplayTagContainer DamageTags => damageTags ??= new GameplayTagContainer();

        /// <summary>Fração do dano que o escudo pode absorver, de 0 a 1 (valores fora contam como 0 ou 1).</summary>
        public float Fraction
        {
            get => fraction;
            set => fraction = value;
        }

        public override void Process(ref AttributeChange change, in EffectContext context, AttributeChangeQueue queue)
        {
            float delta = change.Delta;
            if (delta >= 0f) return;
            if (!change.HasDamageTag(DamageTags)) return;
            if (!queue.TryGetState(shield, out AttributeState state)) return;

            float absorbable = -delta * Mathf.Clamp01(fraction);
            float available = Mathf.Max(0f, state.CurrentValue - state.MinValue);
            float consumed = Mathf.Min(absorbable, available);
            if (consumed <= 0f) return;

            queue.Redirect(shield, -consumed);
            change.Delta = delta + consumed;
        }
    }
}
