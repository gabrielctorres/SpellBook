namespace SpellBook.GAS.Effects
{
    /// <summary>
    /// Por que uma instância de efeito saiu do ASC, em <see cref="Core.AbilitySystemComponent.OnEffectRemoved"/>.
    /// Valores novos entram só no fim.
    /// </summary>
    public enum EffectRemovalReason
    {
        /// <summary>A duração acabou.</summary>
        Expired,
        /// <summary>Removida por código, com <c>RemoveEffect</c> ou <c>RemoveEffects</c>.</summary>
        Removed,
        /// <summary>Removida por <c>RemoveEffectsWithTag</c>.</summary>
        RemovedByTag,
        /// <summary>As pilhas chegaram a zero.</summary>
        StacksDepleted,
    }
}
