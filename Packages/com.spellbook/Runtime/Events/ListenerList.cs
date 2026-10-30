using System;
using System.Collections.Generic;

namespace SpellBook.Events
{
    /// <summary>
    /// Lista de ouvintes segura contra reentrada, usada pelo barramento e pelos canais (Spec 4.7).
    /// O disparo fixa a contagem no início: quem se inscreve durante o disparo só recebe o próximo.
    /// Quem se desinscreve durante o disparo vira <c>null</c> na posição e a lista é compactada ao
    /// fim do disparo mais externo, então nenhum ouvinte é pulado nem chamado duas vezes.
    /// </summary>
    internal sealed class ListenerList<T>
    {
        private readonly List<Action<T>> _listeners = new List<Action<T>>();
        private int _raiseDepth;
        private bool _dirty;

        /// <summary>Inscreve o ouvinte. Inscrever de novo o mesmo delegate não tem efeito.</summary>
        public void Add(Action<T> listener)
        {
            if (listener == null || _listeners.Contains(listener))
            {
                return;
            }

            _listeners.Add(listener);
        }

        /// <summary>Desinscreve o ouvinte. Sem efeito se ele não estiver inscrito.</summary>
        public void Remove(Action<T> listener)
        {
            int index = _listeners.IndexOf(listener);
            if (index < 0)
            {
                return;
            }

            if (_raiseDepth > 0)
            {
                _listeners[index] = null;
                _dirty = true;
            }
            else
            {
                _listeners.RemoveAt(index);
            }
        }

        /// <summary>Desinscreve todos os ouvintes.</summary>
        public void Clear()
        {
            if (_raiseDepth > 0)
            {
                for (int i = 0; i < _listeners.Count; i++)
                {
                    _listeners[i] = null;
                }

                _dirty = true;
                return;
            }

            _listeners.Clear();
            _dirty = false;
        }

        /// <summary>
        /// Chama cada ouvinte inscrito no início do disparo. No Editor e em development build, a
        /// exceção de um ouvinte vai para o console e os outros continuam; em release, ela sobe.
        /// </summary>
        public void Raise(in T value)
        {
            int count = _listeners.Count;
            _raiseDepth++;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Action<T> listener = _listeners[i];
                    if (listener == null)
                    {
                        continue;
                    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    try
                    {
                        listener(value);
                    }
                    catch (Exception exception)
                    {
                        UnityEngine.Debug.LogException(exception);
                    }
#else
                    listener(value);
#endif
                }
            }
            finally
            {
                _raiseDepth--;
                if (_raiseDepth == 0 && _dirty)
                {
                    _listeners.RemoveAll(listener => listener == null);
                    _dirty = false;
                }
            }
        }
    }
}
