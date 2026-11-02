using SpellBook.GAS.Core;

namespace SpellBook.GAS.Attributes
{
    /// <summary>
    /// Fila do pipeline de pré-alteração, uma por ASC (ADR 0033). Dá aos processadores os atributos do dono.
    /// </summary>
    public sealed class AttributeChangeQueue
    {
        internal AttributeChangeQueue(AbilitySystemComponent owner)
        {
            Owner = owner;
        }

        /// <summary>ASC dono dos atributos que o pipeline altera.</summary>
        public AbilitySystemComponent Owner { get; }

        /// <summary>Estado de <paramref name="def"/> no <c>AttributeSet</c> do dono; falso se o dono não tem o atributo.</summary>
        public bool TryGetState(AttributeDefinition def, out AttributeState state)
        {
            state = null;
            AttributeSet set = Owner.AttributeSet;
            return def != null && set != null && set.TryGetState(def, out state);
        }
    }
}
