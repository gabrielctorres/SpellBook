using System;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Abilities;
using UnityEngine;

namespace SpellBook.GAS.Conditions
{
    /// <summary>
    /// Verdadeira se o ponto-alvo está a até <see cref="GameplayAbility.CastRange"/> (avaliado no nível do contexto) da
    /// posição do <see cref="ConditionContext.Owner"/>. Alcance ≤ 0, ou sem habilidade, é "sem limite": verdadeira, com ou
    /// sem ponto. Com alcance &gt; 0, sem ponto ou sem dono, é falsa.
    /// </summary>
    [Serializable]
    [TypeCategory("Alvos", Summary = "O ponto-alvo está dentro do alcance da habilidade.", Line = "Dentro do alcance")]
    public sealed class CastRangeCondition : Condition<ConditionContext>
    {
        public override string DefaultFailureKey => "gas.activation.cast_range";

        public override bool Evaluate(in ConditionContext context)
        {
            GameplayAbility ability = context.Ability;
            if (ability == null) return true;

            float range = ability.CastRange.Evaluate(context.Level, ability.Values);
            if (range <= 0f) return true;
            if (!context.Target.HasPoint || context.Owner == null) return false;

            Vector3 offset = context.Target.Point - context.Owner.transform.position;
            return offset.sqrMagnitude <= range * range;
        }
    }
}
