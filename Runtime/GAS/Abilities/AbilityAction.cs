using System;
using System.Collections;
using UnityEngine;

namespace SpellBook.GAS.Abilities
{
    [Serializable]
    public abstract class AbilityAction
    {
        public abstract IEnumerator Execute(AbilityContext context);
    }
}

