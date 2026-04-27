using System;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Tags;
using SpellBook.GAS.Attributes;
using Sirenix.OdinInspector;

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
        [HorizontalGroup("Mod")]
        [HideLabel]
        public AttributeDefinition Attribute;
        [HorizontalGroup("Mod")]
        [HideLabel]
        public ModifierOperation Operation;
        [HorizontalGroup("Mod")]
        [HideLabel]
        public ModifierValueSource ValueSource;
    }

    [CreateAssetMenu(fileName = "GE_NewEffect", menuName = "GAS/Gameplay Effect")]
    public class GameplayEffect : ScriptableObject
    {
        [TabGroup("EffectConfig", "Main Settings")]
        [BoxGroup("EffectConfig/Main Settings/Duration")]
        [EnumPaging]
        public EffectDurationPolicy DurationPolicy;
        
        [BoxGroup("EffectConfig/Main Settings/Duration")]
        [ShowIf("DurationPolicy", EffectDurationPolicy.HasDuration)]
        public float Duration;

        [BoxGroup("EffectConfig/Main Settings/Periodic")]
        [InfoBox("If Period > 0, effect triggers every Period seconds", InfoMessageType.None)]
        public float Period = 0f;

        [TabGroup("EffectConfig", "Modifiers")]
        [Title("Instant & Persistent Modifiers")]
        [ListDrawerSettings(ShowIndexLabels = true)]
        public List<AttributeEffectModifier> Modifiers;

        [TabGroup("EffectConfig", "Modifiers")]
        [Title("Periodic Modifiers (Tick)")]
        [ListDrawerSettings(ShowIndexLabels = true)]
        [ShowIf("@Period > 0")]
        public List<AttributeEffectModifier> PeriodicModifiers;

        [TabGroup("EffectConfig", "Actions")]
        [SerializeReference]
        [ListDrawerSettings(Expanded = true)]
        public List<EffectAction> OnApplication = new List<EffectAction>();

        [TabGroup("EffectConfig", "Actions")]
        [SerializeReference]
        [ListDrawerSettings(Expanded = true)]
        [ShowIf("@Period > 0")]
        public List<EffectAction> OnTickActions = new List<EffectAction>();

        [TabGroup("EffectConfig", "Actions")]
        [SerializeReference]
        [ListDrawerSettings(Expanded = true)]
        public List<EffectAction> OnRemoval = new List<EffectAction>();

        [TabGroup("EffectConfig", "Tags & Stacking")]
        [Title("Gameplay Tags")]
        public List<GameplayTag> GrantedTags; 
        public List<GameplayTag> ApplicationRequirements;
        public List<GameplayTag> ApplicationImmunityTags;

        [TabGroup("EffectConfig", "Tags & Stacking")]
        [Title("Stacking Logic")]
        [ToggleGroup("CanStack")]
        public bool CanStack;
        [ToggleGroup("CanStack")]
        public int MaxStacks = 1;
        [ToggleGroup("CanStack")]
        public bool RefreshDurationOnStack = true;
    }
}

