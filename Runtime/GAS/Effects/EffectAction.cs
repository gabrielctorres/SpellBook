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
        /// <param name="instance">The active effect instance (null for instant effects).</param>
        public abstract void Execute(AbilitySystemComponent source, AbilitySystemComponent target, ActiveEffectInstance instance = null);

        /// <summary>
        /// Cleans up any persistent state created by the action when the effect is removed.
        /// </summary>
        public virtual void Cleanup(AbilitySystemComponent source, AbilitySystemComponent target, ActiveEffectInstance instance) { }
    }
}

