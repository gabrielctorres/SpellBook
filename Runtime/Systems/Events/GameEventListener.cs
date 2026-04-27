using UnityEngine;
using UnityEngine.Events;

namespace SpellBook.Events
{
    /// <summary>
    /// Componente de ponte para expor o Event Bus ao Inspector da Unity.
    /// Escuta GameEvents (strings) e invoca UnityEvents.
    /// </summary>
    [AddComponentMenu("SpellBook/Events/Game Event Listener")]
    public class GameEventListener : MonoBehaviour
    {
        [Tooltip("O nome do evento (string) para escutar.")]
        public string eventNameToListen;

        [Space]
        public UnityEvent onEventTriggered;

        private void OnEnable()
        {
            EventManager.AddListener<GameEvent>(OnGameEvent);
        }

        private void OnDisable()
        {
            EventManager.RemoveListener<GameEvent>(OnGameEvent);
        }

        private void OnGameEvent(GameEvent eventData)
        {
            if (eventData.EventName == eventNameToListen)
            {
                onEventTriggered?.Invoke();
            }
        }
    }
}

