using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Core;
using SpellBook.GAS.Abilities;
using SpellBook.Systems.Pooling;

namespace SpellBook.GAS.Abilities.Projectiles
{
    /// <summary>
    /// Projétil que utiliza o sistema de ComponentPooler para performance.
    /// </summary>
    public class Projectile : MonoBehaviour, IPoolable
    {
        [Header("Movement")]
        public float Speed = 20f;
        public float Lifetime = 5f;
        
        [Header("Homing (Optional)")]
        public bool IsHoming = false;
        public float HomingTurnSpeed = 10f;
        public Transform Target; 

        // O que o projétil fará ao colidir
        [HideInInspector] public List<AbilityAction> OnHitActions;
        [HideInInspector] public AbilitySystemComponent Source;
        
        // Referência ao Pool local que o criou para retornar corretamente
        private ComponentPooler _originPool;

        public void Setup(ComponentPooler pool, AbilitySystemComponent source, List<AbilityAction> onHitActions)
        {
            _originPool = pool;
            Source = source;
            OnHitActions = onHitActions;
        }

        private void OnEnable()
        {
            StartCoroutine(AutoReturnToPool(Lifetime));
        }

        private IEnumerator AutoReturnToPool(float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool();
        }

        private void Update()
        {
            if (IsHoming && Target != null)
            {
                Vector3 direction = (Target.position - transform.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, HomingTurnSpeed * Time.deltaTime);
                }
            }

            transform.Translate(Vector3.forward * Speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<AbilitySystemComponent>(out var hitASC))
            {
                if (hitASC == Source) return;
            }

            HandleImpact(transform.position, hitASC);
        }

        private void HandleImpact(Vector3 impactPoint, AbilitySystemComponent hitTarget)
        {
            if (OnHitActions != null && OnHitActions.Count > 0 && Source != null)
            {
                var impactContext = new AbilityContext(Source, impactPoint);
                if (hitTarget != null) impactContext.Targets.Add(hitTarget);

                Source.StartCoroutine(ExecuteImpactPipeline(impactContext));
            }

            ReturnToPool();
        }

        public void ReturnToPool()
        {
            StopAllCoroutines();
            if (_originPool != null)
            {
                _originPool.ReturnToPool(this.gameObject);
            }
            else
            {
                Destroy(gameObject); // Fallback
            }
        }

        // IPoolable Implementation
        public void OnSpawnFromPool()
        {
            // Resetar estados aqui se necessário (ex: Velocity = Vector3.zero)
        }

        public void OnReturnToPool()
        {
            // Limpar trilhas, desativar som, etc.
            StopAllCoroutines();
        }

        private IEnumerator ExecuteImpactPipeline(AbilityContext context)
        {
            foreach (var action in OnHitActions)
            {
                if (action == null) continue;
                yield return action.Execute(context);
            }
        }
    }
}


