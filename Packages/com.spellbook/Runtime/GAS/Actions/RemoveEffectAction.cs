using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Actions
{
    /// <summary>
    /// Remove de cada alvo as instâncias de <see cref="Effect"/> e as que concedem <see cref="Tag"/> (ou uma filha dela),
    /// e grava a soma das pilhas removidas em <see cref="ActionContext.LastRemovedStacks"/>. Sem efeito e sem tag, não
    /// remove nada e grava 0.
    /// </summary>
    [Serializable, TypeCategory("Efeito", Summary = "Remove dos alvos um efeito, ou os efeitos que concedem uma tag.")]
    public sealed class RemoveEffectAction : GameplayAction
    {
        [SerializeField] private GameplayEffect effect;
        [SerializeField] private GameplayTag tag;

        /// <summary>Asset do efeito a remover. Nulo não remove por asset.</summary>
        public GameplayEffect Effect
        {
            get => effect;
            set => effect = value;
        }

        /// <summary>Tag dos efeitos a remover, com as filhas. Inválida não remove por tag.</summary>
        public GameplayTag Tag
        {
            get => tag;
            set => tag = value;
        }

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            int removed = 0;
            List<AbilitySystemComponent> targets = context.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                AbilitySystemComponent target = targets[i];
                if (target == null) continue;
                if (effect != null) removed += target.RemoveEffects(effect);
                if (tag.IsValid) removed += target.RemoveEffectsWithTag(tag);
            }
            context.LastRemovedStacks = removed;
            return ActionStatus.Done;
        }

        /// <summary>Acusa a ação sem efeito e sem tag, que não remove nada (erro).</summary>
        public override void Validate(ValidationResult result)
        {
            if (effect == null && !tag.IsValid)
            {
                result.AddError("RemoveEffect sem efeito e sem tag: escolha o que remover.", nameof(effect));
            }
        }
    }
}
