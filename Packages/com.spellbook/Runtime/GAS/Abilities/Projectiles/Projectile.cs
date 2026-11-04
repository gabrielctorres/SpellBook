using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using SpellBook.Core;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Abilities;
using SpellBook.Time;

namespace SpellBook.GAS.Abilities.Projectiles
{
    /// <summary>
    /// Projétil universal (2D/3D) inspirado no TopDown Engine.
    /// Volta pelo <see cref="SpawnerAsset"/> recebido no <c>Setup</c> e reinicia o estado no <c>OnEnable</c> (ADR 0015).
    /// </summary>
    [HelpURL(DocUrls.Gas + "#projectile")]
    public class Projectile : MonoBehaviour, IValidatable
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

        [Header("Pierce")]
        [Tooltip("Alvos que o projétil atravessa depois do primeiro impacto. 0 termina no primeiro.")]
        public int Pierce = 0;
        [Tooltip("Atravessa todos os alvos; termina só pelo Lifetime ou por um colisor sem ASC.")]
        public bool UnlimitedPierce = false;
        [Tooltip("Com perfuração, cada alvo é acertado uma vez por vida do projétil, até 32 alvos; além disso, o choque conta como repetido.")]
        public bool HitEachTargetOnce = true;
        
        [Header("Homing (Optional)")]
        public bool IsHoming = false;
        public float HomingTurnSpeed = 10f;
        public Transform Target; 

        [Header("Time")]
        [Tooltip("Fonte de tempo do movimento, do Lifetime e da InitialInvulnerabilityDuration. Vazio usa o tempo escalado da Unity.")]
        [SerializeField] private TimeSourceAsset timeSource;

        // O que o projétil fará ao colidir
        [HideInInspector, FormerlySerializedAs("OnHitActions")] public List<GameplayAction> OnImpact;
        [HideInInspector] public AbilitySystemComponent Source;

        /// <summary>
        /// Filtro de candidato (ADR 0035), avaliado em cada choque com um ASC: <c>Owner</c> = quem lançou e <c>Target</c> =
        /// <see cref="AbilityTarget.AtUnit"/> do ASC atingido. Quem não passa é atravessado sem impacto, sem gastar
        /// perfuração e sem terminar. Nulo aceita todos.
        /// </summary>
        [SerializeReference]
        [TypePicker]
        public Condition<ConditionContext> ImpactFilter;

        /// <summary>
        /// Roda uma vez quando o projétil termina, no ASC de quem lançou, com o motivo em
        /// <see cref="ActionContext.EndReason"/> e a posição dele em <see cref="ActionContext.Point"/>. Vazia, o projétil só
        /// volta ao pool.
        /// </summary>
        [SerializeReference]
        [TypePicker]
        [ActionEvent("Ao terminar", DefaultTarget.EventTargets, false)]
        public List<GameplayAction> OnEnded = new List<GameplayAction>();

        // Spawner que criou o projétil; vazio volta pelo SpawnerAsset.Default
        protected SpawnerAsset _spawner;

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
        /// <summary>Segundos de vida e de invulnerabilidade que faltam, contados no <c>FixedUpdate</c> pela fonte de tempo.</summary>
        private float _lifetimeRemaining;
        private float _invulnerabilityRemaining;
        private bool _released;
        /// <summary>Capacidade do vetor "já acertou" (P6): os acertos além dele contam como repetidos.</summary>
        public const int HitCapacity = 32;
        private readonly AbilitySystemComponent[] _hitTargets = new AbilitySystemComponent[HitCapacity];
        private int _hitTargetCount;
        private int _impactCount;
        /// <summary>Delta do último <c>FixedUpdate</c>, lido da fonte de tempo por diferença.</summary>
        protected float _deltaTime;
        private double _timeCursor;

        private ITimeSource TimeSource => timeSource != null ? timeSource : TimeSourceAsset.Scaled;

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

        // Modelo do contexto de impacto e de término, derivado no Setup do contexto de quem disparou (habilidade, nível e
        // instigador). É derivado já no Setup porque o contexto de quem disparou volta ao pool do executor quando a lista
        // dele acaba. Cada impacto, o término e o filtro leem dele; o modelo volta ao pool do ASC dono no término ou num
        // novo Setup.
        private ActionContext _impactTemplate;

        public void Setup(SpawnerAsset spawner, AbilitySystemComponent source, List<GameplayAction> onImpact)
        {
            _spawner = spawner;
            Source = source;
            OnImpact = onImpact;
            ReleaseImpactTemplate();
        }

