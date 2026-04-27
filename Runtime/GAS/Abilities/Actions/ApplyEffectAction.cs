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
            if (ApplyToSource && context.Source != null)
            {
                foreach (var effect in Effects) context.Source.ApplyEffect(effect, context.Source);
            }

            foreach (var target in context.Targets)
            {
                foreach (var effect in Effects) target.ApplyEffect(effect, context.Source);
            }

            yield return null;
        }
    }
}

