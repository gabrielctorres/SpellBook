using System;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Actions;
using UnityEngine;

namespace SpellBook.GAS.Abilities.Actions
{
    public enum AnimationParamType { Trigger, Bool, Int }

    [Serializable, TypeCategory("Apresentação", Summary = "Muda um parâmetro do Animator de quem lançou.")]
    public class AnimationAction : GameplayAction
    {
        public string ParameterName;
        public AnimationParamType Type;
        public bool BoolValue;
        public int IntValue;

        public override bool UsesTargets => false;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (context.Instigator == null) return ActionStatus.Done;
            var animator = context.Instigator.GetComponentInChildren<Animator>();
            if (animator == null) return ActionStatus.Done;

            switch (Type)
            {
                case AnimationParamType.Trigger:
                    animator.SetTrigger(ParameterName);
                    break;
                case AnimationParamType.Bool:
                    animator.SetBool(ParameterName, BoolValue);
                    break;
                case AnimationParamType.Int:
                    animator.SetInteger(ParameterName, IntValue);
                    break;
            }

            return ActionStatus.Done;
        }
    }
}
