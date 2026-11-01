using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
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
    public class GameplayEffect : ScriptableObject, ISerializationCallbackReceiver, IValidatable
    {
        [Header("Identidade")]
        [Tooltip("Nome exibido do efeito (lista de buffs, tooltip). Vazio por padrão.")]
        [SerializeField] private string displayName;

        [Tooltip("Ícone do efeito. Opcional.")]
        [SerializeField] private Sprite icon;

        /// <summary>Nome exibido; vazio por padrão, nunca nulo.</summary>
        public string DisplayName => displayName ?? string.Empty;

        /// <summary>Ícone do efeito; nulo por padrão.</summary>
        public Sprite Icon => icon;

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

        [Tooltip("Nível com que o efeito é avaliado quando aplicado sem habilidade (efeitos de partida, ApplyEffect direto).")]
        [SerializeField, Min(1)] private int defaultLevel = 1;

        /// <summary>Nível da aplicação sem habilidade (pelo menos 1). Sem habilidade não há tabela: o modo Table vale o Fallback.</summary>
        public int DefaultLevel => defaultLevel < 1 ? 1 : defaultLevel;

        [Header("Periodic")]
        [Tooltip("If Period > 0, effect triggers every Period seconds")]
        public float Period = 0f;

        [Header("Instant & Persistent Modifiers")]
        public List<AttributeEffectModifier> Modifiers;

        [Header("Periodic Modifiers (Tick)")]
        [Tooltip("Usado só quando Period > 0")]
        public List<AttributeEffectModifier> PeriodicModifiers;

        [Header("Metadados")]
        [Tooltip("Efeito benéfico para o alvo. Pode coexistir com Is Debuff, com aviso do validador.")]
        [SerializeField] private bool isBuff;

        [Tooltip("Efeito prejudicial para o alvo.")]
        [SerializeField] private bool isDebuff;

        [Tooltip("Não aparece em listas de efeitos da UI.")]
        [SerializeField] private bool isHidden;

        [Tooltip("Pode ser removido por uma ação de purga.")]
        [SerializeField] private bool isPurgable;

        public bool IsBuff => isBuff;
        public bool IsDebuff => isDebuff;
        public bool IsHidden => isHidden;
        public bool IsPurgable => isPurgable;

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

        [Tooltip("Com N pilhas, cada modificador (persistente e do tique) vale N vezes o valor de uma pilha. Desligue quando a pilha só conta.")]
        [SerializeField] private bool stacksMultiplyMagnitude = true;

        /// <summary>
        /// Pilhas multiplicam o valor de cada modificador antes da fórmula (ADR 0029, item 2). Ligado por padrão, também em
        /// assets antigos sem o campo.
        /// </summary>
        public bool StacksMultiplyMagnitude
        {
            get => stacksMultiplyMagnitude;
            set => stacksMultiplyMagnitude = value;
        }

        public void Validate(ValidationResult result)
        {
            if (isBuff && isDebuff)
            {
                result.AddWarning("Efeito marcado como Is Buff e Is Debuff ao mesmo tempo.", nameof(isDebuff));
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (float.IsNaN(legacyDuration)) return;
            duration = ScalableFloat.Constant(legacyDuration);
            legacyDuration = float.NaN;
        }
    }
}

