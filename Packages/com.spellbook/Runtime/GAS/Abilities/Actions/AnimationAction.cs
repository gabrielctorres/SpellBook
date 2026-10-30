using System;
using System.Collections;
using UnityEngine;

namespace SpellBook.GAS.Abilities.Actions
{
    public enum AnimationParamType { Trigger, Bool, Int }

    [Serializable]
    public class AnimationAction : AbilityAction
    {
        public string ParameterName;
        public AnimationParamType Type;
        public bool BoolValue;
        public int IntValue;

        public override IEnumerator Execute(AbilityContext context)
        {
            var animator = context.Source.GetComponentInChildren<Animator>();
            if (animator == null) yield break;

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

            yield return null;
        }
    }
}

