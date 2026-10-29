using System;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Tags;
using SpellBook.GAS.Attributes;

namespace SpellBook.GAS.Effects
{
    public enum EffectDurationPolicy
    {
        Instant,
        Infinite,
        HasDuration
    }

    [Serializable]
    public struct AttributeEffectModifier
    {
        public AttributeDefinition Attribute;
        public ModifierOperation Operation;
        public ModifierValueSource ValueSource;
    }

    [CreateAssetMenu(fileName = "GE_NewEffect", menuName = "GAS/Gameplay Effect")]
    public class GameplayEffect : ScriptableObject
    {
        [Header("Duration")]
        public EffectDurationPolicy DurationPolicy;
        
        public float Duration;

        [Header("Periodic")]
        [Tooltip("If Period > 0, effect triggers every Period seconds")]
        public float Period = 0f;

        [Header("Instant & Persistent Modifiers")]
        public List<AttributeEffectModifier> Modifiers;

        [Header("Periodic Modifiers (Tick)")]
        [Tooltip("Usado só quando Period > 0")]
        public List<AttributeEffectModifier> PeriodicModifiers;

        [Header("Actions")]
        [SerializeReference]
        public List<EffectAction> OnApplication = new List<EffectAction>();

        [SerializeReference]
        [Tooltip("Usado só quando Period > 0")]
        public List<EffectAction> OnTickActions = new List<EffectAction>();

        [SerializeReference]
        public List<EffectAction> OnRemoval = new List<EffectAction>();

        [Header("Gameplay Tags")]
        public List<GameplayTag> GrantedTags; 
        public List<GameplayTag> ApplicationRequirements;
        public List<GameplayTag> ApplicationImmunityTags;

        [Header("Stacking Logic")]
        public bool CanStack;
        public int MaxStacks = 1;
        public bool RefreshDurationOnStack = true;
    }
}

