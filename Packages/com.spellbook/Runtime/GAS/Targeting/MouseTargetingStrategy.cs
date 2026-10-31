using System.Collections.Generic;
using UnityEngine;
using SpellBook.Core;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Targeting
{
    /// <summary>
    /// Estratégia de busca de alvos baseada na posição do mouse.
    /// Útil para habilidades de alvo único (Single Target): retorna o alvo válido mais próximo da origem.
    /// </summary>
    [CreateAssetMenu(fileName = "Mouse Targeting", menuName = "GAS/Targeting/Mouse")]
    [HelpURL(DocUrls.Gas + "#mousetargetingstrategy")]
    public class MouseTargetingStrategy : TargetingStrategy
    {
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private LayerMask targetLayer;

        public override IEnumerable<AbilitySystemComponent> GetTargets(AbilitySystemComponent source, Vector3 origin)
        {
            // A origem aqui é a posição do mouse enviada pelo Handler
            var results = new List<AbilitySystemComponent>();
            OverlapCircle(source, origin, radius, targetLayer, results);

            if (results.Count > 1)
            {
                SortByDistance(results, origin);
                results.RemoveRange(1, results.Count - 1);
            }
            return results;
        }
    }
}
