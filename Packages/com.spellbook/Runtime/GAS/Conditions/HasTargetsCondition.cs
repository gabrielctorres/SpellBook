using System;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;

namespace SpellBook.GAS.Conditions
{
    /// <summary>
    /// Verdadeira se o contexto tem ao menos um alvo não destruído em <see cref="ConditionContext.Targets"/>. Numa lista de
    /// ações, os alvos são os da execução: os da ação que avalia a condição.
    /// </summary>
    [Serializable]
    [TypeCategory("Alvos", Summary = "Há ao menos um alvo.", Line = "Tem alvos")]
    public sealed class HasTargetsCondition : Condition<ConditionContext>
    {
        public override string DefaultFailureKey => "gas.activation.has_targets";

        public override bool Evaluate(in ConditionContext context)
        {
            var targets = context.Targets;
            if (targets == null) return false;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null) return true;
            }

            return false;
        }
    }
}
