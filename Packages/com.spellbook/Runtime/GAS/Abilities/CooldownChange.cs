namespace SpellBook.GAS.Abilities
{
    /// <summary>Motivo de <c>AbilitySystemComponent.OnCooldownChanged</c>. Valores novos só no fim.</summary>
    public enum CooldownChange
    {
        /// <summary>A ativação aplicou a recarga.</summary>
        Started,

        /// <summary>O tempo restante mudou por código (<c>ReduceCooldown</c> ou <c>RestartCooldown</c>) e a recarga continua.</summary>
        Changed,

        /// <summary>A habilidade ficou pronta: a recarga expirou ou foi removida.</summary>
        Ended
    }
}
