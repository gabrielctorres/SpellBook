namespace SpellBook.Time
{
    /// <summary>
    /// Fonte de tempo injetável (ADR 0014). Só consulta: o consumidor guarda o último
    /// <see cref="ElapsedTime"/> lido e avança pela diferença.
    /// </summary>
    public interface ITimeSource
    {
        /// <summary>Tempo acumulado da fonte, em segundos.</summary>
        double ElapsedTime { get; }

        /// <summary>Delta do quadro atual, em segundos, já com a pausa da fonte aplicada.</summary>
        float DeltaTime { get; }
    }
}
