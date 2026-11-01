using System;
using System.Collections.Generic;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;

namespace SpellBook.GAS.Conditions
{
    /// <summary>Verdadeira se todas as condições são; lista vazia é verdadeira e entrada nula é ignorada.</summary>
    [Serializable]
    [TypeCategory("Lógica", Summary = "Todas as condições valem.", Line = "Todas ({conditions})")]
    public sealed class AndCondition : AndConditionBase<ConditionContext>
    {
        /// <summary>Condições avaliadas em ordem.</summary>
        public List<Condition<ConditionContext>> Conditions => conditions;
    }

    /// <summary>Verdadeira se alguma condição é; lista vazia é falsa e entrada nula é ignorada.</summary>
    [Serializable]
    [TypeCategory("Lógica", Summary = "Alguma condição vale.", Line = "Alguma ({conditions})")]
    public sealed class OrCondition : OrConditionBase<ConditionContext>
    {
        /// <summary>Condições avaliadas em ordem.</summary>
        public List<Condition<ConditionContext>> Conditions => conditions;
    }

    /// <summary>Inverte a condição; sem condição é falsa.</summary>
    [Serializable]
    [TypeCategory("Lógica", Summary = "A condição não vale.", Line = "Não {condition}")]
    public sealed class NotCondition : NotConditionBase<ConditionContext>
    {
        /// <summary>Condição invertida.</summary>
        public Condition<ConditionContext> Condition
        {
            get => condition;
            set => condition = value;
        }
    }
}
