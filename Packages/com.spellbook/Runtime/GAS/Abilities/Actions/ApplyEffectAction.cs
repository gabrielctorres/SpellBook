using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Effects;

namespace SpellBook.GAS.Abilities.Actions
{
    [Serializable]
    public class ApplyEffectAction : AbilityAction
    {
        public List<GameplayEffect> Effects;
        public bool ApplyToSource = false;

        public override IEnumerator Execute(AbilityContext context)
        {
            if (Effects == null || context.Source == null) yield break;

            if (ApplyToSource)
            {
                foreach (var effect in Effects) context.Source.ApplyEffect(effect, context.Source);
            }

            // Cópia: aplicar um efeito pode disparar ações que alteram a lista de alvos
            var targets = context.GetValidTargets().ToArray();
            foreach (var target in targets)
            {
                if (target == null) continue;
                foreach (var effect in Effects) target.ApplyEffect(effect, context.Source);
            }

            yield return null;
        }
    }
}
