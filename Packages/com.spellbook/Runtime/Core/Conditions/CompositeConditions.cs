using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Core.Conditions
{
    /// <summary>
    /// Verdadeira se todas as condições são; lista vazia é verdadeira e entrada nula é ignorada (ADR 0032, item 1).
    /// Cada módulo fecha esta base sobre o próprio contexto com um tipo concreto não genérico.
    /// </summary>
    [Serializable]
    public abstract class AndConditionBase<TContext> : Condition<TContext> where TContext : struct
    {
        [SerializeReference]
        [TypePicker]
        protected List<Condition<TContext>> conditions = new List<Condition<TContext>>();

        public override bool Evaluate(in TContext context)
        {
            if (conditions == null)
            {
                return true;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                Condition<TContext> condition = conditions[i];
                if (condition != null && !condition.Evaluate(in context))
                {
                    return false;
                }
            }

            return true;
        }

        public override void Validate(ValidationResult result, string propertyPath)
        {
            CompositeValidation.ValidateChildren(conditions, result, propertyPath);
        }
    }

    /// <summary>
    /// Verdadeira se alguma condição é; lista vazia é falsa e entrada nula é ignorada (ADR 0032, item 1).
    /// Cada módulo fecha esta base sobre o próprio contexto com um tipo concreto não genérico.
    /// </summary>
    [Serializable]
    public abstract class OrConditionBase<TContext> : Condition<TContext> where TContext : struct
    {
        [SerializeReference]
        [TypePicker]
        protected List<Condition<TContext>> conditions = new List<Condition<TContext>>();

        public override bool Evaluate(in TContext context)
        {
            if (conditions == null)
            {
                return false;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                Condition<TContext> condition = conditions[i];
                if (condition != null && condition.Evaluate(in context))
                {
                    return true;
                }
            }

            return false;
        }

        public override void Validate(ValidationResult result, string propertyPath)
        {
            if (conditions == null || conditions.Count == 0)
            {
                result.AddWarning("\"Ou\" sem condições: sempre falso.", propertyPath);
                return;
            }

            CompositeValidation.ValidateChildren(conditions, result, propertyPath);
        }
    }

    /// <summary>
    /// Inverte a condição; condição nula é falsa (ADR 0032, item 1).
    /// Cada módulo fecha esta base sobre o próprio contexto com um tipo concreto não genérico.
    /// </summary>
    [Serializable]
    public abstract class NotConditionBase<TContext> : Condition<TContext> where TContext : struct
    {
        [SerializeReference]
        [TypePicker]
        protected Condition<TContext> condition;

        public override bool Evaluate(in TContext context)
        {
            return condition != null && !condition.Evaluate(in context);
        }

        public override void Validate(ValidationResult result, string propertyPath)
        {
            string path = propertyPath + "." + nameof(condition);
            if (condition == null)
            {
                result.AddError("\"Não\" sem condição: sempre falso.", path);
                return;
            }

            condition.Validate(result, path);
        }
    }

    internal static class CompositeValidation
    {
        public static void ValidateChildren<TContext>(List<Condition<TContext>> conditions, ValidationResult result, string propertyPath)
            where TContext : struct
        {
            if (conditions == null)
            {
                return;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                conditions[i]?.Validate(result, $"{propertyPath}.conditions.Array.data[{i}]");
            }
        }
    }
}
