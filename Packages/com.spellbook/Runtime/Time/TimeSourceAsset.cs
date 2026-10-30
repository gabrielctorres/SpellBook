using System.Collections.Generic;
using UnityEngine;

namespace SpellBook.Time
{
    /// <summary>
    /// Fonte de tempo reutilizável como asset (ADR 0014). As instâncias vivas ficam num registro
    /// estático e voltam ao estado inicial ao entrar em Play, com ou sem domain reload (PRD P20).
    /// </summary>
    public abstract class TimeSourceAsset : ScriptableObject, ITimeSource
    {
        private static readonly List<TimeSourceAsset> _live = new List<TimeSourceAsset>();
        private static TimeSourceAsset _scaled;

        public abstract double ElapsedTime { get; }
        public abstract float DeltaTime { get; }

        /// <summary>Fonte escalada padrão, usada quando um campo de fonte fica vazio. Não é salva em disco.</summary>
        public static TimeSourceAsset Scaled
        {
            get
            {
                if (_scaled == null)
                {
                    _scaled = CreateInstance<ScaledTimeSource>();
                    _scaled.name = "Scaled Time Source (padrão)";
                    _scaled.hideFlags = HideFlags.DontSave;
                }

                return _scaled;
            }
        }

        /// <summary>Volta a fonte ao estado inicial. Chamado ao entrar em Play.</summary>
        protected virtual void ResetState()
        {
        }

        /// <summary>Registra a instância para o reset de sessão. Quem sobrescreve chama a base.</summary>
        protected virtual void OnEnable()
        {
            if (!_live.Contains(this))
            {
                _live.Add(this);
            }
        }

        /// <summary>Remove a instância do registro. Quem sobrescreve chama a base.</summary>
        protected virtual void OnDisable()
        {
            _live.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetAllOnSessionStart()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i] == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }

                _live[i].ResetState();
            }
        }
    }
}
