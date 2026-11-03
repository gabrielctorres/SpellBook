using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using UnityEngine;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Abilities.Actions
{
    /// <summary>
    /// Põe nos alvos do evento (<see cref="ActionContext.EventTargets"/>) os alvos que o seletor da ação
    /// (<see cref="GameplayAction.Target"/>) escolheu, trocando os atuais ou somando a eles.
    /// </summary>
    [Serializable, TypeCategory("Alvo", Summary = "Troca ou soma aos alvos do evento os alvos que o seletor escolheu.")]
    public class TargetingAction : GameplayAction
    {
        public enum TargetMode { Replace, Append }

        [Tooltip("Replace substitui os alvos atuais; Append adiciona aos existentes.")]
        public TargetMode Mode = TargetMode.Replace;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            List<AbilitySystemComponent> found = context.Targets;
            List<AbilitySystemComponent> eventTargets = context.EventTargets;

            if (Mode == TargetMode.Replace) eventTargets.Clear();
            for (int i = eventTargets.Count - 1; i >= 0; i--)
            {
                if (eventTargets[i] == null) eventTargets.RemoveAt(i);
            }
            for (int i = 0; i < found.Count; i++)
            {
                if (!eventTargets.Contains(found[i])) eventTargets.Add(found[i]);
            }

            return ActionStatus.Done;
        }
    }
}
