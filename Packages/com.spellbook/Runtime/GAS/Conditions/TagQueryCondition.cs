using System;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Conditions
{
    /// <summary>
    /// Verdadeira se as tags ativas do <see cref="ConditionContext.Owner"/> satisfazem a consulta
    /// (<c>AbilitySystemComponent.HasMatching</c>). Sem dono: falsa.
    /// </summary>
    [Serializable]
    [TypeCategory("Tags", Summary = "As tags do dono satisfazem a consulta.", Line = "Tags do dono casam com a consulta")]
    public sealed class TagQueryCondition : Condition<ConditionContext>
    {
        [SerializeField] private GameplayTagQuery query = new GameplayTagQuery();

        /// <summary>Consulta (todas, alguma e nenhuma) sobre as tags do dono.</summary>
        public GameplayTagQuery Query => query;

        public override string DefaultFailureKey => "gas.activation.tag_query";

        public override bool Evaluate(in ConditionContext context)
        {
            return context.Owner != null && context.Owner.HasMatching(query);
        }
    }
}
