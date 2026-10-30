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
        public int Level { get; set; }
        public bool IsActive { get; internal set; }
        public AbilityContext ActiveContext { get; internal set; }

        internal Coroutine Routine;
        internal readonly List<Coroutine> ChildRoutines = new List<Coroutine>();

        internal AbilitySpec(GameplayAbility ability, int level)
        {
            Ability = ability;
            Level = level;
        }
    }
}
