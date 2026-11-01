using System;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;

namespace SpellBook.GAS.Conditions
{
    /// <summary>
    /// Verdadeira se a ativação tem um ponto-alvo (<see cref="ConditionContext.Target"/> com <c>HasPoint</c>).
    /// Ponha a regra dela antes da <see cref="CastRangeCondition"/> para a falta de alvo ter motivo próprio.
    /// </summary>
    [Serializable]
    [TypeCategory("Alvos", Summary = "A ativação tem um ponto-alvo.", Line = "Tem ponto-alvo")]
    public sealed class HasTargetPointCondition : Condition<ConditionContext>
    {
        public override string DefaultFailureKey => "gas.activation.no_target";

        public override bool Evaluate(in ConditionContext context)
        {
            return context.Target.HasPoint;
        }
    }
}
