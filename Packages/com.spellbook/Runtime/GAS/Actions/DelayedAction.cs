using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using UnityEngine;

namespace SpellBook.GAS.Actions
{
    /// <summary>
    /// Agenda as ações filhas para <see cref="Delay"/> segundos depois, no tempo do ASC, e termina na hora: a ação seguinte
    /// da lista roda na mesma chamada. As filhas rodam numa execução nova, do mesmo dono da lista, então a habilidade fica
    /// ativa até elas terminarem, e cancelar a habilidade ou remover o efeito as cancela. Com
    /// <see cref="ContinueIfOwnerEnds"/>, a execução nova fica sem dono e roda mesmo assim. As filhas sem seletor recebem os
    /// alvos que a <see cref="DelayedAction"/> recebeu.
    /// </summary>
    [Serializable, TypeCategory("Fluxo", Summary = "Roda as ações depois de um tempo, sem esperar por elas.")]
    public sealed class DelayedAction : GameplayAction
    {
        [SerializeField] private ScalableFloat delay = ScalableFloat.Constant(1f);
        [SerializeField] private bool continueIfOwnerEnds;
        [SerializeReference, TypePicker] private List<GameplayAction> actions = new List<GameplayAction>();

        /// <summary>Atraso por nível, em segundos do ASC. Zero ou menos roda as filhas na mesma chamada.</summary>
        public ScalableFloat Delay
        {
            get => delay;
            set => delay = value;
        }

        /// <summary>Se as filhas rodam mesmo quando a habilidade ou o efeito dono termina ou é cancelado antes.</summary>
        public bool ContinueIfOwnerEnds
        {
            get => continueIfOwnerEnds;
            set => continueIfOwnerEnds = value;
        }

        /// <summary>Ações que rodam, em sequência, depois do atraso.</summary>
        public List<GameplayAction> Actions
        {
            get => actions;
            set => actions = value;
        }

        public override bool UsesTargets => false;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (context.Owner == null) return ActionStatus.Done;

            float seconds = delay != null
                ? delay.Evaluate(context.Level, context.Ability != null ? context.Ability.Values : null)
                : 0f;
            context.Owner.Runner.Schedule(actions, seconds, continueIfOwnerEnds);
            return ActionStatus.Done;
        }

        public override void CollectEffects(List<GameplayEffect> results)
        {
            ActionLists.CollectEffects(actions, results);
        }
    }
}
