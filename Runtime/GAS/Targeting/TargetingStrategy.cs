using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Targeting
{
    public abstract class TargetingStrategy : ScriptableObject
    {
        public abstract IEnumerable<AbilitySystemComponent> GetTargets(Transform source, Vector3 targetPosition);
    }
}

