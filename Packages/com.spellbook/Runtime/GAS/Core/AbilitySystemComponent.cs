using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.Core;
using SpellBook.Tags;
using SpellBook.Time;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Abilities;
namespace SpellBook.GAS.Core
{
    [RequireComponent(typeof(AttributeSet))]
    [HelpURL(DocUrls.Gas + "#abilitysystemcomponent")]
    public class AbilitySystemComponent : MonoBehaviour
    {
        [SerializeField] private AttributeSet attributeSet;
        [Tooltip("Habilidades concedidas automaticamente no Awake.")]
        [SerializeField] private List<GameplayAbility> startingAbilities = new List<GameplayAbility>();
        [Tooltip("Fonte de tempo de efeitos e recargas. Vazio usa o tempo escalado da Unity.")]
        [SerializeField] private TimeSourceAsset timeSource;
        [Tooltip("Avança sozinho no Update pela fonte de tempo. Desligado, só avança por Tick.")]
        [SerializeField] private bool autoTick = true;

        private ITimeSource _timeSourceOverride;
        private double _timeCursor;
        private double _tickedTime;

        // Contagem de referência: várias fontes podem conceder a mesma tag
        private readonly Dictionary<GameplayTag, int> _activeTags = new Dictionary<GameplayTag, int>();
        private readonly List<ActiveEffectInstance> _activeEffects = new List<ActiveEffectInstance>();
        private readonly List<AbilitySpec> _abilities = new List<AbilitySpec>();

        public event Action<GameplayTag, bool> OnTagChanged;
        public event Action<AbilitySpec> OnAbilityActivated;
        public event Action<AbilitySpec, bool> OnAbilityEnded; // spec, cancelled
        public event Action<GameplayAbility, AbilityActivationResult> OnAbilityFailed;

        public AttributeSet AttributeSet => attributeSet;
        public IReadOnlyList<AbilitySpec> Abilities => _abilities;

        /// <summary>
        /// Fonte de tempo em uso: a dada por <see cref="SetTimeSource"/>, senão a do Inspector, senão
        /// <see cref="TimeSourceAsset.Scaled"/>.
        /// </summary>
        public ITimeSource TimeSource
        {
            get
            {
                if (_timeSourceOverride != null) return _timeSourceOverride;
                return timeSource != null ? timeSource : TimeSourceAsset.Scaled;
            }
        }

        /// <summary>Relógio próprio do ASC: a soma de todos os <see cref="Tick"/> com delta positivo, em segundos.</summary>
        public double TickedTime => _tickedTime;

        private void Awake()
        {
            if (attributeSet == null) attributeSet = GetComponent<AttributeSet>();

            if (startingAbilities != null)
            {
                foreach (var ability in startingAbilities) GiveAbility(ability);
            }
        }

        private void OnEnable()
        {
            OnTagChanged += HandleTagChangedForCancellation;
            _timeCursor = TimeSource.ElapsedTime;
        }

        private void OnDisable()
        {
            OnTagChanged -= HandleTagChangedForCancellation;
            CancelAllAbilities();
        }

        private void Update()
        {
            // Consumo por diferença (ADR 0014): o cursor anda mesmo sem autoTick, para não saltar ao religar
            double now = TimeSource.ElapsedTime;
            float deltaTime = (float)(now - _timeCursor);
            _timeCursor = now;
            if (autoTick) Tick(deltaTime);
        }

        /// <summary>
        /// Troca a fonte de tempo por código (por exemplo, um <see cref="ManualTimeSource"/> num teste).
        /// <c>null</c> volta à fonte do Inspector. O tempo passado na fonte antiga não é contado.
        /// </summary>
        public void SetTimeSource(ITimeSource source)
        {
            _timeSourceOverride = source;
            _timeCursor = TimeSource.ElapsedTime;
        }

        /// <summary>
        /// Avança efeitos e recargas em <paramref name="deltaTime"/> segundos e soma em <see cref="TickedTime"/>.
        /// Ignora delta zero ou negativo. Com <c>autoTick</c> ligado, o próprio ASC chama a cada quadro.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f)) return;

