using System.Collections.Generic;
using UnityEngine;

namespace SpellBook.Core.NewEventSystem
{
    public abstract class BaseGameEvent<T> : ScriptableObject
    {
        private readonly List<IGameEventListener<T>> listeners = new List<IGameEventListener<T>>();
        public void Raise(T item)
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                listeners[i].OnEventRaised(item);
            }
        }
        public void RegisterLister(IGameEventListener<T> listener)
        {
            if (!listeners.Contains(listener))
            {
                listeners.Add(listener);
            }
        }
        public void UnregisterListener(IGameEventListener<T> listener)
        {
            if (listeners.Contains(listener))
            {
                listeners.Remove(listener);
            }
        }

    }

}
