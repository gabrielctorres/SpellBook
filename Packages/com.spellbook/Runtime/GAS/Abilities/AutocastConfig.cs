using System;
using UnityEngine;

namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Autocast de uma habilidade: um interruptor por personagem que liga os gatilhos marcados "só com autocast". O estado
    /// fica no <see cref="AbilitySpec"/> e muda por <c>AbilitySystemComponent.SetAutocast</c>. Vale para
    /// <see cref="AbilityBehavior.Instant"/> e <see cref="AbilityBehavior.Channel"/>.
    /// </summary>
    [Serializable]
    public sealed class AutocastConfig
    {
        [Tooltip("Permite ligar e desligar o autocast. Desligado, o padrão, SetAutocast devolve falso.")]
        [SerializeField] private bool allowed;

        [Tooltip("O autocast nasce ligado quando a habilidade é concedida.")]
        [SerializeField] private bool startsOn;

        /// <summary>Permite ligar e desligar o autocast. Falso por padrão.</summary>
        public bool Allowed
        {
            get => allowed;
            set => allowed = value;
        }

        /// <summary>O autocast nasce ligado na concessão. Só vale com <see cref="Allowed"/>.</summary>
        public bool StartsOn
        {
            get => startsOn;
            set => startsOn = value;
        }
    }
}