        /// <summary>
        /// Como <see cref="Setup(SpawnerAsset, AbilitySystemComponent, List{GameplayAction})"/>, com o instigador de
        /// <paramref name="parentContext"/> como <see cref="Source"/>. O impacto roda num contexto derivado dele
        /// (<see cref="ActionContext.CreateDerived"/>), com o ponto de impacto; "Ao terminar" também.
        /// </summary>
        public void Setup(SpawnerAsset spawner, ActionContext parentContext, List<GameplayAction> onImpact)
        {
            Setup(spawner, parentContext?.Instigator, onImpact);
            if (parentContext != null)
            {
                _impactTemplate = parentContext.CreateDerived(parentContext.Point);
                // Contexto sem executor (criado à mão): os impactos alugam do pool de quem lançou
                if (_impactTemplate.Owner == null) _impactTemplate.Owner = Source;
            }
        }

        private void ReleaseImpactTemplate()
        {
            if (_impactTemplate == null) return;
            AbilitySystemComponent owner = _impactTemplate.Owner;
            if (owner != null) owner.Runner.ReturnContext(_impactTemplate);
            _impactTemplate = null;
        }

        /// <summary>
        /// Acusa <see cref="Pierce"/> negativo (erro); perfuração acima do que o "já acertou" guarda, "sem limite" sem
        /// <see cref="Lifetime"/>, perfuração sem <see cref="HitEachTargetOnce"/> e, em <see cref="OnEnded"/>, ação que
        /// lança este mesmo projétil (avisos). Valida a lista "Ao terminar".
        /// </summary>
        public virtual void Validate(ValidationResult result)
        {
            if (Pierce < 0) result.AddError($"Pierce é {Pierce}: use 0 para terminar no primeiro impacto.", nameof(Pierce));
            else if (Pierce >= HitCapacity)
            {
                result.AddWarning($"Pierce {Pierce} passa de {HitCapacity - 1}: depois de {HitCapacity} alvos, o acerto conta como repetido.",
                    nameof(Pierce));
            }

            if (UnlimitedPierce && Lifetime <= 0f)
            {
                result.AddWarning("Unlimited Pierce sem Lifetime: o projétil só termina num colisor sem ASC.", nameof(UnlimitedPierce));
            }

            if (!HitEachTargetOnce && (UnlimitedPierce || Pierce > 0))
            {
                result.AddWarning("Perfuração sem Hit Each Target Once: o mesmo alvo pode ser acertado a cada choque.", nameof(HitEachTargetOnce));
            }

            ActionLists.Validate(this, nameof(OnEnded), OnEnded, result, (action, path, _) =>
            {
                if (action is SpawnProjectileAction spawn && spawn.ProjectilePrefab != null && spawn.ProjectilePrefab == gameObject)
                {
                    result.AddWarning("\"Ao terminar\" lança este mesmo projétil: possível laço sem fim.", path);
                }
            });
        }

        protected virtual void OnEnable()
        {
            _timeCursor = TimeSource.ElapsedTime;
            _deltaTime = 0f;
            Initialization();
            _lifetimeRemaining = Lifetime > 0f ? Lifetime : float.PositiveInfinity; // Lifetime <= 0: sem limite
            _invulnerabilityRemaining = InitialInvulnerabilityDuration;
            if (_invulnerabilityRemaining > 0f) _isInvulnerable = true; // sem contador, a flag fica com a subclasse
            _released = false;
            Array.Clear(_hitTargets, 0, _hitTargetCount);
            _hitTargetCount = 0;
            _impactCount = 0;
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

        protected virtual void FixedUpdate()
        {
            // Consumo por diferença (ADR 0014); dentro do FixedUpdate o relógio escalado é o de física
            double now = TimeSource.ElapsedTime;
            _deltaTime = (float)(now - _timeCursor);
            _timeCursor = now;

            if (_invulnerabilityRemaining > 0f)
            {
                _invulnerabilityRemaining -= _deltaTime;
                if (_invulnerabilityRemaining <= 0f) _isInvulnerable = false;
            }

            _lifetimeRemaining -= _deltaTime;
            if (_lifetimeRemaining <= 0f)
            {
                End(ActionEndReason.Lifetime);
                return;
            }

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

            _movement = Direction * Speed * _deltaTime;

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
            Speed += Acceleration * _deltaTime;
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
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, HomingTurnSpeed * _deltaTime);
                
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
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, HomingTurnSpeed * _deltaTime);
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

