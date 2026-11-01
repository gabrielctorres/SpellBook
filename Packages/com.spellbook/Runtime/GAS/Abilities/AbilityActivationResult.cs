namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Resultado de uma tentativa de ativação. Útil para feedback de UI ("Sem mana", "Em recarga").
    /// </summary>
    public enum AbilityActivationResult
    {
        Success,
        InvalidAbility,          // Habilidade nula
        NotGranted,              // Habilidade não foi concedida ao ASC (GiveAbility)
        AlreadyActive,           // A mesma habilidade ainda está em execução
        BlockedByTags,           // O ASC possui alguma das BlockedTags
        MissingRequiredTags,     // O ASC não possui todas as RequiredTags
        BlockedByActiveAbility,  // Uma habilidade ativa bloqueia esta via BlockAbilitiesWithTag
        OnCooldown,
        CannotAffordCost
    }
}
