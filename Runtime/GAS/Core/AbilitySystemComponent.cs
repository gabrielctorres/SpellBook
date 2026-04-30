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

        private readonly HashSet<GameplayTagDefinition> _activeTags = new HashSet<GameplayTagDefinition>();
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
            if (_activeTags.Add(def)) OnTagChanged?.Invoke(tag, true);
        }

        public void RemoveTag(GameplayTag tag)
        {
            if (!tag.IsValid) return;
            var def = tag.GetDefinition();
            if (_activeTags.Remove(def)) OnTagChanged?.Invoke(tag, false);
        }

        public bool HasTag(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            var defToCheck = tag.GetDefinition();
            foreach (var activeDef in _activeTags)
            {
                if (activeDef.Matches(defToCheck)) return true;
            }
            return false;
        }

        public bool HasAllTags(IEnumerable<GameplayTag> tags)
        {
            foreach (var tag in tags) if (!HasTag(tag)) return false;
            return true;
        }

        public bool HasAnyTag(IEnumerable<GameplayTag> tags)
        {
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
            if (source == null) source = this;

            if (effect.ApplicationRequirements.Count > 0 && !HasAllTags(effect.ApplicationRequirements)) return null;
            if (effect.ApplicationImmunityTags.Count > 0 && HasAnyTag(effect.ApplicationImmunityTags)) return null;

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
                else
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

            foreach (var mod in effect.Modifiers)
            {
                if (attributeSet.TryGetState(mod.Attribute, out var state))
                {
                    float value = mod.ValueSource.GetValue(source, this);
                    if (mod.Operation == ModifierOperation.Add)
                        state.SetBaseValue(state.BaseValue + value);
                }
            }
        }

        private ActiveEffectInstance ApplyDurationEffect(GameplayEffect effect, float duration, AbilitySystemComponent source)
        {
            var instance = new ActiveEffectInstance(effect, duration, source, this);
            _activeEffects.Add(instance);

            foreach (var tag in effect.GrantedTags) AddTag(tag);

            foreach (var mod in effect.Modifiers)
            {
                float value = mod.ValueSource.GetValue(source, this);
                attributeSet.AddModifier(mod.Attribute, new AttributeModifier(value, mod.Operation, instance));
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
            foreach (var mod in instance.Effect.PeriodicModifiers)
            {
                if (attributeSet.TryGetState(mod.Attribute, out var state))
                {
                    float value = mod.ValueSource.GetValue(instance.Source, this);
                    // Periodic modifiers act as instant changes to the base value
                    if (mod.Operation == ModifierOperation.Add)
                        state.SetBaseValue(state.BaseValue + value);
                }
            }
        }

        public void RemoveEffect(ActiveEffectInstance instance)
        {
            instance.Cleanup();
            instance.OnEffectExpired -= RemoveEffect;
            instance.OnTick -= HandleEffectTick;
            _activeEffects.Remove(instance);

            foreach (var tag in instance.Effect.GrantedTags) RemoveTag(tag);

            foreach (var mod in instance.Effect.Modifiers)
            {
                if (attributeSet.TryGetState(mod.Attribute, out var state))
                {
                    state.RemoveModifiersFromSource(instance);
                }
            }
        }
        #endregion
    }
}