        /// <summary>Termina o projétil com o motivo <see cref="ActionEndReason.Released"/>.</summary>
        public virtual void ReturnToPool()
        {
            End(ActionEndReason.Released);
        }

        /// <summary>
        /// Termina o projétil uma vez: roda <see cref="OnEnded"/> com <paramref name="reason"/> e devolve ao spawner.
        /// </summary>
        protected virtual void End(ActionEndReason reason)
        {
            // O spawner pode desativar só no fim do quadro: volta uma vez só até o próximo OnEnable
            if (_released || !gameObject.activeInHierarchy) return;
            _released = true;

            _shouldMove = false;
            if (_collider != null) _collider.enabled = false;
            if (_collider2D != null) _collider2D.enabled = false;

            StopAllCoroutines();
            if (OnEnded != null && OnEnded.Count > 0 && Source != null && Source.isActiveAndEnabled)
            {
                ActionContext endContext = RentContext(transform.position);
                endContext.EndReason = reason;
                // Sem alvo e sem dono, como o impacto: o projétil volta ao pool logo abaixo
                Source.RunActions(OnEnded, endContext, null);
            }
            ReleaseImpactTemplate();
            SpawnerAsset.OrDefault(_spawner).Release(gameObject);
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
            if (_isInvulnerable || _released) return;

            // O colisor pode estar num filho da unidade: o ASC é o primeiro da hierarquia, sem alocar quando não há
            AbilitySystemComponent hitASC = FindAsc(other.transform);
            if (hitASC != null)
            {
                if (hitASC == Source && !DamageOwner) return;
                if (TracksHits && IsRepeatedHit(hitASC)) return;
                if (ImpactFilter != null && !PassesImpactFilter(hitASC)) return;
            }

            HandleImpact(transform.position, hitASC);
        }

        private bool TracksHits => HitEachTargetOnce && (UnlimitedPierce || Pierce > 0);

        // Filtro de candidato (ADR 0035): quem lançou avalia o ASC atingido, na habilidade e no nível do lançamento
        private bool PassesImpactFilter(AbilitySystemComponent candidate)
        {
            var context = new ConditionContext(Source, _impactTemplate?.Ability, _impactTemplate?.Level ?? 1, null,
                AbilityTarget.AtUnit(candidate), null);
            return ImpactFilter.Evaluate(in context);
        }

        private ActionContext RentContext(Vector3 point)
        {
            ActionContext context = _impactTemplate != null ? _impactTemplate.CreateDerived(point) : Source.Runner.RentContext();
            context.SetPoint(point);
            return context;
        }

        private static AbilitySystemComponent FindAsc(Transform node)
        {
            for (; node != null; node = node.parent)
            {
                if (node.TryGetComponent(out AbilitySystemComponent asc)) return asc;
            }
            return null;
        }

        // Alvo já acertado, ou vetor cheio (P6): o choque conta como repetido
        private bool IsRepeatedHit(AbilitySystemComponent target)
        {
            if (_hitTargetCount >= HitCapacity) return true;
            for (int i = 0; i < _hitTargetCount; i++)
            {
                if (_hitTargets[i] == target) return true;
            }
            return false;
        }

        /// <summary>
        /// Roda <see cref="OnImpact"/> no ASC de quem lançou e conta o impacto. Termina quando os impactos passam de
        /// <see cref="Pierce"/> (<see cref="ActionEndReason.Hit"/>), salvo com <see cref="UnlimitedPierce"/>. Sem
        /// <paramref name="hitTarget"/> (colisor sem ASC), termina sempre, mesmo com perfuração
        /// (<see cref="ActionEndReason.Obstacle"/>, P7).
        /// </summary>
        protected virtual void HandleImpact(Vector3 impactPoint, AbilitySystemComponent hitTarget)
        {
            if (OnImpact != null && OnImpact.Count > 0 && Source != null && Source.isActiveAndEnabled)
            {
                ActionContext impactContext = RentContext(impactPoint);
                if (hitTarget != null) impactContext.EventTargets.Add(hitTarget);

                // Roda no ASC de quem lançou, sem dono: o projétil pode voltar ao pool logo abaixo
                Source.RunActions(OnImpact, impactContext, null);
            }

            if (hitTarget != null)
            {
                if (TracksHits && _hitTargetCount < HitCapacity) _hitTargets[_hitTargetCount++] = hitTarget;
                _impactCount++;
                if (UnlimitedPierce || _impactCount <= Pierce) return;
            }

            End(hitTarget != null ? ActionEndReason.Hit : ActionEndReason.Obstacle);
        }
    }
}


