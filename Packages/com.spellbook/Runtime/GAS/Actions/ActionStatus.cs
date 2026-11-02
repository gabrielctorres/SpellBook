namespace SpellBook.GAS.Actions
{
    /// <summary>
    /// Estado de uma <see cref="GameplayAction"/> numa execução. O executor guarda um quadro por ação em curso e o
    /// passa por referência a cada chamada; a ação guarda aqui o que precisa entre chamadas, nunca em campos próprios.
    /// </summary>
    public struct ActionFrame
    {
        /// <summary>Fase da ação. Começa em 0 na primeira chamada.</summary>
        public int Phase;
        /// <summary>Índice livre, como a repetição atual.</summary>
        public int Index;
        /// <summary>Contador livre.</summary>
        public int Count;
        /// <summary>Referência livre. Atribuir um objeto novo a cada execução aloca.</summary>
        public object Data;
    }

    /// <summary>O que o executor faz depois de uma chamada a <see cref="GameplayAction.Execute"/>.</summary>
    public readonly struct ActionStatus
    {
        internal enum StatusKind : byte { Done, Wait }

        internal readonly StatusKind Kind;
        internal readonly float Seconds;

        private ActionStatus(StatusKind kind, float seconds)
        {
            Kind = kind;
            Seconds = seconds;
        }

        /// <summary>A ação terminou; o executor passa à próxima.</summary>
        public static ActionStatus Done => default;

        /// <summary>
        /// Chama a ação de novo depois de <paramref name="seconds"/> segundos no tempo do ASC, contados do fim do passo
        /// anterior. Zero ou negativo não espera: a ação é chamada de novo na mesma chamada.
        /// </summary>
        public static ActionStatus Wait(float seconds) => new ActionStatus(StatusKind.Wait, seconds);
    }
}
