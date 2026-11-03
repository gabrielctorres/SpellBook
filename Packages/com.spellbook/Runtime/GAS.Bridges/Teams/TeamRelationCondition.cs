using System;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.Teams;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>
    /// Verdadeira se a relação do time do <see cref="ConditionContext.Owner"/> com o da unidade do
    /// <see cref="ConditionContext.Target"/> está na <see cref="Mask"/>. Sem unidade no alvo, é falsa. Serve em regra de
    /// ativação, ação condicional e filtro de candidato (ADR 0035).
    /// </summary>
    [Serializable]
    [TypeCategory("Time", Summary = "A relação de time com a unidade-alvo está na máscara.", Line = "Relação com o alvo: {mask}")]
    public sealed class TeamRelationCondition : Condition<ConditionContext>
    {
        [Tooltip("Relações aceitas. Self casa só quando o alvo é o próprio dono.")]
        [SerializeField] private TeamRelationMask mask = TeamRelationMask.Hostile;

        /// <summary>Relações aceitas. O padrão é <see cref="TeamRelationMask.Hostile"/>.</summary>
        public TeamRelationMask Mask { get => mask; set => mask = value; }

        /// <summary>A mesma chave para toda falha, inclusive sem unidade no alvo.</summary>
        public override string DefaultFailureKey => "teams.condition.relation";

        public override bool Evaluate(in ConditionContext context)
        {
            AbilitySystemComponent unit = context.Target.Unit;
            if (unit == null)
            {
                return false;
            }

            ITeamMember owner = AscTeams.GetMember(context.Owner);
            ITeamMember target = AscTeams.GetMember(unit);
            return mask.Matches(owner.GetRelation(target), context.Owner == unit);
        }

        /// <summary>Avisa quando a máscara está vazia, porque aí a condição nunca vale.</summary>
        public override void Validate(ValidationResult result, string propertyPath)
        {
            if (mask == TeamRelationMask.None)
            {
                result.AddWarning("Máscara vazia: a condição nunca vale. Marque ao menos uma relação.",
                    propertyPath + "." + nameof(mask));
            }
        }
    }
}
