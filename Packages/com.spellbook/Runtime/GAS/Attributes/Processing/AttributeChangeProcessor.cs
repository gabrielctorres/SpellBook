using System;
using SpellBook.Core.Validation;
using SpellBook.GAS.Effects;

namespace SpellBook.GAS.Attributes
{
    /// <summary>
    /// Passo do pipeline de pré-alteração de um atributo (ADR 0033). Roda, na ordem da lista da
    /// <see cref="AttributeDefinition"/>, em toda mudança de base por efeito instantâneo, tique ou
    /// <c>ApplyAttributeChange</c>; modificadores persistentes não passam por aqui.
    /// </summary>
    [Serializable]
    public abstract class AttributeChangeProcessor
    {
        /// <summary>
        /// Muda o <see cref="AttributeChange.Delta"/> da alteração. A <paramref name="queue"/> dá acesso aos atributos do
        /// dono. Numa alteração por código, o efeito é nulo e o contexto é vazio.
        /// </summary>
        public abstract void Process(ref AttributeChange change, in EffectContext context, AttributeChangeQueue queue);

        /// <summary>Confere a configuração do processador no asset de <paramref name="owner"/>.</summary>
        public virtual void Validate(ValidationResult result, string propertyPath, AttributeDefinition owner) { }
    }
}
