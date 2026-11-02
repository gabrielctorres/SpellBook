using System;
using System.Collections;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using UnityEngine;
using SpellBook.Tags;
using SpellBook.GAS.Effects;

namespace SpellBook.GAS.Abilities.Actions
{
    /// <summary>
    /// Executa a lista de ações várias vezes, com intervalo opcional entre repetições.
    /// </summary>
    [Serializable, TypeCategory("Fluxo", Summary = "Repete as ações várias vezes, com intervalo opcional.")]
    public class RepeatAction : AbilityAction
    {
        [Min(1)] public int Count = 2;
        [Min(0)] public float Interval = 0f;

        [SerializeReference]
        [TypePicker]
        public List<AbilityAction> Actions = new List<AbilityAction>();

        public override IEnumerator Execute(AbilityContext context)
        {
            for (int i = 0; i < Count; i++)
            {
                if (context.IsCancelled) yield break;
                yield return RunSequence(Actions, context);

                if (Interval > 0f && i < Count - 1) yield return context.WaitSeconds(Interval);
            }
        }

        public override void CollectEffects(List<GameplayEffect> effects)
        {
            CollectEffects(Actions, effects);
        }
    }

    /// <summary>
    /// Executa todas as ações ao mesmo tempo e espera todas terminarem.
    /// </summary>
    [Serializable, TypeCategory("Fluxo", Summary = "Roda as ações ao mesmo tempo e espera todas terminarem.")]
    public class ParallelAction : AbilityAction
    {
        [SerializeReference]
        [TypePicker]
        public List<AbilityAction> Actions = new List<AbilityAction>();

        public override IEnumerator Execute(AbilityContext context)
        {
            if (Actions == null || context.Source == null) yield break;

            var pending = new int[1];
            foreach (var action in Actions)
            {
                if (action == null) continue;

                pending[0]++;
                if (context.StartChildRoutine(RunAndSignal(action, context, pending)) == null)
                {
                    // Não foi possível iniciar (ASC desativado)
                    pending[0]--;
                }
            }

            while (pending[0] > 0 && !context.IsCancelled) yield return null;
        }

        private static IEnumerator RunAndSignal(AbilityAction action, AbilityContext context, int[] pending)
        {
            yield return Run(action, context);
            pending[0]--;
        }

        public override void CollectEffects(List<GameplayEffect> effects)
        {
            CollectEffects(Actions, effects);
        }
    }

    /// <summary>
    /// Executa "Then" ou "Else" conforme uma condição avaliada no momento da execução.
    /// </summary>
    [Serializable, TypeCategory("Fluxo", Summary = "Roda as ações de Then ou de Else conforme a condição.")]
    public class ConditionalAction : AbilityAction
    {
        public enum ConditionType
        {
            SourceHasAllTags,
            SourceHasAnyTag,
            HasTargets
        }

        public ConditionType Condition = ConditionType.HasTargets;
        [Tooltip("Usado pelas condições de tag.")]
        public List<GameplayTag> Tags = new List<GameplayTag>();

        [SerializeReference]
        [TypePicker]
        public List<AbilityAction> Then = new List<AbilityAction>();

        [SerializeReference]
        [TypePicker]
        public List<AbilityAction> Else = new List<AbilityAction>();

        public override IEnumerator Execute(AbilityContext context)
        {
            yield return RunSequence(Evaluate(context) ? Then : Else, context);
        }

        public override void CollectEffects(List<GameplayEffect> effects)
        {
            CollectEffects(Then, effects);
            CollectEffects(Else, effects);
        }

        private bool Evaluate(AbilityContext context)
        {
            switch (Condition)
            {
                case ConditionType.SourceHasAllTags:
                    return context.Source != null && context.Source.HasAllTags(Tags);
                case ConditionType.SourceHasAnyTag:
                    return context.Source != null && context.Source.HasAnyTag(Tags);
                case ConditionType.HasTargets:
                    return context.GetValidTargets().Count > 0;
                default:
                    return false;
            }
        }
    }
}
