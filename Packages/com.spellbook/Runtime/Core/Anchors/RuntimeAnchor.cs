using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellBook.Core
{
    /// <summary>
    /// Base não genérica de toda âncora (ADR 0039, item 5). Deixa quem não conhece o tipo do valor, como a UI, ler e
    /// publicar o objeto e ouvir a troca. Só o <see cref="RuntimeAnchor{T}"/> deriva dela. Ao entrar em Play, a âncora
    /// volta a vazio e esquece os assinantes de <see cref="Changed"/>.
    /// </summary>
    public abstract class RuntimeAnchor : ScriptableObject, RuntimeAnchorRegistry.IAnchor
    {
        /// <summary>O objeto publicado, sem tipo, ou <c>null</c> se não houver um ou se ele foi destruído.</summary>
        public abstract UnityEngine.Object UntypedValue { get; }

        /// <summary>Tipo que a âncora aceita.</summary>
        public abstract Type ValueType { get; }

        /// <summary>Chamado com a própria âncora quando o objeto publicado muda, logo depois do evento tipado.</summary>
        public event Action<RuntimeAnchor> Changed;

        /// <summary>
        /// Publica <paramref name="value"/> se ele for do <see cref="ValueType"/>; <c>null</c> esvazia. Devolve
        /// <c>false</c>, sem mudar nada e sem avisar, se o tipo for outro.
        /// </summary>
        public abstract bool ProvideUntyped(UnityEngine.Object value);

        /// <summary>Esvazia a âncora, mas só se <paramref name="value"/> for o objeto publicado.</summary>
        public abstract void UnsetUntyped(UnityEngine.Object value);

        private protected void RaiseChanged()
        {
            Changed?.Invoke(this);
        }

        // Abstrato e privado ao assembly: só o RuntimeAnchor<T> deriva da base
        private protected abstract void ResetValue();

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
            ResetValue();
            Changed = null;
        }
    }

    /// <summary>
    /// Asset que guarda um objeto de runtime, como um componente de cena, para quem aponta o asset
    /// (ADR 0012). O valor não é salvo. Ao entrar em Play, com ou sem domain reload, toda âncora
    /// volta a vazio e esquece os assinantes de <see cref="OnChanged"/>.
    /// </summary>
    /// <typeparam name="T">Tipo do objeto publicado.</typeparam>
    public abstract class RuntimeAnchor<T> : RuntimeAnchor where T : UnityEngine.Object
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
            RaiseChanged();
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
            RaiseChanged();
        }

        /// <inheritdoc />
        public override UnityEngine.Object UntypedValue => Value;

        /// <inheritdoc />
        public override Type ValueType => typeof(T);

        /// <inheritdoc />
        public override bool ProvideUntyped(UnityEngine.Object value)
        {
            if (value is null)
            {
                Provide(null);
                return true;
            }

            if (!(value is T typed))
            {
                return false;
            }

            Provide(typed);
            return true;
        }

        /// <inheritdoc />
        public override void UnsetUntyped(UnityEngine.Object value)
        {
            if (value is T typed)
            {
                Unset(typed);
            }
        }

        private protected override void ResetValue()
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
