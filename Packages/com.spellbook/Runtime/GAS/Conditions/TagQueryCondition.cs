using System;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Conditions
{
    /// <summary>De quem a <see cref="TagQueryCondition"/> lê as tags (ADR 0035, item 2).</summary>
    public enum ConditionSubject
    {
        /// <summary>O <see cref="ConditionContext.Owner"/>, quem avalia a condição.</summary>
        Owner,
        /// <summary>A unidade do <see cref="ConditionContext.Target"/>, como o candidato de um filtro de alvo.</summary>
        TargetUnit,
    }

    /// <summary>
    /// Verdadeira se as tags ativas do <see cref="Subject"/> satisfazem a consulta
    /// (<c>AbilitySystemComponent.HasMatching</c>). Sem dono, ou sem unidade com <see cref="ConditionSubject.TargetUnit"/>,
    /// é falsa.
    /// </summary>
    [Serializable]
    [TypeCategory("Tags", Summary = "As tags do dono ou da unidade-alvo satisfazem a consulta.", Line = "Tags casam com a consulta")]
    public sealed class TagQueryCondition : Condition<ConditionContext>
    {
        [Tooltip("Owner lê as tags de quem avalia; Target Unit, as da unidade-alvo (sem unidade, é falsa).")]
        [SerializeField] private ConditionSubject subject = ConditionSubject.Owner;
        [SerializeField] private GameplayTagQuery query = new GameplayTagQuery();

        /// <summary>De quem ler as tags. O padrão é <see cref="ConditionSubject.Owner"/>.</summary>
        public ConditionSubject Subject { get => subject; set => subject = value; }

        /// <summary>Consulta (todas, alguma e nenhuma) sobre as tags do <see cref="Subject"/>.</summary>
        public GameplayTagQuery Query => query;

        public override string DefaultFailureKey => "gas.activation.tag_query";

        public override bool Evaluate(in ConditionContext context)
        {
            AbilitySystemComponent tagged = subject == ConditionSubject.TargetUnit ? context.Target.Unit : context.Owner;
            return tagged != null && tagged.HasMatching(query);
        }

        /// <summary>Repassa à <see cref="GameplayTagQuery.Validate"/>: consulta vazia (aviso) e tag inválida ou repetida.</summary>
        public override void Validate(ValidationResult result, string propertyPath)
        {
            query?.Validate(result, propertyPath + "." + nameof(query));
        }
    }
}
