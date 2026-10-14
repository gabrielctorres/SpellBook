using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Tags;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Abilities;
namespace SpellBook.GAS.Core
{
    [RequireComponent(typeof(AttributeSet))]
    public class AbilitySystemComponent : MonoBehaviour
    {
        [SerializeField] private AttributeSet attributeSet;

        // Contagem de referência: várias fontes podem conceder a mesma tag
        private readonly Dictionary<GameplayTagDefinition, int> _activeTags = new Dictionary<GameplayTagDefinition, int>();
        private readonly List<ActiveEffectInstance> _activeEffects = new List<ActiveEffectInstance>();

        public event Action<GameplayTag, bool> OnTagChanged;
        public AttributeSet AttributeSet => attributeSet;

        private void Awake()
        {
            if (attributeSet == null) attributeSet = GetComponent<AttributeSet>();
        }

        private void Update()
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                _activeEffects[i].Update(Time.deltaTime);
            }
        }

        #region Ability Activation
        public void TryActivateAbility(GameplayAbility ability, Vector3 targetPos)
        {
            if (ability.CanActivate(this))
            {
                StartCoroutine(ability.Activate(this, targetPos));
            }
        }

        public float GetAttributeValue(AttributeDefinition def)
        {
            return attributeSet != null ? attributeSet.GetValue(def) : 0f;
        }
        #endregion

        #region Tag Management
        public void AddTag(GameplayTag tag)
        {
            if (!tag.IsValid) return;
            var def = tag.GetDefinition();
            _activeTags.TryGetValue(def, out int count);
            _activeTags[def] = count + 1;
            if (count == 0) OnTagChanged?.Invoke(tag, true);
        }

        public void RemoveTag(GameplayTag tag)
        {
            if (!tag.IsValid) return;
            var def = tag.GetDefinition();
            if (!_activeTags.TryGetValue(def, out int count)) return;

            if (count <= 1)
            {
                _activeTags.Remove(def);
                OnTagChanged?.Invoke(tag, false);
            }
            else
            {
                _activeTags[def] = count - 1;
            }
        }

        public bool HasTag(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            var defToCheck = tag.GetDefinition();
            foreach (var activeDef in _activeTags.Keys)
            {
                if (activeDef.Matches(defToCheck)) return true;
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

            if (effect.GrantedTags != null)
            {
                foreach (var tag in effect.GrantedTags) AddTag(tag);
            }

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

            if (instance.Effect.GrantedTags != null)
            {
                foreach (var tag in instance.Effect.GrantedTags) RemoveTag(tag);
            }

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

