using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SmitePackage.Core.Events
{
    [System.Serializable]
    public class CustomGameEvent : UnityEvent<Component, object> { }
    public class GameEventListner : MonoBehaviour
    {
        public GameEvent gameEvent;
        public CustomGameEvent response;
        private void OnEnable()
        {
            gameEvent.RegisterLister(this);
        }
        private void OnDisable()
        {
            gameEvent.UnregisterListener(this);
        }

        public void OnEventRaise(Component sender, object data)
        {
            response.Invoke(sender, data);
        }
    }
}