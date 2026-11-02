using System;
using System.Collections;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using UnityEngine;
using SpellBook.GAS.Effects;

namespace SpellBook.GAS.Abilities.Actions
{
    /// <summary>Aplica os efeitos com a habilidade, o nível e o ponto de alvo do contexto (<see cref="EffectSpec.FromAbility"/>).</summary>
    [Serializable, TypeCategory("Efeito", Summary = "Aplica efeitos nos alvos e, se marcado, em quem lançou.")]
    public class ApplyEffectAction : AbilityAction
    {
        public List<GameplayEffect> Effects;
        public bool ApplyToSource = false;

        public override IEnumerator Execute(AbilityContext context)
        {
            if (Effects == null || context.Source == null) yield break;

            if (ApplyToSource)
            {
                foreach (var effect in Effects) context.Source.ApplyEffect(EffectSpec.FromAbility(effect, context));
            }

            // Cópia: aplicar um efeito pode disparar ações que alteram a lista de alvos
            var targets = context.GetValidTargets().ToArray();
            foreach (var target in targets)
            {
                if (target == null) continue;
                foreach (var effect in Effects) target.ApplyEffect(EffectSpec.FromAbility(effect, context));
            }

            yield return null;
        }

        public override void CollectEffects(List<GameplayEffect> effects)
        {
            if (Effects != null) effects.AddRange(Effects);
        }
    }
}
