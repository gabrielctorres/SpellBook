using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpellBook.Core.Events
{
    [CreateAssetMenu(fileName = "New Event", menuName = "SmitePackage/Core/GameEvent", order = 0)]
    public class GameEvent : ScriptableObject
    {
        private List<GameEventListner> listners = new List<GameEventListner>();

        public void Raise(Component sender, object data)
        {
            for (int i = listners.Count - 1; i >= 0; i--)
            {
                listners[i].OnEventRaise(sender, data);
            }
        }
        public void RegisterLister(GameEventListner listner)
        {
            if (!listners.Contains(listner))
                listners.Add(listner);
        }
        public void UnregisterListener(GameEventListner listner)
        {
            if (listners.Contains(listner))
                listners.Remove(listner);
        }
    }
}