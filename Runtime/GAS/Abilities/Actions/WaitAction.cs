using System;
using System.Collections;
using UnityEngine;

namespace SpellBook.GAS.Abilities.Actions
{
    [Serializable]
    public class WaitAction : AbilityAction
    {
        public float Duration;
        public override IEnumerator Execute(AbilityContext context)
        {
            yield return new WaitForSeconds(Duration);
        }
    }
}

