using System;

namespace SpellBook.Events
{
    /// <summary>
    /// Barramento estático por tipo de struct, para fatos globais entre sistemas de código (ADR 0016).
    /// Não aloca memória em <see cref="Raise"/> e é limpo ao entrar em Play. A ordem de chamada dos
    /// ouvintes não é garantida.
    /// </summary>
    /// <typeparam name="T">Struct do evento.</typeparam>
    public static class EventBus<T> where T : struct, IEvent
    {
        private static readonly ListenerList<T> _listeners = new ListenerList<T>();

        static EventBus()
        {
            EventRegistry.Register(_listeners.Clear);
        }

        /// <summary>Inscreve o ouvinte. Inscrever de novo o mesmo delegate não tem efeito.</summary>
        public static void Subscribe(Action<T> listener)
        {
            _listeners.Add(listener);
        }

        /// <summary>Desinscreve o ouvinte. Sem efeito se ele não estiver inscrito.</summary>
        public static void Unsubscribe(Action<T> listener)
        {
            _listeners.Remove(listener);
        }

        /// <summary>Entrega o evento a todos os ouvintes inscritos no início do disparo.</summary>
        public static void Raise(in T value)
        {
            _listeners.Raise(value);
        }
    }
}
