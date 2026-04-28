using System.Collections.Generic;
using SpellBook.GAS.Core;
using UnityEngine;

namespace SpellBook.GAS.Targeting
{
    [CreateAssetMenu(fileName = "Circle Targeting", menuName = "GAS/Targeting/Circle")]
    public class CircleTargeting : TargetingStrategy
    {
        [SerializeField] private float radius = 5f;
        [SerializeField] private LayerMask targetLayer;

        public override IEnumerable<AbilitySystemComponent> GetTargets(Transform source, Vector3 targetPosition)
        {
            var results = new List<AbilitySystemComponent>();

            if (physicsMode == PhysicsMode.Physics3D)
            {
                var colliders = Physics.OverlapSphere(targetPosition, radius, targetLayer);
                foreach (var col in colliders)
                {
                    if (col.TryGetComponent<AbilitySystemComponent>(out var asc))
                    {
                        results.Add(asc);
                    }
                }
            }
            else
            {
                var colliders = Physics2D.OverlapCircleAll(targetPosition, radius, targetLayer);
                foreach (var col in colliders)
                {
                    if (col.TryGetComponent<AbilitySystemComponent>(out var asc))
                    {
                        results.Add(asc);
                    }
                }
            }
            
            return results;
        }
    }
}

