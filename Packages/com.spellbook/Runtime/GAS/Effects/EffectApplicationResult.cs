namespace SpellBook.GAS.Effects
{
    /// <summary>
    /// Desfecho de <see cref="Core.AbilitySystemComponent.TryApplyEffect(GameplayEffect, Core.AbilitySystemComponent, out ActiveEffectInstance)"/>.
    /// Valores novos entram só no fim.
    /// </summary>
    public enum EffectApplicationResult
    {
        /// <summary>Instância nova de efeito com duração ou infinito.</summary>
        Applied,
        /// <summary>Reaplicação de efeito com <c>CanStack</c>, mesmo em <c>MaxStacks</c>.</summary>
        Stacked,
        /// <summary>Efeito instantâneo aplicado; não há instância.</summary>
        AppliedInstant,
        /// <summary>O alvo tem uma das <c>ApplicationImmunityTags</c>.</summary>
        BlockedByImmunity,
        /// <summary>Falta ao alvo alguma das <c>ApplicationRequirements</c>.</summary>
        MissingRequirements,
        /// <summary>Efeito nulo.</summary>
        Invalid,
        /// <summary>Reaplicação de efeito sem <c>CanStack</c>: a instância existente foi renovada.</summary>
        Refreshed,
    }
}
