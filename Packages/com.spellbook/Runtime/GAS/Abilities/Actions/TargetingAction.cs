using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using UnityEngine;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Targeting;

namespace SpellBook.GAS.Abilities.Actions
{
    /// <summary>Busca alvos com a <see cref="Strategy"/> e troca ou soma aos alvos do evento.</summary>
    [Serializable, TypeCategory("Alvo", Summary = "Busca alvos com uma estratégia e troca ou soma aos alvos atuais.")]
    public class TargetingAction : GameplayAction
    {
        public enum TargetMode { Replace, Append }

        public TargetingStrategy Strategy;

        [Tooltip("De onde a busca deve partir? Caster ou Location (ponto de impacto)?")]
        public AbilityTargetSource Origin = AbilityTargetSource.Location;

        [Tooltip("Replace substitui os alvos atuais; Append adiciona aos existentes.")]
        public TargetMode Mode = TargetMode.Replace;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (Strategy == null || context.Instigator == null) return ActionStatus.Done;

            Vector3 originPos = context.Point;
            List<AbilitySystemComponent> currentTargets = context.EventTargets;
            currentTargets.RemoveAll(t => t == null);

            if (Origin == AbilityTargetSource.Caster)
            {
                originPos = context.Instigator.transform.position;
            }
            else if (Origin == AbilityTargetSource.Target && currentTargets.Count > 0)
            {
                originPos = currentTargets[0].transform.position;
            }

            var found = Strategy.GetTargets(context.Instigator, originPos);

            if (Mode == TargetMode.Replace) currentTargets.Clear();
            foreach (var target in found)
            {
                if (!currentTargets.Contains(target)) currentTargets.Add(target);
            }

            return ActionStatus.Done;
        }
    }
}
