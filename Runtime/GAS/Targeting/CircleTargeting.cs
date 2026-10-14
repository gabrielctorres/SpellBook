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
        [Tooltip("0 = ilimitado. Quando limitado, prioriza os mais próximos da origem.")]
        [SerializeField, Min(0)] private int maxTargets = 0;

        public override IEnumerable<AbilitySystemComponent> GetTargets(AbilitySystemComponent source, Vector3 origin)
        {
            var results = new List<AbilitySystemComponent>();
            OverlapCircle(source, origin, radius, targetLayer, results);

            if (maxTargets > 0 && results.Count > maxTargets)
            {
                SortByDistance(results, origin);
                results.RemoveRange(maxTargets, results.Count - maxTargets);
            }
            return results;
        }
    }
}
