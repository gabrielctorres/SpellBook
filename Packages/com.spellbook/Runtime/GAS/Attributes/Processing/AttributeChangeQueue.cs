using System.Collections.Generic;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Attributes
{
    /// <summary>
    /// Fila do pipeline de pré-alteração, uma por ASC (ADR 0033). Dá aos processadores os atributos do dono e guarda as
    /// alterações redirecionadas, que passam pelos processadores do atributo de destino e são aplicadas antes da
    /// alteração que as gerou (item 5).
    /// </summary>
    public sealed class AttributeChangeQueue
    {
        internal readonly struct Entry
        {
            public Entry(AttributeDefinition target, float delta)
            {
                Target = target;
                Delta = delta;
            }

            public AttributeDefinition Target { get; }
            public float Delta { get; }
        }

        // Pré-alocada; cresce uma vez e é reutilizada, com um trecho por nível do pipeline
        private readonly List<Entry> _entries = new List<Entry>(8);

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

        /// <summary>
        /// Pede uma alteração adicional de <paramref name="delta"/> em <paramref name="target"/>, do mesmo dono, aplicada
        /// antes da alteração em curso. Atributo nulo ou ausente do dono é ignorado.
        /// </summary>
        public void Redirect(AttributeDefinition target, float delta)
        {
            if (target == null) return;
            _entries.Add(new Entry(target, delta));
        }

        internal int Count => _entries.Count;

        internal Entry this[int index] => _entries[index];

        /// <summary>Descarta as entradas a partir de <paramref name="count"/>, o início do trecho do nível que terminou.</summary>
        internal void TrimTo(int count)
        {
            if (count < _entries.Count) _entries.RemoveRange(count, _entries.Count - count);
        }
    }
}
