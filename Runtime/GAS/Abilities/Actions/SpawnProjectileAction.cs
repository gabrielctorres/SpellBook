using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.Systems.Pooling;

namespace SpellBook.GAS.Abilities.Actions
{
    [Serializable]
    public class SpawnProjectileAction : AbilityAction
    {
        [Header("Projectile Prefab")]
        public GameObject ProjectilePrefab;
        
        [Header("Impact Payload")]
        [SerializeReference]
        public List<AbilityAction> OnHitActions = new List<AbilityAction>();

        [Header("Spawn Configuration")]
        [Range(1, 20)] public int ProjectileCount = 1;
        [Range(0, 360)] public float SpreadAngle = 0f;
        public float DelayBetweenShots = 0f;

        [Header("Homing (Optional)")]
        public bool HomeInOnTargets = false;
        public float HomingTurnSpeed = 10f;

        public override IEnumerator Execute(AbilityContext context)
        {
            if (ProjectilePrefab == null) yield break;

            Vector3 spawnBasePos = context.Source.transform.position + Vector3.up * 1.2f + context.Source.transform.forward * 1f;
            Vector3 targetDir = (context.TargetPosition - spawnBasePos).normalized;
            if (targetDir == Vector3.zero) targetDir = context.Source.transform.forward;

            float startAngle = -SpreadAngle / 2f;
            float angleStep = (ProjectileCount > 1) ? SpreadAngle / (ProjectileCount - 1) : 0f;

            for (int i = 0; i < ProjectileCount; i++)
            {
                float currentAngle = (ProjectileCount > 1) ? startAngle + (angleStep * i) : 0f;
                Quaternion spreadRotation = Quaternion.Euler(0, currentAngle, 0);
                Quaternion finalRotation = Quaternion.LookRotation(spreadRotation * targetDir);

                // SOLICITA AO MANAGER (Ele gerencia os poolers internamente por prefab)
                GameObject projObj = PoolManager.Instance.Get(ProjectilePrefab, spawnBasePos, finalRotation);
                
                if (projObj.TryGetComponent<Projectile>(out var projScript))
                {
                    // Busca o pooler específico para este projétil para que ele saiba onde voltar
                    var pooler = PoolManager.Instance.GetPooler(ProjectilePrefab);
                    projScript.Setup(pooler, context.Source, OnHitActions);

                    if (HomeInOnTargets && context.Targets.Count > 0)
                    {
                        int targetIndex = i % context.Targets.Count;
                        projScript.Target = context.Targets[targetIndex].transform;
                        projScript.IsHoming = true;
                        projScript.HomingTurnSpeed = HomingTurnSpeed;
                    }
                }

                if (DelayBetweenShots > 0 && i < ProjectileCount - 1)
                {
                    yield return new WaitForSeconds(DelayBetweenShots);
                }
            }

            yield return null;
        }
    }
}


