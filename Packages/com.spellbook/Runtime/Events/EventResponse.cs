using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using UnityEngine;
using UnityEngine.Events;

namespace SpellBook.Events
{
    /// <summary>
    /// Liga um canal a reações num <see cref="EventListener"/>. Para um payload próprio, crie
    /// <c>XEventResponse : EventResponse&lt;X&gt;</c> ao lado do <c>XEventChannel</c>.
    /// </summary>
    [Serializable]
    public abstract class EventResponse
    {
        /// <summary>Canal que a resposta ouve, ou <c>null</c> se nenhum foi escolhido.</summary>
        public abstract EventChannel Channel { get; }

        /// <summary>Passa a ouvir o canal em nome de <paramref name="listener"/>. Sem canal, não faz nada.</summary>
        public abstract void Subscribe(EventListener listener);

        /// <summary>Para de ouvir o canal da última inscrição.</summary>
        public abstract void Unsubscribe();
    }

    /// <summary>Resposta a um canal com payload <typeparamref name="T"/>: reações na ordem da lista, depois o escape.</summary>
    [Serializable]
    public abstract class EventResponse<T> : EventResponse
    {
        [SerializeField, Tooltip("Canal que dispara esta resposta.")]
        private EventChannel<T> channel;

        [SerializeReference, TypePicker, Tooltip("Reações executadas na ordem da lista a cada disparo.")]
        private List<Reaction> reactions = new List<Reaction>();

        [SerializeField, Tooltip("UnityEvent chamado depois das reações. Use só quando nenhuma reação pronta serve.")]
        private EscapeEvent<T> escape = new EscapeEvent<T>();

        [NonSerialized] private EventChannel<T> _subscribed;
        [NonSerialized] private EventListener _listener;
        [NonSerialized] private Action<T> _handler;

        public override EventChannel Channel => channel;

        public override void Subscribe(EventListener listener)
        {
            Unsubscribe();
            if (channel == null) return;

            _listener = listener;
            _subscribed = channel;
            _handler ??= OnRaised;
            _subscribed.Raised += _handler;
        }

        public override void Unsubscribe()
        {
            if (_subscribed != null)
            {
                _subscribed.Raised -= _handler;
            }

            _subscribed = null;
            _listener = null;
        }

        private void OnRaised(T payload)
        {
            for (int i = 0; i < reactions.Count; i++)
            {
                reactions[i]?.Execute(_listener, in payload);
            }

            escape.Event?.Invoke(payload);
        }
    }

    /// <summary>
    /// UnityEvent de escape de uma resposta. Fica numa classe própria para aparecer no Inspector como foldout
    /// "Escape", fechado por padrão.
    /// </summary>
    [Serializable]
    public sealed class EscapeEvent<T>
    {
        public UnityEvent<T> Event = new UnityEvent<T>();
    }
}
