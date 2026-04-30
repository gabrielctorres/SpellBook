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
        private ComponentPooler _originPool;

        protected Vector3 _movement;
        protected float _initialSpeed;
        protected SpriteRenderer _spriteRenderer;
        protected Rigidbody _rigidBody;
        protected Rigidbody2D _rigidBody2D;
        protected bool _facingRightInitially;
        protected bool _initialFlipX;
        protected Vector3 _initialLocalScale;
        protected bool _shouldMove = true;
        protected bool _spawnerIsFacingRight;

        protected virtual void Awake()
        {
            _facingRightInitially = ProjectileIsFacingRight;
            _initialSpeed = Speed;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _rigidBody = GetComponent<Rigidbody>();
            _rigidBody2D = GetComponent<Rigidbody2D>();
            
            if (_spriteRenderer != null) { _initialFlipX = _spriteRenderer.flipX; }
            _initialLocalScale = transform.localScale;
        }

        public void Setup(ComponentPooler pool, AbilitySystemComponent source, List<AbilityAction> onHitActions)
        {
            _originPool = pool;
            Source = source;
            OnHitActions = onHitActions;
        }

        private void OnEnable()
        {
            Initialization();
            StartCoroutine(AutoReturnToPool(Lifetime));
        }

        protected virtual void Initialization()
        {
            Speed = _initialSpeed;
            ProjectileIsFacingRight = _facingRightInitially;
            if (_spriteRenderer != null) { _spriteRenderer.flipX = _initialFlipX; }
            transform.localScale = _initialLocalScale;
            _shouldMove = true;
        }

        private IEnumerator AutoReturnToPool(float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool();
        }

        private void Update()
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
                Vector3 homingDir = (Target.position - transform.position).normalized;
                if (homingDir != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(homingDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, HomingTurnSpeed * Time.deltaTime);
                    Direction = transform.forward;
                }
            }

            _movement = Direction * Speed * Time.deltaTime;

            if (_rigidBody != null)
            {
                _rigidBody.MovePosition(this.transform.position + _movement);
            }
            else if (_rigidBody2D != null)
            {
                _rigidBody2D.MovePosition((Vector2)this.transform.position + (Vector2)_movement);
            }
            else
            {
                transform.Translate(_movement, Space.World);
            }

            // We apply the acceleration to increase the speed
            Speed += Acceleration * Time.deltaTime;
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
                        transform.forward = newDirection;
                        break;
                    case MovementVectors.Right:
                        transform.right = newDirection;
                        break;
                    case MovementVectors.Up:
                        transform.up = newDirection;
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
            ProjectileIsFacingRight = !ProjectileIsFacingRight;
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


