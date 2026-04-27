using System;
using UnityEngine;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Effects
{
    [Serializable]
    public abstract class EffectAction
    {
        /// <summary>
        /// Executes an action triggered by a Gameplay Effect.
        /// </summary>
        /// <param name="source">The ASC that applied the effect.</param>
        /// <param name="target">The ASC that has the effect.</param>
        public abstract void Execute(AbilitySystemComponent source, AbilitySystemComponent target);
    }
}

