using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Attributes
{
    public abstract class Formula : ScriptableObject
    {
        /// <summary>
        /// Calculates a value based on the provided Ability System Components.
        /// </summary>
        public abstract float Calculate(AbilitySystemComponent source, AbilitySystemComponent target);

        /// <summary>
        /// Returns the attributes this formula depends on for the Source ASC.
        /// </summary>
        public virtual IEnumerable<AttributeDefinition> GetSourceDependencies() => Enumerable.Empty<AttributeDefinition>();
    }
}

