using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;

namespace SpellBook.Events
{
    /// <summary>
    /// Disparado quando um slot de habilidade precisa ser atualizado (ex: troca de arma/build).
    /// </summary>
    public struct AbilitySlotUpdateEvent : IEvent
    {
        public readonly int SlotIndex;
        public readonly GameplayAbility Ability;
        public readonly string Keybind;
        public readonly AbilitySystemComponent ASC;

        public AbilitySlotUpdateEvent(int slotIndex, GameplayAbility ability, string keybind, AbilitySystemComponent asc)
        {
            SlotIndex = slotIndex;
            Ability = ability;
            Keybind = keybind;
            ASC = asc;
        }
    }

    /// <summary>
    /// Disparado quando uma habilidade é ativada com sucesso.
    /// Útil para UI de Cooldown e VFX globais.
    /// </summary>
    public struct AbilityActivatedEvent : IEvent
    {
        public readonly GameplayAbility Ability;
        public readonly AbilitySystemComponent ASC;

        public AbilityActivatedEvent(GameplayAbility ability, AbilitySystemComponent asc)
        {
            Ability = ability;
            ASC = asc;
        }
    }
}

