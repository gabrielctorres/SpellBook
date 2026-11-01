using System;
using SpellBook.Core.Validation;

namespace SpellBook.Core.Conditions
{
    /// <summary>
    /// Predicado puro sobre um contexto de módulo (ADR 0019 e 0032). Cada módulo define um contexto (<c>struct</c>) e
    /// herda daqui as condições dele. Uma condição não guarda motivo de falha, estado nem cache mutável e não aloca ao avaliar.
    /// </summary>
    /// <typeparam name="TContext">Contexto de avaliação do módulo, passado por referência somente leitura.</typeparam>
    [Serializable]
    public abstract class Condition<TContext> where TContext : struct
    {
        /// <summary><c>true</c> se a condição vale no contexto. Não pode alterar estado nem alocar.</summary>
        public abstract bool Evaluate(in TContext context);

        /// <summary>
        /// Chave de texto (ADR 0023) do motivo padrão quando o ponto de uso não configura um; nunca texto pronto.
        /// <c>null</c> se a condição não tem motivo padrão.
        /// </summary>
        public virtual string DefaultFailureKey => null;

        /// <summary>Acrescenta ao <paramref name="result"/> os problemas de configuração, no caminho <paramref name="propertyPath"/>.</summary>
        public virtual void Validate(ValidationResult result, string propertyPath)
        {
        }
    }
}
