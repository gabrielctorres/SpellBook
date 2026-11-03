using System;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using UnityEngine;

namespace SpellBook.GAS.Conditions
{
    /// <summary>Até onde a <see cref="CastRangeCondition"/> mede quando o alvo é uma unidade.</summary>
    public enum RangeMeasure
    {
        /// <summary>Até a posição da unidade.</summary>
        Center,
        /// <summary>Até o ponto do colisor da unidade mais perto do dono; sem colisor, até a posição dela.</summary>
        Edge,
    }

    /// <summary>
    /// Verdadeira se o alvo está a até <see cref="GameplayAbility.CastRange"/> (avaliado no nível do contexto) da
    /// posição do <see cref="ConditionContext.Owner"/>. Com unidade, mede até ela pelo <see cref="Measure"/>; sem
    /// unidade, até o ponto. Alcance ≤ 0, ou sem habilidade, é "sem limite": verdadeira, com ou sem alvo. Com
    /// alcance &gt; 0, sem ponto ou sem dono, é falsa.
    /// </summary>
    [Serializable]
    [TypeCategory("Alvos", Summary = "O alvo está dentro do alcance da habilidade.", Line = "Dentro do alcance")]
    public sealed class CastRangeCondition : Condition<ConditionContext>
    {
        [Tooltip("Com unidade-alvo: mede até o centro dela ou até a borda do colisor dela.")]
        [SerializeField] private RangeMeasure measure = RangeMeasure.Center;

        /// <summary>Até onde medir quando o alvo é uma unidade. O padrão é <see cref="RangeMeasure.Center"/>.</summary>
        public RangeMeasure Measure { get => measure; set => measure = value; }

        public override string DefaultFailureKey => "gas.activation.cast_range";

        public override bool Evaluate(in ConditionContext context)
        {
            GameplayAbility ability = context.Ability;
            if (ability == null) return true;

            float range = ability.CastRange.Evaluate(context.Level, ability.Values);
            if (range <= 0f) return true;
            if (!context.Target.HasPoint || context.Owner == null) return false;

            Vector3 origin = context.Owner.transform.position;
            Vector3 offset = TargetPosition(context.Target, origin) - origin;
            return offset.sqrMagnitude <= range * range;
        }

        private Vector3 TargetPosition(in AbilityTarget target, Vector3 origin)
        {
            AbilitySystemComponent unit = target.Unit;
            if (unit == null) return target.Point;
            if (measure == RangeMeasure.Center) return unit.transform.position;

            if (unit.TryGetComponent(out Collider collider)) return collider.ClosestPoint(origin);
            if (unit.TryGetComponent(out Collider2D collider2D))
            {
                Vector2 closest = collider2D.ClosestPoint(origin);
                return new Vector3(closest.x, closest.y, unit.transform.position.z);
            }
            return unit.transform.position;
        }
    }
}
