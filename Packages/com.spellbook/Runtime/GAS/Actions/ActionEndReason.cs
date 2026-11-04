namespace SpellBook.GAS.Actions
{
    /// <summary>
    /// Por que um projétil ou um thinker terminou, lido em <see cref="ActionContext.EndReason"/> na lista "Ao terminar".
    /// </summary>
    public enum ActionEndReason
    {
        /// <summary>Fora de uma lista "Ao terminar".</summary>
        None,
        /// <summary>O projétil gastou a perfuração.</summary>
        Hit,
        /// <summary>O <c>Lifetime</c> do projétil venceu.</summary>
        Lifetime,
        /// <summary>O projétil bateu num colisor sem ASC.</summary>
        Obstacle,
        /// <summary>Devolvido por código.</summary>
        Released,
        /// <summary>A duração do thinker venceu.</summary>
        Expired,
        /// <summary>Quem criou o thinker sumiu ou foi desativado.</summary>
        OwnerGone,
        /// <summary>A execução que criou o thinker terminou ou foi cancelada.</summary>
        ExecutionEnded,
    }
}
