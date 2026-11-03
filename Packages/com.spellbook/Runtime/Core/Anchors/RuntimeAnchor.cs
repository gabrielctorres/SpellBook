using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellBook.Core
{
    /// <summary>
    /// Asset que guarda um objeto de runtime, como um componente de cena, para quem aponta o asset
    /// (ADR 0012). O valor não é salvo. Ao entrar em Play, com ou sem domain reload, toda âncora
    /// volta a vazio e esquece os assinantes de <see cref="OnChanged"/>.
    /// </summary>
    /// <typeparam name="T">Tipo do objeto publicado.</typeparam>
    public abstract class RuntimeAnchor<T> : ScriptableObject, RuntimeAnchorRegistry.IAnchor where T : UnityEngine.Object
    {
        [NonSerialized]
        private T _value;

        /// <summary>O objeto publicado, ou <c>null</c> se não houver um ou se ele foi destruído.</summary>
        public T Value => _value != null ? _value : null;

        /// <summary>Verdadeiro se há um objeto publicado que não foi destruído.</summary>
        public bool HasValue => _value != null;

        /// <summary>Chamado com o novo valor (ou <c>null</c>) quando o objeto publicado muda.</summary>
        public event Action<T> OnChanged;

        /// <summary>Publica <paramref name="value"/>. Só avisa se o valor mudou.</summary>
        public void Provide(T value)
        {
            if (ReferenceEquals(_value, value))
            {
                return;
            }

            _value = value;
            OnChanged?.Invoke(Value);
        }

        /// <summary>Esvazia a âncora, mas só se <paramref name="value"/> for o objeto publicado.</summary>
        public void Unset(T value)
        {
            if (value is null || !ReferenceEquals(_value, value))
            {
                return;
            }

            _value = null;
            OnChanged?.Invoke(null);
        }

        /// <summary>Registra a instância para o reset de sessão. Quem sobrescreve chama a base.</summary>
        protected virtual void OnEnable()
        {
            RuntimeAnchorRegistry.Register(this);
        }

        /// <summary>Remove a instância do registro. Quem sobrescreve chama a base.</summary>
        protected virtual void OnDisable()
        {
            RuntimeAnchorRegistry.Unregister(this);
        }

        void RuntimeAnchorRegistry.IAnchor.ResetState()
        {
            _value = null;
            OnChanged = null;
        }
    }

    /// <summary>
    /// Registro das âncoras vivas de todos os tipos. Fica fora da classe genérica porque a Unity não
    /// chama <c>RuntimeInitializeOnLoadMethod</c> em tipo genérico.
    /// </summary>
    internal static class RuntimeAnchorRegistry
    {
        internal interface IAnchor
        {
            void ResetState();
        }

        private static readonly List<IAnchor> _live = new List<IAnchor>();

        internal static void Register(IAnchor anchor)
        {
            if (!_live.Contains(anchor))
            {
                _live.Add(anchor);
            }
        }

        internal static void Unregister(IAnchor anchor)
        {
            _live.Remove(anchor);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetAllOnSessionStart()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i] is UnityEngine.Object obj && obj == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }

                _live[i].ResetState();
            }
        }
    }
}
