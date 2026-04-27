namespace SpellBook.Events
{
    /// <summary>
    /// Evento simples baseado em string para prototipagem rápida e uso via Inspector.
    /// Implementado como struct para evitar alocação no Heap.
    /// </summary>
    public struct GameEvent : IEvent
    {
        public readonly string EventName;

        public GameEvent(string eventName)
        {
            EventName = eventName;
        }
    }
}