            _tickedTime += deltaTime;
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                _activeEffects[i].Update(deltaTime);
            }
        }

        public float GetAttributeValue(AttributeDefinition def)
        {
            return attributeSet != null ? attributeSet.GetValue(def) : 0f;
        }

        #region Ability Management
        /// <summary>
        /// Concede uma habilidade ao ASC. Se já concedida, retorna o spec existente.
        /// </summary>
        public AbilitySpec GiveAbility(GameplayAbility ability, int level = 1)
        {
            if (ability == null) return null;

            var existing = FindSpec(ability);
            if (existing != null) return existing;

            var spec = new AbilitySpec(ability, level);
            _abilities.Add(spec);
            return spec;
        }

        /// <summary>
        /// Remove uma habilidade concedida, cancelando-a se estiver ativa.
        /// </summary>
        public void RemoveAbility(GameplayAbility ability)
        {
            var spec = FindSpec(ability);
            if (spec == null) return;

            CancelAbility(spec);
            _abilities.Remove(spec);
        }

        public AbilitySpec FindSpec(GameplayAbility ability)
        {
            if (ability == null) return null;
            for (int i = 0; i < _abilities.Count; i++)
            {
                if (_abilities[i].Ability == ability) return _abilities[i];
            }
            return null;
        }

        public bool IsAbilityActive(GameplayAbility ability)
        {
            var spec = FindSpec(ability);
            return spec != null && spec.IsActive;
        }
        #endregion

        #region Ability Activation
        public AbilityActivationResult GetActivationResult(GameplayAbility ability)
        {
            if (ability == null) return AbilityActivationResult.InvalidAbility;

            var spec = FindSpec(ability);
            if (spec == null) return AbilityActivationResult.NotGranted;
            if (spec.IsActive) return AbilityActivationResult.AlreadyActive;

            if (HasAnyTag(ability.BlockedTags)) return AbilityActivationResult.BlockedByTags;
            if (!HasAllTags(ability.RequiredTags)) return AbilityActivationResult.MissingRequiredTags;
            if (IsBlockedByActiveAbility(ability)) return AbilityActivationResult.BlockedByActiveAbility;
            if (IsOnCooldown(ability.CooldownEffect)) return AbilityActivationResult.OnCooldown;
            if (!CanAffordCost(ability.CostEffect)) return AbilityActivationResult.CannotAffordCost;

            return AbilityActivationResult.Success;
        }

        public AbilityActivationResult TryActivateAbility(GameplayAbility ability, Vector3 targetPos)
        {
            var result = GetActivationResult(ability);
            if (result != AbilityActivationResult.Success)
            {
                OnAbilityFailed?.Invoke(ability, result);
                return result;
            }

            ActivateSpec(FindSpec(ability), targetPos);
            return result;
        }

        public AbilityActivationResult TryActivateAbility(AbilitySpec spec, Vector3 targetPos)
        {
            if (spec == null || !_abilities.Contains(spec))
            {
                var result = spec == null ? AbilityActivationResult.InvalidAbility : AbilityActivationResult.NotGranted;
                OnAbilityFailed?.Invoke(spec?.Ability, result);
                return result;
            }
            return TryActivateAbility(spec.Ability, targetPos);
        }

        public bool IsOnCooldown(GameplayEffect cooldownEffect)
        {
            if (cooldownEffect == null) return false;
            if (HasAnyTag(cooldownEffect.GrantedTags)) return true;
            return GetCooldownTimeRemaining(cooldownEffect) > 0f;
        }

        /// <summary>
        /// Verifica se os modifiers Add negativos do custo deixariam algum atributo abaixo do MinValue.
        /// </summary>
        public bool CanAffordCost(GameplayEffect costEffect)
        {
            if (costEffect == null || costEffect.Modifiers == null) return true;

            foreach (var mod in costEffect.Modifiers)
            {
                if (mod.Attribute == null || mod.Operation != ModifierOperation.Add) continue;

                float value = mod.ValueSource.GetValue(this, this);
                if (value >= 0f) continue;

                if (attributeSet == null || !attributeSet.TryGetState(mod.Attribute, out var state)) return false;
                if (state.CurrentValue + value < mod.Attribute.MinValue) return false;
            }
            return true;
        }

        private bool IsBlockedByActiveAbility(GameplayAbility ability)
        {
            if (!ability.AbilityTag.IsValid) return false;

            foreach (var spec in _abilities)
            {
                if (!spec.IsActive || spec.Ability.BlockAbilitiesWithTag == null) continue;
                foreach (var tag in spec.Ability.BlockAbilitiesWithTag)
                {
                    if (ability.AbilityTag.Matches(tag)) return true;
                }
            }
            return false;
        }

        private void ActivateSpec(AbilitySpec spec, Vector3 targetPos)
        {
            var ability = spec.Ability;

            // Commit: cooldown e custo
            if (ability.CooldownEffect != null) ApplyEffect(ability.CooldownEffect, ability.GetCooldownDuration(this));
            if (ability.CostEffect != null) ApplyEffect(ability.CostEffect);

            CancelAbilitiesWithTags(ability.CancelAbilitiesWithTag, spec);

            var context = new AbilityContext(this, targetPos)
            {
                Ability = ability,
                Spec = spec,
                Level = spec.Level
            };

            spec.IsActive = true;
            spec.ActiveContext = context;
            AddTags(ability.ActivationOwnedTags);

            // Dispara eventos DEPOIS de aplicar o cooldown, para que a UI leia o tempo correto
            OnAbilityActivated?.Invoke(spec);

            // A tag concedida acima pode ter cancelado a própria habilidade
            if (!spec.IsActive || spec.ActiveContext != context) return;

            var routine = StartCoroutine(RunAbility(spec, context));
            // Se a habilidade terminou de forma síncrona, não guarda a rotina
            if (spec.IsActive && spec.ActiveContext == context) spec.Routine = routine;
        }

        private IEnumerator RunAbility(AbilitySpec spec, AbilityContext context)
        {
            yield return AbilityAction.RunSequence(spec.Ability.Actions, context);
            if (spec.IsActive && spec.ActiveContext == context) EndAbility(spec, false);
        }
        #endregion

        #region Ability Cancellation
        /// <summary>
        /// Interrompe a habilidade, chamando OnCancel nas ações em execução.
        /// </summary>
        public void CancelAbility(AbilitySpec spec)
        {
            if (spec == null || !spec.IsActive) return;

            var context = spec.ActiveContext;
            context.IsCancelled = true;

            if (spec.Routine != null) StopCoroutine(spec.Routine);
            foreach (var child in spec.ChildRoutines)
            {
                if (child != null) StopCoroutine(child);
            }

            // Ordem reversa: as ações mais internas restauram estado primeiro
            var running = context.RunningActions.ToArray();
            context.RunningActions.Clear();
            for (int i = running.Length - 1; i >= 0; i--)
            {
                try { running[i].OnCancel(context); }
                catch (Exception e) { Debug.LogException(e, this); }
            }

            EndAbility(spec, true);
        }

        public void CancelAbility(GameplayAbility ability) => CancelAbility(FindSpec(ability));

        /// <summary>
        /// Cancela habilidades ativas cujo AbilityTag corresponda a alguma das tags.
        /// </summary>
        public void CancelAbilitiesWithTags(IEnumerable<GameplayTag> tags, AbilitySpec ignore = null)
        {
            if (tags == null) return;

            for (int i = _abilities.Count - 1; i >= 0; i--)
            {
                if (i >= _abilities.Count) continue;
                var spec = _abilities[i];
                if (spec == ignore || !spec.IsActive || !spec.Ability.AbilityTag.IsValid) continue;

                foreach (var tag in tags)
                {
                    if (spec.Ability.AbilityTag.Matches(tag))
                    {
                        CancelAbility(spec);
                        break;
                    }
                }
            }
        }

        public void CancelAllAbilities()
        {
            for (int i = _abilities.Count - 1; i >= 0; i--)
            {
                if (i < _abilities.Count) CancelAbility(_abilities[i]);
            }
        }

        private void EndAbility(AbilitySpec spec, bool cancelled)
        {
            if (!spec.IsActive) return;

            spec.IsActive = false;
            spec.Routine = null;
            spec.ChildRoutines.Clear();
            spec.ActiveContext = null;

            RemoveTags(spec.Ability.ActivationOwnedTags);
            OnAbilityEnded?.Invoke(spec, cancelled);
        }

        private void HandleTagChangedForCancellation(GameplayTag tag, bool added)
        {
            if (!added) return;

            for (int i = _abilities.Count - 1; i >= 0; i--)
            {
                if (i >= _abilities.Count) continue;
                var spec = _abilities[i];
                if (!spec.IsActive || spec.Ability.CancelledByTags == null) continue;

                foreach (var cancelTag in spec.Ability.CancelledByTags)
                {
                    if (tag.Matches(cancelTag))
                    {
                        CancelAbility(spec);
                        break;
                    }
                }
            }
        }
        #endregion

        #region Tag Management
        public void AddTag(GameplayTag tag)
        {
            if (!tag.IsValid) return;
            _activeTags.TryGetValue(tag, out int count);
            _activeTags[tag] = count + 1;
            if (count == 0) OnTagChanged?.Invoke(tag, true);
        }

        public void RemoveTag(GameplayTag tag)
        {
            if (!tag.IsValid) return;
            if (!_activeTags.TryGetValue(tag, out int count)) return;

            if (count <= 1)
            {
                _activeTags.Remove(tag);
                OnTagChanged?.Invoke(tag, false);
            }
            else
            {
                _activeTags[tag] = count - 1;
            }
        }

        public void AddTags(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return;
            foreach (var tag in tags) AddTag(tag);
        }

        public void RemoveTags(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return;
            foreach (var tag in tags) RemoveTag(tag);
        }

        public bool HasTag(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            foreach (var activeTag in _activeTags.Keys)
            {
                if (activeTag.Matches(tag)) return true;
            }
            return false;
        }

        public bool HasAllTags(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return true;
            foreach (var tag in tags) if (!HasTag(tag)) return false;
            return true;
        }

        public bool HasAnyTag(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return false;
            foreach (var tag in tags) if (HasTag(tag)) return true;
            return false;
        }
        #endregion

        #region Gameplay Effects
        public ActiveEffectInstance ApplyEffect(GameplayEffect effect, AbilitySystemComponent source = null)
        {
            return ApplyEffect(effect, effect.Duration, source);
        }

        public ActiveEffectInstance ApplyEffect(GameplayEffect effect, float durationOverride, AbilitySystemComponent source = null)
        {
            if (effect == null) return null;
            if (source == null) source = this;

            if (!HasAllTags(effect.ApplicationRequirements)) return null;
            if (HasAnyTag(effect.ApplicationImmunityTags)) return null;

            if (effect.DurationPolicy == EffectDurationPolicy.Instant)
            {
                ApplyInstantEffect(effect, source);
                return null;
            }
            else
            {
                // Handle Stacking
                var existing = _activeEffects.Find(e => e.Effect == effect);
                if (existing != null && effect.CanStack)
                {
                    existing.AddStack();
                    return existing;
                }

                return ApplyDurationEffect(effect, durationOverride, source);
            }
        }

        public float GetCooldownTimeRemaining(GameplayEffect cooldownEffect)
        {
            if (cooldownEffect == null) return 0;

            float maxTime = 0;
            foreach (var instance in _activeEffects)
            {
                if (instance.Effect == cooldownEffect)
                {
                    maxTime = Mathf.Max(maxTime, instance.TimeRemaining);
                }
                else if (cooldownEffect.GrantedTags != null && instance.Effect.GrantedTags != null)
                {
                    // Check if any of the tags match
                    foreach (var tag in cooldownEffect.GrantedTags)
                    {
                        if (instance.Effect.GrantedTags.Contains(tag))
                        {
                            maxTime = Mathf.Max(maxTime, instance.TimeRemaining);
                        }
                    }
                }
            }
            return maxTime;
        }

        public float GetCooldownNormalized(GameplayEffect cooldownEffect)
        {
            if (cooldownEffect == null || cooldownEffect.Duration <= 0) return 0;
            float remaining = GetCooldownTimeRemaining(cooldownEffect);
            return Mathf.Clamp01(remaining / cooldownEffect.Duration);
        }

        private void ApplyInstantEffect(GameplayEffect effect, AbilitySystemComponent source)
        {
            // Trigger Application Actions
            if (effect.OnApplication != null)
            {
                foreach (var action in effect.OnApplication) action?.Execute(source, this);
            }

            ApplyModifiersToBase(effect, effect.Modifiers, source);
        }

        private ActiveEffectInstance ApplyDurationEffect(GameplayEffect effect, float duration, AbilitySystemComponent source)
        {
            var instance = new ActiveEffectInstance(effect, duration, source, this);
            _activeEffects.Add(instance);

            AddTags(effect.GrantedTags);

            if (effect.Modifiers != null)
            {
                foreach (var mod in effect.Modifiers)
                {
                    if (!IsModifierValid(effect, mod)) continue;
                    float value = mod.ValueSource.GetValue(source, this);
                    attributeSet.AddModifier(mod.Attribute, new AttributeModifier(value, mod.Operation, instance));
                }
            }

            if (effect.Period > 0)
            {
                instance.OnTick += HandleEffectTick;
            }

            if (effect.DurationPolicy == EffectDurationPolicy.HasDuration)
            {
                instance.OnEffectExpired += RemoveEffect;
            }
            return instance;
        }

        private void HandleEffectTick(ActiveEffectInstance instance)
        {
            // Periodic modifiers act as instant changes to the base value
            ApplyModifiersToBase(instance.Effect, instance.Effect.PeriodicModifiers, instance.Source);
        }

        /// <summary>
        /// Aplica modifiers diretamente no valor base (efeitos Instant e ticks periódicos).
        /// Add: base + v | Multiply: base * (1 + v) | Override: v
        /// </summary>
        private void ApplyModifiersToBase(GameplayEffect effect, List<AttributeEffectModifier> modifiers, AbilitySystemComponent source)
        {
            if (modifiers == null || attributeSet == null) return;

            foreach (var mod in modifiers)
            {
                if (!IsModifierValid(effect, mod)) continue;
                if (!attributeSet.TryGetState(mod.Attribute, out var state)) continue;

                float value = mod.ValueSource.GetValue(source, this);
                switch (mod.Operation)
                {
                    case ModifierOperation.Add:
                        state.SetBaseValue(state.BaseValue + value);
                        break;
                    case ModifierOperation.Multiply:
                        state.SetBaseValue(state.BaseValue * (1f + value));
                        break;
                    case ModifierOperation.Override:
                        state.SetBaseValue(value);
                        break;
                }
            }
        }

        private static bool IsModifierValid(GameplayEffect effect, AttributeEffectModifier mod)
        {
            if (mod.Attribute != null) return true;
            Debug.LogWarning($"[GAS] Modifier sem atributo ignorado no efeito '{(effect != null ? effect.name : "null")}'.", effect);
            return false;
        }

        public void RemoveEffect(ActiveEffectInstance instance)
        {
            instance.Cleanup();
            instance.OnEffectExpired -= RemoveEffect;
            instance.OnTick -= HandleEffectTick;
            _activeEffects.Remove(instance);

            RemoveTags(instance.Effect.GrantedTags);

            if (instance.Effect.Modifiers != null)
            {
                foreach (var mod in instance.Effect.Modifiers)
                {
                    if (attributeSet.TryGetState(mod.Attribute, out var state))
                    {
                        state.RemoveModifiersFromSource(instance);
                    }
                }
            }
        }
        #endregion
    }
}

