using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using UnityEngine;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Abilities.Projectiles;

namespace SpellBook.GAS.Abilities.Actions
{
    [Serializable, TypeCategory("Mundo", Summary = "Lança projéteis que rodam ações ao acertar.")]
    public class SpawnProjectileAction : GameplayAction
    {
        public enum ProjectileSpawnMode { ThreeDimensional, TwoDimensional }

        [Header("Projectile Prefab")]
        public GameObject ProjectilePrefab;
        [Tooltip("Cria e devolve os projéteis. Vazio usa o spawner padrão, por Instantiate.")]
        public SpawnerAsset Spawner;

        [Header("Impact Payload")]
        [SerializeReference]
        [TypePicker]
        public List<GameplayAction> OnHitActions = new List<GameplayAction>();

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

        internal override bool ReadsInstigator => true;

        /// <summary>Origem e direção da rajada, medidas no primeiro tiro e mantidas entre os tiros.</summary>
        private struct Burst
        {
            public Vector3 Position;
            public Vector3 Direction;
        }

        // frame.Index: próximo tiro; frame.Data: a Burst, guardada só quando há espera entre os tiros
        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            AbilitySystemComponent caster = context.Instigator;
            if (ProjectilePrefab == null || caster == null) return ActionStatus.Done;

            Burst burst = frame.Phase == 0 ? Aim(caster, context.Point) : (Burst)frame.Data;
            SpawnerAsset spawner = SpawnerAsset.OrDefault(Spawner);
            while (frame.Index < ProjectileCount)
            {
                Fire(context, caster, spawner, burst, frame.Index);
                frame.Index++;

                if (DelayBetweenShots > 0 && frame.Index < ProjectileCount)
                {
                    if (frame.Phase == 0)
                    {
                        frame.Phase = 1;
                        frame.Data = burst;
                    }
                    return ActionStatus.Wait(DelayBetweenShots);
                }
            }

            return ActionStatus.Done;
        }

        public override void CollectEffects(List<GameplayEffect> effects)
        {
            ActionLists.CollectEffects(OnHitActions, effects);
        }

        // O impacto é um evento próprio, com o acertado como alvo
        internal override void CollectChildLists(List<ChildActionList> results)
        {
            results.Add(new ChildActionList(nameof(OnHitActions), OnHitActions, ownEvent: true));
        }

        private Burst Aim(AbilitySystemComponent caster, Vector3 point)
        {
            Transform transform = caster.transform;
            Vector3 forward = (SpawnMode == ProjectileSpawnMode.ThreeDimensional) ? transform.forward : transform.right;
            Vector3 up = Vector3.up;

            Vector3 position = transform.position +
                               up * SpawnOffset.y +
                               forward * SpawnOffset.z +
                               transform.right * SpawnOffset.x;

            Vector3 direction = (point - position).normalized;
            if (direction == Vector3.zero) direction = forward;
            return new Burst { Position = position, Direction = direction };
        }

        private void Fire(ActionContext context, AbilitySystemComponent caster, SpawnerAsset spawner, Burst burst, int shot)
        {
            float startAngle = -SpreadAngle / 2f;
            float angleStep = (ProjectileCount > 1) ? SpreadAngle / (ProjectileCount - 1) : 0f;
            float currentAngle = (ProjectileCount > 1) ? startAngle + (angleStep * shot) : 0f;

            Quaternion finalRotation;
            Vector3 finalDirection;

            if (SpawnMode == ProjectileSpawnMode.ThreeDimensional)
            {
                Quaternion spreadRotation = Quaternion.Euler(0, currentAngle, 0);
                finalDirection = spreadRotation * burst.Direction;
                finalRotation = Quaternion.LookRotation(finalDirection);
            }
            else
            {
                float baseAngle = Mathf.Atan2(burst.Direction.y, burst.Direction.x) * Mathf.Rad2Deg;
                finalRotation = Quaternion.Euler(0, 0, baseAngle + currentAngle);
                finalDirection = finalRotation * Vector3.right;
            }

            GameObject projObj = spawner.Spawn(ProjectilePrefab, burst.Position, finalRotation);

            if (projObj == null)
            {
                // Spawner sem instância (ex.: pool vazio que não cresce): pula só este tiro e mantém a cadência
                Debug.LogWarning($"[SpawnProjectileAction] O spawner '{spawner.name}' não criou uma instância de '{ProjectilePrefab.name}'; o tiro foi pulado.", spawner);
            }
            else if (projObj.TryGetComponent<Projectile>(out var projScript))
            {
                // O projétil volta pelo mesmo spawner que o criou (ADR 0015)
                projScript.Setup(spawner, context, OnHitActions);

                // Determina se o spawner está virado para a direita (baseado no forward do transform)
                bool spawnerFacingRight = caster.transform.right.x > 0;
                projScript.SetDirection(finalDirection, finalRotation, spawnerFacingRight);

                List<AbilitySystemComponent> targets = context.EventTargets;
                targets.RemoveAll(t => t == null);
                if (HomeInOnTargets && targets.Count > 0)
                {
                    int targetIndex = shot % targets.Count;
                    projScript.Target = targets[targetIndex].transform;
                    projScript.IsHoming = true;
                    projScript.HomingTurnSpeed = HomingTurnSpeed;
                }
            }
        }
    }
}
