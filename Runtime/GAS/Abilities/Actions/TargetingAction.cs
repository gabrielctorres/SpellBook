using System;
using System.Collections;
using UnityEngine;
using SpellBook.GAS.Targeting;

namespace SpellBook.GAS.Abilities.Actions
{
    [Serializable]
    public class TargetingAction : AbilityAction
    {
        public enum TargetMode { Replace, Append }

        public TargetingStrategy Strategy;

        [Tooltip("De onde a busca deve partir? Caster ou Location (ponto de impacto)?")]
        public AbilityTargetSource Origin = AbilityTargetSource.Location;

        [Tooltip("Replace substitui os alvos atuais; Append adiciona aos existentes.")]
        public TargetMode Mode = TargetMode.Replace;

        public override IEnumerator Execute(AbilityContext context)
        {
            if (Strategy == null || context.Source == null) yield break;

            Vector3 originPos = context.TargetPosition;
            var currentTargets = context.GetValidTargets();

            if (Origin == AbilityTargetSource.Caster)
            {
                originPos = context.Source.transform.position;
            }
            else if (Origin == AbilityTargetSource.Target && currentTargets.Count > 0)
            {
                originPos = currentTargets[0].transform.position;
            }

            var found = Strategy.GetTargets(context.Source, originPos);

            if (Mode == TargetMode.Replace) currentTargets.Clear();
            foreach (var target in found)
            {
                if (!currentTargets.Contains(target)) currentTargets.Add(target);
            }

            yield return null;
        }
    }
}
