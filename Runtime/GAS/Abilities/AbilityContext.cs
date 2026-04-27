using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Abilities
{
    public class AbilityContext
    {
        public AbilitySystemComponent Source;
        public Vector3 TargetPosition;
        public List<AbilitySystemComponent> Targets = new List<AbilitySystemComponent>();
        public Dictionary<string, object> CustomData = new Dictionary<string, object>();

        public AbilityContext(AbilitySystemComponent source, Vector3 targetPosition)
        {
            Source = source;
            TargetPosition = targetPosition;
        }
    }
}

