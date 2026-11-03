using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Actions
{
    /// <summary>
    /// Envia o evento de gameplay <see cref="Tag"/> ao ASC de cada alvo, com <see cref="ActionContext.Owner"/> como
    /// instigador, o próprio alvo como unidade do evento e <see cref="Magnitude"/> avaliada no nível do contexto. Numa
    /// lista de efeito, o evento leva o contexto do efeito.
    /// </summary>
    [Serializable, TypeCategory("Evento", Summary = "Envia um evento de gameplay ao ASC de cada alvo.")]
    public sealed class SendGameplayEventAction : GameplayAction
    {
        [SerializeField] private GameplayTag tag;
        [SerializeField] private ScalableFloat magnitude = ScalableFloat.Constant(0f);

        /// <summary>Tag do evento. Inválida não envia nada.</summary>
        public GameplayTag Tag
        {
            get => tag;
            set => tag = value;
        }

        /// <summary>Magnitude do evento por nível, em <see cref="GameplayEventData.Magnitude"/>.</summary>
        public ScalableFloat Magnitude
        {
            get => magnitude;
            set => magnitude = value;
        }

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (!tag.IsValid) return ActionStatus.Done;

            float value = magnitude != null
                ? magnitude.Evaluate(context.Level, context.Ability != null ? context.Ability.Values : null)
                : 0f;
            List<AbilitySystemComponent> targets = context.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                AbilitySystemComponent target = targets[i];
                if (target == null) continue;

                var data = new GameplayEventData(context.Owner, target, value);
                if (context.HasEffect) data = data.WithEffectContext(context.Effect);
                target.SendGameplayEvent(tag, in data);
            }
            return ActionStatus.Done;
        }
    }
}
