using SpellBook.GAS.Actions;

namespace SpellBook.GAS.Thinkers
{
    /// <summary>
    /// Vigia de um thinker com <see cref="GameplayThinker.EndWithExecution"/> (ADR 0036, item 5): roda no ASC do criador,
    /// com o vínculo <see cref="ExecutionLink.FollowsOwner"/>, e espera até ser cancelada. O cancelamento, quando o dono da
    /// execução termina ou é cancelado, termina o thinker com <see cref="ActionEndReason.ExecutionEnded"/>, ou com
    /// <see cref="ActionEndReason.OwnerGone"/> se o ASC do criador saiu (ADR 0036, item 6). Sem estado: o
    /// thinker fica no quadro. Fora do catálogo.
    /// </summary>
    internal sealed class ThinkerWatchAction : GameplayAction
    {
        /// <summary>Lista de uma vigia, compartilhada por todas as execuções.</summary>
        internal static readonly GameplayAction[] List = { new ThinkerWatchAction() };

        public override bool UsesTargets => false;

        // Lê o thinker que a CreateThinkerAction acabou de criar, copiado para o contexto da vigia
        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (frame.Phase > 0 || context.LastCreatedThinker == null) return ActionStatus.Done;
            frame.Phase = 1;
            frame.Data = context.LastCreatedThinker;
            return ActionStatus.WaitUntilCancelled;
        }

        // O thinker desligado sem término cancela a vigia no OnDisable dele: aí não termina. O criador desligado ou
        // destruído cancela a vigia no OnDisable do ASC dele, antes do LateUpdate do thinker: aí o motivo é OwnerGone
        protected override void OnCancel(ActionContext context, ref ActionFrame frame)
        {
            if (frame.Data is GameplayThinker thinker && thinker != null && thinker.isActiveAndEnabled)
            {
                bool ownerGone = context.Owner == null || !context.Owner.isActiveAndEnabled;
                thinker.End(ownerGone ? ActionEndReason.OwnerGone : ActionEndReason.ExecutionEnded);
            }
        }
    }
}
