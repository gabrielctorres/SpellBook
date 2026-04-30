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
        public enum ProjectileSpawnMode { ThreeDimensional, TwoDimensional }

        [Header("Projectile Prefab")]
        public GameObject ProjectilePrefab;
        
        [Header("Impact Payload")]
        [SerializeReference]
        public List<AbilityAction> OnHitActions = new List<AbilityAction>();

        [Header("Spawn Configuration")]
        public ProjectileSpawnMode SpawnMode = ProjectileSpawnMode.ThreeDimensional;
        [Range(1, 20)] public int ProjectileCount = 1;
        [Range(0, 360)] public float SpreadAngle = 0f;
        public float DelayBetweenShots = 0f;

        [Header("Offset")]
        public Vector3 SpawnOffset = new Vector3(0, 1.2f, 1f);

        [Header("Homing (Optional)")]
        public bool HomeInOnTargets = false;
        public float HomingTurnSpeed = 10f;

        public override IEnumerator Execute(AbilityContext context)
        {
            if (ProjectilePrefab == null) yield break;

            Vector3 forward = (SpawnMode == ProjectileSpawnMode.ThreeDimensional) ? context.Source.transform.forward : context.Source.transform.right;
            Vector3 up = Vector3.up;

            Vector3 spawnBasePos = context.Source.transform.position + 
                                  up * SpawnOffset.y + 
                                  forward * SpawnOffset.z + 
                                  context.Source.transform.right * SpawnOffset.x;

            Vector3 targetDir = (context.TargetPosition - spawnBasePos).normalized;
            if (targetDir == Vector3.zero) targetDir = forward;

            float startAngle = -SpreadAngle / 2f;
            float angleStep = (ProjectileCount > 1) ? SpreadAngle / (ProjectileCount - 1) : 0f;

            for (int i = 0; i < ProjectileCount; i++)
            {
                float currentAngle = (ProjectileCount > 1) ? startAngle + (angleStep * i) : 0f;
                
                Quaternion finalRotation;
                Vector3 finalDirection;

                if (SpawnMode == ProjectileSpawnMode.ThreeDimensional)
                {
                    Quaternion spreadRotation = Quaternion.Euler(0, currentAngle, 0);
                    finalDirection = spreadRotation * targetDir;
                    finalRotation = Quaternion.LookRotation(finalDirection);
                }
                else
                {
                    float baseAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
                    finalRotation = Quaternion.Euler(0, 0, baseAngle + currentAngle);
                    finalDirection = finalRotation * Vector3.right;
                }

                // SOLICITA AO MANAGER (Ele gerencia os poolers internamente por prefab)
                GameObject projObj = PoolManager.Instance.Get(ProjectilePrefab, spawnBasePos, finalRotation);
                
                if (projObj.TryGetComponent<Projectile>(out var projScript))
                {
                    // Busca o pooler específico para este projétil para que ele saiba onde voltar
                    var pooler = PoolManager.Instance.GetPooler(ProjectilePrefab);
                    projScript.Setup(pooler, context.Source, OnHitActions);

                    // Determina se o spawner está virado para a direita (baseado no forward do transform)
                    bool spawnerFacingRight = context.Source.transform.right.x > 0;
                    projScript.SetDirection(finalDirection, finalRotation, spawnerFacingRight);

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


