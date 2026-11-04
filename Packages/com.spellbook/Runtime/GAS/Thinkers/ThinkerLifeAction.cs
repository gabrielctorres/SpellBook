using SpellBook.GAS.Actions;

namespace SpellBook.GAS.Thinkers
{
    /// <summary>
    /// Vida de um <see cref="GameplayThinker"/> (ADR 0036, item 3): roda "Ao criar" e conta pulsos e duração com
    /// <see cref="ActionStatus.Wait"/>, no cursor lógico do ASC do thinker. Pulso e fim no mesmo instante: o pulso
    /// primeiro. Uma instância por thinker, fora do catálogo; o estado da vida fica no quadro.
    /// </summary>
    internal sealed class ThinkerLifeAction : GameplayAction
    {
        private const int Running = 1;
        private readonly GameplayThinker _thinker;

        public ThinkerLifeAction(GameplayThinker thinker) => _thinker = thinker;

        public override bool UsesTargets => false;

        // frame.Count: pulsos já rodados. O instante de cada marco sai dele, sem acumular erro entre esperas.
        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            float duration = _thinker.LifeDuration;
            float interval = _thinker.LifeInterval;
            if (frame.Phase == 0)
            {
                frame.Phase = Running;
                _thinker.RunCreated();
                if (_thinker.HasEnded) return ActionStatus.Done;
                if (duration <= 0f && interval <= 0f) return ActionStatus.WaitUntilCancelled;
                return ActionStatus.Wait((float)NextMark(frame.Count, duration, interval));
            }

            // Acordou num marco: o pulso, se é a vez dele, e depois o fim
            double now = NextMark(frame.Count, duration, interval);
            if (interval > 0f && (double)(frame.Count + 1) * interval <= now)
            {
                frame.Count++;
                _thinker.RunPulse();
                if (_thinker.HasEnded) return ActionStatus.Done;
            }
            if (duration > 0f && now >= duration)
            {
                _thinker.End(ActionEndReason.Expired);
                return ActionStatus.Done;
            }
            return ActionStatus.Wait((float)(NextMark(frame.Count, duration, interval) - now));
        }

        // Instante, desde o começo da vida, do próximo pulso ou do fim, o que vier antes
        private static double NextMark(int pulses, float duration, float interval)
        {
            double pulse = interval > 0f ? (double)(pulses + 1) * interval : double.PositiveInfinity;
            double end = duration > 0f ? duration : double.PositiveInfinity;
            return pulse < end ? pulse : end;
        }
    }
}
