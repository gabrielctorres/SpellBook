using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Events
{
    /// <summary>
    /// Canal de evento em asset: fato global que o designer liga no Inspector (ADR 0016). Base sem
    /// tipo, para o Editor e o debugger listarem canais de qualquer payload. Os ouvintes de todos os
    /// canais vivos são limpos ao entrar em Play, com ou sem domain reload (CA19).
    /// </summary>
    public abstract class EventChannel : ScriptableObject
    {
        private static readonly List<EventChannel> _live = new List<EventChannel>();

        [SerializeField, TextArea, Tooltip("Para que serve o canal e quando ele é disparado.")]
        private string description;

        [SerializeField, Tooltip("Escreve cada disparo no console.")]
        private bool logRaises;

#if UNITY_EDITOR
        /// <summary>Observa cada disparo (canal, payload, quem disparou). É por onde o debugger grava o histórico.</summary>
        internal static Action<EventChannel, object, Object> Observed;
#endif

        static EventChannel()
        {
            EventRegistry.Register(ClearAllListeners);
        }

        /// <summary>Tipo do payload do canal.</summary>
        public abstract Type PayloadType { get; }

        /// <summary>Quantidade de ouvintes inscritos agora.</summary>
        public abstract int ListenerCount { get; }

        /// <summary>
        /// Esvazia <paramref name="targets"/> e põe nela o alvo de cada ouvinte inscrito, ou o próprio
        /// delegate quando ele é um método estático.
        /// </summary>
        public abstract void GetListeners(List<object> targets);

        internal static IReadOnlyList<EventChannel> LiveChannels => _live;

        internal bool LogRaises => logRaises;

        internal abstract void ClearListeners();

        /// <summary>Último payload disparado, como <c>object</c>, para o Inspector do canal.</summary>
        internal abstract object BoxedLastPayload { get; }

        /// <summary>Dispara o payload de teste do Inspector, sem emissor.</summary>
        internal abstract void RaiseDebugPayload();

        /// <summary>Põe o canal na limpeza de sessão. Quem sobrescreve chama a base.</summary>
        protected virtual void OnEnable()
        {
            if (!_live.Contains(this))
            {
                _live.Add(this);
            }
        }

        /// <summary>Tira o canal da limpeza de sessão. Quem sobrescreve chama a base.</summary>
        protected virtual void OnDisable()
        {
            _live.Remove(this);
        }

        private static void ClearAllListeners()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i] == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }

                _live[i].ClearListeners();
            }
        }
    }

    /// <summary>
    /// Canal com payload <typeparamref name="T"/>. Para um payload próprio, crie
    /// <c>XEventChannel : EventChannel&lt;X&gt;</c>. A ordem de chamada dos ouvintes não é garantida.
    /// </summary>
    public abstract class EventChannel<T> : EventChannel
    {
        private readonly ListenerList<T> _listeners = new ListenerList<T>();

        [SerializeField, Tooltip("Payload enviado pelo botão Raise do Inspector, no Play.")]
        private T debugPayload;

        /// <summary>Inscreve ou desinscreve um ouvinte. Inscrever duas vezes o mesmo delegate não tem efeito.</summary>
        public event Action<T> Raised
        {
            add => _listeners.Add(value);
            remove => _listeners.Remove(value);
        }

        /// <summary>Último payload disparado. Não é limpo ao entrar em Play.</summary>
        public T LastPayload { get; private set; }

        public override Type PayloadType => typeof(T);

        public override int ListenerCount => _listeners.Count;

        public override void GetListeners(List<object> targets)
        {
            _listeners.CopyTargets(targets);
        }

        /// <summary>Entrega <paramref name="payload"/> a todos os ouvintes inscritos no início do disparo.</summary>
        public void Raise(T payload)
        {
            Raise(payload, null);
        }

        /// <summary>Como <see cref="Raise(T)"/>, informando quem disparou (para o log e o debugger).</summary>
        public void Raise(T payload, Object sender)
        {
            LastPayload = payload;
#if UNITY_EDITOR
            Observed?.Invoke(this, payload, sender);
#endif
            if (LogRaises)
            {
                Debug.Log($"[Events] {name}: Raise({payload})" + (sender != null ? $" por {sender.name}" : ""), this);
            }

            _listeners.Raise(payload);
        }

        internal override void ClearListeners()
        {
            _listeners.Clear();
        }

        internal override object BoxedLastPayload => LastPayload;

        internal override void RaiseDebugPayload()
        {
            Raise(debugPayload);
        }
    }
}
