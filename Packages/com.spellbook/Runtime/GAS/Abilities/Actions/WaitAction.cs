using System;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Actions;

namespace SpellBook.GAS.Abilities.Actions
{
    [Serializable, TypeCategory("Fluxo", Summary = "Espera alguns segundos no tempo do sistema de habilidades do ator.")]
    public class WaitAction : GameplayAction
    {
        /// <summary>Segundos do tempo do ASC, contados do fim do passo anterior. Zero ou menos não espera.</summary>
        public float Duration;

        public override bool UsesTargets => false;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (frame.Phase != 0 || Duration <= 0f) return ActionStatus.Done;

            frame.Phase = 1;
            return ActionStatus.Wait(Duration);
        }
    }
}
