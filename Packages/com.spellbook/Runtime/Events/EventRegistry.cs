using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>
    /// Ações de limpeza dos registros estáticos do módulo. Rodam ao entrar em Play, com ou sem
    /// domain reload, para nenhum ouvinte de uma sessão anterior continuar inscrito (ADR 0016, CA19).
    /// </summary>
    internal static class EventRegistry
    {
        private static readonly List<Action> _clearActions = new List<Action>();

        /// <summary>
        /// Registra uma ação de limpeza. A lista não é esvaziada no reset: sem domain reload, quem
        /// se registrou numa sessão anterior não se registra de novo.
        /// </summary>
        public static void Register(Action clear)
        {
            if (!_clearActions.Contains(clear))
            {
                _clearActions.Add(clear);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetAllOnSessionStart()
        {
            for (int i = 0; i < _clearActions.Count; i++)
            {
                _clearActions[i]();
            }
        }
    }
}
