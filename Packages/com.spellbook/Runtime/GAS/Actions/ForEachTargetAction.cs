using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using UnityEngine;

namespace SpellBook.GAS.Actions
{
    /// <summary>
    /// Roda as ações uma vez por alvo. O conjunto são os alvos da própria ação (o seletor dela, ou os alvos do evento),
    /// copiados quando ela começa: uma filha que muda os alvos do evento não muda as repetições. Dentro da repetição,
    /// uma filha sem seletor recebe só o alvo corrente.
    /// </summary>
    [Serializable, TypeCategory("Fluxo", Summary = "Roda as ações uma vez por alvo, com o alvo corrente como padrão.")]
    public sealed class ForEachTargetAction : GameplayAction
    {
        [SerializeReference, TypePicker] private List<GameplayAction> actions = new List<GameplayAction>();

        /// <summary>Ações que rodam, em sequência, para cada alvo.</summary>
        public List<GameplayAction> Actions
        {
            get => actions;
            set => actions = value;
        }

        // frame.Index: posição do alvo corrente no conjunto; frame.Phase 1: as filhas desse alvo estão rodando
        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (frame.Phase == 1) frame.Index++;

            List<AbilitySystemComponent> targets = context.Targets;
            // Um alvo destruído durante as repetições anteriores é pulado
            while (frame.Index < targets.Count && targets[frame.Index] == null) frame.Index++;
            if (frame.Index >= targets.Count) return ActionStatus.Done;

            frame.Phase = 1;
            return ActionStatus.Run(actions);
        }

        /// <summary>Alvo da repetição em curso, ou nulo se as filhas não estão rodando.</summary>
        internal static AbilitySystemComponent CurrentTarget(in ActionFrame frame, List<AbilitySystemComponent> targets)
        {
            if (frame.Phase != 1 || frame.Index >= targets.Count) return null;
            return targets[frame.Index];
        }

        public override void CollectEffects(List<GameplayEffect> results)
        {
            ActionLists.CollectEffects(actions, results);
        }

        /// <summary>Avisa a repetição sem ações filhas.</summary>
        public override void Validate(ValidationResult result)
        {
            if (actions == null || actions.Count == 0)
            {
                result.AddWarning("ForEachTarget sem ações: a repetição não faz nada.", nameof(actions));
            }
        }

        internal override void CollectChildLists(List<ChildActionList> results)
        {
            results.Add(new ChildActionList(nameof(actions), actions));
        }
    }
}
