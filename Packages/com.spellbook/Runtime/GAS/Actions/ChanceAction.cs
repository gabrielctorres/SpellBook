using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using UnityEngine;

namespace SpellBook.GAS.Actions
{
    /// <summary>
    /// Roda as ações com uma chance: sorteia um número em [0, 1) na fonte de aleatoriedade do ASC
    /// (<see cref="ActionContext.Random"/>) e roda as filhas se ele for menor que <see cref="Probability"/>.
    /// </summary>
    [Serializable, TypeCategory("Fluxo", Summary = "Roda as ações com uma chance, sorteada no ASC.")]
    public sealed class ChanceAction : GameplayAction
    {
        [SerializeField] private ScalableFloat probability = ScalableFloat.Constant(1f);
        [SerializeReference, TypePicker] private List<GameplayAction> actions = new List<GameplayAction>();

        /// <summary>Chance por nível, de 0 (nunca) a 1 (sempre).</summary>
        public ScalableFloat Probability
        {
            get => probability;
            set => probability = value;
        }

        /// <summary>Ações que rodam, em sequência, quando o sorteio passa.</summary>
        public List<GameplayAction> Actions
        {
            get => actions;
            set => actions = value;
        }

        public override bool UsesTargets => false;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (frame.Phase != 0 || probability == null) return ActionStatus.Done;

            float chance = probability.Evaluate(context.Level, context.Ability != null ? context.Ability.Values : null);
            IRandomSource random = context.Random;
            if (random == null || random.Value() >= chance) return ActionStatus.Done;

            frame.Phase = 1;
            return ActionStatus.Run(actions);
        }

        public override void CollectEffects(List<GameplayEffect> results)
        {
            ActionLists.CollectEffects(actions, results);
        }
    }
}
