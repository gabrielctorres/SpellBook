using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using UnityEngine;
using SpellBook.Tags;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Effects;

namespace SpellBook.GAS.Abilities.Actions
{
    /// <summary>
    /// Executa a lista de ações várias vezes, com intervalo opcional entre repetições. Sem intervalo, as repetições
    /// rodam na mesma chamada.
    /// </summary>
    [Serializable, TypeCategory("Fluxo", Summary = "Repete as ações várias vezes, com intervalo opcional.")]
    public class RepeatAction : GameplayAction
    {
        [Min(1)] public int Count = 2;
        [Min(0)] public float Interval = 0f;

        [SerializeReference]
        [TypePicker]
        public List<GameplayAction> Actions = new List<GameplayAction>();

        public override bool UsesTargets => false;

        // frame.Index: repetição atual; frame.Phase 1: as filhas da repetição acabaram de rodar
        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (frame.Phase == 1)
            {
                frame.Index++;
                if (frame.Index >= Count) return ActionStatus.Done;
                if (Interval > 0f)
                {
                    frame.Phase = 0;
                    return ActionStatus.Wait(Interval);
                }
            }
            else if (frame.Index >= Count)
            {
                return ActionStatus.Done;
            }

            frame.Phase = 1;
            return ActionStatus.Run(Actions);
        }

        public override void CollectEffects(List<GameplayEffect> effects)
        {
            ActionLists.CollectEffects(Actions, effects);
        }
    }

    /// <summary>
    /// Executa todas as ações ao mesmo tempo e espera todas terminarem.
    /// </summary>
    [Serializable, TypeCategory("Fluxo", Summary = "Roda as ações ao mesmo tempo e espera todas terminarem.")]
    public class ParallelAction : GameplayAction
    {
        [SerializeReference]
        [TypePicker]
        public List<GameplayAction> Actions = new List<GameplayAction>();

        public override bool UsesTargets => false;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (frame.Phase != 0 || Actions == null) return ActionStatus.Done;

            frame.Phase = 1;
            return ActionStatus.RunParallel(Actions);
        }

        public override void CollectEffects(List<GameplayEffect> effects)
        {
            ActionLists.CollectEffects(Actions, effects);
        }
    }

    /// <summary>
    /// Executa "Then" ou "Else" conforme uma condição avaliada no momento da execução.
    /// </summary>
    [Serializable, TypeCategory("Fluxo", Summary = "Roda as ações de Then ou de Else conforme a condição.")]
    public class ConditionalAction : GameplayAction
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
        public List<GameplayAction> Then = new List<GameplayAction>();

        [SerializeReference]
        [TypePicker]
        public List<GameplayAction> Else = new List<GameplayAction>();

        public override bool UsesTargets => false;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (frame.Phase != 0) return ActionStatus.Done;

            frame.Phase = 1;
            return ActionStatus.Run(Evaluate(context) ? Then : Else);
        }

        public override void CollectEffects(List<GameplayEffect> effects)
        {
            ActionLists.CollectEffects(Then, effects);
            ActionLists.CollectEffects(Else, effects);
        }

        private bool Evaluate(ActionContext context)
        {
            switch (Condition)
            {
                case ConditionType.SourceHasAllTags:
                    return context.Instigator != null && context.Instigator.HasAllTags(Tags);
                case ConditionType.SourceHasAnyTag:
                    return context.Instigator != null && context.Instigator.HasAnyTag(Tags);
                case ConditionType.HasTargets:
                    context.EventTargets.RemoveAll(t => t == null);
                    return context.EventTargets.Count > 0;
                default:
                    return false;
            }
        }
    }

    internal static class ActionLists
    {
        /// <summary>Repassa <see cref="GameplayAction.CollectEffects"/> a cada ação não nula de <paramref name="actions"/>.</summary>
        public static void CollectEffects(IList<GameplayAction> actions, List<GameplayEffect> effects)
        {
            if (actions == null) return;

            for (int i = 0; i < actions.Count; i++)
            {
                actions[i]?.CollectEffects(effects);
            }
        }
    }
}
