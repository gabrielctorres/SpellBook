using System;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Abilities;

namespace SpellBook.GAS.Actions
{
    /// <summary>
    /// Cobra a recarga, o custo e a carga da habilidade em <see cref="CommitMode.Manual"/>, pelo
    /// <see cref="Core.AbilitySystemComponent.CommitAbility"/>. Se a carga ou o custo faltarem, a habilidade é cancelada e
    /// as ações seguintes não rodam. Uma ativação já cobrada, inclusive em <see cref="CommitMode.OnStart"/>, não cobra de
    /// novo. Fora de uma ativação, como numa lista de efeito, não faz nada.
    /// </summary>
    [Serializable, TypeCategory("Efeito", Summary = "Cobra a recarga, o custo e a carga da habilidade.")]
    public sealed class CommitAction : GameplayAction
    {
        public override bool UsesTargets => false;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (context.Owner != null && context.Spec != null) context.Owner.CommitAbility(context.Spec);
            return ActionStatus.Done;
        }
    }
}
