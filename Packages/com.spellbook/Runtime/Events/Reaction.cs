using System;
using SpellBook.Core.Validation;

namespace SpellBook.Events
{
    /// <summary>
    /// Ação ligada a uma resposta de um <see cref="EventListener"/>, escolhida no Inspector pelo seletor de tipo.
    /// As reações de uma resposta rodam na ordem da lista, antes do escape (<see cref="EscapeEvent{T}"/>).
    /// </summary>
    [Serializable]
    public abstract class Reaction : IValidatable
    {
        /// <summary>Executa a reação sem olhar o payload. É o que as reações prontas sem payload sobrescrevem.</summary>
        public virtual void Execute(EventListener listener)
        {
        }

        /// <summary>Executa a reação com o payload do disparo. Por padrão, chama <see cref="Execute(EventListener)"/>.</summary>
        public virtual void Execute<T>(EventListener listener, in T payload)
        {
            Execute(listener);
        }

        /// <summary>Acusa campos obrigatórios vazios. Por padrão, não acusa nada.</summary>
        public virtual void Validate(ValidationResult result)
        {
        }

        /// <summary>
        /// Valida sabendo o tipo do payload da resposta que contém a reação. Por padrão, chama
        /// <see cref="Validate(ValidationResult)"/>.
        /// </summary>
        public virtual void Validate(ValidationResult result, Type payloadType)
        {
            Validate(result);
        }
    }
}
