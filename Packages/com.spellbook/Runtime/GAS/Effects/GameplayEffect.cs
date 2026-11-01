using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using UnityEngine;
using UnityEngine.Serialization;
using SpellBook.Tags;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Values;

namespace SpellBook.GAS.Effects
{
    public enum EffectDurationPolicy
    {
        Instant,
        Infinite,
        HasDuration
    }

    /// <summary>Um modificador do efeito. É classe: entradas nulas nas listas do efeito são ignoradas.</summary>
    [Serializable]
    public class AttributeEffectModifier
    {
        public AttributeDefinition Attribute;
        public ModifierOperation Operation;
        public ModifierValueSource ValueSource = new ModifierValueSource();
    }

    [CreateAssetMenu(fileName = "GE_NewEffect", menuName = "GAS/Gameplay Effect")]
    [HelpURL(DocUrls.Gas + "#gameplayeffect")]
    public class GameplayEffect : ScriptableObject, ISerializationCallbackReceiver
    {
        [Header("Duration")]
        public EffectDurationPolicy DurationPolicy;
        
        [ShowIf(nameof(DurationPolicy), EffectDurationPolicy.HasDuration)]
        [SerializeField] private ScalableFloat duration = ScalableFloat.Constant(0f);

        // Só leitura do formato 2.x ("Duration: 4"); convertido em OnAfterDeserialize (ADR 0009). Sai antes da 3.0 final.
        [FormerlySerializedAs("Duration")]
        [SerializeField, HideInInspector] private float legacyDuration = float.NaN;

        /// <summary>Duração em segundos por nível (HasDuration). Sem habilidade, vale o nível 1.</summary>
        public ScalableFloat Duration
        {
            get => duration;
            set => duration = value;
        }

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
        [TypePicker]
        public List<EffectAction> OnApplication = new List<EffectAction>();

        [SerializeReference]
        [TypePicker]
        [Tooltip("Usado só quando Period > 0")]
        public List<EffectAction> OnTickActions = new List<EffectAction>();

        [SerializeReference]
        [TypePicker]
        public List<EffectAction> OnRemoval = new List<EffectAction>();

        [Header("Gameplay Tags")]
        public List<GameplayTag> GrantedTags; 
        public List<GameplayTag> ApplicationRequirements;
        public List<GameplayTag> ApplicationImmunityTags;

        [Header("Stacking Logic")]
        public bool CanStack;
        public int MaxStacks = 1;
        public bool RefreshDurationOnStack = true;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (float.IsNaN(legacyDuration)) return;
            duration = ScalableFloat.Constant(legacyDuration);
            legacyDuration = float.NaN;
        }
    }
}

