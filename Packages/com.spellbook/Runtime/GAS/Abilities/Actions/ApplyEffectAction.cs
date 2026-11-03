using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;

namespace SpellBook.GAS.Abilities.Actions
{
    /// <summary>
    /// Aplica os efeitos com a habilidade, o nível e o ponto do contexto (<see cref="EffectSpec.FromAbility"/>) nos alvos
    /// do evento e, com <see cref="ApplyToSource"/>, em quem lançou.
    /// </summary>
    [Serializable, TypeCategory("Efeito", Summary = "Aplica efeitos nos alvos e, se marcado, em quem lançou.")]
    public class ApplyEffectAction : GameplayAction
    {
        public List<GameplayEffect> Effects;
        public bool ApplyToSource = false;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (Effects == null || context.Instigator == null) return ActionStatus.Done;

            if (ApplyToSource)
            {
                foreach (var effect in Effects) context.Instigator.ApplyEffect(EffectSpec.FromAbility(effect, context));
            }

            List<AbilitySystemComponent> targets = context.EventTargets;
            targets.RemoveAll(t => t == null);
            // Cópia: aplicar um efeito pode disparar ações que alteram a lista de alvos
            var snapshot = targets.ToArray();
            foreach (var target in snapshot)
            {
                if (target == null) continue;
                foreach (var effect in Effects) target.ApplyEffect(EffectSpec.FromAbility(effect, context));
            }

            return ActionStatus.Done;
        }

        public override void CollectEffects(List<GameplayEffect> effects)
        {
            if (Effects != null) effects.AddRange(Effects);
        }
    }
}
