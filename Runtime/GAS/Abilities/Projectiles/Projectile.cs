using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Core;
using SpellBook.GAS.Abilities;
using SpellBook.Systems.Pooling;

namespace SpellBook.GAS.Abilities.Projectiles
{
    /// <summary>
    /// Projétil universal (2D/3D) inspirado no TopDown Engine.
    /// Utiliza o sistema de ComponentPooler para performance.
    /// </summary>
    public class Projectile : MonoBehaviour, IPoolable
    {
        public enum MovementVectors { Forward, Right, Up }

        [Header("Movement")]
        /// if true, the projectile will rotate at initialization towards its rotation
        public bool FaceDirection = true;
        /// if true, the projectile will rotate towards movement
        public bool FaceMovement = false;
        /// if FaceMovement is true, the projectile's vector specified below will be aligned to the movement vector, usually you'll want to go with Forward in 3D, Right in 2D
        public MovementVectors MovementVector = MovementVectors.Forward;

        public float Speed = 20f;
        public float Acceleration = 0f;
        public Vector3 Direction = Vector3.forward;
        /// if set to true, the spawner can change the direction of the object. If not the one set in its inspector will be used.
        public bool DirectionCanBeChangedBySpawner = true;
        
        [Header("Flipping")]
        /// the flip factor to apply if and when the projectile is mirrored
        public Vector3 FlipValue = new Vector3(-1, 1, 1);
        /// set this to true if your projectile's model (or sprite) is facing right, false otherwise
        public bool ProjectileIsFacingRight = true;

        [Header("Spawn")]
        /// the initial delay during which the projectile can't be destroyed or cause damage
        public float InitialInvulnerabilityDuration = 0f;
        /// should the projectile damage its owner?
        public bool DamageOwner = false;

        [Header("Lifetime")]
        public float Lifetime = 5f;
        
        [Header("Homing (Optional)")]
        public bool IsHoming = false;
        public float HomingTurnSpeed = 10f;
        public Transform Target; 

        // O que o projétil fará ao colidir
        [HideInInspector] public List<AbilityAction> OnHitActions;
        [HideInInspector] public AbilitySystemComponent Source;
        
        // Referência ao Pool local que o criou para retornar corretamente
        protected ComponentPooler _originPool;

        protected Vector3 _movement;
        protected float _initialSpeed;
        protected SpriteRenderer _spriteRenderer;
        protected Rigidbody _rigidBody;
        protected Rigidbody2D _rigidBody2D;
        protected Collider _collider;
        protected Collider2D _collider2D;
        protected bool _facingRightInitially;
        protected bool _initialFlipX;
        protected Vector3 _initialLocalScale;
        protected bool _shouldMove = true;
        protected bool _spawnerIsFacingRight;
        protected bool _isInvulnerable = false;

        protected virtual void Awake()
        {
            _facingRightInitially = ProjectileIsFacingRight;
            _initialSpeed = Speed;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _rigidBody = GetComponent<Rigidbody>();
            _rigidBody2D = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider>();
            _collider2D = GetComponent<Collider2D>();
            
            if (_spriteRenderer != null) { _initialFlipX = _spriteRenderer.flipX; }
            _initialLocalScale = transform.localScale;
        }

        public void Setup(ComponentPooler pool, AbilitySystemComponent source, List<AbilityAction> onHitActions)
        {
            _originPool = pool;
            Source = source;
            OnHitActions = onHitActions;
        }

        protected virtual void OnEnable()
        {
            Initialization();
            if (Lifetime > 0) StartCoroutine(AutoReturnToPool(Lifetime));
            if (InitialInvulnerabilityDuration > 0)
            {
                StartCoroutine(InitialInvulnerability());
            }
        }

        protected virtual void Initialization()
        {
            Speed = _initialSpeed;
            ProjectileIsFacingRight = _facingRightInitially;
            if (_spriteRenderer != null) { _spriteRenderer.flipX = _initialFlipX; }
            transform.localScale = _initialLocalScale;
            _shouldMove = true;
            _isInvulnerable = false;

            if (_collider != null) _collider.enabled = true;
            if (_collider2D != null) _collider2D.enabled = true;
        }

        protected virtual IEnumerator InitialInvulnerability()
        {
            _isInvulnerable = true;
            yield return new WaitForSeconds(InitialInvulnerabilityDuration);
            _isInvulnerable = false;
        }

        private IEnumerator AutoReturnToPool(float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool();
        }

