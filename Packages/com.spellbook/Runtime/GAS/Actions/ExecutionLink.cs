namespace SpellBook.GAS.Actions
{
    /// <summary>
    /// Como uma execução se liga ao dono lógico dela (ADR 0036, item 4). Passado a
    /// <see cref="SpellBook.GAS.Core.AbilitySystemComponent.RunActions"/>.
    /// </summary>
    public enum ExecutionLink
    {
        /// <summary>O dono espera a execução terminar e a cancela ao ser cancelado. É o padrão.</summary>
        Holds,
        /// <summary>
        /// A execução é cancelada quando o dono termina ou é cancelado (fim da ativação, remoção da instância de efeito,
        /// <c>CancelActions</c>), e o dono não espera por ela.
        /// </summary>
        FollowsOwner,
    }
}
