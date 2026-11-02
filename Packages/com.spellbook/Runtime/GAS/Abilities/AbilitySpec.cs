using System.Collections.Generic;
using UnityEngine;

namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Instância de uma habilidade concedida a um AbilitySystemComponent.
    /// O GameplayAbility (ScriptableObject) é compartilhado; o estado por personagem fica aqui.
    /// </summary>
    public class AbilitySpec
    {
        public GameplayAbility Ability { get; }
        /// <summary>Nível atual, em [1, MaxLevel]. Muda por <c>AbilitySystemComponent.SetAbilityLevel</c>.</summary>
        public int Level { get; internal set; }
        public bool IsActive { get; internal set; }
        public AbilityContext ActiveContext { get; internal set; }

        /// <summary>A ativação aplicou a recarga e o <c>Ended</c> ainda não saiu.</summary>
        internal bool CooldownTracked;

        /// <summary>Cargas atuais (com cargas desligadas, 0). Muda pelo ASC: ativação, recarga, <c>AddCharges</c> e <c>RefillCharges</c>.</summary>
        public int Charges { get; internal set; }

        /// <summary>Máximo de cargas avaliado pelo ASC (inteiro, pelo menos 0; com cargas desligadas, 0).</summary>
        public int MaxCharges { get; internal set; }

        /// <summary>Segundos até voltar a próxima carga; 0 com a recarga parada.</summary>
        public float ChargeRechargeRemaining { get; internal set; }

        /// <summary>Duração aplicada da recarga em curso, já com a redução de recarga; 0 com a recarga parada.</summary>
        public float ChargeRechargeDuration { get; internal set; }

        /// <summary>A recarga de carga está correndo.</summary>
        internal bool ChargeRecharging;

        internal Coroutine Routine;
        internal readonly List<Coroutine> ChildRoutines = new List<Coroutine>();

        internal AbilitySpec(GameplayAbility ability, int level)
        {
            Ability = ability;
            Level = level;
        }
    }
}
