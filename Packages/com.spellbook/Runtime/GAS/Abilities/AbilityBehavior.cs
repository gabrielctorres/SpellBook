namespace SpellBook.GAS.Abilities
{
    /// <summary>Como a ativação de uma habilidade dura.</summary>
    public enum AbilityBehavior
    {
        /// <summary>Termina quando a lista "Ao ativar" acaba.</summary>
        Instant,
        /// <summary>Ativa sozinha na concessão e continua ativa até ser retirada ou cancelada.</summary>
        Passive,
        /// <summary>Ativar liga e ativar de novo desliga. O custo só é cobrado ao ligar.</summary>
        Toggle,
        /// <summary>Canaliza pela duração, ou até ser cancelada, e roda "A cada passo" no intervalo.</summary>
        Channel,
    }
}
