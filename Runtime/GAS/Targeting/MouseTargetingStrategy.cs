using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Targeting
{
    /// <summary>
    /// Estratégia de busca de alvos baseada na posição do mouse.
    /// Útil para habilidades de alvo único (Single Target).
    /// </summary>
    [CreateAssetMenu(fileName = "Mouse Targeting", menuName = "GAS/Targeting/Mouse")]
    public class MouseTargetingStrategy : TargetingStrategy
    {
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private LayerMask targetLayer;

        public override IEnumerable<AbilitySystemComponent> GetTargets(Transform source, Vector3 targetPosition)
        {
            var results = new List<AbilitySystemComponent>();
            
            // O targetPosition aqui é a posição do mouse enviada pelo Handler
            var colliders = Physics.OverlapSphere(targetPosition, radius, targetLayer);

            foreach (var col in colliders)
            {
                if (col.TryGetComponent<AbilitySystemComponent>(out var asc))
                {
                    // Evita que o jogador selecione a si mesmo
                    if (asc.transform == source) continue;

                    results.Add(asc);
                    
                    // Encontramos o alvo principal, podemos parar a busca
                    break; 
                }
            }
            return results;
        }
    }
}

