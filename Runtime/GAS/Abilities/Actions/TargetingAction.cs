using System;
using System.Collections;
using UnityEngine;
using SpellBook.GAS.Targeting;

namespace SpellBook.GAS.Abilities.Actions
{
    [Serializable]
    public class TargetingAction : AbilityAction
    {
        public TargetingStrategy Strategy;

        [Tooltip("De onde a busca deve partir? Caster ou Location (ponto de impacto)?")]
        public AbilityTargetSource Origin = AbilityTargetSource.Location;

        public override IEnumerator Execute(AbilityContext context)
        {
            if (Strategy == null) yield break;

            Vector3 originPos = context.TargetPosition;

            if (Origin == AbilityTargetSource.Caster)
            {
                originPos = context.Source.transform.position;
            }
            else if (Origin == AbilityTargetSource.Target && context.Targets.Count > 0)
            {
                originPos = context.Targets[0].transform.position;
            }

            var found = Strategy.GetTargets(context.Source.transform, originPos);

            context.Targets.Clear();
            context.Targets.AddRange(found);

            yield return null;
        }
    }
}

