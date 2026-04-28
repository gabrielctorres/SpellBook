using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Targeting
{
    public enum PhysicsMode { Physics3D, Physics2D }

    public abstract class TargetingStrategy : ScriptableObject
    {
        [SerializeField] protected PhysicsMode physicsMode = PhysicsMode.Physics3D;
        public abstract IEnumerable<AbilitySystemComponent> GetTargets(Transform source, Vector3 targetPosition);
    }
}

