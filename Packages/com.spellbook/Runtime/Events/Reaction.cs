using System;

namespace SpellBook.Events
{
    /// <summary>
    /// Ação ligada a uma resposta de um <see cref="EventListener"/>, escolhida no Inspector pelo seletor de tipo.
    /// As reações de uma resposta rodam na ordem da lista, antes do escape (<see cref="EscapeEvent{T}"/>).
    /// </summary>
    [Serializable]
    public abstract class Reaction
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
    }
}
