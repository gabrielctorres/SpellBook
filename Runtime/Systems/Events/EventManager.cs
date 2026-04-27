using System;
using System.Collections.Generic;

namespace SpellBook.Events
{
    /// <summary>
    /// Barramento de Eventos estático de alta performance.
    /// Otimizado para zero-allocation durante o trigger usando structs C#.
    /// </summary>
    public static class EventManager
    {
        private static readonly Dictionary<Type, Delegate> _listeners = new Dictionary<Type, Delegate>();

        /// <summary>
        /// Inscreve um listener para um tipo específico de evento.
        /// </summary>
        public static void AddListener<T>(Action<T> listener) where T : struct, IEvent
        {
            Type eventType = typeof(T);
            if (_listeners.TryGetValue(eventType, out Delegate d))
            {
                _listeners[eventType] = Delegate.Combine(d, listener);
            }
            else
            {
                _listeners[eventType] = listener;
            }
        }

        /// <summary>
        /// Remove a inscrição de um listener.
        /// </summary>
        public static void RemoveListener<T>(Action<T> listener) where T : struct, IEvent
        {
            Type eventType = typeof(T);
            if (_listeners.TryGetValue(eventType, out Delegate d))
            {
                Delegate currentDel = Delegate.Remove(d, listener);
                if (currentDel == null)
                {
                    _listeners.Remove(eventType);
                }
                else
                {
                    _listeners[eventType] = currentDel;
                }
            }
        }

        /// <summary>
        /// Dispara um evento para todos os inscritos.
        /// Zero alocação de GC se T for uma struct.
        /// </summary>
        public static void Trigger<T>(T eventData) where T : struct, IEvent
        {
            if (_listeners.TryGetValue(typeof(T), out Delegate d))
            {
                (d as Action<T>)?.Invoke(eventData);
            }
        }
    }
}