        protected virtual void FixedUpdate()
        {
            if (_shouldMove)
            {
                Movement();
            }
        }

        public virtual void Movement()
        {
            if (IsHoming && Target != null)
            {
                HandleHoming();
            }

            _movement = Direction * Speed * Time.deltaTime;

            if (_rigidBody != null)
            {
                _rigidBody.MovePosition(this.transform.position + _movement);
            }
            if (_rigidBody2D != null)
            {
                _rigidBody2D.MovePosition((Vector2)this.transform.position + (Vector2)_movement);
            }
            if (_rigidBody == null && _rigidBody2D == null)
            {
                transform.Translate(_movement, Space.World);
            }

            // We apply the acceleration to increase the speed
            Speed += Acceleration * Time.deltaTime;
        }

        protected virtual void HandleHoming()
        {
            Vector3 targetDir = (Target.position - transform.position).normalized;
            if (targetDir == Vector3.zero) return;

            // Detecta se estamos operando em 2D ou 3D
            bool is2D = _rigidBody2D != null || (_spriteRenderer != null && _rigidBody == null);

            if (is2D)
            {
                float angle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
                Quaternion targetRotation = Quaternion.Euler(0, 0, angle);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, HomingTurnSpeed * Time.deltaTime);
                
                // Em 2D, a direção de movimento é tipicamente o 'right' ou 'up' do transform após a rotação
                switch (MovementVector)
                {
                    case MovementVectors.Right: Direction = transform.right; break;
                    case MovementVectors.Up: Direction = transform.up; break;
                    case MovementVectors.Forward: Direction = transform.forward; break;
                }
            }
            else
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, HomingTurnSpeed * Time.deltaTime);
                Direction = transform.forward;
            }
        }

        /// <summary>
        /// Sets the projectile's direction.
        /// </summary>
        public virtual void SetDirection(Vector3 newDirection, Quaternion newRotation, bool spawnerIsFacingRight = true)
        {
            _spawnerIsFacingRight = spawnerIsFacingRight;

            if (DirectionCanBeChangedBySpawner)
            {
                Direction = newDirection;
            }
            if (ProjectileIsFacingRight != spawnerIsFacingRight)
            {
                Flip();
            }
            if (FaceDirection)
            {
                transform.rotation = newRotation;
            }

            if (FaceMovement)
            {
                switch (MovementVector)
                {
                    case MovementVectors.Forward:
                        transform.forward = Direction;
                        break;
                    case MovementVectors.Right:
                        transform.right = Direction;
                        break;
                    case MovementVectors.Up:
                        transform.up = Direction;
                        break;
                }
            }
        }

        /// <summary>
        /// Flip the projectile
        /// </summary>
        protected virtual void Flip()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.flipX = !_spriteRenderer.flipX;
            }
            else
            {
                this.transform.localScale = Vector3.Scale(this.transform.localScale, FlipValue);
            }
            // Nota: Não invertemos ProjectileIsFacingRight aqui para manter compatibilidade com o padrão de chamada única no spawn
        }

        public virtual void ReturnToPool()
        {
            if (!gameObject.activeInHierarchy) return;
            
            _shouldMove = false;
            if (_collider != null) _collider.enabled = false;
            if (_collider2D != null) _collider2D.enabled = false;

            StopAllCoroutines();
            if (_originPool != null)
            {
                _originPool.ReturnToPool(this.gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // IPoolable Implementation
        public virtual void OnSpawnFromPool() { }

        public virtual void OnReturnToPool()
        {
            StopAllCoroutines();
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            HandleCollision(other.gameObject);
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            HandleCollision(other.gameObject);
        }

        protected virtual void HandleCollision(GameObject other)
        {
            if (_isInvulnerable) return;

            AbilitySystemComponent hitASC = other.GetComponent<AbilitySystemComponent>();
            if (hitASC != null && hitASC == Source && !DamageOwner) return;

            HandleImpact(transform.position, hitASC);
        }

        protected virtual void HandleImpact(Vector3 impactPoint, AbilitySystemComponent hitTarget)
        {
            if (OnHitActions != null && OnHitActions.Count > 0 && Source != null)
            {
                var impactContext = new AbilityContext(Source, impactPoint);
                if (hitTarget != null) impactContext.Targets.Add(hitTarget);

                Source.StartCoroutine(ExecuteImpactPipeline(impactContext));
            }

            ReturnToPool();
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


