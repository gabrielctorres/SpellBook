namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Até onde a consulta de ativação vai na ordem da ADR 0022 (Spec do B-016, P3). As checagens são as mesmas da
    /// tentativa; o escopo só diz onde a consulta para.
    /// </summary>
    public enum ActivationCheckScope
    {
        /// <summary>Todas as checagens, com as regras de ativação no fim, como a tentativa.</summary>
        All,

        /// <summary>
        /// Para antes das regras de ativação: pré-requisitos, tags, cargas ou recarga e custo. É a consulta do slot, que não
        /// tem alvo para as condições.
        /// </summary>
        WithoutConditions,
    }
}
